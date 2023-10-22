using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace OsEngine.Market.Servers.Alor.Tools
{
    public class ApiClient
    {
        private readonly HttpClient _client;

        private readonly TokenProvider _tokenProvider;

        public ApiClient(TokenProvider tokenProvider)
        {
            _client = new HttpClient();
            _tokenProvider = tokenProvider;
        }
        
        private async Task<string> GetAccessTokenAsync()
        {
            return await _tokenProvider.GetAccessTokenAsync();
        }

        public async Task<T> PostAsync<T>(string url, object data)
        {
            var accessToken = await GetAccessTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(url, content);
        
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(responseJson);
            }
            else
            {
                // Handle unsuccessful response here
                throw new HttpRequestException(await response.Content.ReadAsStringAsync());
            }
        }

        public async Task<T> GetAsync<T>(string url)
        {
            var accessToken = await GetAccessTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _client.GetAsync(url);
        
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(responseJson);
            }
            else
            {
                // Handle unsuccessful response here
                throw new HttpRequestException(await response.Content.ReadAsStringAsync());
            }
        }
    }
}