/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
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
            result += CandleCreateMethodType + "\n";
            result += SetForeign + "\n";
            result += CountTradeInCandle + "\n";
            result += VolumeToCloseCandleInVolumeType + "\n";
            result += RencoPunktsToCloseCandleInRencoType + "\n";
            result += RencoIsBuildShadows + "\n";
            result += DeltaPeriods + "\n";
            result += RangeCandlesPunkts + "\n";
            result += ReversCandlesPunktsMinMove + "\n";
            result += ReversCandlesPunktsBackMove + "\n";
            result += ComissionType + "\n";
            result += ComissionValue + "\n";
            result += SaveTradesInCandles + "\n";

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
            Enum.TryParse(values[6], out CandleCreateMethodType);
            SetForeign = Convert.ToBoolean(values[7]);
            CountTradeInCandle = Convert.ToInt32(values[8]);
            VolumeToCloseCandleInVolumeType = values[9].ToDecimal();
            RencoPunktsToCloseCandleInRencoType = values[10].ToDecimal();
            RencoIsBuildShadows = Convert.ToBoolean(values[11]);
            DeltaPeriods = values[12].ToDecimal();
            RangeCandlesPunkts = values[13].ToDecimal();
            ReversCandlesPunktsMinMove = values[14].ToDecimal();
            ReversCandlesPunktsBackMove = values[15].ToDecimal();
            Enum.TryParse(values[16], out ComissionType);
            ComissionValue = values[17].ToDecimal();
            SaveTradesInCandles = Convert.ToBoolean(values[18]);

            for (int i = 19; i < values.Length; i++)
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
        }

        /// <summary>
        /// program that created the robot / 
        /// программа создавшая робота
        /// </summary>
        public StartProgram StartProgram
        {
            get { return _startProgram; }
        }
        private StartProgram _startProgram;

        /// <summary>
        /// имя портфеля для торговли
        /// </summary>
        public string PortfolioName;

        /// <summary>
        /// класс бумаг в скринере
        /// </summary>
        public string SecuritiesClass;

        /// <summary>
        /// имена бумаг добавленых в подключение
        /// </summary>
        public List<ActivatedSecurity> SecuritiesNames = new List<ActivatedSecurity>();

        /// <summary>
        /// таймфрейм
        /// </summary>
        public TimeFrame TimeFrame = TimeFrame.Min1;

        /// <summary>
        /// тип сервера
        /// </summary>
        public ServerType ServerType;

        /// <summary>
        /// включен ли эмулятор
        /// </summary>
        public bool EmulatorIsOn;

        /// <summary>
        /// тип данных для рассчёта свечек в серии свечей
        /// </summary>
        public CandleMarketDataType CandleMarketDataType;

        /// <summary>
        /// метод для создания свечек
        /// </summary>
        public CandleCreateMethodType CandleCreateMethodType;

        /// <summary>
        /// нужно ли запрашивать не торговые интервалы
        /// </summary>
        public bool SetForeign;

        /// <summary>
        /// кол-во трейдов в свече
        /// </summary>
        public int CountTradeInCandle = 100;

        /// <summary>
        /// объём для закрытия свечи
        /// </summary>
        public decimal VolumeToCloseCandleInVolumeType = 1000;

        /// <summary>
        /// движение для закрытия свечи в свечах типа Renco
        /// </summary>
        public decimal RencoPunktsToCloseCandleInRencoType = 100;

        /// <summary>
        /// сторим ли тени в свечках типа Renco
        /// </summary>
        public bool RencoIsBuildShadows;

        /// <summary>
        /// период дельты
        /// </summary>
        public decimal DeltaPeriods = 1000;

        /// <summary>
        /// пункты для свечек Range
        /// </summary>
        public decimal RangeCandlesPunkts;

        /// <summary>
        /// Минимальный откат для свечек Range
        /// </summary>
        public decimal ReversCandlesPunktsMinMove;

        /// <summary>
        /// Откат для создания свечи вниз для свечек Range
        /// </summary>
        public decimal ReversCandlesPunktsBackMove;

        /// <summary>
        /// тип комиссии для позиций
        /// </summary>
        public ComissionType ComissionType;

        /// <summary>
        /// размер комиссии
        /// </summary>
        public decimal ComissionValue;

        /// <summary>
        /// нужно ли сохранять трейды внутри свечи которой они принадлежат
        /// </summary>
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