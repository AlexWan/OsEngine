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
                SendLogMessage("Start Finam Trade Connection", LogMessageType.System);

                _proxy = proxy;
                _accessToken = ((ServerParameterPassword)ServerParameters[0]).Value;
                _accountId = ((ServerParameterString)ServerParameters[1]).Value;

                if (string.IsNullOrEmpty(_accessToken))
                {
                    SendLogMessage("Connection terminated. You must specify the api token. You can get it on the T-Invest website",
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
            throw new NotImplementedException();
        }

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
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
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

        private readonly string _gRPCHost = "ftrr01.finam.ru:443";
        private Metadata _gRpcMetadata;
        private GrpcChannel _channel;
        private CancellationTokenSource _cancellationTokenSource;
        private WebProxy _proxy;

        private AuthService.AuthServiceClient _authClient;
        #endregion


        #region 11 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        public List<IServerParameter> ServerParameters { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public event Action ConnectEvent;
        public event Action DisconnectEvent;
        public event Action<List<Security>> SecurityEvent;
        public event Action<List<Portfolio>> PortfolioEvent;
        public event Action<News> NewsEvent;
        public event Action<MarketDepth> MarketDepthEvent;
        public event Action<Trade> NewTradesEvent;
        public event Action<Order> MyOrderEvent;
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



        public void GetAllActivOrders()
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

        public void GetPortfolios()
        {
            throw new NotImplementedException();
        }

        public void GetSecurities()
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
