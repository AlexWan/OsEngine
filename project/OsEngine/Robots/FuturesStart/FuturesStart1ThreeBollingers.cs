/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;


/*



*/



namespace OsEngine.Robots.FuturesStart
{
    [Bot("FuturesStart1ThreeBollingers")]
    public class FuturesStart1ThreeBollingers : BotPanel
    {
        BotTabSimple _base1;
        BotTabScreener _futs1;

        BotTabSimple _base2;
        BotTabScreener _futs2;

        BotTabSimple _base3;
        BotTabScreener _futs3;

        BotTabSimple _base4;
        BotTabScreener _futs4;

        BotTabSimple _base5;
        BotTabScreener _futs5;

        BotTabSimple _base6;
        BotTabScreener _futs6;

        BotTabSimple _base7;
        BotTabScreener _futs7;

        BotTabSimple _base8;
        BotTabScreener _futs8;

        BotTabSimple _base9;
        BotTabScreener _futs9;

        BotTabSimple _base10;
        BotTabScreener _futs10;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _icebergCount;
        private StrategyParameterString _regimeExit;

        // boost filter
        private StrategyParameterBool _cointegrationFilterIsOn;
        private StrategyParameterInt _cointegrationFilterFilterLen;
        private StrategyParameterDecimal _cointegrationFilterFilterDeviation;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _bollingerLength;
        private StrategyParameterDecimal _bollingerDeviation;
        private StrategyParameterBool _smaFilterIsOn;
        private StrategyParameterInt _smaFilterLen;

