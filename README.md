# Currency Converter API

currency conversion api.

## Setup Instructions

1. Clone the repository
2. Update the `appsettings.json` with your configuration
3. Run the following commands:
   ```bash
   dotnet restore
   dotnet build
   dotnet run --project Api --launch-profile https 
   ```
  - Swagger: https://localhost:7116

  - Api: http://localhost:5008

## Unit Test
```dotnet test CurrencyConverterApi.Tests/CurrencyConverterApi.Tests.csproj```

## API Endpoints

Authorize first using Swagger then taken the token.

Admin User: 
```{ "username": "admin", "password": "adminpass" }```

Pass above to the `/auth/login` end point to get token.

Apply this ```Bearer <token>``` in `Authorization` Header (Can use Swagger Authorize feature)

### Latest Exchange Rates
- GET `/api/v1/exchange-rates/latest?baseCurrency=EUR`


### Currency Conversion
- POST `/api/v1/exchange-rates/convert`
  ```json
  {
    "fromCurrency": "USD",
    "toCurrency": "EUR",
    "amount": 100
  }
  ```

### Historical Exchange Rates
- GET `/api/v1/exchange-rates/historical?baseCurrency=EUR&startDate=2020-01-01&endDate=2020-01-31&page=1&pageSize=10`

## Assumptions

1. The Frankfurter API is the primary data source
2. Restricted currencies (TRY, PLN, THB, MXN) are not allowed in any conversion
3. JWT tokens are issued by an external identity provider
4. Redis is used for distributed caching
5. SQL Server is used for storing user data and audit logs

## Future Enhancements

1. Support for multiple exchange rate providers
2. Real-time exchange rate updates using WebSocket
3. Batch conversion support
4. Historial Rates code needs to be moved to new service.