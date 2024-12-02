using System.Collections.Generic;

namespace OsEngine.Market.Servers.TraderNet.Entity
{
    public class ResponceTrade
    {
        public string c;//тикер
        public string ltp; // цена последней сделки
        public string ltc; //направление последней сделки
        public string ltt; //время последней сделки
        public string lts; // кол-во последней сделки
        public string rev; // кол-во последней сделки
    }

    public class ListTrades
    {
        public string c; // тикер
        public string ltp; //цена
        public string lts; // кол-во
        public string ltt; // время
    }

    public class ResponceDepth
    {
        public string i;//тикер
        public string n;
        public List<DeleteDepth> del; 
        public List<InsertDepth> ins; 
        public List<UpdateDepth> upd; 
        public string lts; 
    }

    public class UpdateDepth
    {
        public string p;// цена строки стакана сделок  
        public string s;// направление цены
        public string q;// количество в строке  
        public string k;// номер позиции в стакане
    }

    public class InsertDepth
    {
        public string p;
        public string s;
        public string q;
        public string k;
    }

    public class DeleteDepth
    {
        public string p;
        public string s;
        public string q;
        public string k;
    }

    public class ListMdTiker
    {
        public string p; // цена
        public string s; // bid/ask
        public string q; // кол-во
    }

    public class ResponsePortfolio
    {
        public List<Acc> acc;
        public List<Pos> pos;
    }

    public class Pos
    {
        public string i; // tiker
        public string q; //кол-во
        public string price_a; //цена
    }

    public class Acc
    {
        public string curr;
        public string s;
    }

    public class ResponceOrders
    {
        public string instr;
        public string date;
        public string id;
        public string p; // цена
        public string q; // кол-во
        public string stat; // статус
        public string userOrderId;
        public string oper; // направление сделки
        public string type; // тип сделки
        public List<ResponceOrdersTrades> trade; // кол-во
    }

    public class ResponceOrdersTrades
    {
        public string date;
        public string id;
        public string p;// цена
        public string q;// кол-во
    }
}
