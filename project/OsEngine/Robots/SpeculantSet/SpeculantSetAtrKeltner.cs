/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Торговый робот для OsEngine

Трендовый импульсный робот с фильтром направления рынка по индексу Мосбиржи.
Импульс определяется по росту волатильности (ATR), уровни - по каналу Кельтнера.

Конструкция: три источника.
1. BotTabScreener для лонгов (KeltnerChannel + ATR на каждой бумаге).
2. BotTabScreener для шортов (независимые параметры для отдельной оптимизации).
3. BotTabSimple с индексом Мосбиржи и Envelops — фильтр направления рынка:
индекс выше верхней линии Envelop -> разрешены только лонги, индекс ниже нижней линии -> только шорты.

Покупка:
1. ATR (индикатор с типом расчёта Percent) вырос на заданную величину за последние N свечей.
2. Индекс выше верхней линии Envelop (если фильтр по индексу включён).
3. Цена выше центральной линии Keltner и ниже верхней линии Keltner.
4. Нет позиции по бумаге и не достигнут лимит позиций скринера.
Вход через BuyAtStopMarketIceberg с ценой активации = верхняя линия Keltner, время жизни заявки = 1 свеча.

Продажа: зеркально (рост ATR тот же - волатильность растёт в обе стороны, индекс ниже нижней линии Envelop,
цена ниже центральной линии Keltner и выше нижней линии Keltner, цена активации = нижняя линия Keltner).

