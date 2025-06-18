using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CurrencyConverterApi.Models;
using CurrencyConverterApi.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace CurrencyConverterApi.Tests.Services
{
    public class FrankfurterExchangeRateServiceTests
    {
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly Mock<ILogger<FrankfurterExchangeRateService>> _mockLogger;
        private readonly IMemoryCache _memoryCache;
        private readonly FrankfurterExchangeRateService _service;
        private readonly HttpClient _httpClient;

        public FrankfurterExchangeRateServiceTests()
        {
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _mockLogger = new Mock<ILogger<FrankfurterExchangeRateService>>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _service = new FrankfurterExchangeRateService(
                _httpClient,
                _memoryCache,
                _mockLogger.Object);
        }

        [Fact]
        public async Task GetLatestRatesAsync_ValidCurrency_ReturnsExchangeRate()
        {
            // Arrange
            var expectedRates = new ExchangeRate
            {
                Base = "EUR",
                Date = DateTime.UtcNow,
                Rates = new CurrencyDictionary
                {
                    { "USD", 1.1m },
                    { "GBP", 0.85m }
                }
            };

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedRates))
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(response);

            // Act
            var result = await _service.GetLatestRatesAsync("EUR");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedRates.Base, result.Base);
            Assert.Equal(expectedRates.Rates["USD"], result.Rates["USD"]);
            Assert.Equal(expectedRates.Rates["GBP"], result.Rates["GBP"]);
        }

        [Fact]
        public async Task ConvertCurrencyAsync_ValidRequest_ReturnsConversionResponse()
        {
            // Arrange
            var request = new ConversionRequest
            {
                FromCurrency = "EUR",
                ToCurrency = "USD",
                Amount = 100m
            };

            var exchangeRate = new ExchangeRate
            {
                Base = "EUR",
                Date = DateTime.UtcNow,
                Rates = new CurrencyDictionary
                {
                    { "USD", 1.1m }
                }
            };

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(exchangeRate))
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(response);

            // Act
            var result = await _service.ConvertCurrencyAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(request.FromCurrency, result.FromCurrency);
            Assert.Equal(request.ToCurrency, result.ToCurrency);
            Assert.Equal(request.Amount, result.Amount);
            Assert.Equal(110m, result.ConvertedAmount);
            Assert.Equal(1.1m, result.Rate);
        }

        [Fact]
        public async Task ConvertCurrencyAsync_RestrictedCurrency_ThrowsArgumentException()
        {
            // Arrange
            var request = new ConversionRequest
            {
                FromCurrency = "TRY",
                ToCurrency = "USD",
                Amount = 100m
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.ConvertCurrencyAsync(request));
        }

        [Fact]
        public async Task GetHistoricalRatesAsync_ValidRequest_ReturnsPaginatedResponse()
        {
            // Arrange
            var request = new HistoricalRatesRequest
            {
                BaseCurrency = "EUR",
                StartDate = DateTime.UtcNow.AddDays(-7),
                EndDate = DateTime.UtcNow,
                Page = 1,
                PageSize = 10
            };

            var historicalRates = new HistoricalExchangeRateResponse
            {
                Base = "EUR",
                Date = DateTime.UtcNow,
                Rates = new Dictionary<DateTime, CurrencyDictionary>
                {
                    {
                        DateTime.UtcNow.AddDays(-1),
                        new CurrencyDictionary { { "USD", 1.1m } }
                    }
                }
            };

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(historicalRates))
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(response);

            // Act
            var result = await _service.GetHistoricalRatesAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Page);
            Assert.Equal(10, result.PageSize);
            Assert.NotNull(result.Items);
        }

        [Fact]
        public async Task GetLatestRatesAsync_HttpError_ThrowsException()
        {
            // Arrange
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(response);

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => 
                _service.GetLatestRatesAsync("EUR"));
        }
    }
} 