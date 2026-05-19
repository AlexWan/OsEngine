/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Candles.Series;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

namespace OsEngine.Robots.FuturesScreeners
{
    [Bot("FuturesScreenerLrAdaPc")]
    public class FuturesScreenerLrAdaPc : BotPanel
    {
        BotTabScreener _futs1;
        BotTabScreener _futs2;
        BotTabScreener _futs3;
        BotTabScreener _futs4;
        BotTabScreener _futs5;
        BotTabScreener _futs6;
        BotTabScreener _futs7;
        BotTabScreener _futs8;
        BotTabScreener _futs9;
        BotTabScreener _futs10;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _icebergCount;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _lrLength;
        private StrategyParameterDecimal _lrDeviation;
        private StrategyParameterInt _pcAdxLength;
        private StrategyParameterInt _pcRatio;

        private StrategyParameterBool _tradeRegimeSecurity1;
        private StrategyParameterBool _tradeRegimeSecurity2;
        private StrategyParameterBool _tradeRegimeSecurity3;
        private StrategyParameterBool _tradeRegimeSecurity4;
        private StrategyParameterBool _tradeRegimeSecurity5;
        private StrategyParameterBool _tradeRegimeSecurity6;
        private StrategyParameterBool _tradeRegimeSecurity7;
        private StrategyParameterBool _tradeRegimeSecurity8;
        private StrategyParameterBool _tradeRegimeSecurity9;
        private StrategyParameterBool _tradeRegimeSecurity10;

        private bool CanTradeThisSecurity(string securityName)
        {
            if (_futs1.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity1.ValueBool;
            }
            if (_futs2.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity2.ValueBool;
            }
            if (_futs3.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity3.ValueBool;
            }
            if (_futs4.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity4.ValueBool;
            }
            if (_futs5.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity5.ValueBool;
            }
            if (_futs6.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity6.ValueBool;
            }
            if (_futs7.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity7.ValueBool;
            }
            if (_futs8.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity8.ValueBool;
            }
            if (_futs9.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity9.ValueBool;
            }
            if (_futs10.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity10.ValueBool;
            }

            return false;
        }

        // Trade periods
        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodsShowDialogButton;
        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        // Auto connection securities

