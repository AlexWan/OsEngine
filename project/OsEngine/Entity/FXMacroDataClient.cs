using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.Entity
{
    public sealed class FXMacroDataClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;

        public FXMacroDataClient(HttpClient httpClient, string apiKey = "", string baseUrl = "https://api.fxmacrodata.com/v1")
        {
            _httpClient = httpClient;
            _apiKey = apiKey ?? string.Empty;
            _baseUrl = baseUrl.TrimEnd('/');
        }

        public Task<string> DataCatalogueAsync(string currency, CancellationToken cancellationToken = default) => GetAsync($"/data_catalogue/{Normalize(currency)}", cancellationToken);
        public Task<string> AnnouncementsAsync(string currency, string indicator, CancellationToken cancellationToken = default) => GetAsync($"/announcements/{Normalize(currency)}/{indicator}", cancellationToken);
        public Task<string> CalendarAsync(string currency, CancellationToken cancellationToken = default) => GetAsync($"/calendar/{Normalize(currency)}", cancellationToken);
        public Task<string> PredictionsAsync(string currency, string indicator, CancellationToken cancellationToken = default) => GetAsync($"/predictions/{Normalize(currency)}/{indicator}", cancellationToken);
        public Task<string> ForexAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default) => GetAsync($"/forex/{Normalize(baseCurrency)}/{Normalize(quoteCurrency)}", cancellationToken);
        public Task<string> CotAsync(string currency, CancellationToken cancellationToken = default) => GetAsync($"/cot/{Normalize(currency)}", cancellationToken);
        public Task<string> CommoditiesLatestAsync(CancellationToken cancellationToken = default) => GetAsync("/commodities/latest", cancellationToken);
        public Task<string> CommodityAsync(string indicator, CancellationToken cancellationToken = default) => GetAsync($"/commodities/{indicator}", cancellationToken);
        public Task<string> CurvesAsync(string currency, CancellationToken cancellationToken = default) => GetAsync($"/curves/{Normalize(currency)}", cancellationToken);
        public Task<string> CurveProxiesAsync(string currency, CancellationToken cancellationToken = default) => GetAsync($"/curve_proxies/{Normalize(currency)}", cancellationToken);
        public Task<string> ForwardCurvesAsync(string currency, CancellationToken cancellationToken = default) => GetAsync($"/forward_curves/{Normalize(currency)}", cancellationToken);
        public Task<string> MarketSessionsAsync(CancellationToken cancellationToken = default) => GetAsync("/market_sessions", cancellationToken);
        public Task<string> RiskSentimentAsync(CancellationToken cancellationToken = default) => GetAsync("/risk_sentiment", cancellationToken);
        public Task<string> NewsAsync(string currency, CancellationToken cancellationToken = default) => GetAsync($"/news/{Normalize(currency)}", cancellationToken);
        public Task<string> PressReleasesAsync(string currency, CancellationToken cancellationToken = default) => GetAsync($"/press-releases/{Normalize(currency)}", cancellationToken);
        public Task<string> CentralBankersAsync(string currency, CancellationToken cancellationToken = default) => GetAsync($"/central_bankers/{Normalize(currency)}", cancellationToken);

        private async Task<string> GetAsync(string path, CancellationToken cancellationToken)
        {
            var query = string.IsNullOrWhiteSpace(_apiKey)
                ? string.Empty
                : "?api_key=" + Uri.EscapeDataString(_apiKey);

            using var response = await _httpClient.GetAsync(_baseUrl + path + query, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        private static string Normalize(string value) => value.Trim().ToLowerInvariant();
    }
}
