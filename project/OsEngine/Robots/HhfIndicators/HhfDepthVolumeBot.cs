/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using OsEngine.IndicatorMarketDepth;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;

namespace OsEngine.Robots.HhfIndicators
{
    /// <summary>
    /// Торговый робот на стаканном индикаторе DepthVolume (Simple).
    /// Ловит стену ликвидности: шорт при всплеске AskDepth выше базы, лонг при всплеске BidDepth выше базы.
    /// Стена считается в % от недавней EMA глубины. Выход по тейк-профиту или стоп-лоссу.
    /// </summary>
    [Bot("HhfDepthVolumeBot")]
    public class HhfDepthVolumeBot : BotPanel
    {
        private BotTabSimple _tab;

        private AMarketDepth _depthVolume;

        private Aindicator _sma;

        private NonTradePeriods _nonTradePeriods;

        private double _avgBidDepth;

        private double _avgAskDepth;

        private DateTime _longSignalSince = DateTime.MinValue;

        private DateTime _shortSignalSince = DateTime.MinValue;

        public StrategyParameterDecimal WallPercent;

        public StrategyParameterInt AveragingPeriod;

        public StrategyParameterInt ConfirmationTime;

        public StrategyParameterDecimal TakeProfit;

        public StrategyParameterDecimal StopLoss;

        public StrategyParameterInt Levels;

        public StrategyParameterInt SmaLength;

        public StrategyParameterString VolumeType;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString Regime;

        public HhfDepthVolumeBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort" });
            WallPercent = CreateParameter("WallPercent", 40m, 1m, 500m, 5m);
            AveragingPeriod = CreateParameter("AveragingPeriod", 90, 5, 600, 5);
            ConfirmationTime = CreateParameter("ConfirmationTime", 3, 1, 300, 1);
            TakeProfit = CreateParameter("TakeProfit", 0.5m, 0.05m, 10m, 0.05m);
            StopLoss = CreateParameter("StopLoss", 0.15m, 0.05m, 10m, 0.05m);
            Levels = CreateParameter("Levels", 10, 1, 20, 1);
            SmaLength = CreateParameter("SmaLength", 20, 2, 200, 1);
            VolumeType = CreateParameter("VolumeType", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 10m, 1m, 100m, 1m);

            _depthVolume = _tab.MarketDepthIndicators.CreateIndicator("DepthVolume", "DV", false);

            if (_depthVolume != null)
            {
                ParameterMarketDepthInt levels = (ParameterMarketDepthInt)_depthVolume.Parameters.Find(p => p.Name == "Levels");

                if (levels != null)
                {
                    levels.ValueInt = Levels.ValueInt;
                }
            }

            _sma = (Aindicator)IndicatorsFactory.CreateIndicatorByName("Sma", "SMA", false);
            ((IndicatorParameterInt)_sma.Parameters.Find(p => p.Name == "Length")).ValueInt = SmaLength.ValueInt;
            _tab.CreateCandleIndicator(_sma, "Prime");

            _nonTradePeriods = new NonTradePeriods(name);

            if (_nonTradePeriods.HaveSettingsInFile == false)
            {
                _nonTradePeriods.SetMoexSpotNonTradePeriods();
            }

            StrategyParameterButton nonTradePeriodsButton = CreateParameterButton("Non trade periods");
            nonTradePeriodsButton.UserClickOnButtonEvent += NonTradePeriodsButton_UserClickOnButtonEvent;

            _tab.MarketDepthUpdateEvent += Tab_MarketDepthUpdateEvent;

            _tab.PositionOpeningSuccesEvent += Tab_PositionOpeningSuccesEvent;

            ParametrsChangeByUser += HhfDepthVolumeBot_ParametrsChangeByUser;

            string engDescription = "Liquidity wall robot on DepthVolume. Short when the ask depth is WallPercent percent above its recent average, long when the bid depth is WallPercent percent above its recent average. Exit by take profit or stop loss.";

            string ruDescription = "Робот на стене ликвидности DepthVolume. Шорт, когда глубина асков на WallPercent процентов выше своей недавней средней, лонг, когда глубина бидов на WallPercent процентов выше средней. Выход по тейк-профиту или стоп-лоссу.";

            Description = OsLocalization.ConvertToLocString($"Eng:{engDescription}_Ru:{ruDescription}_");
        }

