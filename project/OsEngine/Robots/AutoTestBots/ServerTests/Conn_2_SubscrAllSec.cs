using OsEngine.Entity;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Threading;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Conn_2_SubscrAllSec : AServerTester
    {
        public string SecClass = "Futures";

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

            while (_startSubscrible == false)
            {
                if (endWaitTime < DateTime.Now)
                {
                    break;
                }

                Thread.Sleep(50);
            }

            if (_startSubscrible == false)
            {
                this.SetNewError(
                  "Error 1. ServerStatus don`t be Connect by the 2 minutes ");
                TestEnded();
                return;
            }

            TestSubscrible();
            TestEnded();
        }

        private void MyServer_LogMessageEvent(string arg1, Logging.LogMessageType arg2)
        {
            if (arg2 != Logging.LogMessageType.Error)
            {
                return;
            }

            this.SetNewError("Error 2. Error in Server: " + arg1);
        }

        private void TestSubscrible()
        {
            List<Security> securitiesAll = Server.Securities;

            List<Security> secs = new List<Security>();

            for (int i = 0; i < securitiesAll.Count; i++)
            {
                if (securitiesAll[i].NameClass == SecClass)
                {
                    secs.Add(securitiesAll[i]);
                }
            }

            if (secs == null || secs.Count == 0)
            {
                this.SetNewError(
                "Error 3. No securities in server! Class: " + SecClass);
                return;
            }

            this.SetNewServiceInfo("Total Securities to subscrible: " + secs.Count);

            DateTime endWaitTime = DateTime.Now.AddMinutes(10);

            for (int i = 0; i < secs.Count; i++)
            {
                if (endWaitTime < DateTime.Now)
                {
                    this.SetNewError(
                      "Error 4. Whait time is over! 10 minutes");
                    break;
                }

                try
                {
                    CandleSeries series = Server.StartThisSecurity(secs[i].Name, new Entity.TimeFrameBuilder(StartProgram.IsOsTrader), secs[i].NameClass);

                    if (series == null)
                    {
                        i--;
                    }
                    else
                    {
                        this.SetNewServiceInfo("Security subscrible: " + secs[i].Name);
                    }
                }
                catch (Exception ex)
                {
                    this.SetNewError("Error 5. Error on subscrible: " + ex.ToString());
                    break;
                }
            }
        }

        private bool _startSubscrible;

        private void MyServer_ConnectStatusChangeEvent(string stats)
        {
            ServerConnectStatus curStatus;

            Enum.TryParse(stats, out curStatus);

            if (curStatus == ServerConnectStatus.Connect)
            {
                _startSubscrible = true;
            }
        }
    }
}