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
    /// Торговый робот на скринере: стаканный OrderBookImbalance + свечной Боллинджер (минутка).
    /// Лонг — перевес покупателей выше LongThreshold и цена ниже нижней линии Боллинджера.
    /// Шорт — перевес продавцов ниже ShortThreshold и цена выше верхней линии Боллинджера.
    /// Выход — по возврату цены к центральной линии Боллинджера.
    /// </summary>
    [Bot("HhfOrderBookImbalanceScreener")]
    public class HhfOrderBookImbalanceScreener : BotPanel
    {
        private BotTabScreener _screenerTab;

        private readonly Dictionary<string, DateTime> _longSignalSince = new Dictionary<string, DateTime>();

        private readonly Dictionary<string, DateTime> _shortSignalSince = new Dictionary<string, DateTime>();

        public StrategyParameterDecimal LongThreshold;

        public StrategyParameterDecimal ShortThreshold;

        public StrategyParameterInt ConfirmationTime;

        public StrategyParameterString VolumeType;

        public StrategyParameterDecimal Volume;

        public StrategyParameterInt MaxPositions;

        public StrategyParameterInt Levels;

        public StrategyParameterString Mode;

        public StrategyParameterInt BollingerLength;

        public StrategyParameterDecimal BollingerDeviation;

        public HhfOrderBookImbalanceScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _screenerTab = TabsScreener[0];

            LongThreshold = CreateParameter("LongThreshold", 0.6m, 0m, 1m, 0.05m);
            ShortThreshold = CreateParameter("ShortThreshold", -0.6m, -1m, 0m, 0.05m);
            ConfirmationTime = CreateParameter("ConfirmationTime", 2, 1, 300, 1);
            VolumeType = CreateParameter("VolumeType", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 10m, 1m, 100m, 1m);
            MaxPositions = CreateParameter("MaxPositions", 2, 1, 50, 1);
            Levels = CreateParameter("Levels", 5, 1, 20, 1);
            Mode = CreateParameter("Mode", "Simple", new[] { "Simple", "Weighted" });
            BollingerLength = CreateParameter("BollingerLength", 21, 2, 200, 1);
            BollingerDeviation = CreateParameter("BollingerDeviation", 2m, 0.1m, 10m, 0.1m);

            _screenerTab.CreateMarketDepthIndicator(1, "OrderBookImbalance",
                new List<string> { Mode.ValueString, Levels.ValueInt.ToString() }, false);

            _screenerTab.CreateCandleIndicator(1, "Bollinger",
                new List<string> { BollingerLength.ValueInt.ToString(), BollingerDeviation.ValueDecimal.ToString() }, "Prime");

            _screenerTab.MarketDepthUpdateEvent += Screener_MarketDepthUpdateEvent;

            ParametrsChangeByUser += HhfOrderBookImbalanceScreener_ParametrsChangeByUser;

            string engDescription = "Screener robot on OrderBookImbalance and Bollinger. Long when imbalance is above LongThreshold and price is below the lower band. Short when imbalance is below ShortThreshold and price is above the upper band. Exit at the centre line.";

            string ruDescription = "Скринер-робот на OrderBookImbalance и Боллинджере. Лонг, когда дисбаланс выше LongThreshold и цена ниже нижней линии. Шорт, когда дисбаланс ниже ShortThreshold и цена выше верхней линии. Выход по центральной линии.";

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

                if (tab.MarketDepthIndicators.Indicators.Count == 0)
                {
                    return;
                }

                AMarketDepth imbalanceIndicator = tab.MarketDepthIndicators.Indicators[0];

                if (imbalanceIndicator.DataSeries.Count == 0
                    || imbalanceIndicator.DataSeries[0].Values.Count == 0)
                {
                    return;
                }

                if (tab.Indicators == null || tab.Indicators.Count == 0)
                {
                    return;
                }

                Aindicator bollinger = (Aindicator)tab.Indicators[0];

                if (tab.CandlesAll == null || tab.CandlesAll.Count == 0)
                {
                    return;
                }

                int lastIndex = tab.CandlesAll.Count - 1;

                if (bollinger.DataSeries.Count < 3
                    || bollinger.DataSeries[0].Values.Count <= lastIndex
                    || bollinger.DataSeries[1].Values.Count <= lastIndex
                    || bollinger.DataSeries[2].Values.Count <= lastIndex)
                {
                    return;
                }

                double imbalance = (double)imbalanceIndicator.DataSeries[0].Last;
                double price = (double)tab.CandlesAll[lastIndex].Close;
                double upper = (double)bollinger.DataSeries[0].Values[lastIndex];
                double lower = (double)bollinger.DataSeries[1].Values[lastIndex];
                double centre = (double)bollinger.DataSeries[2].Values[lastIndex];

                if (upper == 0 && lower == 0 && centre == 0)
                {
                    return;
                }

                bool havePosition = tab.PositionsOpenAll.Count > 0;

                if (havePosition)
                {
                    Position position = tab.PositionsOpenAll[0];

                    if (position.Direction == Side.Buy && price >= centre)
                    {
                        tab.CloseAllAtMarket();
                    }
                    else if (position.Direction == Side.Sell && price <= centre)
                    {
                        tab.CloseAllAtMarket();
                    }
                }
                else
                {
                    string tabName = tab.TabName;

                    bool longCondition = imbalance >= (double)LongThreshold.ValueDecimal && price < lower;
                    bool shortCondition = imbalance <= (double)ShortThreshold.ValueDecimal && price > upper;

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
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
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

        private void HhfOrderBookImbalanceScreener_ParametrsChangeByUser()
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

                        ParameterMarketDepthString mode = (ParameterMarketDepthString)indicator.Parameters.Find(p => p.Name == "Mode");
                        ParameterMarketDepthInt levels = (ParameterMarketDepthInt)indicator.Parameters.Find(p => p.Name == "Levels");

                        if (mode != null && mode.ValueString != Mode.ValueString)
                        {
                            mode.ValueString = Mode.ValueString;
                        }

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

                        if (indicator.GetType().Name != "Bollinger")
                        {
                            continue;
                        }

                        IndicatorParameterInt length = (IndicatorParameterInt)indicator.Parameters.Find(p => p.Name == "Length");
                        IndicatorParameterDecimal deviation = (IndicatorParameterDecimal)indicator.Parameters.Find(p => p.Name == "Deviation");

                        if (length != null && length.ValueInt != BollingerLength.ValueInt)
                        {
                            length.ValueInt = BollingerLength.ValueInt;
                        }

                        if (deviation != null && deviation.ValueDecimal != BollingerDeviation.ValueDecimal)
                        {
                            deviation.ValueDecimal = BollingerDeviation.ValueDecimal;
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
            return "HhfOrderBookImbalanceScreener";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }
    }
}
