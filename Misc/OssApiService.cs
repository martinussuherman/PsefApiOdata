using IdentityModel.Client;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.JsonWebTokens;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Options;

namespace PsefApiOData.Misc
{
    /// <summary>
    /// OSS (Online Single Submission) API service.
    /// </summary>
    public class OssApiService : IOssApiService
    {
        /// <summary>
        /// OSS API service.
        /// </summary>
        /// <param name="httpClient">Http client to connect to OSS API.</param>
        /// <param name="memoryCache">Memory cache.</param>
        /// <param name="options">OSS API configuration options.</param>
        public OssApiService(
            HttpClient httpClient,
            IMemoryCache memoryCache,
            IOptions<OssApiOptions> options)
        {
            _memoryCache = memoryCache;
            _options = options;
            _httpClient = httpClient;
        }

        /// <summary>
        /// Authenticate to OSS Api.
        /// </summary>
        /// <returns>Token retrieved from the OSS Api authentication endpoint.</returns>
        public async Task<JsonWebToken> Authenticate()
        {
            if (_memoryCache.TryGetValue(
                nameof(JsonWebToken),
                out JsonWebToken cachedToken))
            {
                return cachedToken;
            }

            LoginInfo loginInfo = new LoginInfo
            {
                Username = _options.Value.User,
                Password = _options.Value.Password
            };

            string data = JsonConvert.SerializeObject(
                loginInfo,
                new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });

            HttpResponseMessage response = await _httpClient.PostAsync(
                $"{_options.Value.BaseUri}/consumer/login",
                new StringContent(data, Encoding.UTF8, ApiInfo.JsonOutput));

            if (!response.IsSuccessStatusCode)
            {
                return default;
            }

            OssToken ossToken = JsonConvert.DeserializeObject<OssToken>(
                await response.Content.ReadAsStringAsync());
            JsonWebToken token = new JsonWebToken(ossToken.Token);

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(token.ValidTo);

            _memoryCache.Set(nameof(JsonWebToken), token, cacheEntryOptions);
            return token;
        }

        /// <summary>
        /// Call OSS Api.
        /// </summary>
        /// <param name="token">Access token for the OSS Api.</param>
        /// <param name="uri">Uri of Api request.</param>
        /// <param name="content">Content of Api request.</param>
        /// <returns>JObject retrieved from the OSS Api.</returns>
        public async Task<JObject> CallApiAsync(JsonWebToken token, string uri, string content)
        {
            HttpRequestMessage request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_options.Value.BaseUri}{uri}");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.EncodedToken);
            request.Content = new StringContent(content, Encoding.UTF8, ApiInfo.JsonOutput);
            HttpResponseMessage response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return default;
            }

            return JObject.Parse(await response.Content.ReadAsStringAsync());
        }

        internal class OssToken
        {
            public string Token { get; set; }
        }

        internal class LoginInfo
        {
            public string Username { get; set; }

            public string Password { get; set; }
        }

        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _memoryCache;
        private readonly IOptions<OssApiOptions> _options;
    }
}