        private void Tab_MarketDepthUpdateEvent(MarketDepth md)
        {
            try
            {
                if (md == null)
                {
                    return;
                }

                if (_depthVolume == null || _depthVolume.DataSeries.Count < 2
                    || _depthVolume.DataSeries[0].Values.Count == 0
                    || _depthVolume.DataSeries[1].Values.Count == 0)
                {
                    return;
                }

                double bidDepth = (double)_depthVolume.DataSeries[0].Last;
                double askDepth = (double)_depthVolume.DataSeries[1].Last;

                double alpha = 1.0 / Math.Max(1, AveragingPeriod.ValueInt);

                if (_avgBidDepth == 0)
                {
                    _avgBidDepth = bidDepth;
                }
                else
                {
                    _avgBidDepth = _avgBidDepth * (1 - alpha) + bidDepth * alpha;
                }

                if (_avgAskDepth == 0)
                {
                    _avgAskDepth = askDepth;
                }
                else
                {
                    _avgAskDepth = _avgAskDepth * (1 - alpha) + askDepth * alpha;
                }

                if (_tab.PositionsOpenAll.Count > 0)
                {
                    return;
                }

                if (_nonTradePeriods.CanTradeThisTime(_tab.Connector.MarketTime) == false)
                {
                    return;
                }

                if (Regime.ValueString == "Off")
                {
                    return;
                }

                double price = GetCurrentPrice();
                double smaValue = GetSmaValue();

                bool longAllowed = smaValue > 0 && price > smaValue && Regime.ValueString != "OnlyShort";
                bool shortAllowed = smaValue > 0 && price < smaValue && Regime.ValueString != "OnlyLong";

                bool shortWall = askDepth >= _avgAskDepth * (1 + (double)WallPercent.ValueDecimal / 100);
                bool longWall = bidDepth >= _avgBidDepth * (1 + (double)WallPercent.ValueDecimal / 100);

                bool shortCondition = shortWall && shortAllowed;
                bool longCondition = longWall && longAllowed;

                if (shortCondition == false)
                {
                    _shortSignalSince = DateTime.MinValue;
                }

                if (longCondition == false)
                {
                    _longSignalSince = DateTime.MinValue;
                }

                if (shortCondition)
                {
                    if (_shortSignalSince == DateTime.MinValue)
                    {
                        _shortSignalSince = md.Time;
                    }
                    else if ((md.Time - _shortSignalSince).TotalSeconds >= ConfirmationTime.ValueInt)
                    {
                        _tab.SellAtMarket(GetVolume(_tab));
                        _shortSignalSince = DateTime.MinValue;
                    }
                }

                if (longCondition)
                {
                    if (_longSignalSince == DateTime.MinValue)
                    {
                        _longSignalSince = md.Time;
                    }
                    else if ((md.Time - _longSignalSince).TotalSeconds >= ConfirmationTime.ValueInt)
                    {
                        _tab.BuyAtMarket(GetVolume(_tab));
                        _longSignalSince = DateTime.MinValue;
                    }
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void Tab_PositionOpeningSuccesEvent(Position position)
        {
            try
            {
                SetStopAndProfit(position);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void NonTradePeriodsButton_UserClickOnButtonEvent()
        {
            _nonTradePeriods.ShowDialog();
        }

        private double GetCurrentPrice()
        {
            if (_tab.PriceBestBid != 0 && _tab.PriceBestAsk != 0)
            {
                return (double)((_tab.PriceBestBid + _tab.PriceBestAsk) / 2);
            }

            return 0;
        }

        private double GetSmaValue()
        {
            if (_sma == null || _sma.DataSeries.Count == 0)
            {
                return 0;
            }

            if (_tab.CandlesAll == null || _tab.CandlesAll.Count == 0)
            {
                return 0;
            }

            int lastIndex = _tab.CandlesAll.Count - 1;

            if (_sma.DataSeries[0].Values.Count <= lastIndex)
            {
                return 0;
            }

            return (double)_sma.DataSeries[0].Values[lastIndex];
        }

        private void SetStopAndProfit(Position position)
        {
            if (position == null)
            {
                return;
            }

            decimal entryPrice = position.EntryPrice;
            decimal takeProfit = TakeProfit.ValueDecimal;
            decimal stopLoss = StopLoss.ValueDecimal;

            if (position.Direction == Side.Buy)
            {
                decimal profitPrice = entryPrice * (1 + takeProfit / 100);
                decimal stopPrice = entryPrice * (1 - stopLoss / 100);

                _tab.CloseAtProfit(position, profitPrice, profitPrice);
                _tab.CloseAtStop(position, stopPrice, stopPrice);
            }
            else if (position.Direction == Side.Sell)
            {
                decimal profitPrice = entryPrice * (1 - takeProfit / 100);
                decimal stopPrice = entryPrice * (1 + stopLoss / 100);

                _tab.CloseAtProfit(position, profitPrice, profitPrice);
                _tab.CloseAtStop(position, stopPrice, stopPrice);
            }
        }

        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (VolumeType.ValueString == "Contracts")
            {
                volume = Volume.ValueDecimal;
            }
            else if (VolumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;

                if (contractPrice != 0)
                {
                    volume = Volume.ValueDecimal / contractPrice;
                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
            }
            else if (VolumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null || myPortfolio.ValueCurrent == 0)
                {
                    return 0;
                }

                decimal price = tab.PriceBestAsk;

                if (price == 0)
                {
                    price = 1;
                }

                decimal lot = tab.Security.Lot;

                if (lot == 0)
                {
                    lot = 1;
                }

                decimal moneyOnPosition = myPortfolio.ValueCurrent * (Volume.ValueDecimal / 100);
                decimal qty = moneyOnPosition / price / lot;

                if (StartProgram == StartProgram.IsOsTrader)
                {
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

        private void HhfDepthVolumeBot_ParametrsChangeByUser()
        {
            try
            {
                if (_depthVolume == null)
                {
                    return;
                }

                ParameterMarketDepthInt levels = (ParameterMarketDepthInt)_depthVolume.Parameters.Find(p => p.Name == "Levels");

                if (levels != null && levels.ValueInt != Levels.ValueInt)
                {
                    levels.ValueInt = Levels.ValueInt;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public override string GetNameStrategyType()
        {
            return "HhfDepthVolumeBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }
    }
}
