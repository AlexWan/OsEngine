using System;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OsEngine.Entity;

namespace OsEngine.Market.Servers.FTX.FtxApi
{
    public class FtxRestApi
    {
        private const string Url = "https://ftx.com/";

        private readonly Client _client;

        private readonly HttpClient _httpClient;

        private readonly HMACSHA256 _hashMaker;

        private long _nonce;
      
        public FtxRestApi(Client client)
        {
            _client = client;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(Url),
                Timeout = TimeSpan.FromSeconds(30)
            };

            _hashMaker = new HMACSHA256(Encoding.UTF8.GetBytes(_client.ApiSecret));
        }

        #region Coins

        public async Task<JToken> GetCoinsAsync()
        {
            var resultString = $"api/coins";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return ParseResponce(result);
        }

        #endregion

        #region Futures

        public async Task<JToken> GetAllFuturesAsync()
        {
            var resultString = $"api/futures";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return ParseResponce(result);
        }

        public async Task<JToken> GetFutureAsync(string future)
        {
            var resultString = $"api/futures/{future}";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return ParseResponce(result);
        }

        public async Task<JToken> GetFutureStatsAsync(string future)
        {
            var resultString = $"api/futures/{future}/stats";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return ParseResponce(result);
        }

        public async Task<JToken> GetFundingRatesAsync(DateTime start, DateTime end)
        {
            var resultString = $"api/funding_rates?start_time={Util.Util.GetSecondsFromEpochStart(start)}&end_time={Util.Util.GetSecondsFromEpochStart(end)}";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return ParseResponce(result);
        }
        #endregion

        #region Markets
        public async Task<JToken> GetMarketsAsync()
        {
            var resultString = $"api/markets";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return ParseResponce(result);
        }

        public async Task<JToken> GetSingleMarketsAsync(string marketName)
        {
            var resultString = $"api/markets/{marketName}";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return ParseResponce(result);
        }

        public async Task<JToken> GetMarketOrderBookAsync(string marketName, int depth = 20)
        {
            var resultString = $"api/markets/{marketName}/orderbook?depth={depth}";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return ParseResponce(result);
        }
        
        public async Task<JToken> GetMarketTradesAsync(string marketName, int limit, DateTime start, DateTime end)
        {
            var resultString = $"api/markets/{marketName}/trades?limit={limit}&start_time={Util.Util.GetSecondsFromEpochStart(start)}&end_time={Util.Util.GetSecondsFromEpochStart(end)}";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return ParseResponce(result);
        }

        public async Task<JToken> GetHistoricalPricesAsync(string marketName, int resolution, int limit, DateTime start, DateTime end)
        {
            var resultString = $"api/markets/{marketName}/candles?resolution={resolution}&limit={limit}&start_time={Util.Util.GetSecondsFromEpochStart(start)}&end_time={Util.Util.GetSecondsFromEpochStart(end)}";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return ParseResponce(result);
        }
        #endregion

        #region Account

