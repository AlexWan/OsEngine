/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using OsEngine.Logging;
using OsEngine.Entity;
using OsEngine.Language;
using System.Threading;
using OsEngine.Market;
using OsEngine.Market.Servers;

namespace OsEngine.OsData
{
    public class OsDataMaster
    {
        #region Service

        public OsDataMaster()
        {
            _awaitUiMasterAloneTest = new AwaitObject(OsLocalization.Data.Label46, 100, 0, true);
            AwaitUi ui = new AwaitUi(_awaitUiMasterAloneTest);

            Task.Run(Load);
            ui.ShowDialog();
            Thread.Sleep(500);
        }

        private void Load()
        {
            if (!Directory.Exists("Data"))
            {
                Directory.CreateDirectory("Data");
            }

            // folder name is our name of the set/название папок это у нас название сетов

            string[] folders = Directory.GetDirectories("Data");

            if (folders != null
                && folders.Length > 0)
            {
                string[] nameFolders = new string[folders.Length];

                for (int i = 0; i < folders.Length; i++)
                {
                    nameFolders[i] = folders[i].Split('\\')[1];
                }

                lock (_setsLocker)
                {
                    Sets.Clear();

                    for (int i = 0; i < nameFolders.Length; i++)
                    {
                        if (nameFolders[i].Split('_')[0] == "Set")
                        {
                            Sets.Add(new OsDataSet(nameFolders[i]));
                            Sets[Sets.Count - 1].NewLogMessageEvent += SendNewLogMessage;

                        }
                    }
                }
            }

            _awaitUiMasterAloneTest.Dispose();

            if (NeedUpDateTableEvent != null)
            {
                NeedUpDateTableEvent();
            }
        }

        public List<OsDataSet> Sets = new List<OsDataSet>();

        private readonly object _setsLocker = new object();

        private AwaitObject _awaitUiMasterAloneTest;

        public event Action NeedUpDateTableEvent;

        #endregion

        #region Set switching

        public OsDataSet SelectedSet;

        public void SortSets()
        {
            if (Sets == null ||
                Sets.Count == 0)
            {
                return;
            }

            List<OsDataSet> sortSets = new List<OsDataSet>();

            for (int i = 0; i < Sets.Count; i++)
            {
                if (Sets[i].BaseSettings.Regime == DataSetState.On)
                {
                    sortSets.Add(Sets[i]);
                }
            }

            for (int i = 0; i < Sets.Count; i++)
            {
                if (Sets[i].BaseSettings.Regime == DataSetState.Off)
                {
                    sortSets.Add(Sets[i]);
                }
            }
            Sets = sortSets;
        }

        /// <summary>
        /// Create a new data set with the specified settings.
        /// </summary>
        public OsDataSet CreateSet(string name, ServerType source, string sourceName,
            List<TimeFrame> timeframes, DateTime timeStart, DateTime timeEnd)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Set name is required");
            }

            string setName = name.StartsWith("Set_", StringComparison.InvariantCultureIgnoreCase)
                ? name
                : "Set_" + name;

            OsDataSet set;

