/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.GateIo.GateIoFutures.Entities;
using OsEngine.OsData.BinaryEntity;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.OsData
{
    /// <summary>
    /// data storage set
    /// </summary>
    public class OsDataSet
    {
        #region Service

        public string SetName;

        public SettingsToLoadSecurity BaseSettings = new SettingsToLoadSecurity();

        public OsDataSet(string nameSet)
        {
            SetName = nameSet;
            Load();

            Task task = new Task(WorkerArea);
            task.Start();
        }

        public void Save()
        {
            try
            {
                if (SetName == "Set_")
                {
                    return;
                }

                if (!Directory.Exists("Data\\" + SetName))
                {
                    Directory.CreateDirectory("Data\\" + SetName);
                }
                using (StreamWriter writer = new StreamWriter("Data\\" + SetName + @"\\Settings.txt", false))
                {
                    writer.WriteLine(BaseSettings.GetSaveStr());

                    for (int i = 0; SecuritiesLoad != null && i < SecuritiesLoad.Count; i++)
                    {
                        string security = SecuritiesLoad[i].GetSaveStr();
                        writer.WriteLine(security);
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void Load()
        {
            if (!File.Exists("Data\\" + SetName + @"\\Settings.txt"))
            {
                return;
            }

            try
            {

                if (SecuritiesLoad == null)
                {
                    SecuritiesLoad = new List<SecurityToLoad>();
                }

                using (StreamReader reader = new StreamReader("Data\\" + SetName + @"\\Settings.txt"))
                {
                    BaseSettings.Load(reader.ReadLine());

                    while (reader.EndOfStream == false)
                    {
                        SecuritiesLoad.Add(new SecurityToLoad());
                        SecuritiesLoad[SecuritiesLoad.Count - 1].Load(reader.ReadLine());
                        SecuritiesLoad[SecuritiesLoad.Count - 1].NewLogMessageEvent += SendNewLogMessage;
                    }

                    reader.Close();
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public void Delete()
        {
            _isDeleted = true;

            if (File.Exists("Data\\" + SetName + @"\\Settings.txt"))
            {
                File.Delete("Data\\" + SetName + @"\\Settings.txt");
            }

            if (Directory.Exists("Data\\" + SetName))
            {
                try
                {
                    DirectoryInfo info = new DirectoryInfo("Data\\" + SetName);
                    info.Delete(true);
                }
                catch (Exception)
                {
                    // ignore
                }
            }

            for (int i = 0; SecuritiesLoad != null && i < SecuritiesLoad.Count; i++)
            {
                SecuritiesLoad[i].Delete();
                SecuritiesLoad[i].NewLogMessageEvent -= NewLogMessageEvent;
            }

            if (SecuritiesLoad != null)
            {
                SecuritiesLoad.Clear();
            }
        }

        private bool _isDeleted = false;

        #endregion

        #region Control

        public void AddNewSecurity()
        {
            List<IServer> servers = ServerMaster.GetServers();

            if (servers == null)
            {
                return;
            }

            IServer myServer = null;

            for (int i = 0; i < servers.Count; i++)
            {
                IServer server = servers[i];

                if (server.ServerType == BaseSettings.Source)
                {
                    if (string.IsNullOrEmpty(BaseSettings.SourceName) == false
                        && server.ServerNameAndPrefix.StartsWith(BaseSettings.SourceName))
                    {
                        myServer = server;
                        break;
                    }
                    else if (string.IsNullOrEmpty(BaseSettings.SourceName) == true)
                    {
                        myServer = server;
                        break;
                    }
                }
            }

            if (myServer == null)
            {
                if (NewLogMessageEvent != null)
                {
                    NewLogMessageEvent(OsLocalization.Data.Label12, LogMessageType.System);
                }

                return;
            }

            List<Security> securities = myServer.Securities;

            if (securities == null
                || securities.Count == 0)
            {
                if (NewLogMessageEvent != null)
                {
                    NewLogMessageEvent(OsLocalization.Data.Label13, LogMessageType.System);
                }
                return;
            }

            NewSecurityUi ui = new NewSecurityUi(securities);
            ui.ShowDialog();

            if (ui.SelectedSecurity != null && ui.SelectedSecurity.Count != 0)
            {
                if (SecuritiesLoad == null)
                {
                    SecuritiesLoad = new List<SecurityToLoad>();
                }

                for (int i = 0; i < ui.SelectedSecurity.Count; i++)
                {
                    SecurityToLoad record = new SecurityToLoad();
                    record.SecName = ui.SelectedSecurity[i].Name;
                    record.SecId = ui.SelectedSecurity[i].NameId;
                    record.SecClass = ui.SelectedSecurity[i].NameClass;
                    record.SecExchange = ui.SelectedSecurity[i].Exchange;
                    record.SecNameFull = ui.SelectedSecurity[i].NameFull;
                    record.SetName = SetName;
                    record.PriceStep = ui.SelectedSecurity[i].PriceStep;
                    record.VolumeStep = ui.SelectedSecurity[i].VolumeStep;
                    record.NewLogMessageEvent += SendNewLogMessage;

                    if (record.SecName == null)
                    {
                        record.SecName = "";
                    }

                    if (record.SecId == null)
                    {
                        record.SecId = "";
                    }

                    if (record.SecClass == null)
                    {
                        record.SecClass = "";
                    }

                    if (record.SecExchange == null)
                    {
                        record.SecExchange = "";
                    }

                    record.CopySettingsFromParam(BaseSettings);

                    if (SecuritiesLoad.Find(s => s.SecId == record.SecId) == null)
                    {
                        SecuritiesLoad.Add(record);
                    }
                }

                ui.SelectedSecurity = null;
            }
            Save();
        }

        public void DeleteSecurity(int index)
        {
            if (SecuritiesLoad == null ||
                SecuritiesLoad.Count <= index)
            {
                return;
            }

            SecuritiesLoad[index].Delete();
            SecuritiesLoad[index].NewLogMessageEvent -= NewLogMessageEvent;
            SecuritiesLoad.RemoveAt(index);

            Save();
        }

        public void ChangeCollapsedStateBySecurity(int index)
        {
            if (SecuritiesLoad == null ||
                SecuritiesLoad.Count <= index)
            {
                return;
            }

            SecuritiesLoad[index].IsCollapsed = !SecuritiesLoad[index].IsCollapsed;

            Save();
        }

        public decimal PercentLoad()
        {
            decimal max = 0;
            decimal loaded = 0;

            for (int i = 0; SecuritiesLoad != null && i < SecuritiesLoad.Count; i++)
            {
                SecurityToLoad sec = SecuritiesLoad[i];

                max += sec.PiecesToLoad();
                loaded += sec.LoadedPieces();
            }

            if (max == 0)
            {
                return 0;
            }

            if (loaded == 0)
            {
                return 0;
            }

            decimal onePerc = max / 100;

            decimal result = loaded / onePerc;

            return Math.Round(result, 2);
        }

        #endregion

        #region Data loading

        public List<SecurityToLoad> SecuritiesLoad;

        private IServer _myServer;

        private async void WorkerArea()
        {
            try
            {
                await Task.Delay(5000);

                while (true)
                {
                    await Task.Delay(5000);

                    if (_isDeleted)
                    {
                        return;
                    }

                    Process();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void Process()
        {
            if (SecuritiesLoad == null ||
            SecuritiesLoad.Count == 0)
            {
                return;
            }

            if (_myServer == null && BaseSettings.Regime == DataSetState.On)
            {
                TryFindServer();
                return;
            }

            if (_myServer == null) return;

            if (_myServer.ServerStatus != ServerConnectStatus.Connect)
            {
                return;
            }

            if (_myServer.LastStartServerTime.AddSeconds(30) > DateTime.Now)
            {
                return;
            }

            for (int i = 0; i < SecuritiesLoad.Count; i++)
            {
                if (_isDeleted)
                {
                    return;
                }

                SecuritiesLoad[i].Process(_myServer);
            }
        }

        private void TryFindServer()
        {
            List<IServer> servers = ServerMaster.GetServers();

            if (servers == null
                || servers.Count == 0)
            {
                return;
            }

            for (int i = 0; i < servers.Count; i++)
            {
                IServer server = servers[i];

                if (servers[i].ServerType == BaseSettings.Source)
                {
                    if (string.IsNullOrEmpty(BaseSettings.SourceName) == false
                        && server.ServerNameAndPrefix.StartsWith(BaseSettings.SourceName))
                    {
                        _myServer = server;
                        break;
                    }
                    else if (string.IsNullOrEmpty(BaseSettings.SourceName) == true)
                    {
                        _myServer = server;
                        break;
                    }
                }
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
            else
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion
    }

    public class SecurityToLoad
    {
        #region Service

        public SecurityToLoad() { }

        public string SetName = "";

        public string SecName = "";

        public string SecId = "";

        public string SecClass = "";

        public string SecExchange = "";

        public string SecNameFull = "";

        public decimal PriceStep = 0;

        public decimal VolumeStep = 0;

        public bool IsCollapsed = false;

        public void CopySettingsFromParam(SettingsToLoadSecurity param)
        {
            SettingsToLoadSecurities.Regime = param.Regime;

            SettingsToLoadSecurities.Tf1SecondIsOn = param.Tf1SecondIsOn;
            SettingsToLoadSecurities.Tf2SecondIsOn = param.Tf2SecondIsOn;
            SettingsToLoadSecurities.Tf5SecondIsOn = param.Tf5SecondIsOn;
            SettingsToLoadSecurities.Tf10SecondIsOn = param.Tf10SecondIsOn;
            SettingsToLoadSecurities.Tf15SecondIsOn = param.Tf15SecondIsOn;
            SettingsToLoadSecurities.Tf20SecondIsOn = param.Tf20SecondIsOn;
            SettingsToLoadSecurities.Tf30SecondIsOn = param.Tf30SecondIsOn;

            SettingsToLoadSecurities.Tf1MinuteIsOn = param.Tf1MinuteIsOn;
            SettingsToLoadSecurities.Tf2MinuteIsOn = param.Tf2MinuteIsOn;
            SettingsToLoadSecurities.Tf5MinuteIsOn = param.Tf5MinuteIsOn;
            SettingsToLoadSecurities.Tf10MinuteIsOn = param.Tf10MinuteIsOn;
            SettingsToLoadSecurities.Tf15MinuteIsOn = param.Tf15MinuteIsOn;
            SettingsToLoadSecurities.Tf30MinuteIsOn = param.Tf30MinuteIsOn;
            SettingsToLoadSecurities.Tf1HourIsOn = param.Tf1HourIsOn;
            SettingsToLoadSecurities.Tf2HourIsOn = param.Tf2HourIsOn;
            SettingsToLoadSecurities.Tf4HourIsOn = param.Tf4HourIsOn;
            SettingsToLoadSecurities.TfDayIsOn = param.TfDayIsOn;
            SettingsToLoadSecurities.TfTickIsOn = param.TfTickIsOn;
            SettingsToLoadSecurities.TfMarketDepthIsOn = param.TfMarketDepthIsOn;

            SettingsToLoadSecurities.Source = param.Source;
            SettingsToLoadSecurities.SourceName = param.SourceName;
            SettingsToLoadSecurities.TimeStart = param.TimeStart;
            SettingsToLoadSecurities.TimeEnd = param.TimeEnd;
            SettingsToLoadSecurities.MarketDepthDepth = param.MarketDepthDepth;
            SettingsToLoadSecurities.NeedToUpdate = param.NeedToUpdate;

            ActivateLoaders();

        }

        public SettingsToLoadSecurity SettingsToLoadSecurities = new SettingsToLoadSecurity();

        public void Delete()
        {
            string pathSecurityFolder = "Data\\" + SetName + "\\" + SecName.RemoveExcessFromSecurityName();

            try
            {
                if (Directory.Exists(pathSecurityFolder))
                {
                    Directory.Delete(pathSecurityFolder, true);
                }
            }
            catch
            {
                // ignore
            }

            for (int i = 0; i < SecLoaders.Count; i++)
            {
                SecLoaders[i].Delete();
                SecLoaders[i].NewLogMessageEvent -= SendNewLogMessage;
            }

            SecLoaders.Clear();
        }

        public void Load(string saveStr)
        {
            string[] saveArray = saveStr.Split('~');

            SecName = saveArray[0];
            SecId = saveArray[1];
            SecClass = saveArray[2];
            IsCollapsed = Convert.ToBoolean(saveArray[3]);
            SecExchange = saveArray[4];
            SetName = saveArray[5];

            if (saveArray[6].Contains("False"))
            {
                SettingsToLoadSecurities.Load(saveArray[6]);
                if (saveArray.Length > 7)
                {
                    SecNameFull = saveArray[7];
                }
                ActivateLoaders();
            }
            else
            {
                PriceStep = saveArray[6].ToDecimal();
                VolumeStep = saveArray[7].ToDecimal();
                SettingsToLoadSecurities.Load(saveArray[8]);
                if (saveArray.Length > 9)
                {
                    SecNameFull = saveArray[9];
                }

                ActivateLoaders();
            }

        }

        public string GetSaveStr()
        {
            string result = SecName + "~";
            result += SecId + "~";
            result += SecClass + "~";
            result += IsCollapsed + "~";
            result += SecExchange + "~";
            result += SetName + "~";
            result += PriceStep + "~";
            result += VolumeStep + "~";
            result += SettingsToLoadSecurities.GetSaveStr() + "~";
            result += SecNameFull;

            return result;
        }

        public int PiecesToLoad()
        {
            int result = 0;

            for (int i = 0; i < SecLoaders.Count; i++)
            {
                result += SecLoaders[i].DataPies.Count;
            }

            return result;
        }

        public int LoadedPieces()
        {
            int result = 0;

            for (int i = 0; i < SecLoaders.Count; i++)
            {
                List<DataPie> pieces = SecLoaders[i].DataPies;

                for (int i2 = 0; i2 < pieces.Count; i2++)
                {
                    if (pieces[i2].Status == DataPieStatus.Load)
                    {
                        result += 1;
                    }
                }
            }

            return result;
        }

        #endregion

        #region Work on creation/deletion of final data stores

        private void ActivateLoaders()
        {
            if (SettingsToLoadSecurities.Tf1SecondIsOn)
            {
                TryCreateLoader(TimeFrame.Sec1);
            }
            else
            {
                TryDeleteLoader(TimeFrame.Sec1);
            }

            if (SettingsToLoadSecurities.Tf2SecondIsOn)
            {
                TryCreateLoader(TimeFrame.Sec2);
            }
            else
            {
                TryDeleteLoader(TimeFrame.Sec2);
            }

            if (SettingsToLoadSecurities.Tf5SecondIsOn)
            {
                TryCreateLoader(TimeFrame.Sec5);
            }
            else
            {
                TryDeleteLoader(TimeFrame.Sec5);
            }

            if (SettingsToLoadSecurities.Tf10SecondIsOn)
            {
                TryCreateLoader(TimeFrame.Sec10);
            }
            else
            {
                TryDeleteLoader(TimeFrame.Sec10);
            }

            if (SettingsToLoadSecurities.Tf15SecondIsOn)
            {
                TryCreateLoader(TimeFrame.Sec15);
            }
            else
            {
                TryDeleteLoader(TimeFrame.Sec15);
            }

            if (SettingsToLoadSecurities.Tf20SecondIsOn)
            {
                TryCreateLoader(TimeFrame.Sec20);
            }
            else
            {
                TryDeleteLoader(TimeFrame.Sec20);
            }

            if (SettingsToLoadSecurities.Tf30SecondIsOn)
            {
                TryCreateLoader(TimeFrame.Sec30);
            }
            else
            {
                TryDeleteLoader(TimeFrame.Sec30);
            }

            if (SettingsToLoadSecurities.Tf1MinuteIsOn)
            {
                TryCreateLoader(TimeFrame.Min1);
            }
            else
            {
                TryDeleteLoader(TimeFrame.Min1);
            }

            if (SettingsToLoadSecurities.Tf2MinuteIsOn)
            {
                TryCreateLoader(TimeFrame.Min2);
            }
            else
            {
                TryDeleteLoader(TimeFrame.Min2);
            }

            if (SettingsToLoadSecurities.Tf5MinuteIsOn)
            {
                TryCreateLoader(TimeFrame.Min5);
            }
            else
            {
                TryDeleteLoader(TimeFrame.Min5);
            }

            if (SettingsToLoadSecurities.Tf10MinuteIsOn)
            {
                TryCreateLoader(TimeFrame.Min10);
            }
            else
            {
                TryDeleteLoader(TimeFrame.Min10);
            }

            if (SettingsToLoadSecurities.Tf15MinuteIsOn)
            {
                TryCreateLoader(TimeFrame.Min15);
            }
            else
            {
                TryDeleteLoader(TimeFrame.Min15);
            }

            if (SettingsToLoadSecurities.Tf30MinuteIsOn)
            {
                TryCreateLoader(TimeFrame.Min30);
            }
            else
            {
                TryDeleteLoader(TimeFrame.Min30);
            }

            if (SettingsToLoadSecurities.Tf1HourIsOn)
            {
                TryCreateLoader(TimeFrame.Hour1);
            }
            else
            {
                TryDeleteLoader(TimeFrame.Hour1);
            }

            if (SettingsToLoadSecurities.Tf2HourIsOn)
            {
                TryCreateLoader(TimeFrame.Hour2);
            }
            else
            {
                TryDeleteLoader(TimeFrame.Hour2);
            }

            if (SettingsToLoadSecurities.Tf4HourIsOn)
            {
                TryCreateLoader(TimeFrame.Hour4);
            }
            else
            {
                TryDeleteLoader(TimeFrame.Hour4);
            }

            if (SettingsToLoadSecurities.TfDayIsOn)
            {
                TryCreateLoader(TimeFrame.Day);
            }
            else
            {
                TryDeleteLoader(TimeFrame.Day);
            }

            if (SettingsToLoadSecurities.TfTickIsOn)
            {
                TryCreateLoader(TimeFrame.Tick);
            }
            else
            {
                TryDeleteLoader(TimeFrame.Tick);
            }

            if (SettingsToLoadSecurities.TfMarketDepthIsOn)
            {
                TryCreateLoader(TimeFrame.MarketDepth);
            }
            else
            {
                TryDeleteLoader(TimeFrame.MarketDepth);
            }

            for (int i = 0; i < SecLoaders.Count; i++)
            {
                if (SecLoaders[i].TimeStart != SettingsToLoadSecurities.TimeStart
                    || SecLoaders[i].TimeEnd != SettingsToLoadSecurities.TimeEnd)
                {
                    SecLoaders[i].TimeStart = SettingsToLoadSecurities.TimeStart;
                    SecLoaders[i].TimeEnd = SettingsToLoadSecurities.TimeEnd;
                    SecLoaders[i].Status = SecurityLoadStatus.Activate;
                    SecLoaders[i].DataPies.Clear();
                    SecLoaders[i].CreateDataPies();
                }
            }
        }

        private void TryCreateLoader(TimeFrame frame)
        {
            bool isInArray = false;

            for (int i = 0; i < SecLoaders.Count; i++)
            {
                if (SecLoaders[i].TimeFrame == frame)
                {
                    isInArray = true;
                    break;
                }
            }

            if (isInArray == true)
            {
                return;
            }

            if (string.IsNullOrEmpty(SetName)
                || SetName == "Set_")
            {
                return;
            }

            SecurityTfLoader loader = new SecurityTfLoader(SetName, SecName, frame, SecClass, SecId, SecExchange, PriceStep, VolumeStep);
            loader.TimeStart = SettingsToLoadSecurities.TimeStart;
            loader.TimeEnd = SettingsToLoadSecurities.TimeEnd;
            loader.NewLogMessageEvent += SendNewLogMessage;
            loader.CreateDataPies();

            SecLoaders.Add(loader);
        }

        private void TryDeleteLoader(TimeFrame frame)
        {
            for (int i = 0; i < SecLoaders.Count; i++)
            {
                if (SecLoaders[i].TimeFrame == frame)
                {
                    SecLoaders[i].Delete();
                    SecLoaders[i].NewLogMessageEvent -= SendNewLogMessage;
                    SecLoaders.RemoveAt(i);
                    break;
                }
            }
        }

        public List<SecurityTfLoader> SecLoaders = new List<SecurityTfLoader>();

        public void Process(IServer server)
        {
            for (int i = 0; i < SecLoaders.Count; i++)
            {
                if (SettingsToLoadSecurities.Regime == DataSetState.Off)
                {
                    if (SecLoaders[i].TimeFrame == TimeFrame.MarketDepth)
                    {
                        SecLoaders[i].Process(server, SettingsToLoadSecurities);
                    }

                    continue;
                }

                SecLoaders[i].Process(server, SettingsToLoadSecurities);
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
            else
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion
    }

    public enum DataSetState
    {
        Off,
        On
    }

    public class SecurityTfLoader
    {
        #region Service

        private SecurityTfLoader() { }

        public SecurityTfLoader(string setName,
            string securityName,
            TimeFrame frame,
            string secClass,
            string secId,
            string exchange,
            decimal priceStep,
            decimal volumeStep)
        {
            _setName = setName;
            TimeFrame = frame;
            SecName = securityName;
            SecClass = secClass;
            SecId = secId;
            Exchange = exchange;
            PriceStep = priceStep;
            VolumeStep = volumeStep;

            CreatePaths();
        }

        private void CreatePaths()
        {

            if (!Directory.Exists("Data"))
            {
                Directory.CreateDirectory("Data");
            }

            if (!Directory.Exists("Data\\" + _setName))
            {
                Directory.CreateDirectory("Data\\" + _setName);
            }

            _pathSetFolder = "Data\\" + _setName;

            if (!Directory.Exists(_pathSetFolder))
            {
                Directory.CreateDirectory(_pathSetFolder);
            }

            _pathSecurityFolder = "Data\\" + _setName + "\\" + SecName.RemoveExcessFromSecurityName();

            if (!Directory.Exists(_pathSecurityFolder))
            {
                Directory.CreateDirectory(_pathSecurityFolder);
            }

            _pathMyTfFolder = _pathSecurityFolder + "\\" + TimeFrame.ToString();

            if (!Directory.Exists(_pathMyTfFolder))
            {
                Directory.CreateDirectory(_pathMyTfFolder);
            }

            _pathMyTxtFile = _pathMyTfFolder + "\\" + SecName.RemoveExcessFromSecurityName() + ".txt";

            _pathMyTempPieInTfFolder = _pathMyTfFolder + "\\Temp";

            if (!Directory.Exists(_pathMyTempPieInTfFolder))
            {
                Directory.CreateDirectory(_pathMyTempPieInTfFolder);
            }
        }

        public void Delete()
        {
            _isDeleted = true;

            try
            {
                if (Directory.Exists(_pathMyTfFolder))
                {
                    Directory.Delete(_pathMyTfFolder, true);
                }
            }
            catch
            {
                // ignore
            }

            for (int i = 0; DataPies != null && i < DataPies.Count; i++)
            {
                DataPies[i].Delete();
            }

            if (DataPies != null)
            {
                DataPies.Clear();
            }

            for (int i = 0; i < MdSources.Count; i++)
            {
                MdSources[i].Delete();
                MdSources[i].NewLogMessageEvent -= SendNewLogMessage;
            }

            if (MdSources != null)
            {
                MdSources = null;
            }
        }

        private bool _isDeleted = false;

        private string _setName;

        private string _pathSetFolder;

        private string _pathSecurityFolder;

        private string _pathMyTfFolder;

        private string _pathMyTempPieInTfFolder;

        private string _pathMyTxtFile;

        #endregion

        #region Information about the loader

        public TimeFrame TimeFrame;

        public string SecName = "";

        public string SecId = "";

        public string SecClass = "";

        public string Exchange = "";

        public decimal PriceStep;

        public decimal VolumeStep;

        public DateTime TimeStart;

        public DateTime TimeEnd;

        public DateTime TimeStartInReal;

        public DateTime TimeEndInReal;

        public void CheckTimeInSets()
        {
            if (_isDeleted)
            {
                return;
            }

            DateTime start = DateTime.MaxValue;
            DateTime end = DateTime.MinValue;

            for (int i = 0; DataPies != null && i < DataPies.Count; i++)
            {
                DataPie curPie = DataPies[i];

                DateTime curStart = curPie.StartFact;
                DateTime curEnd = curPie.EndFact;

                if (curStart != DateTime.MinValue &&
                    curStart < start)
                {
                    start = curStart;
                }

                if (curEnd != DateTime.MinValue &&
                    curEnd > end)
                {
                    end = curEnd;
                }
            }

            if (start == DateTime.MaxValue ||
                end == DateTime.MinValue)
            {
                TimeStartInReal = DateTime.MinValue;
                TimeEndInReal = DateTime.MinValue;

                return;
            }

            TimeStartInReal = start;
            TimeEndInReal = end;
        }

        public SecurityLoadStatus Status;

        public decimal PercentLoad()
        {
            if (_isDeleted)
            {
                return 0;
            }
            decimal max = 0;
            decimal loaded = 0;

            max += PiecesToLoad();
            loaded += LoadedPieces();


            if (max == 0)
            {
                return 0;
            }

            if (loaded == 0)
            {
                return 0;
            }

            decimal onePerc = max / 100;

            decimal result = loaded / onePerc;

            return Math.Round(result, 2);
        }

        private int PiecesToLoad()
        {
            int result = DataPies.Count;

            return result;
        }

        public int LoadedPieces()
        {
            int result = 0;

            List<DataPie> pieces = DataPies;

            for (int i2 = 0; i2 < pieces.Count; i2++)
            {
                if (pieces[i2].Status == DataPieStatus.Load)
                {
                    result += 1;
                }
            }

            return result;
        }

        public int Objects()
        {
            int result = 0;

            List<DataPie> pieces = DataPies;

            for (int i2 = 0; i2 < pieces.Count; i2++)
            {
                result += pieces[i2].ObjectCount;
            }

            return result;
        }

        #endregion

        #region Data loading 

        public void Process(IServer server, SettingsToLoadSecurity param)
        {
            try
            {
                if (TimeFrame == TimeFrame.Tick)
                {
                    ProcessTrades(server, param, true);
                }
                else if (TimeFrame == TimeFrame.MarketDepth)
                {
                    ProcessMarketDepth(param);
                }
                else if (TimeFrame == TimeFrame.Sec1
                    || TimeFrame == TimeFrame.Sec2
                    || TimeFrame == TimeFrame.Sec5
                    || TimeFrame == TimeFrame.Sec10
                    || TimeFrame == TimeFrame.Sec15
                    || TimeFrame == TimeFrame.Sec20
                    || TimeFrame == TimeFrame.Sec30)
                {
                    ProcessCandlesLessMinute(server, param);
                }
                else
                {
                    ProcessCandles(server, param);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public List<DataPie> DataPies = new List<DataPie>();

        public void CreateDataPies()
        {
            if (_isDeleted)
            {
                return;
            }
            if (TimeFrame == TimeFrame.MarketDepth)
            {
                Status = SecurityLoadStatus.Loading;
                return;
            }

            // создать пирог запросов

            List<DataPie> newCandleDataPies = new List<DataPie>();

            TimeSpan interval = new TimeSpan(30, 0, 0, 0);

            // трейды, рубим по 3 дня

            if (TimeFrame == TimeFrame.Tick
                    || TimeFrame == TimeFrame.Sec1
                    || TimeFrame == TimeFrame.Sec2
                    || TimeFrame == TimeFrame.Sec5
                    || TimeFrame == TimeFrame.Sec10
                    || TimeFrame == TimeFrame.Sec15
                    || TimeFrame == TimeFrame.Sec20
                    || TimeFrame == TimeFrame.Sec30)
            {
                TimeSpan timeInSet = TimeEnd - TimeStart;

                if (timeInSet.TotalDays >= 3)
                {
                    interval = new TimeSpan(3, 0, 0, 0);
                }
                else if (timeInSet.TotalDays >= 2)
                {
                    interval = new TimeSpan(2, 0, 0, 0);
                }
                else if (timeInSet.TotalDays >= 1)
                {
                    interval = new TimeSpan(1, 0, 0, 0);
                }
                else
                {
                    interval = new TimeSpan(0, 0, 0, 0);
                }
            }

            // 1 минутки рубим на месяца
            else if (TimeFrame == TimeFrame.Min1
                || TimeFrame == TimeFrame.Min2)
            {
                interval = new TimeSpan(30, 0, 0, 0);
            }

            // от 2 до 5 минут рубим по 2 месяца
            else if (TimeFrame == TimeFrame.Min3
               || TimeFrame == TimeFrame.Min5)
            {
                interval = new TimeSpan(60, 0, 0, 0);
            }

            // от 10 минут рубим по пол года
            else
            {
                interval = new TimeSpan(180, 0, 0, 0);
            }

            // цикл создания кусков данных

            DateTime timeStart = TimeStart;
            DateTime timeNow = timeStart.Add(interval);

            while (true)
            {
                DataPie newPie = new DataPie(_pathMyTempPieInTfFolder);
                newPie.Start = timeStart;
                newPie.End = timeNow;

                if (newPie.End > TimeEnd)
                {
                    newPie.End = TimeEnd;
                }

                newPie.LoadPieSettings();

                if (newPie.End > DateTime.Now.AddDays(1))
                {
                    newPie.End = DateTime.Now.Date.AddDays(1);
                }

                if (TimeFrame == TimeFrame.Tick
                    || TimeFrame == TimeFrame.Sec1
                    || TimeFrame == TimeFrame.Sec2
                    || TimeFrame == TimeFrame.Sec5
                    || TimeFrame == TimeFrame.Sec10
                    || TimeFrame == TimeFrame.Sec15
                    || TimeFrame == TimeFrame.Sec20
                    || TimeFrame == TimeFrame.Sec30)
                {
                    newPie.UpDateStatus();
                }
                else if (TimeFrame == TimeFrame.MarketDepth)
                {
                    // делаем ничего
                }
                else
                {
                    newPie.UpDateStatus();
                }

                newCandleDataPies.Add(newPie);

                if (TimeFrame == TimeFrame.Tick)
                {
                    timeStart = timeNow.AddDays(1);
                }
                else
                {
                    timeStart = timeNow;
                }

                timeNow = timeStart.Add(interval);

                if (timeNow > TimeEnd)
                {
                    timeNow = TimeEnd;
                }

                if (timeStart > TimeEnd)
                {
                    break;
                }

                if (timeStart > DateTime.Now.AddDays(1))
                {
                    break;
                }

                if (timeStart.Date > DateTime.Now.Date)
                {
                    break;
                }

                if (timeStart == timeNow)
                {
                    break;
                }
            }

            DataPies = newCandleDataPies;

            CheckTimeInSets();

        }

        private string _saveStrCandleCount;

        #endregion

        #region Candles

        private void ProcessCandles(IServer server, SettingsToLoadSecurity param)
        {
            if (_isDeleted)
            {
                return;
            }

            // пробуем создавать куски данных нужные для выгрузки

            if (DataPies == null
                || DataPies.Count == 0)
            {
                CreateDataPies();
                Status = SecurityLoadStatus.Activate;
                return;
            }

            if (DataPies.Count == 0)
            {
                return;
            }

            // загружаем эти куски из источника

            Status = SecurityLoadStatus.Loading;

            for (int i = 0; i < DataPies.Count; i++)
            {
                if (_isDeleted) { return; }

                if (DataPies[i].Status == DataPieStatus.Load &&
                   i + 1 != DataPies.Count)
                {
                    continue;
                }

                if (DataPies[i].Status == DataPieStatus.Load &&
                    i + 1 == DataPies.Count &&
                   param.NeedToUpdate == false)
                {
                    continue;
                }

                if (DataPies[i].CountTriesToLoadSet >= 3 &&
                    i + 1 != DataPies.Count)
                {
                    continue;
                }

                if (DataPies[i].CountTriesToLoadSet >= 3 &&
                   i + 1 == DataPies.Count &&
                   param.NeedToUpdate == false)
                {
                    continue;
                }

                if (param.Regime == DataSetState.Off)
                {
                    return;
                }

                DataPies[i].CountTriesToLoadSet = DataPies[i].CountTriesToLoadSet + 1;

                LoadCandleDataPieFromServer(DataPies[i], server);

                if (_isDeleted) { return; }

                if (DataPies[i].Status == DataPieStatus.Load)
                {
                    CheckTimeInSets();
                }
            }

            // сохраняем все куски в файл исходный

            Status = SecurityLoadStatus.Load;

            SaveCandleDataExitFile(DataPies);
        }

        private void LoadCandleDataPieFromServer(DataPie pie, IServer server)
        {
            if (_isDeleted) { return; }

            TimeFrameBuilder timeFrameBuilder = new TimeFrameBuilder(StartProgram.IsOsData);
            timeFrameBuilder.TimeFrame = TimeFrame;

            if (pie.Status == DataPieStatus.Load)
            {
                //return;
            }

            string id = SecId;

            if (id == null
                || id == "")
            {
                id = SecName;
            }

            SendNewLogMessage("Try load sec: " + id + " " + SecName + " , tf: " + TimeFrame +
                " , start: " + pie.Start.ToShortDateString() + " , end: " + pie.End.ToShortDateString(), LogMessageType.System);

            List<Candle> candles =
                server.GetCandleDataToSecurity(
                    id, SecClass, timeFrameBuilder,
                    pie.Start, pie.End.AddHours(23).AddMinutes(59), pie.Start, false);

            if (candles == null ||
                candles.Count == 0)
            {
                SendNewLogMessage("Error. No candles. sec: " + id + " " + SecName + " , tf: " + TimeFrame +
    " , start: " + pie.Start.ToShortDateString() + " , end: " + pie.End.ToString(), LogMessageType.System);

                return;
            }

            pie.SetNewCandlesInPie(candles);

            SendNewLogMessage("Candles Load Successfully. sec: " + id + " " + SecName + " , tf: " + TimeFrame +
" , start: " + pie.Start.ToShortDateString() + " , end: " + pie.End.ToString(), LogMessageType.System);
            pie.Status = DataPieStatus.Load;
        }

        public List<Candle> GetCandlesAllHistory()
        {
            if (_isDeleted) { return null; }

            if (DataPies == null)
            {
                return null;
            }

            List<Candle> extCandles = new List<Candle>();

            for (int i = 0; i < DataPies.Count; i++)
            {
                List<Candle> curCandle = DataPies[i].LoadCandleDataPieInTempFile();


                if (curCandle == null ||
                    curCandle.Count == 0)
                {
                    continue;
                }

                if (extCandles.Count == 0)
                {
                    extCandles.AddRange(curCandle);
                }
                else
                {
                    extCandles = extCandles.Merge(curCandle);
                }
            }

            return extCandles;
        }

        private void SaveCandleDataExitFile(List<DataPie> candleDataPies)
        {
            if (_isDeleted) { return; }

            string curSaveStrCandleCount = "";

            for (int i = 0; i < candleDataPies.Count; i++)
            {
                curSaveStrCandleCount += candleDataPies[i].ObjectCount;
            }

            if (curSaveStrCandleCount == _saveStrCandleCount)
            {
                return;
            }

            _saveStrCandleCount = curSaveStrCandleCount;

            List<Candle> extCandles = GetCandlesAllHistory();

            if (extCandles.Count == 0)
            {
                return;
            }

            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < extCandles.Count; i++)
            {
                builder.Append(extCandles[i].StringToSave);

                if (i + 1 != extCandles.Count)
                {
                    builder.Append("\n");
                }
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(_pathMyTxtFile, false))
                {
                    writer.WriteLine(builder.ToString());
                }
            }
            catch (Exception error)
            {
                if (_isDeleted) { return; }
                if (NewLogMessageEvent != null)
                {
                    NewLogMessageEvent(error.ToString(), LogMessageType.Error);
                }
            }

        }

        #endregion

        #region Trades

        private void ProcessTrades(IServer server, SettingsToLoadSecurity param, bool needToSave)
        {
            if (_isDeleted) { return; }

            // пробуем создавать куски данных нужные для выгрузки
            if (DataPies == null
                || DataPies.Count == 0)
            {
                CreateDataPies();
                Status = SecurityLoadStatus.Activate;
                return;
            }

            if (DataPies.Count == 0)
            {
                return;
            }

            // загружаем эти куски из источника

            Status = SecurityLoadStatus.Loading;

            for (int i = 0; i < DataPies.Count; i++)
            {
                if (_isDeleted) { return; }

                if (DataPies[i].Status == DataPieStatus.Load &&
                   i + 1 != DataPies.Count)
                {
                    continue;
                }

                if (DataPies[i].Status == DataPieStatus.Load &&
                    i + 1 == DataPies.Count &&
                   param.NeedToUpdate == false)
                {
                    continue;
                }

                if (DataPies[i].CountTriesToLoadSet >= 3 &&
                    i + 1 != DataPies.Count)
                {
                    continue;
                }

                if (DataPies[i].CountTriesToLoadSet >= 3 &&
                   i + 1 == DataPies.Count &&
                   param.NeedToUpdate == false)
                {
                    continue;
                }

                if (param.Regime == DataSetState.Off)
                {
                    return;
                }

                DataPies[i].CountTriesToLoadSet = DataPies[i].CountTriesToLoadSet + 1;

                LoadTradeDataPieFromServer(DataPies[i], server);

                if (_isDeleted) { return; }

                if (DataPies[i].Status == DataPieStatus.Load)
                {
                    CheckTimeInSets();
                }
            }

            // сохраняем все куски в файл исходный

            Status = SecurityLoadStatus.Load;

            if (needToSave)
            {
                SaveTradeDataExitFile();
            }
        }

        private void LoadTradeDataPieFromServer(DataPie pie, IServer server)
        {
            if (_isDeleted) { return; }

            TimeFrameBuilder timeFrameBuilder = new TimeFrameBuilder(StartProgram.IsOsData);
            timeFrameBuilder.TimeFrame = TimeFrame;

            string id = SecId;

            if (id == null
                || id == "")
            {
                id = SecName;
            }

            SendNewLogMessage("Try load sec: " + id + " " + SecName + " , tf: " + TimeFrame +
                " , start: " + pie.Start.ToShortDateString() + " , end: " + pie.End.ToShortDateString(), LogMessageType.System);

            List<Trade> trades =
                server.GetTickDataToSecurity(
                    id, SecClass, pie.Start, pie.End.AddDays(1), pie.Start, false);

            if (trades == null ||
                trades.Count == 0)
            {
                SendNewLogMessage("Error. No trades. sec: " + id + " " + SecName + " , tf: " + TimeFrame +
    " , start: " + pie.Start.ToShortDateString() + " , end: " + pie.End.ToShortDateString(), LogMessageType.System);

                return;
            }

            pie.SetNewTradesInPie(trades);

            SendNewLogMessage("Trades Load Successfully. sec: " + id + " " + SecName + " , tf: " + TimeFrame +
" , start: " + pie.Start.ToShortDateString() + " , end: " + pie.End.ToShortDateString(), LogMessageType.System);
            pie.Status = DataPieStatus.Load;
        }

        private List<Trade> GetTradesAllHistory()
        {
            if (_isDeleted) { return null; }

            if (DataPies == null)
            {
                return null;
            }

            List<Trade> extTrades = new List<Trade>();

            for (int i = 0; i < DataPies.Count; i++)
            {
                List<Trade> curTrades = DataPies[i].LoadTradeDataPieFromTempFile();

                if (curTrades == null ||
                   curTrades.Count == 0)
                {
                    continue;
                }

                if (extTrades.Count == 0)
                {
                    extTrades.AddRange(curTrades);
                }
                else
                {
                    extTrades = extTrades.Merge(curTrades);
                }
            }

            return extTrades;
        }

        private void SaveTradeDataExitFile()
        {
            if (_isDeleted)
            {
                return;
            }

            string curSaveStrObjectsCount = "";

            for (int i = 0; i < DataPies.Count; i++)
            {
                curSaveStrObjectsCount += DataPies[i].ObjectCount;
            }

            if (curSaveStrObjectsCount == _saveStrCandleCount)
            {
                return;
            }

            _saveStrCandleCount = curSaveStrObjectsCount;
            try
            {
                if (File.Exists(_pathMyTxtFile) == true)
                {
                    File.Delete(_pathMyTxtFile);
                }

                Trade lastTradeInLastPie = null;

                for (int i = 0; i < DataPies.Count; i++)
                {
                    List<Trade> curTrades = DataPies[i].LoadTradeDataPieFromTempFile();

                    if (curTrades == null ||
                       curTrades.Count == 0)
                    {
                        continue;
                    }

                    if (lastTradeInLastPie != null
                        && curTrades[0].Time < lastTradeInLastPie.Time)
                    {
                        if (NewLogMessageEvent != null)
                        {
                            NewLogMessageEvent(SecName + " " + TimeFrame + " Connector error. Trade time in pie Out of order", LogMessageType.Error);
                        }
                        return;
                    }

                    lastTradeInLastPie = curTrades[^1];

                    using (StreamWriter writer = new StreamWriter(_pathMyTxtFile, true))
                    {
                        for (int i2 = 0; i2 < curTrades.Count; i2++)
                        {
                            writer.WriteLine(curTrades[i2].GetSaveString());
                        }
                    }
                }

                if (NewLogMessageEvent != null)
                {
                    NewLogMessageEvent(SecName + " " + TimeFrame + " " + OsLocalization.Data.Label59, LogMessageType.System);
                }
            }
            catch (Exception error)
            {
                if (_isDeleted) { return; }
                if (NewLogMessageEvent != null)
                {
                    NewLogMessageEvent(error.ToString(), LogMessageType.Error);
                }
            }

        }

        #endregion

        #region Market Depth

        List<MarketDepthLoader> MdSources = new List<MarketDepthLoader>();

        private void ProcessMarketDepth(SettingsToLoadSecurity param)
        {
            if (_isDeleted)
            {
                return;
            }

            MarketDepthLoader loader = null;

            for (int i = 0; i < MdSources.Count; i++)
            {
                if (MdSources[i].SecName == SecName
                    && MdSources[i].SecClass == SecClass
                    && MdSources[i].Depth == param.MarketDepthDepth)
                {
                    loader = MdSources[i];

                    if (param.Regime == DataSetState.Off)
                    {
                        loader.IsLoad = false;
                        return;
                    }

                    break;
                }
            }

            if (loader == null)
            {
                loader = new MarketDepthLoader(SecName, SecClass, param.Source, param.SourceName, param.MarketDepthDepth, PriceStep, VolumeStep, _pathMyTfFolder);
                loader.NewLogMessageEvent += SendNewLogMessage;
                MdSources.Add(loader);
            }

            loader.IsLoad = true;
        }

        #endregion

        #region Candle with time frame less than minute

        private void ProcessCandlesLessMinute(IServer server, SettingsToLoadSecurity param)
        {
            if (_isDeleted)
            {
                return;
            }

            if (param.Regime == DataSetState.Off)
            {
                return;
            }

            ProcessTrades(server, param, false);

            List<Candle> candles = GetExtCandlesFromTrades();

            if (candles == null ||
                candles.Count == 0)
            {
                return;
            }

            SaveCandleLessMinuteDataExitFile(candles);
        }

        public List<Candle> GetExtCandlesFromTrades()
        {
            List<Trade> trades = GetTradesAllHistory();

            if (trades == null
                || trades.Count == 0)
            {
                return null;
            }

            TimeFrameBuilder timeFrameBuilder = new TimeFrameBuilder(StartProgram.IsOsData);
            timeFrameBuilder.TimeFrame = TimeFrame;

            CandleSeries series = new CandleSeries(timeFrameBuilder, new Security() { Name = "Unknown" }, StartProgram.IsOsConverter);

            series.IsStarted = true;

            series.SetNewTicks(trades);

            return series.CandlesAll;
        }

        private void SaveCandleLessMinuteDataExitFile(List<Candle> candles)
        {
            if (_isDeleted) { return; }

            string curSaveStrCandleCount = candles.Count.ToString();

            if (curSaveStrCandleCount == _saveStrCandleCount)
            {
                return;
            }

            _saveStrCandleCount = curSaveStrCandleCount;

            List<Candle> extCandles = candles;

            if (extCandles.Count == 0)
            {
                return;
            }

            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < extCandles.Count; i++)
            {
                builder.Append(extCandles[i].StringToSave);

                if (i + 1 != extCandles.Count)
                {
                    builder.Append("\n");
                }
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(_pathMyTxtFile, false))
                {
                    writer.WriteLine(builder.ToString());
                }
            }
            catch (Exception error)
            {
                if (_isDeleted) { return; }
                if (NewLogMessageEvent != null)
                {
                    NewLogMessageEvent(error.ToString(), LogMessageType.Error);
                }
            }

        }

        #endregion

        #region Logging

        public void SendNewLogMessage(string message, LogMessageType type)
        {
            if (_isDeleted) { return; }

            if (NewLogMessageEvent != null)
            {
                NewLogMessageEvent(message, type);
            }
            else
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion
    }

    public class MarketDepthLoader
    {
        #region Constructor

        public MarketDepthLoader(string secName, string secClass, ServerType serverType, string serverName, int depth, decimal priceStep, decimal volumeStep, string pathSecurityFolder)
        {
            _secName = secName;
            _secClass = secClass;
            _serverType = serverType;
            _serverName = serverName;
            _depth = depth;
            _priceStep = priceStep;
            _volumeStep = volumeStep;
            _pathSecurityFolder = pathSecurityFolder;

            CreateSource();
        }

        public void Delete()
        {
            _isDeleted = true;

            OffStream();

            if (MarketDepthSource != null)
            {
                MarketDepthSource.Clear();
                MarketDepthSource.Delete();
                MarketDepthSource.MarketDepthUpdateEvent -= MarketDepthSource_MarketDepthUpdateEvent;
                MarketDepthSource.LogMessageEvent -= SendNewLogMessage;
                MarketDepthSource = null;
            }

            MarketDepthQueue.Clear();
            MarketDepthQueue = null;
            _lastMarketDepth = null;
        }

        private void CreateSource()
        {
            string nameTab = "osDataMdSource_"
                + _serverType + "_"
                + _secName + "_"
                + _secClass + "_"
                + _depth;

            MarketDepthSource = new BotTabSimple(nameTab, StartProgram.IsOsData);
            MarketDepthSource.Connector.SecurityName = _secName;
            MarketDepthSource.Connector.SecurityClass = _secClass;
            MarketDepthSource.Connector.ServerType = _serverType;
            MarketDepthSource.Connector.ServerFullName = _serverName;
            MarketDepthSource.TimeFrameBuilder.TimeFrame = TimeFrame.MarketDepth;

            MarketDepthSource.MarketDepthUpdateEvent += MarketDepthSource_MarketDepthUpdateEvent;
            MarketDepthSource.LogMessageEvent += SendNewLogMessage;

            Task.Run(() => UpdateMarketDataAsync());
        }

        private void CreateHeader(BinaryWriter writer)
        {
            try
            {
                byte[] signature = Encoding.UTF8.GetBytes("QScalp History Data");
                writer.Write(signature);
                writer.Write((byte)4);
                WriteString(writer, "OsEngine");
                WriteString(writer, $"VolumeStep:{_volumeStep}");

                long startTicks = DateTime.UtcNow.Ticks;
                _lastTimeStampMarketDepth = TimeManager.GetTimeStampMillisecondsFromStartTime(DateTime.UtcNow);
                writer.Write(startTicks);

                byte streamCount = (byte)1;
                writer.Write(streamCount);

                writer.Write(GetStreamId(StreamType.Quotes));

                string instrumentCode = $"{_serverType.ToString()}:{_secName}:{_secClass}:1:{_priceStep}";
                WriteString(writer, instrumentCode);

                writer.Flush();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private byte GetStreamId(StreamType streamType)
        {
            return streamType switch
            {
                StreamType.Quotes => 0x10,
                StreamType.Deals => 0x20,
                StreamType.OwnOrders => 0x30,
                StreamType.OwnTrades => 0x40,
                StreamType.Messages => 0x50,
                StreamType.AuxInfo => 0x60,
                StreamType.OrdLog => 0x70,
                _ => throw new ArgumentException("Unknown stream type")
            };
        }

        private void WriteString(BinaryWriter writer, string value)
        {
            try
            {
                if (string.IsNullOrEmpty(value))
                {
                    ULeb128.WriteULeb128(writer, 0);
                    return;
                }

                byte[] bytes = Encoding.UTF8.GetBytes(value);
                ULeb128.WriteULeb128(writer, (ulong)bytes.Length);
                writer.Write(bytes);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Thread converter

        private async Task UpdateMarketDataAsync()
        {
            while (true)
            {
                try
                {
                    if (MainWindow.ProccesIsWorked == false) return;

                    if (_isDeleted) return;

                    if (MarketDepthQueue.IsEmpty == false)
                    {
                        MarketDepth md = null;

                        if (MarketDepthQueue.TryDequeue(out md))
                        {
                            if (md == null) continue;

                            if (_lastMarketDepth == null || (_lastMarketDepth != null && _lastMarketDepth.Time.Day != md.Time.Day))
                            {
                                _lastMarketDepth = null;
                                _lastPrice = 0;
                                _lastTimeStampMarketDepth = 0;

                                _filePath = _pathSecurityFolder + "\\" + SecName.RemoveExcessFromSecurityName() + "." + md.Time.ToString("yyyy-MM-dd") + ".Quotes" + ".qsh";

                                OffStream();
                            }

                            if (_lastMarketDepth == null && File.Exists(_filePath))
                            {
                                ReadBinaryFile();
                            }

                            if (_lastMarketDepth == null)
                            {
                                _fileStream = new FileStream(_filePath, FileMode.Create, FileAccess.Write);
                                _binaryWriter = new BinaryWriter(_fileStream, Encoding.UTF8);

                                CreateHeader(_binaryWriter);
                                WriteFrameHeader(_binaryWriter, md.Time);
                                WriteFirstMarketDepthData(_binaryWriter, md);

                                _lastMarketDepth = md;
                            }
                            else
                            {
                                if (_fileStream == null)
                                {
                                    _fileStream = new FileStream(_filePath, FileMode.Append, FileAccess.Write);
                                }

                                if (_binaryWriter == null)
                                {
                                    _binaryWriter = new BinaryWriter(_fileStream, Encoding.UTF8);
                                }

                                WriteSecondMarketDepthData(_binaryWriter, md);
                            }
                        }
                    }
                    else
                    {
                        _binaryWriter.Flush();

                        OffStream();

                        await Task.Delay(1000);
                    }
                }
                catch (Exception ex)
                {
                    SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        private void OffStream()
        {
            if (_binaryWriter != null)
            {
                _binaryWriter.Dispose();
                _binaryWriter = null;
            }

            if (_fileStream != null)
            {
                _fileStream.Dispose();
                _fileStream = null;
            }
        }

        private void WriteSecondMarketDepthData(BinaryWriter writer, MarketDepth md)
        {
            try
            {
                List<QuoteChange> changes = new List<QuoteChange>();

                ProcessAsksChanges(_lastMarketDepth.Asks, md.Asks, changes);
                ProcessBidsChanges(_lastMarketDepth.Bids, md.Bids, changes);

                if (changes.Count > 0)
                {
                    WriteFrameHeader(writer, md.Time);
                    WriteChangesToFile(writer, changes);
                    _lastMarketDepth = md;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WriteChangesToFile(BinaryWriter writer, List<QuoteChange> changes)
        {
            Leb128.WriteLeb128(writer, changes.Count);

            for (int i = changes.Count - 1; i >= 0; i--)
            {
                QuoteChange change = changes[i];

                Leb128.WriteLeb128(writer, change.Price - _lastPrice);
                _lastPrice = change.Price;

                Leb128.WriteLeb128(writer, change.Volume);
            }
        }

        private void ProcessAsksChanges(List<MarketDepthLevel> oldAsks, List<MarketDepthLevel> newAsks, List<QuoteChange> changes)
        {
            Dictionary<double, double> oldAsksDict = new Dictionary<double, double>();
            for (int i = 0; i < oldAsks.Count; i++)
            {
                MarketDepthLevel ask = oldAsks[i];
                oldAsksDict[ask.Price] = ask.Ask;
            }

            Dictionary<double, double> newAsksDict = new Dictionary<double, double>();
            for (int i = 0; i < newAsks.Count; i++)
            {
                MarketDepthLevel ask = newAsks[i];
                newAsksDict[ask.Price] = ask.Ask;
            }

            for (int i = 0; i < oldAsks.Count; i++)
            {
                MarketDepthLevel oldAsk = oldAsks[i];

                if (!newAsksDict.ContainsKey(oldAsk.Price))
                {
                    changes.Add(new QuoteChange
                    {
                        Price = (long)((decimal)oldAsk.Price / _priceStep),
                        Volume = 0
                    });
                }
            }

            for (int i = 0; i < newAsks.Count; i++)
            {
                MarketDepthLevel newAsk = newAsks[i];

                if (oldAsksDict.TryGetValue(newAsk.Price, out double oldVolume))
                {
                    if (newAsk.Ask != oldVolume)
                    {
                        changes.Add(new QuoteChange
                        {
                            Price = (long)((decimal)newAsk.Price / _priceStep),
                            Volume = (long)((decimal)newAsk.Ask / _volumeStep)
                        });
                    }
                }
                else
                {
                    changes.Add(new QuoteChange
                    {
                        Price = (long)((decimal)newAsk.Price / _priceStep),
                        Volume = (long)((decimal)newAsk.Ask / _volumeStep)
                    });
                }
            }
        }

        private void ProcessBidsChanges(List<MarketDepthLevel> oldBids, List<MarketDepthLevel> newBids, List<QuoteChange> changes)
        {
            Dictionary<double, double> oldBidsDict = new Dictionary<double, double>();
            for (int i = 0; i < oldBids.Count; i++)
            {
                MarketDepthLevel bid = oldBids[i];
                oldBidsDict[bid.Price] = bid.Bid;
            }

            Dictionary<double, double> newBidsDict = new Dictionary<double, double>();
            for (int i = 0; i < newBids.Count; i++)
            {
                MarketDepthLevel bid = newBids[i];
                newBidsDict[bid.Price] = bid.Bid;
            }

            for (int i = 0; i < oldBids.Count; i++)
            {
                MarketDepthLevel oldBid = oldBids[i];

                if (!newBidsDict.ContainsKey(oldBid.Price))
                {
                    changes.Insert(0, new QuoteChange
                    {
                        Price = (long)((decimal)oldBid.Price / _priceStep),
                        Volume = 0
                    });
                }
            }

            for (int i = 0; i < newBids.Count; i++)
            {
                MarketDepthLevel newBid = newBids[i];

                if (oldBidsDict.TryGetValue(newBid.Price, out double oldVolume))
                {
                    if (newBid.Bid != oldVolume)
                    {
                        changes.Insert(0, new QuoteChange
                        {
                            Price = (long)((decimal)newBid.Price / _priceStep),
                            Volume = -(long)((decimal)newBid.Bid / _volumeStep)
                        });
                    }
                }
                else
                {
                    changes.Insert(0, new QuoteChange
                    {
                        Price = (long)((decimal)newBid.Price / _priceStep),
                        Volume = -(long)((decimal)newBid.Bid / _volumeStep)
                    });
                }
            }
        }

        private void WriteFirstMarketDepthData(BinaryWriter writer, MarketDepth md)
        {
            try
            {
                List<QuoteChange> changes = new List<QuoteChange>();

                for (int i = 0; i < md.Asks.Count; i++)
                {
                    QuoteChange quoteChange = new QuoteChange();

                    quoteChange.Price = (long)((decimal)md.Asks[i].Price / _priceStep);
                    quoteChange.Volume = (long)((decimal)md.Asks[i].Ask / _volumeStep);

                    changes.Add(quoteChange);
                }

                for (int i = 0; i < md.Bids.Count; i++)
                {
                    QuoteChange quoteChange = new QuoteChange();

                    quoteChange.Price = (long)((decimal)md.Bids[i].Price / _priceStep);
                    quoteChange.Volume = -(long)((decimal)md.Bids[i].Bid / _volumeStep);

                    changes.Insert(0, quoteChange);
                }

                WriteChangesToFile(writer, changes);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WriteFrameHeader(BinaryWriter writer, DateTime time)
        {
            try
            {
                long timeStamp = TimeManager.GetTimeStampMillisecondsFromStartTime(time);

                WriteGrowing(writer, timeStamp);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WriteGrowing(BinaryWriter writer, long timeStamp)
        {
            try
            {
                long diff = timeStamp - _lastTimeStampMarketDepth;
                _lastTimeStampMarketDepth = timeStamp;

                if (diff >= 0 && diff <= 268435454)
                {
                    ULeb128.WriteULeb128(writer, ((ulong)diff));
                }
                else
                {
                    ULeb128.WriteULeb128(writer, 268435455);
                    Leb128.WriteLeb128(writer, diff);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Read file

        private bool ReadBinaryFile()
        {
            using (FileStream fs = File.OpenRead(_filePath))
            {
                try
                {
                    Stream stream = GetDataStream(fs, _prefix);

                    if (stream == null)
                    {
                        ServerMaster.SendNewLogMessage("Incorrect file format", LogMessageType.Error);
                        return false;
                    }

                    DataBinaryReader dataReader = new DataBinaryReader(stream);

                    int version = stream.ReadByte();

                    switch (version)
                    {
                        case 4:

                            string appName = dataReader.ReadString();
                            string comment = dataReader.ReadString();

                            if (comment != "")
                            {
                                string[] volumeSplit = comment.Split(':');

                                double volumeStep = 1;

                                if (volumeSplit.Length == 2)
                                {
                                    if (volumeSplit[0] == "VolumeStep" && double.TryParse(volumeSplit[1].Replace(',', '.'), NumberStyles.Float, NumberFormatInfo.InvariantInfo, out volumeStep))
                                    {
                                        _volumeStep = (decimal)volumeStep;
                                    }
                                }
                            }

                            if (_volumeStep == 0)
                                _volumeStep = 1;

                            DateTime time = new DateTime(dataReader.ReadInt64(), DateTimeKind.Utc);
                            _lastTimeStampMarketDepth = TimeManager.GetTimeStampMillisecondsFromStartTime(time);

                            int streamCount = dataReader.ReadByte();

                            if (streamCount == 0) return false;

                            StreamType streamType = (StreamType)dataReader.ReadByte();

                            if (streamType != StreamType.Quotes) return false;

                            string securityName = dataReader.ReadString();

                            string[] step = securityName.Split(':');

                            double priceStep = 1;
                            if (step.Length == 5)
                            {
                                if (double.TryParse(step[4].Replace(',', '.'), NumberStyles.Float, NumberFormatInfo.InvariantInfo, out priceStep))
                                {
                                    _priceStep = (decimal)priceStep;
                                }
                                else return false;
                            }
                            else return false;

                            break;

                        default:
                            ServerMaster.SendNewLogMessage("Unsupported file version (" + version + ")", LogMessageType.Error);
                            return false;
                    }

                    MarketDepth marketDepth = new MarketDepth();

                    try
                    {
                        while (true)
                        {
                            marketDepth.SetMarketDepthFromBinaryFile(dataReader, _priceStep, (double)_volumeStep, _lastTimeStampMarketDepth);
                            _lastMarketDepth = marketDepth;
                            _lastTimeStampMarketDepth = TimeManager.GetTimeStampMillisecondsFromStartTime(_lastMarketDepth.Time);
                            _lastPrice = marketDepth.LastBinaryPrice;
                        }
                    }
                    catch (EndOfStreamException)
                    {
                        //ignore
                    }

                    dataReader.Dispose();
                    dataReader.Close();
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }


        private bool CheckPrefix(Stream stream, byte[] buffer, byte[] prefix)
        {
            int length = stream.Read(buffer, 0, buffer.Length);
            if (length != prefix.Length)
                return false;

            for (int i = 0; i < buffer.Length; i++)
                if (buffer[i] != prefix[i])
                    return false;

            return true;
        }

        private Stream GetDataStream(FileStream fs, byte[] prefix)
        {
            byte[] buffer = new byte[prefix.Length];

            if (CheckPrefix(fs, buffer, prefix))
                return fs;

            Stream stream = null;

            try
            {
                fs.Position = 0;
                stream = new GZipStream(fs, CompressionMode.Decompress, true);

                if (CheckPrefix(stream, buffer, prefix))
                    return stream;
            }
            catch { }

            if (stream != null)
            {
                stream.Dispose();
                stream = null;
            }

            try
            {
                fs.Position = 0;
                stream = new DeflateStream(fs, CompressionMode.Decompress, true);

                if (CheckPrefix(stream, buffer, prefix))
                    return stream;
            }
            catch { }

            if (stream != null)
            {
                stream.Dispose();
                stream = null;
            }

            return null;
        }

        #endregion

        #region Fields

        private bool _isDeleted;

        private ServerType _serverType;

        private string _serverName;

        private readonly string _pathSecurityFolder;

        private string _filePath;

        public BotTabSimple MarketDepthSource;

        private ConcurrentQueue<MarketDepth> MarketDepthQueue = new ConcurrentQueue<MarketDepth>();

        public event Action<string, LogMessageType> NewLogMessageEvent;

        private MarketDepth _lastMarketDepth;

        private FileStream _fileStream;

        private BinaryWriter _binaryWriter;

        private decimal _priceStep;

        private decimal _volumeStep;

        private long _lastPrice;

        private long _lastTimeStampMarketDepth;

        private readonly byte[] _prefix = Encoding.UTF8.GetBytes("QScalp History Data");

        #endregion

        #region Properties

        public string SecName
        {
            get { return _secName; }
        }
        private string _secName;

        public string SecClass
        {
            get { return _secClass; }
        }
        private string _secClass;

        public int Depth
        {
            get { return _depth; }
        }
        private int _depth;

        public bool IsLoad;

        #endregion

        #region Events 

        private void MarketDepthSource_MarketDepthUpdateEvent(MarketDepth md)
        {
            if (_isDeleted == true) return;

            if (IsLoad == false) return;

            MarketDepthQueue.Enqueue(md);
        }

        #endregion

        #region Log

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (_isDeleted) { return; }

            if (NewLogMessageEvent != null)
            {
                NewLogMessageEvent(message, type);
            }
            else
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        #endregion
    }

    public class SettingsToLoadSecurity
    {
        public SettingsToLoadSecurity()
        {
            Regime = DataSetState.Off;
            Tf1MinuteIsOn = false;
            Tf2MinuteIsOn = false;
            Tf5MinuteIsOn = true;
            Tf10MinuteIsOn = false;
            Tf15MinuteIsOn = false;
            Tf30MinuteIsOn = true;
            Tf1HourIsOn = false;
            Tf2HourIsOn = false;
            Tf4HourIsOn = false;
            TfTickIsOn = false;
            TfMarketDepthIsOn = false;
            Source = ServerType.None;
            TimeStart = DateTime.Now.AddDays(-5);
            TimeEnd = DateTime.Now.AddDays(5);
            MarketDepthDepth = 5;

        }

        public void Load(string saveStr)
        {
            string[] saveArray = saveStr.Split('%');
            Enum.TryParse(saveArray[0], out Regime);

            Tf1SecondIsOn = Convert.ToBoolean(saveArray[1]);
            Tf2SecondIsOn = Convert.ToBoolean(saveArray[2]);
            Tf5SecondIsOn = Convert.ToBoolean(saveArray[3]);
            Tf10SecondIsOn = Convert.ToBoolean(saveArray[4]);
            Tf15SecondIsOn = Convert.ToBoolean(saveArray[5]);
            Tf20SecondIsOn = Convert.ToBoolean(saveArray[6]);
            Tf30SecondIsOn = Convert.ToBoolean(saveArray[7]);

            Tf1MinuteIsOn = Convert.ToBoolean(saveArray[8]);
            Tf2MinuteIsOn = Convert.ToBoolean(saveArray[9]);
            Tf5MinuteIsOn = Convert.ToBoolean(saveArray[10]);
            Tf10MinuteIsOn = Convert.ToBoolean(saveArray[11]);
            Tf15MinuteIsOn = Convert.ToBoolean(saveArray[12]);
            Tf30MinuteIsOn = Convert.ToBoolean(saveArray[13]);
            Tf1HourIsOn = Convert.ToBoolean(saveArray[14]);
            Tf2HourIsOn = Convert.ToBoolean(saveArray[15]);
            Tf4HourIsOn = Convert.ToBoolean(saveArray[16]);
            TfTickIsOn = Convert.ToBoolean(saveArray[17]);
            TfMarketDepthIsOn = Convert.ToBoolean(saveArray[18]);

            Enum.TryParse(saveArray[19], out Source);
            try
            {
                TimeStart = Convert.ToDateTime(saveArray[20], CultureInfo.InvariantCulture);
                TimeEnd = Convert.ToDateTime(saveArray[21], CultureInfo.InvariantCulture);
            }
            catch
            {
                TimeStart = Convert.ToDateTime(saveArray[20]);
                TimeEnd = Convert.ToDateTime(saveArray[21]);
            }

            MarketDepthDepth = Convert.ToInt32(saveArray[22]);
            NeedToUpdate = Convert.ToBoolean(saveArray[23]);

            try
            {
                TfDayIsOn = Convert.ToBoolean(saveArray[24]);
            }
            catch
            {
                // ignore
            }

            try
            {
                SourceName = saveArray[25];
            }
            catch
            {
                // ignore
            }
        }

        public string GetSaveStr()
        {
            string result = "";

            result += Regime + "%";

            result += Tf1SecondIsOn + "%";
            result += Tf2SecondIsOn + "%";
            result += Tf5SecondIsOn + "%";
            result += Tf10SecondIsOn + "%";
            result += Tf15SecondIsOn + "%";
            result += Tf20SecondIsOn + "%";
            result += Tf30SecondIsOn + "%";

            result += Tf1MinuteIsOn + "%";
            result += Tf2MinuteIsOn + "%";
            result += Tf5MinuteIsOn + "%";
            result += Tf10MinuteIsOn + "%";
            result += Tf15MinuteIsOn + "%";
            result += Tf30MinuteIsOn + "%";
            result += Tf1HourIsOn + "%";
            result += Tf2HourIsOn + "%";
            result += Tf4HourIsOn + "%";
            result += TfTickIsOn + "%";
            result += TfMarketDepthIsOn + "%";

            result += Source + "%";
            result += TimeStart.ToString(CultureInfo.InvariantCulture) + "%";
            result += TimeEnd.ToString(CultureInfo.InvariantCulture) + "%";
            result += MarketDepthDepth + "%";
            result += NeedToUpdate + "%";
            result += TfDayIsOn + "%";
            result += SourceName + "%";

            return result;
        }

        public DataSetState Regime;
        public DateTime TimeStart;
        public DateTime TimeEnd;
        public ServerType Source;
        public string SourceName;

        public bool Tf1SecondIsOn;
        public bool Tf2SecondIsOn;
        public bool Tf5SecondIsOn;
        public bool Tf10SecondIsOn;
        public bool Tf15SecondIsOn;
        public bool Tf20SecondIsOn;
        public bool Tf30SecondIsOn;

        public bool Tf1MinuteIsOn;
        public bool Tf2MinuteIsOn;
        public bool Tf5MinuteIsOn;
        public bool Tf10MinuteIsOn;
        public bool Tf15MinuteIsOn;
        public bool Tf30MinuteIsOn;
        public bool Tf1HourIsOn;
        public bool Tf2HourIsOn;
        public bool Tf4HourIsOn;
        public bool TfDayIsOn;
        public bool TfTickIsOn;
        public bool TfMarketDepthIsOn;
        public int MarketDepthDepth;
        public bool NeedToUpdate;
    }

    public class DataPie
    {
        public DataPie(string tempFileDirectory)
        {
            _pathMyTempPieInTfFolder = tempFileDirectory;
        }

        public string _pathMyTempPieInTfFolder;

        public int CountTriesToLoadSet
        {
            get
            {
                return _countTriesToLoadSet;
            }
            set
            {
                if (_countTriesToLoadSet == value)
                {
                    return;
                }

                _countTriesToLoadSet = value;
                SavePieSettings();
            }
        }
        private int _countTriesToLoadSet;

        public void Delete()
        {

        }

        public void Clear()
        {
            try
            {
                CountTriesToLoadSet = 0;
                ObjectCount = 0;
                StartFact = DateTime.MinValue;
                EndFact = DateTime.MinValue;
                Status = DataPieStatus.None;

                if (File.Exists(_pathMyTempPieInTfFolder + "\\" + TempFileName))
                {
                    File.Delete(_pathMyTempPieInTfFolder + "\\" + TempFileName);
                }
            }
            catch
            {
                // ignore
            }
        }

        public void UpDateStatus()
        {
            // 1 Актуальное время старта
            CandlePieStatusInfo CandlesInfo = null;

            if (_pathMyTempPieInTfFolder.Contains("Tick") == false
                &&
                _pathMyTempPieInTfFolder.Contains("Sec") == false)
            {
                CandlesInfo = LoadCandlesPieStatus();
            }

            TradePieStatusInfo TradesInfo = null;

            if ((CandlesInfo == null
                || CandlesInfo.FirstCandle == null)
                &&
                (_pathMyTempPieInTfFolder.Contains("Tick") == true
                || _pathMyTempPieInTfFolder.Contains("Sec") == true))
            {
                TradesInfo = LoadTradesPieStatus();
            }

            DateTime start = DateTime.MinValue;

            if (CandlesInfo != null && CandlesInfo.FirstCandle != null)
            {
                start = CandlesInfo.FirstCandle.TimeStart;
            }

            if (TradesInfo != null && TradesInfo.FirstTrade != null)
            {
                start = TradesInfo.FirstTrade.Time;
            }

            StartFact = start;

            // 2 актуальное время конца

            DateTime end = DateTime.MinValue;

            if (CandlesInfo != null && CandlesInfo.LastCandle != null)
            {
                end = CandlesInfo.LastCandle.TimeStart;
            }

            if (TradesInfo != null && TradesInfo.LastTrade != null)
            {
                end = TradesInfo.LastTrade.Time;
            }

            EndFact = end;

            if (CandlesInfo == null &&
                TradesInfo == null)
            {
                ObjectCount = 0;
            }

            if (CandlesInfo != null)
            {
                ObjectCount = CandlesInfo.CandlesCount;
            }

            if (TradesInfo != null)
            {
                ObjectCount = TradesInfo.TradesCount;
            }
        }

        public DateTime Start;

        public DateTime StartFact;

        public DateTime End;

        public DateTime EndFact;

        public DataPieStatus Status;

        public int ObjectCount;

        public string TempFileName
        {
            get
            {
                if (_tempFileName != null)
                {
                    return _tempFileName;
                }

                _tempFileName = Start.ToString("yyyyMMdd") + "_" + End.ToString("yyyyMMdd") + ".txt";

                return _tempFileName;
            }
        }

        private string _tempFileName;

        public void LoadPieSettings()
        {
            string pathToTempFile = _pathMyTempPieInTfFolder + "\\" + "Settings_" + TempFileName;

            if (File.Exists(pathToTempFile) == false)
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(pathToTempFile))
                {
                    _countTriesToLoadSet = Convert.ToInt32(reader.ReadLine());
                }
            }
            catch
            {
                //ignore
            }
        }

        private void SavePieSettings()
        {
            string pathToTempFile = _pathMyTempPieInTfFolder + "\\" + "Settings_" + TempFileName;

            try
            {
                using (StreamWriter writer = new StreamWriter(pathToTempFile, false))
                {

                    writer.WriteLine(CountTriesToLoadSet);

                }
            }
            catch
            {
                // ignore
            }
        }

        #region Candles

        public void SetNewCandlesInPie(List<Candle> candles)
        {
            SaveCandleDataPieInTempFile(candles);
            UpDateStatus();
        }

        public CandlePieStatusInfo LoadCandlesPieStatus()
        {
            string pathToTempFile = _pathMyTempPieInTfFolder + "\\" + TempFileName;

            if (File.Exists(pathToTempFile) == false)
            {
                return null;
            }

            CandlePieStatusInfo result = new CandlePieStatusInfo();

            int candlesCount = 0;

            try
            {
                using (StreamReader reader = new StreamReader(pathToTempFile))
                {
                    while (reader.EndOfStream == false)
                    {
                        candlesCount++;
                        string str = reader.ReadLine();

                        if (result.FirstCandle == null)
                        {
                            Candle newCandle = new Candle();
                            newCandle.SetCandleFromString(str);
                            result.FirstCandle = newCandle;
                        }
                        if (reader.EndOfStream == true)
                        {
                            Candle newCandle = new Candle();
                            newCandle.SetCandleFromString(str);
                            result.LastCandle = newCandle;
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            result.CandlesCount = candlesCount;

            if (result.CandlesCount != 0)
            {
                Status = DataPieStatus.Load;
            }

            return result;
        }

        public List<Candle> LoadCandleDataPieInTempFile()
        {
            string pathToTempFile = _pathMyTempPieInTfFolder + "\\" + TempFileName;

            if (File.Exists(pathToTempFile) == false)
            {
                return null;
            }

            List<Candle> candles = new List<Candle>();

            try
            {
                using (StreamReader reader = new StreamReader(pathToTempFile))
                {
                    while (reader.EndOfStream == false)
                    {
                        string str = reader.ReadLine();

                        Candle newCandle = new Candle();
                        newCandle.SetCandleFromString(str);
                        candles.Add(newCandle);
                    }
                }
            }
            catch
            {
                // ignore
            }

            if (candles.Count != 0)
            {
                Status = DataPieStatus.Load;
            }

            return candles;
        }

        private void SaveCandleDataPieInTempFile(List<Candle> candles)
        {
            string pathToTempFile = _pathMyTempPieInTfFolder + "\\" + TempFileName;

            try
            {
                DateTime realEnd = End.AddDays(1);

                using (StreamWriter writer = new StreamWriter(pathToTempFile, false))
                {
                    for (int i = 0; i < candles.Count; i++)
                    {
                        if (candles[i].TimeStart < Start)
                        {
                            continue;
                        }

                        if (candles[i].TimeStart > realEnd)
                        {
                            break;
                        }

                        writer.WriteLine(candles[i].StringToSave);
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region Trades

        public TradePieStatusInfo LoadTradesPieStatus()
        {
            string pathToTempFile = _pathMyTempPieInTfFolder + "\\" + TempFileName;

            if (File.Exists(pathToTempFile) == false)
            {
                return null;
            }

            TradePieStatusInfo info = new TradePieStatusInfo();

            int tradesCount = 0;

            try
            {
                using (StreamReader reader = new StreamReader(pathToTempFile))
                {
                    while (reader.EndOfStream == false)
                    {
                        tradesCount++;
                        string str = reader.ReadLine();

                        if (info.FirstTrade == null)
                        {
                            Trade firstTrade = new Trade();
                            firstTrade.SetTradeFromString(str);
                            info.FirstTrade = firstTrade;
                        }

                        if (reader.EndOfStream == true)
                        {
                            Trade lastTrade = new Trade();
                            lastTrade.SetTradeFromString(str);
                            info.LastTrade = lastTrade;
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            info.TradesCount = tradesCount;

            if (info.FirstTrade != null)
            {
                Status = DataPieStatus.Load;
            }

            return info;
        }

        public List<Trade> LoadTradeDataPieFromTempFile()
        {

            string pathToTempFile = _pathMyTempPieInTfFolder + "\\" + Start.ToString("yyyyMMdd") + "_" + End.ToString("yyyyMMdd") + ".txt";

            if (File.Exists(pathToTempFile) == false)
            {
                return null;
            }

            List<Trade> trades = new List<Trade>();

            try
            {
                using (StreamReader reader = new StreamReader(pathToTempFile))
                {
                    while (reader.EndOfStream == false)
                    {
                        string str = reader.ReadLine();

                        Trade newTrade = new Trade();
                        newTrade.SetTradeFromString(str);
                        trades.Add(newTrade);
                    }
                }
            }
            catch
            {
                // ignore
            }

            if (trades.Count != 0)
            {
                Status = DataPieStatus.Load;
            }

            return trades;
        }

        public void SetNewTradesInPie(List<Trade> trades)
        {
            SaveTradesDataPieInTempFile(trades);
            UpDateStatus();
        }

        private void SaveTradesDataPieInTempFile(List<Trade> trades)
        {
            string pathToTempFile = _pathMyTempPieInTfFolder + "\\" + TempFileName;

            try
            {
                DateTime realEnd = End.AddDays(1);

                using (StreamWriter writer = new StreamWriter(pathToTempFile, false))
                {
                    for (int i = 0; i < trades.Count; i++)
                    {
                        if (trades[i].Time < Start)
                        {
                            continue;
                        }

                        if (trades[i].Time > realEnd)
                        {
                            break;
                        }

                        writer.WriteLine(trades[i].GetSaveString());
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        #endregion
    }

    public enum SecurityLoadStatus
    {
        None,
        Activate,
        Load,
        Loading,
        Error
    }

    public enum DataPieStatus
    {
        None,
        Load,
        InProcess
    }

    public class QuoteChange
    {
        public long Price;
        public long Volume;
    }

    public class TradePieStatusInfo
    {
        public Trade FirstTrade;

        public Trade LastTrade;

        public int TradesCount;

    }

    public class CandlePieStatusInfo
    {
        public Candle FirstCandle;

        public Candle LastCandle;

        public int CandlesCount;
    }
}