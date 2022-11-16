/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Logging;
using OsEngine.Market;
using System.Windows.Forms;
using System.Threading;
using OsEngine.Robots.Engines;
using OsEngine.Language;
using OsEngine.Alerts;

namespace OsEngine.Market.Connectors
{
    public class MassSourcesCreator
    {

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
        public TimeFrame TimeFrame = TimeFrame.Min30;

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
        public int CountTradeInCandle;

        /// <summary>
        /// объём для закрытия свечи
        /// </summary>
        public decimal VolumeToCloseCandleInVolumeType;

        /// <summary>
        /// движение для закрытия свечи в свечах типа Renco
        /// </summary>
        public decimal RencoPunktsToCloseCandleInRencoType;

        /// <summary>
        /// сторим ли тени в свечках типа Renco
        /// </summary>
        public bool RencoIsBuildShadows;

        /// <summary>
        /// период дельты
        /// </summary>
        public decimal DeltaPeriods;

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
