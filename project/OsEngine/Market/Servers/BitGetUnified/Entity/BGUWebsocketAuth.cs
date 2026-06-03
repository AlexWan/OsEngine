/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;


namespace OsEngine.Market.Servers.BitGetUnified.Entity
{
    public class BGUWebsocketAuth
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
