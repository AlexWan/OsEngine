/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;

namespace OsEngine.OsTrader.ClientManagement
{
    public class TradeClientConnector
    {
        public int Number;

        public ServerType ServerType;

        public string DeployStatus
        {
            get
            {
                if(MyServer == null)
                {
                   return "Collapsed";
                }
                else
                {
                    return "Deployed";
                } 
            }
        }

        public string ServerStatus
        {
            get
            {
                if (MyServer == null)
                {
                    return "No server";
                }
                else
                {
                    return MyServer.ServerStatus.ToString();
                }
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

            for (int i = 0; i < ServerParameters.Count; i++)
            {
                saveStr += ServerParameters[i].GetSaveString() + "&";
            }

            saveStr += "&";

            return saveStr;
        }

        public void LoadFromString(string saveString)
        {
            string[] saveValues = saveString.Split("&");

            Number = Convert.ToInt32(saveValues[0]);
            Enum.TryParse(saveValues[1], out ServerType);

            for (int i = 6; i < saveValues.Length; i++)
            {
                string val = saveValues[i];

                if (val.Contains("*"))
                {
                    TradeClientConnectorParameter newParam = new TradeClientConnectorParameter();
                    newParam.LoadFromString(val);
                    ServerParameters.Add(newParam);
                }
            }
        }

        #region Parameters

        public List<TradeClientConnectorParameter> ServerParameters = new List<TradeClientConnectorParameter>();

        public TradeClientConnectorParameter AddNewParameter()
        {
            int newParameterNumber = 0;

            for (int i = 0; i < ServerParameters.Count; i++)
            {
                if (ServerParameters[i].Number >= newParameterNumber)
                {
                    newParameterNumber = ServerParameters[i].Number + 1;
                }
            }

            TradeClientConnectorParameter newParameter = new TradeClientConnectorParameter();
            newParameter.Number = newParameterNumber;
            ServerParameters.Add(newParameter);

            if (NewParameterCreateEvent != null)
            {
                NewParameterCreateEvent();
            }

            return newParameter;
        }

        public void RemoveParameterAt(int number)
        {
            TradeClientConnectorParameter connectorToRemove = null;

            for (int i = 0; i < ServerParameters.Count; i++)
            {
                if (ServerParameters[i].Number == number)
                {
                    connectorToRemove = ServerParameters[i];
                    ServerParameters.RemoveAt(i);
                    break;
                }
            }

            if (connectorToRemove != null)
            {
                if (ParameterDeleteEvent != null)
                {
                    ParameterDeleteEvent();
                }
            }
        }

        public event Action NewParameterCreateEvent;

        public event Action ParameterDeleteEvent;

        #endregion

        #region Server management

        public AServer MyServer;

        public void Deploy()
        {
            try
            {
                if (MyServer != null)
                {
                    return;
                }
                string error = "";

                MyServer = ServerMaster.GetServerOrCreate(this, out error);

                if (error != "")
                {
                    SendNewLogMessage(error, LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(),LogMessageType.Error);
            }
        }

        public void Collapse()
        {
            try
            {
                if (MyServer != null)
                {
                    ServerMaster.DeleteServer(MyServer.ServerType, MyServer.ServerNum);
                    MyServer = null;
                    return;
                }

                string error = "";

                MyServer = ServerMaster.GetServer(this, out error);

                if (error != "")
                {
                    SendNewLogMessage(error, LogMessageType.Error);
                }

                if (MyServer != null)
                {
                    ServerMaster.DeleteServer(MyServer.ServerType, MyServer.ServerNum);
                    MyServer = null;
                    return;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public void ShowGui()
        {
            try
            {
                string error = "";

                MyServer = ServerMaster.GetServerOrCreate(this, out error);

                if (error != "")
                {
                    SendNewLogMessage(error, LogMessageType.Error);
                }

                MyServer.ShowDialog(MyServer.ServerNum);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public void Connect()
        {
            try
            {
                if (MyServer != null)
                {
                    MyServer.StartServer();
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public void Disconnect()
        {
            try
            {
                if (MyServer != null)
                {
                    MyServer.StopServer();
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

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

    public class TradeClientConnectorParameter
    {
        public int Number;

        public string ParameterName;

        public string ParameterValue;

        public string GetSaveString()
        {
            string saveStr = "";

            saveStr += Number + "*";
            saveStr += ParameterName + "*";
            saveStr += ParameterValue;

            return saveStr;
        }

        public void LoadFromString(string saveString)
        {
            string[] saveValues = saveString.Split("*");

            Number = Convert.ToInt32(saveValues[0]);
            ParameterName = saveValues[1];
            ParameterValue = saveValues[2];
        }
    }

}