        public FuturesScreenerLrAdaPc(string name, StartProgram startProgram) : base(name, startProgram)
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
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3End = new TimeOfDay() { Hour = 24, Minute = 00 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3OnOff = true;

            _tradePeriodsSettings.TradeInSunday = false;
            _tradePeriodsSettings.TradeInSaturday = false;

            _tradePeriodsSettings.Load();

            // Basic settings
            _regime = CreateParameter("Regime base", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort" }, "Base");

            _icebergCount = CreateParameter("Iceberg orders count", 1, 1, 3, 1, "Base");
            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods", "Base");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;

            _lrLength = CreateParameter("Linear regression Length", 180, 20, 300, 10, "Base");
            _lrDeviation = CreateParameter("Linear regression deviation", 2.4m, 1, 4, 0.1m, "Base");
            _pcAdxLength = CreateParameter("Pc adx length", 50, 5, 300, 1, "Base");
            _pcRatio = CreateParameter("Pc ratio", 500, 5, 2000, 1, "Base");

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Base");
            _volume = CreateParameter("Volume", 15, 1.0m, 50, 4, "Base");
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Base");

            _tradeRegimeSecurity1 = CreateParameter("Trade security 1", true, "Trade securities");
            _tradeRegimeSecurity2 = CreateParameter("Trade security 2", true, "Trade securities");
            _tradeRegimeSecurity3 = CreateParameter("Trade security 3", true, "Trade securities");
            _tradeRegimeSecurity4 = CreateParameter("Trade security 4", true, "Trade securities");
            _tradeRegimeSecurity5 = CreateParameter("Trade security 5", true, "Trade securities");
            _tradeRegimeSecurity6 = CreateParameter("Trade security 6", true, "Trade securities");
            _tradeRegimeSecurity7 = CreateParameter("Trade security 7", true, "Trade securities");
            _tradeRegimeSecurity8 = CreateParameter("Trade security 8", true, "Trade securities");
            _tradeRegimeSecurity9 = CreateParameter("Trade security 9", true, "Trade securities");
            _tradeRegimeSecurity10 = CreateParameter("Trade security 10", true, "Trade securities");

            // Source creation

            _futs1 = TabCreate<BotTabScreener>();
            _futs1.CandleFinishedEvent += _futs1_CandleFinishedEvent;
            CreateIndicators(_futs1);

            _futs2 = TabCreate<BotTabScreener>();
            _futs2.CandleFinishedEvent += _futs2_CandleFinishedEvent;
            CreateIndicators(_futs2);

            _futs3 = TabCreate<BotTabScreener>();
            _futs3.CandleFinishedEvent += _futs3_CandleFinishedEvent;
            CreateIndicators(_futs3);

            _futs4 = TabCreate<BotTabScreener>();
            _futs4.CandleFinishedEvent += _futs4_CandleFinishedEvent;
            CreateIndicators(_futs4);

            _futs5 = TabCreate<BotTabScreener>();
            _futs5.CandleFinishedEvent += _futs5_CandleFinishedEvent;
            CreateIndicators(_futs5);

            _futs6 = TabCreate<BotTabScreener>();
            _futs6.CandleFinishedEvent += _futs6_CandleFinishedEvent;
            CreateIndicators(_futs6);

            _futs7 = TabCreate<BotTabScreener>();
            _futs7.CandleFinishedEvent += _futs7_CandleFinishedEvent;
            CreateIndicators(_futs7);

            _futs8 = TabCreate<BotTabScreener>();
            _futs8.CandleFinishedEvent += _futs8_CandleFinishedEvent;
            CreateIndicators(_futs8);

            _futs9 = TabCreate<BotTabScreener>();
            _futs9.CandleFinishedEvent += _futs9_CandleFinishedEvent;
            CreateIndicators(_futs9);

            _futs10 = TabCreate<BotTabScreener>();
            _futs10.CandleFinishedEvent += _futs10_CandleFinishedEvent;
            CreateIndicators(_futs10);


            ParametrsChangeByUser += FuturesStartContangoScreener_ParametrsChangeByUser;

            Description = OsLocalization.ConvertToLocString(
              "Eng:A trend screener for futures based on adaptive price channel breakouts with a long linear regression filter. An example of moving futures from one series to another._" +
              "Ru:Трендовый скринер фьючерсов на пробое адаптивного канала прайсченнел с фильтром по длинной линейной регрессии. Пример перекладывания фьючерсов из одной серии в другую_");
        }

        private void FuturesStartContangoScreener_ParametrsChangeByUser()
        {
            UpdateSettingsInIndicators(_futs1);
            UpdateSettingsInIndicators(_futs2);
            UpdateSettingsInIndicators(_futs3);
            UpdateSettingsInIndicators(_futs4);
            UpdateSettingsInIndicators(_futs5);
            UpdateSettingsInIndicators(_futs6);
            UpdateSettingsInIndicators(_futs7);
            UpdateSettingsInIndicators(_futs8);
            UpdateSettingsInIndicators(_futs9);
            UpdateSettingsInIndicators(_futs10);
        }

        private void CreateIndicators(BotTabScreener futuresSource)
        {
            futuresSource.CreateCandleIndicator(1,
                "LinearRegressionChannelFast_Indicator",
                new List<string>() {
                    _lrLength.ValueInt.ToString(), "Close",
                    _lrDeviation.ValueDecimal.ToString(),
                    _lrDeviation.ValueDecimal.ToString()
                }, "Prime");

            futuresSource.CreateCandleIndicator(2,
                "PriceChannelAdaptive",
                new List<string>() { _pcAdxLength.ValueInt.ToString(), _pcRatio.ValueInt.ToString() },
                "Prime");
        }

        private void UpdateSettingsInIndicators(BotTabScreener futuresSource)
        {
            futuresSource._indicators[0].Parameters
              = new List<string>()
             {
                 _lrLength.ValueInt.ToString(),
                 "Close",
                 _lrDeviation.ValueDecimal.ToString(),
                 _lrDeviation.ValueDecimal.ToString()
             };

            futuresSource._indicators[1].Parameters
             = new List<string>() {
                    _pcAdxLength.ValueInt.ToString(),
                    _pcRatio.ValueInt.ToString() };

            futuresSource.UpdateIndicatorsParameters();
        }

        #region Logic Entry

        private void _futs1_CandleFinishedEvent(List<Candle> candles, BotTabSimple arg2)
        {
            TryEntryLogic(_futs1);
        }

        private void _futs2_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_futs2);
        }

