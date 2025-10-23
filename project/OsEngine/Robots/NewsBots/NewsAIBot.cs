/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


/* Description
Trading robot for OsEngine

Works with a news source and a screener in which LLM analyzes the news and decides whether to trade or not.
The LLM gets the role of a financial analyst and, 
based on the result of the news analysis, must give a short answer 
– the name of the ticker of the instrument and the direction of the transaction.
The ticker searches for the security in the screener and, if found, enters into the transaction.
Exit: via stop-loss or take-profit
 */

namespace OsEngine.Robots.NewsBots
{
    [Bot("NewsAIBot")]
    public class NewsAIBot : BotPanel
    {
        private BotTabScreener _tabScreener;

        // Basic Settings
        private StrategyParameterString _regime;

        public StrategyParameterDecimal _profitPercent;

        public StrategyParameterDecimal _stopPercent;

        // GetVolume Settings
        private StrategyParameterString _volumeType;

        private StrategyParameterDecimal _volume;

        private StrategyParameterString _tradeAssetInPortfolio;

        // IA settings
        private StrategyParameterString _promtLanguage;

        // GPTunnel settings
        private StrategyParameterCheckBox _useGPTunnel;

        private string _baseGPTUrl = "https://gptunnel.ru";

        private string _prefix = "/v1/";

        private StrategyParameterString _authGPTToken;

        private StrategyParameterString _balanceChose;

        private StrategyParameterString _authGigaKey;

        private StrategyParameterString _gptunnelModels;

        private Dictionary<string, string> _gptunnelModelsIds = new Dictionary<string, string>
        {
            { "GPT-4", "gpt-4" },
            { "GPT-4 Turbo", "gpt-4-turbo"},
            { "GPT-4o", "gpt-4o"},
            {"LLaMA 3.3 70B",  "llama-v3p3-70b"},
            {"LLaMA 4 Maverick", "llama4-maverick" },
            {"Mistral Medium 3", "mistral-medium-3" },
            {"Grok 3", "grok-3" },
            {"DeepSeek V3", "deepseek-3"},
            {"Qwen 3 30B", "qwen3-30b-a3b" }
        };

        // GigaChat settings
        private StrategyParameterCheckBox _useGigaChat;

        private StrategyParameterString _gigachatModels;

        private string _baseUrlGiga = "https://gigachat.devices.sberbank.ru/api";

        private string _gigaChatToken = string.Empty;

        DateTime _tokenExpirationTime = DateTime.MinValue;

        private Dictionary<string, string> _gigachatModelsIds = new Dictionary<string, string>
        {
            { "GigaChat Lite", "GigaChat" },
            { "GigaChat Pro", "GigaChat-Pro" },
            { "GigaChat Max", "GigaChat-Max" },
            { "GigaChat 2 Lite", "GigaChat-2" },
            { "GigaChat 2 Pro", "GigaChat-2-Pro" },
            { "GigaChat 2 Max", "GigaChat-2-Max" }
        };

        // assigning a role
        private string _roleRu = "Ты - успешный, профессиональный финансовый аналитик и трейдер. " +
            "Ты получаешь новости о биржах, публичных компаниях, акциях, экономическую статистику и анализируешь их." +
            " Делаешь однозначный вывод о росте или падении цены конкретного финансового инструмента";

        private string _roleEn = "You are a successful, professional financial analyst and trader. " +
            "You receive news about stock exchanges, public companies, stocks, economic statistics and analyze them. " +
            "You make an unambiguous conclusion about the rise or fall of the price of a specific financial instrument";

        // setting the task
        private string _promtRu = "После получения новости дай ответ о возможности заключить сделку по конкретному инструменту в следующем формате: " +
          "1. Напиши слово NO, если из новости невозможно сделать вывод о росте или падении цены у какого либо биржевого инструмента или нескольких инструментов.\r\n" +
          "2. Если из новости можно сделать вывод о росте или падении цены у какого либо биржевого инструмента или нескольких инструментов," +
          " напиши строку с параметрами сделки через запятую, для разных инструментов раздели через точку с запятой: тикер, Buy/Sell; Например: AFLT,Buy;SBRF,Sell; " +
          "Именной в таком формате, не пиши ничего лишнего" +
          "Новость: ";

        private string _promtEn = "After receiving the news, give an answer about the possibility of concluding a deal on a specific instrument in the following format: " +
            "1. Write the word NO if it is impossible to draw a conclusion from the news about the rise or fall of the price of any exchange instrument" +
            " or several instruments. 2. If it is possible to draw a conclusion from the news about the rise or " +
            "fall of the price of any exchange instrument or several instruments, " +
            "write a line with the parameters of the deal separated by commas, for different instruments, separate by semicolons: ticker, Buy/Sell; Example: AFLT,Buy;SBRF,Sell; " +
            "Write only in this format." +
            "News: ";

