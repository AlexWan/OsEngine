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
            result += ServerType + "\n";
            result += EmulatorIsOn + "\n";
            result += CandleMarketDataType + "\n";
            result += ComissionType + "\n";
            result += ComissionValue + "\n";
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
            Enum.TryParse(values[3], out ServerType);
            EmulatorIsOn = Convert.ToBoolean(values[4]);
            Enum.TryParse(values[5], out CandleMarketDataType);
            Enum.TryParse(values[6], out ComissionType);
            ComissionValue = values[7].ToDecimal();
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

        public ComissionType ComissionType;

        public decimal ComissionValue;

        public bool SaveTradesInCandles;

    }

    /// <summary>
    /// класс для хранения бумаги активированной к подключению во время массового добавления бумаг в скринере и индексах
    /// </summary>
    public class ActivatedSecurity
    {
        /// <summary>
        /// имя бумаги
        /// </summary>
        public string SecurityName;

        /// <summary>
        /// имя класса
        /// </summary>
        public string SecurityClass;

        /// <summary>
        /// включена ли бумага к активации
        /// </summary>
        public bool IsOn;

        /// <summary>
        /// взять строку сохранения
        /// </summary>
        public string GetSaveStr()
        {
            string result = "";

            result += SecurityName + "^" + SecurityClass + "^" + IsOn;

            return result;
        }

        /// <summary>
        /// настроить класс из строки сохранения
        /// </summary>
        public void SetFromStr(string str)
        {
            string[] strArray = str.Split('^');

            SecurityName = strArray[0];
            SecurityClass = strArray[1];
            IsOn = Convert.ToBoolean(strArray[2]);

        }
    }
}