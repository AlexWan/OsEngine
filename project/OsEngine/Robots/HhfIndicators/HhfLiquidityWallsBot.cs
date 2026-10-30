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
using System.Collections.Generic;

namespace OsEngine.Robots.HhfIndicators
{
    /// <summary>
    /// Скринер-робот на пробое «плиты» ликвидности (LiquidityWalls) + канал Келтнера.
    /// Ловит крупную плиту на краю стакана, которая держалась WallPersistenceTime секунд и исчезла:
    /// аск-плита исчезла выше верхней линии Келтнера — лонг, бид-плита исчезла ниже нижней — шорт.
    /// </summary>
    [Bot("HhfLiquidityWallsBot")]
    public class HhfLiquidityWallsBot : BotPanel
    {
        private BotTabScreener _screenerTab;

        private NonTradePeriods _nonTradePeriods;

        private readonly Dictionary<string, double> _avgAskWall = new Dictionary<string, double>();

        private readonly Dictionary<string, double> _avgBidWall = new Dictionary<string, double>();

        private readonly Dictionary<string, DateTime> _askWallSince = new Dictionary<string, DateTime>();

        private readonly Dictionary<string, DateTime> _bidWallSince = new Dictionary<string, DateTime>();

        public StrategyParameterString Regime;

        public StrategyParameterInt Levels;

        public StrategyParameterDecimal WallPercent;

        public StrategyParameterInt WallPersistenceTime;

        public StrategyParameterInt AveragingPeriod;

        public StrategyParameterInt KeltnerEmaPeriod;

        public StrategyParameterDecimal KeltnerMultiplier;

        public StrategyParameterDecimal TakeProfit;

        public StrategyParameterDecimal StopLoss;

        public StrategyParameterString VolumeType;

        public StrategyParameterDecimal Volume;

        public StrategyParameterInt MaxPositions;

        public HhfLiquidityWallsBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _screenerTab = TabsScreener[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort" });
            Levels = CreateParameter("Levels", 20, 1, 50, 1);
            WallPercent = CreateParameter("WallPercent", 50m, 1m, 500m, 5m);
            WallPersistenceTime = CreateParameter("WallPersistenceTime", 10, 1, 300, 1);
            AveragingPeriod = CreateParameter("AveragingPeriod", 60, 5, 600, 5);
            KeltnerEmaPeriod = CreateParameter("KeltnerEmaPeriod", 20, 2, 200, 1);
            KeltnerMultiplier = CreateParameter("KeltnerMultiplier", 2m, 0.1m, 10m, 0.1m);
            TakeProfit = CreateParameter("TakeProfit", 0.2m, 0.05m, 10m, 0.05m);
            StopLoss = CreateParameter("StopLoss", 0.2m, 0.05m, 10m, 0.05m);
            VolumeType = CreateParameter("VolumeType", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 10m, 1m, 100m, 1m);
            MaxPositions = CreateParameter("MaxPositions", 3, 1, 50, 1);

            _screenerTab.CreateMarketDepthIndicator(1, "LiquidityWalls",
                new List<string> { Levels.ValueInt.ToString() }, false);

            _screenerTab.CreateCandleIndicator(1, "KeltnerChannel",
                new List<string> { KeltnerEmaPeriod.ValueInt.ToString(), "20", "20", KeltnerMultiplier.ValueDecimal.ToString() }, "Prime");

            _nonTradePeriods = new NonTradePeriods(name);

            if (_nonTradePeriods.HaveSettingsInFile == false)
            {
                _nonTradePeriods.SetMoexSpotNonTradePeriods();
            }

            StrategyParameterButton nonTradePeriodsButton = CreateParameterButton("Non trade periods");
            nonTradePeriodsButton.UserClickOnButtonEvent += NonTradePeriodsButton_UserClickOnButtonEvent;

            _screenerTab.MarketDepthUpdateEvent += Screener_MarketDepthUpdateEvent;

            _screenerTab.PositionOpeningSuccesEvent += Screener_PositionOpeningSuccesEvent;

            ParametrsChangeByUser += HhfLiquidityWallsBot_ParametrsChangeByUser;

            string engDescription = "Liquidity wall breakout robot on a screener. Trades a large slab on the edge of the order book that held for a while and then vanished. Long when the ask slab vanishes above the upper Keltner line, short when the bid slab vanishes below the lower Keltner line. Exit by take profit or stop loss.";

            string ruDescription = "Скринер-робот на пробое плиты ликвидности. Торгует крупную плиту на краю стакана, которая держалась и исчезла. Лонг, когда исчезла аск-плита выше верхней линии Келтнера, шорт, когда исчезла бид-плита ниже нижней линии Келтнера. Выход по тейк-профиту или стоп-лоссу.";

            Description = OsLocalization.ConvertToLocString($"Eng:{engDescription}_Ru:{ruDescription}_");
        }

