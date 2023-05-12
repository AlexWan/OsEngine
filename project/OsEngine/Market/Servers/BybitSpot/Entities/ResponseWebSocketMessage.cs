using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.BybitSpot.Entities
{
    public class ResponseWebSocketMessage<T>
    {
        public string topic;
        public long ts;
        public string type;
        public T data;
    }
    public class SubscribleMessage
    {
        public string op;
        public bool success;
        public string req_id;
        public string ret_msg;
        public string conn_id;
    }
    public class ResponseTrade
    {
        public string v;
        public long t;
        public string p;
        public string q;
        public bool m;
        public string type;
    }
    public class ResponseOrderBook
    {
        public string s;
        public string t;
        public string[,] b;
        public string[,] a;
    }
    public class ResponseMyTrades
    {
        public string e;
        public string E;
        public string s;
        public string q;
        public string t;
        public string p;
        public string T;
        public string o;
        public string c;
        public string O;
        public string a;
        public string A;
        public string m;
        public string S;
        public string b;
    }
    public class ResponseOrder
    {
        public string e;
        public string E;
        public string s;
        public string c;
        public string S;
        public string o;
        public string f;
        public string q;
        public string p;
        public string X;
        public string i;
        public string M;
        public string l;
        public string z;
        public string L;
        public string n;
        public string N;
        public string u;
        public string w;
        public string m;
        public string O;
        public string Z;
        public string A;
        public string C;
        public string v;
        public string d;
        public string sg;
        public string st;
        public string ct;
        public string so;
        public string b;
    }
}
