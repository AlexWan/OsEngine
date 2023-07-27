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
        public WServerTester(string name, StartProgram startProgram) : base(name, startProgram)
        {
            StrategyParameterButton buttonSecTests = CreateParameterButton("Start test sec", "V1");
            buttonSecTests.UserClickOnButtonEvent += ButtonSecTests_UserClickOnButtonEvent;

            StrategyParameterButton buttonMarketDepth = CreateParameterButton("Start test md", "V2");
            buttonMarketDepth.UserClickOnButtonEvent += ButtonMarketDepth_UserClickOnButtonEvent;
            MarketDepthSecToTestCount = CreateParameter("Securities count", 5, 5, 5, 1, "V2");
            MarketDepthMinutesToTest = CreateParameter("Md tester work time minutes", 5, 5, 5, 1, "V2");

            StrategyParameterButton buttonDataTest1 = CreateParameterButton("Start test data 1", "D1");
            buttonDataTest1.UserClickOnButtonEvent += ButtonDataTest1_UserClickOnButtonEvent;
            SecurityNameDataTest1 = CreateParameter("Sec name data test 1", "ADAUSDT","D1");

            StrategyParameterButton buttonDataTest2 = CreateParameterButton("Start test data 2", "D2");
            buttonDataTest2.UserClickOnButtonEvent += ButtonDataTest2_UserClickOnButtonEvent;
            SecurityNameDataTest2 = CreateParameter("Sec name data test 2", "ADAUSDT", "D2");
        }

        StrategyParameterInt MarketDepthSecToTestCount;
        StrategyParameterInt MarketDepthMinutesToTest;
        StrategyParameterString SecurityNameDataTest1;
        StrategyParameterString SecurityNameDataTest2;

        public override string GetNameStrategyType()
        {
            return "WServerTester";
        }

        public override void ShowIndividualSettingsDialog()
        {

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

        private ServerTestType CurTestType;

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
                    tester.MinutesToTest = MarketDepthMinutesToTest.ValueInt;
                    tester.CountSecuritiesToConnect = MarketDepthSecToTestCount.ValueInt;
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
                else if (CurTestType == ServerTestType.Data_1)
                {
                    Data_1 tester = new Data_1();
                    tester.SecName = SecurityNameDataTest1.ValueString;
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = (AServer)servers[i];
                    SendNewLogMessage("Tests started " + tester.GetType().Name + " " + servers[i].ServerType.ToString(), LogMessageType.Error);
                    tester.Start();
                }
                else if (CurTestType == ServerTestType.Data_2)
                {
                    Data_2 tester = new Data_2();
                    tester.SecName = SecurityNameDataTest2.ValueString;
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
    }

    public enum ServerTestType
    {
        Var_1_Securities,
        Var_2_MarketDepth,
        Data_1,
        Data_2,
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

        List<string> _serviceInfo = new List<string>();

        public void SetNewServiceInfo(string serviceInfo)
        {
            _serviceInfo.Add(serviceInfo);
        }

        List<string> _errors = new List<string>();

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

        public AServer _myServer;

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
