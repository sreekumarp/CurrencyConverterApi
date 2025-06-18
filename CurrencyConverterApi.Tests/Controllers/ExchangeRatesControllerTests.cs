using System;
using System.Threading.Tasks;
using CurrencyConverterApi.Controllers;
using CurrencyConverterApi.Models;
using CurrencyConverterApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CurrencyConverterApi.Tests.Controllers
{
    public class ExchangeRatesControllerTests
    {
        private readonly Mock<IExchangeRateService> _mockExchangeRateService;
        private readonly Mock<ILogger<ExchangeRatesController>> _mockLogger;
        private readonly ExchangeRatesController _controller;

        public ExchangeRatesControllerTests()
        {
            _mockExchangeRateService = new Mock<IExchangeRateService>();
            _mockLogger = new Mock<ILogger<ExchangeRatesController>>();
            _controller = new ExchangeRatesController(
                _mockExchangeRateService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task GetLatestRates_ValidCurrency_ReturnsOkResult()
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

            _mockExchangeRateService
                .Setup(x => x.GetLatestRatesAsync(It.IsAny<string>()))
                .ReturnsAsync(expectedRates);

            // Act
            var result = await _controller.GetLatestRates("EUR");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<ExchangeRate>(okResult.Value);
            Assert.Equal(expectedRates.Base, returnValue.Base);
            Assert.Equal(expectedRates.Rates["USD"], returnValue.Rates["USD"]);
        }

        [Fact]
        public async Task GetLatestRates_ServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            _mockExchangeRateService
                .Setup(x => x.GetLatestRatesAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.GetLatestRates("EUR");

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task ConvertCurrency_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new ConversionRequest
            {
                FromCurrency = "EUR",
                ToCurrency = "USD",
                Amount = 100m
            };

            var expectedResponse = new ConversionResponse
            {
                FromCurrency = "EUR",
                ToCurrency = "USD",
                Amount = 100m,
                ConvertedAmount = 110m,
                Rate = 1.1m,
                Date = DateTime.UtcNow
            };

            _mockExchangeRateService
                .Setup(x => x.ConvertCurrencyAsync(It.IsAny<ConversionRequest>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.ConvertCurrency(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<ConversionResponse>(okResult.Value);
            Assert.Equal(expectedResponse.FromCurrency, returnValue.FromCurrency);
            Assert.Equal(expectedResponse.ToCurrency, returnValue.ToCurrency);
            Assert.Equal(expectedResponse.ConvertedAmount, returnValue.ConvertedAmount);
        }

        [Fact]
        public async Task ConvertCurrency_InvalidRequest_ReturnsBadRequest()
        {
            // Arrange
            var request = new ConversionRequest
            {
                FromCurrency = "TRY",
                ToCurrency = "USD",
                Amount = 100m
            };

            _mockExchangeRateService
                .Setup(x => x.ConvertCurrencyAsync(It.IsAny<ConversionRequest>()))
                .ThrowsAsync(new ArgumentException("Invalid currency"));

            // Act
            var result = await _controller.ConvertCurrency(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Invalid currency", badRequestResult.Value);
        }

        [Fact]
        public async Task GetHistoricalRates_ValidRequest_ReturnsOkResult()
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

            var expectedResponse = new PaginatedResponse<HistoricalExchangeRate>
            {
                Items = new[]
                {
                    new HistoricalExchangeRate
                    {
                        Date = DateTime.UtcNow.AddDays(-1),
                        Rates = new CurrencyDictionary { { "USD", 1.1m } }
                    }
                },
                Page = 1,
                PageSize = 10,
                TotalItems = 1,
                TotalPages = 1
            };

            _mockExchangeRateService
                .Setup(x => x.GetHistoricalRatesAsync(It.IsAny<HistoricalRatesRequest>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetHistoricalRates(
                request.BaseCurrency,
                request.StartDate,
                request.EndDate,
                request.Page,
                request.PageSize);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<PaginatedResponse<HistoricalExchangeRate>>(okResult.Value);
            Assert.Equal(expectedResponse.Page, returnValue.Page);
            Assert.Equal(expectedResponse.PageSize, returnValue.PageSize);
            Assert.Single(returnValue.Items);
        }
    }
} 