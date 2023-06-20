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
            StrategyParameterButton button = CreateParameterButton("Start Test");
            button.UserClickOnButtonEvent += Button_UserClickOnButtonEvent;

            SecuriteisTestIsOn = CreateParameter("Securities test is on", true);

            MarketDepthTestIsOn = CreateParameter("Market Depth test is on", true);

        }

        StrategyParameterBool SecuriteisTestIsOn;

        StrategyParameterBool MarketDepthTestIsOn;

        public override string GetNameStrategyType()
        {
            return "WServerTester";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private void Button_UserClickOnButtonEvent()
        {
            if(_threadIsWork == true)
            {
                return;
            }

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

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
                if(SecuriteisTestIsOn.ValueBool == true)
                {
                    SecuritiesTester tester = new SecuritiesTester();
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = servers[i];
                    SendNewLogMessage("Securities tests started", LogMessageType.Error);
                    tester.Start();
                }

                if(MarketDepthTestIsOn.ValueBool == true)
                {
                    MarketDepthTester tester = new MarketDepthTester();
                    tester.LogMessage += SendNewLogMessage;
                    tester.TestEndEvent += Tester_TestEndEvent;
                    _testers.Add(tester);
                    tester.Server = servers[i];
                    SendNewLogMessage("Market depth tests started", LogMessageType.Error);
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

    public abstract class AServerTester
    {
        public IServer Server
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
                report += "Erors: \n";

                for(int i = 0;i < _errors.Count;i++)
                {
                    report += (i+1) + "  " +  _errors[i] + "\n";
                }
            }

            return report;
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

        public IServer _myServer;

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
