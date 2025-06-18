using System;
using System.Threading.Tasks;
using CurrencyConverterApi.Models;

namespace CurrencyConverterApi.Services
{
    public interface IExchangeRateService
    {
        Task<ExchangeRate> GetLatestRatesAsync(string baseCurrency);
        Task<ConversionResponse> ConvertCurrencyAsync(ConversionRequest request);
        Task<PaginatedResponse<HistoricalExchangeRate>> GetHistoricalRatesAsync(HistoricalRatesRequest request);
    }
} 