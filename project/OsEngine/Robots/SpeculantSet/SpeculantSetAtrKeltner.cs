/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Wiki;

/* Description
Торговый робот для OsEngine

Трендовый импульсный робот. Импульс определяется по росту волатильности (ATR),
уровни - по каналу Кельтнера, фильтр тренда - по Bollinger.

Конструкция: два источника.
1. BotTabScreener для лонгов (KeltnerChannel + ATR + Bollinger на каждой бумаге).
2. BotTabScreener для шортов (независимые параметры для отдельной оптимизации).

Покупка:
1. ATR (индикатор с типом расчёта Percent) вырос на заданную величину за последние N свечей.
2. Цена выше центральной линии Keltner и ниже верхней линии Keltner.
3. Фильтр Bollinger: цена выше верхней линии Bollinger.
4. Нет позиции по бумаге и не достигнут лимит позиций скринера.
Вход через BuyAtStopMarketIceberg с ценой активации = верхняя линия Keltner, время жизни заявки = 1 свеча.

Продажа: зеркально (рост ATR тот же - волатильность растёт в обе стороны,
цена ниже центральной линии Keltner и выше нижней линии Keltner, фильтр Bollinger: цена ниже
нижней линии Bollinger, цена активации = нижняя линия Keltner). Шорт не открывается
в окне дивидендной отсечки (5 дней до и 2 дня после).

Выход: CloseAtStopMarketIceberg по нижней линии Keltner (шорт - по верхней).
Стоп передвигается на каждой закрытой свече только в сторону прибыли и только в торговое время.

Неторговые периоды: торговля 10.00-18.00, в выходные не торгуем. В неторговое время
стоп-заявки на вход отменяются, а стопы открытых позиций деактивируются
(StopOrderIsActive = false), в торговое время всё включается обратно.
 */

namespace OsEngine.Robots.SpeculantSet
{
    [Bot("SpeculantSetAtrKeltner")] // Создаём атрибут, чтобы ничего не писать в BotFactory
    public class SpeculantSetAtrKeltner : BotPanel
    {
        #region Sources

        private BotTabScreener _screenerLong;
        private BotTabScreener _screenerShort;

        #endregion

        #region Parameters Base

        private StrategyParameterString _regime;
        private StrategyParameterButton _tradePeriodsShowDialogButton;

        // Торговые периоды
        private NonTradePeriods _tradePeriodsSettings;

        #endregion

        #region Parameters Long

        private StrategyParameterBool _longIsOn;
        private StrategyParameterInt _longAtrPeriod;
        private StrategyParameterInt _longAtrGrowthCandles;
        private StrategyParameterDecimal _longAtrGrowthValue;
        private StrategyParameterInt _longKeltnerEmaLength;
        private StrategyParameterInt _longKeltnerAtrLength;
        private StrategyParameterDecimal _longKeltnerDeviation;
        private StrategyParameterInt _longBollingerLength;
        private StrategyParameterDecimal _longBollingerDeviation;
        private StrategyParameterInt _longMaxPositions;
        private StrategyParameterInt _longIcebergOrdersCount;
        private StrategyParameterInt _longIcebergMillisecondsDistance;
        private StrategyParameterString _longVolumeType;
        private StrategyParameterDecimal _longVolume;
        private StrategyParameterString _longTradeAssetInPortfolio;

        #endregion

        #region Parameters Short

        private StrategyParameterBool _shortIsOn;
        private StrategyParameterInt _shortAtrPeriod;
        private StrategyParameterInt _shortAtrGrowthCandles;
        private StrategyParameterDecimal _shortAtrGrowthValue;
        private StrategyParameterInt _shortKeltnerEmaLength;
        private StrategyParameterInt _shortKeltnerAtrLength;
        private StrategyParameterDecimal _shortKeltnerDeviation;
        private StrategyParameterInt _shortBollingerLength;
        private StrategyParameterDecimal _shortBollingerDeviation;
        private StrategyParameterInt _shortMaxPositions;
        private StrategyParameterInt _shortIcebergOrdersCount;
        private StrategyParameterInt _shortIcebergMillisecondsDistance;
        private StrategyParameterString _shortVolumeType;
        private StrategyParameterDecimal _shortVolume;
        private StrategyParameterString _shortTradeAssetInPortfolio;

        // Дивидендная блокировка шортов
        private StrategyParameterBool _shortBlockDuringDividends;

