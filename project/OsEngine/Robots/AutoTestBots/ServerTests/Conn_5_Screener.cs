
using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Robots.Engines;
using System;
using System.Collections.Generic;
using System.Threading;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Conn_5_Screener : AServerTester
    {
        public string SecuritiesClass = "Futures";

        public int SecuritiesCount = 15;

        public string TimeFrame = "";

        public int MinutesToTest;

        public IServerPermission Permission;

        public override void Process()
        {
            AServer myServer = Server;

            if (myServer == null ||
                myServer.ServerStatus == ServerConnectStatus.Disconnect)
            {
                this.SetNewError("Error 1. Connection server status disconnect");
                TestEnded();
                return;
            }

            Permission = ServerMaster.GetServerPermission(myServer.ServerType);

            if (Permission == null)
            {
                this.SetNewError("Error 2. No serverPermission to server: " + myServer.ServerType);
                TestEnded();
                return;
            }

            if (SecuritiesCount < 15)
            {
                this.SetNewError("Error 3. You indicated the number of papers is less than 15. You can't do that.");
                TestEnded();
                return;
            }

            if (string.IsNullOrEmpty(SecuritiesClass))
            {
                this.SetNewError("Error 4. You did not specify the class of securities to be subscribed to");
                TestEnded();
                return;
            }

            Test();

            if (_botToShowDialog != null)
            {
                _botToShowDialog.Delete();
            }

            if (_screener != null)
            {
                _screener.Delete();
            }

            TestEnded();
        }

        private BotPanel _botToShowDialog;

        public void ShowDialog()
        {
            if (_screener == null)
            {
                return;
            }

            if (_botToShowDialog == null)
            {
                _botToShowDialog = new CandleEngine(DateTime.Now.Ticks.ToString(), StartProgram.IsOsTrader);
                _botToShowDialog.TabsScreener.Add(_screener);
                _botToShowDialog.GetTabs().Add(_screener);
            }

            _botToShowDialog.ShowChartDialog();
        }

        BotTabScreener _screener;

        private void Test()
        {
            Thread.Sleep(10000);

            // 1 проверяем тайм-фрейм на доступность 

            TimeFrame myTimeFrame;

            Enum.TryParse(TimeFrame, out myTimeFrame);

            if (myTimeFrame == Entity.TimeFrame.Min1
                && Permission.TradeTimeFramePermission.TimeFrameMin1IsOn == false)
            {
                this.SetNewError("Error 5. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Min2
            && Permission.TradeTimeFramePermission.TimeFrameMin2IsOn == false)
            {
                this.SetNewError("Error 6. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Min3
            && Permission.TradeTimeFramePermission.TimeFrameMin3IsOn == false)
            {
                this.SetNewError("Error 7. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Min5
            && Permission.TradeTimeFramePermission.TimeFrameMin5IsOn == false)
            {
                this.SetNewError("Error 8. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Min10
            && Permission.TradeTimeFramePermission.TimeFrameMin10IsOn == false)
            {
                this.SetNewError("Error 9. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Min15
            && Permission.TradeTimeFramePermission.TimeFrameMin15IsOn == false)
            {
                this.SetNewError("Error 10. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Min20
            && Permission.TradeTimeFramePermission.TimeFrameMin20IsOn == false)
            {
                this.SetNewError("Error 11. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Min20
            && Permission.TradeTimeFramePermission.TimeFrameMin20IsOn == false)
            {
                this.SetNewError("Error 12. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Min30
            && Permission.TradeTimeFramePermission.TimeFrameMin30IsOn == false)
            {
                this.SetNewError("Error 13. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Min45
            && Permission.TradeTimeFramePermission.TimeFrameMin45IsOn == false)
            {
                this.SetNewError("Error 14. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Hour1
            && Permission.TradeTimeFramePermission.TimeFrameHour1IsOn == false)
            {
                this.SetNewError("Error 15. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Hour2
            && Permission.TradeTimeFramePermission.TimeFrameHour2IsOn == false)
            {
                this.SetNewError("Error 16. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Hour4
            && Permission.TradeTimeFramePermission.TimeFrameHour4IsOn == false)
            {
                this.SetNewError("Error 17. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Day
            && Permission.TradeTimeFramePermission.TimeFrameDayIsOn == false)
            {
                this.SetNewError("Error 18. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Sec1
            && Permission.TradeTimeFramePermission.TimeFrameSec1IsOn == false)
            {
                this.SetNewError("Error 19. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Sec2
            && Permission.TradeTimeFramePermission.TimeFrameSec2IsOn == false)
            {
                this.SetNewError("Error 20. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Sec5
            && Permission.TradeTimeFramePermission.TimeFrameSec5IsOn == false)
            {
                this.SetNewError("Error 21. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Sec10
            && Permission.TradeTimeFramePermission.TimeFrameSec10IsOn == false)
            {
                this.SetNewError("Error 22. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Sec15
            && Permission.TradeTimeFramePermission.TimeFrameSec15IsOn == false)
            {
                this.SetNewError("Error 23. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Sec20
            && Permission.TradeTimeFramePermission.TimeFrameSec20IsOn == false)
            {
                this.SetNewError("Error 24. No permission to this timeFrame: " + myTimeFrame);
                return;
            }
            else if (myTimeFrame == Entity.TimeFrame.Sec30
            && Permission.TradeTimeFramePermission.TimeFrameSec30IsOn == false)
            {
                this.SetNewError("Error 25. No permission to this timeFrame: " + myTimeFrame);
                return;
            }

            // 2 берём портфель который нужно подключить

            List<Portfolio> portfolios = _myServer.Portfolios;

            if (_myServer.Portfolios.Count == 0)
            {
                this.SetNewError("Error 26. No portfolio in server.");
                return;
            }

            Portfolio myPortfolio = portfolios[0];

            // 3 берём бумаги которые надо подключить

            List<Security> securitiesAll = _myServer.Securities;

            List<Security> securitiesToStart = new List<Security>();

            for (int i = 0; securitiesAll != null && i < securitiesAll.Count; i++)
            {
                if (securitiesAll[i].NameClass == SecuritiesClass)
                {
                    securitiesToStart.Add(securitiesAll[i]);

                    if (securitiesToStart.Count >= SecuritiesCount)
                    {
                        break;
                    }
                }
            }

            if (securitiesToStart.Count < SecuritiesCount)
            {
                this.SetNewError(
                    "Error 27. There are no securities in the class you specified. Class: " + SecuritiesClass
                    + ". SecCount: " + securitiesToStart.Count);
                return;
            }

            StartScreener(securitiesToStart, myPortfolio, myTimeFrame);

            // ожидаем всех данных от скринера.

            DateTime timeStart = DateTime.Now;

            while (true)
            {
                Thread.Sleep(1000);

                if (timeStart.AddMinutes(MinutesToTest) < DateTime.Now)
                {
                    this.SetNewServiceInfo("End by time awaiting");
                    break;
                }

                bool allDataIsReady = true;

                for (int i = 0; i < _awaitableSecurities.Count; i++)
                {
                    AwaitableSecurities sec = _awaitableSecurities[i];

                    if (sec.TradesIsIncome == false
                        || sec.CandlesIsIncome == false
                        || sec.MarketDepthsIsIncome == false)
                    {
                        allDataIsReady = false;
                        break;
                    }
                }

                if (allDataIsReady)
                {
                    this.SetNewServiceInfo("End by ready all await data");
                    break;
                }
            }

            _testIsEnded = true;

            // записываем результаты того какие данные нам пришли

            for (int i = 0; i < _awaitableSecurities.Count; i++)
            {
                if (_awaitableSecurities[i].CandlesCount == 0)
                {
                    SetNewError("Error 28. No candles");
                    return;
                }

                if (_awaitableSecurities[i].MarketDepthsIncomeCount == 0)
                {
                    SetNewError("Error 29. No marketDepths");
                    return;
                }

                if (_awaitableSecurities[i].TradesIncomeCount == 0)
                {
                    SetNewError("Error 30. No trades");
                    return;
                }

                this.SetNewServiceInfo(_awaitableSecurities[i].GetReportString());
            }
        }

        private void StartScreener(List<Security> securities,
            Portfolio portfolio, TimeFrame timeFrame)
        {
            BotTabScreener newScreener = new BotTabScreener(DateTime.Now.Ticks.ToString(), StartProgram.IsOsTrader);

            _screener = newScreener;
            _screener.CandleUpdateEvent += _screener_CandleUpdateEvent;
            _screener.MarketDepthUpdateEvent += _screener_MarketDepthUpdateEvent;
            _screener.NewTickEvent += _screener_NewTickEvent;

            newScreener.ServerType = Server.ServerType;
            newScreener.PortfolioName = portfolio.Number; // добавить проверку подключен ли портфель

            List<ActivatedSecurity> securitiesToScreener = new List<ActivatedSecurity>();

            for (int i = 0; i < securities.Count; i++)
            {
                ActivatedSecurity newSec = new ActivatedSecurity();
                newSec.IsOn = true;
                newSec.SecurityName = securities[i].Name;
                newSec.SecurityClass = securities[i].NameClass;

                securitiesToScreener.Add(newSec);

                AwaitableSecurities newSecA = new AwaitableSecurities();
                newSecA.TimeFrame = timeFrame;
                newSecA.Security = securities[i];
                _awaitableSecurities.Add(newSecA);
            }

            newScreener.SecuritiesNames = securitiesToScreener;

            _screener.NeedToReloadTabs = true;
        }

        List<AwaitableSecurities> _awaitableSecurities = new List<AwaitableSecurities>();

        private bool _testIsEnded;

        private void _screener_NewTickEvent(Trade trade, BotTabSimple tab)
        {
            if (_testIsEnded)
            {
                return;
            }
            AwaitableSecurities mySec = null;

            for (int i = 0; i < _awaitableSecurities.Count; i++)
            {
                if (_awaitableSecurities[i].Security.Name == tab.Security.Name
                    && _awaitableSecurities[i].Security.NameClass == tab.Security.NameClass)
                {
                    mySec = _awaitableSecurities[i];
                    break;
                }
            }

            if (mySec == null)
            {
                return;
            }

            mySec.TradesIsIncome = true;
            mySec.TradesIncomeCount++;

            CheckTrade(trade);
        }

        private void _screener_MarketDepthUpdateEvent(MarketDepth md, BotTabSimple tab)
        {
            if (_testIsEnded)
            {
                return;
            }
            AwaitableSecurities mySec = null;

            for (int i = 0; i < _awaitableSecurities.Count; i++)
            {
                if (_awaitableSecurities[i].Security.Name == tab.Security.Name
                    && _awaitableSecurities[i].Security.NameClass == tab.Security.NameClass)
                {
                    mySec = _awaitableSecurities[i];
                    break;
                }
            }

            if (mySec == null)
            {
                return;
            }
            mySec.MarketDepthsIncomeCount++;
            mySec.MarketDepthsIsIncome = true;
            CheckMd(md);
        }

        private void _screener_CandleUpdateEvent(List<Candle> candles, BotTabSimple tab)
        {
            if (_testIsEnded)
            {
                return;
            }

            if (tab.TimeFrameBuilder.TimeFrame.ToString() != TimeFrame)
            {
                return;
            }

            AwaitableSecurities mySec = null;

            for (int i = 0; i < _awaitableSecurities.Count; i++)
            {
                if (_awaitableSecurities[i].Security.Name == tab.Security.Name
                    && _awaitableSecurities[i].Security.NameClass == tab.Security.NameClass)
                {
                    mySec = _awaitableSecurities[i];
                    break;
                }
            }

            if (mySec == null)
            {
                return;
            }

            mySec.CandlesIsIncome = true;
            mySec.CandlesCount = candles.Count;

            CheckCandles(candles, tab.TimeFrameBuilder.TimeFrame);
        }

        // методы проверки

        List<List<Trade>> _trades = new List<List<Trade>>();

        private void CheckTrade(Trade newTrade)
        {
            if (string.IsNullOrEmpty(newTrade.SecurityNameCode))
            {
                SetNewError("Trades Error 31. No Security Name");
                return;
            }

            if (newTrade.Side == Side.None)
            {
                SetNewError("Trades Error 32. No Trade SIDE. Sec name " + newTrade.SecurityNameCode);
                return;
            }

            if (string.IsNullOrEmpty(newTrade.Id))
            {
                SetNewError("Trades Error 33. No Trade Id. Sec name " + newTrade.SecurityNameCode);
                return;
            }

            if (newTrade.Price <= 0)
            {
                SetNewError("Trades Error 34. Bad Trade Price. Sec name "
                + newTrade.SecurityNameCode
                    + " Price: " + newTrade.Price);
                return;
            }

            if (newTrade.Volume <= 0)
            {
                SetNewError("Trades Error 35. Bad Trade Volume. Sec name "
                + newTrade.SecurityNameCode
                    + " Volume: " + newTrade.Volume);
                return;
            }

            if (newTrade.Time == DateTime.MinValue)
            {
                SetNewError("Trades Error 36. Bad Trade TIME. Sec name "
                    + newTrade.SecurityNameCode);
                return;
            }

            // проверяем последние данные по этой бумаге

            Trade previousTrade = null;

            for (int i = 0; i < _trades.Count; i++)
            {
                if (_trades[i][0].SecurityNameCode == newTrade.SecurityNameCode)
                {
                    previousTrade = _trades[i][0];
                    break;
                }
            }

            if (previousTrade != null)
            {
                if (previousTrade.Time > newTrade.Time)
                {
                    SetNewError("Trades Error 37. Previous trade time is greater than the current time. Sec name "
                        + newTrade.SecurityNameCode);
                    return;
                }

                for (int i = 0; i < _trades.Count; i++)
                {
                    if (_trades[i][0].SecurityNameCode == newTrade.SecurityNameCode)
                    {
                        _trades[i].Add(newTrade);
                        break;
                    }
                }

            }
            else
            {
                _trades.Add(new List<Trade> { newTrade });
            }
        }

        List<MarketDepth> _md = new List<MarketDepth>();

        private void CheckMd(MarketDepth md)
        {
            // Базовые проверки
            if (md.Bids == null ||
                md.Asks == null)
            {
                SetNewError("MD Error 38. null in bids or asks array");
                return;
            }
            if (md.Bids.Count == 0 ||
                md.Asks.Count == 0)
            {
                SetNewError("MD Error 39. Zero count in bids or asks array");
                return;
            }

            if (md.Bids.Count > 25 ||
                md.Asks.Count > 25)
            {
                SetNewError("MD Error 40. Count in bids or asks more 25 lines");
                return;
            }

            if (string.IsNullOrEmpty(md.SecurityNameCode))
            {
                SetNewError("MD Error 41. Security name is null or empty");
                return;
            }

            for (int i = 0; i < md.Bids.Count; i++)
            {
                if (md.Bids[i] == null)
                {
                    SetNewError("MD Error 42. Bids array have null level");
                    return;
                }
                if (md.Bids[i].Ask != 0)
                {
                    SetNewError("MD Error 43. Ask in bids array is note zero");
                    return;
                }
                if (md.Bids[i].Bid == 0)
                {
                    SetNewError("MD Error 44. Bid in bids array is zero");
                    return;
                }
            }

            for (int i = 0; i < md.Asks.Count; i++)
            {
                if (md.Asks[i] == null)
                {
                    SetNewError("MD Error 45. Asks array have null level");
                    return;
                }
                if (md.Asks[i].Bid != 0)
                {
                    SetNewError("MD Error 46. Bid in asks array is note zero");
                    return;
                }
                if (md.Asks[i].Ask == 0)
                {
                    SetNewError("MD Error 47. Ask in asks array is zero");
                    return;
                }
            }

            // проверка времени

            if (md.Time == DateTime.MinValue)
            {
                SetNewError("MD Error 48. Time is min value");
            }

            MarketDepth oldDepth = null;

            for (int i = 0; i < _md.Count; i++)
            {
                if (_md[i].SecurityNameCode == md.SecurityNameCode)
                {
                    oldDepth = _md[i];
                }
            }

            if (oldDepth != null && oldDepth.Time == md.Time)
            {
                SetNewError("MD Error 49. Time in md is note change");
            }

            bool isSaved = false;

            for (int i = 0; i < _md.Count; i++)
            {
                if (_md[i].SecurityNameCode == md.SecurityNameCode)
                {
                    _md[i] = md;
                    isSaved = true;
                    break;
                }
            }

            if (isSaved == false)
            {
                _md.Add(md);
            }

            for (int i = 1; i < md.Bids.Count; i++)
            {
                if (md.Bids[i].Price == 0)
                {
                    SetNewError("MD Error 50. Bids[i] price == 0");
                    return;
                }
            }

            for (int i = 1; i < md.Asks.Count; i++)
            {
                if (md.Asks[i].Price == 0)
                {
                    SetNewError("MD Error 51. Asks[i] price == 0");
                    return;
                }
            }

            // проверка массивов Bids и Asks на запутанность

            if (md.Bids[0].Price >= md.Asks[0].Price)
            {
                SetNewError("MD Error 52. Bid price >= Ask price");
                return;
            }

            for (int i = 1; i < md.Bids.Count; i++)
            {
                // Bids – уровни заявок на покупку.
                // 0 индекс самый высокий.И далее, чем больше индекс тем меньше цена

                if (md.Bids[i].Price == md.Bids[i - 1].Price)
                {
                    SetNewError("MD Error 53. Bids[i] price == Bids[i-1] price");
                }

                if (md.Bids[i].Price > md.Bids[i - 1].Price)
                {
                    SetNewError("MD Error 54. Bids[i] price > Bids[i-1] price");
                }
            }

            for (int i = 1; i < md.Asks.Count; i++)
            {
                // Asks – уровни заявок на продажу.
                // 0 индекс самый низкий.И далее, чем больше индекс тем выше цена

                if (md.Asks[i].Price == md.Asks[i - 1].Price)
                {
                    SetNewError("MD Error 55. Asks[i] price == Asks[i-1] price");
                }

                if (md.Asks[i].Price < md.Asks[i - 1].Price)
                {
                    SetNewError("MD Error 56. Asks[i] price < Asks[i-1] price");
                }
            }

            for (int i = 0; i < md.Bids.Count; i++)
            {
                // 5.1.10.	С одинаковой ценой не может быть несколько уровней

                MarketDepthLevel curLevel = md.Bids[i];

                for (int j = 0; j < md.Bids.Count; j++)
                {
                    if (j == i)
                    {
                        continue;
                    }

                    if (curLevel.Price == md.Bids[j].Price)
                    {
                        SetNewError("MD Error 57. bids with same price");
                    }
                }
            }

            for (int i = 0; i < md.Asks.Count; i++)
            {
                // 5.1.10.	С одинаковой ценой не может быть несколько уровней

                MarketDepthLevel curLevel = md.Asks[i];

                for (int j = 0; j < md.Asks.Count; j++)
                {
                    if (j == i)
                    {
                        continue;
                    }

                    if (curLevel.Price == md.Asks[j].Price)
                    {
                        SetNewError("MD Error 58. Asks with same price");
                    }
                }
            }
        }

        private void CheckCandles(List<Candle> candles, TimeFrame timeFrame)
        {
            if (candles == null)
            {
                SetNewError("Error 59. Array is null. " + timeFrame.ToString());
                return;
            }

            if (candles.Count == 0)
            {
                SetNewError("Error 60. Array is empty. " + timeFrame.ToString());
                return;
            }

            // 2 правильно ли расположено время в массиве. Сначала - старые данные. К концу массива - новые.
            // 3 нет ли задвоения свечек

            for (int i = 1; i < candles.Count; i++)
            {
                Candle candleNow = candles[i];
                Candle candleLast = candles[i - 1];

                if (candleLast.TimeStart > candleNow.TimeStart)
                {
                    SetNewError("Error 61. The time in the old candle is big than in the current candle " + timeFrame.ToString());
                    return;
                }

                if (candleLast.TimeStart == candleNow.TimeStart)
                {
                    SetNewError("Error 62. Candle time is equal!" + timeFrame.ToString());
                    return;
                }
            }

            // 4 ошибка если open ниже лоя или выше хая
            // 5 ошибка если close ниже лоя или выше хая
            // 6 ошибка если OHLC равен нулю

            for (int i = 0; i < candles.Count; i++)
            {
                Candle candleNow = candles[i];

                if (candleNow.Open > candleNow.High)
                {
                    SetNewError("Error 63. Candle open above the high" + timeFrame.ToString());
                    return;
                }
                if (candleNow.Open < candleNow.Low)
                {
                    SetNewError("Error 64. Candle open below the low" + timeFrame.ToString());
                    return;
                }

                if (candleNow.Close > candleNow.High)
                {
                    SetNewError("Error 65. Candle Close above the high" + timeFrame.ToString());
                    return;
                }
                if (candleNow.Close < candleNow.Low)
                {
                    SetNewError("Error 66. Candle Close below the low" + timeFrame.ToString());
                    return;
                }

                if (candleNow.Open == 0)
                {
                    SetNewError("Error 67. Candle Open is zero" + timeFrame.ToString());
                    return;
                }

                if (candleNow.High == 0)
                {
                    SetNewError("Error 68. Candle High is zero" + timeFrame.ToString());
                    return;
                }

                if (candleNow.Low == 0)
                {
                    SetNewError("Error 69. Candle Low is zero" + timeFrame.ToString());
                    return;
                }

                if (candleNow.Close == 0)
                {
                    SetNewError("Error 70. Candle Close is zero" + timeFrame.ToString());
                    return;
                }

                if (candleNow.Open == candleNow.High
                    && candleNow.High == candleNow.Low
                    && candleNow.Low == candleNow.Close
                    && candleNow.Volume == 0)
                {
                    // всё нормально. Некоторые биржи так закрывают пробелы в данных
                }
                else if (candleNow.Volume == 0)
                {
                    SetNewError("Error 71. Candle Volume is zero" + timeFrame.ToString());
                    return;
                }
            }
        }
    }

    public class AwaitableSecurities
    {
        public Security Security;

        public TimeFrame TimeFrame;

        public bool CandlesIsIncome;

        public int CandlesCount;

        public bool TradesIsIncome;

        public int TradesIncomeCount;

        public bool MarketDepthsIsIncome;

        public int MarketDepthsIncomeCount;

        public string GetReportString()
        {

            string report = Security.Name + " " + Security.NameClass + " " + TimeFrame.ToString() + " || ";

            report += "Candles: " + CandlesIsIncome + "  count: " + CandlesCount + " || ";

            report += "Trades: " + TradesIsIncome + "  сount: " + TradesIncomeCount + " || ";

            report += "Md: " + MarketDepthsIsIncome + "  count: " + MarketDepthsIncomeCount;

            return report;
        }
    }
}