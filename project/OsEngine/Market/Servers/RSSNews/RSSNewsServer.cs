using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Generic;
using System.Net;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

namespace OsEngine.Market.Servers.RSSNews
{
    class RSSNewsServer : AServer
    {
        public RSSNewsServer(int uniqueNumber)
        {
            ServerNum = uniqueNumber;

            RSSNewsServerRealization realization = new RSSNewsServerRealization();
            ServerRealization = realization;

            CreateParameterString("RSS feeds URL", "");
            CreateParameterInt("Update period news (sec)", 30);
        }
    }

    public class RSSNewsServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public RSSNewsServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadForPublicMessages = new Thread(RSSChannelsReader);
            threadForPublicMessages.IsBackground = true;
            threadForPublicMessages.Name = "RSSReader";
            threadForPublicMessages.Start();
        }

        public DateTime ServerTime { get; set; }

        public void Connect(WebProxy proxy)
        {
            _rssUrlsString = ((ServerParameterString)ServerParameters[0]).Value;
            _updatePeriod = ((ServerParameterInt)ServerParameters[1]).Value;

            if (string.IsNullOrEmpty(_rssUrlsString))
            {
                SendLogMessage("Can`t run connector. No RSS Url feed are specified", LogMessageType.Error);
                return;
            }

            if (_updatePeriod <= 0)
            {
                SendLogMessage("Can`t run connector. The news update period parameter must be a positive number.", LogMessageType.Error);
                return;
            }

            string[] rssUrls = _rssUrlsString.Split(',');

            _rssReadSettings.DtdProcessing = DtdProcessing.Ignore;

            int countChannel = 0;

            for (int i = 0; i < rssUrls.Length; i++)
            {
                try
                {
                    _reader = XmlReader.Create(rssUrls[i], _rssReadSettings);

                    SyndicationFeed feed = SyndicationFeed.Load(_reader);

                    _reader.Close();

                    IEnumerator<SyndicationItem> enumerator = feed.Items.GetEnumerator();

                    while (enumerator.MoveNext() && i == countChannel)
                    {
                        News news = new News();

                        news.TimeMessage = enumerator.Current.PublishDate.DateTime;
                        news.Source = feed.Title.Text;

                        string newsText = enumerator.Current?.Title?.Text ?? "There is no news name";
                        newsText += "\n";
                        newsText += enumerator.Current?.Summary?.Text ?? "";

                        news.Value = CleanString(newsText);

                        _newsList.Add(news);

                        countChannel++;
                    }

                    SendLogMessage($"{feed.Title.Text} channel page was loaded", LogMessageType.Connect);

                    _availableUrls.Add(rssUrls[i]);

                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
                    SendLogMessage("Couldn't access the RSS feed " + rssUrls[i], LogMessageType.Error);

                    countChannel++;
                }
            }

            if (_availableUrls.Count > 0)
            {
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent();
            }
            else
            {
                SendLogMessage("Couldn't access the RSS feed", LogMessageType.Error);

                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }

        }

        public void Dispose()
        {
            _newsList.Clear();
            _availableUrls.Clear();

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public ServerType ServerType
        {
            get { return ServerType.RSSNews; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string _rssUrlsString;

        List<string> _availableUrls = new List<string>();

        private int _updatePeriod;

        private XmlReader _reader;

        private List<News> _newsList = new List<News>();

        private XmlReaderSettings _rssReadSettings = new XmlReaderSettings();

        private bool _newsIsSubscribed = false;

        private int _additionsSum;

        #endregion

        #region 3 News subscribe

        public bool SubscribeNews()
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return false;
            }

            _newsIsSubscribed = true;

            _additionsSum = _newsList.Count;

            return true;
        }

        #endregion

        #region 4 RSS page parsing

        private void RSSChannelsReader()
        {
            string newsText = string.Empty;

            _rssReadSettings.DtdProcessing = DtdProcessing.Ignore;

            while (true)
            {
                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect || !_newsIsSubscribed)
                    {
                        Thread.Sleep(3000);
                        continue;
                    }

                    for (int i = 0; i < _availableUrls.Count; i++)
                    {
                        _reader = XmlReader.Create(_availableUrls[i], _rssReadSettings);

                        SyndicationFeed feed = SyndicationFeed.Load(_reader);

                        _reader.Close();

                        IEnumerator<SyndicationItem> enumerator = feed.Items.GetEnumerator();

                        while (enumerator.MoveNext())
                        {
                            News news = new News();

                            news.TimeMessage = enumerator.Current.PublishDate.DateTime;
                            news.Source = feed.Title.Text;

                            newsText = enumerator.Current?.Title?.Text ?? "There is no news name";
                            newsText += "\n";
                            newsText += enumerator.Current?.Summary?.Text ?? "";

                            news.Value = CleanString(newsText);

                            if (_newsList.Find(n => n.TimeMessage == news.TimeMessage) != null)
                            {
                                break;
                            }
                            else
                            {
                                _newsList.Add(news);
                                _additionsSum++;
                            }
                        }

                        for (int k = _newsList.Count - 1; _additionsSum > 0; k--)
                        {
                            NewsEvent(_newsList[k]);

                            _additionsSum--;
                        }
                    }

                    Thread.Sleep(_updatePeriod * 1000);

                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
                    SendLogMessage("Couldn't access the RSS feed", LogMessageType.Error);

                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                }
            }
        }

        private string CleanString(string input)
        {
            string result;

            result = Regex.Replace(input, "<.*?>", string.Empty);

            return ReplaceHtmlEntities(result);
        }

        private string ReplaceHtmlEntities(string input)
        {
            if (input == null || input.Length == 0)
                return input;

            // Используем регулярное выражение для поиска всех сущностей вида "&...;"
            Regex regex = new Regex("&[^;]+;");
            MatchCollection matches = regex.Matches(input);

            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                // Получаем сущность без амперсанда и точки с запятой
                string entity = match.Value.Substring(1, match.Value.Length - 2);

                try
                {
                    // Пытаемся заменить сущность на соответствующий символ
                    char decodedChar = WebUtility.HtmlDecode($"&{entity};")[0];
                    input = input.Replace(match.Value, decodedChar.ToString());
                }
                catch (Exception)
                {
                    SendLogMessage($"Не удалось убрать HTML-сущность: {match.Value}", LogMessageType.NoName);
                }
            }

            return input;
        }

        public event Action<News> NewsEvent;

        #endregion

        #region 5 Log

        public event Action<string, LogMessageType> LogMessageEvent;

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
        }

        #endregion

        #region 6 Not used functions

        public void CancelAllOrders()
        {
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
        }

        public bool CancelOrder(Order order)
        {
            return false;
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
        }

        public void Subscribe(Security security)
        {
        }

        public void GetAllActivOrders()
        {
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            return null;
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            return null;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            return OrderStateType.None;
        }

        public void GetPortfolios()
        {
            Portfolio portfolio = new Portfolio();
            portfolio.ValueCurrent = 1;
            portfolio.Number = "RSSNews fake portfolio";

            if (PortfolioEvent != null)
            {
                PortfolioEvent(new List<Portfolio>() { portfolio });
            }
        }

        public void GetSecurities()
        {
            List<Security> securities = new List<Security>();

            Security fakeSec = new Security();
            fakeSec.Name = "Noname";
            fakeSec.NameId = "NonameId";
            fakeSec.NameClass = "NoClass";
            fakeSec.NameFull = "Nonamefull";

            securities.Add(fakeSec);

            SecurityEvent(securities);
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public void SendOrder(Order order)
        {
        }

        public event Action<List<Security>> SecurityEvent;
        public event Action<List<Portfolio>> PortfolioEvent;
        public event Action<MarketDepth> MarketDepthEvent { add { } remove { } }
        public event Action<Trade> NewTradesEvent { add { } remove { } }
        public event Action<Order> MyOrderEvent { add { } remove { } }
        public event Action<MyTrade> MyTradeEvent { add { } remove { } }
        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        #endregion
    }
}

