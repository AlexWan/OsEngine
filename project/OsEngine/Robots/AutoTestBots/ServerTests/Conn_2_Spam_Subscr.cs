using OsEngine.Entity;
using OsEngine.Market.Servers;
using System;
using System.Threading;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Conn_2_Spam_Subscr : AServerTester
    {
        public string SecutiesToSubscrible = "BTCUSDT_BNBUSDT_ETHUSDT_ADAUSDT";

        public override void Process()
        {
            AServer myServer = Server;

            myServer.ConnectStatusChangeEvent += MyServer_ConnectStatusChangeEvent;

            myServer.LogMessageEvent += MyServer_LogMessageEvent;

            if (myServer.ServerStatus == ServerConnectStatus.Disconnect)
            {
                myServer.StartServer();
            }
            else if (myServer.ServerStatus == ServerConnectStatus.Connect)
            {
                myServer.StopServer();
                Thread.Sleep(10000);
                myServer.StartServer();
            }

            DateTime endWaitTime = DateTime.Now.AddMinutes(2);

            while(_startSubscrible == false)
            {
                if(endWaitTime < DateTime.Now)
                {
                    break;
                }

                Thread.Sleep(50);
            }

            if(_startSubscrible == false)
            {
                this.SetNewError(
                  "Error 1. ServerStatus don`t be Connect by the 2 minutes ");
                TestEnded();
                return;
            }

            TestSpamSubscrible();
            TestEnded();
        }

        private void MyServer_LogMessageEvent(string arg1, Logging.LogMessageType arg2)
        {
            if(arg2 != Logging.LogMessageType.Error)
            {
                return;
            }

            this.SetNewError("Error 2. Error in Server: " + arg1);
        }

        private void TestSpamSubscrible()
        {
            string[] secs = SecutiesToSubscrible.Split('_');

            DateTime endWaitTime = DateTime.Now.AddMinutes(3);

            for (int i = 0; i < secs.Length; i++)
            {
                if (endWaitTime < DateTime.Now)
                {
                    this.SetNewError(
                      "Error 3. Whait time is over! 3 minutes");
                    break;
                }

                string curStr = secs[i];

                if(string.IsNullOrEmpty(curStr))
                {
                    continue;
                }

                try
                {
                    CandleSeries series = Server.StartThisSecurity(curStr, new Entity.TimeFrameBuilder(), null);

                    if (series == null)
                    {
                        i--;
                    }
                    else
                    {
                        this.SetNewServiceInfo("Security subscrible: " + curStr);
                    }
                }
                catch(Exception ex)
                {
                    this.SetNewError("Error 4. Error on subscrible: " + ex.ToString());
                    break;
                }
            }
        }

        private bool _startSubscrible;

        private void MyServer_ConnectStatusChangeEvent(string stats)
        {
            ServerConnectStatus curStatus;

            Enum.TryParse(stats, out curStatus);

            if(curStatus == ServerConnectStatus.Connect)
            {
                _startSubscrible = true;
            }
        }
    }
}