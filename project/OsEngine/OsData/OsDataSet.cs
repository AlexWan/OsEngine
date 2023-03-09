/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.CandleChart;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Finam;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using System.Text;

namespace OsEngine.OsData
{
    /// <summary>
    /// data storage set/сет хранения данных
    /// </summary>
    public class OsDataSet
    {
        // регулируемые настройки

        /// <summary>
        /// unique set name/уникальное имя сета
        /// </summary>
        public string SetName;

        public SettingsToLoadSecurity BaseSettings = new SettingsToLoadSecurity();

        // service/сервис
        /// <summary>
        /// constructor/конструктор
        /// </summary>
        public OsDataSet(string nameUniq)
        {
            SetName = nameUniq;
            Load();

            Task task = new Task(WorkerArea);
            task.Start();

            _painter = new OsDataSetPainter(this);
            _painter.NewLogMessageEvent += SendNewLogMessage;
        }

        /// <summary>
        /// save settings/сохранить настройки
        /// </summary>
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
                        string securitie = SecuritiesLoad[i].GetSaveStr();
                        writer.WriteLine(securitie);
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        /// <summary>
        /// load settings/загрузить настройки
        /// </summary>
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
            catch (Exception)
            {
                // ignored
            }
        }

        /// <summary>
        /// delete settings/удалить настройки
        /// </summary>
        public void Delete()
        {
            StopPaint();

            _painter.Delete();

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
        }

        /// <summary>
        /// show settings window/показать окно настроек
        /// </summary>
        public bool ShowDialog()
        {
            OsDataSetUi ui = new OsDataSetUi(this);
            ui.ShowDialog();

            if (ui.IsSaved)
            {
                _painter.RePaintInterface();
            }

            return ui.IsSaved;
        }

        // control/управление

