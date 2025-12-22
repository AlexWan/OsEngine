using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using System.Threading;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.Language;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    [Bot("WServerTester")]
    public class WServerTester : BotPanel
    {
        #region Settings and Parameters

        public WServerTester(string name, StartProgram startProgram) : base(name, startProgram)
        {
            StrategyParameterButton buttonSecTests = CreateParameterButton("Start test sec", "V1");
            buttonSecTests.UserClickOnButtonEvent += ButtonSecTests_UserClickOnButtonEvent;

            StrategyParameterButton buttonMarketDepth = CreateParameterButton("Start test md", "V2");
            buttonMarketDepth.UserClickOnButtonEvent += ButtonMarketDepth_UserClickOnButtonEvent;
            V2_SecuritiesSeparator = CreateParameter("Securities Separator v2", "_", "V2");
            V2_SecurityName = CreateParameter("Md tester Sec names v2", "ADAUSDT_BNBUSDT_ETHUSDT_BTCUSDT", "V2");
            V2_ClassCode = CreateParameter("Md tester Class Code v2", "Futures", "V2");
            V2_MarketDepthMinutesToTest = CreateParameter("Md tester work time minutes v2", 5, 5, 5, 1, "V2");

            StrategyParameterButton buttonTrades = CreateParameterButton("Start test trades", "V3");
            buttonTrades.UserClickOnButtonEvent += ButtonTrades_UserClickOnButtonEvent;
            V3_SecuritiesSeparator = CreateParameter("Securities Separator v3", "_", "V3");
            V3_SecurityName = CreateParameter("Sec names v3", "ADAUSDT_BNBUSDT_ETHUSDT_BTCUSDT", "V3");
            V3_ClassCode = CreateParameter("Class Code v3", "Futures", "V3");
            V3_TradesMinutesToTest = CreateParameter("Tester work time minutes v3", 5, 5, 5, 1, "V3");

            StrategyParameterButton buttonDataTest1 = CreateParameterButton("Start test data 1", "D1");
            buttonDataTest1.UserClickOnButtonEvent += ButtonDataTest1_UserClickOnButtonEvent;
            D1_SecurityName = CreateParameter("Sec name data test 1", "ADAUSDT", "D1");
            D1_SecurityClass = CreateParameter("Sec class data test 1", "Futures", "D1");
            D1_StartDate = CreateParameter("Base date for data request test 1", DateTime.Now.ToString(), "D1");

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
            D4_SecuritiesSeparator = CreateParameter("Securities separator data test 4", "_", "D4");
            D4_SecuritiesNames = CreateParameter("Sec name data test 4", "ADAUSDT_BNBUSDT_ETHUSDT_BTCUSDT", "D4");
            D4_SecurityClass = CreateParameter("Sec class data test 4", "Futures", "D4");
            D4_StartDate = CreateParameter("Base date for data request test 4", DateTime.Now.ToString(), "D4");

            StrategyParameterButton buttonDataTest5 = CreateParameterButton("Start test data 5", "D5");
            buttonDataTest5.UserClickOnButtonEvent += ButtonDataTest5_UserClickOnButtonEvent;
            D5_SecuritiesSeparator = CreateParameter("Securities separator test 5", "_", "D5");
            D5_SecuritiesNames = CreateParameter("Sec name data test 5", "ADAUSDT_BNBUSDT_ETHUSDT", "D5");
            D5_SecurityClass = CreateParameter("Sec class data test 5", "Futures", "D5");

            StrategyParameterButton buttonConnectionTest1 = CreateParameterButton("Start test connection 1", "C1");
            buttonConnectionTest1.UserClickOnButtonEvent += ButtonConnectionTest1_UserClickOnButtonEvent;

            C2_SecuritiesClass = CreateParameter("Sec class connection test 2", "Futures", "C2");
            StrategyParameterButton buttonConnectionTest2 = CreateParameterButton("Start test connection 2", "C2");
            buttonConnectionTest2.UserClickOnButtonEvent += ButtonConnectionTest2_UserClickOnButtonEvent;

            C3_SecuritiesClass = CreateParameter("Sec class connection test 3", "Futures", "C3");
            StrategyParameterButton buttonConnectionTest3 = CreateParameterButton("Start test connection 3", "C3");
            buttonConnectionTest3.UserClickOnButtonEvent += ButtonConnectionTest3_UserClickOnButtonEvent;

            StrategyParameterButton buttonConnectionTest4 = CreateParameterButton("Start test connection 4", "C4");
            buttonConnectionTest4.UserClickOnButtonEvent += ButtonConnectionTest4_UserClickOnButtonEvent;
            C4_SecuritiesSeparator = CreateParameter("Securities separator test 4", "_", "C4");
            C4_SecuritiesNames = CreateParameter("Sec name connection test 4", "ADAUSDT_BNBUSDT_ETHUSDT_BTCUSDT", "C4");
            C4_SecuritiesClass = CreateParameter("Sec class connection test 4", "Futures", "C4");

            StrategyParameterButton buttonConnectionTest5 = CreateParameterButton("Start test connection 5", "C5");
            buttonConnectionTest5.UserClickOnButtonEvent += ButtonConnectionTest5_UserClickOnButtonEvent;
            C5_SecuritiesClass = CreateParameter("Sec class connection test 5", "Futures", "C5");
            C5_SecuritiesCount = CreateParameter("Sec count connection test 5", 15, 1, 150, 1, "C5");
            C5_SecuritiesMinutesToTest = CreateParameter("Screneer tester work time minutes C5", 5, 5, 5, 1, "C5");

            StrategyParameterButton buttonConnectionTest5_ShowScreener = CreateParameterButton("Show screener. test connection 5", "C5");
            buttonConnectionTest5_ShowScreener.UserClickOnButtonEvent += ButtonConnectionTest5_ShowScreener_UserClickOnButtonEvent;
            List<string> timeFrames = new List<string>();
            timeFrames.Add(TimeFrame.Sec1.ToString()); timeFrames.Add(TimeFrame.Sec2.ToString());
            timeFrames.Add(TimeFrame.Sec5.ToString()); timeFrames.Add(TimeFrame.Sec10.ToString());
            timeFrames.Add(TimeFrame.Sec15.ToString()); timeFrames.Add(TimeFrame.Sec20.ToString());
            timeFrames.Add(TimeFrame.Sec30.ToString()); timeFrames.Add(TimeFrame.Min1.ToString());
            timeFrames.Add(TimeFrame.Min2.ToString()); timeFrames.Add(TimeFrame.Min3.ToString());
            timeFrames.Add(TimeFrame.Min5.ToString()); timeFrames.Add(TimeFrame.Min10.ToString());
            timeFrames.Add(TimeFrame.Min15.ToString()); timeFrames.Add(TimeFrame.Min20.ToString());
            timeFrames.Add(TimeFrame.Min30.ToString()); timeFrames.Add(TimeFrame.Min45.ToString());
            timeFrames.Add(TimeFrame.Hour1.ToString()); timeFrames.Add(TimeFrame.Hour2.ToString());
            timeFrames.Add(TimeFrame.Hour4.ToString()); timeFrames.Add(TimeFrame.Day.ToString());
            C5_TimeFrame = CreateParameter("Sec timeFrame connection test 5", "Min1", timeFrames.ToArray(), "C5");

            StrategyParameterButton buttonOrdersTest1 = CreateParameterButton("Start test orders 1", "O1");
            buttonOrdersTest1.UserClickOnButtonEvent += ButtonOrdersTest1_UserClickOnButtonEvent;
            O1_PortfolioName = CreateParameter("Portfolio. orders test 1", "BinanceFutures", "O1");
            O1_SecurityName = CreateParameter("Sec name. orders test 1", "ETHUSDT", "O1");
            O1_SecurityClass = CreateParameter("Sec class. orders test 1", "Futures", "O1");
            O1_VolumeLess = CreateParameter("Volume. To small", 0.01m, 1, 1, 1, "O1");
            O1_VolumeMax = CreateParameter("Volume. To big", 5000m, 1, 1, 1, "O1");

            StrategyParameterButton buttonOrdersTest2 = CreateParameterButton("Start test orders 2", "O2");
            buttonOrdersTest2.UserClickOnButtonEvent += ButtonOrdersTest2_UserClickOnButtonEvent;
            O2_PortfolioName = CreateParameter("Portfolio. orders test 2", "BinanceFutures", "O2");
            O2_SecurityName = CreateParameter("Sec name. orders test 2", "ETHUSDT", "O2");
            O2_SecurityClass = CreateParameter("Sec class. orders test 2", "Futures", "O2");
            O2_Volume = CreateParameter("Volume. orders test 2", 0.01m, 1, 1, 1, "O2");

            StrategyParameterButton buttonOrdersTest3 = CreateParameterButton("Start test orders 3", "O3");
            buttonOrdersTest3.UserClickOnButtonEvent += ButtonOrdersTest3_UserClickOnButtonEvent;
            O3_PortfolioName = CreateParameter("Portfolio. orders test 3", "BinanceFutures", "O3");
            O3_SecurityName = CreateParameter("Sec name. orders test 3", "ETHUSDT", "O3");
            O3_SecurityClass = CreateParameter("Sec class. orders test 3", "Futures", "O3");
            O3_Volume = CreateParameter("Volume. orders test 3", 0.01m, 1, 1, 1, "O3");

            StrategyParameterButton buttonOrdersTest4 = CreateParameterButton("Start test orders 4", "O4");
            buttonOrdersTest4.UserClickOnButtonEvent += ButtonOrdersTest4_UserClickOnButtonEvent;
            O4_PortfolioName = CreateParameter("Portfolio. orders test 4", "BinanceFutures", "O4");
            O4_SecurityName = CreateParameter("Sec name. orders test 4", "ETHUSDT", "O4");
            O4_SecurityClass = CreateParameter("Sec class. orders test 4", "Futures", "O4");
            O4_Volume = CreateParameter("Volume. orders test 4", 0.01m, 1, 1, 1, "O4");
            O4_CountOrders = CreateParameter("Count orders test 4", 25, 1, 1, 1, "O4");

            StrategyParameterButton buttonOrdersTest5 = CreateParameterButton("Start test orders 5", "O5");
            buttonOrdersTest5.UserClickOnButtonEvent += ButtonOrdersTest5_UserClickOnButtonEvent;
            O5_PortfolioName = CreateParameter("Portfolio. orders test 5", "BinanceFutures", "O5");
            O5_SecurityName = CreateParameter("Sec name. orders test 5", "ETHUSDT", "O5");
            O5_SecurityClass = CreateParameter("Sec class. orders test 5", "Futures", "O5");
            O5_Volume = CreateParameter("Volume. orders test 5", 0.01m, 1, 1, 1, "O5");
            O5_CountOrders = CreateParameter("Count orders test 5", 5, 1, 1, 1, "O5");

            StrategyParameterButton buttonOrdersTest6 = CreateParameterButton("Start test orders 6", "O6");
            buttonOrdersTest6.UserClickOnButtonEvent += ButtonOrdersTest6_UserClickOnButtonEvent;
            O6_SecurityName = CreateParameter("Sec name. orders test 6", "ETHUSDT", "O6");
            O6_SecurityClass = CreateParameter("Sec class. orders test 6", "Futures", "O6");
            O6_PortfolioName = CreateParameter("Portfolio. orders test 6", "BinanceFutures", "O6");
            O6_Volume = CreateParameter("Volume. orders test 6", 0.01m, 1, 1, 1, "O6");
            O6_FakeBigPrice = CreateParameter("Fake big price. orders test 6", 0.01m, 1, 1, 1, "O6");
            O6_FakeSmallPrice = CreateParameter("Fake small price. orders test 6", 0.01m, 1, 1, 1, "O6");

            StrategyParameterButton buttonOrdersTest7 = CreateParameterButton("Start test orders 7", "O7");
            buttonOrdersTest7.UserClickOnButtonEvent += ButtonOrdersTest7_UserClickOnButtonEvent;
            O7_PortfolioName = CreateParameter("Portfolio. orders test 7", "BinanceFutures", "O7");
            O7_SecurityName = CreateParameter("Sec name. orders test 7", "ETHUSDT", "O7");
            O7_SecurityClass = CreateParameter("Sec class. orders test 7", "Futures", "O7");
            O7_Volume = CreateParameter("Volume. orders test 7", 0.01m, 1, 1, 1, "O7");
            O7_CountOrders = CreateParameter("Count orders test 7", 5, 1, 1, 1, "O7");

            StrategyParameterButton buttonOrdersTest8 = CreateParameterButton("Start test orders 8", "O8");
            buttonOrdersTest8.UserClickOnButtonEvent += ButtonOrdersTest8_UserClickOnButtonEvent;
            O8_PortfolioName = CreateParameter("Portfolio. orders test 8", "BinanceFutures", "O8");
            O8_SecurityName = CreateParameter("Sec name. orders test 8", "ETHUSDT", "O8");
            O8_SecurityClass = CreateParameter("Sec class. orders test 8", "Futures", "O8");
            O8_Volume = CreateParameter("Volume. orders test 8", 0.01m, 1, 1, 1, "O8");

            StrategyParameterButton buttonOrdersTest9 = CreateParameterButton("Start test orders 9", "O9");
            buttonOrdersTest9.UserClickOnButtonEvent += ButtonOrdersTest9_UserClickOnButtonEvent;
            O9_PortfolioName = CreateParameter("Portfolio. orders test 9", "BinanceFutures", "O9");
            O9_SecurityName = CreateParameter("Sec name. orders test 9", "ETHUSDT", "O9");
            O9_SecurityClass = CreateParameter("Sec class. orders test 9", "Futures", "O9");
            O9_Volume = CreateParameter("Volume. orders test 9", 0.01m, 1, 1, 1, "O9");

            StrategyParameterButton buttonOrdersTest10 = CreateParameterButton("Start test orders 10", "O10");
            buttonOrdersTest10.UserClickOnButtonEvent += ButtonOrdersTest10_UserClickOnButtonEvent;
            O10_PortfolioName = CreateParameter("Portfolio. orders test 10", "BinanceFutures", "O10");
            O10_SecurityName = CreateParameter("Sec name. orders test 10", "ETHUSDT", "O10");
            O10_SecurityClass = CreateParameter("Sec class. orders test 10", "Futures", "O10");
            O10_Volume = CreateParameter("Volume. orders test 10", 0.01m, 1, 1, 1, "O10");

            StrategyParameterButton buttonOrdersTest11 = CreateParameterButton("Start test orders 11", "O11");
            buttonOrdersTest11.UserClickOnButtonEvent += ButtonOrdersTest11_UserClickOnButtonEvent;
            O11_PortfolioName = CreateParameter("Portfolio. orders test 11", "BinanceFutures", "O11");
            O11_SecurityName = CreateParameter("Sec name. orders test 11", "ETHUSDT", "O11");
            O11_SecurityClass = CreateParameter("Sec class. orders test 11", "Futures", "O11");
            O11_Volume = CreateParameter("Volume. orders test 11", 0.01m, 1, 1, 1, "O11");

            StrategyParameterButton buttonOrdersTest12 = CreateParameterButton("Start test orders 12", "O12");
            buttonOrdersTest12.UserClickOnButtonEvent += ButtonOrdersTest12_UserClickOnButtonEvent;
            O12_PortfolioName = CreateParameter("Portfolio. orders test 12", "BinanceFutures", "O12");
            O12_SecurityName = CreateParameter("Sec name. orders test 12", "ETHUSDT", "O12");
            O12_SecurityClass = CreateParameter("Sec class. orders test 12", "Futures", "O12");
            O12_Volume = CreateParameter("Volume. orders test 12", 0.01m, 1, 1, 1, "O12");
            O12_CountOrders = CreateParameter("Count orders test 12", 5, 1, 1, 1, "O12");


            StrategyParameterButton buttonPortfolioTest1 = CreateParameterButton("Start test portfolio 1", "P1");
            buttonPortfolioTest1.UserClickOnButtonEvent += ButtonPortfolioTest1_UserClickOnButtonEvent;
            P1_PortfolioName = CreateParameter("Portfolio.  portfolio 1", "BinanceFutures", "P1");
            P1_SecurityName = CreateParameter("Sec name.  portfolio 1", "ETHUSDT", "P1");
            P1_SecurityClass = CreateParameter("Sec class.  portfolio 1", "Futures", "P1");
            P1_AssetInPortfolioName = CreateParameter("Asset In portfolio 1", "ETH", "P1");
            P1_Volume = CreateParameter("Volume.  portfolio 1", 0.01m, 1, 1, 1, "P1");

            Description = OsLocalization.Description.DescriptionLabel125;
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
        StrategyParameterString V2_SecuritiesSeparator;

        StrategyParameterString V3_SecurityName;
        StrategyParameterString V3_ClassCode;
        StrategyParameterInt V3_TradesMinutesToTest;
        StrategyParameterString V3_SecuritiesSeparator;

        StrategyParameterString D1_SecurityName;
        StrategyParameterString D1_SecurityClass;
        StrategyParameterString D1_StartDate;

        StrategyParameterString D2_SecurityName;
        StrategyParameterString D2_SecurityClass;

        StrategyParameterString D3_SecurityName;
        StrategyParameterString D3_SecurityClass;

        StrategyParameterString D4_SecuritiesNames;
        StrategyParameterString D4_SecuritiesSeparator;
        StrategyParameterString D4_SecurityClass;
        StrategyParameterString D4_StartDate;


        StrategyParameterString D5_SecuritiesNames;
        StrategyParameterString D5_SecuritiesSeparator;
        StrategyParameterString D5_SecurityClass;

        StrategyParameterString C2_SecuritiesClass;

        StrategyParameterString C3_SecuritiesClass;

        StrategyParameterString C4_SecuritiesNames;
        StrategyParameterString C4_SecuritiesSeparator;
        StrategyParameterString C4_SecuritiesClass;

        StrategyParameterString C5_SecuritiesClass;
        StrategyParameterInt C5_SecuritiesCount;
        StrategyParameterString C5_TimeFrame;
        StrategyParameterInt C5_SecuritiesMinutesToTest;

        StrategyParameterString O1_SecurityName;
        StrategyParameterString O1_SecurityClass;
        StrategyParameterString O1_PortfolioName;
        StrategyParameterDecimal O1_VolumeLess;
        StrategyParameterDecimal O1_VolumeMax;

        StrategyParameterString O2_SecurityName;
        StrategyParameterString O2_SecurityClass;
        StrategyParameterString O2_PortfolioName;
        StrategyParameterDecimal O2_Volume;

        StrategyParameterString O3_SecurityName;
        StrategyParameterString O3_SecurityClass;
        StrategyParameterString O3_PortfolioName;
        StrategyParameterDecimal O3_Volume;

        StrategyParameterString O4_SecurityName;
        StrategyParameterString O4_SecurityClass;
        StrategyParameterString O4_PortfolioName;
        StrategyParameterDecimal O4_Volume;
        StrategyParameterInt O4_CountOrders;

        StrategyParameterString O5_SecurityName;
        StrategyParameterString O5_SecurityClass;
        StrategyParameterString O5_PortfolioName;
        StrategyParameterDecimal O5_Volume;
        StrategyParameterInt O5_CountOrders;

        StrategyParameterString O6_SecurityName;
        StrategyParameterString O6_SecurityClass;
        StrategyParameterString O6_PortfolioName;
        StrategyParameterDecimal O6_Volume;
        StrategyParameterDecimal O6_FakeBigPrice;
        StrategyParameterDecimal O6_FakeSmallPrice;

        StrategyParameterString O7_SecurityName;
        StrategyParameterString O7_SecurityClass;
        StrategyParameterString O7_PortfolioName;
        StrategyParameterDecimal O7_Volume;
        StrategyParameterInt O7_CountOrders;

        StrategyParameterString O8_SecurityName;
        StrategyParameterString O8_SecurityClass;
        StrategyParameterString O8_PortfolioName;
        StrategyParameterDecimal O8_Volume;

        StrategyParameterString O9_SecurityName;
        StrategyParameterString O9_SecurityClass;
        StrategyParameterString O9_PortfolioName;
        StrategyParameterDecimal O9_Volume;

        StrategyParameterString O10_SecurityName;
        StrategyParameterString O10_SecurityClass;
        StrategyParameterString O10_PortfolioName;
        StrategyParameterDecimal O10_Volume;

        StrategyParameterString O11_SecurityName;
        StrategyParameterString O11_SecurityClass;
        StrategyParameterString O11_PortfolioName;
        StrategyParameterDecimal O11_Volume;

        StrategyParameterString O12_SecurityName;
        StrategyParameterString O12_SecurityClass;
        StrategyParameterString O12_PortfolioName;
        StrategyParameterDecimal O12_Volume;
        StrategyParameterInt O12_CountOrders;

        StrategyParameterString P1_SecurityName;
        StrategyParameterString P1_SecurityClass;
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

        private void ButtonTrades_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Var_3_Trades;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonSecTests_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
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

        private void ButtonConnectionTest5_ShowScreener_UserClickOnButtonEvent()
        {
            if (_testers == null ||
                 _testers.Count == 0)
            {
                SendNewLogMessage("No test in array", LogMessageType.Error);
                return;
            }

            AServerTester test = _testers[0];

            if (test.GetType().Name != "Conn_5_Screener")
            {
                SendNewLogMessage("We need to run a test first. Conn_5_Screener", LogMessageType.Error);
                return;
            }

            Conn_5_Screener testScreener = (Conn_5_Screener)test;

            testScreener.ShowDialog();

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

        private void ButtonOrdersTest6_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Order_6;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonOrdersTest7_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Order_7;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonOrdersTest8_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Order_8;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonOrdersTest9_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Order_9;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonOrdersTest10_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Order_10;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonOrdersTest11_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Order_11;

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        private void ButtonOrdersTest12_UserClickOnButtonEvent()
        {
            if (_threadIsWork == true)
            {
                return;
            }

            CurTestType = ServerTestType.Order_12;

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

            try
            {
                List<IServer> servers = ServerMaster.GetServers();

                if (servers == null ||
                    servers.Count == 0)
                {
                    _threadIsWork = false;
                    SendNewLogMessage("No Servers Found", LogMessageType.Error);
                    return;
                }

                if (servers.Count > 1)
                {
                    _threadIsWork = false;
                    SendNewLogMessage("You've created more than one server! Tests are not possible. Only one at a time!", LogMessageType.Error);
                    return;
                }

                for (int i = 0; servers != null && i < servers.Count; i++)
                {
                    string servType = servers[i].GetType().BaseType.ToString();

                    if (servType.EndsWith("AServer") == false)
                    {
                        continue;
                    }

                    if (CurTestType == ServerTestType.Var_1_Securities)
                    {
                        Var_1_Securities tester = new Var_1_Securities();
                        tester.LogMessage += SendNewLogMessage;
                        tester.TestEndEvent += Tester_TestEndEvent;
                        _testers.Add(tester);
                        tester.Server = (AServer)servers[i];
                        SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                        tester.Start();
                    }
                    else if (CurTestType == ServerTestType.Var_2_MarketDepth)
                    {
                        Var_2_MarketDepth tester = new Var_2_MarketDepth();
                        tester.MinutesToTest = V2_MarketDepthMinutesToTest.ValueInt;
                        tester.SecNames = V2_SecurityName.ValueString;
                        tester.SecClassCode = V2_ClassCode.ValueString;
                        tester.SecuritiesSeparator = V2_SecuritiesSeparator.ValueString;
                        tester.LogMessage += SendNewLogMessage;
                        tester.TestEndEvent += Tester_TestEndEvent;
                        _testers.Add(tester);
                        tester.Server = (AServer)servers[i];
                        SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                        tester.Start();
                    }
                    else if (CurTestType == ServerTestType.Var_3_Trades)
                    {
                        Var_3_Trades tester = new Var_3_Trades();
                        tester.MinutesToTest = V3_TradesMinutesToTest.ValueInt;
                        tester.SecNames = V3_SecurityName.ValueString;
                        tester.SecClassCode = V3_ClassCode.ValueString;
                        tester.SecuritiesSeparator = V3_SecuritiesSeparator.ValueString;
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
                        tester.StartDate = DateTime.Parse(D1_StartDate.ValueString);
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
                        tester.SecuritiesSeparator = D4_SecuritiesSeparator.ValueString;
                        tester.StartDate = DateTime.Parse(D4_StartDate.ValueString);
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
                        tester.SecClass = D5_SecurityClass.ValueString;
                        tester.SecuritiesSeparator = D5_SecuritiesSeparator.ValueString;
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
                        Conn_2_SubscrAllSec tester = new Conn_2_SubscrAllSec();
                        tester.SecClass = C2_SecuritiesClass.ValueString;
                        tester.LogMessage += SendNewLogMessage;
                        tester.TestEndEvent += Tester_TestEndEvent;
                        _testers.Add(tester);
                        tester.Server = (AServer)servers[i];
                        SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                        tester.Start();
                    }
                    else if (CurTestType == ServerTestType.Conn_3)
                    {
                        Conn_3_Stress_Memory tester = new Conn_3_Stress_Memory();
                        tester.SecClass = C3_SecuritiesClass.ValueString;
                        tester.LogMessage += SendNewLogMessage;
                        tester.TestEndEvent += Tester_TestEndEvent;
                        _testers.Add(tester);
                        tester.Server = (AServer)servers[i];
                        SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                        tester.Start();
                    }
                    else if (CurTestType == ServerTestType.Conn_4)
                    {
                        Conn_4_Validation_Candles tester = new Conn_4_Validation_Candles();
                        tester.SecutiesToSubscribe = C4_SecuritiesNames.ValueString;
                        tester.SecuritiesClass = C4_SecuritiesClass.ValueString;
                        tester.SecuritiesSeparator = C4_SecuritiesSeparator.ValueString;
                        tester.LogMessage += SendNewLogMessage;
                        tester.TestEndEvent += Tester_TestEndEvent;
                        _testers.Add(tester);
                        tester.Server = (AServer)servers[i];
                        SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                        tester.Start();
                    }
                    else if (CurTestType == ServerTestType.Conn_5)
                    {
                        Conn_5_Screener tester = new Conn_5_Screener();
                        tester.MinutesToTest = C5_SecuritiesMinutesToTest.ValueInt;
                        tester.SecuritiesClass = C5_SecuritiesClass.ValueString;
                        tester.SecuritiesCount = C5_SecuritiesCount.ValueInt;
                        tester.TimeFrame = C5_TimeFrame.ValueString;

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
                        tester.SecurityNameToTrade = O1_SecurityName.ValueString;
                        tester.SecurityClassToTrade = O1_SecurityClass.ValueString;
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
                        tester.SecurityNameToTrade = O2_SecurityName.ValueString;
                        tester.SecurityClassToTrade = O2_SecurityClass.ValueString;
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
                        tester.SecurityNameToTrade = O3_SecurityName.ValueString;
                        tester.SecurityClassToTrade = O3_SecurityClass.ValueString;
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
                        tester.SecurityNameToTrade = O4_SecurityName.ValueString;
                        tester.SecurityClassToTrade = O4_SecurityClass.ValueString;
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
                        tester.SecurityNameToTrade = O5_SecurityName.ValueString;
                        tester.SecurityClassToTrade = O5_SecurityClass.ValueString;
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
                    else if (CurTestType == ServerTestType.Order_6)
                    {
                        Orders_6_ChangePriceError tester = new Orders_6_ChangePriceError();
                        tester.SecurityNameToTrade = O6_SecurityName.ValueString;
                        tester.SecurityClassToTrade = O6_SecurityClass.ValueString;
                        tester.PortfolioName = O6_PortfolioName.ValueString;
                        tester.VolumeToTrade = O6_Volume.ValueDecimal;
                        tester.FakeBigPrice = O6_FakeBigPrice.ValueDecimal;
                        tester.FakeSmallPrice = O6_FakeSmallPrice.ValueDecimal;

                        tester.LogMessage += SendNewLogMessage;
                        tester.TestEndEvent += Tester_TestEndEvent;
                        _testers.Add(tester);
                        tester.Server = (AServer)servers[i];
                        SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                        tester.Start();
                    }
                    else if (CurTestType == ServerTestType.Order_7)
                    {
                        Orders_7_Add_Move_Cancel_Spam tester = new Orders_7_Add_Move_Cancel_Spam();
                        tester.SecurityNameToTrade = O7_SecurityName.ValueString;
                        tester.SecurityClassToTrade = O7_SecurityClass.ValueString;
                        tester.PortfolioName = O7_PortfolioName.ValueString;
                        tester.VolumeToTrade = O7_Volume.ValueDecimal;
                        tester.CountOrders = O7_CountOrders.ValueInt;
                        tester.LogMessage += SendNewLogMessage;
                        tester.TestEndEvent += Tester_TestEndEvent;
                        _testers.Add(tester);
                        tester.Server = (AServer)servers[i];
                        SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                        tester.Start();
                    }
                    else if (CurTestType == ServerTestType.Order_8)
                    {
                        Orders_8_RequestOnReconnect tester = new Orders_8_RequestOnReconnect();
                        tester.SecurityNameToTrade = O8_SecurityName.ValueString;
                        tester.SecurityClassToTrade = O8_SecurityClass.ValueString;
                        tester.PortfolioName = O8_PortfolioName.ValueString;
                        tester.VolumeToTrade = O8_Volume.ValueDecimal;
                        tester.LogMessage += SendNewLogMessage;
                        tester.TestEndEvent += Tester_TestEndEvent;
                        _testers.Add(tester);
                        tester.Server = (AServer)servers[i];
                        SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                        tester.Start();
                    }
                    else if (CurTestType == ServerTestType.Order_9)
                    {
                        Orders_9_RequestLostActivOrder tester = new Orders_9_RequestLostActivOrder();
                        tester.SecurityNameToTrade = O9_SecurityName.ValueString;
                        tester.SecurityClassToTrade = O9_SecurityClass.ValueString;
                        tester.PortfolioName = O9_PortfolioName.ValueString;
                        tester.VolumeToTrade = O9_Volume.ValueDecimal;
                        tester.LogMessage += SendNewLogMessage;
                        tester.TestEndEvent += Tester_TestEndEvent;
                        _testers.Add(tester);
                        tester.Server = (AServer)servers[i];
                        SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                        tester.Start();
                    }
                    else if (CurTestType == ServerTestType.Order_10)
                    {
                        Orders_10_RequestLostDoneOrder tester = new Orders_10_RequestLostDoneOrder();
                        tester.SecurityNameToTrade = O10_SecurityName.ValueString;
                        tester.SecurityClassToTrade = O10_SecurityClass.ValueString;
                        tester.PortfolioName = O10_PortfolioName.ValueString;
                        tester.VolumeToTrade = O10_Volume.ValueDecimal;
                        tester.LogMessage += SendNewLogMessage;
                        tester.TestEndEvent += Tester_TestEndEvent;
                        _testers.Add(tester);
                        tester.Server = (AServer)servers[i];
                        SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                        tester.Start();
                    }
                    else if (CurTestType == ServerTestType.Order_11)
                    {
                        Orders_11_RequestLostMyTrades tester = new Orders_11_RequestLostMyTrades();
                        tester.SecurityNameToTrade = O11_SecurityName.ValueString;
                        tester.SecurityClassToTrade = O11_SecurityClass.ValueString;
                        tester.PortfolioName = O11_PortfolioName.ValueString;
                        tester.VolumeToTrade = O11_Volume.ValueDecimal;
                        tester.LogMessage += SendNewLogMessage;
                        tester.TestEndEvent += Tester_TestEndEvent;
                        _testers.Add(tester);
                        tester.Server = (AServer)servers[i];
                        SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                        tester.Start();
                    }
                    else if (CurTestType == ServerTestType.Order_12)
                    {
                        Orders_12_RequestOrdersList tester = new Orders_12_RequestOrdersList();
                        tester.SecurityNameToTrade = O12_SecurityName.ValueString;
                        tester.SecurityClassToTrade = O12_SecurityClass.ValueString;
                        tester.PortfolioName = O12_PortfolioName.ValueString;
                        tester.VolumeToTrade = O12_Volume.ValueDecimal;
                        tester.OrdersCount = O12_CountOrders.ValueInt;
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
                        tester.SecurityNameToTrade = P1_SecurityName.ValueString;
                        tester.SecurityClassToTrade = P1_SecurityClass.ValueString;
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
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                SendNewLogMessage("Tests ended with critical error", LogMessageType.Error);
                _threadIsWork = false;
            }
        }

        private bool _threadIsWork;

        List<AServerTester> _testers = new List<AServerTester>();

        private string _testerLocker = "testerLocker";

        private void Tester_TestEndEvent(AServerTester serverTest)
        {
            lock (_testerLocker)
            {
                serverTest.LogMessage -= SendNewLogMessage;
                serverTest.TestEndEvent -= Tester_TestEndEvent;

                for (int i = 0; i < _testers.Count; i++)
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
        Var_3_Trades,
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
        Order_6,
        Order_7,
        Order_8,
        Order_9,
        Order_10,
        Order_11,
        Order_12,
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
            Thread worker = new Thread(WorkerPlace);
            worker.Start();
        }

        private void WorkerPlace()
        {
            try
            {
                Process();
            }
            catch (Exception e)
            {
                SetNewError(e.ToString());
                TestEnded();
            }
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

                for (int i = 0; i < _errors.Count; i++)
                {
                    report += (i + 1) + "  " + _errors[i] + "\n";
                }
            }

            if (_serviceInfo.Count != 0)
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
            for (int i = 0; i < _errors.Count; i++)
            {
                if (_errors[i].Equals(error))
                {
                    return;
                }
            }

            _errors.Add(error);
        }

        public List<string> _errors = new List<string>();

        public event Action<string, LogMessageType> LogMessage { add { } remove { } }

        public void TestEnded()
        {
            if (TestEndEvent != null)
            {
                TestEndEvent(this);
            }
        }

        public event Action<AServerTester> TestEndEvent;
    }
}