using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

namespace OsEngine.Market.Servers.SmartLabNews
{
    public class SmartLabNewsServer : AServer
    {
        public SmartLabNewsServer()
        {
            SmartLabNewsServerRealization realization = new SmartLabNewsServerRealization();
            ServerRealization = realization;

            CreateParameterBoolean("Stocks and companies", false);
            CreateParameterBoolean("Bonds", false);
            CreateParameterBoolean("Cryptocurrency", false);
            CreateParameterBoolean("FOREX", false);
            CreateParameterBoolean("Options", false);
            CreateParameterBoolean("Trading signals", false);
            CreateParameterBoolean("Information disclosure", false);
        }
    }

    public class SmartLabNewsServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public SmartLabNewsServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadForPublicMessages = new Thread(NewsReader);
            threadForPublicMessages.IsBackground = true;
            threadForPublicMessages.Name = "NewsReader";
            threadForPublicMessages.Start();
        }

        public DateTime ServerTime { get; set; }

        public void Connect(WebProxy proxy)
        {
            try
            {
                _useStocks = ((ServerParameterBool)ServerParameters[0]).Value;
                _useBonds = ((ServerParameterBool)ServerParameters[1]).Value;
                _useCrypto = ((ServerParameterBool)ServerParameters[2]).Value;
                _useForex = ((ServerParameterBool)ServerParameters[3]).Value;
                _useOptions = ((ServerParameterBool)ServerParameters[4]).Value;
                _useSignals = ((ServerParameterBool)ServerParameters[5]).Value;
                _useDisclosure = ((ServerParameterBool)ServerParameters[6]).Value;

                string[] tagsChannels = new string[]
                {
                    "stock=company",
                    "thematic=облигац",
                    "thematic=крипт",
                    "thematic=форекс",
                    "thematic=опцион",
                    "thematic=торговые сигналы",
                    "open=info"
                };

                for (int i = 0; i < 7; i++)
                {
                    if (((ServerParameterBool)ServerParameters[i]).Value)
                        _selectedСhannelTags.Add(tagsChannels[i]);
                }

                if (_selectedСhannelTags.Count == 0)
                {
                    SendLogMessage("Connection is not possible. No channel is selected", LogMessageType.Error);
                    return;
                }

                if (_useStocks)
                {
                    _availableSources.Add(_stocksUrl);
                }

                if (_useDisclosure)
                {
                    _availableSources.Add(_disclosureUrl);
                }

                if (_useBonds || _useCrypto || _useForex || _useOptions || _useSignals)
                {
                    _availableSources.Add(_allPostsUrl);
                }

                for (int i = 0; i < _availableSources.Count; i++)
                {
                    SyndicationFeed feed = GetFeed(_availableSources[i]);

                    if (feed == null)
                    {
                        SendLogMessage($"Couldn't access the URL: {_availableSources[i]}", LogMessageType.Error);

                        if (ServerStatus != ServerConnectStatus.Disconnect)
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                        }

                        return;
                    }
                    else
                    {
                        for (int j = 0; j < _selectedСhannelTags.Count; j++)
                        {
                            News news = GetNews(feed, _selectedСhannelTags[j], "start");

                            if (news != null)
                            {
                                _newsList.Add(news);
                                _additionsSum++;
                            }
                        }
                    }
                }

                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent();
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
                SendLogMessage("Couldn't access the Smart-Lab.ru channels", LogMessageType.Error);

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
            _selectedСhannelTags.Clear();
            _availableSources.Clear();

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public ServerType ServerType
        {
            get { return ServerType.SmartLabNews; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private bool _useStocks;
        private bool _useBonds;
        private bool _useCrypto;
        private bool _useForex;
        private bool _useOptions;
        private bool _useSignals;
        private bool _useDisclosure;
        private bool _newsIsSubscribed = false;

        private string _allPostsUrl = "https://smart-lab.ru/rss/flow/";
        private string _stocksUrl = "https://smart-lab.ru/news/rss/";
        private string _disclosureUrl = "https://smart-lab.ru/disclosure/rss/";

        private int _additionsSum;

        private List<News> _newsList = new List<News>();
        private List<string> _selectedСhannelTags = new List<string>();
        private List<string> _availableSources = new List<string>();

        private Dictionary<string, string> _sourceNames = new Dictionary<string, string>()
        {
            {"company", "Новости акций и компаний"},
            {"облигац", "Облигации"},
            { "крипт", "Криптовалюты"},
            { "форекс", "Форекс"},
            { "опцион", "Опционы" },
            { "торговые сигналы", "Торговые сигналы" },
            { "info", "Раскрытие информации" }
        };

        #endregion

        #region 3 News subscribe

        public bool SubscribeNews()
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return false;
            }

            _newsIsSubscribed = true;

            return true;
        }

        #endregion

        #region 4 SmartLab News parsing

        private void NewsReader()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect || !_newsIsSubscribed)
                    {
                        Thread.Sleep(3000);
                        continue;
                    }

                    for (int i = 0; i < _availableSources.Count; i++)
                    {
                        SyndicationFeed feed = GetFeed(_availableSources[i]);

                        for (int j = 0; j < _selectedСhannelTags.Count; j++)
                        {
                            News news = GetNews(feed, _selectedСhannelTags[j], "work");

                            if (news != null)
                            {
                                _additionsSum++;
                                _newsList.Add(news);
                            }
                        }
                    }

                    for (int i = _newsList.Count - 1; _additionsSum > 0; i--)
                    {
                        NewsEvent(_newsList[i]);
                        _additionsSum--;
                    }

                    Thread.Sleep(30000);

                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
                    SendLogMessage("Couldn't access the Smart-Lab.ru channels", LogMessageType.Error);

                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                }
            }
        }

        private SyndicationFeed GetFeed(string feedUrl)
        {
            try
            {
                XmlReader _reader = XmlReader.Create(feedUrl);

                SyndicationFeed feed = SyndicationFeed.Load(_reader);

                _reader.Close();
                return feed;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        private News GetNews(SyndicationFeed feed, string tag, string stage)
        {
            string[] themaAndTag = tag.Split('=');

            string newsText = string.Empty;

            List<string> tags = new List<string>();

            IEnumerator<SyndicationItem> enumerator = feed.Items.GetEnumerator();

            while (enumerator.MoveNext())
            {
                if (_newsList.Count > 0 && stage == "work")
                {
                    if (_newsList.Find(n => n.TimeMessage == enumerator.Current.PublishDate.DateTime) != null)
                        break;
                }

                News news = new News();

                news.TimeMessage = enumerator.Current.PublishDate.DateTime;
                news.Source = "smart-lab.ru" + "\n" + _sourceNames[themaAndTag[1]];
                newsText = enumerator.Current?.Title?.Text ?? "There is no news name";
                newsText += "\n";

                if (enumerator.Current != null && enumerator.Current.Categories.Count > 0)
                {
                    for (int i = 0; i < enumerator.Current.Categories.Count; i++)
                    {
                        if (enumerator.Current.Categories[i].Name != null)
                            tags.Add(enumerator.Current.Categories[i].Name);
                    }
                }

                if (themaAndTag[0] == "open")
                {
                    if (feed.Title.Text != "Лента раскрытия информации")
                        return null;

                    string fullNews = GetFullNews(enumerator.Current?.Id);
                    newsText += fullNews;
                    news.Value = newsText;
                }
                else if (themaAndTag[0] == "thematic")
                {
                    if (feed.Title.Text != "Поток записей на smart-lab.ru")
                        return null;

                    if (tags.Count > 0 && tags.Find(t => t.Contains(themaAndTag[1])) != null)
                    {
                        newsText += enumerator.Current?.Summary?.Text ?? "";
                        news.Value = CleanString(newsText);
                    }
                    else
                    {
                        news = null;
                        tags.Clear();
                        continue;
                    }
                }
                else if (themaAndTag[0] == "stock")
                {
                    if (feed.Title.Text != "Лента всех новостей акций")
                        return null;

                    string fullNews = GetFullNews(enumerator.Current?.Id);
                    newsText += fullNews;
                    news.Value = newsText;
                }

                return news;
            }

            return null;
        }

        private string GetFullNews(string urlPost)
        {
            string result = "Full content is not available";

            string tid = ExtractTidFromUrl(urlPost);

            if (string.IsNullOrEmpty(tid))
            {
                return result;
            }

            using (HttpClient client =  new HttpClient())
            {
                try
                {
                    string html = client.GetStringAsync(urlPost).GetAwaiter().GetResult();

                    string topicPattern = $@"<div class=""topic[^""]*""[^>]*tid=""{tid}""[^>]*>.*?<div class=""content"">(.*?)</div>";

                    Match match = Regex.Match(html, topicPattern, RegexOptions.Singleline);

                    if (match.Success)
                    {
                        string content = match.Groups[1].Value;
                        content = Regex.Replace(content, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);

                        string plainText = Regex.Replace(content, "<.*?>", string.Empty);
                        result = WebUtility.HtmlDecode(plainText.Trim());
                        return Regex.Replace(result, @"(\r?\n)+", "\r\n");
                    }
                    else
                    {
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage("Full content is not available" + ex.Message, LogMessageType.Error);
                    return result;
                }
            }
        }

        private string ExtractTidFromUrl(string url)
        {
            string pattern = @"/blog/(\d+)\.php";
            Match match = Regex.Match(url, pattern);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null;
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

            Regex regex = new Regex("&[^;]+;");
            MatchCollection matches = regex.Matches(input);

            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];

                string entity = match.Value.Substring(1, match.Value.Length - 2);

                try
                {
                    char decodedChar = WebUtility.HtmlDecode($"&{entity};")[0];
                    input = input.Replace(match.Value, decodedChar.ToString());
                }
                catch (Exception)
                {
                    SendLogMessage($"Не удалось убрать HTML-сущность: {match.Value}", LogMessageType.NoName);
                }
            }

            return Regex.Replace(input, @"(\r?\n)+", "\r\n");
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

        public void CancelAllOrders() { }

        public void CancelAllOrdersToSecurity(Security security) { }

        public bool CancelOrder(Order order) { return false; }

        public void ChangeOrderPrice(Order order, decimal newPrice) { }

        public void Subscribe(Security security) { }

        public void GetAllActivOrders() { }

        public List<Order> GetActiveOrders(int startIndex, int count) { return null; }

        public List<Order> GetHistoricalOrders(int startIndex, int count) { return null; }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime) { return null; }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount) { return null; }

        public OrderStateType GetOrderStatus(Order order) { return OrderStateType.None; }

        public void GetPortfolios()
        {
            Portfolio portfolio = new Portfolio();
            portfolio.ValueCurrent = 1;
            portfolio.Number = "SmartLabNews fake portfolio";

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

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime) { return null; }

        public void SendOrder(Order order) { }

        public event Action<List<Security>> SecurityEvent;
        public event Action<List<Portfolio>> PortfolioEvent;
        public event Action<MarketDepth> MarketDepthEvent { add { } remove { } }
        public event Action<Trade> NewTradesEvent { add { } remove { } }
        public event Action<Order> MyOrderEvent { add { } remove { } }
        public event Action<MyTrade> MyTradeEvent { add { } remove { } }
        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        public void SetLeverage(Security security, decimal leverage) { }

        #endregion
    }
}

