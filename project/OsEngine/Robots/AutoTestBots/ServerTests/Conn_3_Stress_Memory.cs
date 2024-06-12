using OsEngine.Entity;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Conn_3_Stress_Memory : AServerTester
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

            Test();
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

        private void Test()
        {
            Thread.Sleep(10000);

            Process proc = System.Diagnostics.Process.GetCurrentProcess();
            long startMemory = proc.PrivateMemorySize64;
            this.SetNewServiceInfo("Memory on start: " + startMemory);

            List<long> memoryList = new List<long>();

            ServerConnectStatus _lastStatus = ServerConnectStatus.Disconnect;

            AServer myServer = Server;

            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(1000);

                if (i != 0 && _lastStatus == myServer.ServerStatus)
                {
                    this.SetNewError(
                        "Error 1. ServerStatus don`t change after 10 seconds before it`s start or stop. Iteration: " + i);
                    return;
                }

                if (myServer.ServerStatus == ServerConnectStatus.Disconnect)
                {
                    _lastStatus = ServerConnectStatus.Disconnect;
                    myServer.StartServer();
                    Thread.Sleep(10000);
                    if (SubscribleFirst30Securities())
                    {
                        proc = System.Diagnostics.Process.GetCurrentProcess();
                        memoryList.Add(proc.PrivateMemorySize64);
                        this.SetNewServiceInfo("Iteration " + i + " Memory: " + proc.PrivateMemorySize64);
                    }
                    else
                    {
                        return;
                    }
                }

                if (myServer.ServerStatus == ServerConnectStatus.Connect)
                {
                    _lastStatus = ServerConnectStatus.Connect;
                    myServer.StopServer();
                    Thread.Sleep(10000);
                }
            }

            for (int i = 0; i < memoryList.Count; i++)
            {
                if (memoryList[i] / (startMemory / 100) > 120)
                {
                    this.SetNewError("Error 2. Iteration: " + i + " Memory up more 20 %. Actual: " + memoryList[i]);
                }
            }
        }

        private bool SubscribleFirst30Securities()
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
                return false;
            }

            DateTime endWaitTime = DateTime.Now.AddMinutes(10);

            for (int i = 0; i < 30 && i < secs.Count; i++)
            {
                if (endWaitTime < DateTime.Now)
                {
                    this.SetNewError(
                      "Error 4. Subscrible time is over! 10 minutes");
                    break;
                }

                try
                {
                    CandleSeries series = Server.StartThisSecurity(secs[i].Name, new Entity.TimeFrameBuilder(StartProgram.IsOsTrader), secs[i].NameClass);

                    if (series == null)
                    {
                        i--;
                    }
                }
                catch (Exception ex)
                {
                    this.SetNewError("Error 5. Error on subscrible: " + ex.ToString());
                    return false;
                }
            }

            return true;

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