            lock (_setsLocker)
            {
                if (Sets != null)
                {
                    for (int i = 0; i < Sets.Count; i++)
                    {
                        if (Sets[i] != null &&
                            Sets[i].SetName == setName)
                        {
                            throw new InvalidOperationException($"Set '{setName}' already exists");
                        }
                    }
                }

                List<ServerType> allowedSources = ServerMaster.ServersTypesToOsData;

                bool sourceAllowed = false;

                for (int i = 0; i < allowedSources.Count; i++)
                {
                    if (allowedSources[i] == source)
                    {
                        sourceAllowed = true;
                        break;
                    }
                }

                if (!sourceAllowed)
                {
                    throw new ArgumentException($"Server type '{source}' is not supported for OsData");
                }

                if (string.IsNullOrWhiteSpace(sourceName))
                {
                    throw new ArgumentException("Source name is required");
                }

                bool sourceFound = false;
                List<IServer> servers = ServerMaster.GetServers();

                if (servers != null)
                {
                    for (int i = 0; i < servers.Count; i++)
                    {
                        if (servers[i] != null &&
                            servers[i].ServerType == source &&
                            servers[i].ServerNameAndPrefix.StartsWith(sourceName))
                        {
                            sourceFound = true;
                            break;
                        }
                    }
                }

                if (!sourceFound)
                {
                    throw new ArgumentException($"Active server of type '{source}' with name prefix '{sourceName}' not found");
                }

                if (timeframes == null ||
                    timeframes.Count == 0)
                {
                    throw new ArgumentException("At least one timeframe is required");
                }

                IServerPermission permission = ServerMaster.GetServerPermission(source);

                if (permission == null)
                {
                    throw new InvalidOperationException($"Cannot get permissions for server type '{source}'");
                }

                for (int i = 0; i < timeframes.Count; i++)
                {
                    if (!IsTimeFrameSupportedByServer(timeframes[i], permission))
                    {
                        throw new ArgumentException($"Timeframe '{timeframes[i]}' is not supported by server '{source}'");
                    }
                }

                if (timeStart > timeEnd)
                {
                    throw new ArgumentException("Date from must be less than or equal to date to");
                }

                set = new OsDataSet(setName);
                set.BaseSettings.Regime = DataSetState.Off;
                set.BaseSettings.Source = source;
                set.BaseSettings.SourceName = sourceName;
                set.BaseSettings.TimeStart = timeStart;
                set.BaseSettings.TimeEnd = timeEnd;

                // A folder from a previous run may still exist. Reset every timeframe
                // flag and clear stale securities so the set is created exactly with
                // the requested configuration.
                ResetAllTimeFrameFlags(set.BaseSettings);

                if (set.SecuritiesLoad != null)
                {
                    set.SecuritiesLoad.Clear();
                }
                else
                {
                    set.SecuritiesLoad = new List<SecurityToLoad>();
                }

                for (int i = 0; i < timeframes.Count; i++)
                {
                    SetTimeFrameFlag(set.BaseSettings, timeframes[i], true);
                }

                set.Save();

                if (Sets == null)
                {
                    Sets = new List<OsDataSet>();
                }

                set.NewLogMessageEvent += SendNewLogMessage;
                Sets.Add(set);
            }

            NeedUpDateTableEvent?.Invoke();