        public async Task<JToken> GetAccountInfoAsync()
        {
            var resultString = $"api/account";
            var sign = GenerateSignature(HttpMethod.Get, "/api/account", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return ParseResponce(result);
        }


        public async Task<JToken> GetPositionsAsync()
        {
            var resultString = $"api/positions";
            var sign = GenerateSignature(HttpMethod.Get, "/api/positions", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return ParseResponce(result);
        }

        public async Task<JToken> ChangeAccountLeverageAsync(int leverage)
        {
            var resultString = $"api/account/leverage";
         
            var body = $"{{\"leverage\": {leverage}}}";

            var sign = GenerateSignature(HttpMethod.Post, "/api/account/leverage", body);

            var result = await CallAsyncSign(HttpMethod.Post, resultString, sign, body);

            return ParseResponce(result);
        }

        #endregion

        #region Wallet

        public async Task<JToken> GetCoinAsync()
        {
            var resultString = $"api/wallet/coins";
           
            var sign = GenerateSignature(HttpMethod.Get, "/api/wallet/coins", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return ParseResponce(result);
        }

        public async Task<JToken> GetBalancesAsync()
        {
            var resultString = $"api/wallet/balances";

            var sign = GenerateSignature(HttpMethod.Get, "/api/wallet/balances", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return ParseResponce(result);
        }

        public async Task<JToken> GetDepositAddressAsync(string coin)
        {
            var resultString = $"api/wallet/deposit_address/{coin}";
            
            var sign = GenerateSignature(HttpMethod.Get, $"/api/wallet/deposit_address/{coin}", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return ParseResponce(result);
        }

        public async Task<JToken> GetDepositHistoryAsync()
        {
            var resultString = $"api/wallet/deposits";

            var sign = GenerateSignature(HttpMethod.Get, "/api/wallet/deposits", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return ParseResponce(result);
        }

        public async Task<JToken> GetWithdrawalHistoryAsync()
        {
            var resultString = $"api/wallet/withdrawals";

            var sign = GenerateSignature(HttpMethod.Get, "/api/wallet/withdrawals", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return ParseResponce(result);
        }

        public async Task<JToken> RequestWithdrawalAsync(string coin, decimal size, string addr, string tag, string pass, string code)
        {
            var resultString = $"api/wallet/withdrawals";

            var body = $"{{"+
                $"\"coin\": \"{coin}\"," +
                $"\"size\": {size},"+
                $"\"address\": \"{addr}\","+
                $"\"tag\": {tag},"+
                $"\"password\": \"{pass}\","+
                $"\"code\": {code}" +
                "}";

            var sign = GenerateSignature(HttpMethod.Post, "/api/wallet/withdrawals", body);

            var result = await CallAsyncSign(HttpMethod.Post, resultString, sign, body);

            return ParseResponce(result);
        }

        #endregion

        #region Orders

        public async Task<JToken> PlaceOrderAsync(string instrument, Side side, decimal? price, OrderPriceType orderType, decimal amount, bool reduceOnly = true)
        {
            var path = $"api/orders";

            var body =
                $"{{\"market\": \"{instrument}\"," +
                $"\"side\": \"{side.ToString().ToLower()}\"," +
                $"\"price\": {price?.ToString(CultureInfo.InvariantCulture) ?? "null" }," +
                $"\"type\": \"{orderType.ToString().ToLower()}\"," +
                $"\"size\": {amount.ToString(CultureInfo.InvariantCulture)}," +
                $"\"reduceOnly\": {reduceOnly.ToString().ToLower()}}}";

            var sign = GenerateSignature(HttpMethod.Post, "/api/orders", body);
            var result = await CallAsyncSign(HttpMethod.Post, path, sign, body);

            return ParseResponce(result);
        }

        public async Task<JToken> PlaceStopOrderAsync(string instrument, Side side, decimal triggerPrice, decimal amount, bool reduceOnly = false)
        {
            var path = $"api/conditional_orders";

            var body =
                $"{{\"market\": \"{instrument}\"," +
                $"\"side\": \"{side.ToString().ToLower()}\"," +
                $"\"triggerPrice\": {triggerPrice.ToString(CultureInfo.InvariantCulture)}," +
                $"\"type\": \"stop\"," +
                $"\"size\": {amount.ToString(CultureInfo.InvariantCulture)}," +
                $"\"reduceOnly\": {reduceOnly.ToString().ToLower()}}}";

            var sign = GenerateSignature(HttpMethod.Post, "/api/conditional_orders", body);
            var result = await CallAsyncSign(HttpMethod.Post, path, sign, body);

            return ParseResponce(result);
        }

        public async Task<JToken> PlaceTrailingStopOrderAsync(string instrument, Side side, decimal trailValue, decimal amount, bool reduceOnly = false)
        {
            var path = $"api/conditional_orders";

            var body =
                $"{{\"market\": \"{instrument}\"," +
                $"\"side\": \"{side.ToString().ToLower()}\"," +
                $"\"trailValue\": {trailValue.ToString(CultureInfo.InvariantCulture)}," +
                $"\"type\": \"trailingStop\"," +
                $"\"size\": {amount.ToString(CultureInfo.InvariantCulture)}," +
                $"\"reduceOnly\": {reduceOnly.ToString().ToLower()}}}";

            var sign = GenerateSignature(HttpMethod.Post, "/api/conditional_orders", body);
            var result = await CallAsyncSign(HttpMethod.Post, path, sign, body);

            return ParseResponce(result);
        }

        public async Task<JToken> PlaceTakeProfitOrderAsync(string instrument, Side side, decimal triggerPrice, decimal amount, bool reduceOnly = false)
        {
            var path = $"api/conditional_orders";

            var body =
                $"{{\"market\": \"{instrument}\"," +
                $"\"side\": \"{side.ToString().ToLower()}\"," +
                $"\"triggerPrice\": {triggerPrice.ToString(CultureInfo.InvariantCulture)}," +
                $"\"type\": \"takeProfit\"," +
                $"\"size\": {amount.ToString(CultureInfo.InvariantCulture)}," +
                $"\"reduceOnly\": {reduceOnly.ToString().ToLower()}}}";

            var sign = GenerateSignature(HttpMethod.Post, "/api/conditional_orders", body);
            var result = await CallAsyncSign(HttpMethod.Post, path, sign, body);

            return ParseResponce(result);
        }

        public async Task<JToken> GetOpenOrdersAsync(string instrument)
        {
            var path = $"api/orders?market={instrument}";

            var sign = GenerateSignature(HttpMethod.Get, $"/api/orders?market={instrument}", "");
           
            var result = await CallAsyncSign(HttpMethod.Get, path, sign);

            return ParseResponce(result);
        }

        public async Task<JToken> GetOrderStatusAsync(string id)
        {
            var resultString = $"api/orders/{id}";

            var sign = GenerateSignature(HttpMethod.Get, $"/api/orders/{id}", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return ParseResponce(result);
        }

        public async Task<JToken> GetOrderStatusByClientIdAsync(string clientOrderId)
        {
            var resultString = $"api/orders/by_client_id/{clientOrderId}";

            var sign = GenerateSignature(HttpMethod.Get, $"/api/orders/by_client_id/{clientOrderId}", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return ParseResponce(result);
        }

        public async Task<JToken> CancelOrderAsync(string id)
        {
            var resultString = $"api/orders/{id}";

            var sign = GenerateSignature(HttpMethod.Delete, $"/api/orders/{id}", "");

            var result = await CallAsyncSign(HttpMethod.Delete, resultString, sign);

            return ParseResponce(result);
        }

        public async Task<JToken> CancelOrderByClientIdAsync(string clientOrderId)
        {
            var resultString = $"api/orders/by_client_id/{clientOrderId}";

            var sign = GenerateSignature(HttpMethod.Delete, $"/api/orders/by_client_id/{clientOrderId}", "");

            var result = await CallAsyncSign(HttpMethod.Delete, resultString, sign);

            return ParseResponce(result);
        }

        public async Task<JToken> CancelAllOrdersAsync(string instrument)
        {
            var resultString = $"api/orders";

            var body =
                $"{{\"market\": \"{instrument}\"}}";

                var sign = GenerateSignature(HttpMethod.Delete, $"/api/orders", body);

            var result = await CallAsyncSign(HttpMethod.Delete, resultString, sign, body);

            return ParseResponce(result);
        }

        #endregion

        #region Fills

        public async Task<JToken> GetFillsAsync(string market, int limit, DateTime start, DateTime end)
        {
            var resultString = $"api/fills?market={market}&limit={limit}&start_time={Util.Util.GetSecondsFromEpochStart(start)}&end_time={Util.Util.GetSecondsFromEpochStart(end)}";

            var sign = GenerateSignature(HttpMethod.Get, $"/{resultString}", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return ParseResponce(result);
        }

        #endregion

        #region Funding

        public async Task<JToken> GetFundingPaymentAsync(DateTime start, DateTime end)
        {
            var resultString = $"api/funding_payments?start_time={Util.Util.GetSecondsFromEpochStart(start)}&end_time={Util.Util.GetSecondsFromEpochStart(end)}";

            var sign = GenerateSignature(HttpMethod.Get, $"/{resultString}", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return ParseResponce(result);
        }

        #endregion

        #region Leveraged Tokens

        public async Task<JToken> GetLeveragedTokensListAsync()
        {
            var resultString = $"api/lt/tokens";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return ParseResponce(result);
        }

        public async Task<JToken> GetTokenInfoAsync(string tokenName)
        {
            var resultString = $"api/lt/{tokenName}";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return ParseResponce(result);
        }

        public async Task<JToken> GetLeveragedTokenBalancesAsync()
        {
            var resultString = $"api/lt/balances";

            var sign = GenerateSignature(HttpMethod.Get, $"/api/lt/balances", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return ParseResponce(result);
        }

        public async Task<JToken> GetLeveragedTokenCreationListAsync()
        {
            var resultString = $"api/lt/creations";

            var sign = GenerateSignature(HttpMethod.Get, $"/api/lt/creations", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return ParseResponce(result);
        }

        public async Task<JToken> RequestLeveragedTokenCreationAsync(string tokenName, decimal size)
        {
            var resultString = $"api/lt/{tokenName}/create";
          
            var body = $"{{\"size\": {size}}}";

            var sign = GenerateSignature(HttpMethod.Post, $"/api/lt/{tokenName}/create", body);

            var result = await CallAsyncSign(HttpMethod.Post, resultString, sign, body);

            return ParseResponce(result);
        }

        public async Task<JToken> GetLeveragedTokenRedemptionListAsync()
        {
            var resultString = $"api/lt/redemptions";

            var sign = GenerateSignature(HttpMethod.Get, $"/api/lt/redemptions", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return ParseResponce(result);
        }

        public async Task<JToken> RequestLeveragedTokenRedemptionAsync(string tokenName, decimal size)
        {
            var resultString = $"api/lt/{tokenName}/redeem";

            var body = $"{{\"size\": {size}}}";

            var sign = GenerateSignature(HttpMethod.Post, $"/api/lt/{tokenName}/redeem", body);

            var result = await CallAsyncSign(HttpMethod.Post, resultString, sign, body);

            return ParseResponce(result);
        }

        #endregion

        #region Util

        private async Task<string> CallAsync(HttpMethod method, string endpoint, string body = null)
        {
            var request = new HttpRequestMessage(method, endpoint);

            if (body != null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

            var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return result;
        }

        private async Task<string> CallAsyncSign(HttpMethod method, string endpoint, string sign, string body = null)
        {
            var request = new HttpRequestMessage(method, endpoint);

            if (body != null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            request.Headers.Add("FTX-KEY", _client.ApiKey);
            request.Headers.Add("FTX-SIGN", sign);
            request.Headers.Add("FTX-TS", _nonce.ToString());

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

            var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return result;
        }

        private string GenerateSignature(HttpMethod method, string url, string requestBody)
        {
            _nonce = GetNonce();
            var signature = $"{_nonce}{method.ToString().ToUpper()}{url}{requestBody}";
            var hash = _hashMaker.ComputeHash(Encoding.UTF8.GetBytes(signature));
            var hashStringBase64 = BitConverter.ToString(hash).Replace("-", string.Empty);
            return hashStringBase64.ToLower();
        }

        private long GetNonce()
        {
            return Util.Util.GetMillisecondsFromEpochStart();
        }

        private JToken ParseResponce(string responce)
        {
            return JToken.Parse(responce);
        }


        #endregion
    }
}