        // Trade periods
        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodsShowDialogButton;
        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        public FuturesStart1ThreeBollingers(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // non trade periods
            _tradePeriodsSettings = new NonTradePeriods(name);

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1Start = new TimeOfDay() { Hour = 0, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1End = new TimeOfDay() { Hour = 10, Minute = 05 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1OnOff = true;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2Start = new TimeOfDay() { Hour = 13, Minute = 54 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2End = new TimeOfDay() { Hour = 14, Minute = 6 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2OnOff = false;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3Start = new TimeOfDay() { Hour = 18, Minute = 30 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3End = new TimeOfDay() { Hour = 23, Minute = 58 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3OnOff = true;

            _tradePeriodsSettings.TradeInSunday = false;
            _tradePeriodsSettings.TradeInSaturday = false;

            _tradePeriodsSettings.Load();

            // Basic settings
            _regime = CreateParameter("Regime base", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort" });
            _regimeExit = CreateParameter("Regime exit", "Both", new[] { "Both", "BollingerOneSide", "ProfitLoss", "BollingerOppositeSide" });
            _cointegrationFilterIsOn = CreateParameter("Boost filter is on", true);
            _cointegrationFilterFilterLen = CreateParameter("Boost filter Len", 50, 50, 300, 10);
            _cointegrationFilterFilterDeviation = CreateParameter("Boost filter deviation", 1.4m, 1, 4, 0.1m);

            _icebergCount = CreateParameter("Iceberg orders count", 1, 1, 3, 1);
            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 10, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _smaFilterIsOn = CreateParameter("Sma filter is on", true);
            _smaFilterLen = CreateParameter("Sma filter Len", 170, 100, 300, 10);
            _bollingerLength = CreateParameter("Bollinger Length", 180, 20, 300, 10);
            _bollingerDeviation = CreateParameter("Bollinger deviation", 2.4m, 1, 4, 0.1m);

            // Source creation

            _base1 = TabCreate<BotTabSimple>();
            _base1.CandleFinishedEvent += _base1_CandleFinishedEvent;
            _futs1 = TabCreate<BotTabScreener>();
            _futs1.CandleFinishedEvent += _futs1_CandleFinishedEvent;
            CreateIndicators(_base1, _futs1); 

            _base2 = TabCreate<BotTabSimple>();
            _base2.CandleFinishedEvent += _base2_CandleFinishedEvent; 
            _futs2 = TabCreate<BotTabScreener>();
            _futs2.CandleFinishedEvent += _futs2_CandleFinishedEvent;
            CreateIndicators(_base2, _futs2);

            _base3 = TabCreate<BotTabSimple>();
            _base3.CandleFinishedEvent += _base3_CandleFinishedEvent;
            _futs3 = TabCreate<BotTabScreener>();
            _futs3.CandleFinishedEvent += _futs3_CandleFinishedEvent;
            CreateIndicators(_base3, _futs3);

            _base4 = TabCreate<BotTabSimple>();
            _base4.CandleFinishedEvent += _base4_CandleFinishedEvent; 
            _futs4 = TabCreate<BotTabScreener>();
            _futs4.CandleFinishedEvent += _futs4_CandleFinishedEvent;
            CreateIndicators(_base4, _futs4);

            _base5 = TabCreate<BotTabSimple>();
            _base5.CandleFinishedEvent += _base5_CandleFinishedEvent; 
            _futs5 = TabCreate<BotTabScreener>();
            _futs5.CandleFinishedEvent += _futs5_CandleFinishedEvent;
            CreateIndicators(_base5, _futs5);

            _base6 = TabCreate<BotTabSimple>();
            _base6.CandleFinishedEvent += _base6_CandleFinishedEvent; 
            _futs6 = TabCreate<BotTabScreener>();
            _futs6.CandleFinishedEvent += _futs6_CandleFinishedEvent;
            CreateIndicators(_base6, _futs6);

            _base7 = TabCreate<BotTabSimple>();
            _base7.CandleFinishedEvent += _base7_CandleFinishedEvent; 
            _futs7 = TabCreate<BotTabScreener>();
            _futs7.CandleFinishedEvent += _futs7_CandleFinishedEvent;
            CreateIndicators(_base7, _futs7);

            _base8 = TabCreate<BotTabSimple>();
            _base8.CandleFinishedEvent += _base8_CandleFinishedEvent;
            _futs8 = TabCreate<BotTabScreener>();
            _futs8.CandleFinishedEvent += _futs8_CandleFinishedEvent;
            CreateIndicators(_base8, _futs8);

            _base9 = TabCreate<BotTabSimple>();
            _base9.CandleFinishedEvent += _base9_CandleFinishedEvent; 
            _futs9 = TabCreate<BotTabScreener>();
            _futs9.CandleFinishedEvent += _futs9_CandleFinishedEvent;
            CreateIndicators(_base9, _futs9);

            _base10 = TabCreate<BotTabSimple>();
            _base10.CandleFinishedEvent += _base10_CandleFinishedEvent; 
            _futs10 = TabCreate<BotTabScreener>();
            _futs10.CandleFinishedEvent += _futs10_CandleFinishedEvent;
            CreateIndicators(_base10, _futs10);

            ParametrsChangeByUser += FuturesStartContangoScreener_ParametrsChangeByUser;
        }

        private void FuturesStartContangoScreener_ParametrsChangeByUser()
        {
            UpdateSettingsInIndicators(_base1, _futs1);
            UpdateSettingsInIndicators(_base2, _futs2);
            UpdateSettingsInIndicators(_base3, _futs3);
            UpdateSettingsInIndicators(_base4, _futs4);
            UpdateSettingsInIndicators(_base5, _futs5);
            UpdateSettingsInIndicators(_base6, _futs6);
            UpdateSettingsInIndicators(_base7, _futs7);
            UpdateSettingsInIndicators(_base8, _futs8);
            UpdateSettingsInIndicators(_base9, _futs9);
            UpdateSettingsInIndicators(_base10, _futs10);
        }

        private void CreateIndicators(BotTabSimple baseSource, BotTabScreener futuresSource)
        {
            Aindicator bollingerInd = IndicatorsFactory.CreateIndicatorByName("Bollinger", "Bollinger", false);
            bollingerInd = (Aindicator)baseSource.CreateCandleIndicator(bollingerInd, "Prime");
            ((IndicatorParameterInt)bollingerInd.Parameters[0]).ValueInt = _bollingerLength.ValueInt;
            ((IndicatorParameterDecimal)bollingerInd.Parameters[1]).ValueDecimal = _bollingerDeviation.ValueDecimal;
            bollingerInd.Save();

            futuresSource.CreateCandleIndicator(1, "Bollinger", new List<string>() { 
                _bollingerLength.ValueInt.ToString(), _bollingerDeviation.ValueDecimal.ToString() }, "Prime");
        }

        private void UpdateSettingsInIndicators(BotTabSimple baseSource, BotTabScreener futuresSource)
        {
            Aindicator bollingerInd = (Aindicator)baseSource.Indicators[0];
            bool isChanged = false;

            if(((IndicatorParameterInt)bollingerInd.Parameters[0]).ValueInt != _bollingerLength.ValueInt)
            {
                ((IndicatorParameterInt)bollingerInd.Parameters[0]).ValueInt = _bollingerLength.ValueInt;
                isChanged = true;
            }
            if(((IndicatorParameterDecimal)bollingerInd.Parameters[1]).ValueDecimal != _bollingerDeviation.ValueDecimal)
            {
                ((IndicatorParameterDecimal)bollingerInd.Parameters[1]).ValueDecimal = _bollingerDeviation.ValueDecimal;
                isChanged = true;
            }
            
            if(isChanged)
            {
                bollingerInd.Save();
                bollingerInd.Reload();
            }

            futuresSource._indicators[0].Parameters
             = new List<string>()
             {
                 _bollingerLength.ValueInt.ToString(),
                 _bollingerDeviation.ValueDecimal.ToString()
             };

            futuresSource.UpdateIndicatorsParameters();
        }

        #region Logic Entry

        private void _futs1_CandleFinishedEvent(List<Candle> candles, BotTabSimple arg2)
        {
            TryEntryLogic(_base1, _futs1);
        }

        private void _base1_CandleFinishedEvent(List<Candle> candles)
        {
            TryEntryLogic(_base1, _futs1);
        }

        private void _futs2_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_base2, _futs2);
        }

        private void _base2_CandleFinishedEvent(List<Candle> obj)
        {
            TryEntryLogic(_base2, _futs2);
        }

        private void _futs3_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_base3, _futs3);
        }

        private void _base3_CandleFinishedEvent(List<Candle> obj)
        {
            TryEntryLogic(_base3, _futs3);
        }

        private void _futs4_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_base4, _futs4);
        }

        private void _base4_CandleFinishedEvent(List<Candle> obj)
        {
            TryEntryLogic(_base4, _futs4);
        }

        private void _futs5_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_base5, _futs5);
        }

