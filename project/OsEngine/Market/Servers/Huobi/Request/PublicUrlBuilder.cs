namespace OsEngine.Market.Servers.Huobi.Request
{
    public class PublicUrlBuilder
    {
        private readonly string _host;

        public PublicUrlBuilder(string host)
        {
            _host = host;
        }

        public string Build(string path, GetRequest request = null)
        {
            if (request != null)
            {
                return $"https://{_host}{path}?{request.BuildParams()}";
            }
            else
            {
                return $"https://{_host}{path}";
            }
        }
    }
}
