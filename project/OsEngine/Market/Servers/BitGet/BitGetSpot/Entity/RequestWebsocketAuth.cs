using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitGet.BitGetSpot.Entity
{
    public class RequestWebsocketAuth
    {
        public string op;
        public List<AuthItem> args;
    }

    public class AuthItem
    {
        public string apiKey;
        public string passphrase;
        public string timestamp;
        public string sign;
    }
}
