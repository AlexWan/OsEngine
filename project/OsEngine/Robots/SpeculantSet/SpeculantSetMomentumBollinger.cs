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

Конструкция: три источника.
1. BotTabScreener для лонгов (Bollinger + Momentum на каждой бумаге).
2. BotTabScreener для шортов (независимые параметры для отдельной оптимизации).
3. BotTabSimple с индексом Мосбиржи и Envelops — фильтр направления рынка:
индекс выше верхней линии Envelop -> разрешены только лонги, индекс ниже нижней линии -> только шорты.

Покупка:
1. Momentum > минимального значения.
2. Индекс выше верхней линии Envelop (если фильтр по индексу включён).
3. Цена выше центральной линии Bollinger и ниже верхней линии Bollinger.
4. Нет позиции по бумаге и не достигнут лимит позиций скринера.
Вход через BuyAtStopMarketIceberg с ценой активации = верхняя линия Bollinger, время жизни заявки = 1 свеча.

Продажа: зеркально (Momentum < максимального значения, индекс ниже нижней линии Envelop, цена ниже центральной
линии Bollinger и выше нижней линии Bollinger, цена активации = нижняя линия Bollinger).

Выход: CloseAtStopMarketIceberg по нижней линии Bollinger или по центральной (параметр).
Стоп передвигается на каждой закрытой свече только в сторону прибыли и только в торговое время.

Неторговые периоды: торговля 10.00-18.00, в выходные не торгуем. В неторговое время
стоп-заявки на вход отменяются, а стопы открытых позиций деактивируются
(StopOrderIsActive = false), в торговое время всё включается обратно.
 */

namespace OsEngine.Robots.SpeculantSet
{
    [Bot("SpeculantSetMomentumBollinger")] // Создаём атрибут, чтобы ничего не писать в BotFactory
    public class SpeculantSetMomentumBollinger : BotPanel
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
        private StrategyParameterInt _longMomentumPeriod;
        private StrategyParameterDecimal _longMomentumMinValue;
        private StrategyParameterInt _longBollingerLength;
        private StrategyParameterDecimal _longBollingerDeviation;
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
        private StrategyParameterInt _shortMomentumPeriod;
        private StrategyParameterDecimal _shortMomentumMaxValue;
        private StrategyParameterInt _shortBollingerLength;
        private StrategyParameterDecimal _shortBollingerDeviation;
        private StrategyParameterString _shortExitLine;
        private StrategyParameterInt _shortMaxPositions;
        private StrategyParameterInt _shortIcebergOrdersCount;
        private StrategyParameterInt _shortIcebergMillisecondsDistance;
        private StrategyParameterString _shortVolumeType;
        private StrategyParameterDecimal _shortVolume;
        private StrategyParameterString _shortTradeAssetInPortfolio;

        #endregion

        #region Constructor

        public SpeculantSetMomentumBollinger(string name, StartProgram startProgram) : base(name, startProgram)
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
            _longMomentumPeriod = CreateParameter("Long momentum period", 65, 5, 150, 5, "Long");
            _longMomentumMinValue = CreateParameter("Long momentum min value", 102m, 90, 120, 1m, "Long");
            _longBollingerLength = CreateParameter("Long bollinger length", 120, 5, 100, 5, "Long");
            _longBollingerDeviation = CreateParameter("Long bollinger deviation", 2.5m, 1, 4, 0.1m, "Long");
            _longExitLine = CreateParameter("Long exit line", "BollingerDown", new[] { "BollingerDown", "BollingerCenter" }, "Long");
            _longMaxPositions = CreateParameter("Long max positions", 5, 1, 20, 1, "Long");
            _longIcebergOrdersCount = CreateParameter("Long iceberg orders count", 3, 1, 10, 1, "Long");
            _longIcebergMillisecondsDistance = CreateParameter("Long iceberg milliseconds distance", 1000, 500, 10000, 500, "Long");
            _longVolumeType = CreateParameter("Long volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Long");
            _longVolume = CreateParameter("Long volume", 10, 1.0m, 50, 4, "Long");
            _longTradeAssetInPortfolio = CreateParameter("Long trade asset in portfolio", "Prime", "Long");

            // Настройки шорта
            _shortIsOn = CreateParameter("Short is on", true, "Short");
            _shortMomentumPeriod = CreateParameter("Short momentum period", 65, 5, 150, 5, "Short");
            _shortMomentumMaxValue = CreateParameter("Short momentum max value", 100m, 80, 110, 1m, "Short");
            _shortBollingerLength = CreateParameter("Short bollinger length", 120, 5, 100, 5, "Short");
            _shortBollingerDeviation = CreateParameter("Short bollinger deviation", 2.5m, 1, 4, 0.1m, "Short");
            _shortExitLine = CreateParameter("Short exit line", "BollingerUp", new[] { "BollingerUp", "BollingerCenter" }, "Short");
            _shortMaxPositions = CreateParameter("Short max positions", 5, 1, 20, 1, "Short");
            _shortIcebergOrdersCount = CreateParameter("Short iceberg orders count", 3, 1, 10, 1, "Short");
            _shortIcebergMillisecondsDistance = CreateParameter("Short iceberg milliseconds distance", 1000, 500, 10000, 500, "Short");
            _shortVolumeType = CreateParameter("Short volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Short");
            _shortVolume = CreateParameter("Short volume", 10, 1.0m, 50, 4, "Short");
            _shortTradeAssetInPortfolio = CreateParameter("Short trade asset in portfolio", "Prime", "Short");

