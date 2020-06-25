using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.FTX.Entities;
using OsEngine.Market.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.FTX
{
    public class FTXServer : AServer
    {
        private const string APIKey = "u02JwwSZxGxWGc1hldEeDDrcS3kCENVKdaOO4S_h";
        private const string APISecret = "SVeIMHxG-vjfSV5H7kESQ6oHpbLWqgGXQKlWo3TS";
        public FTXServer()
        {
            FTXServerRealization realization = new FTXServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
        }
    }

    public class FTXServerRealization : AServerRealization
    {
        #region private fields
        private string _apiKey;
        private string _secretKey;
        private readonly string _webSocketEndpointUrl = "wss://ftx.com/ws/";

        private WsSource _wsSource;

        private CancellationTokenSource _cancelTokenSource;

        private readonly ConcurrentQueue<string> _queueMessagesReceivedFromExchange = new ConcurrentQueue<string>();

        private DateTime _lastTimeUpdateSocket;

        private DateTime _timeStartSocket;
        #endregion

        #region public properties
        public override ServerType ServerType => ServerType.FTX;
        #endregion

        #region private methods
        private void WsSourceByteDataEvent(WsMessageType messageType, byte[] data)
        {
            throw new NotImplementedException();
        }

        private void WsSourceMessageEvent(WsMessageType msgType, string message)
        {
            switch (msgType)
            {
                case WsMessageType.Opened:
                    SendLoginMessage();
                    OnConnectEvent();
                    break;
                case WsMessageType.Closed:
                    OnDisconnectEvent();
                    break;
                case WsMessageType.StringData:
                    _queueMessagesReceivedFromExchange.Enqueue(message);
                    break;
                case WsMessageType.Error:
                    SendLogMessage(message, LogMessageType.Error);
                    break;
                default:
                    throw new NotSupportedException(message);
            }
        }

        private void StartMessageReader()
        {
            Task.Run(() => MessageReader(_cancelTokenSource.Token), _cancelTokenSource.Token);
            Task.Run(() => SourceAliveCheckerThread(_cancelTokenSource.Token), _cancelTokenSource.Token);
        }

        private async void MessageReader(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!_queueMessagesReceivedFromExchange.IsEmpty &&
                        _queueMessagesReceivedFromExchange.TryDequeue(out string mes))
                    {
                        var response = JsonConvert.DeserializeObject<BasicResponse>(mes);

                        switch (response.Type)
                        {
                            case TypeEnum.Pong:
                                _lastTimeUpdateSocket = DateTime.Now;
                                SendLogMessage(mes, LogMessageType.NoName);
                                break;
                            case TypeEnum.Error:
                                SendLogMessage(mes, LogMessageType.Error);
                                break;
                            default:
                                SendLogMessage(mes, LogMessageType.System);
                                break;
                        }
                    }
                    else
                    {
                        await Task.Delay(20);
                    }
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    SendLogMessage("MessageReader error: " + exception, LogMessageType.Error);
                }
            }
        }


        private async void SourceAliveCheckerThread(CancellationToken token)
        {
            var pingRequest = new PingRequest();
            var pingMessage = JsonConvert.SerializeObject(pingRequest);
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(15000);
                _wsSource.SendMessage(pingMessage);

                if (_lastTimeUpdateSocket == DateTime.MinValue)
                {
                    continue;
                }
                if (_lastTimeUpdateSocket.AddSeconds(60) < DateTime.Now)
                {
                    SendLogMessage("The websocket is disabled. Restart", LogMessageType.Error);
                    Dispose();
                    OnDisconnectEvent();
                    return;
                }
            }
        }

        private void SendLoginMessage()
        {
            var start = new DateTime(1970, 1, 1);
            var nowUTC = TimeManager.GetExchangeTime("UTC");
            long time = Convert.ToInt64((nowUTC - start).TotalMilliseconds);

            var hashMaker = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            var signaturePayload = $"{time}websocket_login";
            var hash = hashMaker.ComputeHash(Encoding.UTF8.GetBytes(signaturePayload));
            var hashString = BitConverter.ToString(hash).Replace("-", string.Empty);
            var signature = hashString.ToLower();

            var authenticationRequest = new AuthenticationRequest(_apiKey, signature, time.ToString());

            var json = JsonConvert.SerializeObject(authenticationRequest);

            _wsSource.SendMessage(json);
        }
        #endregion

        #region public methods
        public override void CanselOrder(Order order)
        {
            base.CanselOrder(order);
        }

        public override void Connect()
        {
            _apiKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            _cancelTokenSource = new CancellationTokenSource();

            StartMessageReader();

            _wsSource = new WsSource(_webSocketEndpointUrl);
            _wsSource.MessageEvent += WsSourceMessageEvent;
            _wsSource.ByteDataEvent += WsSourceByteDataEvent;
            _wsSource.Start();
            _timeStartSocket = DateTime.UtcNow;
        }

        public override void Dispose()
        {
            try
            {
                if(_wsSource != null)
                {
                    _wsSource.Dispose();
                    _wsSource.MessageEvent -= WsSourceMessageEvent;
                    _wsSource.ByteDataEvent -= WsSourceByteDataEvent;
                    _wsSource = null;
                }

                if (_cancelTokenSource != null && !_cancelTokenSource.IsCancellationRequested)
                {
                    _cancelTokenSource.Cancel();
                }

            }
            catch (Exception e)
            {
                SendLogMessage("FTX dispose error: " + e, LogMessageType.Error);
            }
        }

        public override List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return base.GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, actualTime);
        }

        public override void GetOrdersState(List<Order> orders)
        {
            base.GetOrdersState(orders);
        }

        public override void GetPortfolios()
        {
            base.GetPortfolios();
        }

        public override void GetSecurities()
        {
            base.GetSecurities();
        }

        public override List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return base.GetTickDataToSecurity(security, startTime, endTime, actualTime);
        }

        public override void SendOrder(Order order)
        {
            base.SendOrder(order);
        }

        public override void Subscrible(Security security)
        {
            base.Subscrible(security);
        }
        #endregion


        #region public events
        public override event Action ConnectEvent;
        #endregion
    }
}
