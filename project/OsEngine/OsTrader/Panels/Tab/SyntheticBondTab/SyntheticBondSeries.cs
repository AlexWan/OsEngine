using OsEngine.Entity;
using OsEngine.Entity.SynteticBondEntity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Isberg;
using OsEngine.OsTrader.Panels.Tab;
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

        public SyntheticBondSeries(StartProgram startProgram, string tabName)
        {
            StartProgram = startProgram;
            SynteticBondName = tabName;

            LoadSettingsSyntheticBond();

            if (startProgram == StartProgram.IsOsTrader)
            {
                Thread thread = new Thread(MainSynteticBondThread);
                thread.Start();
            }
            else if (startProgram == StartProgram.IsTester
                || startProgram == StartProgram.IsOsOptimizer)
            {
                SubscribeToTesterEndMinuteEvent();
            }
        }

        public void SaveSettingsSyntheticBond()
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
                using (StreamWriter writer = new StreamWriter(@"Engine\" + SynteticBondName + @"SynteticBondModificationsFuturesToLoad.txt", false))
                {
                    for (int i = 0; i < SyntheticBonds.Count; i++)
                    {
                        SyntheticBond modification = SyntheticBonds[i];

                        writer.WriteLine(modification.BaseIsbergParameters.BotTab.TabName);
                        writer.WriteLine(modification.FuturesIsbergParameters.BotTab.TabName);

                        writer.WriteLine(modification.BaseMultiplicator.ToString());
                        writer.WriteLine(modification.FuturesMultiplicator.ToString());
                        writer.WriteLine(modification.BaseUseRationing.ToString());
                        writer.WriteLine(modification.FuturesUseRationing.ToString());
                        writer.WriteLine(modification.BaseRationingMode.ToString());
                        writer.WriteLine(modification.FuturesRationingMode.ToString());

                        writer.WriteLine(modification.BaseRationingSecurity == null
                            ? "None"
                            : modification.BaseRationingSecurity.TabName);

                        writer.WriteLine(modification.FuturesRationingSecurity == null
                            ? "None"
                            : modification.FuturesRationingSecurity.TabName);

                        writer.WriteLine(modification.BaseTimeOffset.ToString());
                        writer.WriteLine(modification.FuturesTimeOffset.ToString());
                        writer.WriteLine(modification.BaseTimeOffsetRationing.ToString());
                        writer.WriteLine(modification.FuturesTimeOffsetRationing.ToString());

                        writer.WriteLine(modification.CointegrationBuilder.CointegrationLookBack.ToString());
                        writer.WriteLine(modification.CointegrationBuilder.CointegrationDeviation.ToString());
                        writer.WriteLine(modification.SeparationLength.ToString());
                        writer.WriteLine(modification.MainRationingMode.ToString());

                        writer.WriteLine("Scenarios:" + modification.Scenarios.Count.ToString());
                        for (int s = 0; s < modification.Scenarios.Count; s++)
                        {
                            BondScenario scenario = modification.Scenarios[s];
                            writer.WriteLine(scenario.Name);
                            writer.WriteLine(scenario.MaxSpread.ToString());
                            writer.WriteLine(scenario.MinSpread.ToString());
                        }
                    }
                }

                for (int i = 0; i < SyntheticBonds.Count; i++)
                {
                    for (int s = 0; s < SyntheticBonds[i].Scenarios.Count; s++)
                    {
                        SyntheticBonds[i].Scenarios[s].ArbitrationIceberg?.Save();
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
            SaveSettingsSyntheticBond();
            SettingsChangedEvent?.Invoke();
        }

        private void CreateSyntheticBondFromFile(ref SyntheticBond syntheticBond, StreamReader reader)
        {
            try
            {
                string baseTabName = reader.ReadLine();
                string futuresTabName = reader.ReadLine();

                BotTabSimple baseBotTab = new BotTabSimple(baseTabName, StartProgram);
                baseBotTab.SecuritySubscribeEvent += SecuritySubscribe;

                BotTabSimple futuresBotTab = new BotTabSimple(futuresTabName, StartProgram);
                futuresBotTab.SecuritySubscribeEvent += SecuritySubscribe;

                syntheticBond.BaseIsbergParameters = new ArbitrationParameters();
                syntheticBond.BaseIsbergParameters.BotTab = baseBotTab;

                syntheticBond.FuturesIsbergParameters = new ArbitrationParameters();
                syntheticBond.FuturesIsbergParameters.BotTab = futuresBotTab;

                syntheticBond.Scenarios = new List<BondScenario>();

                syntheticBond.BaseMultiplicator = Convert.ToInt32(reader.ReadLine());
                syntheticBond.FuturesMultiplicator = Convert.ToInt32(reader.ReadLine());
                syntheticBond.BaseUseRationing = Convert.ToBoolean(reader.ReadLine());
                syntheticBond.FuturesUseRationing = Convert.ToBoolean(reader.ReadLine());

                string baseRationingMode = reader.ReadLine();
                if (baseRationingMode == "Division") syntheticBond.BaseRationingMode = RationingMode.Division;
                else if (baseRationingMode == "Multiplication") syntheticBond.BaseRationingMode = RationingMode.Multiplication;
                else if (baseRationingMode == "Difference") syntheticBond.BaseRationingMode = RationingMode.Difference;
                else if (baseRationingMode == "Addition") syntheticBond.BaseRationingMode = RationingMode.Addition;

                string futuresRationingMode = reader.ReadLine();
                if (futuresRationingMode == "Division") syntheticBond.FuturesRationingMode = RationingMode.Division;
                else if (futuresRationingMode == "Multiplication") syntheticBond.FuturesRationingMode = RationingMode.Multiplication;
                else if (futuresRationingMode == "Difference") syntheticBond.FuturesRationingMode = RationingMode.Difference;
                else if (futuresRationingMode == "Addition") syntheticBond.FuturesRationingMode = RationingMode.Addition;

                string baseRationingSecurity = reader.ReadLine();
                if (baseRationingSecurity == "None")
                {
                    syntheticBond.BaseRationingSecurity = null;
                }
                else
                {
                    syntheticBond.BaseRationingSecurity = new BotTabSimple(baseRationingSecurity, StartProgram);
                    syntheticBond.BaseRationingSecurity.SecuritySubscribeEvent += SecuritySubscribe;
                }

                string futuresRationingSecurity = reader.ReadLine();
                if (futuresRationingSecurity == "None")
                {
                    syntheticBond.FuturesRationingSecurity = null;
                }
                else
                {
                    syntheticBond.FuturesRationingSecurity = new BotTabSimple(futuresRationingSecurity, StartProgram);
                    syntheticBond.FuturesRationingSecurity.SecuritySubscribeEvent += SecuritySubscribe;
                }

                syntheticBond.BaseTimeOffset = Convert.ToInt32(reader.ReadLine());
                syntheticBond.FuturesTimeOffset = Convert.ToInt32(reader.ReadLine());
                syntheticBond.BaseTimeOffsetRationing = Convert.ToInt32(reader.ReadLine());
                syntheticBond.FuturesTimeOffsetRationing = Convert.ToInt32(reader.ReadLine());

                syntheticBond.CointegrationBuilder = new CointegrationBuilder();
                syntheticBond.CointegrationBuilder.CointegrationLookBack = Convert.ToInt32(reader.ReadLine());
                syntheticBond.CointegrationBuilder.CointegrationDeviation = reader.ReadLine().ToDecimal();
                syntheticBond.SeparationLength = reader.ReadLine().ToDecimal();

                string rationingMainMode = reader.ReadLine();
                if (rationingMainMode == "Division") syntheticBond.MainRationingMode = RationingMode.Division;
                else if (rationingMainMode == "Multiplication") syntheticBond.MainRationingMode = RationingMode.Multiplication;
                else if (rationingMainMode == "Difference") syntheticBond.MainRationingMode = RationingMode.Difference;
                else if (rationingMainMode == "Addition") syntheticBond.MainRationingMode = RationingMode.Addition;

                string scenariosLine = reader.ReadLine();

                if (scenariosLine != null && scenariosLine.StartsWith("Scenarios:"))
                {
                    int scenarioCount;
                    if (!int.TryParse(scenariosLine.Substring("Scenarios:".Length), out scenarioCount))
                    {
                        scenarioCount = 0;
                    }

                    for (int s = 0; s < scenarioCount; s++)
                    {
                        string scenarioName = reader.ReadLine() ?? "Script 1";
                        decimal maxSpread = reader.ReadLine().ToDecimal();
                        decimal minSpread = reader.ReadLine().ToDecimal();

                        BondScenario scenario = new BondScenario(futuresTabName, scenarioName);
                        scenario.MaxSpread = maxSpread;
                        scenario.MinSpread = minSpread;
                        scenario.SetBotTabs(baseBotTab, futuresBotTab);
                        syntheticBond.Scenarios.Add(scenario);
                    }

                    if (syntheticBond.Scenarios.Count == 0)
                    {
                        BondScenario defaultScenario = new BondScenario(futuresTabName, "Script 1");
                        defaultScenario.SetBotTabs(baseBotTab, futuresBotTab);
                        syntheticBond.Scenarios.Add(defaultScenario);
                    }
                }
                else
                {
                    decimal legacyMaxSpread = scenariosLine != null ? scenariosLine.ToDecimal() : 0;
                    decimal legacyMinSpread = reader.ReadLine().ToDecimal();
                    reader.ReadLine();
                    reader.ReadLine();
                    reader.ReadLine();

                    BondScenario defaultScenario = new BondScenario(futuresTabName, "Script 1");
                    defaultScenario.MaxSpread = legacyMaxSpread;
                    defaultScenario.MinSpread = legacyMinSpread;
                    defaultScenario.SetBotTabs(baseBotTab, futuresBotTab);
                    syntheticBond.Scenarios.Add(defaultScenario);
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void LoadSettingsSyntheticBond()
        {
            try
            {
                if (!File.Exists(@"Engine\" + SynteticBondName + @"SynteticBondModificationsFuturesToLoad.txt"))
                {
                    return;
                }

                SyntheticBonds = new List<SyntheticBond>();

                using (StreamReader reader = new StreamReader(@"Engine\" + SynteticBondName + @"SynteticBondModificationsFuturesToLoad.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        SyntheticBond syntheticBond = new SyntheticBond();

                        CreateSyntheticBondFromFile(ref syntheticBond, reader);

                        SyntheticBonds.Add(syntheticBond);
                    }

                    reader.Close();
                }

                PropagateBaseSecurityToAll();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void DeleteSynteticBond()
        {
            try
            {
                for (int i = 0; i < SyntheticBonds.Count; i++)
                {
                    SyntheticBond futures = SyntheticBonds[i];

                    futures.BaseIsbergParameters.BotTab.SecuritySubscribeEvent -= SecuritySubscribe;
                    futures.BaseIsbergParameters.BotTab.Delete();

                    futures.FuturesIsbergParameters.BotTab.SecuritySubscribeEvent -= SecuritySubscribe;
                    futures.FuturesIsbergParameters.BotTab.Delete();

                    for (int s = 0; s < futures.Scenarios.Count; s++)
                    {
                        futures.Scenarios[s].Delete();
                    }
                }

                SyntheticBonds.Clear();

                if (!File.Exists(@"Engine\" + SynteticBondName + @"SynteticBondModificationsFuturesToLoad.txt"))
                {
                    return;
                }

                File.Delete(@"Engine\" + SynteticBondName + @"SynteticBondModificationsFuturesToLoad.txt");
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void ChooseBaseSecurity()
        {
            try
            {
                BaseTab.SecuritySubscribeEvent += SecuritySubscribe;
                BaseTab.DialogClosed += OnBaseSecurityDialogClosed;
                BaseTab.ShowConnectorDialog();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void OnBaseSecurityDialogClosed()
        {
            BaseTab.DialogClosed -= OnBaseSecurityDialogClosed;
            PropagateBaseSecurityToAll();
        }

        public void PropagateBaseSecurityToAll()
        {
            if (SyntheticBonds == null || SyntheticBonds.Count <= 1)
            {
                return;
            }

            BotTabSimple sourceTab = SyntheticBonds[0].BaseIsbergParameters.BotTab;

            if (sourceTab == null || sourceTab.Connector == null)
            {
                return;
            }

            ConnectorCandles sourceConnector = sourceTab.Connector;

            for (int i = 1; i < SyntheticBonds.Count; i++)
            {
                BotTabSimple targetTab = SyntheticBonds[i].BaseIsbergParameters.BotTab;

                if (targetTab == null || targetTab.Connector == null)
                {
                    continue;
                }

                ConnectorCandles targetConnector = targetTab.Connector;
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
        }

        public void ChooseFuturesSecurity(SyntheticBond settings)
        {
            settings.FuturesIsbergParameters.BotTab.SecuritySubscribeEvent += SecuritySubscribe;
            settings.FuturesIsbergParameters.BotTab.ShowConnectorDialog();
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
                    SyntheticBond settings = SyntheticBonds[i];

                    if (CheckTradingReadyForTester(settings) == false)
                    {
                        continue;
                    }

                    UpdateSeparation(settings);

                    UpdateCointegration(settings);

                    UpdateDaysBeforeExpiration(settings);

                    UpdateProfitPerDay(settings);
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private bool CheckTradingReadyForTester(SyntheticBond settings)
        {
            if (settings.BaseIsbergParameters == null
                || settings.BaseIsbergParameters.BotTab == null
                || settings.BaseIsbergParameters.BotTab.CandlesAll == null
                || settings.BaseIsbergParameters.BotTab.CandlesAll.Count == 0)
            {
                return false;
            }

            if (settings.FuturesIsbergParameters == null
                || settings.FuturesIsbergParameters.BotTab == null
                || settings.FuturesIsbergParameters.BotTab.CandlesAll == null
                || settings.FuturesIsbergParameters.BotTab.CandlesAll.Count == 0)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region Main thread

        private void MainSynteticBondThread()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(1000);

                    for (int i = 0; SyntheticBonds != null && i < SyntheticBonds.Count; i++)
                    {
                        SyntheticBond syntheticBond = SyntheticBonds[i];

                        if (CheckTadingReady(syntheticBond) == false)
                        {
                            continue;
                        }

                        UpdateSeparation(syntheticBond);

                        UpdateCointegration(syntheticBond);

                        UpdateDaysBeforeExpiration(syntheticBond);

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

        private void UpdateProfitPerDay(SyntheticBond bond)
        {
            try
            {
                if (bond.DaysBeforeExpiration != -1 && bond.DaysBeforeExpiration != 0 && bond.PercentSeparationCandles != null && bond.PercentSeparationCandles.Count != 0)
                {
                    bond.ProfitPerDay = bond.PercentSeparationCandles[^1].Value / bond.DaysBeforeExpiration;
                }
                else if (bond.PercentSeparationCandles != null && bond.PercentSeparationCandles.Count != 0)
                {
                    bond.ProfitPerDay = 0;
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateDaysBeforeExpiration(SyntheticBond bond)
        {
            try
            {
                if (bond.FuturesIsbergParameters.BotTab.Security != null)
                {
                    if (bond.FuturesIsbergParameters.BotTab.Security.Expiration != DateTime.MinValue && bond.FuturesIsbergParameters.BotTab.Security.Expiration != DateTime.UnixEpoch)
                    {
                        bond.DaysBeforeExpiration = (bond.FuturesIsbergParameters.BotTab.Security.Expiration - bond.FuturesIsbergParameters.BotTab.TimeServerCurrent).Days;
                    }
                    else
                    {
                        bond.DaysBeforeExpiration = -1;
                    }
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSeparation(SyntheticBond bond)
        {
            try
            {
                List<Candle> candlesSec1 = bond.BaseIsbergParameters.BotTab.CandlesAll; ;
                List<Candle> candlesSec2 = bond.FuturesIsbergParameters.BotTab.CandlesAll;
                List<Candle> candlesRationingBase = null;
                List<Candle> candlesRationingFutures = null;

                if (bond.BaseUseRationing)
                {
                    if (bond.BaseRationingSecurity == null)
                    {
                        return;
                    }
                    else if (bond.BaseRationingSecurity.CandlesAll == null)
                    {
                        return;
                    }
                    else if (bond.BaseRationingSecurity.CandlesAll.Count == 0)
                    {
                        return;
                    }

                    candlesRationingBase = bond.BaseRationingSecurity.CandlesAll;
                }

                if (bond.FuturesUseRationing)
                {
                    if (bond.FuturesRationingSecurity == null)
                    {
                        return;
                    }
                    else if (bond.FuturesRationingSecurity.CandlesAll == null)
                    {
                        return;
                    }
                    else if (bond.FuturesRationingSecurity.CandlesAll.Count == 0)
                    {
                        return;
                    }

                    candlesRationingFutures = bond.FuturesRationingSecurity.CandlesAll;
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
                _separationCaches.TryGetValue(bond, out cache);

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
                        bond.BaseTimeOffset,
                        bond.BaseMultiplicator,
                        bond.BaseUseRationing,
                        candlesRationingBase,
                        bond.BaseTimeOffsetRationing,
                        bond.BaseRationingMode,
                        cache.ProcessedSec1);

                    if (sec1Updated)
                    {
                        bool sec2Updated = UpdateLastProcessedCandle(
                            candlesSec2[sec2Count - 1],
                            bond.FuturesTimeOffset,
                            bond.FuturesMultiplicator,
                            bond.FuturesUseRationing,
                            candlesRationingFutures,
                            bond.FuturesTimeOffsetRationing,
                            bond.FuturesRationingMode,
                            cache.ProcessedSec2);

                        if (sec2Updated)
                        {
                            UpdateSeparationAndNotify(cache.ProcessedSec1, cache.ProcessedSec2, bond);
                            return;
                        }
                    }
                }

                int tailSize = (int)bond.SeparationLength * 2;

                candlesSec1 = TakeTail(candlesSec1, tailSize);
                candlesSec2 = TakeTail(candlesSec2, tailSize);

                // 1 сдвигаем время

                candlesSec1 = GetOffsetCandles(candlesSec1, bond.BaseTimeOffset);
                candlesSec2 = GetOffsetCandles(candlesSec2, bond.FuturesTimeOffset);

                if (bond.BaseUseRationing)
                {
                    candlesRationingBase = TakeTail(candlesRationingBase, tailSize);
                    candlesRationingBase = GetOffsetCandles(candlesRationingBase, bond.BaseTimeOffsetRationing);
                }

                if (bond.FuturesUseRationing)
                {
                    candlesRationingFutures = TakeTail(candlesRationingFutures, tailSize);
                    candlesRationingFutures = GetOffsetCandles(candlesRationingFutures, bond.FuturesTimeOffsetRationing);
                }

                // 2 умножаем на коэффициенты

                candlesSec1 = GetCoeffCandles(candlesSec1, bond.BaseMultiplicator);
                candlesSec2 = GetCoeffCandles(candlesSec2, bond.FuturesMultiplicator);

                // 3 нормализуем

                if (bond.BaseUseRationing && bond.BaseRationingMode == RationingMode.Division)
                {
                    candlesSec1 = GetDivision(candlesSec1, candlesRationingBase);
                }
                else if (bond.BaseUseRationing && bond.BaseRationingMode == RationingMode.Multiplication)
                {
                    candlesSec1 = GetMult(candlesSec1, candlesRationingBase);
                }
                else if (bond.BaseUseRationing && bond.BaseRationingMode == RationingMode.Difference)
                {
                    candlesSec1 = GetDiff(candlesSec1, candlesRationingBase);
                }
                else if (bond.BaseUseRationing && bond.BaseRationingMode == RationingMode.Addition)
                {
                    candlesSec1 = GetAddition(candlesSec1, candlesRationingBase);
                }

                if (bond.FuturesUseRationing && bond.FuturesRationingMode == RationingMode.Division)
                {
                    candlesSec2 = GetDivision(candlesSec2, candlesRationingFutures);
                }
                else if (bond.FuturesUseRationing && bond.FuturesRationingMode == RationingMode.Multiplication)
                {
                    candlesSec2 = GetMult(candlesSec2, candlesRationingFutures);
                }
                else if (bond.FuturesUseRationing && bond.FuturesRationingMode == RationingMode.Difference)
                {
                    candlesSec2 = GetDiff(candlesSec2, candlesRationingFutures);
                }
                else if (bond.FuturesUseRationing && bond.FuturesRationingMode == RationingMode.Addition)
                {
                    candlesSec2 = GetAddition(candlesSec2, candlesRationingFutures);
                }

                if (cache == null)
                {
                    cache = new SeparationCache();
                    _separationCaches[bond] = cache;
                }

                cache.ProcessedSec1 = candlesSec1;
                cache.ProcessedSec2 = candlesSec2;
                cache.Sec1SourceCount = sec1Count;
                cache.Sec2SourceCount = sec2Count;
                cache.RationingBaseSourceCount = rationingBaseCount;
                cache.RationingFuturesSourceCount = rationingFuturesCount;

                // 4 Отнимаем одно от другого

                UpdateSeparationAndNotify(candlesSec1, candlesSec2, bond);
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSeparationAndNotify(List<Candle> candlesSec1, List<Candle> candlesSec2, SyntheticBond bond)
        {
            decimal oldPercentValue = -1;
            DateTime oldPercentTime = DateTime.MinValue;
            decimal oldAbsoluteValue = -1;
            DateTime oldAbsoluteTime = DateTime.MinValue;
            int oldPercentCount = 0;
            int oldAbsoluteCount = 0;

            if (bond.PercentSeparationCandles != null && bond.PercentSeparationCandles.Count > 0)
            {
                oldPercentCount = bond.PercentSeparationCandles.Count;
                PairIndicatorValue lastPercent = bond.PercentSeparationCandles[bond.PercentSeparationCandles.Count - 1];
                oldPercentValue = lastPercent.Value;
                oldPercentTime = lastPercent.Time;
            }

            if (bond.AbsoluteSeparationCandles != null && bond.AbsoluteSeparationCandles.Count > 0)
            {
                oldAbsoluteCount = bond.AbsoluteSeparationCandles.Count;
                PairIndicatorValue lastAbsolute = bond.AbsoluteSeparationCandles[bond.AbsoluteSeparationCandles.Count - 1];
                oldAbsoluteValue = lastAbsolute.Value;
                oldAbsoluteTime = lastAbsolute.Time;
            }

            UpdateSubtractTheCandles(candlesSec1, candlesSec2, ref bond);

            bool changed = false;

            if (bond.PercentSeparationCandles != null && bond.PercentSeparationCandles.Count > 0)
            {
                PairIndicatorValue newLastPercent = bond.PercentSeparationCandles[bond.PercentSeparationCandles.Count - 1];
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
                if (bond.AbsoluteSeparationCandles != null && bond.AbsoluteSeparationCandles.Count > 0)
                {
                    PairIndicatorValue newLastAbsolute = bond.AbsoluteSeparationCandles[bond.AbsoluteSeparationCandles.Count - 1];
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
                ContangoChangeEvent?.Invoke(bond);
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

        private void UpdateCointegration(SyntheticBond bond)
        {
            try
            {
                List<PairIndicatorValue> oldCointegration = bond.CointegrationBuilder.Cointegration;
                int oldCount = 0;
                decimal oldLastValue = 0;

                if (oldCointegration != null && oldCointegration.Count > 0)
                {
                    oldCount = oldCointegration.Count;
                    oldLastValue = oldCointegration[oldCount - 1].Value;
                }

                List<Candle> candlesSec1;
                List<Candle> candlesSec2;
                GetProcessedCandles(bond, out candlesSec1, out candlesSec2);

                if (candlesSec1 == null || candlesSec1.Count == 0)
                {
                    candlesSec1 = bond.BaseIsbergParameters.BotTab.CandlesAll;
                }
                if (candlesSec2 == null || candlesSec2.Count == 0)
                {
                    candlesSec2 = bond.FuturesIsbergParameters.BotTab.CandlesAll;
                }

                bool needBeautifulValues = StartProgram == StartProgram.IsOsTrader;
                bond.CointegrationBuilder.ReloadCointegration(candlesSec1, candlesSec2, needBeautifulValues);

                MinBalancesChangeEvent?.Invoke(bond);

                List<PairIndicatorValue> newCointegration = bond.CointegrationBuilder.Cointegration;
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
                    CointegrationChangeEvent?.Invoke(bond);
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public bool CheckTadingReady(SyntheticBond bond)
        {
            if (bond.BaseIsbergParameters == null)
            {
                return false;
            }
            else if (bond.BaseIsbergParameters.BotTab == null)
            {
                return false;
            }
            else if (bond.BaseIsbergParameters.BotTab.ServerStatus != ServerConnectStatus.Connect)
            {
                return false;
            }
            else if (bond.BaseIsbergParameters.BotTab.CandlesAll == null)
            {
                return false;
            }
            else if (bond.BaseIsbergParameters.BotTab.CandlesAll.Count == 0)
            {
                return false;
            }
            else if (bond.FuturesIsbergParameters == null)
            {
                return false;
            }
            else if (bond.FuturesIsbergParameters.BotTab == null)
            {
                return false;
            }
            else if (bond.FuturesIsbergParameters.BotTab.ServerStatus != ServerConnectStatus.Connect)
            {
                return false;
            }
            else if (bond.FuturesIsbergParameters.BotTab.CandlesAll == null)
            {
                return false;
            }
            else if (bond.FuturesIsbergParameters.BotTab.CandlesAll.Count == 0)
            {
                return false;
            }
            return true;
        }

        #endregion

        #region Offset logic

        public void ShowOffsetWindow(ref SyntheticBond modificationSyntheticBond)
        {
            try
            {
                string key = modificationSyntheticBond.FuturesIsbergParameters.BotTab.TabName;

                if (_bondsOffsetUi.TryGetValue(key, out SyntheticBondOffsetUi existingWindow))
                {
                    existingWindow.Activate();
                    existingWindow.WindowState = WindowState.Normal;
                    existingWindow.Focus();
                    return;
                }

                SyntheticBondOffsetUi bondOffsetUi = new SyntheticBondOffsetUi(this, ref modificationSyntheticBond);
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

        public void ShowChartWindow(ref SyntheticBond modificationSyntheticBond)
        {
            try
            {
                string key = modificationSyntheticBond.FuturesIsbergParameters.BotTab.TabName;

                if (_bondsChartUi.TryGetValue(key, out SynteticBondChartUi existingWindow))
                {
                    existingWindow.Activate();
                    existingWindow.WindowState = WindowState.Normal;
                    existingWindow.Focus();
                    return;
                }

                SynteticBondChartUi bondChartUi = new SynteticBondChartUi(this, ref modificationSyntheticBond);
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
            SynteticBondChartUi closedWindow = (SynteticBondChartUi)sender;
            closedWindow.Closed -= BondChartUi_Closed;
            _bondsChartUi.TryRemove(closedWindow.Key, out _);

            OnSettingsChanged();
        }

        #endregion

        #region Trade logic

        public void ShowTradeWindow(ref SyntheticBond modificationSyntheticBond)
        {
            try
            {
                string key = modificationSyntheticBond.FuturesIsbergParameters.BotTab.TabName;

                if (_bondsTradeUi.TryGetValue(key, out SynteticBondTradeUi existingWindow))
                {
                    existingWindow.Activate();
                    existingWindow.WindowState = WindowState.Normal;
                    existingWindow.Focus();
                    return;
                }

                SynteticBondTradeUi bondTradeUi = new SynteticBondTradeUi(this, ref modificationSyntheticBond);
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
            SynteticBondTradeUi closedWindow = (SynteticBondTradeUi)sender;
            closedWindow.Closed -= BondTradeUi_Closed;
            _bondsTradeUi.TryRemove(closedWindow.Key, out _);

            OnSettingsChanged();
        }

        public void CloseTradeWindow(SyntheticBond settings)
        {
            if (settings == null
                || settings.FuturesIsbergParameters == null
                || settings.FuturesIsbergParameters.BotTab == null)
            {
                return;
            }

            string key = settings.FuturesIsbergParameters.BotTab.TabName;

            if (_bondsTradeUi.TryRemove(key, out SynteticBondTradeUi window))
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

        private ConcurrentDictionary<string, SynteticBondTradeUi> _bondsTradeUi = new ConcurrentDictionary<string, SynteticBondTradeUi>();
        private ConcurrentDictionary<string, SynteticBondChartUi> _bondsChartUi = new ConcurrentDictionary<string, SynteticBondChartUi>();
        private ConcurrentDictionary<string, SyntheticBondOffsetUi> _bondsOffsetUi = new ConcurrentDictionary<string, SyntheticBondOffsetUi>();

        private ConcurrentDictionary<SyntheticBond, SeparationCache> _separationCaches = new ConcurrentDictionary<SyntheticBond, SeparationCache>();

        #endregion

        #region Public properties

        public string SynteticBondName;

        public BotTabSimple BaseTab
        {
            get
            {
                if (SyntheticBonds != null && SyntheticBonds.Count > 0
                    && SyntheticBonds[0].BaseIsbergParameters != null
                    && SyntheticBonds[0].BaseIsbergParameters.BotTab != null)
                {
                    return SyntheticBonds[0].BaseIsbergParameters.BotTab;
                }
                return _legacyBaseTab;
            }
            set
            {
                _legacyBaseTab = value;
            }
        }

        private BotTabSimple _legacyBaseTab;

        public List<SyntheticBond> SyntheticBonds;

        public int SyntheticBondNum;

        public decimal TotalVolumeSec1;

        public decimal TotalVolumeSec2;

        public decimal OneOrderVolumeSec1;

        public decimal OneOrderVolumeSec2;

        public TimeSpan LifetimeOrder;

        public SynteticBondOrderPosition OrderPositionSec1;

        public SynteticBondOrderPosition OrderPositionSec2;

        public bool EventsIsOn
        {
            get
            {
                return BaseTab.EventsIsOn;
            }
            set
            {
                for (int i = 0; i < SyntheticBonds.Count; i++)
                {
                    SyntheticBond futures = SyntheticBonds[i];

                    if (futures.BaseIsbergParameters.BotTab.EventsIsOn != value)
                    {
                        futures.BaseIsbergParameters.BotTab.EventsIsOn = value;
                    }

                    if (futures.FuturesIsbergParameters.BotTab.EventsIsOn != value)
                    {
                        futures.FuturesIsbergParameters.BotTab.EventsIsOn = value;
                    }

                    if (futures.BaseRationingSecurity != null && futures.BaseRationingSecurity.EventsIsOn != value)
                    {
                        futures.BaseRationingSecurity.EventsIsOn = value;
                    }

                    if (futures.FuturesRationingSecurity != null && futures.FuturesRationingSecurity.EventsIsOn != value)
                    {
                        futures.FuturesRationingSecurity.EventsIsOn = value;
                    }
                }
            }
        }

        public bool EmulatorIsOn
        {
            get
            {
                return BaseTab.EmulatorIsOn;
            }
            set
            {
                for (int i = 0; i < SyntheticBonds.Count; i++)
                {
                    SyntheticBond futures = SyntheticBonds[i];

                    if (futures.BaseIsbergParameters.BotTab.EmulatorIsOn != value)
                    {
                        futures.BaseIsbergParameters.BotTab.EmulatorIsOn = value;
                    }

                    if (futures.BaseRationingSecurity != null && futures.BaseRationingSecurity.EmulatorIsOn != value)
                    {
                        futures.BaseRationingSecurity.EmulatorIsOn = value;
                    }

                    if (futures.FuturesIsbergParameters.BotTab.EmulatorIsOn != value)
                    {
                        futures.FuturesIsbergParameters.BotTab.EmulatorIsOn = value;
                    }

                    if (futures.FuturesIsbergParameters != null && futures.FuturesRationingSecurity.EmulatorIsOn != value)
                    {
                        futures.FuturesRationingSecurity.EmulatorIsOn = value;
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
