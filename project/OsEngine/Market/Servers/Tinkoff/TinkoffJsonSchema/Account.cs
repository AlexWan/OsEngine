using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Tinkoff.TinkoffJsonSchema
{

    public class AccountsResponse
    {
        public List<Account> accounts;
    }

    public class Account
    {
        public string id;
        public string type;
        public string name;
        public string status;
        public string openedDate;
        public string closedDate;
        public string accessLevel;
    }
}
