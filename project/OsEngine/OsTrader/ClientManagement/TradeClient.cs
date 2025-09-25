/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace OsEngine.OsTrader.ClientManagement
{
    public class TradeClient : ILogItem
    {
        public TradeClient()
        {
            
        }

        public Log Log;

        public int Number
        {
            get
            {
                return _number;
            }
            set
            {
                _number = value;

                if(Log == null)
                {
                    Log = new Log("TradeClient" + Number, Entity.StartProgram.IsOsTrader);
                }
            }
        }
        private int _number;

        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if(_name == value)
                {
                    return;
                }

                _name = value.Replace("#","").Replace("&", "");

                if(NameChangeEvent != null)
                {
                    NameChangeEvent();
                }
            }
        }
        private string _name;

        public string Status
        {
            get
            {
                return "Unknown";
            }
        }

        public TradeClientRegime Regime;

        public void Save()
        {
            try
            {
                string dir = Directory.GetCurrentDirectory();
                dir += "\\Engine\\ClientManagement\\";

                if (Directory.Exists(dir) == false)
                {
                    Directory.CreateDirectory(dir);
                }

                using (StreamWriter writer = new StreamWriter(@"Engine\ClientManagement\" + Number + @"TradeClient.txt", false))
                {
                    writer.WriteLine(Number);
                    writer.WriteLine(Name);
                    writer.WriteLine(Regime.ToString());
                    writer.WriteLine("");
                    writer.WriteLine("");
                    writer.WriteLine("");
                    writer.WriteLine(GetSaveStringConnectors());
                    writer.WriteLine(GetSaveStringRobots());


                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }

        }

        public void LoadFromFile(string fileAddress)
        {
            if (!File.Exists(fileAddress))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(fileAddress))
                {
                    Number = Convert.ToInt32(reader.ReadLine());
                    Name = reader.ReadLine();
                    Enum.TryParse(reader.ReadLine(), out Regime);
                    reader.ReadLine();
                    reader.ReadLine();
                    reader.ReadLine();

                    LoadConnectorsFromString(reader.ReadLine());
                    LoadRobotsFromString(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }

        }

        public void Delete()
        {
            try
            {
                if (File.Exists(@"Engine\ClientManagement\" + Number + @"TradeClient.txt") == true)
                {
                    File.Delete(@"Engine\ClientManagement\" + Number + @"TradeClient.txt");
                }
            }
            catch
            {
                // ignore
            }
        }

        public event Action NameChangeEvent;

        #region Connectors

        public List<TradeClientConnector> ConnectorsSettings = new List<TradeClientConnector>();

        private string GetSaveStringConnectors()
        {
            string saveStr = "";

            for(int i = 0;i < ConnectorsSettings.Count;i++)
            {
                saveStr += ConnectorsSettings[i].GetSaveString();

                if(i + 1 != ConnectorsSettings.Count)
                {
                    saveStr += "#";
                }
            }

            return saveStr;
        }

        private void LoadConnectorsFromString(string saveStr)
        {
            string[] connectors = saveStr.Split('#');

            for(int i = 0;i < connectors.Length;i++)
            {
                string currentSaveStr = connectors[i];

                if(string.IsNullOrEmpty(currentSaveStr) == true)
                {
                    continue;
                }

                TradeClientConnector connector = new TradeClientConnector();
                connector.LogMessageEvent += SendNewLogMessage;
                connector.LoadFromString(currentSaveStr);
                ConnectorsSettings.Add(connector);
            }
        }

        public TradeClientConnector AddNewConnector()
        {
            int newClientNumber = 0;

            for (int i = 0; i < ConnectorsSettings.Count; i++)
            {
                if (ConnectorsSettings[i].Number >= newClientNumber)
                {
                    newClientNumber = ConnectorsSettings[i].Number + 1;
                }
            }

            TradeClientConnector newConnector = new TradeClientConnector();
            newConnector.LogMessageEvent += SendNewLogMessage;
            newConnector.Number = newClientNumber;
            ConnectorsSettings.Add(newConnector);

            if (NewConnectorEvent != null)
            {
                NewConnectorEvent(newConnector);
            }

            Save();

            return newConnector;
        }

        public void RemoveConnectorAtNumber(int number)
        {
            TradeClientConnector connectorToRemove = null;

            for (int i = 0; i < ConnectorsSettings.Count; i++)
            {
                if (ConnectorsSettings[i].Number == number)
                {
                    connectorToRemove = ConnectorsSettings[i];
                    connectorToRemove.LogMessageEvent -= SendNewLogMessage;
                    ConnectorsSettings.RemoveAt(i);
                    break;
                }
            }

            if (connectorToRemove != null)
            {
                if (DeleteConnectorEvent != null)
                {
                    DeleteConnectorEvent(connectorToRemove);
                }
            }

            Save();
        }

        public event Action<TradeClientConnector> NewConnectorEvent;

        public event Action<TradeClientConnector> DeleteConnectorEvent;

        #endregion

        #region Robots

        public List<TradeClientRobot> RobotsSettings = new List<TradeClientRobot>();

        private string GetSaveStringRobots()
        {
            string saveStr = "";

            for (int i = 0; i < RobotsSettings.Count; i++)
            {
                saveStr += RobotsSettings[i].GetSaveString();

                if (i + 1 != RobotsSettings.Count)
                {
                    saveStr += "#";
                }
            }

            return saveStr;
        }

        private void LoadRobotsFromString(string saveStr)
        {
            if(saveStr == null)
            {
                return;
            }

            string[] robots = saveStr.Split('#');

            for (int i = 0; i < robots.Length; i++)
            {
                string currentSaveStr = robots[i];

                if (string.IsNullOrEmpty(currentSaveStr) == true)
                {
                    continue;
                }

                TradeClientRobot connector = new TradeClientRobot();
                connector.LogMessageEvent += SendNewLogMessage;
                connector.LoadFromString(currentSaveStr);
                RobotsSettings.Add(connector);
            }
        }

        public TradeClientRobot AddNewRobot()
        {
            int newClientNumber = 0;

            for (int i = 0; i < RobotsSettings.Count; i++)
            {
                if (RobotsSettings[i].Number >= newClientNumber)
                {
                    newClientNumber = RobotsSettings[i].Number + 1;
                }
            }

            TradeClientRobot newRobot = new TradeClientRobot();
            newRobot.LogMessageEvent += SendNewLogMessage;
            newRobot.Number = newClientNumber;
            RobotsSettings.Add(newRobot);

            if (NewRobotEvent != null)
            {
                NewRobotEvent(newRobot);
            }

            Save();

            return newRobot;
        }

        public void RemoveRobotAtNumber(int number)
        {
            TradeClientRobot connectorToRemove = null;

            for (int i = 0; i < RobotsSettings.Count; i++)
            {
                if (RobotsSettings[i].Number == number)
                {
                    connectorToRemove = RobotsSettings[i];
                    connectorToRemove.LogMessageEvent -= SendNewLogMessage;
                    RobotsSettings.RemoveAt(i);
                    break;
                }
            }

            if (connectorToRemove != null)
            {
                if (DeleteRobotEvent != null)
                {
                    DeleteRobotEvent(connectorToRemove);
                }
            }

            Save();
        }

        public event Action<TradeClientRobot> NewRobotEvent;

        public event Action<TradeClientRobot> DeleteRobotEvent;

        #endregion
    }

    public enum TradeClientRegime
    {
        Manual,
        Auto
    }

}
