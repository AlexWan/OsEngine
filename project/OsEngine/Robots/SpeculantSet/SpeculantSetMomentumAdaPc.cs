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

Трендовый импульсный робот.

Конструкция: два источника.
1. BotTabScreener для лонгов (PriceChannelAdaptive + Envelop + Momentum на каждой бумаге).
2. BotTabScreener для шортов (независимые параметры для отдельной оптимизации).

Покупка:
1. Momentum > минимального значения.
2. Цена выше середины канала AdaptivePriceChannel (Up + Down) / 2 и ниже верхней линии канала.
Условия проверяются по значениям линий канала вторым с конца: последнее значение перестраивается
по экстремумам текущей свечи.
3. Фильтр Envelop: цена не ниже верхней линии Envelop, иначе лонг запрещён.
4. Нет позиции по бумаге и не достигнут лимит позиций скринера.
Вход через BuyAtStopMarketIceberg с ценой активации = верхняя линия AdaptivePriceChannel (последнее значение),
время жизни заявки = 1 свеча.

Продажа: зеркально (Momentum < максимального значения, цена ниже середины
канала и выше нижней линии канала, фильтр Envelop: цена не выше нижней линии Envelop,
цена активации = нижняя линия AdaptivePriceChannel).

Выход: CloseAtStopMarketIceberg по нижней линии канала или по середине канала (параметр).
Стоп передвигается на каждой закрытой свече только в сторону прибыли и только в торговое время.

Неторговые периоды: торговля 10.00-18.00, в выходные не торгуем. В неторговое время
стоп-заявки на вход отменяются, а стопы открытых позиций деактивируются
(StopOrderIsActive = false), в торговое время всё включается обратно.
 */

namespace OsEngine.Robots.SpeculantSet
{
    [Bot("SpeculantSetMomentumAdaPc")] // Создаём атрибут, чтобы ничего не писать в BotFactory
    public class SpeculantSetMomentumAdaPc : BotPanel
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
        private StrategyParameterInt _longMomentumPeriod;
        private StrategyParameterDecimal _longMomentumMinValue;
        private StrategyParameterInt _longAdpcAdxPeriod;
        private StrategyParameterInt _longAdpcRatio;
        private StrategyParameterInt _longEnvelopLength;
        private StrategyParameterDecimal _longEnvelopDeviation;
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
        private StrategyParameterInt _shortAdpcAdxPeriod;
        private StrategyParameterInt _shortAdpcRatio;
        private StrategyParameterInt _shortEnvelopLength;
        private StrategyParameterDecimal _shortEnvelopDeviation;
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

        public SpeculantSetMomentumAdaPc(string name, StartProgram startProgram) : base(name, startProgram)
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
            _longMomentumPeriod = CreateParameter("Long momentum period", 65, 5, 150, 5, "Long");
            _longMomentumMinValue = CreateParameter("Long momentum min value", 102m, 90, 120, 1m, "Long");
            _longAdpcAdxPeriod = CreateParameter("Long adpc adx period", 95, 5, 300, 1, "Long");
            _longAdpcRatio = CreateParameter("Long adpc ratio", 640, 5, 2000, 1, "Long");
            _longEnvelopLength = CreateParameter("Long envelop length", 186, 50, 500, 10, "Long");
            _longEnvelopDeviation = CreateParameter("Long envelop deviation", 1.9m, 0.5m, 10, 0.1m, "Long");
            _longMaxPositions = CreateParameter("Long max positions", 5, 1, 20, 1, "Long");
            _longIcebergOrdersCount = CreateParameter("Long iceberg orders count", 3, 1, 10, 1, "Long");
            _longIcebergMillisecondsDistance = CreateParameter("Long iceberg milliseconds distance", 1000, 500, 10000, 500, "Long");
            _longVolumeType = CreateParameter("Long volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Long");
            _longVolume = CreateParameter("Long volume", 7.5m, 1.0m, 50, 4, "Long");
            _longTradeAssetInPortfolio = CreateParameter("Long trade asset in portfolio", "Prime", "Long");