        private void _base5_CandleFinishedEvent(List<Candle> obj)
        {
            TryEntryLogic(_base5, _futs5);
        }

        private void _futs6_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_base6, _futs6);
        }

        private void _base6_CandleFinishedEvent(List<Candle> obj)
        {
            TryEntryLogic(_base6, _futs6);
        }

        private void _futs7_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_base7, _futs7);
        }

        private void _base7_CandleFinishedEvent(List<Candle> obj)
        {
            TryEntryLogic(_base7, _futs7);
        }

        private void _futs8_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_base8, _futs8);
        }

        private void _base8_CandleFinishedEvent(List<Candle> obj)
        {
            TryEntryLogic(_base8, _futs8);
        }

        private void _futs9_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_base9, _futs9);
        }

        private void _base9_CandleFinishedEvent(List<Candle> obj)
        {
            TryEntryLogic(_base9, _futs9);
        }

        private void _futs10_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_base10, _futs10);
        }

        private void _base10_CandleFinishedEvent(List<Candle> obj)
        {
            TryEntryLogic(_base10, _futs10);
        }

        #endregion

        #region Logic

        private void TryEntryLogic(BotTabSimple baseSource, BotTabScreener futuresScreener)
        {
            if(_regime.ValueString == "Off")
            {
                return;
            }

            if(baseSource.IsConnected == false
                || baseSource.IsReadyToTrade == false)
            {
                return;
            }

            List<Candle> baseCandles = baseSource.CandlesFinishedOnly;

            if(baseCandles == null 
                || baseCandles.Count < 20)
            {
                return;
            }

            if (_tradePeriodsSettings.CanTradeThisTime(baseCandles[^1].TimeStart) == false)
            {
                return;
            }

            BotTabSimple futuresSource = GetFuturesToTrade(baseSource, futuresScreener, baseCandles[^1].TimeStart);

            if(futuresSource == null)
            {
                return;
            }

            if(futuresSource.IsConnected == false
                || futuresSource.IsReadyToTrade == false)
            {
                return;
            }

            List<Candle> futuresCandles = futuresSource.CandlesFinishedOnly;

            if (futuresCandles == null 
                || futuresCandles.Count < 20)
            {
                return;
            }

            if (futuresCandles[^1].TimeStart != baseCandles[^1].TimeStart)
            {
                return;
            }

            List<Position> futuresPositions = futuresSource.PositionsOpenAll;

            if(futuresPositions.Count > 0)
            { // вход в логику закрытия позиции
                TryClosePositionLogic(baseSource, futuresSource, baseCandles, futuresCandles, futuresPositions[0]);
            }
            else
            { // вход в логику открытия позиций
                TryOpenPositionLogic(baseSource, futuresSource, baseCandles, futuresCandles);
            }
        }

        private BotTabSimple GetFuturesToTrade(BotTabSimple baseSource, BotTabScreener futures, DateTime currentTime)
        {
            /*
            Берём фьюч в пару:
            1) Если уже есть позиция
            2) Берём ближайшую пару фьюч / спот. 
            2.2) Если до ближайшего фьючерса меньше 5 дней до экспирации, не учитываем его как точку входа.
            2.3) Но не дальше чем 4 месяца, на случай если пропущена серия в тестере.
            */

            // 1 берём фьючерс, если по нему уже есть открытая позиция

            for (int i = 0;i < futures.Tabs.Count;i++)
            {
                BotTabSimple currentFutures = futures.Tabs[i];

                if(currentFutures.PositionsOpenAll.Count != 0)
                {
                    return currentFutures;
                }
            }

            // 2 теперь пробуем найти ближайший

            BotTabSimple selectedFutures = null;

            for (int i = 0;i < futures.Tabs.Count;i++)
            {
                Security sec = futures.Tabs[i].Security;

                if(sec.Expiration == DateTime.MinValue)
                {
                    continue;
                }

                double daysByExpiration = (sec.Expiration - currentTime).TotalDays;

                if(daysByExpiration < 3
                    || daysByExpiration > 100)
                {
                    continue;
                }

                if (selectedFutures != null
                    && selectedFutures.Security.Expiration < sec.Expiration)
                {
                    continue;
                }

                selectedFutures = futures.Tabs[i];
            }

            return selectedFutures;
        }

        private void TryOpenPositionLogic(
            BotTabSimple baseSource, 
            BotTabSimple futuresSource,
            List<Candle> baseCandles,
            List<Candle> futuresCandles)
        {
            /*
            
            Шорт:
            1) Фьючерс над боллинджером
            2) Спот под боллинджером
            3) За последний час раздвижка увеличилась
            4) СМА выше цены спота. Т.е. рынок смотрит вниз

            Лонг:
            1) Фьючерс под боллинджером
            2) Спот над боллинджером
            3) За последний час раздвижка уменьшилась
            4) СМА ниже цены спота. Т.е. рынок смотрит вверх

            */

            // 1 берём по обоим вкладкам боллинджеры

            Aindicator baseBollinger = (Aindicator)baseSource.Indicators[0];
            Aindicator futuresBollinger = (Aindicator)futuresSource.Indicators[0];

            if (baseBollinger.DataSeries[0].Last == 0
                || futuresBollinger.DataSeries[0].Last == 0)
            {
                return;
            }

            // 2 считаем положение раздвижки

            if (_cointegrationFilterIsOn.ValueBool == true)
            {
                decimal baseLastPrice = baseCandles[^1].Close;
                decimal futuresLastPrice = futuresCandles[^1].Close;

                CointegrationBuilder cointegrationIndicator = new CointegrationBuilder();
                cointegrationIndicator.CointegrationLookBack = _cointegrationFilterFilterLen.ValueInt;
                cointegrationIndicator.CointegrationDeviation = _cointegrationFilterFilterDeviation.ValueDecimal;
                cointegrationIndicator.ReloadCointegration(baseCandles, futuresCandles, false);

                if (cointegrationIndicator.Cointegration == null
                    || cointegrationIndicator.Cointegration.Count == 0)
                {
                    return;
                }

                if (cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.No)
                { // ускорения прямо сейчас не наблюдается
                    return;
                }

                PercentBoost boosts = CalculateBoosts(futuresCandles);

                 if (_regime.ValueString != "OnlyLong"
                    && futuresLastPrice < futuresBollinger.DataSeries[1].Last
                    && cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Up
                    && boosts.CointegrationLineSideUpPercent >= 80)
                { // 
                    futuresSource.BuyAtIcebergMarket(GetVolume(futuresSource), _icebergCount.ValueInt, 1000);
                }
                if (_regime.ValueString != "OnlyShort"
                    && futuresLastPrice > futuresBollinger.DataSeries[0].Last
                    && cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Down
                    && boosts.CointegrationLineSideDownPercent >= 80)
                { 
                    futuresSource.SellAtIcebergMarket(GetVolume(futuresSource), _icebergCount.ValueInt, 1000);
                }
            }

            // 3 проверяем условия 

           /* decimal baseLastPrice = baseCandles[^1].Close;
            decimal futuresLastPrice = futuresCandles[^1].Close;

            if(_regime.ValueString != "OnlyShort"
                && baseLastPrice > baseBollinger.DataSeries[0].Last          // база выше верхнего боллинджера
                && futuresLastPrice < futuresBollinger.DataSeries[1].Last)   // фьючерс ниже нижнего боллинджера
            {// ШОРТ

                if (_smaFilterIsOn.ValueBool == true)
                {
                    decimal smaValue = Sma(baseCandles, _smaFilterLen.ValueInt, baseCandles.Count - 1);

                    if (baseLastPrice < smaValue)
                    {
                        return;
                    }
                }
                futuresSource.SellAtIcebergMarket(GetVolume(futuresSource), _icebergCount.ValueInt, 1000);
                
            }
            else if (_regime.ValueString != "OnlyLong"
                && baseLastPrice < baseBollinger.DataSeries[1].Last        // база ниже нижнего боллинджера
                && futuresLastPrice > futuresBollinger.DataSeries[0].Last) // фьючерс выше верхнего боллинджера
            {// ЛОНГ
                if (_smaFilterIsOn.ValueBool == true)
                {
                    decimal smaValue = Sma(baseCandles, _smaFilterLen.ValueInt, baseCandles.Count - 1);

                    if (baseLastPrice > smaValue)
                    {
                        return;
                    }
                }

                futuresSource.BuyAtIcebergMarket(GetVolume(futuresSource), _icebergCount.ValueInt, 1000);
            }*/

        }

        private void TryClosePositionLogic(
            BotTabSimple baseSource,
            BotTabSimple futuresSource,
            List<Candle> baseCandles,
            List<Candle> futuresCandles,
            Position pos)
        {

            /*

            Выход:
                   1) И спот и фьючерс с одной стороны боллинджера. Подключаемый
                   2) Фьючерс закрылся с обратной стороны боллинджера. Подключаемый
                   3) Выходим из позиции по фьючу, если до экспирации меньше или равно 2 торговых дня. По любой цене. 

            */
            if (StartProgram != StartProgram.IsOsTrader)
            {
                if(pos.State != PositionStateType.Open)
                {// в тестере и оптимизаторе не допускаем спама ордерами
                    return;
                }
            }

            Aindicator baseBollinger = (Aindicator)baseSource.Indicators[0];
            Aindicator futuresBollinger = (Aindicator)futuresSource.Indicators[0];

            if (baseBollinger.DataSeries[0].Last == 0
                || futuresBollinger.DataSeries[0].Last == 0)
            {
                return;
            }

            decimal baseLastPrice = baseCandles[^1].Close;
            decimal futuresLastPrice = futuresCandles[^1].Close;

            bool needToExit = false;

            if (_regimeExit.ValueString == "Both"
                || _regimeExit.ValueString == "BollingerOneSide")
            {
                if(baseLastPrice > baseBollinger.DataSeries[0].Last          
                && futuresLastPrice > futuresBollinger.DataSeries[0].Last)
                {
                    needToExit = true;
                }

                if (baseLastPrice < baseBollinger.DataSeries[1].Last
                  && futuresLastPrice < futuresBollinger.DataSeries[1].Last)
                {
                    needToExit = true;
                }
            }

            if (_regimeExit.ValueString == "Both"
                || _regimeExit.ValueString == "BollingerOppositeSide")
            {
                if(pos.Direction == Side.Buy
                    && futuresLastPrice > futuresBollinger.DataSeries[0].Last)
                {
                    needToExit = true;
                }

                if (pos.Direction == Side.Sell
                    && futuresLastPrice < futuresBollinger.DataSeries[1].Last)
                {
                    needToExit = true;
                }
            }

            if (_regimeExit.ValueString == "Both"
              || _regimeExit.ValueString == "ProfitLoss")
            {
                if(pos.StopOrderPrice == 0)
                {
                    decimal stopPrice = 0;
                    decimal profitPrice = 0;

                    if (pos.Direction == Side.Buy)
                    {
                        stopPrice = pos.EntryPrice - pos.EntryPrice * 0.004m;
                        profitPrice = pos.EntryPrice + pos.EntryPrice * 0.004m;
                    }
                    else if (pos.Direction == Side.Sell)
                    {
                        stopPrice = pos.EntryPrice + pos.EntryPrice * 0.004m;
                        profitPrice = pos.EntryPrice - pos.EntryPrice * 0.004m;
                    }

                    futuresSource.CloseAtStopMarket(pos, stopPrice);
                    futuresSource.CloseAtProfitMarket(pos, profitPrice);
                }
            }

            double daysByExpiration = (futuresSource.Security.Expiration - futuresCandles[^1].TimeStart).TotalDays;

            if(daysByExpiration < 3)
            {
                needToExit = true;
            }

            if(needToExit == true)
            {
                futuresSource.CloseAtIcebergMarket(pos, pos.OpenVolume, _icebergCount.ValueInt, 1000);
            }
        }

        #endregion

        #region Helpers

        // Method for calculating MovingAverage
        private decimal Sma(List<Candle> candles, int len, int index)
        {
            if (candles.Count == 0
                || index >= candles.Count
                || index <= 0)
            {
                return 0;
            }

            decimal summ = 0;

            int countPoints = 0;

            for (int i = index; i >= 0 && i > index - len; i--)
            {
                countPoints++;
                summ += candles[i].Close;
            }

            if (countPoints == 0)
            {
                return 0;
            }

            return summ / countPoints;
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (_volumeType.ValueString == "Contracts")
            {
                volume = _volume.ValueDecimal;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = _volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = _volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (_volumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (_tradeAssetInPortfolio.ValueString == "Prime")
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
                        if (positionOnBoard[i].SecurityNameCode == _tradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (_volume.ValueDecimal / 100);

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

        public PercentBoost CalculateBoosts(List<Candle> futuresCandles)
        {
            PercentBoost percentBoost = new PercentBoost();

            if(_base1.IsConnected)
            {
                List<Candle> baseCandles = _base1.CandlesFinishedOnly;
                GetCointegrationSide(futuresCandles, baseCandles, percentBoost);
            }
            if (_base2.IsConnected)
            {
                List<Candle> baseCandles = _base2.CandlesFinishedOnly;
                GetCointegrationSide(futuresCandles, baseCandles, percentBoost);
            }
            if (_base3.IsConnected)
            {
                List<Candle> baseCandles = _base3.CandlesFinishedOnly;
                GetCointegrationSide(futuresCandles, baseCandles, percentBoost);
            }
            if (_base4.IsConnected)
            {
                List<Candle> baseCandles = _base4.CandlesFinishedOnly;
                GetCointegrationSide(futuresCandles, baseCandles, percentBoost);
            }
            if (_base5.IsConnected)
            {
                List<Candle> baseCandles = _base5.CandlesFinishedOnly;
                GetCointegrationSide(futuresCandles, baseCandles, percentBoost);
            }
            if (_base6.IsConnected)
            {
                List<Candle> baseCandles = _base6.CandlesFinishedOnly;
                GetCointegrationSide(futuresCandles, baseCandles, percentBoost);
            }
            if (_base7.IsConnected)
            {
                List<Candle> baseCandles = _base7.CandlesFinishedOnly;
                GetCointegrationSide(futuresCandles, baseCandles, percentBoost);
            }
            if (_base8.IsConnected)
            {
                List<Candle> baseCandles = _base8.CandlesFinishedOnly;
                GetCointegrationSide(futuresCandles, baseCandles, percentBoost);
            }
            if (_base9.IsConnected)
            {
                List<Candle> baseCandles = _base9.CandlesFinishedOnly;
                GetCointegrationSide(futuresCandles, baseCandles, percentBoost);
            }
            if (_base10.IsConnected)
            {
                List<Candle> baseCandles = _base10.CandlesFinishedOnly;
                GetCointegrationSide(futuresCandles, baseCandles, percentBoost);
            }

            decimal commonValue = 0;
            commonValue += percentBoost.CountUp;
            commonValue += percentBoost.CountDown;
            commonValue += percentBoost.CountNo;

            if(commonValue == 0)
            {
                return percentBoost;
            }

            percentBoost.CointegrationLineSideDownPercent = percentBoost.CountDown / (commonValue / 100);
            percentBoost.CointegrationLineSideUpPercent = percentBoost.CountUp / (commonValue / 100);
            percentBoost.CointegrationLineSideNonePercent = percentBoost.CountNo / (commonValue / 100);

            return percentBoost;
        }

        private void GetCointegrationSide(List<Candle> futuresCandles, List<Candle> baseCandles, PercentBoost percentBoost)
        {

            if(baseCandles == null 
                || baseCandles.Count < 20)
            {
                return;
            }
            CointegrationBuilder cointegrationIndicator = new CointegrationBuilder();
            cointegrationIndicator.CointegrationLookBack = _cointegrationFilterFilterLen.ValueInt;
            cointegrationIndicator.CointegrationDeviation = _cointegrationFilterFilterDeviation.ValueDecimal;
            cointegrationIndicator.ReloadCointegration(baseCandles, futuresCandles, false);

            if (cointegrationIndicator.Cointegration == null
                || cointegrationIndicator.Cointegration.Count == 0)
            {
                return;
            }

            if(cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Up)
            {
                percentBoost.CountUp += 1;
            }
            else if(cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Down)
            {
                percentBoost.CountDown += 1;
            }
            else if(cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.No)
            {
                percentBoost.CountNo += 1;
            }
        }

        #endregion

    }

    public class PercentBoost
    {
        public decimal CointegrationLineSideDownPercent;

        public decimal CointegrationLineSideUpPercent;

        public decimal CointegrationLineSideNonePercent;

        public int CountDown;

        public int CountUp;

        public int CountNo;

    }
}