        public NewsAIBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.News);
            TabsNews[0].NewsEvent += NewsAIBot_NewsEvent;

            TabCreate(BotTabType.Screener);
            _tabScreener = TabsScreener[0];
            _tabScreener.PositionOpeningSuccesEvent += PositionOpeningSuccesEvent;

            ParametrsChangeByUser += NewsAIBot_ParametrsChangeByUser;

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");

            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Base");

            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4, "Base");

            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Base");

            _profitPercent = CreateParameter("Profit percent", 1.5m, 0, 20, 1m, "Base");

            _stopPercent = CreateParameter("Stop percent", 0.5m, 0, 20, 1m, "Base");

            // AI settings
            _promtLanguage = CreateParameter("Language for promt", "Eng", new[] { "Eng", "Rus" }, "AI settings");

            _useGPTunnel = CreateParameterCheckBox("Use GPTunnel", false, "AI settings");

            _gptunnelModels = CreateParameter("GPTunnel Models", "DeepSeek V3", new[] { "GPT-4", "GPT-4 Turbo", "GPT-4o", "LLaMA 3.3 70B", "LLaMA 4 Maverick", "Mistral Medium 3", "Grok 3",
                                      "DeepSeek V3", "Qwen 3 30B"}, "AI settings");

            _authGPTToken = CreateParameter("GPTunnel key", "", "AI settings");

            _balanceChose = CreateParameter("GPTunnel payment balance", "Private", new[] { "Private", "Buisness" }, "AI settings");

            _useGigaChat = CreateParameterCheckBox("Use GigaChat", true, "AI settings");

            _gigachatModels = CreateParameter("GigaChat Models", "GigaChat Lite", new[] { "GigaChat Lite", "GigaChat Pro", "GigaChat Max", "GigaChat 2 Lite", "GigaChat 2 Pro", "GigaChat 2 Max" }, "AI settings");

            _authGigaKey = CreateParameter("GigaChat key", "", "AI settings");


            ParamGuiSettings.SetBorderUnderParameter("Language for promt", System.Drawing.Color.LightGray, 4);

            ParamGuiSettings.SetBorderUnderParameter("GPTunnel payment balance", System.Drawing.Color.LightGray, 4);

            Description = OsLocalization.Description.DescriptionLabel52;
        }


        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "NewsAIBot";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // check parameters
        private void NewsAIBot_ParametrsChangeByUser()
        {
            if (_useGigaChat.CheckState == CheckState.Checked && _useGPTunnel.CheckState == CheckState.Checked
                || _useGigaChat.CheckState != CheckState.Checked && _useGPTunnel.CheckState != CheckState.Checked)
            {
                SendNewLogMessage("You need to choose one service: GPTunnel or GigaChat", Logging.LogMessageType.Error);

                if (_regime.ValueString != "Off")
                {
                    _regime.ValueString = "Off";
                    return;
                }

                return;
            }

            if (_useGigaChat.CheckState == CheckState.Checked && _authGigaKey.ValueString == "")
            {
                SendNewLogMessage("Enter the key from GigaChat", Logging.LogMessageType.Error);

                if (_regime.ValueString != "Off")
                {
                    _regime.ValueString = "Off";
                    return;
                }

                return;
            }

            if (_useGPTunnel.CheckState == CheckState.Checked && _authGPTToken.ValueString == "")
            {
                SendNewLogMessage("Enter the key from GPTunnel", Logging.LogMessageType.Error);

                if (_regime.ValueString != "Off")
                {
                    _regime.ValueString = "Off";
                    return;
                }

                return;
            }
        }

        // We received the news
        private void NewsAIBot_NewsEvent(News news)
        {
            if (_regime.ValueString == "Off" || _regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            // get or update GigaChat token
            if (_useGigaChat.CheckState == CheckState.Checked && (_gigaChatToken == string.Empty || _tokenExpirationTime < DateTime.Now))
            {
                GigaChatTokenResponse tokenResponse = GetAccessTokenAsync().Result;

                if (tokenResponse != null)
                {
                    _gigaChatToken = tokenResponse.AccessToken;
                    _tokenExpirationTime = DateTimeOffset.FromUnixTimeMilliseconds(tokenResponse.ExpiresAt).DateTime.ToLocalTime();
                }
                else
                {
                    SendNewLogMessage("Сouldn't get a token for GigaChat", Logging.LogMessageType.Error);
                    return;
                }
            }

            CheckNews(news.Value);
        }

        private void CheckNews(string news)
        {
            string answer = SendMessageToAIAsync(news).Result;

            if (answer != null && answer == "NO")
            {
                SendNewLogMessage("The news does not give a deal.", Logging.LogMessageType.User);
            }
            else if (answer != null && answer != "NO")
            {
                // if there is more than one security in the response
                string[] ans = answer.Split(';');

                if (ans.Length > 0)
                {
                    for (int i = 0; i < ans.Length; i++)
                    {
                        FindAndMakeDeal(ans[i]);
                    }
                }
                else
                {
                    FindAndMakeDeal(ans[0]);
                }
            }
        }

        /// <summary>
        /// Search for a security in the screener and enter into a transaction
        /// </summary>
        /// <param name="secDir">security ticker name and direction</param>
        private void FindAndMakeDeal(string secDir)
        {
            string[] secAndDir = secDir.Split(',');

            BotTabSimple tab = _tabScreener.Tabs.Find(p => p.Security.Name.Contains(secAndDir[0]));

            if (tab != null)
            {
                if (secAndDir[1] == "Buy")
                {
                    if (_regime.ValueString != "OnlyShort")
                    {
                        tab.ManualPositionSupport.DisableManualSupport();

                        tab.BuyAtMarket(GetVolume(tab));
                    }
                }
                else if (secAndDir[1] == "Sell")
                {
                    if (_regime.ValueString != "OnlyLong")
                    {
                        tab.ManualPositionSupport.DisableManualSupport();

                        tab.SellAtMarket(GetVolume(tab));
                    }
                }
                else
                {
                    SendNewLogMessage("The direction is not known", Logging.LogMessageType.Error);
                }
            }
            else
            {
                SendNewLogMessage("Security not found", Logging.LogMessageType.Error);
            }
        }

        /// <summary>
        /// Send news to GPTunnel or GigaChat
        /// </summary>
        private async Task<string> SendMessageToAIAsync(string news)
        {
            string url = _useGigaChat.CheckState == CheckState.Checked ? _baseUrlGiga : _baseGPTUrl;
            string token = _useGigaChat.CheckState == CheckState.Checked ? _gigaChatToken : _authGPTToken.ValueString;

            using HttpClient client = new();

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{url}{_prefix}chat/completions");

            // Добавляем заголовок авторизации
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            ChatRequest requestBody = null;

            if (_useGigaChat.CheckState == CheckState.Checked)
            {
                requestBody = new GigaChatRequest
                {
                    Model = _gigachatModelsIds[_gigachatModels.ValueString],
                    Messages = new List<Message>
                    {
                        new Message { Role = "system", Content = _promtLanguage.ValueString == "Rus" ? _roleRu : _roleEn },
                        new Message { Role = "user", Content = _promtLanguage.ValueString == "Rus" ? _promtRu + news : _promtEn + news }
                    },
                    Temperature = 0.001f,
                    Stream = false,
                    UpdateInterval = 0,
                    MaxTokens = 1000
                };
            }
            else
            {
                requestBody = new GPTunnelRequest
                {
                    Model = _gptunnelModelsIds[_gptunnelModels.ValueString],
                    UseWalletBalance = _balanceChose.ValueString == "Private",
                    Messages = new List<Message>
                    {
                        new Message { Role = "system", Content = _promtLanguage.ValueString == "Rus" ? _roleRu : _roleEn },
                        new Message { Role = "user", Content = _promtLanguage.ValueString == "Rus" ? _promtRu + news : _promtEn + news }
                    },
                    Temperature = 1
                };
            }

            try
            {
                JsonSerializerSettings jsonSettings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    ContractResolver = new DefaultContractResolver()
                };

                string jsonBody = JsonConvert.SerializeObject(requestBody, jsonSettings);

                requestMessage.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.SendAsync(requestMessage);

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();

                    ChatResponse responseString = JsonConvert.DeserializeObject<ChatResponse>(responseContent);

                    if (responseString.Choices != null)
                    {
                        SendNewLogMessage($"{responseString.Model} answer: {responseString.Choices[0].Message.Content}", Logging.LogMessageType.User);

                        return responseString.Choices[0].Message.Content;
                    }
                    else
                    {
                        SendNewLogMessage("Deserialize response error", Logging.LogMessageType.Error);
                        return null;
                    }
                }
                else
                {
                    SendNewLogMessage($"Error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}", Logging.LogMessageType.Error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Request error: {ex.Message}", Logging.LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// Set a stop and take after opening a position
        /// </summary>
        private void PositionOpeningSuccesEvent(Position position, BotTabSimple tab)
        {
            decimal stopPrice = 0;
            decimal profitOrderPrice = 0;

            if (position.Direction == Side.Buy)
            {
                stopPrice = position.EntryPrice - position.EntryPrice * (_stopPercent.ValueDecimal / 100);

                profitOrderPrice = position.EntryPrice + position.EntryPrice * (_profitPercent.ValueDecimal / 100);
            }
            else if (position.Direction == Side.Sell)
            {
                stopPrice = position.EntryPrice + position.EntryPrice * (_stopPercent.ValueDecimal / 100);

                profitOrderPrice = position.EntryPrice - position.EntryPrice * (_profitPercent.ValueDecimal / 100);
            }

            tab.CloseAtStopMarket(position, stopPrice);
            tab.CloseAtProfitMarket(position, profitOrderPrice);
        }

        /// <summary>
        /// Get GigaChat token
        /// </summary>
        public async Task<GigaChatTokenResponse> GetAccessTokenAsync()
        {
            string rqUid = Guid.NewGuid().ToString();

            using HttpClient httpClient = new HttpClient();

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://ngw.devices.sberbank.ru:9443/api/v2/oauth");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _authGigaKey.ValueString);
            request.Headers.Add("RqUID", rqUid);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            FormUrlEncodedContent content = new FormUrlEncodedContent(new[]
            {
               new KeyValuePair<string, string>("scope", "GIGACHAT_API_PERS")
            });

            request.Content = content;

            try
            {
                HttpResponseMessage response = await httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    GigaChatTokenResponse tokenResponse = JsonConvert.DeserializeObject<GigaChatTokenResponse>(responseContent);

                    return tokenResponse;
                }
                else
                {
                    SendNewLogMessage($"Error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}", Logging.LogMessageType.Error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Request error: {ex.Message}", Logging.LogMessageType.Error);
                return null;
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (_volumeType.ValueString == "Contracts")
            {
                volume = _volume.ValueDecimal;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = _volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = _volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (_volumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (_tradeAssetInPortfolio.ValueString == "Prime")
                {
                    portfolioPrimeAsset = myPortfolio.ValueCurrent;
                }
                else
                {
                    List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                    if (positionOnBoard == null)
                    {
                        return 0;
                    }

                    for (int i = 0; i < positionOnBoard.Count; i++)
                    {
                        if (positionOnBoard[i].SecurityNameCode == _tradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (_volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    if (tab.Security.UsePriceStepCostToCalculateVolume == true
                    && tab.Security.PriceStep != tab.Security.PriceStepCost
                    && tab.PriceBestAsk != 0
                    && tab.Security.PriceStep != 0
                    && tab.Security.PriceStepCost != 0)
                    {// расчёт количества контрактов для фьючерсов и опционов на Мосбирже
                        qty = moneyOnPosition / (tab.PriceBestAsk / tab.Security.PriceStep * tab.Security.PriceStepCost);
                    }
                    qty = Math.Round(qty, tab.Security.DecimalsVolume);
                }
                else
                {
                    qty = Math.Round(qty, 7);
                }

                return qty;
            }

            return volume;
        }
    }

    #region Entity

    public class ChatRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("messages")]
        public List<Message> Messages { get; set; }

        [JsonProperty("temperature")]
        public float Temperature { get; set; }
    }

    public class GigaChatRequest : ChatRequest
    {
        [JsonProperty("stream")]
        public bool Stream { get; set; }

        [JsonProperty("update_interval")]
        public int UpdateInterval { get; set; }

        public int? MaxTokens { get; set; }
    }

    public class GPTunnelRequest : ChatRequest
    {
        [JsonProperty("useWalletBalance")]
        public bool UseWalletBalance { get; set; }
    }

    public class Message
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }
    }

    public class ChatResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("object")]
        public string Object { get; set; }

        [JsonProperty("created")]
        public long Created { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("choices")]
        public List<Choice> Choices { get; set; }

        [JsonProperty("usage")]
        public Usage Usage { get; set; }
    }

    public class Choice
    {
        [JsonProperty("index")]
        public int Index { get; set; }

        [JsonProperty("message")]
        public Message Message { get; set; }

        [JsonProperty("finish_reason")]
        public string Finish_reason { get; set; }
    }

    public class GigaChatTokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("expires_at")]
        public long ExpiresAt { get; set; }
    }

    public class Usage
    {
        [JsonProperty("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonProperty("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonProperty("total_tokens")]
        public int TotalTokens { get; set; }

        // GigaChat prop
        [JsonProperty("precached_prompt_tokens")]
        public int PrecachedPromptTokens { get; set; }

        // GPTunnel prop
        [JsonProperty("prompt_cost")]
        public double PromptCost { get; set; }

        [JsonProperty("completion_cost")]
        public double CompletionCost { get; set; }

        [JsonProperty("total_cost")]
        public double TotalCost { get; set; }
    }

    #endregion
}