            // Настройки шорта
            _shortIsOn = CreateParameter("Short is on", true, "Short");
            _shortMomentumPeriod = CreateParameter("Short momentum period", 35, 5, 150, 5, "Short");
            _shortMomentumMaxValue = CreateParameter("Short momentum max value", 98m, 80, 110, 1m, "Short");
            _shortAdpcAdxPeriod = CreateParameter("Short adpc adx period", 52, 5, 300, 1, "Short");
            _shortAdpcRatio = CreateParameter("Short adpc ratio", 840, 5, 2000, 1, "Short");
            _shortEnvelopLength = CreateParameter("Short envelop length", 1500, 50, 500, 10, "Short");
            _shortEnvelopDeviation = CreateParameter("Short envelop deviation", 3.7m, 0.5m, 10, 0.1m, "Short");
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

            // Создаём индикаторы PriceChannelAdaptive, Envelops и Momentum на лонговом скринере
            _screenerLong.CreateCandleIndicator(1, "PriceChannelAdaptive",
                new List<string>() { _longAdpcAdxPeriod.ValueInt.ToString(), _longAdpcRatio.ValueInt.ToString() }, "Prime");
            _screenerLong.CreateCandleIndicator(2, "Envelops",
                new List<string>() { _longEnvelopLength.ValueInt.ToString(), _longEnvelopDeviation.ValueDecimal.ToString() }, "Prime");
            _screenerLong.CreateCandleIndicator(3, "Momentum",
                new List<string>() { _longMomentumPeriod.ValueInt.ToString(), "Close" }, "Second");

            // Создаём индикаторы PriceChannelAdaptive, Envelops и Momentum на шортовом скринере
            _screenerShort.CreateCandleIndicator(1, "PriceChannelAdaptive",
                new List<string>() { _shortAdpcAdxPeriod.ValueInt.ToString(), _shortAdpcRatio.ValueInt.ToString() }, "Prime");
            _screenerShort.CreateCandleIndicator(2, "Envelops",
                new List<string>() { _shortEnvelopLength.ValueInt.ToString(), _shortEnvelopDeviation.ValueDecimal.ToString() }, "Prime");
            _screenerShort.CreateCandleIndicator(3, "Momentum",
                new List<string>() { _shortMomentumPeriod.ValueInt.ToString(), "Close" }, "Second");

            // Подписка на событие изменения параметров пользователем
            ParametrsChangeByUser += SpeculantSetMomentumAdaPc_ParametrsChangeByUser;

            DeleteEvent += SpeculantSetMomentumAdaPc_DeleteEvent;

