using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using System.Threading;
using OsEngine.OsTrader.Panels.Attributes;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    [Bot("WServerTester")]
    public class WServerTester : BotPanel
    {
        #region Settings and Params

        public WServerTester(string name, StartProgram startProgram) : base(name, startProgram)
        {
            StrategyParameterButton buttonSecTests = CreateParameterButton("Start test sec", "V1");
            buttonSecTests.UserClickOnButtonEvent += ButtonSecTests_UserClickOnButtonEvent;

            StrategyParameterButton buttonMarketDepth = CreateParameterButton("Start test md", "V2");
            buttonMarketDepth.UserClickOnButtonEvent += ButtonMarketDepth_UserClickOnButtonEvent;
            V2_SecurityName = CreateParameter("Md tester Sec names", "ADAUSDT_BNBUSDT_ETHUSDT_BTCUSDT", "V2");
            V2_ClassCode = CreateParameter("Md tester Class Code", "Futures", "V2");
            V2_MarketDepthMinutesToTest = CreateParameter("Md tester work time minutes", 5, 5, 5, 1, "V2");

            StrategyParameterButton buttonDataTest1 = CreateParameterButton("Start test data 1", "D1");
            buttonDataTest1.UserClickOnButtonEvent += ButtonDataTest1_UserClickOnButtonEvent;
            D1_SecurityName = CreateParameter("Sec name data test 1", "ADAUSDT","D1");
            D1_SecurityClass = CreateParameter("Sec class data test 1", "Futures", "D1");

            StrategyParameterButton buttonDataTest2 = CreateParameterButton("Start test data 2", "D2");
            buttonDataTest2.UserClickOnButtonEvent += ButtonDataTest2_UserClickOnButtonEvent;
            D2_SecurityName = CreateParameter("Sec name data test 2", "ADAUSDT", "D2");
            D2_SecurityClass = CreateParameter("Sec class data test 2", "Futures", "D2");

            StrategyParameterButton buttonDataTest3 = CreateParameterButton("Start test data 3", "D3");
            buttonDataTest3.UserClickOnButtonEvent += ButtonDataTest3_UserClickOnButtonEvent1;
            D3_SecurityName = CreateParameter("Sec name data test 3", "ADAUSDT", "D3");
            D3_SecurityClass = CreateParameter("Sec class data test 3", "Futures", "D3");

            StrategyParameterButton buttonDataTest4 = CreateParameterButton("Start test data 4", "D4");
            buttonDataTest4.UserClickOnButtonEvent += ButtonDataTest4_UserClickOnButtonEvent;
            D4_SecuritiesNames = CreateParameter("Sec name data test 4", "ADAUSDT_BNBUSDT_ETHUSDT_BTCUSDT", "D4");
            D4_SecurityClass = CreateParameter("Sec class data test 4", "Futures", "D4");

            StrategyParameterButton buttonDataTest5 = CreateParameterButton("Start test data 5", "D5");
            buttonDataTest5.UserClickOnButtonEvent += ButtonDataTest5_UserClickOnButtonEvent;
            D5_SecuritiesNames = CreateParameter("Sec name data test 5", "ADAUSDT_BNBUSDT_ETHUSDT", "D5");

            StrategyParameterButton buttonConnectionTest1 = CreateParameterButton("Start test connection 1", "C1");
            buttonConnectionTest1.UserClickOnButtonEvent += ButtonConnectionTest1_UserClickOnButtonEvent;

            StrategyParameterButton buttonConnectionTest2 = CreateParameterButton("Start test connection 2", "C2");
            buttonConnectionTest2.UserClickOnButtonEvent += ButtonConnectionTest2_UserClickOnButtonEvent;
            C2_SecuritiesNames = CreateParameter("Sec name test spam secs", "ADAUSDT_BNBUSDT_ETHUSDT", "C2");

            StrategyParameterButton buttonConnectionTest3 = CreateParameterButton("Start test connection 3", "C3");
            buttonConnectionTest3.UserClickOnButtonEvent += ButtonConnectionTest3_UserClickOnButtonEvent;

            StrategyParameterButton buttonConnectionTest4 = CreateParameterButton("Start test connection 4", "C4");
            buttonConnectionTest4.UserClickOnButtonEvent += ButtonConnectionTest4_UserClickOnButtonEvent;

            StrategyParameterButton buttonConnectionTest5 = CreateParameterButton("Start test connection 5", "C5");
            buttonConnectionTest5.UserClickOnButtonEvent += ButtonConnectionTest5_UserClickOnButtonEvent;
            C5_SecuritiesNames = CreateParameter("Sec name connection test 5", "ADAUSDT_BNBUSDT_ETHUSDT_BTCUSDT", "C5");

            StrategyParameterButton buttonOrdersTest1 = CreateParameterButton("Start test orders 1", "O1");
            buttonOrdersTest1.UserClickOnButtonEvent += ButtonOrdersTest1_UserClickOnButtonEvent;
            O1_PortfolioName = CreateParameter("Portfolio. orders test 1", "BinanceFutures", "O1");
            O1_SecurityName = CreateParameter("Sec name. orders test 1", "ETHUSDT", "O1");
            O1_VolumeLess = CreateParameter("Volume. orders test 1", 0.01m, 1, 1, 1, "O1");
            O1_VolumeMax = CreateParameter("Volume. More than needed 1", 5000m, 1, 1, 1, "O1");

            StrategyParameterButton buttonOrdersTest2 = CreateParameterButton("Start test orders 2", "O2");
            buttonOrdersTest2.UserClickOnButtonEvent += ButtonOrdersTest2_UserClickOnButtonEvent;
            O2_PortfolioName = CreateParameter("Portfolio. orders test 2", "BinanceFutures", "O2");
            O2_SecurityName = CreateParameter("Sec name. orders test 2", "ETHUSDT", "O2");
            O2_Volume = CreateParameter("Volume. orders test 2", 0.01m, 1, 1, 1, "O2");

            StrategyParameterButton buttonOrdersTest3 = CreateParameterButton("Start test orders 3", "O3");
            buttonOrdersTest3.UserClickOnButtonEvent += ButtonOrdersTest3_UserClickOnButtonEvent;
            O3_PortfolioName = CreateParameter("Portfolio. orders test 3", "BinanceFutures", "O3");
            O3_SecurityName = CreateParameter("Sec name. orders test 3", "ETHUSDT", "O3");
            O3_Volume = CreateParameter("Volume. orders test 3", 0.01m, 1, 1, 1, "O3");

            StrategyParameterButton buttonOrdersTest4 = CreateParameterButton("Start test orders 4", "O4");
            buttonOrdersTest4.UserClickOnButtonEvent += ButtonOrdersTest4_UserClickOnButtonEvent;
            O4_PortfolioName = CreateParameter("Portfolio. orders test 4", "BinanceFutures", "O4");
            O4_SecurityName = CreateParameter("Sec name. orders test 4", "ETHUSDT", "O4");
            O4_Volume = CreateParameter("Volume. orders test 4", 0.01m, 1, 1, 1, "O4");
            O4_CountOrders = CreateParameter("Count orders test 4", 5, 1, 1, 1, "O4");

            StrategyParameterButton buttonOrdersTest5 = CreateParameterButton("Start test orders 5", "O5");
            buttonOrdersTest5.UserClickOnButtonEvent += ButtonOrdersTest5_UserClickOnButtonEvent;
            O5_PortfolioName = CreateParameter("Portfolio. orders test 5", "BinanceFutures", "O5");
            O5_SecurityName = CreateParameter("Sec name. orders test 5", "ETHUSDT", "O5");
            O5_Volume = CreateParameter("Volume. orders test 5", 0.01m, 1, 1, 1, "O5");
            O5_CountOrders = CreateParameter("Count orders test 5", 5, 1, 1, 1, "O5");

            StrategyParameterButton buttonPortfolioTest1 = CreateParameterButton("Start test portfolio 1", "P1");
            buttonPortfolioTest1.UserClickOnButtonEvent += ButtonPortfolioTest1_UserClickOnButtonEvent;
            P1_PortfolioName = CreateParameter("Portfolio.  portfolio 1", "BinanceFutures", "P1");
            P1_SecurityName = CreateParameter("Sec name.  portfolio 1", "ETHUSDT", "P1");
            P1_AssetInPortfolioName = CreateParameter("Asset In portfolio 1", "ETH", "P1");
            P1_Volume = CreateParameter("Volume.  portfolio 1", 0.01m, 1, 1, 1, "P1");

        }

        public override string GetNameStrategyType()
        {
            return "WServerTester";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        StrategyParameterString V2_SecurityName;
        StrategyParameterString V2_ClassCode;
        StrategyParameterInt V2_MarketDepthMinutesToTest;


        StrategyParameterString D1_SecurityName;
        StrategyParameterString D1_SecurityClass;

        StrategyParameterString D2_SecurityName;
        StrategyParameterString D2_SecurityClass;

        StrategyParameterString D3_SecurityName;
        StrategyParameterString D3_SecurityClass;

        StrategyParameterString D4_SecuritiesNames;
        StrategyParameterString D4_SecurityClass;

        StrategyParameterString D5_SecuritiesNames;

        StrategyParameterString C2_SecuritiesNames;

        StrategyParameterString C5_SecuritiesNames;

        StrategyParameterString O1_SecurityName;
        StrategyParameterString O1_PortfolioName;
        StrategyParameterDecimal O1_VolumeLess;
        StrategyParameterDecimal O1_VolumeMax;

        StrategyParameterString O2_SecurityName;
        StrategyParameterString O2_PortfolioName;
        StrategyParameterDecimal O2_Volume;

        StrategyParameterString O3_SecurityName;
        StrategyParameterString O3_PortfolioName;
        StrategyParameterDecimal O3_Volume;

        StrategyParameterString O4_SecurityName;
        StrategyParameterString O4_PortfolioName;
        StrategyParameterDecimal O4_Volume;
        StrategyParameterInt O4_CountOrders;

        StrategyParameterString O5_SecurityName;
        StrategyParameterString O5_PortfolioName;
        StrategyParameterDecimal O5_Volume;
        StrategyParameterInt O5_CountOrders;

        StrategyParameterString P1_SecurityName;
        StrategyParameterString P1_AssetInPortfolioName;
        StrategyParameterString P1_PortfolioName;
        StrategyParameterDecimal P1_Volume;

        #endregion

        #region Start Test By Buttons

        private void ButtonDataTest1_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Data_1;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonDataTest2_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Data_2;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonDataTest3_UserClickOnButtonEvent1()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Data_3;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonDataTest4_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Data_4;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonDataTest5_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Data_5;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonMarketDepth_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Var_2_MarketDepth;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonSecTests_UserClickOnButtonEvent()
        {
            if(_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Var_1_Securities;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonConnectionTest1_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Conn_1;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonConnectionTest2_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Conn_2;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonConnectionTest3_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Conn_3;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonConnectionTest4_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Conn_4;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonConnectionTest5_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Conn_5;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonOrdersTest1_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Order_1;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonOrdersTest2_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Order_2;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonOrdersTest3_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Order_3;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonOrdersTest4_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Order_4;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonOrdersTest5_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Order_5;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonPortfolioTest1_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Portfolio_1;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private ServerTestType CurTestType;

        #endregion

        #region Test work thread

        private void WorkerThreadArea()
        {
            _threadIsWork = true;

            List<IServer> servers = ServerMaster.GetServers();

            if(servers == null ||
                servers.Count == 0)
            {
                _threadIsWork = false;
                SendNewLogMessage("No Servers Found", LogMessageType.Error);
                return;
            }

            for(int i = 0; servers != null && i < servers.Count;i++)
            {
                string servType = servers[i].GetType().BaseType.ToString();

                if (servType.EndsWith("AServer") == false) 
                {
                    continue;
                }

                if(CurTestType == ServerTestType.Var_1_Securities)
                {
                    Var_1_Securities tester = new Var_1_Securities();
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " +  servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
                else if(CurTestType == ServerTestType.Var_2_MarketDepth)
                {
                    Var_2_MarketDepth tester = new Var_2_MarketDepth();
                    tester.MinutesToTest = V2_MarketDepthMinutesToTest.ValueInt;
                    tester.SecNames = V2_SecurityName.ValueString;
                    tester.SecClassCode = V2_ClassCode.ValueString;
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
                else if (CurTestType == ServerTestType.Data_1)
                {
                    Data_1_Integrity tester = new Data_1_Integrity();
                    tester.SecName = D1_SecurityName.ValueString;
                    tester.SecClass = D1_SecurityClass.ValueString;
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
                else if (CurTestType == ServerTestType.Data_2)
                {
                    Data_2_Validation_Candles tester = new Data_2_Validation_Candles();
                    tester.SecName = D2_SecurityName.ValueString;
                    tester.SecClass = D2_SecurityClass.ValueString;
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
                else if (CurTestType == ServerTestType.Data_3)
                {
                    Data_3_Validation_Trades tester = new Data_3_Validation_Trades();
                    tester.SecName = D3_SecurityName.ValueString;
                    tester.SecClass = D3_SecurityClass.ValueString;
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
                else if (CurTestType == ServerTestType.Data_4)
                {
                    Data_4_Stress_Candles tester = new Data_4_Stress_Candles();
                    tester.SecNames = D4_SecuritiesNames.ValueString;
                    tester.SecClass = D4_SecurityClass.ValueString;
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
                else if (CurTestType == ServerTestType.Data_5)
                {
                    Data_5_Stress_Trades tester = new Data_5_Stress_Trades();
                    tester.SecNames = D5_SecuritiesNames.ValueString;
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
                else if (CurTestType == ServerTestType.Conn_1)
                {
                    Conn_1_Status tester = new Conn_1_Status();
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
                else if (CurTestType == ServerTestType.Conn_2)
                {
                    Conn_2_Spam_Subscr tester = new Conn_2_Spam_Subscr();
                    tester.SecutiesToSubscrible = C2_SecuritiesNames.ValueString;
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
                else if (CurTestType == ServerTestType.Conn_3)
                {
                    Conn_3_SubscrAllSec tester = new Conn_3_SubscrAllSec();
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
                else if (CurTestType == ServerTestType.Conn_4)
                {
                    Conn_4_Stress_Memory tester = new Conn_4_Stress_Memory();
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
                else if (CurTestType == ServerTestType.Conn_5)
                {
                    Conn_5_Validation_Candles tester = new Conn_5_Validation_Candles();
                    tester.SecutiesToSubscrible = C5_SecuritiesNames.ValueString;
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
                else if (CurTestType == ServerTestType.Order_1)
                {
                    Orders_1_FakeOrders tester = new Orders_1_FakeOrders();
                    tester.SecurityToTrade = O1_SecurityName.ValueString;
                    tester.PortfolioName = O1_PortfolioName.ValueString;
                    tester.VolumeMin = O1_VolumeLess.ValueDecimal;
                    tester.VolumeMax = O1_VolumeMax.ValueDecimal;
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
                else if (CurTestType == ServerTestType.Order_2)
                {
                    Orders_2_LimitsExecute tester = new Orders_2_LimitsExecute();
                    tester.SecurityToTrade = O2_SecurityName.ValueString;
                    tester.PortfolioName = O2_PortfolioName.ValueString;
                    tester.VolumeToTrade = O2_Volume.ValueDecimal;
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
                else if (CurTestType == ServerTestType.Order_3)
                {
                    Orders_3_MarketOrders tester = new Orders_3_MarketOrders();
                    tester.SecurityToTrade = O3_SecurityName.ValueString;
                    tester.PortfolioName = O3_PortfolioName.ValueString;
                    tester.VolumeToTrade = O3_Volume.ValueDecimal;
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
                else if (CurTestType == ServerTestType.Order_4)
                {
                    Orders_4_LimitCancel tester = new Orders_4_LimitCancel();
                    tester.SecurityToTrade = O4_SecurityName.ValueString;
                    tester.PortfolioName = O4_PortfolioName.ValueString;
                    tester.VolumeToTrade = O4_Volume.ValueDecimal;
                    tester.CountOrders = O4_CountOrders.ValueInt;
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
                else if (CurTestType == ServerTestType.Order_5)
                {
                    Orders_5_ChangePrice tester = new Orders_5_ChangePrice();
                    tester.SecurityToTrade = O5_SecurityName.ValueString;
                    tester.PortfolioName = O5_PortfolioName.ValueString;
                    tester.VolumeToTrade = O5_Volume.ValueDecimal;
                    tester.CountOrders = O5_CountOrders.ValueInt;
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
                else if (CurTestType == ServerTestType.Portfolio_1)
                {
                    Portfolio_1_Validation tester = new Portfolio_1_Validation();
                    tester.SecurityToTrade = P1_SecurityName.ValueString;
                    tester.PortfolioName = P1_PortfolioName.ValueString;
                    tester.VolumeToTrade = P1_Volume.ValueDecimal;
                    tester.AssetInPortfolio = P1_AssetInPortfolioName.ValueString;
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
            }

            while (_testers.Count > 0)
            {
                Thread.Sleep(1000);
            }

            SendNewLogMessage("Tests ended", LogMessageType.Error);
            _threadIsWork = false;
        }

        private bool _threadIsWork;

        List<AServerTester> _testers = new List<AServerTester>();

        private string _testerLocker = "testerLocker";

        private void Tester_TestEndEvent(AServerTester serverTest)
        {
            lock(_testerLocker)
            {
                serverTest.LogMessage -= SendNewLogMessage;
                serverTest.TestEndEvent -= Tester_TestEndEvent;

                for (int i = 0;i < _testers.Count;i++)
                {
                    string type = _testers[i].GetType().Name;

                    if (_testers[i].GetType().Name == serverTest.GetType().Name &&
                        _testers[i].Server.ServerType == serverTest.Server.ServerType)
                    {
                        _testers.RemoveAt(i);
                        break;
                    }
                }
            }

            SendNewLogMessage(serverTest.GetReport(), LogMessageType.Error);
        }

        #endregion
    }

    public enum ServerTestType
    {
        Var_1_Securities,
        Var_2_MarketDepth,
        Data_1,
        Data_2,
        Data_3,
        Data_4,
        Data_5,
        Conn_1,
        Conn_2,
        Conn_3,
        Conn_4,
        Conn_5,
        Order_1,
        Order_2,
        Order_3,
        Order_4,
        Order_5,
        Portfolio_1,
    }

    public abstract class AServerTester
    {
        public AServer Server
        {
            get
            {
                return _myServer;
            }
            set
            {
                _myServer = value;
            }
        }
        public AServer _myServer;

        public void Start()
        {
            Thread worker = new Thread(Process);
            worker.Start();
        }

        public abstract void Process();

        public string GetReport()
        {
            string report = "REPORT " + this.GetType().Name + "  \n";

            report += "SERVER: " + Server.ServerType + "  \n";

            if (_errors.Count == 0)
            {
                report += "STATUS: OK";
            }
            else
            {
                report += "STATUS: FAIL \n";
                report += "Errors: \n";

                for(int i = 0;i < _errors.Count;i++)
                {
                    report += (i+1) + "  " +  _errors[i] + "\n";
                }
            }

            if(_serviceInfo.Count != 0)
            {
                report += "\n SERVICE INFO \n";

                for (int i = 0; i < _serviceInfo.Count; i++)
                {
                    report += (i + 1) + "  " + _serviceInfo[i] + "\n";
                }
            }

            return report;
        }

        public void SetNewServiceInfo(string serviceInfo)
        {
            _serviceInfo.Add(serviceInfo);
        }

        List<string> _serviceInfo = new List<string>();

        public void SetNewError(string error)
        {
            for(int i = 0;i < _errors.Count;i++)
            {
                if (_errors[i].Equals(error))
                {
                    return;
                }
            }

            _errors.Add(error);
        }

        public List<string> _errors = new List<string>();

        public event Action<string, LogMessageType> LogMessage;

        public void TestEnded()
        {
            if(TestEndEvent != null)
            {
                TestEndEvent(this);
            }
        }

        public event Action<AServerTester> TestEndEvent;
    }
}