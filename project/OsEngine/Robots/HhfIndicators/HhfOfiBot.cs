/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using OsEngine.IndicatorMarketDepth;
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
    /// Скринер-робот на стаканных индикаторах Ofi и OrderBookImbalance.
    /// Ловит крупный импульс потока заявок (Ofi) и входит, когда перевес стакана на стороне входа.
    /// Лонг — Ofi положительный и импульс выше базы, удерживается ConfirmationTime секунд, дисбаланс выше ImbalanceThreshold.
    /// Шорт — зеркально. Тейк-профит лимитной заявкой, стоп-лосс стоп-заявкой.
    /// </summary>
    [Bot("HhfOfiBot")]
    public class HhfOfiBot : BotPanel
    {
        private BotTabScreener _screenerTab;

        private NonTradePeriods _nonTradePeriods;

        private readonly Dictionary<string, double> _avgAbsOfi = new Dictionary<string, double>();

        private readonly Dictionary<string, DateTime> _longSignalSince = new Dictionary<string, DateTime>();

        private readonly Dictionary<string, DateTime> _shortSignalSince = new Dictionary<string, DateTime>();

        public StrategyParameterString Regime;

        public StrategyParameterDecimal ImpulsePercent;

        public StrategyParameterInt AveragingPeriod;

        public StrategyParameterInt ConfirmationTime;

        public StrategyParameterInt ImbalanceLevels;

        public StrategyParameterDecimal ImbalanceThreshold;

        public StrategyParameterDecimal TakeProfit;

        public StrategyParameterDecimal StopLoss;

        public StrategyParameterString VolumeType;

        public StrategyParameterDecimal Volume;

        public StrategyParameterInt MaxPositions;

        public HhfOfiBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _screenerTab = TabsScreener[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort" });
            ImpulsePercent = CreateParameter("ImpulsePercent", 60m, 1m, 500m, 5m);
            AveragingPeriod = CreateParameter("AveragingPeriod", 60, 5, 600, 5);
            ConfirmationTime = CreateParameter("ConfirmationTime", 2, 1, 300, 1);
            ImbalanceLevels = CreateParameter("ImbalanceLevels", 5, 1, 20, 1);
            ImbalanceThreshold = CreateParameter("ImbalanceThreshold", 0.5m, 0m, 1m, 0.05m);
            TakeProfit = CreateParameter("TakeProfit", 0.1m, 0.05m, 10m, 0.05m);
            StopLoss = CreateParameter("StopLoss", 0.1m, 0.05m, 10m, 0.05m);
            VolumeType = CreateParameter("VolumeType", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 10m, 1m, 100m, 1m);
            MaxPositions = CreateParameter("MaxPositions", 3, 1, 50, 1);

            _screenerTab.CreateMarketDepthIndicator(1, "Ofi", null, false);

            _screenerTab.CreateMarketDepthIndicator(2, "OrderBookImbalance",
                new List<string> { "Simple", ImbalanceLevels.ValueInt.ToString() }, false);

            _nonTradePeriods = new NonTradePeriods(name);

            if (_nonTradePeriods.HaveSettingsInFile == false)
            {
                _nonTradePeriods.SetMoexSpotNonTradePeriods();
            }

            StrategyParameterButton nonTradePeriodsButton = CreateParameterButton("Non trade periods");
            nonTradePeriodsButton.UserClickOnButtonEvent += NonTradePeriodsButton_UserClickOnButtonEvent;

            _screenerTab.MarketDepthUpdateEvent += Screener_MarketDepthUpdateEvent;

            _screenerTab.PositionOpeningSuccesEvent += Screener_PositionOpeningSuccesEvent;

            ParametrsChangeByUser += HhfOfiBot_ParametrsChangeByUser;

            string engDescription = "Screener robot on OFI (order flow impulse) and OrderBookImbalance. Long when OFI is positive, its absolute value is ImpulsePercent percent above the recent average for ConfirmationTime seconds, and the imbalance is above ImbalanceThreshold. Short is mirrored. Take profit by a limit order, stop loss by a stop order.";

            string ruDescription = "Скринер-робот на OFI (импульс потока заявок) и OrderBookImbalance. Лонг, когда OFI положительный, его модуль выше недавнего среднего на ImpulsePercent процентов в течение ConfirmationTime секунд, и дисбаланс выше ImbalanceThreshold. Шорт зеркально. Тейк-профит лимитной заявкой, стоп-лосс стоп-заявкой.";

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

                AMarketDepth ofi = GetMarketDepthIndicator(tab, "Ofi");
                AMarketDepth obi = GetMarketDepthIndicator(tab, "OrderBookImbalance");

                if (ofi == null || obi == null)
                {
                    return;
                }

                if (ofi.DataSeries.Count == 0 || ofi.DataSeries[0].Values.Count == 0)
                {
                    return;
                }

                if (obi.DataSeries.Count == 0 || obi.DataSeries[0].Values.Count == 0)
                {
                    return;
                }

                double ofiValue = (double)ofi.DataSeries[0].Last;
                double imbalance = (double)obi.DataSeries[0].Last;
                double absOfi = Math.Abs(ofiValue);

                string tabName = tab.TabName;

                double alpha = 1.0 / Math.Max(1, AveragingPeriod.ValueInt);
                double avgAbs = GetAvg(_avgAbsOfi, tabName, absOfi, alpha);
                _avgAbsOfi[tabName] = avgAbs;

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

                double impulseLevel = avgAbs * (1 + (double)ImpulsePercent.ValueDecimal / 100);
                double threshold = (double)ImbalanceThreshold.ValueDecimal;

                bool longCondition = ofiValue > 0 && absOfi >= impulseLevel && imbalance >= threshold && Regime.ValueString != "OnlyShort";
                bool shortCondition = ofiValue < 0 && absOfi >= impulseLevel && imbalance <= -threshold && Regime.ValueString != "OnlyLong";

                if (longCondition == false)
                {
                    _longSignalSince.Remove(tabName);
                }

                if (shortCondition == false)
                {
                    _shortSignalSince.Remove(tabName);
                }

                if (CountOpenPositions() >= MaxPositions.ValueInt)
                {
                    return;
                }

                if (longCondition)
                {
                    if (_longSignalSince.ContainsKey(tabName) == false)
                    {
                        _longSignalSince[tabName] = md.Time;
                    }
                    else if ((md.Time - _longSignalSince[tabName]).TotalSeconds >= ConfirmationTime.ValueInt)
                    {
                        tab.BuyAtMarket(GetVolume(tab));
                        _longSignalSince.Remove(tabName);
                    }
                }

                if (shortCondition)
                {
                    if (_shortSignalSince.ContainsKey(tabName) == false)
                    {
                        _shortSignalSince[tabName] = md.Time;
                    }
                    else if ((md.Time - _shortSignalSince[tabName]).TotalSeconds >= ConfirmationTime.ValueInt)
                    {
                        tab.SellAtMarket(GetVolume(tab));
                        _shortSignalSince.Remove(tabName);
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
                if (tab != null)
                {
                    tab.ManualPositionSupport.SecondToCloseIsOn = false;
                    tab.ManualPositionSupport.SecondToOpenIsOn = false;
                }

                SetStopAndProfit(position, tab);
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

        private AMarketDepth GetMarketDepthIndicator(BotTabSimple tab, string typeName)
        {
            for (int i = 0; i < tab.MarketDepthIndicators.Indicators.Count; i++)
            {
                if (tab.MarketDepthIndicators.Indicators[i].GetType().Name == typeName)
                {
                    return tab.MarketDepthIndicators.Indicators[i];
                }
            }

            return null;
        }

        private double GetAvg(Dictionary<string, double> dict, string tabName, double value, double alpha)
        {
            if (dict.ContainsKey(tabName) == false || dict[tabName] == 0)
            {
                return value;
            }

            return dict[tabName] * (1 - alpha) + value * alpha;
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

        private void HhfOfiBot_ParametrsChangeByUser()
        {
            try
            {
                for (int i = 0; i < _screenerTab.Tabs.Count; i++)
                {
                    BotTabSimple tab = _screenerTab.Tabs[i];

                    for (int j = 0; j < tab.MarketDepthIndicators.Indicators.Count; j++)
                    {
                        AMarketDepth indicator = tab.MarketDepthIndicators.Indicators[j];

                        if (indicator.GetType().Name != "OrderBookImbalance")
                        {
                            continue;
                        }

                        ParameterMarketDepthInt levels = (ParameterMarketDepthInt)indicator.Parameters.Find(p => p.Name == "Levels");

                        if (levels != null && levels.ValueInt != ImbalanceLevels.ValueInt)
                        {
                            levels.ValueInt = ImbalanceLevels.ValueInt;
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
            return "HhfOfiBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }
    }
}
