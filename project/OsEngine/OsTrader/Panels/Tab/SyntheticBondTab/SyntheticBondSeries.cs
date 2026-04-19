/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Entity.SyntheticBondEntity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels.Tab.SynteticBondTab;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;

namespace OsEngine.OsTrader.Panels.Tab.SyntheticBondTab
{
    public class SyntheticBondSeries
    {
        #region Constructor

        public SyntheticBondSeries(string uniqueName, int uniqueNumber, StartProgram startProgram)
        {
            StartProgram = startProgram;
            UniqueName = uniqueName;
            UniqueNumber = uniqueNumber;

            Load();

            if (startProgram == StartProgram.IsOsTrader)
            {
                Thread thread = new Thread(MainSyntheticBondThread);
                thread.Start();
            }
            else if (startProgram == StartProgram.IsTester
                || startProgram == StartProgram.IsOsOptimizer)
            {
                SubscribeToTesterEndMinuteEvent();
            }
        }

        public void Save()
        {
            if (StartProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            if (SyntheticBonds == null)
            {
                return;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + UniqueName + @"SyntheticBondNamesToLoad.txt", false))
                {
                    writer.WriteLine(UniqueNumber.ToString());

                    if (_patternBaseTab != null &&
                        _patternBaseTab.TabName != null)
                        writer.WriteLine(_patternBaseTab.TabName.ToString());
                    else
                        writer.WriteLine("None");

                    writer.WriteLine(SyntheticBonds.Count.ToString());

                    for (int i = 0; i < SyntheticBonds.Count; i++)
                    {
                        SyntheticBond syntheticBond = SyntheticBonds[i];
                        writer.WriteLine(syntheticBond.UniqueName);
                        writer.WriteLine(syntheticBond.UniqueNumber);
                        syntheticBond.Save();
                    }
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void OnSettingsChanged()
        {
            _separationCaches.Clear();
            Save();
            SettingsChangedEvent?.Invoke();
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(@"Engine\" + UniqueName + @"SyntheticBondNamesToLoad.txt"))
                {
                    return;
                }

                SyntheticBonds = new List<SyntheticBond>();

                using (StreamReader reader = new StreamReader(@"Engine\" + UniqueName + @"SyntheticBondNamesToLoad.txt"))
                {
                    UniqueNumber = Convert.ToInt32(reader.ReadLine());

                    string patternBaseTabName = reader.ReadLine();

                    if (patternBaseTabName != "None")
                        _patternBaseTab = new BotTabSimple(patternBaseTabName, StartProgram);

                    int syntheticBondCount = Convert.ToInt32(reader.ReadLine());

                    for (int i = 0; i < syntheticBondCount; i++)
                    {
                        string syntheticBondName = reader.ReadLine();
                        int syntheticBondNumber = Convert.ToInt32(reader.ReadLine());
                        SyntheticBond syntheticBond = new SyntheticBond(syntheticBondName, syntheticBondNumber, StartProgram);
                        SyntheticBonds.Add(syntheticBond);
                    }
                }

                PropagateBaseSecurityToAll();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void Clear()
        {
            if (_patternBaseTab != null)
                _patternBaseTab.Clear();

            for (int i = 0; i < SyntheticBonds.Count; i++)
            {
                SyntheticBond syntheticBond = SyntheticBonds[i];
                syntheticBond.Clear();
            }
        }

        public void Delete()
        {
            try
            {
                if (_patternBaseTab != null)
                {
                    _patternBaseTab.Delete();
                    _patternBaseTab = null;
                }

                if (SyntheticBonds == null)
                    return;

                for (int i = 0; i < SyntheticBonds.Count; i++)
                {
                    SyntheticBond syntheticBond = SyntheticBonds[i];
                    syntheticBond.Delete();
                }

                SyntheticBonds.Clear();

                if (File.Exists(@"Engine\" + UniqueName + @"SyntheticBondNamesToLoad.txt"))
                {
                    File.Delete(@"Engine\" + UniqueName + @"SyntheticBondNamesToLoad.txt");
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public bool IsReadyToTrade()
        {
            try
            {
                if (SyntheticBonds == null)
                    return false;
                else if (SyntheticBonds.Count == 0)
                    return false;

                bool isReadyToTrade = true;
                for (int i = 0; i < SyntheticBonds.Count; i++)
                {
                    SyntheticBond syntheticBond = SyntheticBonds[i];

                    if (syntheticBond.IsReadyToTrade() == false)
                        return false;
                }

                return isReadyToTrade;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return false;
            }
        }

        public void ChooseBaseSecurity()
        {
            try
            {
                if (_patternBaseTab != null)
                {
                    _patternBaseTab.Delete();
                    _patternBaseTab = null;
                }

                _patternBaseTab = new BotTabSimple(UniqueName + "PatternBaseTab", StartProgram);
                _patternBaseTab.DialogClosed += OnBaseSecurityDialogClosed;
                _patternBaseTab.SecuritySubscribeEvent += SecuritySubscribe;
                _patternBaseTab.ShowConnectorDialog();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void OnBaseSecurityDialogClosed()
        {
            _patternBaseTab.DialogClosed -= OnBaseSecurityDialogClosed;
            PropagateBaseSecurityToAll();
        }

        public void PropagateBaseSecurityToAll()
        {
            if (SyntheticBonds == null ||
               (SyntheticBonds != null && SyntheticBonds.Count == 0))
            {
                return;
            }

            if (_patternBaseTab == null ||
               (_patternBaseTab != null && _patternBaseTab.Connector == null))
            {
                return;
            }

            ConnectorCandles sourceConnector = _patternBaseTab.Connector;

            for (int i = 0; i < SyntheticBonds.Count; i++)
            {
                SyntheticBond syntheticBond = SyntheticBonds[i];

                bool needUpdatePatternBaseTab = true;

                BotTabSimple targetPatternTab = syntheticBond.PatternBaseTab;

                if (targetPatternTab == null ||
                   (targetPatternTab != null && targetPatternTab.Connector == null))
                    needUpdatePatternBaseTab = false;
                else if (targetPatternTab.Connector.SecurityName != _patternBaseTab.Connector.SecurityName)
                    needUpdatePatternBaseTab = true;

                if (needUpdatePatternBaseTab)
                {
                    ConnectorCandles targetConnector = targetPatternTab.Connector;
                    UpdateBotTab(ref targetConnector, ref sourceConnector);
                }

                for (int i2 = 0; i2 < syntheticBond.ActiveScenarios.Count; i2++)
                {
                    BondScenario scenario = syntheticBond.ActiveScenarios[i2];

                    for (int i3 = 0; i3 < scenario.ArbitrationIceberg.MainLegs.Count; i3++)
                    {
                        BotTabSimple targetTab = scenario.ArbitrationIceberg.MainLegs[i3].BotTab;

                        bool needUpdate = true;

                        if (targetTab == null ||
                           (targetTab != null && targetTab.Connector == null))
                            needUpdate = false;
                        else if (targetTab.Connector.SecurityName != _patternBaseTab.Connector.SecurityName)
                            needUpdate = true;

                        if (needUpdate)
                        {
                            ConnectorCandles targetConnector = targetTab.Connector;
                            UpdateBotTab(ref targetConnector, ref sourceConnector);
                        }
                    }
                }
            }
        }

        private void UpdateBotTab(ref ConnectorCandles targetConnector, ref ConnectorCandles sourceConnector)
        {
            try
            {
                targetConnector.ServerFullName = sourceConnector.ServerFullName;
                targetConnector.PortfolioName = sourceConnector.PortfolioName;
                targetConnector.EmulatorIsOn = sourceConnector.EmulatorIsOn;
                targetConnector.SecurityName = sourceConnector.SecurityName;
                targetConnector.SecurityClass = sourceConnector.SecurityClass;
                targetConnector.CandleMarketDataType = sourceConnector.CandleMarketDataType;
                targetConnector.CommissionType = sourceConnector.CommissionType;
                targetConnector.CommissionValue = sourceConnector.CommissionValue;
                targetConnector.CandleCreateMethodType = sourceConnector.CandleCreateMethodType;
                targetConnector.TimeFrame = sourceConnector.TimeFrame;
                targetConnector.SaveTradesInCandles = sourceConnector.SaveTradesInCandles;
                targetConnector.ServerType = sourceConnector.ServerType;

                targetConnector.TimeFrameBuilder.Save();
                targetConnector.Save();
                targetConnector.ReconnectHard();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void ChooseFuturesSecurity(SyntheticBond syntheticBond)
        {
            syntheticBond.UpdateFuturesSecurity();
        }

        private void SecuritySubscribe(Security security)
        {
            if (StartProgram == StartProgram.IsTester
                || StartProgram == StartProgram.IsOsOptimizer)
            {
                SubscribeToTesterEndMinuteEvent();
            }

            SecuritySubscribeEvent.Invoke(security);
        }

        #endregion

        #region Tester event-driven updates

        private void SubscribeToTesterEndMinuteEvent()
        {
            List<IServer> servers = ServerMaster.GetServers();

            if (servers == null || servers.Count == 0)
            {
                return;
            }

            for (int i = 0; i < servers.Count; i++)
            {
                TesterServer testerServer = servers[i] as TesterServer;

                if (testerServer != null)
                {
                    testerServer.EndNextMinuteWithCandlesEvent -= OnTesterEndMinute;
                    testerServer.EndNextMinuteWithCandlesEvent += OnTesterEndMinute;
                    break;
                }
            }
        }

        private void OnTesterEndMinute()
        {
            try
            {
                for (int i = 0; SyntheticBonds != null && i < SyntheticBonds.Count; i++)
                {
                    SyntheticBond syntheticBond = SyntheticBonds[i];

                    if (syntheticBond.IsReadyToTrade() == false)
                        continue;

                    UpdateSeparation(syntheticBond);

                    if (syntheticBond.CalculateCointegration)
                    {
                        UpdateCointegration(syntheticBond);
                    }

                    UpdateProfitPerDay(syntheticBond);
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Main thread

        private void MainSyntheticBondThread()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(1000);

                    for (int i = 0; SyntheticBonds != null && i < SyntheticBonds.Count; i++)
                    {
                        SyntheticBond syntheticBond = SyntheticBonds[i];

                        if (syntheticBond.IsReadyToTrade() == false)
                            continue;

                        UpdateSeparation(syntheticBond);

                        if (syntheticBond.CalculateCointegration)
                        {
                            UpdateCointegration(syntheticBond);
                        }

                        UpdateProfitPerDay(syntheticBond);
                    }
                }
                catch (Exception error)
                {
                    ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void UpdateProfitPerDay(SyntheticBond syntheticBond)
        {
            try
            {
                if (syntheticBond.DaysBeforeExpiration != -1 && syntheticBond.DaysBeforeExpiration != 0 && syntheticBond.PercentSeparationCandles != null && syntheticBond.PercentSeparationCandles.Count != 0)
                {
                    syntheticBond.ProfitPerDay = syntheticBond.PercentSeparationCandles[^1].Value / syntheticBond.DaysBeforeExpiration;
                }
                else if (syntheticBond.PercentSeparationCandles != null && syntheticBond.PercentSeparationCandles.Count != 0)
                {
                    syntheticBond.ProfitPerDay = 0;
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSeparation(SyntheticBond syntheticBond)
        {
            try
            {
                if (syntheticBond.PatternBaseTab == null ||
                    syntheticBond.PatternFuturesTab == null)
                    return;

                List<Candle> candlesSec1 = syntheticBond.PatternBaseTab.CandlesAll;
                List<Candle> candlesSec2 = syntheticBond.PatternFuturesTab.CandlesAll;
                List<Candle> candlesRationingBase = null;
                List<Candle> candlesRationingFutures = null;

                if (syntheticBond.BaseUseRationing)
                {
                    if (syntheticBond.BaseRationingSecurity == null)
                    {
                        return;
                    }
                    else if (syntheticBond.BaseRationingSecurity.CandlesAll == null)
                    {
                        return;
                    }
                    else if (syntheticBond.BaseRationingSecurity.CandlesAll.Count == 0)
                    {
                        return;
                    }

                    candlesRationingBase = syntheticBond.BaseRationingSecurity.CandlesAll;
                }

                if (syntheticBond.FuturesUseRationing)
                {
                    if (syntheticBond.FuturesRationingSecurity == null)
                    {
                        return;
                    }
                    else if (syntheticBond.FuturesRationingSecurity.CandlesAll == null)
                    {
                        return;
                    }
                    else if (syntheticBond.FuturesRationingSecurity.CandlesAll.Count == 0)
                    {
                        return;
                    }

                    candlesRationingFutures = syntheticBond.FuturesRationingSecurity.CandlesAll;
                }

                int sec1Count = candlesSec1 != null ? candlesSec1.Count : 0;
                int sec2Count = candlesSec2 != null ? candlesSec2.Count : 0;
                int rationingBaseCount = candlesRationingBase != null ? candlesRationingBase.Count : 0;
                int rationingFuturesCount = candlesRationingFutures != null ? candlesRationingFutures.Count : 0;

                if (sec1Count == 0 || sec2Count == 0)
                {
                    return;
                }

                SeparationCache cache;
                _separationCaches.TryGetValue(syntheticBond, out cache);

                if (cache != null
                    && cache.ProcessedSec1 != null && cache.ProcessedSec1.Count > 0
                    && cache.ProcessedSec2 != null && cache.ProcessedSec2.Count > 0
                    && sec1Count == cache.Sec1SourceCount
                    && sec2Count == cache.Sec2SourceCount
                    && rationingBaseCount == cache.RationingBaseSourceCount
                    && rationingFuturesCount == cache.RationingFuturesSourceCount)
                {
                    bool sec1Updated = UpdateLastProcessedCandle(
                        candlesSec1[sec1Count - 1],
                        syntheticBond.BaseTimeOffset,
                        syntheticBond.BaseMultiplicator,
                        syntheticBond.BaseUseRationing,
                        candlesRationingBase,
                        syntheticBond.BaseTimeOffsetRationing,
                        syntheticBond.BaseRationingMode,
                        cache.ProcessedSec1);

                    if (sec1Updated)
                    {
                        bool sec2Updated = UpdateLastProcessedCandle(
                            candlesSec2[sec2Count - 1],
                            syntheticBond.FuturesTimeOffset,
                            syntheticBond.FuturesMultiplicator,
                            syntheticBond.FuturesUseRationing,
                            candlesRationingFutures,
                            syntheticBond.FuturesTimeOffsetRationing,
                            syntheticBond.FuturesRationingMode,
                            cache.ProcessedSec2);

                        if (sec2Updated)
                        {
                            UpdateSeparationAndNotify(cache.ProcessedSec1, cache.ProcessedSec2, syntheticBond);
                            return;
                        }
                    }
                }

                int tailSize = (int)syntheticBond.SeparationLength * 2;

                candlesSec1 = TakeTail(candlesSec1, tailSize);
                candlesSec2 = TakeTail(candlesSec2, tailSize);

                // 1 сдвигаем время

                candlesSec1 = GetOffsetCandles(candlesSec1, syntheticBond.BaseTimeOffset);
                candlesSec2 = GetOffsetCandles(candlesSec2, syntheticBond.FuturesTimeOffset);

                if (syntheticBond.BaseUseRationing)
                {
                    candlesRationingBase = TakeTail(candlesRationingBase, tailSize);
                    candlesRationingBase = GetOffsetCandles(candlesRationingBase, syntheticBond.BaseTimeOffsetRationing);
                }

                if (syntheticBond.FuturesUseRationing)
                {
                    candlesRationingFutures = TakeTail(candlesRationingFutures, tailSize);
                    candlesRationingFutures = GetOffsetCandles(candlesRationingFutures, syntheticBond.FuturesTimeOffsetRationing);
                }

                if (candlesSec1.Count > 0 &&
                    candlesSec2.Count > 0 &&
                    candlesSec1[^1].TimeStart != candlesSec2[^1].TimeStart)
                {
                    return;
                }

                // 2 умножаем на коэффициенты

                candlesSec1 = GetCoeffCandles(candlesSec1, syntheticBond.BaseMultiplicator);
                candlesSec2 = GetCoeffCandles(candlesSec2, syntheticBond.FuturesMultiplicator);

                // 3 нормализуем

                if (syntheticBond.BaseUseRationing && syntheticBond.BaseRationingMode == RationingMode.Division)
                {
                    candlesSec1 = GetDivision(candlesSec1, candlesRationingBase);
                }
                else if (syntheticBond.BaseUseRationing && syntheticBond.BaseRationingMode == RationingMode.Multiplication)
                {
                    candlesSec1 = GetMult(candlesSec1, candlesRationingBase);
                }
                else if (syntheticBond.BaseUseRationing && syntheticBond.BaseRationingMode == RationingMode.Difference)
                {
                    candlesSec1 = GetDiff(candlesSec1, candlesRationingBase);
                }
                else if (syntheticBond.BaseUseRationing && syntheticBond.BaseRationingMode == RationingMode.Addition)
                {
                    candlesSec1 = GetAddition(candlesSec1, candlesRationingBase);
                }

                if (syntheticBond.FuturesUseRationing && syntheticBond.FuturesRationingMode == RationingMode.Division)
                {
                    candlesSec2 = GetDivision(candlesSec2, candlesRationingFutures);
                }
                else if (syntheticBond.FuturesUseRationing && syntheticBond.FuturesRationingMode == RationingMode.Multiplication)
                {
                    candlesSec2 = GetMult(candlesSec2, candlesRationingFutures);
                }
                else if (syntheticBond.FuturesUseRationing && syntheticBond.FuturesRationingMode == RationingMode.Difference)
                {
                    candlesSec2 = GetDiff(candlesSec2, candlesRationingFutures);
                }
                else if (syntheticBond.FuturesUseRationing && syntheticBond.FuturesRationingMode == RationingMode.Addition)
                {
                    candlesSec2 = GetAddition(candlesSec2, candlesRationingFutures);
                }

                if (cache == null)
                {
                    cache = new SeparationCache();
                    _separationCaches[syntheticBond] = cache;
                }

                cache.ProcessedSec1 = candlesSec1;
                cache.ProcessedSec2 = candlesSec2;
                cache.Sec1SourceCount = sec1Count;
                cache.Sec2SourceCount = sec2Count;
                cache.RationingBaseSourceCount = rationingBaseCount;
                cache.RationingFuturesSourceCount = rationingFuturesCount;

                // 4 Отнимаем одно от другого

                UpdateSeparationAndNotify(candlesSec1, candlesSec2, syntheticBond);
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSeparationAndNotify(List<Candle> candlesSec1, List<Candle> candlesSec2, SyntheticBond syntheticBond)
        {
            decimal oldPercentValue = -1;
            DateTime oldPercentTime = DateTime.MinValue;
            decimal oldAbsoluteValue = -1;
            DateTime oldAbsoluteTime = DateTime.MinValue;
            int oldPercentCount = 0;
            int oldAbsoluteCount = 0;

            if (syntheticBond.PercentSeparationCandles != null && syntheticBond.PercentSeparationCandles.Count > 0)
            {
                oldPercentCount = syntheticBond.PercentSeparationCandles.Count;
                PairIndicatorValue lastPercent = syntheticBond.PercentSeparationCandles[syntheticBond.PercentSeparationCandles.Count - 1];
                oldPercentValue = lastPercent.Value;
                oldPercentTime = lastPercent.Time;
            }

            if (syntheticBond.AbsoluteSeparationCandles != null && syntheticBond.AbsoluteSeparationCandles.Count > 0)
            {
                oldAbsoluteCount = syntheticBond.AbsoluteSeparationCandles.Count;
                PairIndicatorValue lastAbsolute = syntheticBond.AbsoluteSeparationCandles[syntheticBond.AbsoluteSeparationCandles.Count - 1];
                oldAbsoluteValue = lastAbsolute.Value;
                oldAbsoluteTime = lastAbsolute.Time;
            }

            UpdateSubtractTheCandles(candlesSec1, candlesSec2, ref syntheticBond);

            bool changed = false;

            if (syntheticBond.PercentSeparationCandles != null && syntheticBond.PercentSeparationCandles.Count > 0)
            {
                PairIndicatorValue newLastPercent = syntheticBond.PercentSeparationCandles[syntheticBond.PercentSeparationCandles.Count - 1];
                if (oldPercentCount == 0
                    || newLastPercent.Value != oldPercentValue
                    || newLastPercent.Time != oldPercentTime)
                {
                    changed = true;
                }
            }
            else if (oldPercentCount > 0)
            {
                changed = true;
            }

            if (changed == false)
            {
                if (syntheticBond.AbsoluteSeparationCandles != null && syntheticBond.AbsoluteSeparationCandles.Count > 0)
                {
                    PairIndicatorValue newLastAbsolute = syntheticBond.AbsoluteSeparationCandles[syntheticBond.AbsoluteSeparationCandles.Count - 1];
                    if (oldAbsoluteCount == 0
                        || newLastAbsolute.Value != oldAbsoluteValue
                        || newLastAbsolute.Time != oldAbsoluteTime)
                    {
                        changed = true;
                    }
                }
                else if (oldAbsoluteCount > 0)
                {
                    changed = true;
                }
            }

            if (changed)
            {
                ContangoChangeEvent?.Invoke(syntheticBond);
            }
        }

        private void UpdateSubtractTheCandles(List<Candle> candlesOne, List<Candle> candlesTwo, ref SyntheticBond bond)
        {
            List<PairIndicatorValue> valuePercentCandles = new List<PairIndicatorValue>();
            List<PairIndicatorValue> valueAbsoluteCandles = new List<PairIndicatorValue>();

            for (int indFirstSec = candlesOne.Count - 1, indSecondSec = candlesTwo.Count - 1;
                indFirstSec >= 0 && indSecondSec >= 0; indFirstSec--, indSecondSec--)
            {
                if (valuePercentCandles.Count == bond.SeparationLength && valueAbsoluteCandles.Count == bond.SeparationLength)
                    break;

                Candle first = candlesOne[indFirstSec];
                Candle second = candlesTwo[indSecondSec];

                if (first.TimeStart > second.TimeStart)
                {
                    indSecondSec++;
                    continue;
                }
                if (second.TimeStart > first.TimeStart)
                {
                    indFirstSec++;
                    continue;
                }

                decimal valueOpen = 0;
                decimal valueHigh = 0;
                decimal valueLow = 0;
                decimal valueClose = 0;

                if (bond.MainRationingMode == RationingMode.Addition)
                {
                    valueOpen = second.Open + first.Open;
                    valueHigh = second.High + first.High;
                    valueLow = second.Low + first.Low;
                    valueClose = second.Close + first.Close;
                }
                else if (bond.MainRationingMode == RationingMode.Multiplication)
                {
                    valueOpen = second.Open * first.Open;
                    valueHigh = second.High * first.High;
                    valueLow = second.Low * first.Low;
                    valueClose = second.Close * first.Close;
                }
                else if (bond.MainRationingMode == RationingMode.Division)
                {
                    valueOpen = second.Open / first.Open;
                    valueHigh = second.High / first.High;
                    valueLow = second.Low / first.Low;
                    valueClose = second.Close / first.Close;
                }
                else if (bond.MainRationingMode == RationingMode.Difference)
                {
                    valueOpen = second.Open - first.Open;
                    valueHigh = second.High - first.High;
                    valueLow = second.Low - first.Low;
                    valueClose = second.Close - first.Close;
                }

                PairIndicatorValue bondIndicatorAbsoluteValue = new PairIndicatorValue();
                bondIndicatorAbsoluteValue.Value = Math.Round(valueClose, 5);
                bondIndicatorAbsoluteValue.Time = first.TimeStart;

                valueAbsoluteCandles.Add(bondIndicatorAbsoluteValue);

                valueOpen = Math.Round(valueOpen / (first.Open / 100), 5);
                valueHigh = Math.Round(valueHigh / (first.High / 100), 5);
                valueLow = Math.Round(valueLow / (first.Low / 100), 5);
                valueClose = Math.Round(valueClose / (first.Close / 100), 5);

                if (valueClose < valueLow)
                {
                    valueLow = valueClose;
                }

                if (valueOpen < valueLow)
                {
                    valueLow = valueOpen;
                }

                if (valueClose > valueHigh)
                {
                    valueHigh = valueClose;
                }

                if (valueOpen > valueHigh)
                {
                    valueHigh = valueOpen;
                }

                PairIndicatorValue bondIndicatorPercentValue = new PairIndicatorValue();
                bondIndicatorPercentValue.Value = valueClose;
                bondIndicatorPercentValue.Time = first.TimeStart;

                valuePercentCandles.Add(bondIndicatorPercentValue);
            }

            if (valueAbsoluteCandles != null &&
                valueAbsoluteCandles.Count > 1)
            {
                valueAbsoluteCandles.Reverse();
            }

            if (valuePercentCandles != null &&
                valuePercentCandles.Count > 1)
            {
                valuePercentCandles.Reverse();
            }

            bond.PercentSeparationCandles = valuePercentCandles;
            bond.AbsoluteSeparationCandles = valueAbsoluteCandles;
        }

        private List<Candle> GetAddition(List<Candle> candlesOne, List<Candle> candlesTwo)
        {
            List<Candle> newCandles = new List<Candle>();

            for (int i = candlesOne.Count - 1, i2 = candlesTwo.Count - 1; i > -1 && i2 > -1; i--, i2--)
            {
                Candle first = candlesOne[i];
                Candle second = candlesTwo[i2];

                if (first.TimeStart > second.TimeStart)
                {
                    i--;
                    continue;
                }
                if (second.TimeStart > first.TimeStart)
                {
                    i2--;
                    continue;
                }

                decimal valueOpen = Math.Round(first.Open + second.Open, 6);
                decimal valueHigh = Math.Round(first.High + second.High, 6);
                decimal valueLow = Math.Round(first.Low + second.Low, 6);
                decimal valueClose = Math.Round(first.Close + second.Close, 6);

                if (valueClose < valueLow)
                {
                    valueLow = valueClose;
                }

                if (valueOpen < valueLow)
                {
                    valueLow = valueOpen;
                }

                if (valueClose > valueHigh)
                {
                    valueHigh = valueClose;
                }
                if (valueOpen > valueHigh)
                {
                    valueHigh = valueOpen;
                }

                Candle newCandle = new Candle();
                newCandle.Open = valueOpen;
                newCandle.Low = valueLow;
                newCandle.High = valueHigh;
                newCandle.Close = valueClose;

                newCandle.Volume = 1;
                newCandle.TimeStart = first.TimeStart;

                newCandles.Add(newCandle);
            }

            if (newCandles != null &&
                newCandles.Count > 1)
            {
                newCandles.Reverse();
            }

            return newCandles;
        }

        private List<Candle> GetDiff(List<Candle> candlesOne, List<Candle> candlesTwo)
        {
            List<Candle> newCandles = new List<Candle>();

            for (int i = candlesOne.Count - 1, i2 = candlesTwo.Count - 1; i > -1 && i2 > -1; i--, i2--)
            {
                Candle first = candlesOne[i];
                Candle second = candlesTwo[i2];

                if (first.TimeStart > second.TimeStart)
                {
                    i--;
                    continue;
                }
                if (second.TimeStart > first.TimeStart)
                {
                    i2--;
                    continue;
                }

                decimal valueOpen = Math.Round(first.Open - second.Open, 6);
                decimal valueHigh = Math.Round(first.High - second.High, 6);
                decimal valueLow = Math.Round(first.Low - second.Low, 6);
                decimal valueClose = Math.Round(first.Close - second.Close, 6);

                if (valueClose < valueLow)
                {
                    valueLow = valueClose;
                }

                if (valueOpen < valueLow)
                {
                    valueLow = valueOpen;
                }

                if (valueClose > valueHigh)
                {
                    valueHigh = valueClose;
                }
                if (valueOpen > valueHigh)
                {
                    valueHigh = valueOpen;
                }

                Candle newCandle = new Candle();
                newCandle.Open = valueOpen;
                newCandle.Low = valueLow;
                newCandle.High = valueHigh;
                newCandle.Close = valueClose;

                newCandle.Volume = 1;
                newCandle.TimeStart = first.TimeStart;

                newCandles.Add(newCandle);
            }

            if (newCandles != null &&
                newCandles.Count > 1)
            {
                newCandles.Reverse();
            }

            return newCandles;
        }

        private List<Candle> GetMult(List<Candle> candlesOne, List<Candle> candlesTwo)
        {
            List<Candle> newCandles = new List<Candle>();

            for (int i = candlesOne.Count - 1, i2 = candlesTwo.Count - 1; i > -1 && i2 > -1; i--, i2--)
            {
                Candle first = candlesOne[i];
                Candle second = candlesTwo[i2];

                if (first.TimeStart > second.TimeStart)
                { // в случае если время не равно
                    i--;
                    continue;
                }
                if (second.TimeStart > first.TimeStart)
                { // в случае если время не равно
                    i2--;
                    continue;
                }

                decimal valueOpen = Math.Round(first.Open * second.Open, 6);
                decimal valueHigh = Math.Round(first.High * second.High, 6);
                decimal valueLow = Math.Round(first.Low * second.Low, 6);
                decimal valueClose = Math.Round(first.Close * second.Close, 6);

                if (valueClose < valueLow)
                {
                    valueLow = valueClose;
                }

                if (valueOpen < valueLow)
                {
                    valueLow = valueOpen;
                }

                if (valueClose > valueHigh)
                {
                    valueHigh = valueClose;
                }
                if (valueOpen > valueHigh)
                {
                    valueHigh = valueOpen;
                }

                Candle newCandle = new Candle();
                newCandle.Open = valueOpen;
                newCandle.Low = valueLow;
                newCandle.High = valueHigh;
                newCandle.Close = valueClose;

                newCandle.Volume = 1;
                newCandle.TimeStart = first.TimeStart;

                newCandles.Add(newCandle);
            }

            if (newCandles != null &&
                newCandles.Count > 1)
            {
                newCandles.Reverse();
            }

            return newCandles;
        }

        private List<Candle> GetDivision(List<Candle> candlesOne, List<Candle> candlesTwo)
        {
            List<Candle> newCandles = new List<Candle>();

            for (int i = candlesOne.Count - 1, i2 = candlesTwo.Count - 1; i > -1 && i2 > -1; i--, i2--)
            {
                Candle first = candlesOne[i];
                Candle second = candlesTwo[i2];

                if (first.TimeStart > second.TimeStart)
                { // в случае если время не равно
                    i--;
                    continue;
                }
                if (second.TimeStart > first.TimeStart)
                { // в случае если время не равно
                    i2--;
                    continue;
                }

                decimal valueOpen = Math.Round(first.Open / second.Open, 6);
                decimal valueHigh = Math.Round(first.High / second.High, 6);
                decimal valueLow = Math.Round(first.Low / second.Low, 6);
                decimal valueClose = Math.Round(first.Close / second.Close, 6);

                if (valueClose < valueLow)
                {
                    valueLow = valueClose;
                }

                if (valueOpen < valueLow)
                {
                    valueLow = valueOpen;
                }

                if (valueClose > valueHigh)
                {
                    valueHigh = valueClose;
                }
                if (valueOpen > valueHigh)
                {
                    valueHigh = valueOpen;
                }

                Candle newCandle = new Candle();
                newCandle.Open = valueOpen;
                newCandle.Low = valueLow;
                newCandle.High = valueHigh;
                newCandle.Close = valueClose;

                newCandle.Volume = 1;
                newCandle.TimeStart = first.TimeStart;

                newCandles.Add(newCandle);
            }

            if (newCandles != null &&
                newCandles.Count > 1)
            {
                newCandles.Reverse();
            }

            return newCandles;
        }

        private List<Candle> GetCoeffCandles(List<Candle> candles, decimal coeff)
        {
            if (coeff == 0 || coeff == 1)
            {
                return candles;
            }

            if (candles == null ||
                candles.Count == 0)
            {
                return candles;
            }

            List<Candle> newCandles = new List<Candle>();

            for (int i = 0; i < candles.Count; i++)
            {
                Candle curCandle = candles[i];

                Candle newCandle = new Candle();
                newCandle.Low = curCandle.Low * coeff;
                newCandle.High = curCandle.High * coeff;
                newCandle.Open = curCandle.Open * coeff;
                newCandle.Close = curCandle.Close * coeff;
                newCandle.Volume = curCandle.Volume;
                newCandle.TimeStart = curCandle.TimeStart;

                newCandles.Add(newCandle);
            }

            return newCandles;
        }

        private List<Candle> GetOffsetCandles(List<Candle> candles, int hoursOffset)
        {
            if (hoursOffset == 0)
            {
                return candles;
            }

            if (candles == null ||
                candles.Count == 0)
            {
                return candles;
            }


            List<Candle> newCandles = new List<Candle>();

            for (int i = 0; i < candles.Count; i++)
            {
                Candle curCandle = candles[i];

                Candle newCandle = new Candle();
                newCandle.Low = curCandle.Low;
                newCandle.High = curCandle.High;
                newCandle.Open = curCandle.Open;
                newCandle.Close = curCandle.Close;
                newCandle.Volume = curCandle.Volume;
                newCandle.TimeStart = curCandle.TimeStart.AddHours(hoursOffset);

                newCandles.Add(newCandle);
            }

            return newCandles;
        }

        private List<Candle> TakeTail(List<Candle> candles, int count)
        {
            if (candles == null || candles.Count <= count)
            {
                return candles;
            }

            return candles.GetRange(candles.Count - count, count);
        }

        private Candle ProcessSingleCandle(Candle source, int hoursOffset, decimal coeff)
        {
            Candle result = new Candle();

            if (coeff == 0 || coeff == 1)
            {
                result.Open = source.Open;
                result.High = source.High;
                result.Low = source.Low;
                result.Close = source.Close;
            }
            else
            {
                result.Open = source.Open * coeff;
                result.High = source.High * coeff;
                result.Low = source.Low * coeff;
                result.Close = source.Close * coeff;
            }

            result.Volume = source.Volume;
            result.TimeStart = source.TimeStart.AddHours(hoursOffset);

            return result;
        }

        private Candle ApplyNormalizationSingle(Candle candle, Candle rationingCandle, RationingMode mode)
        {
            Candle result = new Candle();
            result.Volume = candle.Volume;
            result.TimeStart = candle.TimeStart;

            if (mode == RationingMode.Division)
            {
                result.Open = Math.Round(candle.Open / rationingCandle.Open, 6);
                result.High = Math.Round(candle.High / rationingCandle.High, 6);
                result.Low = Math.Round(candle.Low / rationingCandle.Low, 6);
                result.Close = Math.Round(candle.Close / rationingCandle.Close, 6);
            }
            else if (mode == RationingMode.Multiplication)
            {
                result.Open = Math.Round(candle.Open * rationingCandle.Open, 6);
                result.High = Math.Round(candle.High * rationingCandle.High, 6);
                result.Low = Math.Round(candle.Low * rationingCandle.Low, 6);
                result.Close = Math.Round(candle.Close * rationingCandle.Close, 6);
            }
            else if (mode == RationingMode.Difference)
            {
                result.Open = Math.Round(candle.Open - rationingCandle.Open, 6);
                result.High = Math.Round(candle.High - rationingCandle.High, 6);
                result.Low = Math.Round(candle.Low - rationingCandle.Low, 6);
                result.Close = Math.Round(candle.Close - rationingCandle.Close, 6);
            }
            else if (mode == RationingMode.Addition)
            {
                result.Open = Math.Round(candle.Open + rationingCandle.Open, 6);
                result.High = Math.Round(candle.High + rationingCandle.High, 6);
                result.Low = Math.Round(candle.Low + rationingCandle.Low, 6);
                result.Close = Math.Round(candle.Close + rationingCandle.Close, 6);
            }

            if (result.Close < result.Low)
            {
                result.Low = result.Close;
            }
            if (result.Open < result.Low)
            {
                result.Low = result.Open;
            }
            if (result.Close > result.High)
            {
                result.High = result.Close;
            }
            if (result.Open > result.High)
            {
                result.High = result.Open;
            }

            return result;
        }

        private bool UpdateLastProcessedCandle(
            Candle rawLastCandle,
            int hoursOffset,
            decimal multiplicator,
            bool useRationing,
            List<Candle> rationingCandles,
            int rationingTimeOffset,
            RationingMode rationingMode,
            List<Candle> processedList)
        {
            Candle processed = ProcessSingleCandle(rawLastCandle, hoursOffset, multiplicator);

            if (useRationing)
            {
                if (rationingCandles == null || rationingCandles.Count == 0)
                {
                    return false;
                }

                Candle rawLastRationing = rationingCandles[rationingCandles.Count - 1];
                Candle processedRationing = ProcessSingleCandle(rawLastRationing, rationingTimeOffset, 1);

                if (processedRationing.TimeStart != processed.TimeStart)
                {
                    return false;
                }

                processed = ApplyNormalizationSingle(processed, processedRationing, rationingMode);
            }

            processedList[processedList.Count - 1] = processed;
            return true;
        }

        private void UpdateCointegration(SyntheticBond syntheticBond)
        {
            try
            {
                List<PairIndicatorValue> oldCointegration = syntheticBond.CointegrationBuilder.Cointegration;
                int oldCount = 0;
                decimal oldLastValue = 0;

                if (oldCointegration != null && oldCointegration.Count > 0)
                {
                    oldCount = oldCointegration.Count;
                    oldLastValue = oldCointegration[oldCount - 1].Value;
                }

                List<Candle> candlesSec1;
                List<Candle> candlesSec2;
                GetProcessedCandles(syntheticBond, out candlesSec1, out candlesSec2);

                if (candlesSec1 == null ||
                  (candlesSec1 != null && candlesSec1.Count == 0))
                    candlesSec1 = syntheticBond.PatternBaseTab.CandlesAll;

                if (candlesSec2 == null ||
                    (candlesSec2 != null && candlesSec2.Count == 0))
                    candlesSec2 = syntheticBond.PatternFuturesTab.CandlesAll;

                if (candlesSec1 == null || candlesSec2 == null) return;

                bool needBeautifulValues = StartProgram == StartProgram.IsOsTrader;
                syntheticBond.CointegrationBuilder.ReloadCointegration(candlesSec1, candlesSec2, needBeautifulValues);

                MinBalancesChangeEvent?.Invoke(syntheticBond);

                List<PairIndicatorValue> newCointegration = syntheticBond.CointegrationBuilder.Cointegration;
                int newCount = 0;

                if (newCointegration != null)
                {
                    newCount = newCointegration.Count;
                }

                bool changed = false;

                if (newCount != oldCount)
                {
                    changed = true;
                }
                else if (newCount > 0 && newCointegration[newCount - 1].Value != oldLastValue)
                {
                    changed = true;
                }

                if (changed)
                {
                    CointegrationChangeEvent?.Invoke(syntheticBond);
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Offset logic

        public void ShowOffsetWindow(ref SyntheticBond syntheticBond)
        {
            try
            {
                string key = syntheticBond.SelectedScenario.ArbitrationIceberg.MainLegs[0].BotTab.TabName;

                if (_bondsOffsetUi.TryGetValue(key, out SyntheticBondOffsetUi existingWindow))
                {
                    existingWindow.Activate();
                    existingWindow.WindowState = WindowState.Normal;
                    existingWindow.Focus();
                    return;
                }

                SyntheticBondOffsetUi bondOffsetUi = new SyntheticBondOffsetUi(this, ref syntheticBond);
                bondOffsetUi.Closed += BondOffsetUi_Closed;

                bondOffsetUi.Tag = key;

                if (_bondsOffsetUi.TryAdd(key, bondOffsetUi))
                {
                    bondOffsetUi.Show();
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void BondOffsetUi_Closed(object sender, EventArgs e)
        {
            SyntheticBondOffsetUi closedWindow = (SyntheticBondOffsetUi)sender;
            closedWindow.Closed -= BondOffsetUi_Closed;
            _bondsOffsetUi.TryRemove(closedWindow.Key, out _);

            OnSettingsChanged();
        }

        #endregion

        #region Chart logic

        public void ShowChartWindow(ref SyntheticBond syntheticBond)
        {
            try
            {
                string key = syntheticBond.SelectedScenario.ArbitrationIceberg.MainLegs[0].BotTab.TabName;

                if (_bondsChartUi.TryGetValue(key, out SyntheticBondChartUi existingWindow))
                {
                    existingWindow.Activate();
                    existingWindow.WindowState = WindowState.Normal;
                    existingWindow.Focus();
                    return;
                }

                SyntheticBondChartUi bondChartUi = new SyntheticBondChartUi(this, ref syntheticBond);
                bondChartUi.Closed += BondChartUi_Closed;

                bondChartUi.Tag = key;

                if (_bondsChartUi.TryAdd(key, bondChartUi))
                {
                    bondChartUi.Show();
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void BondChartUi_Closed(object sender, EventArgs e)
        {
            SyntheticBondChartUi closedWindow = (SyntheticBondChartUi)sender;
            closedWindow.Closed -= BondChartUi_Closed;
            _bondsChartUi.TryRemove(closedWindow.Key, out _);

            OnSettingsChanged();
        }

        #endregion

        #region Trade logic

        public void ShowTradeWindow(ref SyntheticBond syntheticBond)
        {
            try
            {
                if (syntheticBond.PatternFuturesTab == null)
                {
                    return;
                }

                string key = syntheticBond.SelectedScenario.ArbitrationIceberg.MainLegs[0].BotTab.TabName;

                if (_bondsTradeUi.TryGetValue(key, out SyntheticBondTradeUi existingWindow))
                {
                    existingWindow.Activate();
                    existingWindow.WindowState = WindowState.Normal;
                    existingWindow.Focus();
                    return;
                }

                SyntheticBondTradeUi bondTradeUi = new SyntheticBondTradeUi(this, ref syntheticBond);
                bondTradeUi.Closed += BondTradeUi_Closed;

                bondTradeUi.Tag = key;

                if (_bondsTradeUi.TryAdd(key, bondTradeUi))
                {
                    bondTradeUi.Show();
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void BondTradeUi_Closed(object sender, EventArgs e)
        {
            SyntheticBondTradeUi closedWindow = (SyntheticBondTradeUi)sender;
            closedWindow.Closed -= BondTradeUi_Closed;
            _bondsTradeUi.TryRemove(closedWindow.Key, out _);

            OnSettingsChanged();
        }

        public void CloseTradeWindow(SyntheticBond syntheticBond)
        {
            if (syntheticBond.SelectedScenario == null ||
                (syntheticBond.SelectedScenario != null &&
                (syntheticBond.SelectedScenario.ArbitrationIceberg.MainLegs[0].BotTab == null)))
            {
                return;
            }

            string key = syntheticBond.SelectedScenario.ArbitrationIceberg.MainLegs[0].BotTab.TabName;

            if (_bondsTradeUi.TryRemove(key, out SyntheticBondTradeUi window))
            {
                window.Closed -= BondTradeUi_Closed;

                try
                {
                    window.Close();
                }
                catch
                {
                    // ignore
                }
            }
        }

        #endregion

        #region Private fields

        private StartProgram StartProgram;

        private ConcurrentDictionary<string, SyntheticBondTradeUi> _bondsTradeUi = new ConcurrentDictionary<string, SyntheticBondTradeUi>();
        private ConcurrentDictionary<string, SyntheticBondChartUi> _bondsChartUi = new ConcurrentDictionary<string, SyntheticBondChartUi>();
        private ConcurrentDictionary<string, SyntheticBondOffsetUi> _bondsOffsetUi = new ConcurrentDictionary<string, SyntheticBondOffsetUi>();

        private ConcurrentDictionary<SyntheticBond, SeparationCache> _separationCaches = new ConcurrentDictionary<SyntheticBond, SeparationCache>();

        private BotTabSimple _patternBaseTab;

        #endregion

        #region Public properties

        public string UniqueName;

        public List<SyntheticBond> SyntheticBonds;

        public int UniqueNumber;

        public BotTabSimple PatternBaseTab
        {
            get { return _patternBaseTab; }
            set
            {
                if (_patternBaseTab != null)
                {
                    _patternBaseTab.Delete();
                    _patternBaseTab = null;
                }

                _patternBaseTab = value;
            }
        }

        public bool EventsIsOn
        {
            get
            {
                return _patternBaseTab.EventsIsOn;
            }
            set
            {
                if (_patternBaseTab == null)
                    return;

                if (_patternBaseTab.EventsIsOn != value)
                    _patternBaseTab.EventsIsOn = value;

                for (int i = 0; i < SyntheticBonds.Count; i++)
                {
                    SyntheticBond syntheticBond = SyntheticBonds[i];

                    for (int i2 = 0; i2 < syntheticBond.ActiveScenarios.Count; i2++)
                    {
                        BondScenario scenario = syntheticBond.ActiveScenarios[i2];

                        if (scenario.ArbitrationIceberg.MainLegs[0].BotTab != null &&
                            scenario.ArbitrationIceberg.MainLegs[0].BotTab.EventsIsOn != value)
                        {
                            scenario.ArbitrationIceberg.MainLegs[0].BotTab.EventsIsOn = value;
                        }

                        if (scenario.ArbitrationIceberg.SecondaryLegs[0].BotTab != null &&
                            scenario.ArbitrationIceberg.SecondaryLegs[0].BotTab.EventsIsOn != value)
                        {
                            scenario.ArbitrationIceberg.SecondaryLegs[0].BotTab.EventsIsOn = value;
                        }

                        if (syntheticBond.BaseRationingSecurity != null &&
                            syntheticBond.BaseRationingSecurity.EventsIsOn != value)
                        {
                            syntheticBond.BaseRationingSecurity.EventsIsOn = value;
                        }

                        if (syntheticBond.FuturesRationingSecurity != null &&
                            syntheticBond.FuturesRationingSecurity.EventsIsOn != value)
                        {
                            syntheticBond.FuturesRationingSecurity.EventsIsOn = value;
                        }
                    }
                }
            }
        }

        public bool EmulatorIsOn
        {
            get
            {
                return _patternBaseTab.EmulatorIsOn;
            }
            set
            {
                if (_patternBaseTab == null)
                    return;

                if (_patternBaseTab.EmulatorIsOn != value)
                    _patternBaseTab.EmulatorIsOn = value;

                for (int i = 0; i < SyntheticBonds.Count; i++)
                {
                    SyntheticBond syntheticBond = SyntheticBonds[i];

                    for (int i2 = 0; i2 < syntheticBond.ActiveScenarios.Count; i2++)
                    {
                        BondScenario scenario = syntheticBond.ActiveScenarios[i2];

                        if (scenario.ArbitrationIceberg.MainLegs[0].BotTab != null &&
                            scenario.ArbitrationIceberg.MainLegs[0].BotTab.EmulatorIsOn != value)
                        {
                            scenario.ArbitrationIceberg.MainLegs[0].BotTab.EmulatorIsOn = value;
                        }

                        if (scenario.ArbitrationIceberg.SecondaryLegs[0].BotTab != null &&
                            scenario.ArbitrationIceberg.SecondaryLegs[0].BotTab.EmulatorIsOn != value)
                        {
                            scenario.ArbitrationIceberg.SecondaryLegs[0].BotTab.EmulatorIsOn = value;
                        }

                        if (syntheticBond.BaseRationingSecurity != null &&
                            syntheticBond.BaseRationingSecurity.EmulatorIsOn != value)
                        {
                            syntheticBond.BaseRationingSecurity.EmulatorIsOn = value;
                        }

                        if (syntheticBond.FuturesRationingSecurity != null &&
                            syntheticBond.FuturesRationingSecurity.EmulatorIsOn != value)
                        {
                            syntheticBond.FuturesRationingSecurity.EmulatorIsOn = value;
                        }
                    }
                }
            }
        }

        public event Action<Security> SecuritySubscribeEvent;

        public event Action<SyntheticBond> ContangoChangeEvent;

        public event Action<SyntheticBond> CointegrationChangeEvent;

        public event Action<SyntheticBond> MinBalancesChangeEvent;

        public event Action SettingsChangedEvent;

        public void GetProcessedCandles(SyntheticBond bond, out List<Candle> processedSec1, out List<Candle> processedSec2)
        {
            processedSec1 = null;
            processedSec2 = null;

            SeparationCache cache;
            if (_separationCaches.TryGetValue(bond, out cache) && cache != null)
            {
                processedSec1 = cache.ProcessedSec1;
                processedSec2 = cache.ProcessedSec2;
            }
        }

        #endregion

        #region SeparationCache

        private class SeparationCache
        {
            public List<Candle> ProcessedSec1;

            public List<Candle> ProcessedSec2;

            public int Sec1SourceCount;

            public int Sec2SourceCount;

            public int RationingBaseSourceCount;

            public int RationingFuturesSourceCount;
        }

        #endregion
    }
}