            return set;
        }

        /// <summary>
        /// Delete a data set by name.
        /// </summary>
        public bool DeleteSet(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Set name is required");
            }

            string setName = name.StartsWith("Set_", StringComparison.InvariantCultureIgnoreCase)
                ? name
                : "Set_" + name;

            lock (_setsLocker)
            {
                if (Sets == null)
                {
                    return false;
                }

                OsDataSet setToDelete = null;

                for (int i = 0; i < Sets.Count; i++)
                {
                    if (Sets[i] != null &&
                        Sets[i].SetName == setName)
                    {
                        setToDelete = Sets[i];
                        break;
                    }
                }

                if (setToDelete == null)
                {
                    return false;
                }

                setToDelete.NewLogMessageEvent -= SendNewLogMessage;
                setToDelete.Delete();
                Sets.Remove(setToDelete);
            }

            NeedUpDateTableEvent?.Invoke();

            return true;
        }

        /// <summary>
        /// Get a data set by name. Returns null if not found.
        /// </summary>
        public OsDataSet GetSet(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Set name is required");
            }

            string setName = name.StartsWith("Set_", StringComparison.InvariantCultureIgnoreCase)
                ? name
                : "Set_" + name;

            lock (_setsLocker)
            {
                if (Sets == null)
                {
                    return null;
                }

                for (int i = 0; i < Sets.Count; i++)
                {
                    if (Sets[i] != null &&
                        Sets[i].SetName == setName)
                    {
                        return Sets[i];
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Update set settings inside a lock, propagate changes to securities and save.
        /// </summary>
        public bool UpdateSetSettings(string name, Action<SettingsToLoadSecurity> updateAction)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Set name is required");
            }

            if (updateAction == null)
            {
                throw new ArgumentException("Update action is required");
            }

            string setName = name.StartsWith("Set_", StringComparison.InvariantCultureIgnoreCase)
                ? name
                : "Set_" + name;

            lock (_setsLocker)
            {
                if (Sets == null)
                {
                    return false;
                }

                OsDataSet set = null;

                for (int i = 0; i < Sets.Count; i++)
                {
                    if (Sets[i] != null &&
                        Sets[i].SetName == setName)
                    {
                        set = Sets[i];
                        break;
                    }
                }

                if (set == null)
                {
                    return false;
                }

                updateAction(set.BaseSettings);

                if (set.SecuritiesLoad != null)
                {
                    for (int i = 0; i < set.SecuritiesLoad.Count; i++)
                    {
                        if (set.SecuritiesLoad[i] != null)
                        {
                            set.SecuritiesLoad[i].CopySettingsFromParam(set.BaseSettings);
                        }
                    }
                }

                set.Save();
            }

            NeedUpDateTableEvent?.Invoke();

            return true;
        }

        public static void ResetAllTimeFrameFlags(SettingsToLoadSecurity settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.Tf1SecondIsOn = false;
            settings.Tf2SecondIsOn = false;
            settings.Tf5SecondIsOn = false;
            settings.Tf10SecondIsOn = false;
            settings.Tf15SecondIsOn = false;
            settings.Tf20SecondIsOn = false;
            settings.Tf30SecondIsOn = false;
            settings.Tf1MinuteIsOn = false;
            settings.Tf2MinuteIsOn = false;
            settings.Tf5MinuteIsOn = false;
            settings.Tf10MinuteIsOn = false;
            settings.Tf15MinuteIsOn = false;
            settings.Tf30MinuteIsOn = false;
            settings.Tf1HourIsOn = false;
            settings.Tf2HourIsOn = false;
            settings.Tf4HourIsOn = false;
            settings.TfDayIsOn = false;
            settings.TfTickIsOn = false;
            settings.TfMarketDepthIsOn = false;
        }

        public static bool IsTimeFrameSupportedByServer(TimeFrame timeFrame, IServerPermission permission)
        {
            switch (timeFrame)
            {
                case TimeFrame.Sec1: return permission.DataFeedTf1SecondCanLoad;
                case TimeFrame.Sec2: return permission.DataFeedTf2SecondCanLoad;
                case TimeFrame.Sec5: return permission.DataFeedTf5SecondCanLoad;
                case TimeFrame.Sec10: return permission.DataFeedTf10SecondCanLoad;
                case TimeFrame.Sec15: return permission.DataFeedTf15SecondCanLoad;
                case TimeFrame.Sec20: return permission.DataFeedTf20SecondCanLoad;
                case TimeFrame.Sec30: return permission.DataFeedTf30SecondCanLoad;
                case TimeFrame.Min1: return permission.DataFeedTf1MinuteCanLoad;
                case TimeFrame.Min2: return permission.DataFeedTf2MinuteCanLoad;
                case TimeFrame.Min5: return permission.DataFeedTf5MinuteCanLoad;
                case TimeFrame.Min10: return permission.DataFeedTf10MinuteCanLoad;
                case TimeFrame.Min15: return permission.DataFeedTf15MinuteCanLoad;
                case TimeFrame.Min30: return permission.DataFeedTf30MinuteCanLoad;
                case TimeFrame.Hour1: return permission.DataFeedTf1HourCanLoad;
                case TimeFrame.Hour2: return permission.DataFeedTf2HourCanLoad;
                case TimeFrame.Hour4: return permission.DataFeedTf4HourCanLoad;
                case TimeFrame.Day: return permission.DataFeedTfDayCanLoad;
                case TimeFrame.Tick: return permission.DataFeedTfTickCanLoad;
                case TimeFrame.MarketDepth: return permission.DataFeedTfMarketDepthCanLoad;
                default: return false;
            }
        }

        public static List<TimeFrame> GetActiveTimeFrames(SettingsToLoadSecurity settings)
        {
            List<TimeFrame> result = new List<TimeFrame>();

            if (settings.Tf1SecondIsOn) result.Add(TimeFrame.Sec1);
            if (settings.Tf2SecondIsOn) result.Add(TimeFrame.Sec2);
            if (settings.Tf5SecondIsOn) result.Add(TimeFrame.Sec5);
            if (settings.Tf10SecondIsOn) result.Add(TimeFrame.Sec10);
            if (settings.Tf15SecondIsOn) result.Add(TimeFrame.Sec15);
            if (settings.Tf20SecondIsOn) result.Add(TimeFrame.Sec20);
            if (settings.Tf30SecondIsOn) result.Add(TimeFrame.Sec30);
            if (settings.Tf1MinuteIsOn) result.Add(TimeFrame.Min1);
            if (settings.Tf2MinuteIsOn) result.Add(TimeFrame.Min2);
            if (settings.Tf5MinuteIsOn) result.Add(TimeFrame.Min5);
            if (settings.Tf10MinuteIsOn) result.Add(TimeFrame.Min10);
            if (settings.Tf15MinuteIsOn) result.Add(TimeFrame.Min15);
            if (settings.Tf30MinuteIsOn) result.Add(TimeFrame.Min30);
            if (settings.Tf1HourIsOn) result.Add(TimeFrame.Hour1);
            if (settings.Tf2HourIsOn) result.Add(TimeFrame.Hour2);
            if (settings.Tf4HourIsOn) result.Add(TimeFrame.Hour4);
            if (settings.TfDayIsOn) result.Add(TimeFrame.Day);
            if (settings.TfTickIsOn) result.Add(TimeFrame.Tick);
            if (settings.TfMarketDepthIsOn) result.Add(TimeFrame.MarketDepth);

            return result;
        }

        public static void SetTimeFrameFlag(SettingsToLoadSecurity settings, TimeFrame timeFrame, bool value)
        {
            switch (timeFrame)
            {
                case TimeFrame.Sec1: settings.Tf1SecondIsOn = value; break;
                case TimeFrame.Sec2: settings.Tf2SecondIsOn = value; break;
                case TimeFrame.Sec5: settings.Tf5SecondIsOn = value; break;
                case TimeFrame.Sec10: settings.Tf10SecondIsOn = value; break;
                case TimeFrame.Sec15: settings.Tf15SecondIsOn = value; break;
                case TimeFrame.Sec20: settings.Tf20SecondIsOn = value; break;
                case TimeFrame.Sec30: settings.Tf30SecondIsOn = value; break;
                case TimeFrame.Min1: settings.Tf1MinuteIsOn = value; break;
                case TimeFrame.Min2: settings.Tf2MinuteIsOn = value; break;
                case TimeFrame.Min5: settings.Tf5MinuteIsOn = value; break;
                case TimeFrame.Min10: settings.Tf10MinuteIsOn = value; break;
                case TimeFrame.Min15: settings.Tf15MinuteIsOn = value; break;
                case TimeFrame.Min30: settings.Tf30MinuteIsOn = value; break;
                case TimeFrame.Hour1: settings.Tf1HourIsOn = value; break;
                case TimeFrame.Hour2: settings.Tf2HourIsOn = value; break;
                case TimeFrame.Hour4: settings.Tf4HourIsOn = value; break;
                case TimeFrame.Day: settings.TfDayIsOn = value; break;
                case TimeFrame.Tick: settings.TfTickIsOn = value; break;
                case TimeFrame.MarketDepth: settings.TfMarketDepthIsOn = value; break;
            }
        }

        #endregion

        #region Logging

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (NewLogMessageEvent != null)
            {
                NewLogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion

        #region Load completed events

        /// <summary>
        /// Interval between load-state polls in milliseconds. Default is 5 seconds.
        /// </summary>
        public int LoadEventsPollIntervalMs = 5000;

        /// <summary>
        /// Raised once when a data set finishes loading (including cases where
        /// some data pies could not be loaded after retries).
        /// </summary>
        public event Action<string, decimal> SetLoadCompletedEvent
        {
            add
            {
                lock (_loadEventsLocker)
                {
                    _setLoadCompleted += value;
                    StartLoadEventsWatcher();
                }
            }
            remove
            {
                lock (_loadEventsLocker)
                {
                    _setLoadCompleted -= value;
                    StopLoadEventsWatcherIfNoSubscribers();
                }
            }
        }

        /// <summary>
        /// Raised once when a specific security/timeframe in a data set finishes loading
        /// (including cases where some data pies could not be loaded after retries).
        /// </summary>
        public event Action<string, string, TimeFrame, decimal> SecurityLoadCompletedEvent
        {
            add
            {
                lock (_loadEventsLocker)
                {
                    _securityLoadCompleted += value;
                    StartLoadEventsWatcher();
                }
            }
            remove
            {
                lock (_loadEventsLocker)
                {
                    _securityLoadCompleted -= value;
                    StopLoadEventsWatcherIfNoSubscribers();
                }
            }
        }

        private Action<string, decimal> _setLoadCompleted;

        private Action<string, string, TimeFrame, decimal> _securityLoadCompleted;

        private Thread _loadEventsWatcherThread;

        private readonly object _loadEventsLocker = new object();

        private readonly Dictionary<string, bool> _setLoadCompletedStates = new Dictionary<string, bool>();

        private readonly Dictionary<string, bool> _securityLoadCompletedStates = new Dictionary<string, bool>();

        private void StartLoadEventsWatcher()
        {
            if (_loadEventsWatcherThread != null)
            {
                return;
            }

            _loadEventsWatcherThread = new Thread(LoadEventsWatcherWorker);
            _loadEventsWatcherThread.IsBackground = true;
            _loadEventsWatcherThread.Start();
        }

        private void StopLoadEventsWatcherIfNoSubscribers()
        {
            if (_setLoadCompleted == null && _securityLoadCompleted == null)
            {
                _loadEventsWatcherThread = null;
            }
        }

        private void LoadEventsWatcherWorker()
        {
            while (true)
            {
                Thread.Sleep(LoadEventsPollIntervalMs);

                lock (_loadEventsLocker)
                {
                    if (_loadEventsWatcherThread == null)
                    {
                        return;
                    }
                }

                try
                {
                    CheckLoadCompletedStates();
                }
                catch (Exception ex)
                {
                    SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        private void CheckLoadCompletedStates()
        {
            List<OsDataSet> setsSnapshot;

            lock (_setsLocker)
            {
                if (Sets == null)
                {
                    return;
                }

                setsSnapshot = new List<OsDataSet>(Sets);
            }

            HashSet<string> currentSetNames = new HashSet<string>();
            HashSet<string> currentSecurityKeys = new HashSet<string>();

            for (int i = 0; i < setsSnapshot.Count; i++)
            {
                OsDataSet set = setsSnapshot[i];

                if (set == null ||
                    string.IsNullOrEmpty(set.SetName))
                {
                    continue;
                }

                currentSetNames.Add(set.SetName);

                bool wasSetFinished;
                bool isSetFinished = false;
                decimal setPercent = 0;

                lock (_loadEventsLocker)
                {
                    _setLoadCompletedStates.TryGetValue(set.SetName, out wasSetFinished);
                }

                if (set.BaseSettings.Regime == DataSetState.On)
                {
                    isSetFinished = set.IsLoadFinished();
                    setPercent = set.PercentLoad();
                }

                if (isSetFinished && !wasSetFinished)
                {
                    _setLoadCompleted?.Invoke(set.SetName, setPercent);
                }

                lock (_loadEventsLocker)
                {
                    _setLoadCompletedStates[set.SetName] = isSetFinished;
                }

                if (set.SecuritiesLoad == null)
                {
                    continue;
                }

                for (int j = 0; j < set.SecuritiesLoad.Count; j++)
                {
                    SecurityToLoad security = set.SecuritiesLoad[j];

                    if (security == null ||
                        security.SecLoaders == null)
                    {
                        continue;
                    }

                    for (int k = 0; k < security.SecLoaders.Count; k++)
                    {
                        SecurityTfLoader loader = security.SecLoaders[k];

                        if (loader == null)
                        {
                            continue;
                        }

                        string key = GetSecurityLoadCompletedKey(set.SetName, security.SecName, loader.TimeFrame);
                        currentSecurityKeys.Add(key);

                        bool wasFinished;
                        bool isFinished = false;
                        decimal percent = 0;

                        lock (_loadEventsLocker)
                        {
                            _securityLoadCompletedStates.TryGetValue(key, out wasFinished);
                        }

                        if (set.BaseSettings.Regime == DataSetState.On)
                        {
                            isFinished = loader.Status == SecurityLoadStatus.Load;
                            percent = loader.PercentLoad();
                        }

                        if (isFinished && !wasFinished)
                        {
                            _securityLoadCompleted?.Invoke(set.SetName, security.SecName, loader.TimeFrame, percent);
                        }

                        lock (_loadEventsLocker)
                        {
                            _securityLoadCompletedStates[key] = isFinished;
                        }
                    }
                }
            }

            CleanupLoadCompletedStates(currentSetNames, currentSecurityKeys);
        }

        private void CleanupLoadCompletedStates(HashSet<string> currentSetNames, HashSet<string> currentSecurityKeys)
        {
            lock (_loadEventsLocker)
            {
                List<string> setsToRemove = new List<string>();

                foreach (KeyValuePair<string, bool> pair in _setLoadCompletedStates)
                {
                    if (!currentSetNames.Contains(pair.Key))
                    {
                        setsToRemove.Add(pair.Key);
                    }
                }

                for (int i = 0; i < setsToRemove.Count; i++)
                {
                    _setLoadCompletedStates.Remove(setsToRemove[i]);
                }

                List<string> securitiesToRemove = new List<string>();

                foreach (KeyValuePair<string, bool> pair in _securityLoadCompletedStates)
                {
                    if (!currentSecurityKeys.Contains(pair.Key))
                    {
                        securitiesToRemove.Add(pair.Key);
                    }
                }

                for (int i = 0; i < securitiesToRemove.Count; i++)
                {
                    _securityLoadCompletedStates.Remove(securitiesToRemove[i]);
                }
            }
        }

        private string GetSecurityLoadCompletedKey(string setName, string securityName, TimeFrame timeFrame)
        {
            return $"{setName}|{securityName}|{timeFrame}";
        }

        #endregion
    }
}