Выход: CloseAtStopMarketIceberg по нижней линии Keltner или по центральной (параметр).
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
        private BotTabSimple _tabIndex;

        // Envelop на вкладке индекса
        private Aindicator _envelopIndex;

        #endregion

        #region Parameters Base

        private StrategyParameterString _regime;
        private StrategyParameterBool _indexFilterIsOn;
        private StrategyParameterInt _indexEnvelopLength;
        private StrategyParameterDecimal _indexEnvelopDeviation;
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
        private StrategyParameterString _longExitLine;
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
        private StrategyParameterString _shortExitLine;
        private StrategyParameterInt _shortMaxPositions;
        private StrategyParameterInt _shortIcebergOrdersCount;
        private StrategyParameterInt _shortIcebergMillisecondsDistance;
        private StrategyParameterString _shortVolumeType;
        private StrategyParameterDecimal _shortVolume;
        private StrategyParameterString _shortTradeAssetInPortfolio;

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

            TabCreate(BotTabType.Simple);
            _tabIndex = TabsSimple[0];

            // Подписка на события завершения свечей
            _screenerLong.CandleFinishedEvent += _screenerLong_CandleFinishedEvent;
            _screenerShort.CandleFinishedEvent += _screenerShort_CandleFinishedEvent;

            // Подписка на события позиций
            _screenerLong.PositionOpeningSuccesEvent += _screenerLong_PositionOpeningSuccesEvent;
            _screenerShort.PositionOpeningSuccesEvent += _screenerShort_PositionOpeningSuccesEvent;

            // Базовые настройки
            _regime = CreateParameter("Regime", "Off", new[] { "On", "Off", "OnlyLong", "OnlyShort" }, "Base");
            _indexFilterIsOn = CreateParameter("Index filter", true, "Base");
            _indexEnvelopLength = CreateParameter("Index envelop length", 100, 50, 500, 10, "Base");
            _indexEnvelopDeviation = CreateParameter("Index envelop deviation", 0.4m, 0.5m, 10, 0.1m, "Base");
            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods", "Base");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;

            // Настройки лонга
            _longIsOn = CreateParameter("Long is on", true, "Long");
            _longAtrPeriod = CreateParameter("Long atr period", 14, 5, 100, 1, "Long");
            _longAtrGrowthCandles = CreateParameter("Long atr growth candles", 10, 2, 50, 1, "Long");
            _longAtrGrowthValue = CreateParameter("Long atr growth value", 2.0m, 0.5m, 10, 0.5m, "Long");
            _longKeltnerEmaLength = CreateParameter("Long keltner ema length", 20, 5, 100, 5, "Long");
            _longKeltnerAtrLength = CreateParameter("Long keltner atr length", 10, 5, 100, 5, "Long");
            _longKeltnerDeviation = CreateParameter("Long keltner deviation", 2.0m, 1, 4, 0.1m, "Long");
            _longExitLine = CreateParameter("Long exit line", "KeltnerDown", new[] { "KeltnerDown", "KeltnerCenter" }, "Long");
            _longMaxPositions = CreateParameter("Long max positions", 5, 1, 20, 1, "Long");
            _longIcebergOrdersCount = CreateParameter("Long iceberg orders count", 3, 1, 10, 1, "Long");
            _longIcebergMillisecondsDistance = CreateParameter("Long iceberg milliseconds distance", 1000, 500, 10000, 500, "Long");
            _longVolumeType = CreateParameter("Long volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Long");
            _longVolume = CreateParameter("Long volume", 10, 1.0m, 50, 4, "Long");
            _longTradeAssetInPortfolio = CreateParameter("Long trade asset in portfolio", "Prime", "Long");

            // Настройки шорта
            _shortIsOn = CreateParameter("Short is on", true, "Short");
            _shortAtrPeriod = CreateParameter("Short atr period", 14, 5, 100, 1, "Short");
            _shortAtrGrowthCandles = CreateParameter("Short atr growth candles", 10, 2, 50, 1, "Short");
            _shortAtrGrowthValue = CreateParameter("Short atr growth value", 2.0m, 0.5m, 10, 0.5m, "Short");
            _shortKeltnerEmaLength = CreateParameter("Short keltner ema length", 20, 5, 100, 5, "Short");
            _shortKeltnerAtrLength = CreateParameter("Short keltner atr length", 10, 5, 100, 5, "Short");
            _shortKeltnerDeviation = CreateParameter("Short keltner deviation", 2.0m, 1, 4, 0.1m, "Short");
            _shortExitLine = CreateParameter("Short exit line", "KeltnerUp", new[] { "KeltnerUp", "KeltnerCenter" }, "Short");
            _shortMaxPositions = CreateParameter("Short max positions", 5, 1, 20, 1, "Short");
            _shortIcebergOrdersCount = CreateParameter("Short iceberg orders count", 3, 1, 10, 1, "Short");
            _shortIcebergMillisecondsDistance = CreateParameter("Short iceberg milliseconds distance", 1000, 500, 10000, 500, "Short");
            _shortVolumeType = CreateParameter("Short volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Short");
            _shortVolume = CreateParameter("Short volume", 10, 1.0m, 50, 4, "Short");
            _shortTradeAssetInPortfolio = CreateParameter("Short trade asset in portfolio", "Prime", "Short");

            // Создаём индикаторы KeltnerChannel и ATR на лонговом скринере
            _screenerLong.CreateCandleIndicator(1, "KeltnerChannel",
                new List<string>() { _longKeltnerEmaLength.ValueInt.ToString(), _longKeltnerAtrLength.ValueInt.ToString(),
                    _longKeltnerAtrLength.ValueInt.ToString(), _longKeltnerDeviation.ValueDecimal.ToString(), "Close" }, "Prime");
            _screenerLong.CreateCandleIndicator(2, "ATR",
                new List<string>() { _longAtrPeriod.ValueInt.ToString(), "Percent" }, "Second");

            // Создаём индикаторы KeltnerChannel и ATR на шортовом скринере
            _screenerShort.CreateCandleIndicator(1, "KeltnerChannel",
                new List<string>() { _shortKeltnerEmaLength.ValueInt.ToString(), _shortKeltnerAtrLength.ValueInt.ToString(),
                    _shortKeltnerAtrLength.ValueInt.ToString(), _shortKeltnerDeviation.ValueDecimal.ToString(), "Close" }, "Prime");
            _screenerShort.CreateCandleIndicator(2, "ATR",
                new List<string>() { _shortAtrPeriod.ValueInt.ToString(), "Percent" }, "Second");

            // Создаём индикатор Envelops на вкладке индекса
            _envelopIndex = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "EnvelopIndex", false);
            _envelopIndex = (Aindicator)_tabIndex.CreateCandleIndicator(_envelopIndex, "Prime");
            _envelopIndex.ParametersDigit[0].Value = _indexEnvelopLength.ValueInt;
            _envelopIndex.ParametersDigit[1].Value = _indexEnvelopDeviation.ValueDecimal;
            _envelopIndex.Save();

            // Подписка на событие изменения параметров пользователем
            ParametrsChangeByUser += SpeculantSetAtrKeltner_ParametrsChangeByUser;

            DeleteEvent += SpeculantSetAtrKeltner_DeleteEvent;

            string eng = "Trend volatility robot. Two screeners (long and short) with KeltnerChannel + ATR, market direction filter by the MOEX index Envelop, entries by stop iceberg orders, exits by stop on the Keltner line.";
            string ru = "Трендовый робот на росте волатильности. Два скринера (лонг и шорт) с каналом Кельтнера + ATR, фильтр направления рынка по Envelop индекса Мосбиржи, входы стоп-айсберг заявками, выходы стопом по линии Кельтнера.";
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

            _screenerLong.UpdateIndicatorsParameters();

            _screenerShort._indicators[0].Parameters
                = new List<string>() { _shortKeltnerEmaLength.ValueInt.ToString(), _shortKeltnerAtrLength.ValueInt.ToString(),
                    _shortKeltnerAtrLength.ValueInt.ToString(), _shortKeltnerDeviation.ValueDecimal.ToString(), "Close" };

            _screenerShort._indicators[1].Parameters
                = new List<string>() { _shortAtrPeriod.ValueInt.ToString(), "Percent" };

            _screenerShort.UpdateIndicatorsParameters();

            if (_envelopIndex.ParametersDigit[0].Value != _indexEnvelopLength.ValueInt
                || _envelopIndex.ParametersDigit[1].Value != _indexEnvelopDeviation.ValueDecimal)
            {
                _envelopIndex.ParametersDigit[0].Value = _indexEnvelopLength.ValueInt;
                _envelopIndex.ParametersDigit[1].Value = _indexEnvelopDeviation.ValueDecimal;
                _envelopIndex.Save();
                _envelopIndex.Reload();
            }
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
                    || _regime.ValueString == "OnlyShort"
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

                int candlesNeed = Math.Max(_longAtrPeriod.ValueInt,
                    Math.Max(_longKeltnerEmaLength.ValueInt, _longKeltnerAtrLength.ValueInt))
                    + _longAtrGrowthCandles.ValueInt + 5;

                if (candles.Count < candlesNeed)
                {
                    return;
                }

                Aindicator keltner = (Aindicator)tab.Indicators[0];
                Aindicator atr = (Aindicator)tab.Indicators[1];

                if (keltner.DataSeries[0].Values.Count < candles.Count
                    || atr.DataSeries[0].Values.Count < candles.Count)
                {
                    return;
                }

                List<Position> positions = tab.PositionsOpenAll;

                if (positions.Count == 0)
                { // Логика открытия
                    LogicOpenLong(candles, tab, keltner, atr);
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
                    || _regime.ValueString == "OnlyLong"
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
                    Math.Max(_shortKeltnerEmaLength.ValueInt, _shortKeltnerAtrLength.ValueInt))
                    + _shortAtrGrowthCandles.ValueInt + 5;

                if (candles.Count < candlesNeed)
                {
                    return;
                }

                Aindicator keltner = (Aindicator)tab.Indicators[0];
                Aindicator atr = (Aindicator)tab.Indicators[1];

                if (keltner.DataSeries[0].Values.Count < candles.Count
                    || atr.DataSeries[0].Values.Count < candles.Count)
                {
                    return;
                }

                List<Position> positions = tab.PositionsOpenAll;

                if (positions.Count == 0)
                { // Логика открытия
                    LogicOpenShort(candles, tab, keltner, atr);
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
        private void LogicOpenLong(List<Candle> candles, BotTabSimple tab, Aindicator keltner, Aindicator atr)
        {
            int longPositionsCount = _screenerLong.PositionsOpenAll.FindAll(p => p.Direction == Side.Buy).Count;

            if (longPositionsCount >= _longMaxPositions.ValueInt)
            {
                return;
            }

            // Серии KeltnerChannel: 1 - верхняя линия, 2 - нижняя линия, 3 - центральная линия
            decimal keltnerUp = keltner.DataSeries[1].Last;
            decimal keltnerDown = keltner.DataSeries[2].Last;
            decimal keltnerCenter = keltner.DataSeries[3].Last;
            decimal lastAtr = atr.DataSeries[0].Last;

            // нулевые значения = индикатор не прогрет, не торгуем
            if (keltnerUp == 0
                || keltnerDown == 0
                || keltnerCenter == 0
                || lastAtr == 0)
            {
                return;
            }

            if (IndexFilterAllow(Side.Buy) == false)
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
        private void LogicOpenShort(List<Candle> candles, BotTabSimple tab, Aindicator keltner, Aindicator atr)
        {
            int shortPositionsCount = _screenerShort.PositionsOpenAll.FindAll(p => p.Direction == Side.Sell).Count;

            if (shortPositionsCount >= _shortMaxPositions.ValueInt)
            {
                return;
            }

            // Серии KeltnerChannel: 1 - верхняя линия, 2 - нижняя линия, 3 - центральная линия
            decimal keltnerUp = keltner.DataSeries[1].Last;
            decimal keltnerDown = keltner.DataSeries[2].Last;
            decimal keltnerCenter = keltner.DataSeries[3].Last;
            decimal lastAtr = atr.DataSeries[0].Last;

            // нулевые значения = индикатор не прогрет, не торгуем
            if (keltnerUp == 0
                || keltnerDown == 0
                || keltnerCenter == 0
                || lastAtr == 0)
            {
                return;
            }

            if (IndexFilterAllow(Side.Sell) == false)
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

        // Логика закрытия лонга. Стоп по линии Keltner, передвигается только в сторону прибыли
        private void LogicCloseLong(BotTabSimple tab, Aindicator keltner, Position position)
        {
            if (position.State != PositionStateType.Open)
            {
                return;
            }

            decimal exitPrice = 0;

            if (_longExitLine.ValueString == "KeltnerCenter")
            {
                exitPrice = keltner.DataSeries[3].Last;
            }
            else // "KeltnerDown"
            {
                exitPrice = keltner.DataSeries[2].Last;
            }

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

        // Логика закрытия шорта
        private void LogicCloseShort(BotTabSimple tab, Aindicator keltner, Position position)
        {
            if (position.State != PositionStateType.Open)
            {
                return;
            }

            decimal exitPrice = 0;

            if (_shortExitLine.ValueString == "KeltnerCenter")
            {
                exitPrice = keltner.DataSeries[3].Last;
            }
            else // "KeltnerUp"
            {
                exitPrice = keltner.DataSeries[1].Last;
            }

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

        // Фильтр направления рынка по индексу Мосбиржи.
        // Индекс выше верхней линии Envelop -> разрешены лонги, ниже нижней линии -> разрешены шорты
        private bool IndexFilterAllow(Side side)
        {
            if (_indexFilterIsOn.ValueBool == false)
            {
                return true;
            }

            List<Candle> indexCandles = _tabIndex.CandlesAll;

            // фильтр включён, а данных индекса нет - не торгуем
            if (indexCandles == null
                || indexCandles.Count < _indexEnvelopLength.ValueInt + 5)
            {
                return false;
            }

            // серии Envelops: 0 - верхняя линия, 2 - нижняя линия
            if (_envelopIndex.DataSeries[0].Values.Count < indexCandles.Count
                || _envelopIndex.DataSeries[2].Values.Count < indexCandles.Count)
            {
                return false;
            }

            decimal envelopUp = _envelopIndex.DataSeries[0].Last;
            decimal envelopDown = _envelopIndex.DataSeries[2].Last;

            // нулевые значения = индикатор не прогрет, не торгуем
            if (envelopUp == 0
                || envelopDown == 0)
            {
                return false;
            }

            decimal lastIndexClose = indexCandles[indexCandles.Count - 1].Close;

            if (side == Side.Buy)
            {
                return lastIndexClose > envelopUp;
            }

            return lastIndexClose < envelopDown;
        }

        // Активация / деактивация стопов открытых позиций
        private void SetStopsActive(List<Position> positions, bool isActive)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].StopOrderPrice != 0)
                {
                    positions[i].StopOrderIsActive = isActive;
                }
            }
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
