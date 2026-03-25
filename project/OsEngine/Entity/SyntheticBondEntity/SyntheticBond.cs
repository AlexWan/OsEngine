using OsEngine.OsTrader.Iceberg;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Tab.SyntheticBondTab;
using System.Collections.Generic;

namespace OsEngine.Entity.SynteticBondEntity
{
    public class SyntheticBond
    {
        /// <summary>
        /// Trading scenarios for this bond pair. Each scenario has independent trading parameters.
        /// | Торговые сценарии для данной пары. Каждый сценарий имеет независимые торговые параметры.
        /// </summary>
        public List<BondScenario> Scenarios = new List<BondScenario>();

        /// <summary>
        /// The price multiplier in a pair for futures. The default is 1 | Ценовой мультипликатор в паре для фьючерса. По умолчанию значение 1
        /// </summary>
        public decimal FuturesMultiplicator = 1;

        /// <summary>
        /// The price multiplier in a pair for base. The default is 1 | Ценовой мультипликатор в паре для базы. По умолчанию значение 1
        /// </summary>
        public decimal BaseMultiplicator = 1;

        /// <summary>
        /// Enable or disable rationing for the third security for futures | Включить или выключить нормирование по третьему инструменту для фьючерса
        /// </summary>
        public bool FuturesUseRationing;

        /// <summary>
        /// Enable or disable rationing for the third security for futures | Включить или выключить нормирование по третьему инструменту для базы
        /// </summary>
        public bool BaseUseRationing;

        /// <summary>
        /// The type of rationing for futures. Division, multiplication, difference, addition. | Тип нормирования для фьючерса. Деление, умножение, разница, сложение.
        /// </summary>
        public RationingMode FuturesRationingMode;

        /// <summary>
        /// The type of rationing for base. Division, multiplication, difference, addition. | Тип нормирования для базы. Деление, умножение, разница, сложение.
        /// </summary>
        public RationingMode BaseRationingMode;

        /// <summary>
        /// The main type of rationing. Division, multiplication, difference, addition. | Основной тип нормирования. Деление, умножение, разница, сложение.
        /// </summary>
        public RationingMode MainRationingMode;

        /// <summary>
        /// The security used for rationing for futures | Инструмент для нормирования для фьючерса
        /// </summary>
        public BotTabSimple FuturesRationingSecurity;

        /// <summary>
        /// The security used for rationing for base | Инструмент для нормирования для базы
        /// </summary>
        public BotTabSimple BaseRationingSecurity;

        /// <summary>
        /// The size of the tool offset in hours for futures. It can be either + or - | Размер смещения инструмента в часах для фьючерса. Это может быть значение + или -
        /// </summary>
        public int BaseTimeOffset;

        /// <summary>
        /// The size of the tool offset in hours for futures. It can be either + or - | Размер смещения инструмента в часах для фьючерса. Это может быть значение + или -
        /// </summary>
        public int FuturesTimeOffset;

        /// <summary>
        /// The size of the tool offset in hours for futures. It can be either + or - | Размер смещения инструмента в часах для фьючерса. Это может быть значение + или -
        /// </summary>
        public int FuturesTimeOffsetRationing;

        /// <summary>
        /// The size of the tool offset in hours for base. It can be either + or - | Размер смещения инструмента в часах для базы. Это может быть значение + или -
        /// </summary>
        public int BaseTimeOffsetRationing;

        /// <summary>
        /// Provides access to the futures BotTab for market data (candles, price, security info).
        /// Trading parameters live in each BondScenario.
        /// | Предоставляет доступ к BotTab фьючерса для рыночных данных (свечи, цена, информация об инструменте).
        /// Торговые параметры находятся в каждом BondScenario.
        /// </summary>
        public ArbitrationParameters FuturesIcebergParameters;

        /// <summary>
        /// Provides access to the base BotTab for market data.
        /// | Предоставляет доступ к BotTab базы для рыночных данных.
        /// </summary>
        public ArbitrationParameters BaseIcebergParameters;

        /// <summary>
        /// The current minimum balance between the base and the futures | Текущий минимальный остаток между базой и фьючерсом
        /// </summary>
        public CointegrationBuilder CointegrationBuilder;

        /// <summary>
        /// The current percentage breakdown between the base and the futures | Текущая раздвижка между базой и фьючерсом в процентах
        /// </summary>
        public List<PairIndicatorValue> PercentSeparationCandles = new List<PairIndicatorValue>();

        /// <summary>
        /// The current sliding between the base and the futures in absolute terms | Текущая раздвижка между базой и фьючерсом в абсолюте
        /// </summary>
        public List<PairIndicatorValue> AbsoluteSeparationCandles = new List<PairIndicatorValue>();

        /// <summary>
        /// History separation changes. Amount of data | История изменения раздвижки. Количество данных
        /// </summary>
        public decimal SeparationLength = 50;

        /// <summary>
        /// Days before the expiration of the futures contract. If the value is negative, then the validity period is infinite | Дней до истечения срока действия фьючерса. Если отрицательное значение, то срок действия бесконечный
        /// </summary>
        public int DaysBeforeExpiration = -1;

        /// <summary>
        /// Profit per day | Профит в день
        /// </summary>
        public decimal ProfitPerDay = 0;
    }

    public enum RationingMode
    {
        Difference,

        Division,

        Multiplication,

        Addition
    }

    public enum SynteticBondOrderPosition
    {
        Ask,

        Bid,

        Middle
    }
}
