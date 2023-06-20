using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using System.Threading;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class WTestBotMarketDepth : BotPanel
    {
        public WTestBotMarketDepth(string name, StartProgram startProgram) : base(name, startProgram)
        {
            StrategyParameterButton button = CreateParameterButton("StartTest");
            button.UserClickOnButtonEvent += Button_UserClickOnButtonEvent;
        }

        public override string GetNameStrategyType()
        {
            return "WTestBotMarketDepth";
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

        private bool _threadIsWork;

        private void WorkerThreadArea()
        {
            _threadIsWork = true;

            SendNewLogMessage("Market depth tests started. Whait 5 minutes", LogMessageType.Error);


            List<IServer> servers = ServerMaster.GetServers();

            for(int i = 0;servers == null && i < servers.Count;i++)
            {


            }


            SendNewLogMessage("Market depth tests started. Whait 5 minutes", LogMessageType.Error);
            _threadIsWork = false;
        }


    }

    public class MarketDepthTester
    {
        private MarketDepthTester()
        {

        }



        public event Action<string, LogMessageType> LogMessage;
    }
}
