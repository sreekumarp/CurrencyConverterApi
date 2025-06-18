using CurrencyConverterApi.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CurrencyConverterApi.Services
{
    public class FrankfurterExchangeRateService : IExchangeRateService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<FrankfurterExchangeRateService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly AsyncCircuitBreakerPolicy<HttpResponseMessage> _circuitBreakerPolicy;
        private const string BaseUrl = "https://api.frankfurter.app";
        private static readonly string[] RestrictedCurrencies = { "TRY", "PLN", "THB", "MXN" };
        private readonly IDistributedCache _distributedCache;

        public FrankfurterExchangeRateService(
            HttpClient httpClient,
            IMemoryCache cache,
            ILogger<FrankfurterExchangeRateService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;

            _retryPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .WaitAndRetryAsync(3, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            _circuitBreakerPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 2,
                    durationOfBreak: TimeSpan.FromSeconds(30)
                );
        }

        public async Task<ExchangeRate> GetLatestRatesAsync(string baseCurrency)
        {
            var cacheKey = $"latest_rates_{baseCurrency}";
            
            if (_cache.TryGetValue(cacheKey, out ExchangeRate cachedRates))
            {
                return cachedRates;
            }

            var response = await _retryPolicy
                .WrapAsync(_circuitBreakerPolicy)
                .ExecuteAsync(async () => await _httpClient.GetAsync($"{BaseUrl}/latest?from={baseCurrency}"));

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var rates = JsonSerializer.Deserialize<ExchangeRate>(content);

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                //Can implement background task to clear cache.

            _cache.Set(cacheKey, rates, cacheOptions);

            return rates;
        }

        public async Task<ConversionResponse> ConvertCurrencyAsync(ConversionRequest request)
        {
            ValidateCurrencies(request.FromCurrency, request.ToCurrency);

            var rates = await GetLatestRatesAsync(request.FromCurrency);
            if (!rates.Rates.TryGetValue(request.ToCurrency, out decimal rate))
            {
                throw new ArgumentException($"Currency {request.ToCurrency} not supported");
            }

            return new ConversionResponse
            {
                FromCurrency = request.FromCurrency,
                ToCurrency = request.ToCurrency,
                Amount = request.Amount,
                ConvertedAmount = request.Amount * rate,
                Rate = rate,
                Date = rates.Date
            };
        }
        public async Task<PaginatedResponse<HistoricalExchangeRate>> GetHistoricalRatesAsync(HistoricalRatesRequest request)
        {
            var cacheKey = $"historical_rates_{request.BaseCurrency}";

            Dictionary<DateTime, CurrencyDictionary> ratesFromCache = await GetOrCreateCacheEntryAsync(
                cacheKey,
                request.BaseCurrency,
                request.StartDate,
                request.EndDate);

            // Filter and sort rates for requested date range
            var filteredRates = ratesFromCache
                .Where(r => r.Key >= request.StartDate && r.Key <= request.EndDate)
                .OrderBy(r => r.Key)
                .Select(r => new HistoricalExchangeRate
                {
                    Date = r.Key,
                    Rates = r.Value
                })
                .ToArray();

            return CreatePaginatedResponse(filteredRates, request.Page, request.PageSize);
        }

        private async Task<Dictionary<DateTime, CurrencyDictionary>> GetOrCreateCacheEntryAsync(
            string cacheKey,
            string baseCurrency,
            DateTime startDate,
            DateTime endDate)
        {
            // Try to get from cache first
            if (_cache.TryGetValue(cacheKey, out Dictionary<DateTime, CurrencyDictionary>? cachedRates))
            {
                _logger.LogInformation("Cache hit for {BaseCurrency}", baseCurrency);
                if (cachedRates != null && HasRequiredDateRange(cachedRates, startDate, endDate))
                {
                    return cachedRates;
                }
            }

            cachedRates ??= [];

            try
            {
                var missingDateRanges = GetMissingDateRanges(cachedRates, startDate, endDate);
                foreach (var (rangeStart, rangeEnd) in missingDateRanges)
                {
                    var newRates = await FetchHistoricalRatesAsync(baseCurrency, rangeStart, rangeEnd);
                    foreach (var rate in newRates)
                    {
                        cachedRates[rate.Key] = rate.Value;
                    }
                }

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromHours(1))
                    .RegisterPostEvictionCallback((key, value, reason, state) =>
                    {
                        _logger.LogInformation("Cache entry {Key} evicted: {Reason}", key, reason);
                    });

                _cache.Set(cacheKey, cachedRates, cacheOptions);
                return cachedRates;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching historical rates for {BaseCurrency}", baseCurrency);
                throw;
            }
        }

        private bool HasRequiredDateRange(Dictionary<DateTime, CurrencyDictionary> rates, DateTime startDate, DateTime endDate)
        {
            var datesNeeded = Enumerable.Range(0, (endDate - startDate).Days + 1)
                .Select(offset => startDate.AddDays(offset));
            return !datesNeeded.Any(date => !rates.ContainsKey(date));
        }

        private IEnumerable<(DateTime Start, DateTime End)> GetMissingDateRanges(
            Dictionary<DateTime, CurrencyDictionary> existingRates,
            DateTime startDate,
            DateTime endDate)
        {
            var missingRanges = new List<(DateTime Start, DateTime End)>();
            var currentStart = startDate;

            while (currentStart <= endDate)
            {
                // Find next missing date
                while (currentStart <= endDate && existingRates.ContainsKey(currentStart))
                {
                    currentStart = currentStart.AddDays(1);
                }

                if (currentStart > endDate) break;

                var rangeStart = currentStart;

                // Find end of missing range
                while (currentStart <= endDate && !existingRates.ContainsKey(currentStart))
                {
                    currentStart = currentStart.AddDays(1);
                }

                missingRanges.Add((rangeStart, currentStart.AddDays(-1)));
            }

            return missingRanges;
        }

        private async Task<Dictionary<DateTime, CurrencyDictionary>> FetchHistoricalRatesAsync(
            string baseCurrency,
            DateTime startDate,
            DateTime endDate)
        {

            var response = await _retryPolicy
                 .WrapAsync(_circuitBreakerPolicy)
                 .ExecuteAsync(async () =>
                    await _httpClient.GetAsync($"{BaseUrl}/{startDate:yyyy-MM-dd}..{endDate:yyyy-MM-dd}?from={baseCurrency}"));

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var rates = JsonSerializer.Deserialize<HistoricalExchangeRateResponse>(content);
            return rates.Rates;
        }

        private void ValidateCurrencies(string fromCurrency, string toCurrency)
        {
            if (Array.Exists(RestrictedCurrencies, c => c == fromCurrency || c == toCurrency))
            {
                throw new ArgumentException("Conversion involving TRY, PLN, THB, or MXN is not allowed");
            }
        }

        private PaginatedResponse<HistoricalExchangeRate> CreatePaginatedResponse(HistoricalExchangeRate[] rates, int page, int pageSize)
        {
            var totalItems = rates.Length;
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            var items = rates.Skip((page - 1) * pageSize).Take(pageSize).ToArray();

            return new PaginatedResponse<HistoricalExchangeRate>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages
            };
        }
    }
} 