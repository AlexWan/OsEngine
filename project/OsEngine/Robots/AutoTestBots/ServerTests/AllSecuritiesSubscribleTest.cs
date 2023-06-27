using OsEngine.Logging;
using OsEngine.Market.Connectors;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Threading;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class AllSecuritiesSubscribleTest : AServerTester
    {
        public BotTabScreener _screener;
        private DateTime TimeOut;

        public int countMinutesToTimeOut;
        public bool IsNeedToLoadAllSecurities;
        public int CountToLoadTabs;


        public override void Process()
        {
            TimeOut = DateTime.Now.AddMinutes(countMinutesToTimeOut);
            _screener.NeadToReloadTabs = true;
            _screener.ServerType = Server.ServerType;
            _screener.PortfolioName = Server.Portfolios[0].Number;


            var securities = new List<ActivatedSecurity>();


            int countToLoadTabs = Server.Securities.Count;
            if (IsNeedToLoadAllSecurities == false)
            {
                countToLoadTabs = CountToLoadTabs;
            }

            for (int i = 0; i < countToLoadTabs; i++)
            {
                securities.Add(new ActivatedSecurity()
                {
                    IsOn = true,
                    SecurityName = Server.Securities[i].Name,
                    SecurityClass = Server.Securities[i].NameClass
                });
            }
            _screener.SecuritiesNames = securities;
            

            while (true)
            {
                bool IsTimeOut = CheckIsTimeOut();

                if (IsTimeOut == true)
                {
                    SetNewError("Time Is Out.");
                    break;
                }

                Thread.Sleep(10000);

                bool IsAllLoadSec = true;

                if (_screener.Tabs == null ||
                    _screener.Tabs.Count == 0 ||
                    _screener.Tabs.Count != countToLoadTabs)
                {
                    continue;
                }

                for (int i = 0; i < _screener.Tabs.Count; i++)
                {
                    if (_screener.Tabs[i].PriceBestAsk == null  ||
                        _screener.Tabs[i].PriceBestAsk == null ||
                        _screener.Tabs[i].Trades == null )
                    {
                        IsAllLoadSec = false;
                        break;
                    }
                    if (_screener.Tabs[i].Trades.Count == 0 ||
                        _screener.Tabs[i].PriceBestAsk == 0 ||
                        _screener.Tabs[i].PriceBestAsk == 0)
                    {
                        IsAllLoadSec = false;
                        break;
                    }
                }

                if (IsAllLoadSec == true)
                {
                    break;
                }
            }

            string serviceInfo = IncrementToLoadTabs();
            SetNewServiceInfo(serviceInfo);
            TestEnded();
        }

        private bool CheckIsTimeOut()
        {

            if (DateTime.Now > TimeOut)
            {
                return true;
            }


            return false;
        }

        private string IncrementToLoadTabs()
        {
            int CounterToLoad = 0;
            int CountToFail = 0;

            if (_screener.Tabs == null ||
                    _screener.Tabs.Count == 0)
            {
                SetNewError("Tabs is not upload");
                return "Fail To Load";
            }

            for (int i = 0; i < _screener.Tabs.Count; i++)
            {
                if (_screener.Tabs[i].PriceBestAsk == null ||
                    _screener.Tabs[i].PriceBestAsk == null ||
                    _screener.Tabs[i].Trades == null)
                {
                    CountToFail++;
                    continue;
                }
                else if (_screener.Tabs[i].Trades.Count == 0 ||
                    _screener.Tabs[i].PriceBestAsk == 0 ||
                    _screener.Tabs[i].PriceBestAsk == 0)
                {
                    CountToFail++;
                    continue;
                }
                else
                {
                    CounterToLoad++;
                }
            }

            return $"To Load Tabs: {CounterToLoad}; Fail Load Tabs: {CountToFail}";
        }

        public void ErrorMessageServer(string message, LogMessageType logMessageType)
        {
            if (logMessageType == LogMessageType.Error)
            {
                SetNewError(message);
            }
        }
    }
}
