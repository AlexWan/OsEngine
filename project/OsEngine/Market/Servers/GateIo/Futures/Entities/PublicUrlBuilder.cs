using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.GateIo.Futures.Entities
{
    public class PublicUrlBuilder
    {
        private readonly string _host;
        private readonly string _path;
        private readonly string _wallet;

        public PublicUrlBuilder(string host, string path, string wallet)
        {
            _host = host;
            _wallet = wallet;
            _path = path;
        }

        public string Build(string command, RestRequestBuilder request = null)
        {
            if (request != null)
            {
                return $"{_host}{_path}{_wallet}{command}?{request.BuildParams()}";
            }
            else
            {
                return $"{_host}{_path}{_wallet}{command}";
            }
        }
    }
}