            string eng = "Trend momentum robot. Two screeners (long and short) with Momentum and AdaptivePriceChannel, Envelop entry filter on each security, entries by stop iceberg orders, exits by stop on the AdaptivePriceChannel line.";
            string ru = "Трендовый импульсный робот. Два скринера (лонг и шорт) с Momentum и AdaptivePriceChannel, фильтр входа по Envelop на каждой бумаге, входы стоп-айсберг заявками, выходы стопом по линии AdaptivePriceChannel.";
            Description = OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
        }

        #endregion

        #region Parameters update

        private void SpeculantSetMomentumAdaPc_ParametrsChangeByUser()
        {
            _screenerLong._indicators[0].Parameters
                = new List<string>() { _longAdpcAdxPeriod.ValueInt.ToString(), _longAdpcRatio.ValueInt.ToString() };

            _screenerLong._indicators[1].Parameters
                = new List<string>() { _longEnvelopLength.ValueInt.ToString(), _longEnvelopDeviation.ValueDecimal.ToString() };

            _screenerLong._indicators[2].Parameters
                = new List<string>() { _longMomentumPeriod.ValueInt.ToString(), "Close" };

            _screenerLong.UpdateIndicatorsParameters();

            _screenerShort._indicators[0].Parameters
                = new List<string>() { _shortAdpcAdxPeriod.ValueInt.ToString(), _shortAdpcRatio.ValueInt.ToString() };

            _screenerShort._indicators[1].Parameters
                = new List<string>() { _shortEnvelopLength.ValueInt.ToString(), _shortEnvelopDeviation.ValueDecimal.ToString() };

            _screenerShort._indicators[2].Parameters
                = new List<string>() { _shortMomentumPeriod.ValueInt.ToString(), "Close" };

            _screenerShort.UpdateIndicatorsParameters();
        }

        #endregion

        #region Event handlers

        private void SpeculantSetMomentumAdaPc_DeleteEvent()
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

                int candlesNeed = Math.Max(Math.Max(_longMomentumPeriod.ValueInt, _longAdpcAdxPeriod.ValueInt),
                    _longEnvelopLength.ValueInt) + 5;

                if (candles.Count < candlesNeed)
                {
                    return;
                }

                Aindicator adpc = (Aindicator)tab.Indicators[0];
                Aindicator envelop = (Aindicator)tab.Indicators[1];
                Aindicator momentum = (Aindicator)tab.Indicators[2];

                if (adpc.DataSeries[0].Values.Count < candles.Count
                    || envelop.DataSeries[0].Values.Count < candles.Count
                    || momentum.DataSeries[0].Values.Count < candles.Count)
                {
                    return;
                }

                List<Position> positions = tab.PositionsOpenAll;

                if (positions.Count == 0)
                { // Логика открытия
                    LogicOpenLong(candles, tab, adpc, envelop, momentum);
                }
                else
                { // Логика закрытия позиции
                    LogicCloseLong(tab, adpc, positions[0]);
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

                int candlesNeed = Math.Max(Math.Max(_shortMomentumPeriod.ValueInt, _shortAdpcAdxPeriod.ValueInt),
                    _shortEnvelopLength.ValueInt) + 5;

                if (candles.Count < candlesNeed)
                {
                    return;
                }

                Aindicator adpc = (Aindicator)tab.Indicators[0];
                Aindicator envelop = (Aindicator)tab.Indicators[1];
                Aindicator momentum = (Aindicator)tab.Indicators[2];

                if (adpc.DataSeries[0].Values.Count < candles.Count
                    || envelop.DataSeries[0].Values.Count < candles.Count
                    || momentum.DataSeries[0].Values.Count < candles.Count)
                {
                    return;
                }

                List<Position> positions = tab.PositionsOpenAll;

                if (positions.Count == 0)
                { // Логика открытия
                    LogicOpenShort(candles, tab, adpc, envelop, momentum);
                }
                else
                { // Логика закрытия позиции
                    LogicCloseShort(tab, adpc, positions[0]);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        // Логика открытия лонга
        private void LogicOpenLong(List<Candle> candles, BotTabSimple tab, Aindicator adpc, Aindicator envelop, Aindicator momentum)
        {
            int longPositionsCount = _screenerLong.PositionsOpenAll.FindAll(p => p.Direction == Side.Buy).Count;

            if (longPositionsCount >= _longMaxPositions.ValueInt)
            {
                return;
            }

            // Серии PriceChannelAdaptive: 0 - верхняя линия, 1 - нижняя линия (2 - скрытая служебная)
            // Серии Envelops: 0 - верхняя линия, 1 - центральная линия, 2 - нижняя линия
            decimal adpcUp = adpc.DataSeries[0].Last;
            decimal adpcDown = adpc.DataSeries[1].Last;
            decimal envelopUp = envelop.DataSeries[0].Last;
            decimal envelopDown = envelop.DataSeries[2].Last;
            decimal lastMomentum = momentum.DataSeries[0].Last;

            // нулевые значения = индикатор не прогрет, не торгуем
            if (adpcUp == 0
                || adpcDown == 0
                || envelopUp == 0
                || envelopDown == 0
                || lastMomentum == 0)
            {
                return;
            }

            // условия входа проверяем по линиям канала вторым с конца:
            // последнее значение линий перестраивается по экстремумам текущей свечи
            decimal adpcUpPrev = adpc.DataSeries[0].Values[adpc.DataSeries[0].Values.Count - 2];
            decimal adpcDownPrev = adpc.DataSeries[1].Values[adpc.DataSeries[1].Values.Count - 2];

            if (adpcUpPrev == 0
                || adpcDownPrev == 0)
            {
                return;
            }

            decimal adpcCenterPrev = (adpcUpPrev + adpcDownPrev) / 2;

            decimal lastClose = candles[candles.Count - 1].Close;

            if (lastMomentum <= _longMomentumMinValue.ValueDecimal)
            {
                return;
            }

            if (lastClose <= adpcCenterPrev
                || lastClose >= adpcUpPrev)
            {
                return;
            }

            // фильтр Envelop: цена ниже верхней линии - лонг запрещён
            if (lastClose < envelopUp)
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

            // заявка стоп-маркет: цена активации = верхняя линия канала (последнее значение), жизнь заявки - 1 свеча
            tab.BuyAtStopMarketIceberg(volume, adpcUp, adpcUp,
                StopActivateType.HigherOrEqual, 1, "LongEntry",
                PositionOpenerToStopLifeTimeType.CandlesCount,
                _longIcebergOrdersCount.ValueInt, _longIcebergMillisecondsDistance.ValueInt);
        }

        // Логика открытия шорта
        private void LogicOpenShort(List<Candle> candles, BotTabSimple tab, Aindicator adpc, Aindicator envelop, Aindicator momentum)
        {
            int shortPositionsCount = _screenerShort.PositionsOpenAll.FindAll(p => p.Direction == Side.Sell).Count;

            if (shortPositionsCount >= _shortMaxPositions.ValueInt)
            {
                return;
            }

            // Серии PriceChannelAdaptive: 0 - верхняя линия, 1 - нижняя линия (2 - скрытая служебная)
            // Серии Envelops: 0 - верхняя линия, 1 - центральная линия, 2 - нижняя линия
            decimal adpcUp = adpc.DataSeries[0].Last;
            decimal adpcDown = adpc.DataSeries[1].Last;
            decimal envelopUp = envelop.DataSeries[0].Last;
            decimal envelopDown = envelop.DataSeries[2].Last;
            decimal lastMomentum = momentum.DataSeries[0].Last;

            // нулевые значения = индикатор не прогрет, не торгуем
            if (adpcUp == 0
                || adpcDown == 0
                || envelopUp == 0
                || envelopDown == 0
                || lastMomentum == 0)
            {
                return;
            }

            // условия входа проверяем по линиям канала вторым с конца:
            // последнее значение линий перестраивается по экстремумам текущей свечи
            decimal adpcUpPrev = adpc.DataSeries[0].Values[adpc.DataSeries[0].Values.Count - 2];
            decimal adpcDownPrev = adpc.DataSeries[1].Values[adpc.DataSeries[1].Values.Count - 2];

            if (adpcUpPrev == 0
                || adpcDownPrev == 0)
            {
                return;
            }

            decimal adpcCenterPrev = (adpcUpPrev + adpcDownPrev) / 2;

            decimal lastClose = candles[candles.Count - 1].Close;

            if (lastMomentum >= _shortMomentumMaxValue.ValueDecimal)
            {
                return;
            }

            if (lastClose >= adpcCenterPrev
                || lastClose <= adpcDownPrev)
            {
                return;
            }

            // фильтр Envelop: цена выше нижней линии - шорт запрещён
            if (lastClose > envelopDown)
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

            // заявка стоп-маркет: цена активации = нижняя линия канала (последнее значение), жизнь заявки - 1 свеча
            tab.SellAtStopMarketIceberg(volume, adpcDown, adpcDown,
                StopActivateType.LowerOrEqual, 1, "ShortEntry",
                PositionOpenerToStopLifeTimeType.CandlesCount,
                _shortIcebergOrdersCount.ValueInt, _shortIcebergMillisecondsDistance.ValueInt);
        }

        // Логика закрытия лонга. Стоп по нижней линии канала, передвигается только в сторону прибыли
        private void LogicCloseLong(BotTabSimple tab, Aindicator adpc, Position position)
        {
            if (position.State != PositionStateType.Open)
            {
                return;
            }

            // выход по противоположной границе канала
            decimal exitPrice = adpc.DataSeries[1].Last;

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

        // Логика закрытия шорта. Стоп по верхней линии канала
        private void LogicCloseShort(BotTabSimple tab, Aindicator adpc, Position position)
        {
            if (position.State != PositionStateType.Open)
            {
                return;
            }

            // выход по противоположной границе канала
            decimal exitPrice = adpc.DataSeries[0].Last;

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
