using System.Text.Json.Serialization;

namespace CurrencyConverterApi.Models
{

    public class CurrencyDictionary : Dictionary<string, decimal>
    {
        public CurrencyDictionary() : base() { }

        public CurrencyDictionary(IDictionary<string, decimal> dictionary) : base(dictionary) { }

        public CurrencyDictionary(int capacity) : base(capacity) { }
    }

    public class BaseExchangeRate
    {
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("base")]
        public required string Base { get; set; }

        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

    }

    public class ExchangeRate : BaseExchangeRate
    {
        [JsonPropertyName("rates")]
        public required CurrencyDictionary Rates { get; set; }
    }

    public class HistoricalExchangeRateResponse : BaseExchangeRate
    {
        [JsonPropertyName("rates")]
        public required Dictionary<DateTime, CurrencyDictionary> Rates { get; set; }
    }

    public class ConversionRequest
    {
        public required string FromCurrency { get; set; }
        public required string ToCurrency { get; set; }
        public decimal Amount { get; set; }
    }

    public class ConversionResponse
    {
        public required string FromCurrency { get; set; }
        public required string ToCurrency { get; set; }
        public decimal Amount { get; set; }
        public decimal ConvertedAmount { get; set; }
        public decimal Rate { get; set; }
        public DateTime Date { get; set; }
    }

    public class HistoricalRatesRequest
    {
        public required string BaseCurrency { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class PaginatedResponse<T>
    {
        public required IEnumerable<T> Items { get; set; }
        public int TotalItems { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }
    }
}