        /// <summary>
        /// connect new Security/подключить новый инструмент
        /// </summary>
        public void AddNewSecurity()
        {
            if (ServerMaster.GetServers() == null)
            {
                return;
            }

            IServer myServer = ServerMaster.GetServers().Find(server => server.ServerType == BaseSettings.Source);

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
                    record.SetName = SetName;
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
            }
            Save();
        }

        /// <summary>
        /// remove Security/удалить инструмент для скачивания
        /// </summary>
        /// <param name="index">Security index in array/индекс инструмента в массиве</param>
        public void DeleteSecurity(int index)
        {
            if (SecuritiesLoad == null ||
                SecuritiesLoad.Count <= index)
            {
                return;
            }

            SecuritiesLoad[index].Delete();
            SecuritiesLoad[index].NewLogMessageEvent += NewLogMessageEvent;
            SecuritiesLoad.RemoveAt(index);

            Save();
        }

        /// <summary>
        /// изменить статус бумаги. Свёрнута ли или раскрыта
        /// </summary>
        public void ChangeCollapsedStateBySecurity(int index)
        {
            if (SecuritiesLoad == null ||
                SecuritiesLoad.Count <= index)
            {
                return;
            }

            SecuritiesLoad[index].IsCollapced = !SecuritiesLoad[index].IsCollapced;

            Save();
        }

        public decimal PercentLoad()
        {
            decimal max = 0;
            decimal loaded = 0;

            for(int i = 0; SecuritiesLoad != null && i < SecuritiesLoad.Count;i++)
            {
                SecurityToLoad sec = SecuritiesLoad[i];

                max += sec.PiecesToLoad();
                loaded += sec.LoadedPieces();
            }

            if(max == 0)
            {
                return 0;
            }

            if(loaded == 0)
            {
                return 0;
            }

            decimal onePerc = max / 100;

            decimal result = loaded / onePerc;

            return Math.Round(result, 2);
        }

        // прорисовка

        /// <summary>
        /// chart drawing master/мастер прорисовки чарта
        /// </summary>
        private OsDataSetPainter _painter;

        /// <summary>
        /// is the current set selected for drawing/выбран ли текущий сет для прорисовки
        /// </summary>
        private bool _isSelected;

        /// <summary>
        /// enable drawing of this set/включить прорисовку этого сета
        /// </summary>
        public void StartPaint(
            WindowsFormsHost hostChart,
            System.Windows.Controls.Label setName,
            System.Windows.Controls.Label labelTimeStart,
            System.Windows.Controls.Label labelTimeEnd,
            System.Windows.Controls.ProgressBar bar)
        {
            try
            {
                if (_isSelected == true)
                {
                    return;
                }

                _painter.StartPaint(hostChart, setName, labelTimeStart, labelTimeEnd, bar);
                _isSelected = true;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// stop drawing this set/остановить прорисовку этого сета
        /// </summary>
        public void StopPaint()
        {
            if (_isSelected == false)
            {
                return;
            }

            _painter.StopPaint();
            _isSelected = false;
        }

        // сообщения в лог 

        /// <summary>
        /// send a new message to the top/выслать новое сообщение на верх
        /// </summary>
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

        /// <summary>
        /// send new message to log/выслать новое сообщение в лог
        /// </summary>
        public event Action<string, LogMessageType> NewLogMessageEvent;

        // логика загрузки данных

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

                    if (BaseSettings.Regime == DataSetState.Off)
                    {
                        // completely off/полностью выключены
                        continue;
                    }

                    if (BaseSettings.Regime == DataSetState.On)
                    {
                        Process();
                    }
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

            if (_myServer == null)
            {
                TryFindServer();
                return;
            }

            if (_myServer.ServerStatus != ServerConnectStatus.Connect)
            {
                return;
            }

            if (_myServer.LastStartServerTime.AddSeconds(10) > DateTime.Now)
            {
                return;
            }

            for (int i = 0; i < SecuritiesLoad.Count; i++)
            {
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
                if (servers[i].ServerType == BaseSettings.Source)
                {
                    _myServer = servers[i];
                    break;
                }
            }
        }

        // исходящие события

    }

    public class SecurityToLoad
    {
        public SecurityToLoad()
        {


        }

        public string SetName = "";

        public string SecName = "";

        public string SecId = "";

        public string SecClass = "";

        public string SecExchange = "";

        public bool IsCollapced = false;

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
            SettingsToLoadSecurities.TfTickIsOn = param.TfTickIsOn;
            SettingsToLoadSecurities.TfMarketDepthIsOn = param.TfMarketDepthIsOn;

            SettingsToLoadSecurities.Source = param.Source;
            SettingsToLoadSecurities.TimeStart = param.TimeStart;
            SettingsToLoadSecurities.TimeEnd = param.TimeEnd;
            SettingsToLoadSecurities.MarketDepthDepth = param.MarketDepthDepth;
            SettingsToLoadSecurities.NeadToUpdate = param.NeadToUpdate;
            SettingsToLoadSecurities.NeadToLoadDataInServers = param.NeadToLoadDataInServers;

            ActivateLoaders();

        }

        public SettingsToLoadSecurity SettingsToLoadSecurities = new SettingsToLoadSecurity();

        public void Delete()
        {
            string pathSecurityFolder = "Data\\" + SetName + "\\" + SecName.RemoveExcessFromSecurityName();

            if (Directory.Exists(pathSecurityFolder))
            {
                Directory.Delete(pathSecurityFolder,true);
            }
        }

        public void Load(string saveStr)
        {
            string [] saveArray = saveStr.Split('~');

            SecName = saveArray[0];
            SecId = saveArray[1];
            SecClass = saveArray[2];
            IsCollapced = Convert.ToBoolean(saveArray[3]);
            SecExchange = saveArray[4];
            SetName = saveArray[5];
            SettingsToLoadSecurities.Load(saveArray[6]);
            ActivateLoaders();
        }

        public string GetSaveStr()
        {
            string result = SecName+ "~";
            result += SecId + "~";
            result += SecClass + "~";
            result += IsCollapced + "~";
            result += SecExchange + "~";
            result += SetName + "~";
            result += SettingsToLoadSecurities.GetSaveStr();

            return result;
        }

        public int PiecesToLoad()
        {
            int result = 0;

            for(int i = 0;i < SecLoaders.Count;i++)
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
                List<DataPie> pieses = SecLoaders[i].DataPies;

                for(int i2 = 0;i2< pieses.Count;i2++)
                {
                    if(pieses[i2].Status == DataPieStatus.Load)
                    {
                        result += 1;
                    }
                }
            }

            return result;
        }

        // работа по созданию/удалению конечных хранилищ данных

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
                if(SecLoaders[i].TimeFrame == frame)
                {
                    isInArray = true;
                    break;
                }
            }

            if(isInArray == true)
            {
                return;
            }

            if(string.IsNullOrEmpty(SetName)
                || SetName == "Set_")
            {
                return;
            }

            SecurityTfLoader loader = new SecurityTfLoader(SetName, SecName, frame, SecClass, SecId, SecExchange);
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
           for(int i = 0;i < SecLoaders.Count;i++)
            {
                SecLoaders[i].Process(server);
            }
        }

        // обработка исходящих событий

        // сообщения в лог 

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

    }

    public enum DataSetState
    {
        Off,
        On
    }

    public class SecurityTfLoader
    {
        private SecurityTfLoader()
        {

        }

        public SecurityTfLoader(string setName, 
            string securityName, 
            TimeFrame frame,
            string secClass,
            string secId,
            string exchange)
        {
            _setName = setName;
            TimeFrame = frame;
            SecName = securityName;
            SecClass = secClass;
            SecId = secId;
            Exchange = exchange;

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



            _pathMyTempPieInTfFolder = _pathMyTfFolder + "\\Tepm";

            if (!Directory.Exists(_pathMyTempPieInTfFolder))
            {
                Directory.CreateDirectory(_pathMyTempPieInTfFolder);
            }

        }

        public void Delete()
        {
            try
            {
                if (Directory.Exists(_pathMyTfFolder))
                {
                    Directory.Delete(_pathMyTfFolder,true);
                }
            }
            catch
            {
                // ignore
            }
        }

        private string _setName;

        private string _pathSetFolder;

        private string _pathSecurityFolder;

        private string _pathMyTfFolder;

        private string _pathMyTempPieInTfFolder;

        private string _pathMyTxtFile;

        public TimeFrame TimeFrame;

        public string SecName = "";

        public string SecId = "";

        public string SecClass = "";

        public string Exchange = "";

        public DateTime TimeStart;

        public DateTime TimeEnd;

        // предоставления информации о загрузчике

        public DateTime TimeStartInReal;

        public DateTime TimeEndInReal;

        public void CheckTimeInSets()
        {
            DateTime start = DateTime.MaxValue;
            DateTime end = DateTime.MinValue;

            for (int i = 0; DataPies != null && i < DataPies.Count; i++)
            {
                DataPie curPie = DataPies[i];

                DateTime curStart = curPie.StartActualTime();
                DateTime curEnd = curPie.EndActualTime();

                if(curStart != DateTime.MinValue &&
                    curStart < start)
                {
                    start = curStart;
                }
               
                if(curEnd != DateTime.MinValue && 
                    curEnd > end)
                {
                    end = curEnd;
                }
            }

            if(start == DateTime.MaxValue ||
                end == DateTime.MinValue)
            {
                return;
            }

            TimeStartInReal = start;
            TimeEndInReal = end;
        }

        public SecurityLoadStatus Status;

        public decimal PercentLoad()
        {
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

            List<DataPie> pieses = DataPies;

            for (int i2 = 0; i2 < pieses.Count; i2++)
            {
                if (pieses[i2].Status == DataPieStatus.Load)
                {
                    result += 1;
                }
            }

            return result;
        }

        // загрузка данных 

        public void Process(IServer server)
        {
            if (TimeFrame == TimeFrame.Tick)
            {
                ProcessTrades(server);
            }
            //else if (TimeFrame == TimeFrame.MarketDepth)
           // {
          //      ProcessMarketDepth(server);
          //  }
            else
            {
                ProcessCandles(server);
            }
        }

        public List<DataPie> DataPies = new List<DataPie>();

        public void CreateDataPies()
        {
            if (TimeFrame == TimeFrame.MarketDepth)
            {
                Status = SecurityLoadStatus.Loading;
                return;
            }

            // создать пирог запросов

            List<DataPie> newCandleDataPies = new List<DataPie>();

            TimeSpan interval = new TimeSpan(30, 0, 0, 0);

            // трейды, рубим по 3 дня

            if (TimeFrame == TimeFrame.Tick)
            {
                interval = new TimeSpan(3, 0, 0, 0);
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

            // цилк создания кусков данных

            DateTime timeStart = TimeStart;
            DateTime timeNow = timeStart.Add(interval);

            while (true)
            {
                DataPie newPie = new DataPie();
                newPie.Start = timeStart;
                newPie.End = timeNow;

                if(TimeFrame == TimeFrame.Tick)
                {
                    LoadTradeDataPieInTempFile(newPie);
                }
                else if(TimeFrame == TimeFrame.MarketDepth)
                {
                    // делаем ничего
                }
                else
                {
                    LoadCandleDataPieInTempFile(newPie);
                }

                newCandleDataPies.Add(newPie);

                timeStart = timeNow;
                timeNow = timeNow.Add(interval);

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

                if (timeStart == timeNow)
                {
                    break;
                }
            }

            DataPies = newCandleDataPies;

            CheckTimeInSets();

        }

        private string _saveStrCandleCount;

        #region СВЕЧКИ

        private void ProcessCandles(IServer server)
        {
            // пропуем создавать куски данных нужные для выгрузки
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
                if (DataPies[i].Status == DataPieStatus.Load)
                {
                    continue;
                }
                LoadCandleDataPieFromServer(DataPies[i], server);
                if (DataPies[i].Status == DataPieStatus.Load)
                {
                    SaveCandleDataPieInTempFile(DataPies[i]);
                    CheckTimeInSets();
                }
            }

            List<DataPie> dontLoadDataPies = new List<DataPie>();


            for (int i = 0; i < DataPies.Count; i++)
            {
                if (DataPies[i].Status != DataPieStatus.Load)
                {
                    dontLoadDataPies.Add(DataPies[i]);
                }
            }

            for (int i = 0; i < dontLoadDataPies.Count; i++)
            {
                LoadCandleDataPieFromServer(dontLoadDataPies[i], server);

                if (dontLoadDataPies[i].Status == DataPieStatus.Load)
                {
                    SaveCandleDataPieInTempFile(dontLoadDataPies[i]);
                    CheckTimeInSets();
                }
            }

            // сохраняем все куски в файл исходный

            Status = SecurityLoadStatus.Load;

            SaveCandleDataExitFile(DataPies);
        }

        private void LoadCandleDataPieFromServer(DataPie pie, IServer server)
        {
            TimeFrameBuilder timeFrameBuilder = new TimeFrameBuilder();
            timeFrameBuilder.TimeFrame = TimeFrame;

            if(pie.Status == DataPieStatus.Load)
            {
                return;
            }

            string id = SecId;

            if (id == null 
                || id == "")
            {
                id = SecName;
            }

            SendNewLogMessage("Try load sec: " + id + " , tf: " + TimeFrame + 
                " , start: " + pie.Start.ToShortDateString() + " , end: " + pie.End.ToShortDateString(), LogMessageType.System);

            List<Candle> candles = 
                server.GetCandleDataToSecurity(
                    id, SecClass, timeFrameBuilder, 
                    pie.Start,pie.End, pie.Start, false);

            if(candles == null ||
                candles.Count == 0)
            {
                SendNewLogMessage("Error. No candles. sec: " + id + " , tf: " + TimeFrame +
    " , start: " + pie.Start.ToShortDateString() + " , end: " + pie.End.ToString(), LogMessageType.System);

                return;
            }

            pie.Candles = candles;

            SendNewLogMessage("Candles Load Successfully. sec: " + id + " , tf: " + TimeFrame +
" , start: " + pie.Start.ToShortDateString() + " , end: " + pie.End.ToString(), LogMessageType.System);
            pie.Status = DataPieStatus.Load;
        }

        private void LoadCandleDataPieInTempFile(DataPie pie)
        {
            string pathToTempFile = _pathMyTempPieInTfFolder + "\\" + pie.Start.ToString("yyyyMMdd") + "_" + pie.End.ToString("yyyyMMdd") + ".txt";

            if(File.Exists(pathToTempFile) == false)
            {
                return;
            }

            List<Candle> candles = new List<Candle>();

            try
            {
                using (StreamReader reader = new StreamReader(pathToTempFile))
                {
                    while(reader.EndOfStream == false)
                    {
                        string str = reader.ReadLine();

                        Candle newCandle = new Candle();
                        newCandle.SetCandleFromString(str);
                        candles.Add(newCandle);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            if (candles.Count != 0)
            {
                pie.Candles = candles;
                pie.Status = DataPieStatus.Load;
            }
        }

        private void SaveCandleDataPieInTempFile(DataPie pie)
        {
            string pathToTempFile = _pathMyTempPieInTfFolder + "\\" + pie.Start.ToString("yyyyMMdd") + "_" + pie.End.ToString("yyyyMMdd") + ".txt";

            List<Candle> candles = pie.Candles;

            try
            {
                using (StreamWriter writer = new StreamWriter(pathToTempFile,false))
                {
                    for (int i = 0; i < candles.Count; i++)
                    {
                        if (candles[i].TimeStart < TimeStart)
                        {
                            continue;
                        }

                        if (candles[i].TimeStart > TimeEnd)
                        {
                            break;
                        }

                        writer.WriteLine(candles[i].StringToSave);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public List<Candle> GetCandlesAllHistory()
        {
            if(DataPies == null)
            {
                return null;
            }

            List<Candle> extCandles = new List<Candle>();

            for (int i = 0; i < DataPies.Count; i++)
            {
                if (DataPies[i].Candles == null ||
                    DataPies[i].Candles.Count == 0)
                {
                    continue;
                }

                if (extCandles.Count == 0)
                {
                    extCandles.AddRange(DataPies[i].Candles);
                }
                else
                {
                    extCandles = extCandles.Merge(DataPies[i].Candles);
                }
            }

            return extCandles;
        }

        private void SaveCandleDataExitFile(List<DataPie> candleDataPies)
        {
            string curSaveStrCandleCount = "";

            for(int i = 0;i < candleDataPies.Count;i++)
            {
                curSaveStrCandleCount += candleDataPies[i].ObjectCount;
            }

            if(curSaveStrCandleCount == _saveStrCandleCount)
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

            for (int i = 0;i < extCandles.Count;i++)
            {
                builder.Append(extCandles[i].StringToSave );

                if(i+1 != extCandles.Count)
                {
                    builder.Append("\n");
                }
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(_pathMyTxtFile,false))
                {
                    writer.WriteLine(builder.ToString());
                }
            }
            catch (Exception error)
            {
                if (NewLogMessageEvent != null)
                {
                    NewLogMessageEvent(error.ToString(), LogMessageType.Error);
                }
            }

        }

        #endregion

        #region ТРЕЙДЫ

        private void ProcessTrades(IServer server)
        {
            // пропуем создавать куски данных нужные для выгрузки
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
                if (DataPies[i].Status == DataPieStatus.Load)
                {
                    continue;
                }
                LoadTradeDataPieFromServer(DataPies[i], server);
                if (DataPies[i].Status == DataPieStatus.Load)
                {
                    SaveTradeDataPieInTempFile(DataPies[i]);
                    CheckTimeInSets();
                }
            }

            List<DataPie> dontLoadDataPies = new List<DataPie>();


            for (int i = 0; i < DataPies.Count; i++)
            {
                if (DataPies[i].Status != DataPieStatus.Load)
                {
                    dontLoadDataPies.Add(DataPies[i]);
                }
            }

            for (int i = 0; i < dontLoadDataPies.Count; i++)
            {
                LoadTradeDataPieFromServer(dontLoadDataPies[i], server);

                if (dontLoadDataPies[i].Status == DataPieStatus.Load)
                {
                    SaveTradeDataPieInTempFile(dontLoadDataPies[i]);
                    CheckTimeInSets();
                }
            }

            // сохраняем все куски в файл исходный

            Status = SecurityLoadStatus.Load;

            SaveTradeDataExitFile(DataPies);
        }

        private void LoadTradeDataPieFromServer(DataPie pie, IServer server)
        {
            TimeFrameBuilder timeFrameBuilder = new TimeFrameBuilder();
            timeFrameBuilder.TimeFrame = TimeFrame;

            if (pie.Status == DataPieStatus.Load)
            {
                return;
            }

            string id = SecId;

            if (id == null
                || id == "")
            {
                id = SecName;
            }

            SendNewLogMessage("Try load sec: " + id + " , tf: " + TimeFrame +
                " , start: " + pie.Start.ToShortDateString() + " , end: " + pie.End.ToShortDateString(), LogMessageType.System);

            List<Trade> trades =
                server.GetTickDataToSecurity(
                    id, SecClass,pie.Start, pie.End, pie.Start, false);

          /*  tradesIsLoad =
    _myServer.GetTickDataToSecurity(loadSec.Id, loadSec.Class,
    TimeStart, TimeEnd,
    GetActualTimeToTrade("Data\\" + SetName + "\\" + loadSec.Name.RemoveExcessFromSecurityName() + "\\Tick"), NeadToUpdate);
            */


            if (trades == null ||
                trades.Count == 0)
            {
                SendNewLogMessage("Error. No trades. sec: " + id + " , tf: " + TimeFrame +
    " , start: " + pie.Start.ToShortDateString() + " , end: " + pie.End.ToShortDateString(), LogMessageType.System);

                return;
            }

            pie.Trades = trades;

            SendNewLogMessage("Trades Load Successfully. sec: " + id + " , tf: " + TimeFrame +
" , start: " + pie.Start.ToShortDateString() + " , end: " + pie.End.ToShortDateString(), LogMessageType.System);
            pie.Status = DataPieStatus.Load;
        }

        private void LoadTradeDataPieInTempFile(DataPie pie)
        {
            string pathToTempFile = _pathMyTempPieInTfFolder + "\\" + pie.Start.ToString("yyyyMMdd") + "_" + pie.End.ToString("yyyyMMdd") + ".txt";

            if (File.Exists(pathToTempFile) == false)
            {
                return;
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
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            if (trades.Count != 0)
            {
                pie.Trades = trades;
                pie.Status = DataPieStatus.Load;
            }
        }

        private void SaveTradeDataPieInTempFile(DataPie pie)
        {
            string pathToTempFile = _pathMyTempPieInTfFolder + "\\" + pie.Start.ToString("yyyyMMdd") + "_" + pie.End.ToString("yyyyMMdd") + ".txt";

            List<Trade> trades = pie.Trades;

            try
            {
                using (StreamWriter writer = new StreamWriter(pathToTempFile, false))
                {
                    for (int i = 0; i < trades.Count; i++)
                    {
                        if (trades[i].Time < TimeStart)
                        {
                            continue;
                        }

                        if (trades[i].Time > TimeEnd)
                        {
                            break;
                        }

                        writer.WriteLine(trades[i].GetSaveString());
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private List<Trade> GetTradexAllHistory()
        {
            if (DataPies == null)
            {
                return null;
            }

            List<Trade> extTrades = new List<Trade>();

            for (int i = 0; i < DataPies.Count; i++)
            {
                if (DataPies[i].Trades == null ||
                    DataPies[i].Trades.Count == 0)
                {
                    continue;
                }

                if (extTrades.Count == 0)
                {
                    extTrades.AddRange(DataPies[i].Trades);
                }
                else
                {
                    extTrades = extTrades.Merge(DataPies[i].Trades);
                }
            }

            return extTrades;
        }

        private void SaveTradeDataExitFile(List<DataPie> dataPies)
        {
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

            List<Trade> extTrades = GetTradexAllHistory();

            if (extTrades.Count == 0)
            {
                return;
            }

            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < extTrades.Count; i++)
            {
                builder.Append(extTrades[i].GetSaveString());

                if (i + 1 != extTrades.Count)
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
                if (NewLogMessageEvent != null)
                {
                    NewLogMessageEvent(error.ToString(), LogMessageType.Error);
                }
            }

        }

        #endregion

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
            TimeStart = Convert.ToDateTime(saveArray[20]);
            TimeEnd = Convert.ToDateTime(saveArray[21]);
            MarketDepthDepth = Convert.ToInt32(saveArray[22]);
            NeadToUpdate = Convert.ToBoolean(saveArray[23]);
            NeadToLoadDataInServers = Convert.ToBoolean(saveArray[24]);
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
            result += TimeStart + "%";
            result += TimeEnd + "%";
            result += MarketDepthDepth + "%";
            result += NeadToUpdate + "%";
            result += NeadToLoadDataInServers + "%";

            return result;
        }

        public DataSetState Regime;
        public DateTime TimeStart;
        public DateTime TimeEnd;
        public ServerType Source;

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
        public bool TfTickIsOn;
        public bool TfMarketDepthIsOn;
        public int MarketDepthDepth;
        public bool NeadToUpdate;
        public bool NeadToLoadDataInServers;
    }

    public class DataPie
    {
        public DateTime Start;

        public DateTime StartActualTime()
        {
            DateTime start = DateTime.MinValue;

            if (Candles != null && Candles.Count != 0)
            {
                start = Candles[0].TimeStart;
            }

            if (Trades != null && Trades.Count != 0)
            {
                start = Trades[0].Time;
            }

            return start;
        }

        public DateTime End;

        public DateTime EndActualTime()
        {
            DateTime end = DateTime.MinValue;

            if (Candles != null && Candles.Count > 0)
            {
                end = Candles[Candles.Count - 1].TimeStart;
            }

            if (Trades != null && Trades.Count > 0)
            {
                end = Trades[Trades.Count - 1].Time;
            }

            return end;
        }

        public DataPieStatus Status;

        public List<Candle> Candles;

        public List<Trade> Trades;

        public int ObjectCount
        {
            get
            {
                if(Candles == null &&
                    Trades == null)
                {
                    return 0;
                }

                if(Candles != null)
                {
                    return Candles.Count;
                }

                if (Trades != null)
                {
                    return Trades.Count;
                }

                return 0;
            }
        }
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
}
