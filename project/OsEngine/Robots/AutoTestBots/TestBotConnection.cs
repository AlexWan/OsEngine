/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Threading;

/* Description
TestBot for OsEngine.

Do not turn on - robot for connection testing.
*/

namespace OsEngine.Robots.AutoTestBots
{
    [Bot("TestBotConnection")] //We create an attribute so that we don't write anything in the Boot factory
    public class TestBotConnection : BotPanel
    {
        private BotTabScreener _screener;
        public TestBotConnectionParams testBotConnectionParams;

        public bool TestingIsStart = false;
        public bool TestingIsNeedStop = false;
        private string _server = String.Empty;
        private int CountToReLoadServer;
        private int SecondToReloadServer;
        private int CountTabsToConnectServer;

        public TestBotConnection(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _screener = TabsScreener[0];

            Thread thread = new Thread(UpdatingConnectStatus);
            thread.IsBackground = true;
            thread.Start();

            Description = OsLocalization.Description.DescriptionLabel1;
        }

        // Start testing connector
        public void StartTestingConnector(string ServerName,int countToReloadServer,
            int secondToReloadServer, int countTabsToConnectServer)
        {
            try
            {
                CountToReLoadServer = countToReloadServer;
                SecondToReloadServer = secondToReloadServer;
                CountTabsToConnectServer = countTabsToConnectServer;

                if (TestingIsStart == true)
                {
                    return;
                }

                TestingIsStart = true;

                bool IsNeedDropTest = DropDefaultParamsScreener();

                if (IsNeedDropTest)
                {
                    testBotConnectionParams.DrawingLabeleStatusTest("Stop Test");
                    TestingIsStart = false;
                    TestingIsNeedStop = false;
                    return;
                }

                IServer server = StartServer(ServerName);

                bool IsSuccesLoadSecurity = WaitToLoadSecurities(server);

                if (IsSuccesLoadSecurity == false)
                {
                    testBotConnectionParams.DrawingLabeleStatusTest("Stop Test");
                    TestingIsStart = false;
                    TestingIsNeedStop = false;
                    return;
                }

                SetParamsScreener(server);

                int CountLoadSecurities = ReloadTabs(server);

                IsNeedDropTest = WaitToLoadTabs(CountLoadSecurities);

                if (IsNeedDropTest)
                {
                    testBotConnectionParams.DrawingLabeleStatusTest("Stop Test");
                    TestingIsStart = false;
                    TestingIsNeedStop = false;
                    return;
                }

                DrawingDefault();

                ReloadedServer(server);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.Message, Logging.LogMessageType.Error);
                testBotConnectionParams.DrawingLabeleStatusTest("Error");
            }

            TestingIsStart = false;
            TestingIsNeedStop = false;
        }

        private bool DropDefaultParamsScreener()
        {
            testBotConnectionParams.DrawingLabeleStatusTest("Drop params screener");

            _screener.SecuritiesNames.Clear();
            _screener.PortfolioName = String.Empty;
            bool IsNeedDrop = Wait(2);
            _screener.NeedToReloadTabs = true;

            if (IsNeedDrop)
            {
                return true;
            }
            return false;
        }

        private IServer StartServer(string ServerName)
        {
            testBotConnectionParams.DrawingLabeleStatusTest("Try Start Server");

            _server = ServerName;

            var servers = ServerMaster.GetServers();
            var server = servers.Find(ser => ser.ServerType.ToString().Equals(ServerName));

            server.StartServer();

            return server;
        }

