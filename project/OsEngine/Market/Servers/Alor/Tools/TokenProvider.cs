using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OsEngine.Market.Servers.Alor.Tools
{
    public class TokenProvider
    {
        private readonly string _authTokenHost;
        
        public TokenProvider(string authApiHost)
        {
            _authTokenHost = authApiHost;
        }
        
        public async Task<string> GetAccessTokenAsync()
        {
            using var authApiClient = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, _authTokenHost);
            request.Headers.Add("Accept", "application/json");
            var content = new StringContent("", null, "text/plain");
            request.Content = content;
            var response = await authApiClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var tokenResponseJson = await response.Content.ReadAsStringAsync();
            try
            {
                var tokenData = JsonConvert.DeserializeObject<TokenResponse>(tokenResponseJson);
                return tokenData.AccessToken;
            }
            catch (Exception ex)
            {
                throw new Exception("Invalid token response format");
            }
        }
    }
    
    public struct TokenResponse
    {
        public string AccessToken { get; set; }
    }
}