        private void Screener_MarketDepthUpdateEvent(MarketDepth md, BotTabSimple tab)
        {
            try
            {
                if (md == null || tab == null)
                {
                    return;
                }

                if (md.Asks.Count == 0 || md.Bids.Count == 0)
                {
                    return;
                }

                if (tab.MarketDepthIndicators.Indicators.Count == 0)
                {
                    return;
                }

                AMarketDepth walls = tab.MarketDepthIndicators.Indicators[0];

                if (walls.DataSeries.Count < 4
                    || walls.DataSeries[0].Values.Count == 0
                    || walls.DataSeries[2].Values.Count == 0)
                {
                    return;
                }

                double bidWall = (double)walls.DataSeries[0].Last;
                double bidWallPrice = (double)walls.DataSeries[1].Last;
                double askWall = (double)walls.DataSeries[2].Last;
                double askWallPrice = (double)walls.DataSeries[3].Last;

                string tabName = tab.TabName;

                double alpha = 1.0 / Math.Max(1, AveragingPeriod.ValueInt);

                double avgBid = GetAvg(_avgBidWall, tabName, bidWall, alpha);
                double avgAsk = GetAvg(_avgAskWall, tabName, askWall, alpha);

                _avgBidWall[tabName] = avgBid;
                _avgAskWall[tabName] = avgAsk;

                double bestAskPrice = md.Asks[0].Price;
                double bestBidPrice = md.Bids[0].Price;

                bool askWallAtEdge = Math.Abs(askWallPrice - bestAskPrice) < 0.000001;
                bool bidWallAtEdge = Math.Abs(bidWallPrice - bestBidPrice) < 0.000001;

                bool askPlate = askWall >= avgAsk * (1 + (double)WallPercent.ValueDecimal / 100) && askWallAtEdge;
                bool bidPlate = bidWall >= avgBid * (1 + (double)WallPercent.ValueDecimal / 100) && bidWallAtEdge;

                if (tab.PositionsOpenAll.Count > 0)
                {
                    return;
                }

                if (_nonTradePeriods.CanTradeThisTime(tab.Connector.MarketTime) == false)
                {
                    return;
                }

                if (Regime.ValueString == "Off")
                {
                    return;
                }

                double price = GetCurrentPrice(tab);
                double upperKeltner = GetKeltner(tab, 1);
                double lowerKeltner = GetKeltner(tab, 2);

                if (askPlate)
                {
                    if (_askWallSince.ContainsKey(tabName) == false)
                    {
                        _askWallSince[tabName] = md.Time;
                    }
                }
                else
                {
                    if (_askWallSince.ContainsKey(tabName))
                    {
                        if ((md.Time - _askWallSince[tabName]).TotalSeconds >= WallPersistenceTime.ValueInt
                            && upperKeltner > 0 && price > upperKeltner
                            && Regime.ValueString != "OnlyShort"
                            && CountOpenPositions() < MaxPositions.ValueInt)
                        {
                            tab.BuyAtMarket(GetVolume(tab));
                        }

                        _askWallSince.Remove(tabName);
                    }
                }

                if (bidPlate)
                {
                    if (_bidWallSince.ContainsKey(tabName) == false)
                    {
                        _bidWallSince[tabName] = md.Time;
                    }
                }
                else
                {
                    if (_bidWallSince.ContainsKey(tabName))
                    {
                        if ((md.Time - _bidWallSince[tabName]).TotalSeconds >= WallPersistenceTime.ValueInt
                            && lowerKeltner > 0 && price < lowerKeltner
                            && Regime.ValueString != "OnlyLong"
                            && CountOpenPositions() < MaxPositions.ValueInt)
                        {
                            tab.SellAtMarket(GetVolume(tab));
                        }

                        _bidWallSince.Remove(tabName);
                    }
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void Screener_PositionOpeningSuccesEvent(Position position, BotTabSimple tab)
        {
            try
            {
                SetStopAndProfit(position, tab);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void SetStopAndProfit(Position position, BotTabSimple tab)
        {
            if (position == null || tab == null)
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

                tab.CloseAtProfit(position, profitPrice, profitPrice);
                tab.CloseAtStop(position, stopPrice, stopPrice);
            }
            else if (position.Direction == Side.Sell)
            {
                decimal profitPrice = entryPrice * (1 - takeProfit / 100);
                decimal stopPrice = entryPrice * (1 + stopLoss / 100);

                tab.CloseAtProfit(position, profitPrice, profitPrice);
                tab.CloseAtStop(position, stopPrice, stopPrice);
            }
        }

        private double GetAvg(Dictionary<string, double> dict, string tabName, double value, double alpha)
        {
            if (dict.ContainsKey(tabName) == false || dict[tabName] == 0)
            {
                return value;
            }

            return dict[tabName] * (1 - alpha) + value * alpha;
        }

        private double GetCurrentPrice(BotTabSimple tab)
        {
            if (tab.PriceBestBid != 0 && tab.PriceBestAsk != 0)
            {
                return (double)((tab.PriceBestBid + tab.PriceBestAsk) / 2);
            }

            return 0;
        }

        private double GetKeltner(BotTabSimple tab, int seriesIndex)
        {
            if (tab.Indicators == null || tab.Indicators.Count == 0)
            {
                return 0;
            }

            Aindicator keltner = (Aindicator)tab.Indicators[0];

            if (keltner.DataSeries.Count <= seriesIndex)
            {
                return 0;
            }

            if (tab.CandlesAll == null || tab.CandlesAll.Count == 0)
            {
                return 0;
            }

            int lastIndex = tab.CandlesAll.Count - 1;

            if (keltner.DataSeries[seriesIndex].Values.Count <= lastIndex)
            {
                return 0;
            }

            return (double)keltner.DataSeries[seriesIndex].Values[lastIndex];
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

        private int CountOpenPositions()
        {
            int count = 0;

            for (int i = 0; i < _screenerTab.Tabs.Count; i++)
            {
                count += _screenerTab.Tabs[i].PositionsOpenAll.Count;
            }

            return count;
        }

        private void NonTradePeriodsButton_UserClickOnButtonEvent()
        {
            _nonTradePeriods.ShowDialog();
        }

        private void HhfLiquidityWallsBot_ParametrsChangeByUser()
        {
            try
            {
                for (int i = 0; i < _screenerTab.Tabs.Count; i++)
                {
                    BotTabSimple tab = _screenerTab.Tabs[i];

                    for (int j = 0; j < tab.MarketDepthIndicators.Indicators.Count; j++)
                    {
                        AMarketDepth indicator = tab.MarketDepthIndicators.Indicators[j];

                        if (indicator.GetType().Name != "LiquidityWalls")
                        {
                            continue;
                        }

                        ParameterMarketDepthInt levels = (ParameterMarketDepthInt)indicator.Parameters.Find(p => p.Name == "Levels");

                        if (levels != null && levels.ValueInt != Levels.ValueInt)
                        {
                            levels.ValueInt = Levels.ValueInt;
                        }
                    }

                    if (tab.Indicators == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < tab.Indicators.Count; j++)
                    {
                        Aindicator indicator = (Aindicator)tab.Indicators[j];

                        if (indicator.GetType().Name != "KeltnerChannel")
                        {
                            continue;
                        }

                        IndicatorParameterInt emaPeriod = (IndicatorParameterInt)indicator.Parameters.Find(p => p.Name == "EMA Period");
                        IndicatorParameterDecimal multiplier = (IndicatorParameterDecimal)indicator.Parameters.Find(p => p.Name == "ATR multiplier");

                        if (emaPeriod != null && emaPeriod.ValueInt != KeltnerEmaPeriod.ValueInt)
                        {
                            emaPeriod.ValueInt = KeltnerEmaPeriod.ValueInt;
                        }

                        if (multiplier != null && multiplier.ValueDecimal != KeltnerMultiplier.ValueDecimal)
                        {
                            multiplier.ValueDecimal = KeltnerMultiplier.ValueDecimal;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public override string GetNameStrategyType()
        {
            return "HhfLiquidityWallsBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }
    }
}