        private void _futs3_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_futs3);
        }

        private void _futs4_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_futs4);
        }

        private void _futs5_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_futs5);
        }

        private void _futs6_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_futs6);
        }

        private void _futs7_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_futs7);
        }

        private void _futs8_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_futs8);
        }

        private void _futs9_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_futs9);
        }

        private void _futs10_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_futs10);
        }

        #endregion

        #region Logic

        private void TryEntryLogic(BotTabScreener futuresScreener)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            BotTabSimple futuresSource = GetFuturesToTrade(futuresScreener, futuresScreener.Tabs[0].Connector.MarketTime);

            if (futuresSource == null)
            {
                return;
            }

            List<Candle> futuresCandles = futuresSource.CandlesFinishedOnly;

            if (futuresCandles == null
                || futuresCandles.Count < 20)
            {
                return;
            }

            if (_tradePeriodsSettings.CanTradeThisTime(futuresCandles[^1].TimeStart) == false)
            {
                return;
            }

            if (futuresSource.IsConnected == false
                || futuresSource.IsReadyToTrade == false)
            {
                return;
            }

            if (CanTradeThisSecurity(futuresSource.Security.Name) == false)
            {
                return;
            }

            List<Position> futuresPositions = futuresSource.PositionsOpenAll;

            if (futuresPositions.Count > 0)
            { // вход в логику закрытия позиции
                TryClosePositionLogic(futuresSource, futuresCandles, futuresPositions[0]);
            }
            else
            { // вход в логику открытия позиций
                TryOpenPositionLogic(futuresSource, futuresCandles);
            }
        }

        private BotTabSimple GetFuturesToTrade(BotTabScreener futures, DateTime currentTime)
        {
            /*
            Берём фьюч в пару:
            1) Если уже есть позиция
            2) Берём ближайшую пару фьюч / спот. 
            2.2) Если до ближайшего фьючерса меньше 5 дней до экспирации, не учитываем его как точку входа.
            2.3) Но не дальше чем 4 месяца, на случай если пропущена серия в тестере.
            */

            // 1 берём фьючерс, если по нему уже есть открытая позиция

            for (int i = 0; i < futures.Tabs.Count; i++)
            {
                BotTabSimple currentFutures = futures.Tabs[i];

                if (currentFutures.PositionsOpenAll.Count != 0)
                {
                    return currentFutures;
                }
            }

            // 2 теперь пробуем найти ближайший

            BotTabSimple selectedFutures = null;

            for (int i = 0; i < futures.Tabs.Count; i++)
            {
                Security sec = futures.Tabs[i].Security;

                if (sec == null)
                {
                    continue;
                }

                if (sec.Expiration == DateTime.MinValue)
                {
                    continue;
                }

                double daysByExpiration = (sec.Expiration - currentTime).TotalDays;

                if (daysByExpiration < 3
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
            BotTabSimple futuresSource,
            List<Candle> futuresCandles)
        {
            // 1 берём по обоим вкладкам боллинджеры

            Aindicator lrIndicator = (Aindicator)futuresSource.Indicators[0];

            decimal lrUp = lrIndicator.DataSeries[0].Values[^1];
            decimal lrDown = lrIndicator.DataSeries[2].Values[^1];

            if (lrUp == 0
                || lrDown == 0)
            {
                return;
            }

            Aindicator priceChannel = (Aindicator)futuresSource.Indicators[1];

            decimal linePriceChannelUp = priceChannel.DataSeries[0].Values[^2];
            decimal linePriceChannelDown = priceChannel.DataSeries[1].Values[^2];

            if (linePriceChannelUp == 0
               || linePriceChannelDown == 0)
            {
                return;
            }
            decimal linePriceChannelCentre = linePriceChannelDown + (linePriceChannelUp - linePriceChannelDown) / 2;

            decimal candleClose = futuresCandles[futuresCandles.Count - 1].Close;

            // 2 проверяем условия 

            decimal futuresLastPrice = futuresCandles[^1].Close;

            if (candleClose > linePriceChannelCentre
                && candleClose < linePriceChannelUp
                && candleClose > lrUp
                && _regime.ValueString != "OnlyShort")
            {
                futuresSource.BuyAtStopCancel();
                futuresSource.BuyAtStopMarketIceberg(GetVolume(futuresSource), linePriceChannelUp, linePriceChannelUp,
                    StopActivateType.HigherOrEqual, 1, "", PositionOpenerToStopLifeTimeType.CandlesCount, _icebergCount.ValueInt, 1000);
            }
            else if (candleClose < linePriceChannelCentre
                 && candleClose > linePriceChannelDown
                 && candleClose < lrDown
                 && _regime.ValueString != "OnlyLong")
            {
                futuresSource.SellAtStopCancel();
                futuresSource.SellAtStopMarketIceberg(GetVolume(futuresSource), linePriceChannelDown, linePriceChannelDown,
                    StopActivateType.LowerOrEqual, 1, "", PositionOpenerToStopLifeTimeType.CandlesCount, _icebergCount.ValueInt, 1000);
            }

        }

        private void TryClosePositionLogic(
            BotTabSimple futuresSource,
            List<Candle> futuresCandles,
            Position pos)
        {
            if (StartProgram != StartProgram.IsOsTrader)
            {
                if (pos.State != PositionStateType.Open)
                {// в тестере и оптимизаторе не допускаем спама ордерами
                    return;
                }
            }

            Aindicator lrIndicator = (Aindicator)futuresSource.Indicators[0];

            if (lrIndicator.DataSeries[0].Last == 0)
            {
                return;
            }

            decimal futuresLastPrice = futuresCandles[^1].Close;

            bool needToExit = false;

            if (StartProgram == StartProgram.IsTester
                || StartProgram == StartProgram.IsOsOptimizer)
            {
                if (pos.State != PositionStateType.Open)
                {
                    return;
                }
            }

            Aindicator priceChannel = (Aindicator)futuresSource.Indicators[1];

            decimal lineUp = priceChannel.DataSeries[0].Values[^2];
            decimal lineDown = priceChannel.DataSeries[1].Values[^2];

            if (pos.Direction == Side.Buy)
            {
                if (lineDown == 0)
                {
                    return;
                }

                futuresSource.CloseAtStopMarketIceberg(pos, lineDown, _icebergCount.ValueInt, 1000);
            }
            else if (pos.Direction == Side.Sell)
            {
                if (lineUp == 0)
                {
                    return;
                }

                futuresSource.CloseAtStopMarketIceberg(pos, lineUp, _icebergCount.ValueInt, 1000);
            }

            /*
            DateTime entryTime = pos.TimeOpen;

            DateTime nowTime = futuresCandles[^1].TimeStart;

            TimeSpan timeInPosition = nowTime - entryTime;

            int candlesInPosition = Convert.ToInt32(timeInPosition.TotalMinutes / futuresSource.Connector.TimeFrameBuilder.TimeFrameTimeSpan.TotalMinutes);

            if (candlesInPosition > _extCandlesCount)
            {
                needToExit = true;
            }*/

            double daysByExpiration = (futuresSource.Security.Expiration - futuresCandles[^1].TimeStart).TotalDays;

            if (daysByExpiration < 3)
            {
                needToExit = true;
            }

            if (needToExit == true)
            {
                futuresSource.CloseAtIcebergMarket(pos, pos.OpenVolume, _icebergCount.ValueInt, 1000);
            }
        }

        #endregion

        #region Helpers

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

        #endregion

    }
}
