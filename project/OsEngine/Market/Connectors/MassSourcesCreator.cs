/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Candles;
using OsEngine.Candles.Series;
using OsEngine.Entity;


namespace OsEngine.Market.Connectors
{
    public class MassSourcesCreator
    {
        public string GetSaveString()
        {
            string result = "";
            result += PortfolioName + "\n";
            result += SecuritiesClass + "\n";
            result += TimeFrame + "\n";
            result += ServerType + "&" + ServerName + "\n";
            result += EmulatorIsOn + "\n";
            result += CandleMarketDataType + 
                "&" + MarketDepthBuildMaxSpreadIsOn.ToString() + 
                "&" + MarketDepthBuildMaxSpread +"\n";

            result += CommissionType + "\n";
            result += CommissionValue + "\n";
            result += SaveTradesInCandles + "\n";
            result += CandleCreateMethodType + "\n";
            result += CandleSeriesRealization.GetSaveString() + "\n";

            for (int i = 0; i < SecuritiesNames.Count; i++)
            {
                result += SecuritiesNames[i].GetSaveStr() + "\n";
            }

            return result;
        }

        public void LoadFromString(string saveStr)
        {
            string[] values = saveStr.Split('\n');

            PortfolioName = values[0];
            SecuritiesClass = values[1];
            Enum.TryParse(values[2], out TimeFrame);

            Enum.TryParse(values[3].Split('&')[0], out ServerType);

            if(values[3].Split('&').Length > 1)
            {
                ServerName = values[3].Split('&')[1];
            }
            else
            {
                ServerName = ServerType.ToString();
            }

            EmulatorIsOn = Convert.ToBoolean(values[4]);

            string[] candleMarketDataType = values[5].Split("&");

            Enum.TryParse(candleMarketDataType[0], out CandleMarketDataType);

            if(candleMarketDataType.Length > 1)
            {
                MarketDepthBuildMaxSpreadIsOn = Convert.ToBoolean(candleMarketDataType[1]);
                MarketDepthBuildMaxSpread = candleMarketDataType[2].ToDecimal();
            }

            Enum.TryParse(values[6], out CommissionType);
            CommissionValue = values[7].ToDecimal();
            SaveTradesInCandles = Convert.ToBoolean(values[8]);
            CandleCreateMethodType = values[9];
            CandleSeriesRealization.SetSaveString(values[10]);

            for (int i = 11; i < values.Length; i++)
            {
                string value = values[i];
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                if (value == "\r")
                {
                    break;
                }

                ActivatedSecurity curSec = new ActivatedSecurity();
                curSec.SetFromStr(value);

                SecuritiesNames.Add(curSec);
            }
        }

        public MassSourcesCreator(StartProgram startProgram)
        {
            _startProgram = startProgram;

            CandleSeriesRealization = CandleFactory.CreateCandleSeriesRealization("Simple");
            CandleSeriesRealization.Init(startProgram);
            _candleCreateMethodType = "Simple";
        }

        public ACandlesSeriesRealization CandleSeriesRealization;

        public StartProgram StartProgram
        {
            get { return _startProgram; }
        }
        private StartProgram _startProgram;

        public string PortfolioName;

        public string SecuritiesClass;

        public List<ActivatedSecurity> SecuritiesNames = new List<ActivatedSecurity>();

        public TimeFrame TimeFrame = TimeFrame.Min1;

        public ServerType ServerType;

        public string ServerName;

        public bool EmulatorIsOn;

        public CandleMarketDataType CandleMarketDataType;

        public string CandleCreateMethodType
        {
            get
            {
                return _candleCreateMethodType;
            }
            set
            {
                string newType = value;

                if (newType == _candleCreateMethodType)
                {
                    return;
                }

                if (CandleSeriesRealization != null)
                {
                    CandleSeriesRealization = null;
                }
                _candleCreateMethodType = newType;
                CandleSeriesRealization = CandleFactory.CreateCandleSeriesRealization(newType);
                CandleSeriesRealization.Init(_startProgram);
            }
        }
        private string _candleCreateMethodType;

        public CommissionType CommissionType;

        public decimal CommissionValue;

        public bool SaveTradesInCandles;

        public bool MarketDepthBuildMaxSpreadIsOn;

        public decimal MarketDepthBuildMaxSpread = 0.5m;

    }

    public class ActivatedSecurity
    {

        public string SecurityName;

        public string SecurityClass;

        public bool IsOn;

        public string GetSaveStr()
        {
            string result = "";

            result += SecurityName + "^" + SecurityClass + "^" + IsOn;

            return result;
        }

        public void SetFromStr(string str)
        {
            string[] strArray = str.Split('^');

            SecurityName = strArray[0];
            SecurityClass = strArray[1];
            IsOn = Convert.ToBoolean(strArray[2]);

        }
    }
}