            // Создаём индикаторы Bollinger и Momentum на лонговом скринере
            _screenerLong.CreateCandleIndicator(1, "Bollinger",
                new List<string>() { _longBollingerLength.ValueInt.ToString(), _longBollingerDeviation.ValueDecimal.ToString() }, "Prime");
            _screenerLong.CreateCandleIndicator(2, "Momentum",
                new List<string>() { _longMomentumPeriod.ValueInt.ToString(), "Close" }, "Second");

            // Создаём индикаторы Bollinger и Momentum на шортовом скринере
            _screenerShort.CreateCandleIndicator(1, "Bollinger",
                new List<string>() { _shortBollingerLength.ValueInt.ToString(), _shortBollingerDeviation.ValueDecimal.ToString() }, "Prime");
            _screenerShort.CreateCandleIndicator(2, "Momentum",
                new List<string>() { _shortMomentumPeriod.ValueInt.ToString(), "Close" }, "Second");

            // Создаём индикатор Envelops на вкладке индекса
            _envelopIndex = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "EnvelopIndex", false);
            _envelopIndex = (Aindicator)_tabIndex.CreateCandleIndicator(_envelopIndex, "Prime");
            _envelopIndex.ParametersDigit[0].Value = _indexEnvelopLength.ValueInt;
            _envelopIndex.ParametersDigit[1].Value = _indexEnvelopDeviation.ValueDecimal;
            _envelopIndex.Save();

            // Подписка на событие изменения параметров пользователем
            ParametrsChangeByUser += SpeculantSetMomentumBollinger_ParametrsChangeByUser;

            DeleteEvent += SpeculantSetMomentumBollinger_DeleteEvent;

            string eng = "Trend momentum robot. Two screeners (long and short) with Bollinger + Momentum, market direction filter by the MOEX index Envelop, entries by stop iceberg orders, exits by stop on the Bollinger line.";
            string ru = "Трендовый импульсный робот. Два скринера (лонг и шорт) с Bollinger + Momentum, фильтр направления рынка по Envelop индекса Мосбиржи, входы стоп-айсберг заявками, выходы стопом по линии Bollinger.";
            Description = OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
        }

        #endregion

        #region Parameters update

        private void SpeculantSetMomentumBollinger_ParametrsChangeByUser()
        {
            _screenerLong._indicators[0].Parameters
                = new List<string>() { _longBollingerLength.ValueInt.ToString(), _longBollingerDeviation.ValueDecimal.ToString() };

            _screenerLong._indicators[1].Parameters
                = new List<string>() { _longMomentumPeriod.ValueInt.ToString(), "Close" };

            _screenerLong.UpdateIndicatorsParameters();

            _screenerShort._indicators[0].Parameters
                = new List<string>() { _shortBollingerLength.ValueInt.ToString(), _shortBollingerDeviation.ValueDecimal.ToString() };

            _screenerShort._indicators[1].Parameters
                = new List<string>() { _shortMomentumPeriod.ValueInt.ToString(), "Close" };

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

        private void SpeculantSetMomentumBollinger_DeleteEvent()
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

                int candlesNeed = Math.Max(_longMomentumPeriod.ValueInt, _longBollingerLength.ValueInt) + 5;

                if (candles.Count < candlesNeed)
                {
                    return;
                }

                Aindicator bollinger = (Aindicator)tab.Indicators[0];
                Aindicator momentum = (Aindicator)tab.Indicators[1];

                if (bollinger.DataSeries[0].Values.Count < candles.Count
                    || momentum.DataSeries[0].Values.Count < candles.Count)
                {
                    return;
                }

                List<Position> positions = tab.PositionsOpenAll;

                if (positions.Count == 0)
                { // Логика открытия
                    LogicOpenLong(candles, tab, bollinger, momentum);
                }
                else
                { // Логика закрытия позиции
                    LogicCloseLong(tab, bollinger, positions[0]);
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

                int candlesNeed = Math.Max(_shortMomentumPeriod.ValueInt, _shortBollingerLength.ValueInt) + 5;

                if (candles.Count < candlesNeed)
                {
                    return;
                }

                Aindicator bollinger = (Aindicator)tab.Indicators[0];
                Aindicator momentum = (Aindicator)tab.Indicators[1];

                if (bollinger.DataSeries[0].Values.Count < candles.Count
                    || momentum.DataSeries[0].Values.Count < candles.Count)
                {
                    return;
                }

                List<Position> positions = tab.PositionsOpenAll;

                if (positions.Count == 0)
                { // Логика открытия
                    LogicOpenShort(candles, tab, bollinger, momentum);
                }
                else
                { // Логика закрытия позиции
                    LogicCloseShort(tab, bollinger, positions[0]);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        // Логика открытия лонга
        private void LogicOpenLong(List<Candle> candles, BotTabSimple tab, Aindicator bollinger, Aindicator momentum)
        {
            int longPositionsCount = _screenerLong.PositionsOpenAll.FindAll(p => p.Direction == Side.Buy).Count;

            if (longPositionsCount >= _longMaxPositions.ValueInt)
            {
                return;
            }

            // Серии Bollinger: 0 - верхняя линия, 1 - нижняя линия, 2 - центральная линия
            decimal bollingerUp = bollinger.DataSeries[0].Last;
            decimal bollingerDown = bollinger.DataSeries[1].Last;
            decimal bollingerCenter = bollinger.DataSeries[2].Last;
            decimal lastMomentum = momentum.DataSeries[0].Last;

            // нулевые значения = индикатор не прогрет, не торгуем
            if (bollingerUp == 0
                || bollingerDown == 0
                || bollingerCenter == 0
                || lastMomentum == 0)
            {
                return;
            }

            if (IndexFilterAllow(Side.Buy) == false)
            {
                return;
            }

            decimal lastClose = candles[candles.Count - 1].Close;

            if (lastMomentum <= _longMomentumMinValue.ValueDecimal)
            {
                return;
            }

            if (lastClose <= bollingerCenter
                || lastClose >= bollingerUp)
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

            // заявка стоп-маркет: цена активации = верхняя линия Bollinger, жизнь заявки - 1 свеча
            tab.BuyAtStopMarketIceberg(volume, bollingerUp, bollingerUp,
                StopActivateType.HigherOrEqual, 1, "LongEntry",
                PositionOpenerToStopLifeTimeType.CandlesCount,
                _longIcebergOrdersCount.ValueInt, _longIcebergMillisecondsDistance.ValueInt);
        }

        // Логика открытия шорта
        private void LogicOpenShort(List<Candle> candles, BotTabSimple tab, Aindicator bollinger, Aindicator momentum)
        {
            int shortPositionsCount = _screenerShort.PositionsOpenAll.FindAll(p => p.Direction == Side.Sell).Count;

            if (shortPositionsCount >= _shortMaxPositions.ValueInt)
            {
                return;
            }

            // Серии Bollinger: 0 - верхняя линия, 1 - нижняя линия, 2 - центральная линия
            decimal bollingerUp = bollinger.DataSeries[0].Last;
            decimal bollingerDown = bollinger.DataSeries[1].Last;
            decimal bollingerCenter = bollinger.DataSeries[2].Last;
            decimal lastMomentum = momentum.DataSeries[0].Last;

            // нулевые значения = индикатор не прогрет, не торгуем
            if (bollingerUp == 0
                || bollingerDown == 0
                || bollingerCenter == 0
                || lastMomentum == 0)
            {
                return;
            }

            if (IndexFilterAllow(Side.Sell) == false)
            {
                return;
            }

            decimal lastClose = candles[candles.Count - 1].Close;

            if (lastMomentum >= _shortMomentumMaxValue.ValueDecimal)
            {
                return;
            }

            if (lastClose >= bollingerCenter
                || lastClose <= bollingerDown)
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

            // заявка стоп-маркет: цена активации = нижняя линия Bollinger, жизнь заявки - 1 свеча
            tab.SellAtStopMarketIceberg(volume, bollingerDown, bollingerDown,
                StopActivateType.LowerOrEqual, 1, "ShortEntry",
                PositionOpenerToStopLifeTimeType.CandlesCount,
                _shortIcebergOrdersCount.ValueInt, _shortIcebergMillisecondsDistance.ValueInt);
        }

        // Логика закрытия лонга. Стоп по линии Bollinger, передвигается только в сторону прибыли
        private void LogicCloseLong(BotTabSimple tab, Aindicator bollinger, Position position)
        {
            if (position.State != PositionStateType.Open)
            {
                return;
            }

            decimal exitPrice = 0;

            if (_longExitLine.ValueString == "BollingerCenter")
            {
                exitPrice = bollinger.DataSeries[2].Last;
            }
            else // "BollingerDown"
            {
                exitPrice = bollinger.DataSeries[1].Last;
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
        private void LogicCloseShort(BotTabSimple tab, Aindicator bollinger, Position position)
        {
            if (position.State != PositionStateType.Open)
            {
                return;
            }

            decimal exitPrice = 0;

            if (_shortExitLine.ValueString == "BollingerCenter")
            {
                exitPrice = bollinger.DataSeries[2].Last;
            }
            else // "BollingerUp"
            {
                exitPrice = bollinger.DataSeries[0].Last;
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
