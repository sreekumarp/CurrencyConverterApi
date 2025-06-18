using System;
using System.Threading.Tasks;
using CurrencyConverterApi.Models;
using CurrencyConverterApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CurrencyConverterApi.Controllers
{
    [ApiController]
    [Route("api/v1/exchange-rates")]
    public class ExchangeRatesController : ControllerBase
    {
        private readonly IExchangeRateService _exchangeRateService;
        private readonly ILogger<ExchangeRatesController> _logger;

        public ExchangeRatesController(
            IExchangeRateService exchangeRateService,
            ILogger<ExchangeRatesController> logger)
        {
            _exchangeRateService = exchangeRateService;
            _logger = logger;
        }

        [Authorize(Roles = "Admin,User")]
        [HttpGet("latest")]
        public async Task<ActionResult<ExchangeRate>> GetLatestRates([FromQuery] string baseCurrency = "EUR")
        {
            try
            {
                var rates = await _exchangeRateService.GetLatestRatesAsync(baseCurrency);
                return Ok(rates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving latest exchange rates for base currency {BaseCurrency}", baseCurrency);
                return StatusCode(500, "An error occurred while retrieving exchange rates");
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("convert")]
        public async Task<ActionResult<ConversionResponse>> ConvertCurrency([FromBody] ConversionRequest request)
        {
            try
            {
                var result = await _exchangeRateService.ConvertCurrencyAsync(request);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid currency conversion request: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting currency from {FromCurrency} to {ToCurrency}", 
                    request.FromCurrency, request.ToCurrency);
                return StatusCode(500, "An error occurred while converting currency");
            }
        }

        [Authorize(Roles = "Admin,User,ReadOnly")]
        [HttpGet("historical")]
        public async Task<ActionResult<PaginatedResponse<HistoricalExchangeRate>>> GetHistoricalRates(
            [FromQuery] string baseCurrency = "EUR",
            [FromQuery] DateTime startDate = default,
            [FromQuery] DateTime endDate = default,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                if (startDate == default)
                    startDate = DateTime.UtcNow.AddDays(-30);
                if (endDate == default)
                    endDate = DateTime.UtcNow;

                var request = new HistoricalRatesRequest
                {
                    BaseCurrency = baseCurrency,
                    StartDate = startDate,
                    EndDate = endDate,
                    Page = page,
                    PageSize = pageSize
                };

                var result = await _exchangeRateService.GetHistoricalRatesAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving historical exchange rates for base currency {BaseCurrency}", baseCurrency);
                return StatusCode(500, "An error occurred while retrieving historical exchange rates");
            }
        }
    }
} 