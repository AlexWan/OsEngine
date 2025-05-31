using FinamApi.TradeApi.V1;
using FinamApi.TradeApi.V1.Accounts;
using FinamApi.TradeApi.V1.Assets;
using FinamApi.TradeApi.V1.Auth;
using Grpc.Core;
using Grpc.Net.Client;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.MoexAlgopack.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.FinamGrpc
{
    public class FinamGrpcServer : AServer
    {
        public FinamGrpcServer(int uniqueId)
        {
            ServerNum = uniqueId;

            FinamGrpcServerRealization realization = new FinamGrpcServerRealization();
            ServerRealization = realization;

            // Для работы с API необходим токен secret, сгенерированный на портале Finam ( https://tradeapi.finam.ru/docs/tokens )
            CreateParameterPassword(OsLocalization.Market.ServerParamToken, "");
            // Параметр account_id, это аккаунт в личном кабинете формата КлФ-account_id
            CreateParameterString("Account ID", "");
        }
    }

    public class FinamGrpcServerRealization : IServerRealization
    {

        #region 1 Constructor, Status, Connection
        public FinamGrpcServerRealization()
        {
            ServerTime = DateTime.UtcNow;
        }

        public void Connect(WebProxy proxy)
        {
            try
            {
                SendLogMessage("Start Finam gRPC Connection", LogMessageType.System);

                _proxy = proxy;
                _accessToken = ((ServerParameterPassword)ServerParameters[0]).Value;
                _accountId = ((ServerParameterString)ServerParameters[1]).Value;

                if (string.IsNullOrEmpty(_accessToken))
                {
                    SendLogMessage("Finam gRPC connection terminated. You must specify the api token. You can get it on the Finam website [ https://tradeapi.finam.ru/docs/tokens ]",
                        LogMessageType.Error);
                    return;
                }

                CreateStreamsConnection();

                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent?.Invoke();
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error connecting to server: {ex}", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent?.Invoke();
            }
        }

        public void Dispose()
        {
            SendLogMessage("Connection to Finam gRPC closed. Data streams Closed Event", LogMessageType.System);

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent?.Invoke();
            }
        }

        public List<IServerParameter> ServerParameters { get; set; }
        public event Action ConnectEvent;
        public event Action DisconnectEvent;

        public ServerType ServerType => ServerType.FinamGrpc;
        public DateTime ServerTime { get; set; }
        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;
        #endregion

        #region 2 Properties

        private string _accessToken;
        private string _accountId;
        private string _jwtToken;

        //private Dictionary<string, int> _orderNumbers = new Dictionary<string, int>();

        //private RateGate _rateGateInstruments = new RateGate(200, TimeSpan.FromMinutes(1));

        #endregion

        #region 3 Securities
        public void GetSecurities()
        {
            _rateGateInstruments.WaitToProceed();

            if (_securities.Count > 0)
            {
                SendLogMessage("Securities loaded. Count: " + _securities.Count, LogMessageType.System);

                SecurityEvent?.Invoke(_securities);
            }
        }

        private List<Security> _securities = new List<Security>();

        public event Action<List<Security>> SecurityEvent;

        // TODO fix rategate value
        private RateGate _rateGateInstruments = new RateGate(200, TimeSpan.FromMinutes(1));
        #endregion

        #region 4 Portfolios
        public void GetPortfolios()
        {
            PortfolioEvent?.Invoke(_myPortfolios);
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        private List<Portfolio> _myPortfolios = new List<Portfolio>();
        #endregion

        #region 6 gRPC streams creation
        private void CreateStreamsConnection()
        {
            try
            {
                _gRpcMetadata = new Metadata();

                _gRpcMetadata.Add("Authorization", $"Bearer {_accessToken}");
                _gRpcMetadata.Add("x-app-name", "OsEngine");

                _cancellationTokenSource = new CancellationTokenSource();

                _channel = GrpcChannel.ForAddress(_gRPCHost, new GrpcChannelOptions
                {
                    Credentials = ChannelCredentials.SecureSsl,
                    HttpClient = new HttpClient(new HttpClientHandler { Proxy = _proxy, UseProxy = _proxy != null })
                });

                _authClient = new AuthService.AuthServiceClient(_channel);

                try
                {
                    // Получаем gwt токен
                    AuthResponse auth = _authClient.Auth(new AuthRequest { Secret = _accessToken });
                    if (auth?.Token == null)
                    {
                        //string errorMessage = string.Join(", ", testResponse.Errors.Select(e => $"{e.Code}: {e.Message}"));
                        SendLogMessage($"Authentication failed.", LogMessageType.Error);
                        return;
                    }

                    _jwtToken = auth.Token;
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error while auth. Info: {message}", LogMessageType.Error);
                    return;
                    //SendLogMessage(ex.ToString(), LogMessageType.Error);
                }

                try
                {
                    SendLogMessage("All streams activated. Connect State", LogMessageType.System);
                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent?.Invoke();
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private readonly string _gRPCHost = "https://ftrr01.finam.ru:443";
        private Metadata _gRpcMetadata;
        private GrpcChannel _channel;
        private CancellationTokenSource _cancellationTokenSource;
        private WebProxy _proxy;

        private AuthService.AuthServiceClient _authClient;
        #endregion

        #region 9 Trade

        public void GetAllActivOrders()
        {
            List<Order> orders = null;

            for (int i = 0; orders != null && i < orders.Count; i++)
            {
                MyOrderEvent?.Invoke(orders[i]);
            }
        }

        public event Action<Order> MyOrderEvent;
        private RateGate _rateGateOrders = new RateGate(100, TimeSpan.FromMinutes(1)); // https://russianinvestments.github.io/investAPI/limits/
        #endregion

        #region 10 Helpers

        private string GetGRPCErrorMessage(RpcException ex)
        {
            string message = string.Format("{0}: {1}", ex.Status.StatusCode, ex.Status.Detail);
            //string trackingId = "";

            //if (exception.Trailers == null)
            //    return message;

            //for (int i = 0; i < exception.Trailers.Count; i++)
            //{
            //    if (exception.Trailers[i].Key == "x-tracking-id")
            //        trackingId = exception.Trailers[i].Value;

            //    if (exception.Trailers[i].Key == "message")
            //        message = exception.Trailers[i].Value;
            //}

            //if (trackingId.Length > 0)
            //{
            //    message = "Tracking id: " + trackingId + "; Message: " + message;
            //}


            return message;
        }
        #endregion

        #region 11 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion


        public event Action<News> NewsEvent;
        public event Action<MarketDepth> MarketDepthEvent;
        public event Action<Trade> NewTradesEvent;

        public event Action<MyTrade> MyTradeEvent;
        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

        public void CancelAllOrders()
        {
            throw new NotImplementedException();
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            throw new NotImplementedException();
        }

        public void CancelOrder(Order order)
        {
            throw new NotImplementedException();
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            throw new NotImplementedException();
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            throw new NotImplementedException();
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            throw new NotImplementedException();
        }

        public void GetOrderStatus(Order order)
        {
            throw new NotImplementedException();
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            throw new NotImplementedException();
        }

        public void SendOrder(Order order)
        {
            throw new NotImplementedException();
        }

        public bool SubscribeNews()
        {
            throw new NotImplementedException();
        }

        public void Subscrible(Security security)
        {
            throw new NotImplementedException();
        }
    }
}
