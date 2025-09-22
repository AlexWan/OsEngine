/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsTrader.Grids;
using System;
using System.Collections.Generic;
using System.IO;


namespace OsEngine.OsTrader.ClientManagement
{
    public class TradeClient
    {
        public TradeClient()
        {


        }

        public int Number;

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
                return "Ok";
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

        public List<TradeClientRobots> ClientRobotsSettings = new List<TradeClientRobots>();

        public event Action NameChangeEvent;

        #region Connectors

        public List<TradeClientConnector> ClientConnectorsSettings = new List<TradeClientConnector>();

        private string GetSaveStringConnectors()
        {
            string saveStr = "";

            for(int i = 0;i < ClientConnectorsSettings.Count;i++)
            {
                saveStr += ClientConnectorsSettings[i].GetSaveString();

                if(i + 1 != ClientConnectorsSettings.Count)
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
                connector.LoadFromString(currentSaveStr);
                ClientConnectorsSettings.Add(connector);
            }
        }

        public TradeClientConnector AddNewConnector()
        {
            int newClientNumber = 0;

            for (int i = 0; i < ClientConnectorsSettings.Count; i++)
            {
                if (ClientConnectorsSettings[i].Number >= newClientNumber)
                {
                    newClientNumber = ClientConnectorsSettings[i].Number + 1;
                }
            }

            TradeClientConnector newClient = new TradeClientConnector();
            newClient.Number = newClientNumber;
            ClientConnectorsSettings.Add(newClient);

            if (NewConnectorEvent != null)
            {
                NewConnectorEvent(newClient);
            }

            Save();

            return newClient;
        }

        public void RemoveConnectorAtNumber(int number)
        {
            TradeClientConnector connectorToRemove = null;

            for (int i = 0; i < ClientConnectorsSettings.Count; i++)
            {
                if (ClientConnectorsSettings[i].Number == number)
                {
                    connectorToRemove = ClientConnectorsSettings[i];
                    ClientConnectorsSettings.RemoveAt(i);
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

        #region Log

        public void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                ServerMaster.SendNewLogMessage(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

    }

    public enum TradeClientRegime
    {
        Manual,
        Auto
    }

    public class TradeClientConnector
    {
        public int Number;

        public ServerType ServerType;

        public string DeployStatus
        {
            get
            {
                return "Deployed";
            }
        }

        public string ServerStatus
        {
            get
            {
                return "Disconnect";
            }
        }

        public string GetSaveString()
        {
            string saveStr = "";

            saveStr += Number + "&";
            saveStr += ServerType + "&";
            saveStr += "&";
            saveStr += "&";
            saveStr += "&";
            saveStr += "&";
            saveStr += "&";

            return saveStr;
        }

        public void LoadFromString(string saveString)
        {
            string[] saveValues = saveString.Split("&");

            Number = Convert.ToInt32(saveValues[0]);
            Enum.TryParse(saveValues[1], out ServerType);

        }

    }

    public class TradeClientRobots
    {
        public int Number;

        public bool IsOn;



        public List<TradeClientRobotsParameter> Parameters;

        public List<TradeClientSourceSettings> SourceSettings;




    }

    public class TradeClientRobotsParameter
    {
        public string GetSaveString()
        {
            return "";
        }


    }

    public class TradeClientSourceSettings
    {




    }


}
