/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.Text.Json;
using OsEngine.Entity;
using OsEngine.IndicatorMarketDepth;
using OsEngine.Indicators;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.AutoTestBots
{
    [Bot("MarketDepthIndicatorTestBot")]
    public class MarketDepthIndicatorTestBot : BotPanel
    {
        private const string Marker = "MDI_TEST_DONE";

        private BotTabSimple _tab;

        private StrategyParameterString _indicatorType;

        private StrategyParameterString _testState;

        private StrategyParameterString _reportJson;

        private StrategyParameterInt _minDataDays;

        private StrategyParameterButton _deployButton;

        private AMarketDepth _main;

        private AMarketDepth _control;

        private bool _isDeployed;

        private int _depthEvents;

        private int _tradeEvents;

        private HashSet<DateTime> _depthDays = new HashSet<DateTime>();

        private HashSet<DateTime> _tradeDays = new HashSet<DateTime>();

        private MarketDepth _lastDepth;

        private Trade _lastTrade;

        private int _prevMainCount;

        private int _prevControlCount;

        private DateTime _lastAcceptedDepthTime = DateTime.MinValue;

        private DateTime _lastAcceptedTradeTime = DateTime.MinValue;

        private bool _historyOk = true;

        private bool _monotonicOk = true;

        private bool _throttleOk = true;

        private bool _twinOk = true;

        private int _twinMismatches;

        private string _twinFirstDetail = "";

        private bool _referenceOk = true;

        private int _referenceMismatches;

        private int _referenceSamples;

        private string _referenceFirstDetail = "";

        private decimal _refPerSecondSigned;

        private decimal _refCumulative;

        private DateTime _refCumDate = DateTime.MinValue;

        private int _refPerSecondCount;

        private decimal _refPerSecondVolume;

        private List<decimal> _refRvolBuffer = new List<decimal>();

        private bool _refRvolHasPrev;

        private List<decimal> _refSpreadHistory = new List<decimal>();

        private bool _refOfiHasPrev;

        private double _refPrevBidPrice;

        private double _refPrevBidSize;

        private double _refPrevAskPrice;

        private double _refPrevAskSize;

        private decimal _refTaSecVolume;

        private decimal _refTaSecCount;

        private decimal _refTaSecLargeVolume;

        private List<decimal> _refTaVolumeHistory = new List<decimal>();

        private List<decimal> _refTaCountHistory = new List<decimal>();

        private List<decimal> _refTaLargeVolumeHistory = new List<decimal>();

        private bool _refTaHasPrev;

        private decimal[] _minValues;

        private decimal[] _maxValues;

        private bool[] _nonZeroSeries;

        public MarketDepthIndicatorTestBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _indicatorType = (StrategyParameterString)CreateParameter("IndicatorType", "OrderBookImbalance");
            _testState = (StrategyParameterString)CreateParameter("TestState", "Waiting");
            _reportJson = (StrategyParameterString)CreateParameter("ReportJson", "");
            _minDataDays = (StrategyParameterInt)CreateParameter("MinDataDays", 3, 1, 10, 1);
            _deployButton = CreateParameterButton("DeployIndicator");
            _deployButton.UserClickOnButtonEvent += DeployIndicator;

            _tab.MarketDepthUpdateEvent += Tab_MarketDepthUpdateEvent;
            _tab.NewTickEvent += Tab_NewTickEvent;
            _tab.Connector.TestStartEvent += Connector_TestStartEvent;
            _tab.Connector.TestOverEvent += Connector_TestOverEvent;
        }

        public override string GetNameStrategyType()
        {
            return "MarketDepthIndicatorTestBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }

        private void DeployIndicator()
        {
            if (_isDeployed)
            {
                return;
            }

            string type = _indicatorType.ValueString;

            _main = _tab.MarketDepthIndicators.CreateIndicator(type, "TestIndicator");
            _control = _tab.MarketDepthIndicators.CreateIndicator(type, "TestIndicatorControl");

            if (_main == null || _control == null)
            {
                _testState.ValueString = "Failed";
                SendNewLogMessage("Deploy failed for indicator type " + type, LogMessageType.Error);
                return;
            }

            _isDeployed = true;
            _prevMainCount = _main.DataSeries.Count > 0 ? _main.DataSeries[0].Values.Count : 0;
            _prevControlCount = _control.DataSeries.Count > 0 ? _control.DataSeries[0].Values.Count : 0;

            int seriesCount = _main.DataSeries.Count;

            _minValues = new decimal[seriesCount];
            _maxValues = new decimal[seriesCount];
            _nonZeroSeries = new bool[seriesCount];

            for (int i = 0; i < seriesCount; i++)
            {
                _minValues[i] = decimal.MaxValue;
                _maxValues[i] = decimal.MinValue;
            }

            _testState.ValueString = "Deployed";
        }

        private void Connector_TestStartEvent()
        {
            _depthEvents = 0;
            _tradeEvents = 0;
            _depthDays.Clear();
            _tradeDays.Clear();
            _lastDepth = null;
            _lastTrade = null;
            _lastAcceptedDepthTime = DateTime.MinValue;
            _lastAcceptedTradeTime = DateTime.MinValue;
            _historyOk = true;
            _monotonicOk = true;
            _throttleOk = true;
            _twinOk = true;
            _twinMismatches = 0;
            _referenceOk = true;
            _referenceMismatches = 0;
            _referenceSamples = 0;
            _refPerSecondSigned = 0;
            _refCumulative = 0;
            _refCumDate = DateTime.MinValue;
            _refPerSecondCount = 0;
            _refPerSecondVolume = 0;
            _refRvolBuffer.Clear();
            _refRvolHasPrev = false;
            _refSpreadHistory.Clear();
            _refOfiHasPrev = false;
            _refPrevBidPrice = 0;
            _refPrevBidSize = 0;
            _refPrevAskPrice = 0;
            _refPrevAskSize = 0;
            _refTaSecVolume = 0;
            _refTaSecCount = 0;
            _refTaSecLargeVolume = 0;
            _refTaVolumeHistory.Clear();
            _refTaCountHistory.Clear();
            _refTaLargeVolumeHistory.Clear();
            _refTaHasPrev = false;

            if (_isDeployed)
            {
                _testState.ValueString = "Running";
            }
        }

        private void Tab_MarketDepthUpdateEvent(MarketDepth depth)
        {
            if (_isDeployed == false || depth == null)
            {
                return;
            }

            if (_main.DataSeries.Count == 0)
            {
                return;
            }

            _depthEvents++;
            _depthDays.Add(depth.Time.Date);

            CheckStorageAndHistory(depth.Time);

            int count = _main.DataSeries[0].Values.Count;
            bool accepted = count > _prevMainCount;

            if (accepted)
            {
                if (_lastAcceptedDepthTime != DateTime.MinValue
                    && depth.Time < _lastAcceptedDepthTime.AddSeconds(1))
                {
                    _throttleOk = false;
                }

                _lastAcceptedDepthTime = depth.Time;
                _lastDepth = depth;

                CheckReference(depth);
            }

            SampleRange();

            if (CheckTwin() == false)
            {
                _twinOk = false;
            }

            _prevMainCount = count;
            _prevControlCount = _control.DataSeries[0].Values.Count;
        }

        private void Tab_NewTickEvent(Trade trade)
        {
            if (_isDeployed == false || trade == null)
            {
                return;
            }

            if (_main.DataSeries.Count == 0)
            {
                return;
            }

            _tradeEvents++;
            _tradeDays.Add(trade.Time.Date);

            CheckStorageAndHistory(trade.Time);

            int count = _main.DataSeries[0].Values.Count;
            bool newSecond = count > _prevMainCount;

            if (newSecond)
            {
                if (_lastAcceptedTradeTime != DateTime.MinValue
                    && trade.Time < _lastAcceptedTradeTime.AddSeconds(1))
                {
                    _throttleOk = false;
                }

                _lastAcceptedTradeTime = trade.Time;
                _lastTrade = trade;
            }

            CheckReferenceTrade(trade, newSecond);

            SampleRange();

            if (CheckTwin() == false)
            {
                _twinOk = false;
            }

            _prevMainCount = count;
            _prevControlCount = _control.DataSeries[0].Values.Count;
        }

        private void CheckStorageAndHistory(DateTime eventTime)
        {
            if (_main.DataSeries.Count == 0)
            {
                return;
            }

            int limit = _main.HistoryDepth;

            if (limit <= 0)
            {
                limit = 60;
            }

            for (int i = 0; i < _main.DataSeries.Count; i++)
            {
                int seriesCount = _main.DataSeries[i].Values.Count;

                if (seriesCount > limit)
                {
                    _historyOk = false;
                }
            }

            int count = _main.DataSeries[0].Values.Count;

            if (count < _prevMainCount)
            {
                _monotonicOk = false;
            }
        }

        private void SampleRange()
        {
            for (int i = 0; i < _main.DataSeries.Count; i++)
            {
                decimal value = _main.DataSeries[i].Last;

                if (value < _minValues[i])
                {
                    _minValues[i] = value;
                }

                if (value > _maxValues[i])
                {
                    _maxValues[i] = value;
                }

                if (value != 0)
                {
                    _nonZeroSeries[i] = true;
                }
            }
        }

        private bool CheckTwin()
        {
            if (_control == null)
            {
                return true;
            }

            int seriesCount = _main.DataSeries.Count;

            if (_control.DataSeries.Count != seriesCount)
            {
                _twinMismatches++;
                if (string.IsNullOrEmpty(_twinFirstDetail))
                {
                    _twinFirstDetail = "series count differs";
                }
                return false;
            }

            for (int i = 0; i < seriesCount; i++)
            {
                if (_main.DataSeries[i].Last != _control.DataSeries[i].Last)
                {
                    _twinMismatches++;
                    if (string.IsNullOrEmpty(_twinFirstDetail))
                    {
                        _twinFirstDetail = "series " + _main.DataSeries[i].Name + " differ";
                    }
                    return false;
                }
            }

            return true;
        }

        private void CheckReference(MarketDepth depth)
        {
            string type = _main.GetType().Name;

            if (type == "OrderBookImbalance")
            {
                _referenceSamples++;
                int levels = GetIntParameter(_main, "Levels", 5);
                string mode = GetStringParameter(_main, "Mode", "Simple");

                if (mode == "Weighted")
                {
                    decimal weightedBid = 0;
                    decimal weightedAsk = 0;
                    decimal weight = 1m;

                    for (int i = 0; i < depth.Bids.Count && i < levels; i++)
                    {
                        weightedBid += (decimal)depth.Bids[i].Bid * weight;
                        weight *= 0.5m;
                    }

                    weight = 1m;

                    for (int i = 0; i < depth.Asks.Count && i < levels; i++)
                    {
                        weightedAsk += (decimal)depth.Asks[i].Ask * weight;
                        weight *= 0.5m;
                    }

                    decimal weightedTotal = weightedBid + weightedAsk;
                    decimal expectedWeighted = weightedTotal != 0 ? (weightedBid - weightedAsk) / weightedTotal : 0;

                    CompareReference(0, expectedWeighted);

                    return;
                }

                decimal sumBid = 0;
                decimal sumAsk = 0;

                for (int i = 0; i < depth.Bids.Count && i < levels; i++)
                {
                    sumBid += (decimal)depth.Bids[i].Bid;
                }

                for (int i = 0; i < depth.Asks.Count && i < levels; i++)
                {
                    sumAsk += (decimal)depth.Asks[i].Ask;
                }

                decimal total = sumBid + sumAsk;
                decimal expected = total != 0 ? (sumBid - sumAsk) / total : 0;

                CompareReference(0, expected);

                return;
            }

            if (type == "DepthVolume")
            {
                _referenceSamples++;
                int levels = GetIntParameter(_main, "Levels", 10);
                decimal bidDepth = 0;
                decimal askDepth = 0;

                for (int i = 0; i < depth.Bids.Count && i < levels; i++)
                {
                    bidDepth += (decimal)depth.Bids[i].Bid;
                }

                for (int i = 0; i < depth.Asks.Count && i < levels; i++)
                {
                    askDepth += (decimal)depth.Asks[i].Ask;
                }

                CompareReference(0, bidDepth);
                CompareReference(1, askDepth);

                return;
            }

            if (type == "Spread")
            {
                _referenceSamples++;

                if (depth.Asks.Count == 0 || depth.Bids.Count == 0)
                {
                    CompareReference(0, 0);
                    CompareReference(1, 0);
                    CompareReference(2, 0);
                    return;
                }

                double bestAsk = depth.Asks[0].Price;
                double bestBid = depth.Bids[0].Price;

                if (bestAsk <= 0 || bestBid <= 0 || bestBid >= bestAsk)
                {
                    CompareReference(0, 0);
                    CompareReference(1, 0);
                    CompareReference(2, 0);
                    return;
                }

                decimal spread = (decimal)(bestAsk - bestBid);

                string mode = GetStringParameter(_main, "Mode", "Absolute");

                if (mode == "Percent")
                {
                    spread = spread / ((decimal)bestBid / 100m);
                }

                int period = GetIntParameter(_main, "Period", 10);

                if (period < 1)
                {
                    period = 10;
                }

                _refSpreadHistory.Add(spread);

                while (_refSpreadHistory.Count > period)
                {
                    _refSpreadHistory.RemoveAt(0);
                }

                decimal average = ReferenceAverage(_refSpreadHistory);
                decimal volatility = ReferenceStdDev(_refSpreadHistory, average);

                CompareReference(0, spread);
                CompareReference(1, average);
                CompareReference(2, volatility);

                return;
            }

            if (type == "MicroPrice")
            {
                _referenceSamples++;

                if (depth.Bids.Count == 0 || depth.Asks.Count == 0)
                {
                    CompareReference(0, 0);
                    return;
                }

                double bidPrice = depth.Bids[0].Price;
                double askPrice = depth.Asks[0].Price;
                double bidVolume = depth.Bids[0].Bid;
                double askVolume = depth.Asks[0].Ask;

                if (bidPrice <= 0 || askPrice <= 0 || bidVolume + askVolume <= 0)
                {
                    CompareReference(0, 0);
                    return;
                }

                decimal microPrice = (decimal)((bidPrice * askVolume + askPrice * bidVolume) / (bidVolume + askVolume));

                CompareReference(0, microPrice);

                return;
            }

            if (type == "Ofi")
            {
                _referenceSamples++;

                if (depth.Bids.Count == 0 || depth.Asks.Count == 0)
                {
                    CompareReference(0, 0);
                    return;
                }

                double bidPrice = depth.Bids[0].Price;
                double bidSize = depth.Bids[0].Bid;
                double askPrice = depth.Asks[0].Price;
                double askSize = depth.Asks[0].Ask;

                decimal expectedOfi = 0;

                if (_refOfiHasPrev)
                {
                    decimal eBid = 0;

                    if (bidPrice >= _refPrevBidPrice)
                    {
                        eBid += (decimal)bidSize;
                    }

                    if (bidPrice <= _refPrevBidPrice)
                    {
                        eBid -= (decimal)_refPrevBidSize;
                    }

                    decimal eAsk = 0;

                    if (askPrice <= _refPrevAskPrice)
                    {
                        eAsk -= (decimal)askSize;
                    }

                    if (askPrice >= _refPrevAskPrice)
                    {
                        eAsk += (decimal)_refPrevAskSize;
                    }

                    expectedOfi = eBid + eAsk;
                }

                _refPrevBidPrice = bidPrice;
                _refPrevBidSize = bidSize;
                _refPrevAskPrice = askPrice;
                _refPrevAskSize = askSize;
                _refOfiHasPrev = true;

                CompareReference(0, expectedOfi);

                return;
            }

            if (type == "LiquidityWalls")
            {
                _referenceSamples++;

                int levels = GetIntParameter(_main, "Levels", 20);

                if (levels <= 0)
                {
                    levels = 20;
                }

                decimal bidMaxVolume = 0;
                double bidMaxPrice = 0;

                for (int i = 0; i < depth.Bids.Count && i < levels; i++)
                {
                    decimal volume = (decimal)depth.Bids[i].Bid;

                    if (volume > bidMaxVolume)
                    {
                        bidMaxVolume = volume;
                        bidMaxPrice = depth.Bids[i].Price;
                    }
                }

                decimal askMaxVolume = 0;
                double askMaxPrice = 0;

                for (int i = 0; i < depth.Asks.Count && i < levels; i++)
                {
                    decimal volume = (decimal)depth.Asks[i].Ask;

                    if (volume > askMaxVolume)
                    {
                        askMaxVolume = volume;
                        askMaxPrice = depth.Asks[i].Price;
                    }
                }

                CompareReference(0, bidMaxVolume);
                CompareReference(1, (decimal)bidMaxPrice);
                CompareReference(2, askMaxVolume);
                CompareReference(3, (decimal)askMaxPrice);

                return;
            }

            if (type == "ExpectedSlippage")
            {
                _referenceSamples++;

                decimal volume = GetDecimalParameter(_main, "Volume", 100m);
                decimal buy = ReferenceSlippage(depth.Asks, volume, true);
                decimal sell = ReferenceSlippage(depth.Bids, volume, false);

                CompareReference(0, buy);
                CompareReference(1, sell);

                return;
            }
        }

        private void CompareReference(int seriesIndex, decimal expected)
        {
            if (_main.DataSeries[seriesIndex].Last != expected)
            {
                _referenceOk = false;
                _referenceMismatches++;
                if (string.IsNullOrEmpty(_referenceFirstDetail))
                {
                    _referenceFirstDetail = "series " + _main.DataSeries[seriesIndex].Name
                        + " expected " + expected + " got " + _main.DataSeries[seriesIndex].Last;
                }
            }
        }

        private void CheckReferenceTrade(Trade trade, bool newSecond)
        {
            string type = _main.GetType().Name;

            if (type == "Delta")
            {
                _referenceSamples++;

                if (newSecond)
                {
                    _refPerSecondSigned = 0;
                }

                string aggregation = GetStringParameter(_main, "Aggregation", "Volume");
                decimal coefficient = aggregation == "Volume" ? trade.Volume : 1m;
                decimal signed = (trade.Side == Side.Buy ? 1m : -1m) * coefficient;

                _refPerSecondSigned += signed;

                if (_refCumDate == DateTime.MinValue || _refCumDate != trade.Time.Date)
                {
                    _refCumDate = trade.Time.Date;
                    _refCumulative = 0;
                }

                _refCumulative += signed;

                CompareReferenceTrade(0, _refPerSecondSigned);
                CompareReferenceTrade(1, _refCumulative);

                return;
            }

            if (type == "SpeedOfTape")
            {
                _referenceSamples++;

                if (newSecond)
                {
                    _refPerSecondCount = 0;
                }

                _refPerSecondCount++;

                CompareReferenceTrade(0, _refPerSecondCount);

                return;
            }

            if (type == "Rvol")
            {
                _referenceSamples++;

                int period = GetIntParameter(_main, "Period", 10);

                if (newSecond)
                {
                    if (_refRvolHasPrev)
                    {
                        _refRvolBuffer.Add(_refPerSecondVolume);

                        if (period < 1)
                        {
                            period = 10;
                        }

                        while (_refRvolBuffer.Count > period)
                        {
                            _refRvolBuffer.RemoveAt(0);
                        }
                    }

                    _refPerSecondVolume = 0;
                    _refRvolHasPrev = true;
                }

                _refPerSecondVolume += trade.Volume;

                decimal average = 0;

                for (int i = 0; i < _refRvolBuffer.Count; i++)
                {
                    average += _refRvolBuffer[i];
                }

                if (_refRvolBuffer.Count > 0)
                {
                    average = average / _refRvolBuffer.Count;
                }

                decimal expected = average == 0 ? 0m : _refPerSecondVolume / average;

                CompareReferenceTrade(0, expected);

                return;
            }

            if (type == "TradeSizeAnalytics")
            {
                _referenceSamples++;

                if (newSecond)
                {
                    if (_refTaHasPrev)
                    {
                        int period = GetIntParameter(_main, "Period", 10);

                        if (period < 1)
                        {
                            period = 10;
                        }

                        _refTaVolumeHistory.Add(_refTaSecVolume);
                        _refTaCountHistory.Add(_refTaSecCount);
                        _refTaLargeVolumeHistory.Add(_refTaSecLargeVolume);

                        while (_refTaVolumeHistory.Count > period)
                        {
                            _refTaVolumeHistory.RemoveAt(0);
                            _refTaCountHistory.RemoveAt(0);
                            _refTaLargeVolumeHistory.RemoveAt(0);
                        }
                    }

                    _refTaSecVolume = 0;
                    _refTaSecCount = 0;
                    _refTaSecLargeVolume = 0;
                    _refTaHasPrev = true;
                }

                _refTaSecVolume += trade.Volume;
                _refTaSecCount += 1;

                decimal threshold = GetDecimalParameter(_main, "LargeThreshold", 1000m);

                if (trade.Volume >= threshold)
                {
                    _refTaSecLargeVolume += trade.Volume;
                }

                decimal totalVolume = ReferenceSum(_refTaVolumeHistory);
                decimal totalCount = ReferenceSum(_refTaCountHistory);
                decimal totalLarge = ReferenceSum(_refTaLargeVolumeHistory);

                decimal expectedAvg = totalCount == 0 ? 0 : totalVolume / totalCount;
                decimal expectedShare = totalVolume == 0 ? 0 : totalLarge / totalVolume;

                CompareReferenceTrade(0, expectedAvg);
                CompareReferenceTrade(1, expectedShare);

                return;
            }
        }

        private void CompareReferenceTrade(int seriesIndex, decimal expected)
        {
            if (_main.DataSeries[seriesIndex].Last != expected)
            {
                _referenceOk = false;
                _referenceMismatches++;
                if (string.IsNullOrEmpty(_referenceFirstDetail))
                {
                    _referenceFirstDetail = "series " + _main.DataSeries[seriesIndex].Name
                        + " expected " + expected + " got " + _main.DataSeries[seriesIndex].Last;
                }
            }
        }

        private static int GetIntParameter(AMarketDepth indicator, string name, int defaultValue)
        {
            for (int i = 0; i < indicator.Parameters.Count; i++)
            {
                if (indicator.Parameters[i].Name == name && indicator.Parameters[i] is ParameterMarketDepthInt parameter)
                {
                    return parameter.ValueInt;
                }
            }

            return defaultValue;
        }

        private static string GetStringParameter(AMarketDepth indicator, string name, string defaultValue)
        {
            for (int i = 0; i < indicator.Parameters.Count; i++)
            {
                if (indicator.Parameters[i].Name == name && indicator.Parameters[i] is ParameterMarketDepthString parameter)
                {
                    return parameter.ValueString;
                }
            }

            return defaultValue;
        }

        private static decimal GetDecimalParameter(AMarketDepth indicator, string name, decimal defaultValue)
        {
            for (int i = 0; i < indicator.Parameters.Count; i++)
            {
                if (indicator.Parameters[i].Name == name && indicator.Parameters[i] is ParameterMarketDepthDecimal parameter)
                {
                    return parameter.ValueDecimal;
                }
            }

            return defaultValue;
        }

        private static decimal ReferenceAverage(List<decimal> values)
        {
            if (values.Count == 0)
            {
                return 0;
            }

            decimal sum = 0;

            for (int i = 0; i < values.Count; i++)
            {
                sum += values[i];
            }

            return sum / values.Count;
        }

        private static decimal ReferenceStdDev(List<decimal> values, decimal average)
        {
            if (values.Count < 2)
            {
                return 0;
            }

            decimal sum = 0;

            for (int i = 0; i < values.Count; i++)
            {
                decimal diff = values[i] - average;
                sum += diff * diff;
            }

            decimal variance = sum / values.Count;

            return (decimal)Math.Sqrt((double)variance);
        }

        private static decimal ReferenceSlippage(List<MarketDepthLevel> levels, decimal volume, bool isBuy)
        {
            if (volume <= 0 || levels == null || levels.Count == 0)
            {
                return 0;
            }

            double bestPrice = levels[0].Price;

            if (bestPrice <= 0)
            {
                return 0;
            }

            decimal remaining = volume;
            decimal cost = 0;

            for (int i = 0; i < levels.Count; i++)
            {
                double price = levels[i].Price;
                decimal levelVolume = (decimal)(isBuy ? levels[i].Ask : levels[i].Bid);

                if (price <= 0 || levelVolume <= 0)
                {
                    continue;
                }

                decimal fill = remaining < levelVolume ? remaining : levelVolume;
                cost += (decimal)price * fill;
                remaining -= fill;

                if (remaining <= 0)
                {
                    break;
                }
            }

            decimal filled = volume - remaining;

            if (filled <= 0)
            {
                return 0;
            }

            decimal averagePrice = cost / filled;
            decimal best = (decimal)bestPrice;

            if (isBuy)
            {
                return (averagePrice - best) / best * 100m;
            }

            return (best - averagePrice) / best * 100m;
        }

        private static decimal ReferenceSum(List<decimal> values)
        {
            decimal sum = 0;

            for (int i = 0; i < values.Count; i++)
            {
                sum += values[i];
            }

            return sum;
        }

        private void Connector_TestOverEvent()
        {
            if (_isDeployed == false)
            {
                _testState.ValueString = "Failed";
                _reportJson.ValueString = BuildReport(null);
                SendNewLogMessage(Marker + " not deployed", LogMessageType.System);
                return;
            }

            List<Dictionary<string, object>> tests = new List<Dictionary<string, object>>();

            TestDeploy(tests);
            TestDescription(tests);
            TestSeriesPresence(tests);
            TestParameterNames(tests);
            TestSchema(tests);
            TestInputCoverage(tests);
            TestValuesProduced(tests);
            TestHistoryLimit(tests);
            TestStorageMonotonicity(tests);
            TestThrottling(tests);
            TestTwinDeterminism(tests);
            TestValueRange(tests);
            TestReferenceValue(tests);
            TestRecalculateStability(tests);
            TestHistoryResize(tests);
            TestClearReset(tests);
            TestNoExceptions(tests);

            bool hasFail = false;

            for (int i = 0; i < tests.Count; i++)
            {
                string status = tests[i].ContainsKey("status") ? tests[i]["status"].ToString() : "";

                if (status == "FAIL")
                {
                    hasFail = true;
                    break;
                }
            }

            _testState.ValueString = hasFail ? "Failed" : "Completed";
            _reportJson.ValueString = BuildReport(tests);

            SendNewLogMessage(Marker + " " + _testState.ValueString + " " + _reportJson.ValueString, LogMessageType.System);
        }

        private void TestDeploy(List<Dictionary<string, object>> tests)
        {
            bool ok = _main != null && _control != null && _isDeployed;

            tests.Add(new Dictionary<string, object>
            {
                { "name", "Deploy" },
                { "status", ok ? "PASS" : "FAIL" },
                { "details", ok ? "indicator created" : "indicator not created" }
            });
        }

        private void TestSchema(List<Dictionary<string, object>> tests)
        {
            if (_main == null)
            {
                tests.Add(Fail("Schema", "no indicator"));
                return;
            }

            bool unique = true;
            HashSet<string> names = new HashSet<string>();

            for (int i = 0; i < _main.DataSeries.Count; i++)
            {
                if (names.Add(_main.DataSeries[i].Name) == false)
                {
                    unique = false;
                }
            }

            bool paintOk = true;

            for (int i = 0; i < _main.DataSeries.Count; i++)
            {
                IndicatorChartPaintType paintType = _main.DataSeries[i].ChartPaintType;

                if (paintType != IndicatorChartPaintType.Line
                    && paintType != IndicatorChartPaintType.Column
                    && paintType != IndicatorChartPaintType.Point)
                {
                    paintOk = false;
                }
            }

            bool ok = _main.Parameters.Count > 0 && _main.DataSeries.Count > 0 && unique && paintOk;

            tests.Add(new Dictionary<string, object>
            {
                { "name", "Schema" },
                { "status", ok ? "PASS" : "FAIL" },
                { "details", "parameters " + _main.Parameters.Count + ", series " + _main.DataSeries.Count + ", unique " + unique + ", paint " + paintOk }
            });
        }

        private void TestDescription(List<Dictionary<string, object>> tests)
        {
            if (_main == null)
            {
                tests.Add(Fail("Description", "no indicator"));
                return;
            }

            string description = _main.Description;

            bool ok = string.IsNullOrWhiteSpace(description) == false;

            tests.Add(new Dictionary<string, object>
            {
                { "name", "Description" },
                { "status", ok ? "PASS" : "FAIL" },
                { "details", ok ? "description present" : "description empty" }
            });
        }

        private void TestSeriesPresence(List<Dictionary<string, object>> tests)
        {
            if (_main == null)
            {
                tests.Add(Fail("SeriesPresence", "no indicator"));
                return;
            }

            bool ok = _main.DataSeries.Count > 0;

            tests.Add(new Dictionary<string, object>
            {
                { "name", "SeriesPresence" },
                { "status", ok ? "PASS" : "FAIL" },
                { "details", ok ? "series " + _main.DataSeries.Count : "no series" }
            });
        }

        private void TestParameterNames(List<Dictionary<string, object>> tests)
        {
            if (_main == null)
            {
                tests.Add(Fail("ParameterNames", "no indicator"));
                return;
            }

            bool ok = true;
            string detail = "";
            HashSet<string> names = new HashSet<string>();

            for (int i = 0; i < _main.Parameters.Count; i++)
            {
                string name = _main.Parameters[i].Name;

                if (string.IsNullOrWhiteSpace(name))
                {
                    ok = false;
                    detail += "empty name at " + i + "; ";
                    continue;
                }

                if (names.Add(name) == false)
                {
                    ok = false;
                    detail += "duplicate " + name + "; ";
                }
            }

            tests.Add(new Dictionary<string, object>
            {
                { "name", "ParameterNames" },
                { "status", ok ? "PASS" : "FAIL" },
                { "details", ok ? "parameter names valid" : detail.TrimEnd(' ', ';') }
            });
        }

        private void TestNoExceptions(List<Dictionary<string, object>> tests)
        {
            if (_main == null)
            {
                tests.Add(Fail("NoExceptions", "no indicator"));
                return;
            }

            bool ok = _main.ErrorCount == 0 && (_control == null || _control.ErrorCount == 0);

            tests.Add(new Dictionary<string, object>
            {
                { "name", "NoExceptions" },
                { "status", ok ? "PASS" : "FAIL" },
                { "details", ok ? "no exceptions" : "errors " + _main.ErrorCount + ": " + _main.LastError }
            });
        }

        private void TestInputCoverage(List<Dictionary<string, object>> tests)
        {            MarketDepthIndicatorDataSource source = _main.DataSource;
            int minDays = _minDataDays.ValueInt;

            bool ok;

            if (source == MarketDepthIndicatorDataSource.Trades)
            {
                ok = _tradeEvents > 0 && _tradeDays.Count >= minDays;
                tests.Add(Coverage("InputCoverage", ok, "trades " + _tradeEvents + ", days " + _tradeDays.Count + "/" + minDays));
                return;
            }

            if (source == MarketDepthIndicatorDataSource.Both)
            {
                ok = _depthEvents > 0 && _tradeEvents > 0
                    && _depthDays.Count >= minDays && _tradeDays.Count >= minDays;
                tests.Add(Coverage("InputCoverage", ok, "depth " + _depthEvents + "/" + _depthDays.Count + ", trades " + _tradeEvents + "/" + _tradeDays.Count + ", days " + minDays));
                return;
            }

            ok = _depthEvents > 0 && _depthDays.Count >= minDays;
            tests.Add(Coverage("InputCoverage", ok, "depth " + _depthEvents + ", days " + _depthDays.Count + "/" + minDays));
        }

        private void TestValuesProduced(List<Dictionary<string, object>> tests)
        {
            if (_main == null)
            {
                tests.Add(Fail("ValuesProduced", "no indicator"));
                return;
            }

            bool ok = true;
            string detail = "";

            for (int i = 0; i < _main.DataSeries.Count; i++)
            {
                int count = _main.DataSeries[i].Values.Count;

                if (count == 0)
                {
                    ok = false;
                    detail += _main.DataSeries[i].Name + " empty; ";
                }
            }

            tests.Add(new Dictionary<string, object>
            {
                { "name", "ValuesProduced" },
                { "status", ok ? "PASS" : "FAIL" },
                { "details", ok ? "all series have values" : detail.TrimEnd(' ', ';') }
            });
        }

        private void TestHistoryLimit(List<Dictionary<string, object>> tests)
        {
            tests.Add(new Dictionary<string, object>
            {
                { "name", "HistoryLimit" },
                { "status", _historyOk ? "PASS" : "FAIL" },
                { "details", _historyOk ? "never exceeded HistoryDepth" : "HistoryDepth exceeded" }
            });
        }

        private void TestStorageMonotonicity(List<Dictionary<string, object>> tests)
        {
            tests.Add(new Dictionary<string, object>
            {
                { "name", "StorageMonotonicity" },
                { "status", _monotonicOk ? "PASS" : "FAIL" },
                { "details", _monotonicOk ? "count never decreased" : "count decreased" }
            });
        }

        private void TestThrottling(List<Dictionary<string, object>> tests)
        {
            tests.Add(new Dictionary<string, object>
            {
                { "name", "Throttling" },
                { "status", _throttleOk ? "PASS" : "FAIL" },
                { "details", _throttleOk ? "one slot per second" : "more than one slot per second" }
            });
        }

        private void TestTwinDeterminism(List<Dictionary<string, object>> tests)
        {
            tests.Add(new Dictionary<string, object>
            {
                { "name", "TwinDeterminism" },
                { "status", _twinOk ? "PASS" : "FAIL" },
                { "details", _twinOk ? "identical" : "mismatches " + _twinMismatches + " " + _twinFirstDetail }
            });
        }

        private void TestValueRange(List<Dictionary<string, object>> tests)
        {
            string type = _main.GetType().Name;
            Profile profile = GetProfile(type);

            if (profile == null)
            {
                tests.Add(Skip("ValueRange", "no profile for " + type));
                return;
            }

            bool ok = true;
            string detail = "";

            for (int i = 0; i < _main.DataSeries.Count; i++)
            {
                if (_minValues[i] == decimal.MaxValue || _maxValues[i] == decimal.MinValue)
                {
                    continue;
                }

                if (profile.Min.HasValue && _minValues[i] < profile.Min.Value)
                {
                    ok = false;
                    detail += _main.DataSeries[i].Name + " below " + profile.Min.Value + "; ";
                }

                if (profile.Max.HasValue && _maxValues[i] > profile.Max.Value)
                {
                    ok = false;
                    detail += _main.DataSeries[i].Name + " above " + profile.Max.Value + "; ";
                }
            }

            tests.Add(new Dictionary<string, object>
            {
                { "name", "ValueRange" },
                { "status", ok ? "PASS" : "FAIL" },
                { "details", ok ? "within " + type + " range" : detail.TrimEnd(' ', ';') }
            });
        }

        private void TestReferenceValue(List<Dictionary<string, object>> tests)
        {
            string type = _main.GetType().Name;

            if (HasReference(type) == false)
            {
                tests.Add(Skip("ReferenceValue", "no reference for " + type));
                return;
            }

            tests.Add(new Dictionary<string, object>
            {
                { "name", "ReferenceValue" },
                { "status", _referenceOk ? "PASS" : "FAIL" },
                { "details", _referenceOk ? "samples " + _referenceSamples : "mismatches " + _referenceMismatches + " " + _referenceFirstDetail }
            });
        }

        private void TestRecalculateStability(List<Dictionary<string, object>> tests)
        {
            if (_main == null)
            {
                tests.Add(Fail("RecalculateStability", "no indicator"));
                return;
            }

            int seriesCount = _main.DataSeries.Count;
            decimal[] before = new decimal[seriesCount];

            for (int i = 0; i < seriesCount; i++)
            {
                before[i] = _main.DataSeries[i].Last;
            }

            bool ok = true;

            try
            {
                _main.Recalculate();

                for (int i = 0; i < seriesCount; i++)
                {
                    if (_main.DataSeries[i].Last != before[i])
                    {
                        ok = false;
                    }
                }
            }
            catch (Exception error)
            {
                ok = false;
            }

            tests.Add(new Dictionary<string, object>
            {
                { "name", "RecalculateStability" },
                { "status", ok ? "PASS" : "FAIL" },
                { "details", ok ? "last values unchanged" : "last values changed or exception" }
            });
        }

        private void TestHistoryResize(List<Dictionary<string, object>> tests)
        {
            if (_main == null)
            {
                tests.Add(Fail("HistoryResize", "no indicator"));
                return;
            }

            int original = _main.HistoryDepth;
            bool ok = true;

            try
            {
                _main.HistoryDepth = 30;
                _main.Recalculate();

                for (int i = 0; i < _main.DataSeries.Count; i++)
                {
                    if (_main.DataSeries[i].Values.Count > 30)
                    {
                        ok = false;
                    }
                }

                _main.HistoryDepth = original;
                _main.Recalculate();

                for (int i = 0; i < _main.DataSeries.Count; i++)
                {
                    if (_main.DataSeries[i].Values.Count > original)
                    {
                        ok = false;
                    }
                }
            }
            catch (Exception error)
            {
                ok = false;
            }

            tests.Add(new Dictionary<string, object>
            {
                { "name", "HistoryResize" },
                { "status", ok ? "PASS" : "FAIL" },
                { "details", ok ? "trimmed to 30 and restored" : "resize failed" }
            });
        }

        private void TestClearReset(List<Dictionary<string, object>> tests)
        {
            if (_main == null)
            {
                tests.Add(Fail("ClearReset", "no indicator"));
                return;
            }

            bool ok = true;

            try
            {
                _main.Clear();

                for (int i = 0; i < _main.DataSeries.Count; i++)
                {
                    if (_main.DataSeries[i].Values.Count != 0)
                    {
                        ok = false;
                    }
                }

                if (_lastDepth != null)
                {
                    _main.Process(_lastDepth);
                }
                else if (_lastTrade != null)
                {
                    _main.ProcessTrade(_lastTrade);
                }

                for (int i = 0; i < _main.DataSeries.Count; i++)
                {
                    if (_main.DataSeries[i].Values.Count != 1)
                    {
                        ok = false;
                    }
                }
            }
            catch (Exception error)
            {
                ok = false;
            }

            tests.Add(new Dictionary<string, object>
            {
                { "name", "ClearReset" },
                { "status", ok ? "PASS" : "FAIL" },
                { "details", ok ? "clear and single replay ok" : "clear or replay failed" }
            });
        }

        private static Dictionary<string, object> Coverage(string name, bool ok, string details)
        {
            return new Dictionary<string, object>
            {
                { "name", name },
                { "status", ok ? "PASS" : "FAIL" },
                { "details", details }
            };
        }

        private static Dictionary<string, object> Fail(string name, string details)
        {
            return new Dictionary<string, object>
            {
                { "name", name },
                { "status", "FAIL" },
                { "details", details }
            };
        }

        private static Dictionary<string, object> Skip(string name, string details)
        {
            return new Dictionary<string, object>
            {
                { "name", name },
                { "status", "SKIP" },
                { "details", details }
            };
        }

        private string BuildReport(List<Dictionary<string, object>> tests)
        {
            Dictionary<string, object> report = new Dictionary<string, object>();

            report["indicator"] = _main != null ? _main.GetType().Name : _indicatorType.ValueString;
            report["security"] = _tab.Security != null ? _tab.Security.Name : "";
            report["depth_events"] = _depthEvents;
            report["trade_events"] = _tradeEvents;
            report["depth_days"] = _depthDays.Count;
            report["trade_days"] = _tradeDays.Count;
            report["tests"] = tests ?? new List<Dictionary<string, object>>();

            return JsonSerializer.Serialize(report);
        }

        private static Profile GetProfile(string type)
        {
            if (type == "OrderBookImbalance")
            {
                return new Profile { Min = -1m, Max = 1m };
            }

            if (type == "DepthVolume")
            {
                return new Profile { Min = 0m, Max = null };
            }

            if (type == "Spread")
            {
                return new Profile { Min = 0m, Max = null };
            }

            if (type == "SpeedOfTape")
            {
                return new Profile { Min = 0m, Max = null };
            }

            if (type == "Rvol")
            {
                return new Profile { Min = 0m, Max = null };
            }

            if (type == "MicroPrice")
            {
                return new Profile { Min = 0m, Max = null };
            }

            if (type == "LiquidityWalls")
            {
                return new Profile { Min = 0m, Max = null };
            }

            if (type == "ExpectedSlippage")
            {
                return new Profile { Min = 0m, Max = null };
            }

            if (type == "TradeSizeAnalytics")
            {
                return new Profile { Min = 0m, Max = null };
            }

            if (type == "Spoofing")
            {
                return new Profile { Min = 0m, Max = 1m };
            }

            return null;
        }

        private static bool HasReference(string type)
        {
            return type == "OrderBookImbalance"
                || type == "DepthVolume"
                || type == "Spread"
                || type == "Delta"
                || type == "SpeedOfTape"
                || type == "Rvol"
                || type == "MicroPrice"
                || type == "Ofi"
                || type == "LiquidityWalls"
                || type == "ExpectedSlippage"
                || type == "TradeSizeAnalytics";
        }

        private sealed class Profile
        {
            public decimal? Min;
            public decimal? Max;
        }
    }
}