        #endregion

        #region Parameters Update (автообновление дивидендов)

        private StrategyParameterString _autoUpdateDividends;
        private StrategyParameterTimeOfDay _dividendsUpdateCheckTime;
        private StrategyParameterInt _dividendsMaxAgeDays;
        private StrategyParameterButton _startUpdateDividendsButton;

        private DateTime _lastDividendsUpdateCheckDate = DateTime.MinValue;
        private bool _dividendsUpdating = false;

        #endregion

        #region Constructor

        public SpeculantSetAtrKeltner(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // неторговые периоды. Торговля с 10.00 до 18.00, в выходные не торгуем
            _tradePeriodsSettings = new NonTradePeriods(name);

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1Start = new TimeOfDay() { Hour = 0, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1End = new TimeOfDay() { Hour = 10, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1OnOff = true;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2OnOff = false;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3Start = new TimeOfDay() { Hour = 18, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3End = new TimeOfDay() { Hour = 23, Minute = 59 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3OnOff = true;

            _tradePeriodsSettings.TradeInSunday = false;
            _tradePeriodsSettings.TradeInSaturday = false;

            _tradePeriodsSettings.Load();

            // Создание источников

            TabCreate(BotTabType.Screener);
            _screenerLong = TabsScreener[0];

            TabCreate(BotTabType.Screener);
            _screenerShort = TabsScreener[1];

            // Подписка на события завершения свечей
            _screenerLong.CandleFinishedEvent += _screenerLong_CandleFinishedEvent;
            _screenerShort.CandleFinishedEvent += _screenerShort_CandleFinishedEvent;

            // Подписка на события позиций
            _screenerLong.PositionOpeningSuccesEvent += _screenerLong_PositionOpeningSuccesEvent;
            _screenerShort.PositionOpeningSuccesEvent += _screenerShort_PositionOpeningSuccesEvent;

            // Базовые настройки
            _regime = CreateParameter("Regime", "Off", new[] { "On", "Off" }, "Base");
            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods", "Base");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;

            // Настройки лонга
            _longIsOn = CreateParameter("Long is on", true, "Long");
            _longAtrPeriod = CreateParameter("Long atr period", 15, 5, 100, 1, "Long");
            _longAtrGrowthCandles = CreateParameter("Long atr growth candles", 10, 2, 50, 1, "Long");
            _longAtrGrowthValue = CreateParameter("Long atr growth value", 3.5m, 0.5m, 10, 0.5m, "Long");
            _longKeltnerEmaLength = CreateParameter("Long keltner ema length", 125, 5, 100, 5, "Long");
            _longKeltnerAtrLength = CreateParameter("Long keltner atr length", 10, 5, 100, 5, "Long");
            _longKeltnerDeviation = CreateParameter("Long keltner deviation", 3.8m, 1, 4, 0.1m, "Long");
            _longBollingerLength = CreateParameter("Long bollinger length", 490, 50, 1000, 10, "Long");
            _longBollingerDeviation = CreateParameter("Long bollinger deviation", 0.2m, 0.5m, 4, 0.1m, "Long");
            _longMaxPositions = CreateParameter("Long max positions", 5, 1, 20, 1, "Long");
            _longIcebergOrdersCount = CreateParameter("Long iceberg orders count", 3, 1, 10, 1, "Long");
            _longIcebergMillisecondsDistance = CreateParameter("Long iceberg milliseconds distance", 1000, 500, 10000, 500, "Long");
            _longVolumeType = CreateParameter("Long volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Long");
            _longVolume = CreateParameter("Long volume", 7.5m, 1.0m, 50, 4, "Long");
            _longTradeAssetInPortfolio = CreateParameter("Long trade asset in portfolio", "Prime", "Long");

            // Настройки шорта
            _shortIsOn = CreateParameter("Short is on", true, "Short");
            _shortAtrPeriod = CreateParameter("Short atr period", 14, 5, 100, 1, "Short");
            _shortAtrGrowthCandles = CreateParameter("Short atr growth candles", 10, 2, 50, 1, "Short");
            _shortAtrGrowthValue = CreateParameter("Short atr growth value", 1.5m, 0.5m, 10, 0.5m, "Short");
            _shortKeltnerEmaLength = CreateParameter("Short keltner ema length", 70, 5, 100, 5, "Short");
            _shortKeltnerAtrLength = CreateParameter("Short keltner atr length", 10, 5, 100, 5, "Short");
            _shortKeltnerDeviation = CreateParameter("Short keltner deviation", 3.2m, 1, 4, 0.1m, "Short");
            _shortBollingerLength = CreateParameter("Short bollinger length", 1200, 50, 1000, 10, "Short");
            _shortBollingerDeviation = CreateParameter("Short bollinger deviation", 0.55m, 0.5m, 4, 0.1m, "Short");
            _shortMaxPositions = CreateParameter("Short max positions", 5, 1, 20, 1, "Short");
            _shortIcebergOrdersCount = CreateParameter("Short iceberg orders count", 3, 1, 10, 1, "Short");
            _shortIcebergMillisecondsDistance = CreateParameter("Short iceberg milliseconds distance", 1000, 500, 10000, 500, "Short");
            _shortVolumeType = CreateParameter("Short volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Short");
            _shortVolume = CreateParameter("Short volume", 7.5m, 1.0m, 50, 4, "Short");
            _shortTradeAssetInPortfolio = CreateParameter("Short trade asset in portfolio", "Prime", "Short");

            // Дивидендная блокировка шортов
            _shortBlockDuringDividends = CreateParameter("Short block during dividends", true, "Short");

            // Автообновление базы дивидендов (вкладка Update, работает только в реале)
            _autoUpdateDividends = CreateParameter("Auto update dividends", "Off", new[] { "On", "Off" }, "Update");
            _dividendsUpdateCheckTime = CreateParameterTimeOfDay("Dividends update check time", 8, 0, 0, 0, "Update");
            _dividendsMaxAgeDays = CreateParameter("Dividends max age days", 5, 1, 30, 1, "Update");
            _startUpdateDividendsButton = CreateParameterButton("Start update dividends", "Update");
            _startUpdateDividendsButton.UserClickOnButtonEvent += _startUpdateDividendsButton_UserClickOnButtonEvent;

            // Создаём индикаторы KeltnerChannel, ATR и Bollinger на лонговом скринере
            _screenerLong.CreateCandleIndicator(1, "KeltnerChannel",
                new List<string>() { _longKeltnerEmaLength.ValueInt.ToString(), _longKeltnerAtrLength.ValueInt.ToString(),
                    _longKeltnerAtrLength.ValueInt.ToString(), _longKeltnerDeviation.ValueDecimal.ToString(), "Close" }, "Prime");
            _screenerLong.CreateCandleIndicator(2, "ATR",
                new List<string>() { _longAtrPeriod.ValueInt.ToString(), "Percent" }, "Second");
            _screenerLong.CreateCandleIndicator(3, "Bollinger",
                new List<string>() { _longBollingerLength.ValueInt.ToString(), _longBollingerDeviation.ValueDecimal.ToString() }, "Prime");

            // Создаём индикаторы KeltnerChannel, ATR и Bollinger на шортовом скринере
            _screenerShort.CreateCandleIndicator(1, "KeltnerChannel",
                new List<string>() { _shortKeltnerEmaLength.ValueInt.ToString(), _shortKeltnerAtrLength.ValueInt.ToString(),
                    _shortKeltnerAtrLength.ValueInt.ToString(), _shortKeltnerDeviation.ValueDecimal.ToString(), "Close" }, "Prime");
            _screenerShort.CreateCandleIndicator(2, "ATR",
                new List<string>() { _shortAtrPeriod.ValueInt.ToString(), "Percent" }, "Second");
            _screenerShort.CreateCandleIndicator(3, "Bollinger",
                new List<string>() { _shortBollingerLength.ValueInt.ToString(), _shortBollingerDeviation.ValueDecimal.ToString() }, "Prime");

            // Подписка на событие изменения параметров пользователем
            ParametrsChangeByUser += SpeculantSetAtrKeltner_ParametrsChangeByUser;

            DeleteEvent += SpeculantSetAtrKeltner_DeleteEvent;

            string eng = "Trend volatility robot. Two screeners (long and short) with KeltnerChannel + ATR + Bollinger trend filter, entries by stop iceberg orders, exits by stop on the Keltner line. Shorts are blocked around dividend dates.";
            string ru = "Трендовый робот на росте волатильности. Два скринера (лонг и шорт) с каналом Кельтнера + ATR + фильтром Bollinger, входы стоп-айсберг заявками, выходы стопом по линии Кельтнера. Шорты блокируются вокруг дивидендных отсечек.";
            Description = OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
        }

        #endregion

        #region Parameters update

        private void SpeculantSetAtrKeltner_ParametrsChangeByUser()
        {
            _screenerLong._indicators[0].Parameters
                = new List<string>() { _longKeltnerEmaLength.ValueInt.ToString(), _longKeltnerAtrLength.ValueInt.ToString(),
                    _longKeltnerAtrLength.ValueInt.ToString(), _longKeltnerDeviation.ValueDecimal.ToString(), "Close" };

            _screenerLong._indicators[1].Parameters
                = new List<string>() { _longAtrPeriod.ValueInt.ToString(), "Percent" };

            _screenerLong._indicators[2].Parameters
                = new List<string>() { _longBollingerLength.ValueInt.ToString(), _longBollingerDeviation.ValueDecimal.ToString() };

            _screenerLong.UpdateIndicatorsParameters();

            _screenerShort._indicators[0].Parameters
                = new List<string>() { _shortKeltnerEmaLength.ValueInt.ToString(), _shortKeltnerAtrLength.ValueInt.ToString(),
                    _shortKeltnerAtrLength.ValueInt.ToString(), _shortKeltnerDeviation.ValueDecimal.ToString(), "Close" };

            _screenerShort._indicators[1].Parameters
                = new List<string>() { _shortAtrPeriod.ValueInt.ToString(), "Percent" };

            _screenerShort._indicators[2].Parameters
                = new List<string>() { _shortBollingerLength.ValueInt.ToString(), _shortBollingerDeviation.ValueDecimal.ToString() };

            _screenerShort.UpdateIndicatorsParameters();
        }

        #endregion

        #region Event handlers

        private void SpeculantSetAtrKeltner_DeleteEvent()
        {
            try
            {
                _tradePeriodsSettings.Delete();
            }
            catch (Exception)
            {
                // игнорируем
            }
        }

        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            try
            {
                _tradePeriodsSettings.ShowDialog();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        // Открытие позиции на лонговом скринере. Стопы здесь НЕ выставляются.
        // Если достигнут лимит позиций - отменяем стоп-заявки на вход на всех вкладках
        private void _screenerLong_PositionOpeningSuccesEvent(Position pos, BotTabSimple tab)
        {
            try
            {
                int longPositionsCount = _screenerLong.PositionsOpenAll.FindAll(p => p.Direction == Side.Buy).Count;

                if (longPositionsCount >= _longMaxPositions.ValueInt)
                {
                    for (int i = 0; i < _screenerLong.Tabs.Count; i++)
                    {
                        _screenerLong.Tabs[i].BuyAtStopCancel();
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        // Открытие позиции на шортовом скринере
        private void _screenerShort_PositionOpeningSuccesEvent(Position pos, BotTabSimple tab)
        {
            try
            {
                int shortPositionsCount = _screenerShort.PositionsOpenAll.FindAll(p => p.Direction == Side.Sell).Count;

                if (shortPositionsCount >= _shortMaxPositions.ValueInt)
                {
                    for (int i = 0; i < _screenerShort.Tabs.Count; i++)
                    {
                        _screenerShort.Tabs[i].SellAtStopCancel();
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Logic

        private void _screenerLong_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            try
            {
                if (_regime.ValueString == "Off"
                    || _longIsOn.ValueBool == false)
                {
                    return;
                }

                if (_tradePeriodsSettings.CanTradeThisTime(tab.TimeServerCurrent) == false)
                {
                    // в неторговое время отменяем заявки на вход и деактивируем стопы позиций
                    tab.BuyAtStopCancel();
                    SetStopsActive(_screenerLong.PositionsOpenAll, false);
                    return;
                }

                // в торговое время включаем стопы позиций обратно
                SetStopsActive(_screenerLong.PositionsOpenAll, true);

                // в реале проверяем свежесть базы дивидендов (раз в день, см. регион Dividends)
                if (StartProgram == StartProgram.IsOsTrader)
                {
                    CheckDividendsUpdate(tab.TimeServerCurrent);
                }

                int candlesNeed = Math.Max(_longAtrPeriod.ValueInt,
                    Math.Max(_longKeltnerEmaLength.ValueInt,
                    Math.Max(_longKeltnerAtrLength.ValueInt, _longBollingerLength.ValueInt)))
                    + _longAtrGrowthCandles.ValueInt + 5;

                if (candles.Count < candlesNeed)
                {
                    return;
                }

                Aindicator keltner = (Aindicator)tab.Indicators[0];
                Aindicator atr = (Aindicator)tab.Indicators[1];
                Aindicator bollinger = (Aindicator)tab.Indicators[2];

                if (keltner.DataSeries[0].Values.Count < candles.Count
                    || atr.DataSeries[0].Values.Count < candles.Count
                    || bollinger.DataSeries[0].Values.Count < candles.Count)
                {
                    return;
                }

                List<Position> positions = tab.PositionsOpenAll;

                if (positions.Count == 0)
                { // Логика открытия
                    LogicOpenLong(candles, tab, keltner, atr, bollinger);
                }
                else
                { // Логика закрытия позиции
                    LogicCloseLong(tab, keltner, positions[0]);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _screenerShort_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            try
            {
                if (_regime.ValueString == "Off"
                    || _shortIsOn.ValueBool == false)
                {
                    return;
                }

                if (_tradePeriodsSettings.CanTradeThisTime(tab.TimeServerCurrent) == false)
                {
                    // в неторговое время отменяем заявки на вход и деактивируем стопы позиций
                    tab.SellAtStopCancel();
                    SetStopsActive(_screenerShort.PositionsOpenAll, false);
                    return;
                }

                // в торговое время включаем стопы позиций обратно
                SetStopsActive(_screenerShort.PositionsOpenAll, true);

                int candlesNeed = Math.Max(_shortAtrPeriod.ValueInt,
                    Math.Max(_shortKeltnerEmaLength.ValueInt,
                    Math.Max(_shortKeltnerAtrLength.ValueInt, _shortBollingerLength.ValueInt)))
                    + _shortAtrGrowthCandles.ValueInt + 5;

                if (candles.Count < candlesNeed)
                {
                    return;
                }

                Aindicator keltner = (Aindicator)tab.Indicators[0];
                Aindicator atr = (Aindicator)tab.Indicators[1];
                Aindicator bollinger = (Aindicator)tab.Indicators[2];

                if (keltner.DataSeries[0].Values.Count < candles.Count
                    || atr.DataSeries[0].Values.Count < candles.Count
                    || bollinger.DataSeries[0].Values.Count < candles.Count)
                {
                    return;
                }

                List<Position> positions = tab.PositionsOpenAll;

                if (positions.Count == 0)
                { // Логика открытия
                    LogicOpenShort(candles, tab, keltner, atr, bollinger);
                }
                else
                { // Логика закрытия позиции
                    LogicCloseShort(tab, keltner, positions[0]);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        // Логика открытия лонга
        private void LogicOpenLong(List<Candle> candles, BotTabSimple tab, Aindicator keltner, Aindicator atr, Aindicator bollinger)
        {
            int longPositionsCount = _screenerLong.PositionsOpenAll.FindAll(p => p.Direction == Side.Buy).Count;

            if (longPositionsCount >= _longMaxPositions.ValueInt)
            {
                return;
            }

            // Серии KeltnerChannel: 1 - верхняя линия, 2 - нижняя линия, 3 - центральная линия
            // Серии Bollinger: 0 - верхняя линия, 1 - нижняя линия, 2 - центральная линия
            decimal keltnerUp = keltner.DataSeries[1].Last;
            decimal keltnerDown = keltner.DataSeries[2].Last;
            decimal keltnerCenter = keltner.DataSeries[3].Last;
            decimal lastAtr = atr.DataSeries[0].Last;
            decimal bollingerUp = bollinger.DataSeries[0].Last;

            // нулевые значения = индикатор не прогрет, не торгуем
            if (keltnerUp == 0
                || keltnerDown == 0
                || keltnerCenter == 0
                || lastAtr == 0
                || bollingerUp == 0)
            {
                return;
            }

            // волатильность должна расти: ATR в процентах вырос за последние N свечей
            if (AtrGrows(candles, atr, _longAtrGrowthCandles.ValueInt, _longAtrGrowthValue.ValueDecimal) == false)
            {
                return;
            }

            decimal lastClose = candles[candles.Count - 1].Close;

            if (lastClose <= keltnerCenter
                || lastClose >= keltnerUp)
            {
                return;
            }

            // фильтр Bollinger: лонг разрешён только выше верхней линии
            if (lastClose <= bollingerUp)
            {
                return;
            }

            decimal volume = GetVolume(tab, _longVolumeType, _longVolume, _longTradeAssetInPortfolio);

            if (volume == 0)
            {
                return;
            }

            // перед перевыставлением отменяем предыдущую заявку
            tab.BuyAtStopCancel();

            // заявка стоп-маркет: цена активации = верхняя линия Keltner, жизнь заявки - 1 свеча
            tab.BuyAtStopMarketIceberg(volume, keltnerUp, keltnerUp,
                StopActivateType.HigherOrEqual, 1, "LongEntry",
                PositionOpenerToStopLifeTimeType.CandlesCount,
                _longIcebergOrdersCount.ValueInt, _longIcebergMillisecondsDistance.ValueInt);
        }

        // Логика открытия шорта
        private void LogicOpenShort(List<Candle> candles, BotTabSimple tab, Aindicator keltner, Aindicator atr, Aindicator bollinger)
        {
            int shortPositionsCount = _screenerShort.PositionsOpenAll.FindAll(p => p.Direction == Side.Sell).Count;

            if (shortPositionsCount >= _shortMaxPositions.ValueInt)
            {
                return;
            }

            // Серии KeltnerChannel: 1 - верхняя линия, 2 - нижняя линия, 3 - центральная линия
            // Серии Bollinger: 0 - верхняя линия, 1 - нижняя линия, 2 - центральная линия
            decimal keltnerUp = keltner.DataSeries[1].Last;
            decimal keltnerDown = keltner.DataSeries[2].Last;
            decimal keltnerCenter = keltner.DataSeries[3].Last;
            decimal lastAtr = atr.DataSeries[0].Last;
            decimal bollingerDown = bollinger.DataSeries[1].Last;

            // нулевые значения = индикатор не прогрет, не торгуем
            if (keltnerUp == 0
                || keltnerDown == 0
                || keltnerCenter == 0
                || lastAtr == 0
                || bollingerDown == 0)
            {
                return;
            }

            // волатильность должна расти: ATR в процентах вырос за последние N свечей
            if (AtrGrows(candles, atr, _shortAtrGrowthCandles.ValueInt, _shortAtrGrowthValue.ValueDecimal) == false)
            {
                return;
            }

            decimal lastClose = candles[candles.Count - 1].Close;

            if (lastClose >= keltnerCenter
                || lastClose <= keltnerDown)
            {
                return;
            }

            // фильтр Bollinger: шорт разрешён только ниже нижней линии
            if (lastClose >= bollingerDown)
            {
                return;
            }

            // дивидендная блокировка: вокруг отсечки в шорт не входим
            if (ShortBlockedByDividends(tab))
            {
                return;
            }

            decimal volume = GetVolume(tab, _shortVolumeType, _shortVolume, _shortTradeAssetInPortfolio);

            if (volume == 0)
            {
                return;
            }

            // перед перевыставлением отменяем предыдущую заявку
            tab.SellAtStopCancel();

            // заявка стоп-маркет: цена активации = нижняя линия Keltner, жизнь заявки - 1 свеча
            tab.SellAtStopMarketIceberg(volume, keltnerDown, keltnerDown,
                StopActivateType.LowerOrEqual, 1, "ShortEntry",
                PositionOpenerToStopLifeTimeType.CandlesCount,
                _shortIcebergOrdersCount.ValueInt, _shortIcebergMillisecondsDistance.ValueInt);
        }

        // Рост волатильности: ATR создан с типом расчёта "Percent".
        // Берём движение ATR за период и считаем, сколько это в процентах
        // от значения ATR на начало периода. Если больше growthValue - волатильность растёт
        private bool AtrGrows(List<Candle> candles, Aindicator atr, int growthCandles, decimal growthValue)
        {
            int lastIndex = candles.Count - 1;
            int backIndex = lastIndex - growthCandles;

            if (backIndex < 0)
            {
                return false;
            }

            decimal atrPercentLast = atr.DataSeries[0].Values[lastIndex];
            decimal atrPercentBack = atr.DataSeries[0].Values[backIndex];

            // нулевое значение = индикатор не прогрет
            if (atrPercentLast == 0
                || atrPercentBack == 0)
            {
                return false;
            }

            // движение ATR за период в процентах от значения на начало периода
            decimal move = atrPercentLast - atrPercentBack;
            decimal growthPercent = move / (atrPercentBack / 100);

            return growthPercent >= growthValue;
        }

        // Логика закрытия лонга. Стоп по нижней линии Keltner, передвигается только в сторону прибыли
        private void LogicCloseLong(BotTabSimple tab, Aindicator keltner, Position position)
        {
            if (position.State != PositionStateType.Open)
            {
                return;
            }

            // выход по противоположной границе канала
            decimal exitPrice = keltner.DataSeries[2].Last;

            if (exitPrice == 0)
            {
                return;
            }

            // перестановка стопа только в сторону прибыли (для лонга - вверх)
            if (position.StopOrderPrice == 0
                || exitPrice > position.StopOrderPrice)
            {
                tab.CloseAtStopMarketIceberg(position, exitPrice,
                    _longIcebergOrdersCount.ValueInt, _longIcebergMillisecondsDistance.ValueInt);
            }
        }

        // Логика закрытия шорта. Стоп по верхней линии Keltner
        private void LogicCloseShort(BotTabSimple tab, Aindicator keltner, Position position)
        {
            if (position.State != PositionStateType.Open)
            {
                return;
            }

            // выход по противоположной границе канала
            decimal exitPrice = keltner.DataSeries[1].Last;

            if (exitPrice == 0)
            {
                return;
            }

            // перестановка стопа только в сторону прибыли (для шорта - вниз)
            if (position.StopOrderPrice == 0
                || exitPrice < position.StopOrderPrice)
            {
                tab.CloseAtStopMarketIceberg(position, exitPrice,
                    _shortIcebergOrdersCount.ValueInt, _shortIcebergMillisecondsDistance.ValueInt);
            }
        }

        // Активация / деактивация стопов открытых позиций.
        // Перевзводим стоп только у позиций в состоянии Open: у позиций, которые уже закрываются
        // или закрыты (Closing, ClosingFail, ClosingSurplus, Done, OpeningFail, Deleted), стоп
        // не трогаем, иначе после срабатывания он будет заново активирован и стоп сработает повторно.
        private void SetStopsActive(List<Position> positions, bool isActive)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (positions[i].StopOrderPrice != 0)
                {
                    positions[i].StopOrderIsActive = isActive;
                }
            }
        }

        #endregion

        #region Dividends (блокировка шортов и автообновление базы)

        // Дивидендная блокировка шорта: вокруг отсечки в шорт не входим.
        // Данных по бумаге нет - шорт разрешён
        private bool ShortBlockedByDividends(BotTabSimple tab)
        {
            if (_shortBlockDuringDividends.ValueBool == false)
            {
                return false;
            }

            if (tab.Security == null
                || string.IsNullOrWhiteSpace(tab.Security.Name))
            {
                return false;
            }

            string ticker = tab.Security.Name;
            DateTime currentTime = tab.TimeServerCurrent;

            // окно блокировки фиксированное: 5 дней до отсечки и 2 дня после

            // ближайшая будущая отсечка: блокируем за 5 дней до неё
            WikiDividendFuture future = WikiMaster.GetDividendsFuture(ticker, currentTime);

            if (future != null
                && future.future != null
                && string.IsNullOrWhiteSpace(future.future.registry_close_date) == false)
            {
                if (DateTime.TryParseExact(future.future.registry_close_date, "dd.MM.yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out DateTime futureDate))
                {
                    if (futureDate.Date >= currentTime.Date
                        && futureDate.Date <= currentTime.AddDays(5).Date)
                    {
                        return true;
                    }
                }
            }

            // ближайшая прошлая отсечка: блокируем ещё 2 дня после неё
            WikiDividendPast past = WikiMaster.GetDividendsPast(ticker, currentTime);

            if (past != null
                && past.past != null
                && string.IsNullOrWhiteSpace(past.past.registry_close_date) == false)
            {
                if (DateTime.TryParseExact(past.past.registry_close_date, "dd.MM.yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out DateTime pastDate))
                {
                    if (pastDate.Date <= currentTime.Date
                        && pastDate.Date >= currentTime.AddDays(-2).Date)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Ручной запуск обновления базы дивидендов (кнопка на вкладке Update)
        private void _startUpdateDividendsButton_UserClickOnButtonEvent()
        {
            try
            {
                if (StartProgram != StartProgram.IsOsTrader)
                {
                    SendNewLogMessage("Manual dividends update is available only in real trading mode", Logging.LogMessageType.Error);
                    return;
                }

                string path = GetDividendsBasePath();

                if (!Directory.Exists(path))
                {
                    SendNewLogMessage($"Dividends directory not found: {path}", Logging.LogMessageType.Error);
                    return;
                }

                if (_dividendsUpdating)
                {
                    SendNewLogMessage("Dividends update is already in progress", Logging.LogMessageType.System);
                    return;
                }

                SendNewLogMessage("Manual dividends update started", Logging.LogMessageType.System);
                StartDividendsUpdate();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        // Ежедневная проверка свежести базы дивидендов (только в реале)
        private void CheckDividendsUpdate(DateTime serverTime)
        {
            try
            {
                if (StartProgram != StartProgram.IsOsTrader)
                {
                    return;
                }

                if (_autoUpdateDividends.ValueString == "Off")
                {
                    return;
                }

                if (_lastDividendsUpdateCheckDate.Date == serverTime.Date)
                {
                    return;
                }

                TimeSpan checkTime = _dividendsUpdateCheckTime.Value.TimeSpan;

                if (serverTime.TimeOfDay < checkTime)
                {
                    return;
                }

                _lastDividendsUpdateCheckDate = serverTime;

                if (!IsDividendsBaseStale(serverTime))
                {
                    return;
                }

                SendNewLogMessage("Dividends base is stale. Starting auto update", Logging.LogMessageType.System);
                StartDividendsUpdate();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void StartDividendsUpdate()
        {
            if (_dividendsUpdating)
            {
                return;
            }

            _dividendsUpdating = true;

            Task.Run(() =>
            {
                try
                {
                    WikiMaster.UpdateDividendsBase();
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
                }
                finally
                {
                    _dividendsUpdating = false;
                    SendNewLogMessage("Dividends update finished", Logging.LogMessageType.System);
                }
            });
        }

        private bool IsDividendsBaseStale(DateTime currentTime)
        {
            try
            {
                string path = GetDividendsBasePath();

                if (!Directory.Exists(path))
                {
                    return true;
                }

                DateTime lastWrite = Directory.GetLastWriteTime(path);
                double ageDays = (currentTime - lastWrite).TotalDays;

                return ageDays > _dividendsMaxAgeDays.ValueInt;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
                return false;
            }
        }

        private string GetDividendsBasePath()
        {
            return AppDomain.CurrentDomain.BaseDirectory + "Wiki\\Dividends";
        }

        #endregion

        #region Volume

        // Метод расчёта объёма входа в позицию
        private decimal GetVolume(BotTabSimple tab,
            StrategyParameterString volumeType, StrategyParameterDecimal volumeParam,
            StrategyParameterString tradeAssetInPortfolio)
        {
            decimal volume = 0;

            if (volumeType.ValueString == "Contracts")
            {
                volume = volumeParam.ValueDecimal;
            }
            else if (volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = volumeParam.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = volumeParam.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Тестер или оптимизатор
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (volumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (tradeAssetInPortfolio.ValueString == "Prime")
                {
                    portfolioPrimeAsset = myPortfolio.ValueCurrent;
                }
                else
                {
                    List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                    if (positionOnBoard == null)
                    {
                        return 0;
                    }

                    for (int i = 0; i < positionOnBoard.Count; i++)
                    {
                        if (positionOnBoard[i].SecurityNameCode == tradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    if (StartProgram != StartProgram.IsOsOptimizer)
                    {
                        SendNewLogMessage("Can`t found portfolio " + tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    }
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (volumeParam.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    if (tab.Security.UsePriceStepCostToCalculateVolume == true
                     && tab.Security.PriceStep != tab.Security.PriceStepCost
                     && tab.PriceBestAsk != 0
                     && tab.Security.PriceStep != 0
                     && tab.Security.PriceStepCost != 0)
                    {// расчёт количества контрактов для фьючерсов и опционов на Мосбирже
                        qty = moneyOnPosition / (tab.PriceBestAsk / tab.Security.PriceStep * tab.Security.PriceStepCost);
                    }
                    qty = Math.Round(qty, tab.Security.DecimalsVolume);
                }
                else
                {
                    qty = Math.Round(qty, 7);
                }

                return qty;
            }

            return volume;
        }

        #endregion
    }
}
