namespace CurrencyConverterApi.Models
{
    public class HistoricalExchangeRate
    {
        public DateTime Date { get; set; }
        public CurrencyDictionary Rates { get; set; }
    }
}