using OsEngine.Market.Servers;
using System;
using System.Threading;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Conn_1_Status : AServerTester
    {
        public override void Process()
        {
            AServer myServer = Server;

            myServer.ConnectStatusChangeEvent += MyServer_ConnectStatusChangeEvent;

            myServer.ServerRealization.ConnectEvent += ServerRealization_ConnectEvent;
            myServer.ServerRealization.DisconnectEvent += ServerRealization_DisconnectEvent;
            myServer.ServerRealization.LogMessageEvent += ServerRealization_LogMessageEvent;

            ServerConnectStatus _lastStatus = ServerConnectStatus.Disconnect;

            for(int i = 0;i < 5;i++)
            {
                Thread.Sleep(1000);

                if(i != 0 && _lastStatus == myServer.ServerStatus)
                {
                    this.SetNewError(
                        "Error 1. ServerStatus had not changed 10 seconds after it was started/stopped. Iteration: " + i);
                    TestEnded();
                    return;
                }

                if (myServer.ServerStatus == ServerConnectStatus.Disconnect)
                {
                    _lastStatus = ServerConnectStatus.Disconnect;
                    myServer.StartServer();
                    Thread.Sleep(10000);
                }

                if (myServer.ServerStatus == ServerConnectStatus.Connect)
                {
                    _lastStatus = ServerConnectStatus.Connect;
                    myServer.StopServer();
                    Thread.Sleep(10000);
                }
            }

            TestEnded();
        }

        private void ServerRealization_LogMessageEvent(string message, Logging.LogMessageType logType)
        {
            if(logType == Logging.LogMessageType.Error)
            {
                this.SetNewError("Error 2. Server has ERROR in log " + message);
                return;
            }
        }

        private ServerConnectStatus _lastStatusFromServerRealization;

        private bool _firsStatusFromServerRealizationIsComing = false;

        private ServerConnectStatus _lastStatusFromAserver;

        private bool _firsStatusFromAServerIsComing = false;

        private void ServerRealization_DisconnectEvent()
        {
            if (Server.ServerRealization.ServerStatus != ServerConnectStatus.Disconnect)
            {
                this.SetNewError("Error 3. ServerRealization sent Disconnect status. But it didn't have Disconnect status ");
                return;
            }

            ServerConnectStatus curStatus = ServerConnectStatus.Disconnect;

            if (_firsStatusFromServerRealizationIsComing == false)
            {
                _lastStatusFromServerRealization = curStatus;
                _firsStatusFromServerRealizationIsComing = true;
                return;
            }

            if (_lastStatusFromServerRealization == curStatus)
            {
                this.SetNewError("Error 4. ServerStatus change twice from ServerRealization. Status: " + curStatus);
                return;
            }

            _lastStatusFromServerRealization = curStatus;
        }

        private void ServerRealization_ConnectEvent()
        {
            if (Server.ServerRealization.ServerStatus != ServerConnectStatus.Connect)
            {
                this.SetNewError("Error 5. ServerRealization sent Connect status. But it didn't have Connect status ");
                return;
            }

            ServerConnectStatus curStatus = ServerConnectStatus.Connect;

            if (_firsStatusFromServerRealizationIsComing == false)
            {
                _lastStatusFromServerRealization = curStatus;
                _firsStatusFromServerRealizationIsComing = true;
                return;
            }

            if (_lastStatusFromServerRealization == curStatus)
            {
                this.SetNewError("Error 6. ServerStatus change twice from ServerRealization. Status: " + curStatus);
                return;
            }

            _lastStatusFromServerRealization = curStatus;

        }

        private void MyServer_ConnectStatusChangeEvent(string stats)
        {
            ServerConnectStatus curStatus;

            Enum.TryParse(stats, out curStatus);

            if(_firsStatusFromAServerIsComing == false)
            {
                _lastStatusFromAserver = curStatus;
                _firsStatusFromAServerIsComing = true;
                return;
            }

            if(_lastStatusFromAserver == curStatus)
            {
                this.SetNewError("Error 7. ServerStatus change twice from Aserver. Status: " + curStatus);
                return;
            }

            _lastStatusFromAserver = curStatus;
        }
    }
}
