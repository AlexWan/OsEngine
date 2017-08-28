﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.BitMex.BitMexEntity
{
    public class TypesTrades
    {
        public string timestamp { get; set; }
        public string symbol { get; set; }
        public string side { get; set; }
        public string size { get; set; }
        public string price { get; set; }
        public string tickDirection { get; set; }
        public string trdMatchID { get; set; }
        //public long grossValue { get; set; }
        //public string homeNotional { get; set; }
        //public string foreignNotional { get; set; }
    }

    public class ForeignKeysTrades
    {
        public string symbol { get; set; }
        public string side { get; set; }
    }

    public class AttributesTrades
    {
        public string timestamp { get; set; }
        public string symbol { get; set; }
    }

    public class DatumTrades
    {
        public string timestamp { get; set; }
        public string symbol { get; set; }
        public string side { get; set; }
        public int size { get; set; }
        public decimal price { get; set; }
        public string tickDirection { get; set; }
        public string trdMatchID { get; set; }

        //public int grossValue { get; set; }
        //public double homeNotional { get; set; }
        //public int foreignNotional { get; set; }
    }

    public class FilterTrades
    {
        public string symbol { get; set; }
    }

    public class BitMexTrades
    {
        public string table { get; set; }
        public List<object> keys { get; set; }
        public TypesTrades types { get; set; }
        public ForeignKeysTrades foreignKeys { get; set; }
        public AttributesTrades attributes { get; set; }
        public string action { get; set; }
        public List<DatumTrades> data { get; set; }
        public FilterTrades filter { get; set; }
    }
}
