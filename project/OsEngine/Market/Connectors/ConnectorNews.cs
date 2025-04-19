/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using System.Threading.Tasks;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.Language;

namespace OsEngine.Market.Connectors
{
    public class ConnectorNews
    {
        #region Service code

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="name"> bot name</param>
        /// <param name="startProgram"> program that created the bot which created this connection</param>
        public ConnectorNews(string name, StartProgram startProgram)
        {
            _name = name;
            StartProgram = startProgram;
            ServerType = ServerType.None;
            _canSave = true;
            Load();
            _taskIsDead = false;
            Task.Run(Subscribe);
        }

        /// <summary>
        /// program that created the bot which created this connection
        /// </summary>
        public StartProgram StartProgram;

        /// <summary>
        /// shows whether it is possible to save settings
        /// </summary>
        private bool _canSave;

        /// <summary>
        /// upload settings
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + _name + @"ConnectorNews.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + _name + @"ConnectorNews.txt"))
                {
                    Enum.TryParse(reader.ReadLine(), true, out _serverType);
                    _eventsIsOn = Convert.ToBoolean(reader.ReadLine());
                    _countNewsToSave = Convert.ToInt32(reader.ReadLine());
                    _serverFullName = reader.ReadLine();

                    reader.Close();
                }
            }
            catch
            {
                _eventsIsOn = true;
                _countNewsToSave = 100;
            }
        }

        /// <summary>
        /// save settings in file
        /// </summary>
        public void Save()
        {
            if (_canSave == false)
            {
                return;
            }
            if (StartProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _name + @"ConnectorNews.txt", false))
                {
                    writer.WriteLine(_serverType);
                    writer.WriteLine(_eventsIsOn);
                    writer.WriteLine(_countNewsToSave);
                    writer.WriteLine(_serverFullName);

                    writer.Close();
                }
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// delete object and clear memory
        /// </summary>
        public void Delete()
        {
            _needToStopThread = true;

            if (StartProgram != StartProgram.IsOsOptimizer)
            {

                try
                {
                    if (File.Exists(@"Engine\" + _name + @"ConnectorNews.txt"))
                    {
                        File.Delete(@"Engine\" + _name + @"ConnectorNews.txt");
                    }
                }
                catch
                {
                    // ignore
                }
            }

            if (_myServer != null)
            {
                _myServer.NeedToReconnectEvent -= _myServer_NeedToReconnectEvent;
                _myServer.NewsEvent -= Server_NewsEvent;
                _myServer = null;
            }

            if (NewsArray != null)
            {
                NewsArray.Clear();
                NewsArray = null;
            }

            if(_ui != null)
            {
                _ui.Close();
            }
        }

        /// <summary>
        /// show settings window
        /// </summary>
        public void ShowDialog()
        {
            try
            {
                if (ServerMaster.GetServers() == null ||
                    ServerMaster.GetServers().Count == 0)
                {
                    SendNewLogMessage(OsLocalization.Market.Message1, LogMessageType.Error);
                    return;
                }

                if(_ui == null)
                {
                    _ui = new ConnectorNewsUi(this);
                    _ui.LogMessageEvent += SendNewLogMessage;
                    _ui.Closed += _ui_Closed;
                    _ui.Show();
                }
                else
                {
                    _ui.Activate();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _ui_Closed(object sender, EventArgs e)
        {
            try
            {
                _ui.Closed -= _ui_Closed;
                _ui.LogMessageEvent -= SendNewLogMessage;
                _ui = null;
            }
            catch 
            { 
              // ignore
            }

        }

        private ConnectorNewsUi _ui;

        #endregion

        #region Settings and properties

        public string UniqueName
        {
            get { return _name; }
        }
        private string _name;

        public IServer MyServer
        {
            get { return _myServer; }
        }
        private IServer _myServer;

        public ServerType ServerType
        {
            get
            {
                return _serverType;
            }
            set
            {
                if (_serverType == value)
                {
                    return;
                }
                _serverType = value;
                Save();
            }
        }
        private ServerType _serverType;

        public string ServerFullName
        {
            get
            {
                if(_serverFullName == null)
                {
                    _serverFullName = _serverType.ToString();
                }

                return _serverFullName;
            }
            set
            {
                if (_serverFullName == value)
                {
                    return;
                }
                _serverFullName = value;
                Save();
            }
        }
        private string _serverFullName;

        public bool EventsIsOn
        {
            get
            {
                return _eventsIsOn;
            }
            set
            {
                if (_eventsIsOn == value)
                {
                    return;
                }
                _eventsIsOn = value;
                Save();
            }
        }
        private bool _eventsIsOn = true;

        public int CountNewsToSave
        {
            get
            {
                return _countNewsToSave;
            }
            set
            {
                if (_countNewsToSave == value)
                {
                    return;
                }
                _countNewsToSave = value;
                Save();
            }
        }
        private int _countNewsToSave = 100;

        #endregion

        #region Data subscription

        private DateTime _lastReconnectTime;

        private object _reconnectLocker = new object();

        public int ServerUid;

        private void Reconnect()
        {
            try
            {
                lock (_reconnectLocker)
                {
                    if (_lastReconnectTime.AddSeconds(1) > DateTime.Now)
                    {
                        return;
                    }
                    _lastReconnectTime = DateTime.Now;
                }

                if (_taskIsDead == true)
                {
                    _taskIsDead = false;
                    Task.Run(Subscribe);
                }
            }

            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private bool _lastHardReconnectOver = true;

        public void ReconnectHard()
        {
            if (_lastHardReconnectOver == false)
            {
                return;
            }

            _lastHardReconnectOver = false;

            Reconnect();

            _lastHardReconnectOver = true;
        }

        private bool _taskIsDead;

        private bool _needToStopThread;

        private object _myServerLocker = new object();

        private static int _aliveTasks = 0;

        private static string _aliveTasksArrayLocker = "aliveTasksArrayLocker";

        private bool _alreadyCheckedInAliveTasksArray = false;

        private static int _tasksCountOnSubscribe = 0;

        private static string _tasksCountLocker = "_tasksCountOnLocker";

        private async void Subscribe()
        {
            try
            {
                _alreadyCheckedInAliveTasksArray = false;

                while (true)
                {
                    if (ServerType == ServerType.Optimizer)
                    {
                        await Task.Delay(1);
                    }
                    else if (ServerType == ServerType.Tester)
                    {
                        await Task.Delay(10);
                    }
                    else
                    {
                        int millisecondsToDelay = _aliveTasks * 5;

                        lock (_aliveTasksArrayLocker)
                        {
                            if (_alreadyCheckedInAliveTasksArray == false)
                            {
                                _aliveTasks++;
                                _alreadyCheckedInAliveTasksArray = true;
                            }

                            if (millisecondsToDelay < 500)
                            {
                                millisecondsToDelay = 500;
                            }
                        }

                        await Task.Delay(millisecondsToDelay);
                    }

                    if (_needToStopThread)
                    {
                        lock (_aliveTasksArrayLocker)
                        {
                            if (_aliveTasks > 0)
                            {
                                _aliveTasks--;
                            }
                        }
                        return;
                    }

                    if (ServerType == ServerType.None)
                    {
                        continue;
                    }

                    List<IServer> servers = ServerMaster.GetServers();

                    if (servers == null)
                    {
                        if (ServerType != ServerType.None)
                        {
                            ServerMaster.SetServerToAutoConnection(ServerType, ServerFullName);
                        }
                        continue;
                    }

                    try
                    {
                        if (ServerType == ServerType.Optimizer &&
                            this.ServerUid != 0)
                        {
                            for (int i = 0; i < servers.Count; i++)
                            {
                                if (servers[i] == null)
                                {
                                    servers.RemoveAt(i);
                                    i--;
                                    continue;
                                }
                                if (servers[i].ServerType == ServerType.Optimizer &&
                                    ((OptimizerServer)servers[i]).NumberServer == this.ServerUid)
                                {
                                    _myServer = servers[i];
                                    break;
                                }
                            }
                        }
                        else
                        {
                            _myServer = servers.Find(server => 
                            server.ServerType == ServerType
                            && server.ServerNameAndPrefix == ServerFullName);
                        }
                    }
                    catch
                    {
                        // ignore
                        continue;
                    }

                    if (_myServer == null)
                    {
                        if (ServerType != ServerType.None)
                        {
                            ServerMaster.SetServerToAutoConnection(ServerType, ServerFullName);
                        }
                        continue;
                    }

                    ServerConnectStatus stat = _myServer.ServerStatus;

                    if (stat != ServerConnectStatus.Connect)
                    {
                        continue;
                    }

                    SubscribeOnServer(_myServer);

                    bool result = false;


                    while (result == false)
                    {
                        if (_needToStopThread)
                        {
                            lock (_aliveTasksArrayLocker)
                            {
                                if (_aliveTasks > 0)
                                {
                                    _aliveTasks--;
                                }
                            }
                            return;
                        }
                        if (_myServer == null)
                        {
                            continue;
                        }

                        if (StartProgram == StartProgram.IsOsTrader ||
                            StartProgram == StartProgram.IsOsData)
                        {
                            int millisecondsToDelay = _aliveTasks * 5;

                            if (millisecondsToDelay < 500)
                            {
                                millisecondsToDelay = 500;
                            }

                            await Task.Delay(millisecondsToDelay);
                        }
                        else
                        {
                            await Task.Delay(1);
                        }

                        if (_tasksCountOnSubscribe > 20)
                        {
                            continue;
                        }

                        lock (_tasksCountLocker)
                        {
                            _tasksCountOnSubscribe++;
                        }

                        lock (_myServerLocker)
                        {
                            if (_myServer != null)
                            {
                                result = _myServer.SubscribeNews();
                            }
                        }

                        lock (_tasksCountLocker)
                        {
                            _tasksCountOnSubscribe--;
                        }

                        OptimizerServer myOptimizerServer = _myServer as OptimizerServer;
                        if (myOptimizerServer != null &&
                            myOptimizerServer.ServerType == ServerType.Optimizer &&
                            myOptimizerServer.NumberServer != ServerUid)
                        {
                            for (int i = 0; i < servers.Count; i++)
                            {
                                if (servers[i].ServerType == ServerType.Optimizer &&
                                    ((OptimizerServer)servers[i]).NumberServer == this.ServerUid)
                                {
                                    UnSubscribeOnServer(_myServer);
                                    _myServer = servers[i];
                                    SubscribeOnServer(_myServer);
                                    break;
                                }
                            }
                        }
                    }

                    _taskIsDead = true;

                    if (SubscribeEvent != null)
                    {
                        SubscribeEvent();
                    }

                    lock (_aliveTasksArrayLocker)
                    {
                        if (_aliveTasks > 0)
                        {
                            _aliveTasks--;
                        }
                    }

                    return;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void UnSubscribeOnServer(IServer server)
        {
            server.NeedToReconnectEvent -= _myServer_NeedToReconnectEvent;
            server.NewsEvent -= Server_NewsEvent;
        }

        private void SubscribeOnServer(IServer server)
        {
            server.NewsEvent -= Server_NewsEvent;
            server.NeedToReconnectEvent -= _myServer_NeedToReconnectEvent;

            server.NewsEvent += Server_NewsEvent;
            server.NeedToReconnectEvent += _myServer_NeedToReconnectEvent;
        }

        private void _myServer_NeedToReconnectEvent()
        {
            Reconnect();
        }

        #endregion

        #region News and events

        private void Server_NewsEvent(News news)
        {
            if(_eventsIsOn == false)
            {
                return;
            }

            if(_countNewsToSave > 0)
            {
                NewsArray.Add(news);

                if (NewsArray.Count > _countNewsToSave)
                {
                    NewsArray.RemoveAt(0);
                }
            }

            if(NewsEvent != null)
            {
                NewsEvent(news);
            }
        }

        public List<News> NewsArray = new List<News>();

        /// <summary>
        /// the news has come out
        /// </summary>
        public event Action<News> NewsEvent;

        public event Action SubscribeEvent;

        #endregion

        #region Log

        /// <summary>
        /// send new message to up
        /// </summary>
        public void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            { // if nobody is subscribed to us and there is an error in the log / если на нас никто не подписан и в логе ошибка
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// outgoing log message
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}