        private bool WaitToLoadSecurities(IServer server)
        {
            testBotConnectionParams.DrawingLabeleStatusTest("Wait To Load Securities");

            while (server.Securities == null)
            {
                if (TestingIsNeedStop == true)
                {
                    TestingIsNeedStop = false;
                    TestingIsStart = false;
                    return false;
                }
            }

            while (server.Securities.Count == 0)
            {
                if (TestingIsNeedStop == true)
                {
                    TestingIsNeedStop = false;
                    TestingIsStart = false;
                    return false;
                }
            }

            bool IsNeedReturn = Wait(2);

            if (IsNeedReturn)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private void SetParamsScreener(IServer server)
        {
            _screener.NeedToReloadTabs = true;
            _screener.ServerType = server.ServerType;
            _screener.PortfolioName = server.Portfolios[0].Number; // add check if portfolio is connected
        }

        private int ReloadTabs(IServer server)
        {
            List<ActivatedSecurity> securities = new List<ActivatedSecurity>();

            for (int i = 0; i < server.Securities.Count; i++)
            {
                if (CountTabsToConnectServer <= i)
                {
                    break;
                }

                securities.Add(new ActivatedSecurity()
                {
                    IsOn = true,
                    SecurityName = server.Securities[i].Name,
                    SecurityClass = server.Securities[i].NameClass
                });
            }

            _screener.SecuritiesNames = securities;

            int CountLoadSecurities = securities.Count;

            return CountLoadSecurities;
        }

        private bool WaitToLoadTabs(int CountLoadSecurities)
        {
            bool IsNeedToReturn = true;

            testBotConnectionParams.DrawingLabeleStatusTest("Wait To Load Tabs");

            IsNeedToReturn = Wait(2);

            while (_screener.Tabs.Count != CountLoadSecurities)
            {
                IsNeedToReturn = Wait(2);
            }

            return IsNeedToReturn;
        }

        private void DrawingDefault()
        {
            if (testBotConnectionParams != null)
            {
                testBotConnectionParams.DrawingProgressBar(0);
                testBotConnectionParams.DrawingRamLabelRam(LabelRAM.Start);
                testBotConnectionParams.DrawingRamLabelRam(LabelRAM.End);
            }
        }

        private void ReloadedServer(IServer server)
        {
            testBotConnectionParams.DrawingLabeleStatusTest("Server restart work");

            for (int i = 0; i < CountToReLoadServer; i++)
            {
                bool IsNeedReturn = Wait(SecondToReloadServer);
                server.StopServer();
                Thread.Sleep(2000);
                server.StartServer();

                if (IsNeedReturn)
                {
                    break;
                }

                if (testBotConnectionParams != null)
                {
                    testBotConnectionParams.DrawingRamLabelRam(LabelRAM.End);
                    testBotConnectionParams.DrawingProgressBar((100 / CountToReLoadServer) * (i + 1));
                }
            }

            testBotConnectionParams.DrawingLabeleStatusTest("Stop Test");
        }

        private bool Wait(long seconds)
        {
            DateTime time = DateTime.Now;

            while (time.AddSeconds(seconds) > DateTime.Now)
            {
                if (TestingIsNeedStop == true)
                {
                    TestingIsNeedStop = false;
                    TestingIsStart = false;
                    return true;
                }
            }

            return false;
        }

        private void UpdatingConnectStatus()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(200);

                    if (testBotConnectionParams != null)
                    {
                        var servers = ServerMaster.GetServers();

                        if(servers == null ||
                            servers.Count == 0)
                        {
                            continue;
                        }

                        var server = servers.Find(ser => ser.ServerType.ToString().Equals(_server));

                        if (server == null || server.Equals(String.Empty))
                        {
                            continue;
                        }

                        bool flag = server.ServerStatus == ServerConnectStatus.Connect;

                        testBotConnectionParams.DrawingRectagle(flag);
                    }
                }
                catch (Exception e)
                {
                    SendNewLogMessage(e.ToString(),Logging.LogMessageType.Error);
                    Thread.Sleep(1000);
                }
            }
        }

        public void CreateServer(string Server)
        {
            var type = ServerMaster.ServersTypes.Find(type => type.ToString().Equals(Server));
            ServerMaster.CreateServer(type, false);
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "TestBotConnection";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
            testBotConnectionParams = new TestBotConnectionParams(this);
            testBotConnectionParams.Show();
        }
    }

    public enum LabelRAM
    {
        Start,
        End
    }
}