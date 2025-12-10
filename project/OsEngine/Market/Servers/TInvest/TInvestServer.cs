/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Tinkoff.InvestApi.V1;
using Option = Tinkoff.InvestApi.V1.Option;
using Candle = OsEngine.Entity.Candle;
using Order = OsEngine.Entity.Order;
using Trade = OsEngine.Entity.Trade;
using Security = OsEngine.Entity.Security;
using Portfolio = OsEngine.Entity.Portfolio;
using System.Net;
using System.Net.Http;
using Grpc.Net.Client;
using Grpc.Core;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.TInvest
{
    public class TInvestServer : AServer
    {
        public TInvestServer(int uniqueId)
        {
            ServerNum = uniqueId;

            TInvestServerRealization realization = new TInvestServerRealization();
            ServerRealization = realization;

            CreateParameterPassword(OsLocalization.Market.ServerParamToken, "");

            ServerParameterBool useStock = CreateParameterBoolean(OsLocalization.Market.UseStock, true);
            ServerParameterBool useFutures = CreateParameterBoolean(OsLocalization.Market.UseFutures, true);
            ServerParameterBool useOptions = CreateParameterBoolean(OsLocalization.Market.UseOptions, false); // с некоторого времени торговля опционами не доступна по API Т-Инвестиций
            ServerParameterBool useOther = CreateParameterBoolean(OsLocalization.Market.UseOther, false);
            useStock.ValueChange += UseSector_ValueChange;
            useFutures.ValueChange += UseSector_ValueChange;
            useOptions.ValueChange += UseSector_ValueChange;
            useOther.ValueChange += UseSector_ValueChange;

            CreateParameterBoolean("Filter out non-market data (holiday trading)", true);
            CreateParameterBoolean("Filter out dealer trades", false);
            CreateParameterBoolean(OsLocalization.Market.IgnoreMorningAuctionTrades, true);
        }

        private void UseSector_ValueChange()
        {
            Task.Run(ServerRealization.GetSecurities);
        }
    } 

    public class TInvestServerRealization : IServerRealization
    {
        private readonly TimeZoneInfo _mskTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");

        #region 1 Constructor, Status, Connection

        public TInvestServerRealization()
        {
            Thread worker = new Thread(ConnectionCheckThread);
            worker.Name = "CheckAliveTInvest";
            worker.Start();

            Thread worker2 = new Thread(DataMessageReader);
            worker2.Name = "DataMessageReaderTInvest";
            worker2.Start();

            Thread worker3 = new Thread(PortfolioMessageReader);
            worker3.Name = "PortfolioMessageReaderTInvest";
            worker3.Start();

            Thread worker4 = new Thread(PositionsMessageReader);
            worker4.Name = "PositionsMessageReaderTInvest";
            worker4.Start();

            //Thread worker5 = new Thread(MyTradesMessageReader);
            //worker5.Name = "MyTradesMessageReaderTInvest";
            //worker5.Start();

            Thread worker6 = new Thread(LastPricesPoller);
            worker6.Start();

            Thread worker7 = new Thread(OrderStateMessageReader);
            worker7.Name = "OrderStateMessageReaderTInvest";
            worker7.Start();
        }

        public void Connect(WebProxy proxy)
        {
            _proxy = proxy;

            try
            {
                try
                {
                    string osNameAndVersion = System.Runtime.InteropServices.RuntimeInformation.OSDescription;

                    if (osNameAndVersion.StartsWith("Microsoft Windows 7"))
                    {
                        SendLogMessage(OsLocalization.Market.Label299, LogMessageType.System);
                        return;
                    }

                }
                catch
                {
                    // ignore
                }

                _streamSubscribedSecurities.Clear();
                _pollSubscribedSecurities.Clear();

                SendLogMessage(OsLocalization.Market.Label284, LogMessageType.System);

                _accessToken = ((ServerParameterPassword)ServerParameters[0]).Value;
                _filterOutNonMarketData = ((ServerParameterBool)ServerParameters[5]).Value;
                _filterOutDealerTrades = ((ServerParameterBool)ServerParameters[6]).Value;
                _ignoreMorningAuctionTrades = ((ServerParameterBool)ServerParameters[7]).Value;

                if (string.IsNullOrEmpty(_accessToken))
                {
                    SendLogMessage(OsLocalization.Market.Label283,
                        LogMessageType.Error);
                    return;
                }

                CreateStreamsConnection();
            }
            catch (Exception ex)
            {
                SendLogMessage(OsLocalization.Market.Label289 + ex.Message.ToString(), LogMessageType.Error);
            }
        }

        private void ConnectionCheckThread()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(10000);

                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        continue;
                    }

                    bool streamsIsLost = false;
                    string lostStreamName = null;

                    if (_marketDataStream != null && _lastMarketDataTime.AddMinutes(3) < DateTime.UtcNow)
                    {
                        lostStreamName = "Market data stream";
                        streamsIsLost = true;
                    }

                    if (_portfolioDataStream != null && _lastPortfolioDataTime.AddMinutes(3) < DateTime.UtcNow)
                    {
                        lostStreamName = "Portfolio data stream";
                        streamsIsLost = true;
                    }

                    if (_myOrderStateDataStream != null && _lastMyOrderStateDataTime.AddMinutes(3) < DateTime.UtcNow)
                    {
                        lostStreamName = "Order state data stream";
                        
                        streamsIsLost = true;
                    }

                    if (streamsIsLost)
                    {
                        if (_isDisposedNow == true)
                        {
                            continue;
                        }

                        if (lostStreamName == "Market data stream")
                        {
                            _isReconnectByPingMarketData = true;

                            if (TryReconnectDataStream() == true)
                            {
                                _lastMarketDataTime = DateTime.Now;
                                SendLogMessage(OsLocalization.Market.Label295 + "\nMarket data. ConnectionCheckThread()", LogMessageType.System);
                                Thread.Sleep(2000);
                                _isReconnectByPingMarketData = false;
                                continue;

                            }
                        }

                        if (lostStreamName == "Order state data stream")
                        {
                            _isReconnectByOrdersData = true;

                            if (TryReconnectOrdersStream() == true)
                            {
                                _lastMyOrderStateDataTime = DateTime.Now;
                                SendLogMessage(OsLocalization.Market.Label295 + "\nOrders data. ConnectionCheckThread()", LogMessageType.System);

                                if (ForceCheckOrdersAfterReconnectEvent != null)
                                {
                                    ForceCheckOrdersAfterReconnectEvent();
                                }
                                Thread.Sleep(2000);
                                _isReconnectByOrdersData = false;
                                continue;

                            }
                        }

                        if (lostStreamName == "Portfolio data stream")
                        {
                            _isReconnectByPingPortfoliosData = true;

                            if (TryReconnectPortfolioStream() == true
                            && TryReconnectPositionsStream() == true)
                            {
                                _lastPortfolioDataTime = DateTime.Now;
                                SendLogMessage(OsLocalization.Market.Label295 + "\nPortfolio and Positions data. ConnectionCheckThread()", LogMessageType.System);
                                Thread.Sleep(2000);
                                _isReconnectByPingPortfoliosData = false;
                                continue;
                            }
                        }

                        _isReconnectByPingMarketData = false;
                        _isReconnectByOrdersData = false;
                        _isReconnectByPingPortfoliosData = false;

                        if (ServerStatus == ServerConnectStatus.Connect)
                        {
                            SendLogMessage(OsLocalization.Market.Label286 + lostStreamName, LogMessageType.System);
                            SendMessageOnReconnectInErrorLog();
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                            Thread.Sleep(2000);                          
                        }
                    }
                }
                catch (Exception ex)
                {
                    _isReconnectByPingMarketData = false;
                    _isReconnectByOrdersData = false;
                    _isReconnectByPingPortfoliosData = false;

                    SendLogMessage(ex.ToString(), LogMessageType.System);
                    Thread.Sleep(5000);
                }
            }
        }

        private bool _isDisposedNow = false;

        private bool _isReconnectByPingMarketData = false;

        private bool _isReconnectByPingPortfoliosData = false;

        private bool _isReconnectByOrdersData = false;

        public void Dispose()
        {
            _isDisposedNow = true;

            try
            {

                // останавливаем чтение всех потоков
                if (_marketDataStream != null)
                {
                    try
                    {
                        _marketDataStream.RequestStream.CompleteAsync().Wait();
                        _marketDataStream.ResponseStream.ReadAllAsync();
                        _marketDataStream.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                if (_cancellationTokenSource != null)
                {
                    try
                    {
                        _cancellationTokenSource.Cancel();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                if (_portfolioDataStream != null)
                {
                    try
                    {
                        _portfolioDataStream.ResponseStream.ReadAllAsync();
                        _portfolioDataStream.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                if (_positionsDataStream != null)
                {
                    try
                    {
                        _positionsDataStream.ResponseStream.ReadAllAsync();
                        _positionsDataStream.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                if (_myOrderStateDataStream != null)
                {
                    try
                    {
                        _myOrderStateDataStream.ResponseStream.ReadAllAsync();
                        _myOrderStateDataStream.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                if (_channel != null)
                {
                    try
                    {
                        _channel.Dispose();
                        _channel = null;
                    }
                    catch
                    {
                        // ignore
                    }
                }

                _marketDataStream = null;
                _portfolioDataStream = null;
                _positionsDataStream = null;
                _myOrderStateDataStream = null;
                _marketDataRequestsLastPrice.Clear();
                _marketDataRequestsOrderBook.Clear();
                _marketDataRequestsTrades.Clear();
                _streamSubscribedSecurities.Clear();
                _pollSubscribedSecurities.Clear();
                _myPortfolios.Clear();
                _lastMarketDataTime = DateTime.UtcNow;
                _lastMdTime = DateTime.UtcNow;
                _lastPortfolioDataTime = DateTime.UtcNow;

                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch(Exception ex) 
            {
                SendLogMessage("Error in Dispose method. " + ex.ToString(), LogMessageType.System);
            }


            _isDisposedNow = false;
        }

        public DateTime ServerTime { get; set; }

        public ServerType ServerType => ServerType.TInvest;

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public List<IServerParameter> ServerParameters { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        private bool _useStock = false;
        private bool _useFutures = false;
        private bool _useOptions = false;
        private bool _useOther = false;

        private bool _filterOutNonMarketData; // отфльтровать кухню выходного дня
        private bool _filterOutDealerTrades; // отфльтровать кухонные сделки (дилерские, внутренние)
        private bool _ignoreMorningAuctionTrades; // ignore trades before 7:00 MSK for stocks and before 9:00 for futures
        private string _accessToken;

        private Dictionary<string, int> _orderNumbers = new Dictionary<string, int>();

        private string _orderNumbersLocker = "_orderNumbersLocker";

        private ConcurrentDictionary<string, decimal> _orderPrices = new ConcurrentDictionary<string, decimal>();

        #endregion

        #region 3 Securities

        private RateGate _rateGateInstruments = new RateGate(200, TimeSpan.FromMinutes(1));

        private string _getSecuritiesLocker = "_getSecuritiesLocker";

        public void GetSecurities()
        {
            try
            {
                lock(_getSecuritiesLocker)
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        return;
                    }

                    _useStock = ((ServerParameterBool)ServerParameters[1]).Value;
                    _useFutures = ((ServerParameterBool)ServerParameters[2]).Value;
                    _useOptions = ((ServerParameterBool)ServerParameters[3]).Value;
                    _useOther = ((ServerParameterBool)ServerParameters[4]).Value;

                    _rateGateInstruments.WaitToProceed();
                    CurrenciesResponse currenciesResponse = null;

                    currenciesResponse = _instrumentsClient.Currencies(new InstrumentsRequest(), headers: _gRpcMetadata);
                    UpdateCurrenciesFromServer(currenciesResponse);

                    if (_useStock || _useOther)
                    {
                        _rateGateInstruments.WaitToProceed();

                        SharesResponse result = _instrumentsClient.Shares(new InstrumentsRequest(), headers: _gRpcMetadata);
                        UpdateSharesFromServer(result);
                    }

                    if (_useFutures)
                    {
                        _rateGateInstruments.WaitToProceed();

                        FuturesResponse result = _instrumentsClient.Futures(new InstrumentsRequest(), headers: _gRpcMetadata);
                        UpdateFuturesFromServer(result);
                    }

                    if (_useOptions)
                    {
                        // https://russianinvestments.github.io/investAPI/faq_instruments/ v1.23
                        // No options still for T-Invest 
                        //SendLogMessage("Options trading not supported by T-Invest API", LogMessageType.System);

                        //_rateGateInstruments.WaitToProceed();

                        //OptionsResponse result = null;
                        //try
                        //{
                        //    result = _instrumentsClient.Options(new InstrumentsRequest(), headers: _gRpcMetadata);
                        //}
                        //catch (RpcException ex)
                        //{
                        //    string message = GetGRPCErrorMessage(ex);
                        //    SendLogMessage($"Error getting options data. Info: {message}", LogMessageType.System);
                        //}
                        //catch (Exception ex)
                        //{
                        //    SendLogMessage("Error loading securities", LogMessageType.System);
                        //}

                        //UpdateOptionsFromServer(result);
                    }

                    if (_useOther)
                    {
                        _rateGateInstruments.WaitToProceed();

                        BondsResponse result = _instrumentsClient.Bonds(new InstrumentsRequest(), headers: _gRpcMetadata);
                        UpdateBondsFromServer(result);

                        _rateGateInstruments.WaitToProceed();

                        EtfsResponse etfs = _instrumentsClient.Etfs(new InstrumentsRequest(), headers: _gRpcMetadata);
                        UpdateEtfsFromServer(etfs);

                        _rateGateInstruments.WaitToProceed();
                        IndicativesResponse indicatives = _instrumentsClient.Indicatives(new IndicativesRequest(), headers: _gRpcMetadata);
                        UpdateIndicativesFromServer(indicatives);
                    }

                    if (_securities.Count > 0)
                    {
                        SendLogMessage(OsLocalization.Market.Label287 + " " + _securities.Count, LogMessageType.System);

                        if (SecurityEvent != null)
                        {
                            SecurityEvent.Invoke(_securities);
                        }

                        GetPortfolios();
                    }
                    else
                    {
                        if (ServerStatus != ServerConnectStatus.Disconnect)
                        {
                            SendLogMessage(OsLocalization.Market.Label305, LogMessageType.Error);

                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    SendLogMessage(OsLocalization.Market.Label288 + ex.ToString(), LogMessageType.Error);

                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
        }

        private void UpdateSharesFromServer(SharesResponse sharesResponse)
        {
            try
            {
                if (sharesResponse == null ||
                    sharesResponse.Instruments.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < sharesResponse.Instruments.Count; i++)
                {
                    Share item = sharesResponse.Instruments[i];

                    Security newSecurity = new Security();
                    newSecurity.Name = item.Ticker;
                    newSecurity.NameId = item.Uid;
                    newSecurity.NameFull = item.Name;
                    newSecurity.Exchange = item.Exchange;

                    if (item.MinPriceIncrement != null)
                    {
                        newSecurity.PriceStep = GetValue(item.MinPriceIncrement);
                    }
                    else
                    {
                        newSecurity.PriceStep = 1;
                    }

                    if (newSecurity.PriceStep == 0)
                    {
                        newSecurity.PriceStep = 1;
                    }

                    newSecurity.PriceStepCost = newSecurity.PriceStep;

                    newSecurity.NameClass = SecurityType.Stock.ToString() + " " + item.Currency;


                    newSecurity.SecurityType = SecurityType.Stock;
                    newSecurity.Lot = item.Lot;
                    newSecurity.VolumeStep = 1;

                    newSecurity.State = SecurityStateType.Activ;
                    _securities.Add(newSecurity);
                }

            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading stocks: {e.Message}", LogMessageType.System);
            }
        }

        private void UpdateBondsFromServer(BondsResponse bondsResponse)
        {
            try
            {
                if (bondsResponse == null ||
                    bondsResponse.Instruments.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < bondsResponse.Instruments.Count; i++)
                {
                    Bond item = bondsResponse.Instruments[i];

                    Security newSecurity = new Security();
                    newSecurity.Name = item.Ticker;
                    newSecurity.NameId = item.Uid;
                    newSecurity.NameFull = item.Name;
                    newSecurity.Exchange = item.Exchange;

                    if (item.MinPriceIncrement != null)
                    {
                        newSecurity.PriceStep = GetValue(item.MinPriceIncrement);
                    }
                    else
                    {
                        newSecurity.PriceStep = 1;
                    }

                    if (newSecurity.PriceStep == 0)
                    {
                        newSecurity.PriceStep = 1;
                    }

                    newSecurity.PriceStepCost = newSecurity.PriceStep;


                    newSecurity.NameClass = SecurityType.Bond.ToString() + " " + item.Currency;

                    newSecurity.SecurityType = SecurityType.Bond;
                    newSecurity.Lot = item.Lot;
                    newSecurity.VolumeStep = 1;

                    newSecurity.State = SecurityStateType.Activ;
                    _securities.Add(newSecurity);
                }

            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading bonds: {e.Message}", LogMessageType.System);
            }
        }

        private void UpdateEtfsFromServer(EtfsResponse etfs)
        {
            try
            {
                if (etfs == null ||
                    etfs.Instruments.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < etfs.Instruments.Count; i++)
                {
                    Etf item = etfs.Instruments[i];

                    Security newSecurity = new Security();
                    newSecurity.Name = item.Ticker;
                    newSecurity.NameId = item.Uid;
                    newSecurity.NameFull = item.Name;
                    newSecurity.Exchange = item.Exchange;

                    if (item.MinPriceIncrement != null)
                    {
                        newSecurity.PriceStep = GetValue(item.MinPriceIncrement);
                    }
                    else
                    {
                        newSecurity.PriceStep = 1;
                    }

                    if (newSecurity.PriceStep == 0)
                    {
                        newSecurity.PriceStep = 1;
                    }

                    newSecurity.PriceStepCost = newSecurity.PriceStep;

                    newSecurity.NameClass = SecurityType.Fund.ToString() + " " + item.Currency;

                    newSecurity.SecurityType = SecurityType.Fund;
                    newSecurity.Lot = item.Lot;
                    newSecurity.VolumeStep = 1;

                    newSecurity.State = SecurityStateType.Activ;
                    _securities.Add(newSecurity);
                }
            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading ETFs: {e.Message}", LogMessageType.System);
            }
        }

        private void UpdateIndicativesFromServer(IndicativesResponse indicatives)
        {
            try
            {
                if (indicatives == null ||
                    indicatives.Instruments.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < indicatives.Instruments.Count; i++)
                {
                    IndicativeResponse item = indicatives.Instruments[i];

                    Security newSecurity = new Security();
                    newSecurity.Name = item.Ticker;
                    newSecurity.NameId = item.Uid;
                    newSecurity.NameFull = item.Name;
                    newSecurity.Exchange = item.Exchange;

                    newSecurity.PriceStep = 1;
                    newSecurity.PriceStepCost = newSecurity.PriceStep;


                    newSecurity.NameClass = SecurityType.Index.ToString() + " " + item.Currency;

                    newSecurity.SecurityType = SecurityType.Index;
                    newSecurity.Lot = 1;
                    newSecurity.VolumeStep = 1;

                    newSecurity.State = SecurityStateType.Activ;
                    _securities.Add(newSecurity);
                }

            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading indicatives: {e.Message}", LogMessageType.System);
            }
        }

        private void UpdateCurrenciesFromServer(CurrenciesResponse currenciesResponse)
        {
            try
            {
                if (currenciesResponse == null ||
                    currenciesResponse.Instruments.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < currenciesResponse.Instruments.Count; i++)
                {
                    Currency item = currenciesResponse.Instruments[i];

                    Security newSecurity = new Security();
                    newSecurity.Name = item.Ticker;
                    newSecurity.NameId = item.Uid;
                    newSecurity.NameFull = item.Name;
                    newSecurity.Exchange = item.Exchange;

                    if (item.MinPriceIncrement != null)
                    {
                        newSecurity.PriceStep = GetValue(item.MinPriceIncrement);
                    }
                    else
                    {
                        newSecurity.PriceStep = 1;
                    }

                    if (newSecurity.PriceStep == 0)
                    {
                        newSecurity.PriceStep = 1;
                    }

                    newSecurity.PriceStepCost = newSecurity.PriceStep;


                    newSecurity.NameClass = "Currency pair";

                    newSecurity.SecurityType = SecurityType.CurrencyPair;
                    newSecurity.Lot = item.Lot;
                    newSecurity.VolumeStep = 1;


                    newSecurity.State = SecurityStateType.Activ;
                    _securities.Add(newSecurity);
                }

            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading currency pairs: {e.Message}", LogMessageType.System);
            }
        }

        private void UpdateFuturesFromServer(FuturesResponse futures)
        {
            try
            {
                if (futures == null ||
                    futures.Instruments.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < futures.Instruments.Count; i++)
                {
                    Future item = futures.Instruments[i];

                    Security newSecurity = new Security();
                    newSecurity.Name = item.Ticker;
                    newSecurity.NameId = item.Uid;
                    newSecurity.NameFull = item.Name;
                    newSecurity.Exchange = item.Exchange;
                    newSecurity.UsePriceStepCostToCalculateVolume = true;

                    if (item.MinPriceIncrement != null)
                    {
                        newSecurity.PriceStep = GetValue(item.MinPriceIncrement);
                    }
                    else
                    {
                        newSecurity.PriceStep = 1;
                    }
                  
                    newSecurity.Expiration = TimeZoneInfo.ConvertTimeFromUtc(item.ExpirationDate.ToDateTime(), _mskTimeZone);// convert to MSK;

                    if (newSecurity.PriceStep == 0)
                    {
                        newSecurity.PriceStep = 1;
                    }

                    newSecurity.PriceStepCost = newSecurity.PriceStep;


                    newSecurity.NameClass = SecurityType.Futures.ToString();

                    newSecurity.Lot = item.Lot;

                    newSecurity.SecurityType = SecurityType.Futures;
                    newSecurity.VolumeStep = 1;
                    newSecurity.MarginBuy = GetValue(item.InitialMarginOnBuy);
                    newSecurity.MarginSell = GetValue(item.InitialMarginOnSell);

                    if (item.MinPriceIncrementAmount != null)
                    {
                        newSecurity.PriceStepCost = GetValue(item.MinPriceIncrementAmount);
                    }

                    newSecurity.State = SecurityStateType.Activ;
                    _securities.Add(newSecurity);
                }

            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading futures: {e.Message}", LogMessageType.System);
            }
        }

        private void UpdateOptionsFromServer(OptionsResponse options)
        {
            try
            {
                if (options == null ||
                    options.Instruments.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < options.Instruments.Count; i++)
                {
                    Option item = options.Instruments[i];

                    Security newSecurity = new Security();
                    newSecurity.Name = item.Ticker;
                    newSecurity.NameId = item.Uid;
                    newSecurity.NameFull = item.Name;
                    newSecurity.Exchange = item.Exchange;
                    newSecurity.UsePriceStepCostToCalculateVolume = true;

                    if (item.MinPriceIncrement != null)
                    {
                        newSecurity.PriceStep = GetValue(item.MinPriceIncrement);
                    }
                    else
                    {
                        newSecurity.PriceStep = 1;
                    }

                    if (newSecurity.PriceStep == 0)
                    {
                        newSecurity.PriceStep = 1;
                    }

                    newSecurity.PriceStepCost = newSecurity.PriceStep;


                    newSecurity.NameClass = SecurityType.Option.ToString();

                    newSecurity.SecurityType = SecurityType.Option;
                    newSecurity.Lot = item.Lot;
                    newSecurity.VolumeStep = 1;

                    newSecurity.State = SecurityStateType.Activ;
                    _securities.Add(newSecurity);
                }
            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading options: {e.Message}", LogMessageType.System);
            }
        }

        private List<Security> _securities = new List<Security>();

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

        private DateTime _lastTimeGetPortfolio;

        public void GetPortfolios()
        {
            try
            {
                if(_lastTimeGetPortfolio.AddSeconds(10) > DateTime.Now)
                {
                    return;
                }

                _lastTimeGetPortfolio = DateTime.Now;

                GetAccountsResponse accountsResponse = _usersClient.GetAccounts(new GetAccountsRequest(), _gRpcMetadata);

                // для sandboxa
                if (accountsResponse.Accounts.Count == 0)
                {
                    Portfolio myPortfolio = new Portfolio();
                    myPortfolio.Number = "sandbox";
                    myPortfolio.ValueCurrent = 1;
                    myPortfolio.ValueBegin = 1;
                    _myPortfolios.Add(myPortfolio);
                }

                for (int i = 0; i < accountsResponse.Accounts.Count; i++)
                {
                    try
                    {
                        Account account = accountsResponse.Accounts[i];

                        if (string.IsNullOrEmpty(account.Id))
                        {
                            continue;
                        }

                        if (account.AccessLevel != AccessLevel.AccountAccessLevelFullAccess) // этот игнорируем, так как ключ API не дает доступа    
                        {
                            continue;
                        }

                        if (account.Type == AccountType.InvestBox) // инвест-копилка - это какая-то неторговая приблуда
                        {
                            continue;
                        }

                        PortfolioRequest portfolioRequest = new PortfolioRequest();
                        portfolioRequest.AccountId = account.Id;

                        PortfolioResponse portfolioResponse = null;

                        try
                        {
                            portfolioResponse = _operationsClient.GetPortfolio(portfolioRequest, _gRpcMetadata);
                        }
                        catch
                        {
                            // ignore
                        }

                        GetPortfolios(portfolioResponse);
                        UpdatePositionsInPortfolio(portfolioResponse);
                    }
                    catch (Exception)
                    {
                        // ignore
                    }
                }

                if (_myPortfolios.Count != 0)
                {
                    if (PortfolioEvent != null)
                    {
                        PortfolioEvent(_myPortfolios);
                    }
                }
                else
                {
                    // нет портфелей. Токен просмотровый
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        SendLogMessage(OsLocalization.Market.Label300, LogMessageType.Error);
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                }
            }
            catch (Exception ex)
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    SendLogMessage(OsLocalization.Market.Label290 + ex.ToString(), LogMessageType.Error);

                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
        }

        private void GetPortfolios(PortfolioResponse portfolioResponse)
        {
            if(portfolioResponse == null)
            {
                return;
            }

            Portfolio myPortfolio = _myPortfolios.Find(p => p.Number == portfolioResponse.AccountId);

            if (myPortfolio == null)
            {
                myPortfolio = new Portfolio();
                myPortfolio.Number = portfolioResponse.AccountId;
                myPortfolio.ValueCurrent = portfolioResponse.TotalAmountPortfolio != null ? GetValue(portfolioResponse.TotalAmountPortfolio) : 1;
                myPortfolio.ValueBegin = myPortfolio.ValueCurrent;
                _myPortfolios.Add(myPortfolio);
            }
            else
            {
                myPortfolio.Number = portfolioResponse.AccountId;

                decimal value = portfolioResponse.TotalAmountPortfolio != null ? GetValue(portfolioResponse.TotalAmountPortfolio) : 1;

                if(value != 1)
                {
                    myPortfolio.ValueCurrent = value;
                }
                
            }
        }

        private void UpdatePositionsInPortfolio(PortfolioResponse portfolio)
        {
            Portfolio portf = _myPortfolios.Find(p => p.Number == portfolio.AccountId);

            if (portf == null)
            {
                return;
            }

            List<PositionOnBoard> sectionPoses = new List<PositionOnBoard>();

            PositionsRequest positionsRequest = new PositionsRequest();
            positionsRequest.AccountId = portf.Number;

            PositionsResponse posData = null;

            try
            {
                posData = _operationsClient.GetPositions(positionsRequest, _gRpcMetadata);
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error getting positions in portfolio. Info: {message}", LogMessageType.System);
            }
            catch
            {
                SendLogMessage("Error getting positions in portfolio", LogMessageType.System);
            }

            // переменные для учёта позиций
            decimal futuresAndOptionsGO = 0;
            decimal spotShortValue = 0;

            for (int i = 0; i < posData.Securities.Count; i++)
            {
                PositionsSecurities pos = posData.Securities[i];

                InstrumentRequest instrumentRequest = new InstrumentRequest();
                instrumentRequest.Id = pos.InstrumentUid;
                instrumentRequest.IdType = InstrumentIdType.Uid;

                InstrumentResponse instrument = null;

                try
                {
                    _rateGateInstruments.WaitToProceed();
                    instrument = _instrumentsClient.GetInstrumentBy(instrumentRequest, _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error getting instrument data. Info: {message}", LogMessageType.System);
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error getting instrument data for " + pos.Figi + " " + ex.ToString(), LogMessageType.System);
                }

                PositionOnBoard newPos = new PositionOnBoard();

                newPos.PortfolioName = portf.Number;
                newPos.ValueCurrent = pos.Balance / instrument.Instrument.Lot;
                newPos.ValueBlocked = pos.Blocked / instrument.Instrument.Lot;

                if (newPos.ValueBlocked != 0)
                {
                    newPos.ValueCurrent += newPos.ValueBlocked;
                }

                newPos.ValueBegin = newPos.ValueCurrent;
                newPos.SecurityNameCode = instrument.Instrument.Ticker;

                sectionPoses.Add(newPos);

                if(pos.Balance < 0
                    && instrument.Instrument.Currency == "rub")
                {
                    spotShortValue += GetGoByShortSpotOperations(pos.InstrumentUid, newPos.ValueCurrent);
                }
            }

            for (int i = 0; i < posData.Futures.Count; i++)
            {
                PositionsFutures pos = posData.Futures[i];

                InstrumentRequest instrumentRequest = new InstrumentRequest();
                instrumentRequest.Id = pos.InstrumentUid;
                instrumentRequest.IdType = InstrumentIdType.Uid;
                InstrumentResponse instrument = null;

                try
                {
                    _rateGateInstruments.WaitToProceed();
                    instrument = _instrumentsClient.GetInstrumentBy(instrumentRequest, _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error getting instrument data. Info: {message}", LogMessageType.System);
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error getting instrument data for " + pos.Figi + " " + ex.ToString(), LogMessageType.System);
                }

                PositionOnBoard newPos = new PositionOnBoard();

                newPos.PortfolioName = portf.Number;
                newPos.ValueCurrent = pos.Balance / instrument.Instrument.Lot;
                newPos.ValueBlocked = pos.Blocked / instrument.Instrument.Lot;

                if (newPos.ValueBlocked != 0)
                {
                    newPos.ValueCurrent += newPos.ValueBlocked;
                }

                newPos.ValueBegin = newPos.ValueCurrent;
                newPos.SecurityNameCode = instrument.Instrument.Ticker;

                sectionPoses.Add(newPos);

                if (instrument.Instrument.Currency == "rub")
                {
                    futuresAndOptionsGO += GetGoByFuturesOrOptions(pos.InstrumentUid, newPos.ValueCurrent);
                }
            }

            for (int i = 0; i < posData.Options.Count; i++)
            {
                PositionsOptions pos = posData.Options[i];

                InstrumentRequest instrumentRequest = new InstrumentRequest();
                instrumentRequest.Id = pos.InstrumentUid;
                instrumentRequest.IdType = InstrumentIdType.Uid;
                InstrumentResponse instrument = null;

                try
                {
                    _rateGateInstruments.WaitToProceed();
                    instrument = _instrumentsClient.GetInstrumentBy(instrumentRequest, _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error getting instrument data. Info: {message}", LogMessageType.System);
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error getting instrument data for " + pos.InstrumentUid + " " + ex.ToString(), LogMessageType.System);
                }

                PositionOnBoard newPos = new PositionOnBoard();

                newPos.PortfolioName = portf.Number;
                newPos.ValueCurrent = pos.Balance / instrument.Instrument.Lot;
                newPos.ValueBlocked = pos.Blocked / instrument.Instrument.Lot;

                if (newPos.ValueBlocked != 0)
                {
                    newPos.ValueCurrent += newPos.ValueBlocked;
                }

                newPos.ValueBegin = newPos.ValueCurrent;
                newPos.SecurityNameCode = instrument.Instrument.Ticker;

                sectionPoses.Add(newPos);

                if (instrument.Instrument.Currency == "rub")
                {
                    futuresAndOptionsGO += GetGoByFuturesOrOptions(pos.InstrumentUid, newPos.ValueCurrent);
                }
            }

            PortfolioPosition rubPosition = null;
            for (int i = 0; i < portfolio.Positions.Count; i++)
            {
                if (portfolio.Positions[i].Figi == "RUB000UTSTOM")
                {
                    rubPosition = portfolio.Positions[i];
                    break;
                }
            }

            // Блокированные средства по портфелю целиком

            portf.ValueBlocked = 0;

            for (int i = 0; i < portfolio.Positions.Count; i++)
            {
                if (portfolio.Positions[i].InstrumentType == "currency")
                {
                    portf.ValueBlocked += GetValue(portfolio.Positions[i].BlockedLots)*GetValue(portfolio.Positions[i].AveragePositionPrice);
                }
            }

            // Денежная позиция в портфеле

            for (int i = 0; i < posData.Money.Count; i++)
            {
                MoneyValue posMoney = posData.Money[i];

                PositionOnBoard newPos = new PositionOnBoard();
                newPos.SecurityNameCode = posMoney.Currency;
                newPos.PortfolioName = portf.Number;

                if(newPos.SecurityNameCode == "rub")
                {
                    decimal valuePortfolio = GetValue(posMoney);

                    newPos.ValueCurrent = valuePortfolio - futuresAndOptionsGO - spotShortValue;
                }
                else
                {
                    newPos.ValueCurrent = GetValue(posMoney);
                }
                    
                newPos.ValueBegin = newPos.ValueCurrent;

                sectionPoses.Add(newPos);
            }

            // удаляем не существующие на текущий момент позиции из портфеля

            for(int i = 0; portf.PositionOnBoard != null && i < portf.PositionOnBoard.Count;i++)
            {
                PositionOnBoard pos = portf.PositionOnBoard[i];

                if(pos.ValueCurrent == 0)
                {
                    continue;
                }

                if(sectionPoses.Count == 0 
                    || sectionPoses.Find(p => p.SecurityNameCode == pos.SecurityNameCode) == null)
                {
                    portf.PositionOnBoard.RemoveAt(i);
                    i--;
                }
            }

            // обновляем в портфеле существующие позиции

            for (int i = 0; i < sectionPoses.Count; i++)
            {
                portf.SetNewPosition(sectionPoses[i]);
            }
        }

        private decimal GetGoByShortSpotOperations(string tickerId, decimal volume)
        {
            if (volume >= 0)
            {
                return 0;
            }

            Security mySecurity = _securities.Find(s => s.NameId == tickerId);

            if (mySecurity == null)
            {
                return 0;
            }

            GetLastPricesRequest request = new GetLastPricesRequest();
            request.InstrumentId.Add(tickerId);

            GetLastPricesResponse response = _marketDataServiceClient.GetLastPrices(request, _gRpcMetadata);

            if(response == null 
                || response.LastPrices == null
                || response.LastPrices.Count == 0)
            {
                return 0;
            }

            decimal lastPrice = GetValue(response.LastPrices[0].Price);

            if(lastPrice == 0)
            {
                return 0;
            }


            decimal result = - (volume * lastPrice * mySecurity.Lot) * 2;

            return result;

        }

        private decimal GetGoByFuturesOrOptions(string tickerId, decimal volume)
        {
            if (volume == 0)
            {
                return 0;
            }

            Security mySecurity = _securities.Find(s => s.NameId == tickerId);

            if (mySecurity == null)
            {
                return 0;
            }

            if (volume > 0)
            {
                decimal result = volume * mySecurity.MarginBuy;
                return result;
            }
            else
            {
                decimal result = -volume * mySecurity.MarginSell;
                return result;
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        // https://russianinvestments.github.io/investAPI/limits/
        private RateGate _rateGateMarketData = new RateGate(600, TimeSpan.FromMinutes(1));

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            if(ServerStatus == ServerConnectStatus.Disconnect
                || candleCount <= 0)
            {
                return null;
            }

            if(candleCount > 5000)
            {
                candleCount = 5000;
            }

            DateTime timeEnd = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone); // to MSK
            DateTime timeStart = timeEnd - TimeSpan.FromMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * (candleCount * 1.5));

            List<Candle> candles = GetCandleDataToSecurity(security, timeFrameBuilder, timeStart, timeEnd, timeStart);

            if(candles != null)
            {
                while (candles.Count > candleCount)
                {
                    candles.RemoveAt(0);
                }
            }

            return candles;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            startTime = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startTime, DateTimeKind.Unspecified), _mskTimeZone);
            endTime = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endTime, DateTimeKind.Unspecified), _mskTimeZone);
            actualTime = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(actualTime, DateTimeKind.Unspecified), _mskTimeZone);

            if (startTime != actualTime)
            {
                startTime = actualTime;
            }

            List<Candle> candles = new List<Candle>();
            TimeFrame tf = timeFrameBuilder.TimeFrame;

            int days = 1; // период, за который запрашивать свечи 

            if (tf == TimeFrame.Day)
            {
                days = 500;
            }
            else if (tf == TimeFrame.Hour2 ||
                     tf == TimeFrame.Hour4)
            {
                days = 60;
            }
            else if (tf == TimeFrame.Hour1)
            {
                days = 30;
            }

            else if(tf == TimeFrame.Min30)
            {
                days = 14;
            }
            else if (tf == TimeFrame.Min5
                || tf == TimeFrame.Min10
                || tf == TimeFrame.Min15
                || tf == TimeFrame.Min20)
            {
                days = 5;
            }

            while (startTime < endTime)
            {
                DateTime endDateTime = startTime.AddDays(days);
                if (endDateTime > endTime) // не заказываем лишних данных
                    endDateTime = endTime;

                List<Candle> range = GetCandleHistoryFromDays(startTime, endDateTime, security, tf);

                if (range == null) // Если запрошен некорректный таймфрейм, то возвращает null
                    return null;

                candles.AddRange(range);

                startTime = endDateTime;
            }

            // под конец фильтруем одинаковые от брокера
            return filterCorrectCandles(candles);
        }

        List<Candle> filterCorrectCandles(List<Candle> candles)
        {
            if (candles == null || candles.Count == 0)
                return candles;

            List<Candle> filtered = new List<Candle>();

            filtered.Add(candles[0]);
            for (int i = 1; i < candles.Count; i++)
            {
                Candle curCandle = candles[i];
                Candle prevCandle = candles[i - 1];

                if (curCandle.TimeStart == prevCandle.TimeStart)
                {
                    continue;
                }
                    
                filtered.Add(curCandle);
            }

            return filtered;
        }

        private List<Candle> GetCandleHistoryFromDays(DateTime fromDateTime, DateTime toDateTime, Security security, TimeFrame tf)
        {
            CandleInterval requestedCandleInterval = CreateTimeFrameInterval(tf);

            if (requestedCandleInterval == CandleInterval.Unspecified)
                return null;

            Timestamp from = Timestamp.FromDateTime(fromDateTime);
            Timestamp to = Timestamp.FromDateTime(toDateTime);

            _rateGateMarketData.WaitToProceed();

            GetCandlesResponse candlesResp = null;
            int retries = 3; // try to get 'em this many times

            while (candlesResp == null && retries-- > 0)
            {
                try
                {
                    GetCandlesRequest getCandlesRequest = new GetCandlesRequest();
                    getCandlesRequest.InstrumentId = security.NameId;
                    getCandlesRequest.From = from;
                    getCandlesRequest.To = to;
                    getCandlesRequest.Interval = requestedCandleInterval;
                    getCandlesRequest.CandleSourceType = _filterOutNonMarketData
                        ? GetCandlesRequest.Types.CandleSource.Exchange
                        : GetCandlesRequest.Types.CandleSource.IncludeWeekend;

                    candlesResp = _marketDataServiceClient.GetCandles(getCandlesRequest, _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);

                    if (message == "no server message")
                        SendLogMessage($"Couldn't get candles for {security.Name}. Info: probably invalid time interval {fromDateTime}UTC - {toDateTime}UTC", LogMessageType.System);
                    else
                        SendLogMessage($"Error getting candles for {security.Name}. Info: {message}", LogMessageType.System);
                }
                catch (Exception ex)
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        break; // connection broke before we could get candles
                    }

                    SendLogMessage($"Error getting candles for {security.Name}: " + ex.ToString(),
                        LogMessageType.System);
                }
            }

            List<Candle> candles = ConvertToOsEngineCandles(candlesResp, security);

            return candles;
        }

        // расписания торгов разных бирж по дням
        private Dictionary<DateTime, TradingSchedulesResponse> _tradingSchedules = new Dictionary<DateTime, TradingSchedulesResponse>();

        bool isTodayATradingDayForSecurity(Security security)
        {
            if (security == null)
                return true;

            string exchangeToAskSchedule = security.Exchange.Split('_')[0];

            TradingSchedulesResponse thisDaySchedules = null;

            var mskNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);
            var mskDate = mskNow.Date;

            if (_tradingSchedules.ContainsKey(mskDate))
            {
                thisDaySchedules = _tradingSchedules[mskDate];
            }
            else
            {
                Timestamp from = Timestamp.FromDateTime(DateTime.UtcNow.Date);
                Timestamp to = Timestamp.FromDateTime(TimeZoneInfo.ConvertTimeToUtc(mskDate.AddDays(1).AddTicks(-1), _mskTimeZone));

                TradingSchedulesRequest tradingSchedulesRequest = new TradingSchedulesRequest();
                tradingSchedulesRequest.From = from;
                tradingSchedulesRequest.To = to;

                try
                {
                    _rateGateInstruments.WaitToProceed();
                    thisDaySchedules = _instrumentsClient.TradingSchedules(tradingSchedulesRequest, _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error getting trading schedules. Info: {message}", LogMessageType.System);
                }
                catch (Exception ex)
                {
                    SendLogMessage($"Error fetching trading schedules: {ex.ToString()}", LogMessageType.System);
                }

                _tradingSchedules[mskDate] = thisDaySchedules;
            }

            if (thisDaySchedules == null)
                return true;

            TradingDay day = null;
            for (int i = 0; i < thisDaySchedules.Exchanges.Count; i++)
            {
                if (thisDaySchedules.Exchanges[i].Exchange == exchangeToAskSchedule)
                {
                    day = thisDaySchedules.Exchanges[i].Days[0];
                    break;
                }
            }

            if (day != null)
                return day.IsTradingDay;

            return true;
        }

        private List<Candle> ConvertToOsEngineCandles(GetCandlesResponse response, Security security)
        {
            List<Candle> candles = new List<Candle>();

            if (response == null)
                return candles;

            for (int i = 0; i < response.Candles.Count; i++)
            {
                HistoricCandle histCandle = response.Candles[i];

                Candle candle = new Candle();
                candle.Open = GetValue(histCandle.Open);
                candle.Close = GetValue(histCandle.Close);
                candle.High = GetValue(histCandle.High);
                candle.Low = GetValue(histCandle.Low);
                candle.Volume = histCandle.Volume;
                candle.TimeStart = TimeZoneInfo.ConvertTimeFromUtc(histCandle.Time.ToDateTime(), _mskTimeZone);

                candles.Add(candle);
            }

            return candles;
        }

        private CandleInterval CreateTimeFrameInterval(TimeFrame tf)
        {
            if (tf == TimeFrame.Min1)
            {
                return CandleInterval._1Min;
            }
            if (tf == TimeFrame.Min2)
            {
                return CandleInterval._2Min;
            }
            if (tf == TimeFrame.Min3)
            {
                return CandleInterval._3Min;
            }
            else if (tf == TimeFrame.Min5)
            {
                return CandleInterval._5Min;
            }
            else if (tf == TimeFrame.Min10)
            {
                return CandleInterval._10Min;
            }
            else if (tf == TimeFrame.Min15)
            {
                return CandleInterval._15Min;
            }
            else if (tf == TimeFrame.Min30)
            {
                return CandleInterval._30Min;
            }
            else if (tf == TimeFrame.Hour1)
            {
                return CandleInterval.Hour;
            }
            else if (tf == TimeFrame.Hour2)
            {
                return CandleInterval._2Hour;
            }
            else if (tf == TimeFrame.Hour4)
            {
                return CandleInterval._4Hour;
            }
            else if (tf == TimeFrame.Day)
            {
                return CandleInterval.Day;
            }

            return CandleInterval.Unspecified;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        #endregion

        #region 6 gRPC streams creation

        //private readonly string _gRPCHost = "sandbox-invest-public-api.tbank.ru:443"; // sandbox 
        private readonly string _gRPCHost = "https://invest-public-api.tinkoff.ru:443"; // prod  as of v1.40 should be tbank.ru but doesn't work due to SSL certificate issue
        private Metadata _gRpcMetadata;
        private GrpcChannel _channel;
        private CancellationTokenSource _cancellationTokenSource;
        private WebProxy _proxy;

        private UsersService.UsersServiceClient _usersClient;
        private OperationsService.OperationsServiceClient _operationsClient;
        private OperationsStreamService.OperationsStreamServiceClient _operationsStreamClient;
        private InstrumentsService.InstrumentsServiceClient _instrumentsClient;
        private MarketDataService.MarketDataServiceClient _marketDataServiceClient;
        private MarketDataStreamService.MarketDataStreamServiceClient _marketDataStreamClient;
        private OrdersService.OrdersServiceClient _ordersClient;
        private OrdersStreamService.OrdersStreamServiceClient _ordersStreamClient;

        private void GetUserLimits()
        {
            GetUserTariffRequest request = new GetUserTariffRequest();
            GetUserTariffResponse response = null;
            try
            {
                response = _usersClient.GetUserTariff(request, _gRpcMetadata);
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error getting user limits. Info: {message}", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.System);
            }

            if (response == null)
                return;

            string limits = "";
            for (int i = 0; i < response.StreamLimits.Count; i++)
            {
                StreamLimit sl = response.StreamLimits[i];
                limits += $"\n {sl.Open}/{sl.Limit}: {sl.Streams}";
            }

            SendLogMessage($"User stream limits: {limits}", LogMessageType.User);
        }

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
                    HttpHandler = new SocketsHttpHandler()
                    {
                        // KeepAlive настройки
                        KeepAlivePingDelay = TimeSpan.FromSeconds(10),
                        KeepAlivePingTimeout = TimeSpan.FromSeconds(5),
                        KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always,

                        // Прокси настройки
                        Proxy = _proxy,
                        UseProxy = _proxy != null,

                        // Оптимизации
                        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                        PooledConnectionLifetime = TimeSpan.FromHours(1),
                        EnableMultipleHttp2Connections = true
                    }
                });

                _usersClient = new UsersService.UsersServiceClient(_channel);
                _operationsClient = new OperationsService.OperationsServiceClient(_channel);
                _operationsStreamClient = new OperationsStreamService.OperationsStreamServiceClient(_channel);
                _instrumentsClient = new InstrumentsService.InstrumentsServiceClient(_channel);
                _ordersClient = new OrdersService.OrdersServiceClient(_channel);
                _ordersStreamClient = new OrdersStreamService.OrdersStreamServiceClient(_channel);
                _marketDataServiceClient = new MarketDataService.MarketDataServiceClient(_channel);
                _marketDataStreamClient = new MarketDataStreamService.MarketDataStreamServiceClient(_channel);

                try
                {
                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent();

                    GetUserLimits();
                    ConnectGRPCStreams();
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.System);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.System);
            }
        }

        private void ConnectGRPCStreams()
        {
            RepeatedField<string> accountsList = new RepeatedField<string>();
            for (int i = 0; i < _myPortfolios.Count; i++)
            {
                accountsList.Add(_myPortfolios[i].Number);
            }

            //_myTradesDataStream = _ordersStreamClient.TradesStream(new TradesStreamRequest
            //{
            //    Accounts = { accountsList }
            //}, headers: _gRpcMetadata, cancellationToken: _cancellationTokenSource.Token);

            _myOrderStateDataStream = _ordersStreamClient.OrderStateStream(new OrderStateStreamRequest
            {
                Accounts = { accountsList }
            }, headers: _gRpcMetadata, cancellationToken: _cancellationTokenSource.Token);

            _portfolioDataStream =
                _operationsStreamClient.PortfolioStream(new PortfolioStreamRequest { Accounts = { accountsList } },
                    headers: _gRpcMetadata, cancellationToken: _cancellationTokenSource.Token);

            _positionsDataStream =
                _operationsStreamClient.PositionsStream(new PositionsStreamRequest { Accounts = { accountsList } },
                    headers: _gRpcMetadata, cancellationToken: _cancellationTokenSource.Token);

            _lastPortfolioDataTime = DateTime.UtcNow;
        }

        #endregion

        #region 6 gRPC streams fast reconnect

        private DateTime _lastTryReconnectPortfolioStream;

        private bool TryReconnectPortfolioStream()
        {
            try
            {
                if (_lastTryReconnectPortfolioStream != DateTime.MinValue
                 && _lastTryReconnectPortfolioStream.AddSeconds(30) > DateTime.Now)
                {
                    return false;
                }

                _lastTryReconnectPortfolioStream = DateTime.Now;

                if (_portfolioDataStream != null)
                {
                    try
                    {
                        _portfolioDataStream.ResponseStream.ReadAllAsync();
                        _portfolioDataStream.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                RepeatedField<string> accountsList = new RepeatedField<string>();
                for (int i = 0; i < _myPortfolios.Count; i++)
                {
                    accountsList.Add(_myPortfolios[i].Number);
                }

                _portfolioDataStream =
                    _operationsStreamClient.PortfolioStream(new PortfolioStreamRequest { Accounts = { accountsList } },
                        headers: _gRpcMetadata, cancellationToken: _cancellationTokenSource.Token);
                _lastPortfolioDataTime = DateTime.UtcNow;
            }
            catch
            {
                return false;
            }

            try
            {
                GetPortfolios();
            }
            catch
            {
                // ignore
            }

            return true;
        }

        private DateTime _lastTryReconnectPositionsStream;

        private bool TryReconnectPositionsStream()
        {
            try
            {
                if (_lastTryReconnectPositionsStream != DateTime.MinValue
                    && _lastTryReconnectPositionsStream.AddSeconds(30) > DateTime.Now)
                {
                    return false;
                }

                _lastTryReconnectPositionsStream = DateTime.Now;

                if (_positionsDataStream != null)
                {
                    try
                    {
                        _positionsDataStream.ResponseStream.ReadAllAsync();
                        _positionsDataStream.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                RepeatedField<string> accountsList = new RepeatedField<string>();
                for (int i = 0; i < _myPortfolios.Count; i++)
                {
                    accountsList.Add(_myPortfolios[i].Number);
                }

                _positionsDataStream =
                    _operationsStreamClient.PositionsStream(new PositionsStreamRequest { Accounts = { accountsList } },
                        headers: _gRpcMetadata, cancellationToken: _cancellationTokenSource.Token);

                _lastPortfolioDataTime = DateTime.UtcNow;
            }
            catch
            {
                return false;
            }

            try
            {
                GetPortfolios();
            }
            catch
            {
                // ignore
            }

            return true;
        }

        private DateTime _lastTryReconnectOrdersStream;

        private bool TryReconnectOrdersStream()
        {
            try
            {
                if (_lastTryReconnectOrdersStream != DateTime.MinValue
                    && _lastTryReconnectOrdersStream.AddSeconds(30) > DateTime.Now)
                {
                    return false;
                }

                _lastTryReconnectOrdersStream = DateTime.Now;

                if (_myOrderStateDataStream != null)
                {
                    try
                    {
                        _myOrderStateDataStream.ResponseStream.ReadAllAsync();
                        _myOrderStateDataStream.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                RepeatedField<string> accountsList = new RepeatedField<string>();
                for (int i = 0; i < _myPortfolios.Count; i++)
                {
                    accountsList.Add(_myPortfolios[i].Number);
                }

                _myOrderStateDataStream = _ordersStreamClient.OrderStateStream(new OrderStateStreamRequest
                {
                    Accounts = { accountsList }
                }, headers: _gRpcMetadata, cancellationToken: _cancellationTokenSource.Token);

            }
            catch
            {
                return false;
            }

            return true;
        }

        private DateTime _lastTryReconnectDataStream;

        private bool TryReconnectDataStream()
        {
            try
            {
                lock (_marketDataStreamLocker)
                {
                    if (_lastTryReconnectDataStream != DateTime.MinValue
                    && _lastTryReconnectDataStream.AddSeconds(30) > DateTime.Now)
                    {
                        return false;
                    }

                    _lastTryReconnectDataStream = DateTime.Now;

                    if (_marketDataStream != null)
                    {
                        try
                        {
                            _marketDataStream.RequestStream.CompleteAsync().Wait();
                            _marketDataStream.ResponseStream.ReadAllAsync();
                            _marketDataStream.Dispose();
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    // 1 Пересоздаём стрим.

                    _marketDataStream = _marketDataStreamClient.MarketDataStream(headers: _gRpcMetadata,
                        cancellationToken: _cancellationTokenSource.Token);

                    // 2 Переподписываем поток ЛастПрайс

                    if (_marketDataRequestsLastPrice.Count > 0)
                    {
                        _rateGateSubscribe.WaitToProceed();

                        MarketDataRequest marketDataRequest = new MarketDataRequest();

                        SubscribeLastPriceRequest lpRequest = new SubscribeLastPriceRequest
                        {
                            SubscriptionAction = SubscriptionAction.Subscribe
                        };

                        for (int i = 0; i < _marketDataRequestsLastPrice.Count; i++)
                        {
                            lpRequest.Instruments.Add(_marketDataRequestsLastPrice[i].Instruments[0]);

                        }

                        marketDataRequest.SubscribeLastPriceRequest = lpRequest;
                        _marketDataStream.RequestStream.WriteAsync(marketDataRequest).Wait();
                        _rateGateSubscribe.WaitToProceed();
                    }

                    // 3 Переподписываем поток Стаканов

                    if (_marketDataRequestsOrderBook.Count > 0)
                    {
                        _rateGateSubscribe.WaitToProceed();

                        MarketDataRequest marketDataRequest = new MarketDataRequest();

                        SubscribeOrderBookRequest subscribeOrderBookRequest = new SubscribeOrderBookRequest
                        {
                            SubscriptionAction = SubscriptionAction.Subscribe
                        };

                        for (int i = 0; i < _marketDataRequestsOrderBook.Count; i++)
                        {
                            subscribeOrderBookRequest.Instruments.Add(_marketDataRequestsOrderBook[i].Instruments[0]);
                        }

                        marketDataRequest.SubscribeOrderBookRequest = subscribeOrderBookRequest;
                        _marketDataStream.RequestStream.WriteAsync(marketDataRequest).Wait();

                    }

                    // 3 Переподписываем поток Трейдов из ленты сделок

                    if (_marketDataRequestsTrades.Count > 0)
                    {
                        _rateGateSubscribe.WaitToProceed();
                        MarketDataRequest marketDataRequest = new MarketDataRequest();

                        SubscribeTradesRequest subscribeTradesRequest = new SubscribeTradesRequest
                        {
                            SubscriptionAction = SubscriptionAction.Subscribe,
                            TradeSource = _filterOutDealerTrades
                                ? TradeSourceType.TradeSourceExchange
                                : TradeSourceType.TradeSourceAll,
                            WithOpenInterest = true
                        };

                        for (int i = 0; i < _marketDataRequestsTrades.Count; i++)
                        {
                            subscribeTradesRequest.Instruments.Add(_marketDataRequestsTrades[i].Instruments[0]);
                        }

                        marketDataRequest.SubscribeTradesRequest = subscribeTradesRequest;

                        _marketDataStream.RequestStream.WriteAsync(marketDataRequest).Wait();
                    }
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        public event Action ForceCheckOrdersAfterReconnectEvent;

        #endregion

        #region 7 Security subscribe

        // Для всех типов подписок в методе установлены ограничения максимального количества запросов на подписку. Если количество запросов за минуту превысит 100, то для всех элементов будет установлен статус SUBSCRIPTION_STATUS_TOO_MANY_REQUESTS.
        // мы подписываемся на стаканы+сделки, поэтому лимит пополам
        private RateGate _rateGateSubscribe = new RateGate(50, TimeSpan.FromMinutes(1));
        List<Security> _streamSubscribedSecurities = new List<Security>();
        List<Security> _pollSubscribedSecurities = new List<Security>();

        private bool _useStreamForMarketData = true; // if we are over the limits, then stop using stream and turn to data polling (300+ subscribed secs)

        private AsyncDuplexStreamingCall<MarketDataRequest, MarketDataResponse> _marketDataStream;

        private AsyncServerStreamingCall<OrderStateStreamResponse> _myOrderStateDataStream;
        private AsyncServerStreamingCall<PortfolioStreamResponse> _portfolioDataStream;
        private AsyncServerStreamingCall<PositionsStreamResponse> _positionsDataStream;

        private DateTime _lastMarketDataTime = DateTime.MinValue;
        private DateTime _lastPortfolioDataTime = DateTime.MinValue;
        private DateTime _lastMyOrderStateDataTime = DateTime.MinValue;

        private List<SubscribeOrderBookRequest> _marketDataRequestsOrderBook = new List<SubscribeOrderBookRequest>();
        private List<SubscribeTradesRequest> _marketDataRequestsTrades = new List<SubscribeTradesRequest>();
        private List<SubscribeLastPriceRequest> _marketDataRequestsLastPrice = new List<SubscribeLastPriceRequest>();

        private string _marketDataStreamLocker = "_marketDataStreamLocker";

        public void Subscribe(Security security)
        {
            SubscribeLoop(0, security);
        }

        private void SubscribeLoop(int tryCount, Security security)
        {
            try
            {
                tryCount++;

                if (_streamSubscribedSecurities.Any(s => s.Name == security.Name) ||
                    _pollSubscribedSecurities.Any(s => s.Name == security.Name))
                {
                    return;
                }

                if (_useStreamForMarketData)
                {
                    _streamSubscribedSecurities.Add(security);

                    if (_streamSubscribedSecurities.Count >= 150)
                    {
                        _useStreamForMarketData = false;
                        SendLogMessage("Switching to polling mode for new market data subscriptions.", LogMessageType.System);
                    }
                }
                else
                {
                    _pollSubscribedSecurities.Add(security);
                    return; // Nothing more to do for polled securities
                }

                lock (_marketDataStreamLocker)
                {
                    if (_marketDataStream == null)
                    {
                        _marketDataStream = _marketDataStreamClient.MarketDataStream(headers: _gRpcMetadata,
                            cancellationToken: _cancellationTokenSource.Token);
                        SendLogMessage("Created market data stream", LogMessageType.System);
                    }

                    _rateGateSubscribe.WaitToProceed();

                    MarketDataRequest marketDataRequest = new MarketDataRequest();

                    if (security.SecurityType == SecurityType.Index) // only subscribe to last price info for indices
                    {
                        LastPriceInstrument instrument = new LastPriceInstrument
                        {
                            InstrumentId = security.NameId
                        };

                        SubscribeLastPriceRequest lpRequest = new SubscribeLastPriceRequest
                        {
                            SubscriptionAction = SubscriptionAction.Subscribe,
                            Instruments = { instrument },
                        };
                        marketDataRequest.SubscribeLastPriceRequest = lpRequest;
                        _marketDataRequestsLastPrice.Add(lpRequest);
                        _marketDataStream.RequestStream.WriteAsync(marketDataRequest).Wait();
                    }
                    else
                    {
                        // subscribe to trades and order books for everything else
                        TradeInstrument tradeInstrument = new TradeInstrument();
                        tradeInstrument.InstrumentId = security.NameId;

                        SubscribeTradesRequest subscribeTradesRequest = new SubscribeTradesRequest
                        {
                            SubscriptionAction = SubscriptionAction.Subscribe,
                            Instruments = { tradeInstrument },
                            TradeSource = _filterOutDealerTrades
                                ? TradeSourceType.TradeSourceExchange
                                : TradeSourceType.TradeSourceAll,
                            WithOpenInterest = true
                        };
                        marketDataRequest.SubscribeTradesRequest = subscribeTradesRequest;
                        _marketDataRequestsTrades.Add(subscribeTradesRequest);
                        _marketDataStream.RequestStream.WriteAsync(marketDataRequest).Wait();


                        // only one type of marketdata allowed in request so we need to new up request object
                        marketDataRequest = new MarketDataRequest();

                        // подписываемся на стаканы
                        OrderBookInstrument orderBookInstrument = new OrderBookInstrument();
                        orderBookInstrument.InstrumentId = security.NameId;
                        orderBookInstrument.Depth = 10;
                        orderBookInstrument.OrderBookType =
                            _filterOutDealerTrades ? OrderBookType.Exchange : OrderBookType.All;

                        SubscribeOrderBookRequest subscribeOrderBookRequest = new SubscribeOrderBookRequest
                        { SubscriptionAction = SubscriptionAction.Subscribe, Instruments = { orderBookInstrument } };
                        marketDataRequest.SubscribeOrderBookRequest = subscribeOrderBookRequest;
                        _marketDataRequestsOrderBook.Add(subscribeOrderBookRequest);

                        _marketDataStream.RequestStream.WriteAsync(marketDataRequest).Wait();
                    }
                }
            }
            catch (Exception exception)
            {
                for(int i = 0;i < _streamSubscribedSecurities.Count;i++)
                {

                    if (_streamSubscribedSecurities[i].NameId == security.NameId)
                    {
                        _streamSubscribedSecurities.RemoveAt(i);
                        break;
                    }
                }

                for (int i = 0; i < _pollSubscribedSecurities.Count; i++)
                {

                    if (_pollSubscribedSecurities[i].NameId == security.NameId)
                    {
                        _pollSubscribedSecurities.RemoveAt(i);
                        break;
                    }
                }

                if(tryCount < 3)
                {
                    SendLogMessage(
                        OsLocalization.Market.Label297 + tryCount + " " + security.Name + " \n" + exception.ToString(), LogMessageType.System);
                   
                    SubscribeLoop(tryCount, security);
                }
                else
                {
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        SendLogMessage(
                             OsLocalization.Market.Label298 + security.Name + " \n" + exception.ToString(), LogMessageType.Error);
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }

                    return;
                }
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion

        #region 8 Reading messages from data streams

        private Dictionary<string, OpenInterest> _openInterestData = new Dictionary<string, OpenInterest>(); // save open interest data to use later in trade updates

        private async void DataMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    if (_marketDataStream == null)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    if (await _marketDataStream.ResponseStream.MoveNext() == false)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (_marketDataStream == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    MarketDataResponse marketDataResponse = _marketDataStream.ResponseStream.Current;

                    if (marketDataResponse == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    _lastMarketDataTime = DateTime.UtcNow;

                    if (marketDataResponse.Ping != null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }
                    
                    if (marketDataResponse.OpenInterest != null)
                    {
                        Security security = GetSecurity(marketDataResponse.OpenInterest.InstrumentUid);
                        if (security == null)
                            continue;

                        if (_filterOutNonMarketData)
                        {
                            if (isTodayATradingDayForSecurity(security) == false)
                                continue;
                        }

                        _openInterestData[security.Name] = marketDataResponse.OpenInterest; // save open interest data to cache
                    }

                    if (marketDataResponse.Trade != null)
                    {
                        Security security = GetSecurity(marketDataResponse.Trade.InstrumentUid);
                        if (security == null)
                            continue;

                        if (_filterOutNonMarketData)
                        {
                            if (isTodayATradingDayForSecurity(security) == false)
                                continue;
                        }

                        Trade trade = new Trade();
                        trade.SecurityNameCode = security.Name;
                        trade.Price = GetValue(marketDataResponse.Trade.Price);
                        trade.Time = TimeZoneInfo.ConvertTimeFromUtc(marketDataResponse.Trade.Time.ToDateTime(), _mskTimeZone); // convert to MSK
                        trade.Id = trade.Time.Ticks.ToString();
                        trade.Side = marketDataResponse.Trade.Direction == TradeDirection.Buy ? Side.Buy : Side.Sell;
                        trade.Volume = marketDataResponse.Trade.Quantity;

                        if (_openInterestData.ContainsKey(security.Name))
                        {
                            trade.OpenInterest = _openInterestData[security.Name].OpenInterest_;
                        }

                        if (_ignoreMorningAuctionTrades && trade.Time.Hour < 9) // process only mornings
                        {
                            if (security.SecurityType == SecurityType.Futures)
                            {
                                if (trade.Time < trade.Time.Date.AddHours(9)) // futures start trading at 9
                                {
                                    continue;
                                }
                            }
                            else
                            {
                                if (trade.Time < trade.Time.Date.AddHours(7)) // options start trading at 7
                                {
                                    continue;
                                }
                            }
                        }

                        NewTradesEvent?.Invoke(trade);
                    }

                   
                    if (marketDataResponse.LastPrice != null)
                    {
                        ProcessLastPrice(marketDataResponse.LastPrice);
                    }

                    if (marketDataResponse.Orderbook != null)
                    {
                        Security security = GetSecurity(marketDataResponse.Orderbook.InstrumentUid);
                        if (security == null)
                            continue;

                        if (_filterOutNonMarketData)
                        {
                            if (isTodayATradingDayForSecurity(security) == false)
                                continue;
                        }

                        MarketDepth depth = new MarketDepth();
                        depth.SecurityNameCode = security.Name;
                        depth.Time = TimeZoneInfo.ConvertTimeFromUtc(marketDataResponse.Orderbook.Time.ToDateTime(), _mskTimeZone);// convert to MSK

                        if(marketDataResponse.Orderbook.LimitUp != null)
                        {
                            security.PriceLimitHigh = GetValue(marketDataResponse.Orderbook.LimitUp);
                        }
                        
                        if(marketDataResponse.Orderbook.LimitDown != null)
                        {
                            security.PriceLimitLow = GetValue(marketDataResponse.Orderbook.LimitDown);
                        }
   
                        for (int i = 0; i < marketDataResponse.Orderbook.Bids.Count; i++)
                        {
                            MarketDepthLevel newBid = new MarketDepthLevel();
                            newBid.Price = Convert.ToDouble(GetValue(marketDataResponse.Orderbook.Bids[i].Price));
                            newBid.Bid = marketDataResponse.Orderbook.Bids[i].Quantity;
                            depth.Bids.Add(newBid);
                        }

                        for (int i = 0; i < marketDataResponse.Orderbook.Asks.Count; i++)
                        {
                            MarketDepthLevel newAsk = new MarketDepthLevel();
                            newAsk.Price = Convert.ToDouble(GetValue(marketDataResponse.Orderbook.Asks[i].Price));
                            newAsk.Ask = marketDataResponse.Orderbook.Asks[i].Quantity;
                            depth.Asks.Add(newAsk);
                        }

                        if (_lastMdTime != DateTime.MinValue &&
                            _lastMdTime >= depth.Time)
                        {
                            depth.Time = _lastMdTime.AddMilliseconds(1);
                        }

                        _lastMdTime = depth.Time;

                        MarketDepthEvent?.Invoke(depth);
                    }
                }
                catch (RpcException ex)
                {
                    if(_isDisposedNow == true)
                    {
                        continue;
                    }

                    if(_isReconnectByPingMarketData)
                    {
                        continue;
                    }

                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        string message = GetGRPCErrorMessage(ex);

                        if (message.Contains("limit") == false)
                        {
                            // пробуем восстановить поток без перезапуска коннектора
                            
                            if (TryReconnectDataStream() == true)
                            {
                                SendLogMessage(OsLocalization.Market.Label295 + "\nMarket data", LogMessageType.System);
                                Thread.Sleep(1000);
                                continue;
                            }
                        }

                        // need to reconnect everything
                        if (ServerStatus != ServerConnectStatus.Disconnect)
                        {
                            SendLogMessage(OsLocalization.Market.Label294 + "\nMarket data", LogMessageType.System);
                            SendMessageOnReconnectInErrorLog();
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                        }
                        Thread.Sleep(5000);
                    }
                    Thread.Sleep(5000);
                }
                catch (Exception exception)
                {
                    if(exception.ToString().Contains("Cannot access a disposed"))
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    SendLogMessage(exception.ToString(), LogMessageType.System);
                    Thread.Sleep(5000);
                }
            }
        }

        // Чудо-поток для опроса последних цен инструментов и эмуляции стакана L1.
        // Работает только если количество подписок превышает лимит gRPC-потока

        private void LastPricesPoller()
        {
            Thread.Sleep(10000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect ||
                        _pollSubscribedSecurities.Count == 0)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }
                    
                    Thread.Sleep(500);

                    if (_filterOutNonMarketData)
                    {
                        if (_pollSubscribedSecurities.Count == 0)
                        {
                            continue;
                        }
                        if (isTodayATradingDayForSecurity(_pollSubscribedSecurities[0]) == false)
                            continue;
                    }
                    //var watch = System.Diagnostics.Stopwatch.StartNew();
                    //SendLogMessage($"Polling for {_pollSubscribedSecurities.Count} securities.", LogMessageType.System);

                    UpdateLastPrices(_pollSubscribedSecurities);

                    //watch.Stop();
                    //SendLogMessage($"Polling for {_pollSubscribedSecurities.Count} securities completed in {watch.ElapsedMilliseconds} ms.", LogMessageType.System);
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.System);
                    Thread.Sleep(5000);
                }
            }
        }

        public void UpdateLastPrices(List<Security> securitiesToPoll)
        {
            if (securitiesToPoll.Count == 0)
            {
                return;
            }

            List<string> instrumentIds = new List<string>();

            // Количество инструментов в списке не может быть больше 3000.
            // https://russianinvestments.github.io/investAPI/errors/
            // Поэтому разбиваем обновления на дозы по 3000 штуки
            for (int i = 0; i < securitiesToPoll.Count; i++)
            {
                instrumentIds.Add(securitiesToPoll[i].NameId);

                if (instrumentIds.Count == 3000)
                {
                    GetLastPrices(instrumentIds);

                    instrumentIds.Clear();
                }
            }

            GetLastPrices(instrumentIds);
        }

        private void GetLastPrices(List<string> instrumentIds)
        {
            _rateGateMarketData.WaitToProceed();
            GetLastPricesResponse priceResp = null;
            try
            {
                priceResp = _marketDataServiceClient.GetLastPrices(new GetLastPricesRequest
                {
                    InstrumentId = { instrumentIds }
                }, _gRpcMetadata);
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error getting last prices. Status: {ex.StatusCode}, Message: {message}, Details: {ex.ToString()}", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.System);
            }

            if (priceResp == null)
                return;

            for (int i = 0; i < priceResp.LastPrices.Count; i++)
            {
                ProcessLastPrice(priceResp.LastPrices[i]);
            }
        }

        private void ProcessLastPrice(LastPrice price)
        {
            Security mySec = GetSecurity(price.InstrumentUid);

            if (price.Price == null)
                return;

            if (mySec == null)
            {
                return;
            }

            Trade newTrade = new Trade();

            newTrade.SecurityNameCode = mySec.Name;
            newTrade.Time = TimeZoneInfo.ConvertTimeFromUtc(price.Time.ToDateTime(), _mskTimeZone);// convert to MSK
            newTrade.Price = GetValue(price.Price);
            newTrade.Side = Side.Buy;
            newTrade.Volume = 1;
            newTrade.Id = newTrade.Time.Ticks.ToString();
      
            if (_openInterestData.ContainsKey(mySec.Name))
            {
                newTrade.OpenInterest = _openInterestData[mySec.Name].OpenInterest_;
            }

            NewTradesEvent?.Invoke(newTrade);

            CreateFakeMdByTrade(newTrade);
        }

        private void CreateFakeMdByTrade(Trade trade)
        {
            List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

            MarketDepthLevel newBid = new MarketDepthLevel();
            newBid.Bid = Convert.ToDouble(trade.Volume);
            newBid.Price = Convert.ToDouble(trade.Price);
            bids.Add(newBid);

            MarketDepth depth = new MarketDepth();

            depth.SecurityNameCode = trade.SecurityNameCode;
            depth.Time = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);// convert to MSK
            depth.Bids = bids;

            List<MarketDepthLevel> asks = new List<MarketDepthLevel>();

            MarketDepthLevel newAsk = new MarketDepthLevel();
            newAsk.Ask = Convert.ToDouble(trade.Volume);
            newAsk.Price = Convert.ToDouble(trade.Price);
            asks.Add(newAsk);

            depth.Asks = asks;

            if (depth.Asks == null ||
                depth.Asks.Count == 0 ||
                depth.Bids == null ||
                depth.Bids.Count == 0)
            {
                return;
            }

            if (MarketDepthEvent != null)
            {
                MarketDepthEvent(depth);
            }
        }

        private DateTime _lastMdTime = DateTime.MinValue;

        public event Action<Trade> NewTradesEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        private async void PortfolioMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (_portfolioDataStream == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (await _portfolioDataStream.ResponseStream.MoveNext() == false)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (_portfolioDataStream == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    PortfolioStreamResponse portfolioResponse = _portfolioDataStream.ResponseStream.Current;
                    if (portfolioResponse == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    _lastPortfolioDataTime = DateTime.UtcNow;

                    if (portfolioResponse.Ping != null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (portfolioResponse.Portfolio != null)
                    {
                        Portfolio portf = _myPortfolios.Find((p) => p.Number == portfolioResponse.Portfolio.AccountId);

                        if (portf == null)
                        {
                            continue;
                        }

                        if (portfolioResponse.Portfolio.TotalAmountPortfolio != null)
                        {
                            portf.ValueCurrent = GetValue(portfolioResponse.Portfolio.TotalAmountPortfolio);
                        }
                        else
                        {
                            decimal resultValue = 0;

                            resultValue += GetValue(portfolioResponse.Portfolio.TotalAmountBonds);
                            resultValue += GetValue(portfolioResponse.Portfolio.TotalAmountCurrencies);
                            resultValue += GetValue(portfolioResponse.Portfolio.TotalAmountEtf);
                            resultValue += GetValue(portfolioResponse.Portfolio.TotalAmountFutures);
                            resultValue += GetValue(portfolioResponse.Portfolio.TotalAmountOptions);
                            resultValue += GetValue(portfolioResponse.Portfolio.TotalAmountShares);
                            resultValue += GetValue(portfolioResponse.Portfolio.TotalAmountSp);

                            portf.ValueCurrent = resultValue;
                        }

                        portf.UnrealizedPnl = GetValue(portfolioResponse.Portfolio.DailyYield);
                        UpdatePositionsInPortfolio(portfolioResponse.Portfolio);

                        PortfolioEvent!(_myPortfolios);
                    }
                }
                catch (RpcException exception)
                {
                    if (_isDisposedNow == true)
                    {
                        continue;
                    }

                    if(_isReconnectByPingPortfoliosData == true)
                    {
                        continue;
                    }

                    string message = GetGRPCErrorMessage(exception);
                    
                    if (message.Contains("limit") == false)
                    {
                        // пробуем восстановить поток без перезапуска коннектора
                        
                        if (TryReconnectPortfolioStream() == true)
                        {
                            SendLogMessage(OsLocalization.Market.Label295 + "\nPortfolio", LogMessageType.System);
                            Thread.Sleep(1000);
                            continue;
                        }
                    }

                    // need to reconnect everything
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        SendLogMessage(OsLocalization.Market.Label294 + "\nPortfolio" , LogMessageType.System);
                        SendMessageOnReconnectInErrorLog();
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                    Thread.Sleep(5000);
                }
                catch (Exception exception)
                {
                    if (exception.ToString().Contains("Cannot access a disposed"))
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    SendLogMessage(exception.ToString(), LogMessageType.System);
                    Thread.Sleep(5000);
                }
            }
        }

        private async void PositionsMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (_positionsDataStream == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (await _positionsDataStream.ResponseStream.MoveNext() == false)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (_positionsDataStream == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    PositionsStreamResponse positionsResponse = _positionsDataStream.ResponseStream.Current;
                    if (positionsResponse == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    _lastPortfolioDataTime = DateTime.UtcNow;

                    if (positionsResponse.Ping != null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (positionsResponse.Position != null)
                    {

                        PositionData posData = positionsResponse.Position;
                        Portfolio portf = _myPortfolios.Find((p) => p.Number == posData.AccountId);

                        if (portf == null)
                        {
                            continue;
                        }

                        for (int i = 0; i < posData.Securities.Count; i++)
                        {
                            PositionsSecurities pos = posData.Securities[i];

                            InstrumentRequest instrumentRequest = new InstrumentRequest();
                            instrumentRequest.Id = pos.InstrumentUid;
                            instrumentRequest.IdType = InstrumentIdType.Uid;

                            InstrumentResponse instrument = null;

                            try
                            {
                                _rateGateInstruments.WaitToProceed();
                                instrument = _instrumentsClient.GetInstrumentBy(instrumentRequest, _gRpcMetadata);
                            }
                            catch (RpcException ex)
                            {
                                string message = GetGRPCErrorMessage(ex);
                                SendLogMessage($"Error getting instrument data. Info: {message}", LogMessageType.System);
                            }
                            catch (Exception ex)
                            {
                                SendLogMessage("Error getting instrument data for " + pos.Figi + " " + ex.ToString(), LogMessageType.System);
                            }

                            PositionOnBoard newPos = new PositionOnBoard();

                            newPos.PortfolioName = portf.Number;
                            newPos.ValueCurrent = pos.Balance / instrument.Instrument.Lot;
                            newPos.ValueBlocked = pos.Blocked / instrument.Instrument.Lot;

                            if(newPos.ValueBlocked != 0)
                            {
                                newPos.ValueCurrent += newPos.ValueBlocked;
                            }

                            newPos.SecurityNameCode = instrument.Instrument.Ticker;

                            portf.SetNewPosition(newPos);
                        }

                        for (int i = 0; i < posData.Futures.Count; i++)
                        {
                            PositionsFutures pos = posData.Futures[i];

                            InstrumentRequest instrumentRequest = new InstrumentRequest();
                            instrumentRequest.Id = pos.InstrumentUid;
                            instrumentRequest.IdType = InstrumentIdType.Uid;
                            InstrumentResponse instrument = null;

                            try
                            {
                                _rateGateInstruments.WaitToProceed();
                                instrument = _instrumentsClient.GetInstrumentBy(instrumentRequest, _gRpcMetadata);
                            }
                            catch (RpcException ex)
                            {
                                string message = GetGRPCErrorMessage(ex);
                                SendLogMessage($"Error getting instrument data. Info: {message}", LogMessageType.System);
                            }
                            catch (Exception ex)
                            {
                                SendLogMessage("Error getting instrument data for " + pos.Figi + " " + ex.ToString(), LogMessageType.System);
                            }

                            PositionOnBoard newPos = new PositionOnBoard();

                            newPos.PortfolioName = portf.Number;
                            newPos.ValueCurrent = pos.Balance / instrument.Instrument.Lot;
                            newPos.ValueBlocked = pos.Blocked / instrument.Instrument.Lot;
                            newPos.SecurityNameCode = instrument.Instrument.Ticker;

                            portf.SetNewPosition(newPos);
                            
                        }

                        for (int i = 0; i < posData.Options.Count; i++)
                        {
                            PositionsOptions pos = posData.Options[i];

                            InstrumentRequest instrumentRequest = new InstrumentRequest();
                            instrumentRequest.Id = pos.InstrumentUid;
                            instrumentRequest.IdType = InstrumentIdType.Uid;
                            InstrumentResponse instrument = null;

                            try
                            {
                                _rateGateInstruments.WaitToProceed();
                                instrument = _instrumentsClient.GetInstrumentBy(instrumentRequest, _gRpcMetadata);
                            }
                            catch (RpcException ex)
                            {
                                string message = GetGRPCErrorMessage(ex);
                                SendLogMessage($"Error getting instrument data. Info: {message}", LogMessageType.System);
                            }
                            catch (Exception ex)
                            {
                                SendLogMessage("Error getting instrument data for " + pos.InstrumentUid + " " + ex.ToString(), LogMessageType.System);
                            }

                            PositionOnBoard newPos = new PositionOnBoard();

                            newPos.PortfolioName = portf.Number;
                            newPos.ValueCurrent = pos.Balance / instrument.Instrument.Lot;
                            newPos.ValueBlocked = pos.Blocked / instrument.Instrument.Lot;
                            newPos.SecurityNameCode = instrument.Instrument.Ticker;

                            portf.SetNewPosition(newPos);
                        }

                        for (int i = 0; i < posData.Money.Count; i++)
                        {
                            PositionsMoney pos = posData.Money[i];

                            if(pos.AvailableValue.Currency == "rub")
                            {
                                portf.ValueBlocked = GetValue(pos.BlockedValue);

                                List<PositionOnBoard> posesInPortfolio = portf.PositionOnBoard;

                                for(int j = 0; posesInPortfolio != null && j < posesInPortfolio.Count;j++)
                                {
                                    if (posesInPortfolio[j].SecurityNameCode == "rub")
                                    {
                                        posesInPortfolio[j].ValueBlocked = portf.ValueBlocked;
                                    }
                                }
                            }
                        }

                        if (PortfolioEvent != null)
                        {
                            PortfolioEvent(_myPortfolios);
                        }
                    }
                }
                catch (RpcException exception)
                {
                    if (_isDisposedNow == true)
                    {
                        continue;
                    }

                    if (_isReconnectByPingPortfoliosData == true)
                    {
                        continue;
                    }

                    string message = GetGRPCErrorMessage(exception);

                    if (message.Contains("limit") == false)
                    {
                        // пробуем восстановить поток без перезапуска коннектора

                        if (TryReconnectPositionsStream() == true)
                        {
                            SendLogMessage(OsLocalization.Market.Label295 + "\nPositions", LogMessageType.System);
                            Thread.Sleep(1000);
                            continue;
                        }
                    }

                    // need to reconnect everything
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        SendLogMessage(OsLocalization.Market.Label294 + "\nPositions", LogMessageType.System);
                        SendMessageOnReconnectInErrorLog();
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                    Thread.Sleep(5000);
                }
                catch (Exception exception)
                {
                    if (exception.ToString().Contains("Cannot access a disposed"))
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    SendLogMessage(exception.ToString(), LogMessageType.System);
                    Thread.Sleep(5000);
                }
            }
        }

        private async void OrderStateMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (_myOrderStateDataStream == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (await _myOrderStateDataStream.ResponseStream.MoveNext() == false)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (_myOrderStateDataStream == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    OrderStateStreamResponse orderStateResponse = _myOrderStateDataStream.ResponseStream.Current;
                    if (orderStateResponse == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    _lastMyOrderStateDataTime = DateTime.UtcNow;

                    if (orderStateResponse.Ping != null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (orderStateResponse.OrderState != null)
                    {
                        Security security = GetSecurity(orderStateResponse.OrderState.InstrumentUid);
                        OrderStateStreamResponse.Types.OrderState state = orderStateResponse.OrderState;

                        if (security == null)
                        {
                            Thread.Sleep(1);
                            continue;
                        }

                        Order order = new Order();

                        lock(_orderNumbersLocker)
                        {
                            if (!_orderNumbers.ContainsKey(state.OrderRequestId)) // значит сделка была вручную и это не наш ордер
                            {
                                continue;
                            }

                            order.NumberUser = _orderNumbers[state.OrderRequestId];
                        }

                        order.NumberMarket = state.OrderId;
                        order.SecurityNameCode = security.Name;
                        order.PortfolioNumber = state.AccountId;
                        order.Side = state.Direction == OrderDirection.Buy ? Side.Buy : Side.Sell;
                        order.TypeOrder = state.OrderType == OrderType.Limit || state.OrderType == OrderType.Unspecified
                            ? OrderPriceType.Limit
                            : OrderPriceType.Market;

                        order.Volume = state.LotsRequested;
                        order.VolumeExecute = state.LotsExecuted;

                        if (order.TypeOrder == OrderPriceType.Limit)
                        {
                            if (_orderPrices.TryGetValue(state.OrderRequestId, out decimal originalPrice))
                            {
                                order.Price = originalPrice;
                            }
                            else
                            {
                                // Fallback to potentially incorrect price and log an error
                                order.Price = GetValue(state.OrderPrice)/security.PriceStepCost*security.PriceStep;
                                SendLogMessage($"Could not find original price for order request ID {state.OrderRequestId}. Using price from broker.", LogMessageType.System);
                            }
                        }
                        else
                        {
                            order.Price = 0;
                        }
                        order.TimeCallBack = state.CreatedAt?.ToDateTime() != null ? TimeZoneInfo.ConvertTimeFromUtc(state.CreatedAt.ToDateTime(), _mskTimeZone) : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);// convert to MSK
                        order.SecurityClassCode = security.NameClass;

                        if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusUnspecified)
                        {
                            order.State = OrderStateType.None;
                        }
                        else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusFill)
                        {
                            order.State = OrderStateType.Done;
                        }
                        else if (state.ExecutionReportStatus ==
                                 OrderExecutionReportStatus.ExecutionReportStatusRejected)
                        {
                            order.State = OrderStateType.Fail;
                        }
                        else if (state.ExecutionReportStatus ==
                                 OrderExecutionReportStatus.ExecutionReportStatusCancelled)
                        {
                            order.State = OrderStateType.Cancel;
                        }
                        else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusNew)
                        {
                            order.State = OrderStateType.Active;

                            if (order.TypeOrder == OrderPriceType.Limit && order.Price == 0)
                                continue; // ignore such status
                        }
                        else if (state.ExecutionReportStatus ==
                                 OrderExecutionReportStatus.ExecutionReportStatusPartiallyfill)
                        {
                            order.State = OrderStateType.Partial;
                            if (state.CompletionTime != null)
                            {
                                order.State = OrderStateType.Cancel; // partially filled orders never go to cancelled state 
                            }
                        }

                        if (order.State == OrderStateType.Done ||
                            order.State == OrderStateType.Fail ||
                            order.State == OrderStateType.Cancel)
                        {
                            _orderPrices.TryRemove(state.OrderRequestId, out _);
                        }

                        if (orderStateResponse.OrderState.Trades != null)
                        {
                            for (int i = 0; i < orderStateResponse.OrderState.Trades.Count; i++)
                            {
                                OrderTrade orderTrade = orderStateResponse.OrderState.Trades[i];

                                MyTrade trade = new MyTrade();
                                trade.SecurityNameCode = security.Name;
                                trade.Price = GetValue(orderTrade.Price);
                                trade.Volume = orderTrade.Quantity / security.Lot;
                                trade.NumberOrderParent = order.NumberMarket;
                                trade.NumberTrade = orderTrade.TradeId;
                                trade.Time = TimeZoneInfo.ConvertTimeFromUtc(orderTrade.DateTime.ToDateTime(), _mskTimeZone); // convert to MSK

                                if (trade.Time == DateTime.Parse("01.01.1970 03:00:00"))
                                {
                                    DateTime tTime = orderTrade.DateTime.ToDateTime();
                                    SendLogMessage($"TInvest sent trade with time == {tTime} for trade Id {orderTrade.TradeId}", LogMessageType.System);

                                    trade.Time = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone); // fix trade time
                                }

                                trade.Side = order.Side;

                                MyTradeEvent?.Invoke(trade);
                            }
                        }

                        MyOrderEvent?.Invoke(order);
                    }
                }
                catch (RpcException ex)
                {
                    if (_isDisposedNow == true)
                    {
                        continue;
                    }

                    if(_isReconnectByOrdersData == true)
                    {
                        continue;
                    }

                    string message = GetGRPCErrorMessage(ex);

                    if (message.Contains("limit") == false)
                    {
                        // пробуем восстановить поток без перезапуска коннектора

                        if (TryReconnectOrdersStream() == true)
                        {
                            SendLogMessage(OsLocalization.Market.Label295 + "\nOrders", LogMessageType.System);

                            if(ForceCheckOrdersAfterReconnectEvent != null)
                            {
                                ForceCheckOrdersAfterReconnectEvent();
                            }

                            Thread.Sleep(1000);
                            continue;
                        }
                    }

                    // need to reconnect everything
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        SendLogMessage(OsLocalization.Market.Label294 + "\nOrders", LogMessageType.System);
                        SendMessageOnReconnectInErrorLog();
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                    Thread.Sleep(5000);
                }
                catch (Exception exception)
                {
                    if (exception.ToString().Contains("Cannot access a disposed"))
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    SendLogMessage(exception.ToString(), LogMessageType.System);
                    Thread.Sleep(5000);
                }
            }
        }

        private DateTime _lastErrorMessageOnReconnectTime;

        private void SendMessageOnReconnectInErrorLog()
        {
            if (_lastErrorMessageOnReconnectTime.AddSeconds(5) > DateTime.Now)
            {
                return;
            }

            _lastErrorMessageOnReconnectTime = DateTime.Now;

            SendLogMessage(OsLocalization.Market.Label296, LogMessageType.Error);
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        #endregion

        #region 9 Trade

        private RateGate _rateGateOrders = new RateGate(98, TimeSpan.FromMinutes(1)); // https://russianinvestments.github.io/investAPI/limits/
        private string _rageGateOrdersLocker = "_rageGateOrdersLocker";

        private RateGate _rateGatePostOrders = new RateGate(500, TimeSpan.FromMinutes(1));
        private string _rageGatePostOrdersLocker = "_rageGatePostOrdersLocker";

        public void SendOrder(Order order)
        {
            lock(_rageGatePostOrdersLocker)
            {
                _rateGatePostOrders.WaitToProceed();
            }

            try
            {
                Security security = _streamSubscribedSecurities.Find((sec) =>
                    sec.Name == order.SecurityNameCode);

                if (security == null)
                {
                    security = _pollSubscribedSecurities.Find((sec) => sec.Name == order.SecurityNameCode);
                }

                if(security == null)
                {
                    security = _securities.Find((sec) =>
                    sec.Name == order.SecurityNameCode);
                }

                PostOrderRequest request = new PostOrderRequest();
                request.Direction = order.Side == Side.Buy ? OrderDirection.Buy : OrderDirection.Sell;
                request.OrderType = order.TypeOrder == OrderPriceType.Limit ? OrderType.Limit : OrderType.Market; // еще есть BestPrice
                request.Quantity = Convert.ToInt32(order.Volume);
                request.Price = ConvertToQuotation(order.Price);
                request.ConfirmMarginTrade = true;

                if (security.SecurityType == SecurityType.Bond) // set price type to points in case security type is bond
                {
                    request.PriceType = PriceType.Point;
                }

                request.InstrumentId = security.NameId;
                request.AccountId = order.PortfolioNumber;
                request.TimeInForce = TimeInForceType.TimeInForceDay; // по-умолчанию сегодняшний день

                // генерируем новый номер ордера и добавляем его в словарь
                Guid newUid = Guid.NewGuid();
                string orderId = newUid.ToString();

                lock (_orderNumbersLocker)
                {
                    _orderNumbers.Add(orderId, order.NumberUser);
                }

                _orderPrices[orderId] = order.Price;

                request.OrderId = orderId;

                PostOrderResponse response = null;

                try
                {
                    response = _ordersClient.PostOrder(request, _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);

                    if(message.Contains("Not enough assets"))
                    {
                        message = OsLocalization.Market.Label301;
                    }
                    else if (message.Contains("The price is too high"))
                    {
                        message = OsLocalization.Market.Label302;
                    }
                    else if (message.Contains("The price is outside the limits for"))
                    {
                        message = OsLocalization.Market.Label304;
                    }

                    SendLogMessage(OsLocalization.Market.Label291 +
                            "\n" + message +
                            "\n" + order.SecurityNameCode 
                            + ", " + OsLocalization.Market.Message21 + order.Volume
                            + ", " + OsLocalization.Market.Label303 + " " + order.Price + " " + order.Side
                            , LogMessageType.Error);

                    order.State = OrderStateType.Fail;
                    MyOrderEvent!(order);

                    return;
                }
                catch (Exception exception)
                {
                    SendLogMessage(OsLocalization.Market.Label291 + "\n" + exception.Message, LogMessageType.Error);

                    order.State = OrderStateType.Fail;
                    MyOrderEvent!(order);

                    return;
                }

                if (response.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusRejected)
                {
                    order.State = OrderStateType.Fail;
                }
                else
                {
                    order.State = OrderStateType.Active;
                    order.NumberMarket = response.OrderId;
                }

                MyOrderEvent!(order);
            }
            catch (Exception exception)
            {
                SendLogMessage(OsLocalization.Market.Label291 + "\n" + exception, LogMessageType.Error);
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            try
            {
                lock(_rageGateOrdersLocker)
                {
                    _rateGateOrders.WaitToProceed();
                }

                if (order.TypeOrder == OrderPriceType.Market)
                {
                    SendLogMessage("Can`t change price to market order", LogMessageType.System);
                    return;
                }

                lock (_orderNumbersLocker)
                {
                    // remove old Uuid/NumberUser from list
                    foreach (KeyValuePair<string, int> kvp in _orderNumbers)
                    {
                        if (kvp.Value == order.NumberUser)
                        {
                            _orderNumbers.Remove(kvp.Key);
                            break;
                        }
                    }
                }
                ReplaceOrderRequest request = new ReplaceOrderRequest();
                request.AccountId = order.PortfolioNumber;
                request.OrderId = order.NumberMarket;
                request.ConfirmMarginTrade = true;

                lock (_orderNumbersLocker)
                {
                    Guid newUid = Guid.NewGuid();
                    string orderId = newUid.ToString();

                    _orderNumbers.Add(orderId, order.NumberUser);
                    request.IdempotencyKey = orderId;

                    _orderPrices[orderId] = newPrice;
                }

                request.Quantity = Convert.ToInt32(order.Volume - order.VolumeExecute);

                if (request.Quantity <= 0 || order.State != OrderStateType.Active)
                {
                    SendLogMessage("Can`t change order price because it`s not in Active state", LogMessageType.System);
                    return;
                }

                request.Price = ConvertToQuotation(newPrice);

                PostOrderResponse response = null;

                try
                {
                    response = _ordersClient.ReplaceOrder(request, _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error replacing order. Info: {message}", LogMessageType.System);

                    order.State = OrderStateType.Fail;
                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }

                    return;
                }
                catch (Exception exception)
                {
                    SendLogMessage("Error on order Execution \n" + exception.Message, LogMessageType.System);

                    order.State = OrderStateType.Fail;
                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }

                    return;
                }

                if (response.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusRejected)
                {
                    order.State = OrderStateType.Fail;
                }
                else
                {
                    // А теперь записываем новые данные для нового ордера
                    order.State = OrderStateType.Active;
                    order.NumberMarket = response.OrderId;

                    lock(_orderNumbersLocker)
                    {
                        order.NumberUser = _orderNumbers[response.OrderRequestId];
                    }
                    
                    order.Price = newPrice;
                    order.Volume = request.Quantity;
                    order.VolumeExecute = 0;
                    order.TimeCallBack = TimeZoneInfo.ConvertTimeFromUtc(response.ResponseMetadata.ServerTime.ToDateTime(), _mskTimeZone);// convert to MSK
                }

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(order);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.System);
            }
        }

        List<string> _cancelOrderNums = new List<string>();

        private string _cancelOrdersLocker = "_cancelOrdersLocker";

        public bool CancelOrder(Order order)
        {
            try
            {
                lock(_cancelOrdersLocker)
                {
                    int countTryRevokeOrder = 0;

                    for (int i = 0; i < _cancelOrderNums.Count; i++)
                    {
                        if (_cancelOrderNums[i].Equals(order.NumberMarket))
                        {
                            countTryRevokeOrder++;
                        }
                    }

                    if (countTryRevokeOrder >= 2)
                    {
                        SendLogMessage(OsLocalization.Market.Label292 + " " + order.SecurityNameCode,
                            LogMessageType.Error);
                        return false;
                    }

                    _cancelOrderNums.Add(order.NumberMarket);

                    while (_cancelOrderNums.Count > 100)
                    {
                        _cancelOrderNums.RemoveAt(0);
                    }
                }

                lock (_rageGateOrdersLocker)
                {
                    _rateGateOrders.WaitToProceed();
                }

                CancelOrderRequest request = new CancelOrderRequest();
                request.AccountId = order.PortfolioNumber;
                request.OrderId = order.NumberMarket;

                CancelOrderResponse response = null;

                try
                {
                    response = _ordersClient.CancelOrder(request, _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage( OsLocalization.Market.Label293 + "\n" + message, LogMessageType.Error);
                }
                catch (Exception exception)
                {
                    SendLogMessage(OsLocalization.Market.Label293 + "\n" +
                        exception.Message + "  " + order.SecurityClassCode, LogMessageType.Error);
                }

                if (response != null)
                {
                    return true;
                }
                else
                {
                    OrderStateType state = GetOrderStatus(order);

                    if (state == OrderStateType.None)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(OsLocalization.Market.Label293 + "\n" + exception.ToString(), LogMessageType.System);
            }
            return false;
        }

        public void CancelAllOrders()
        {
            List<Order> orders = GetAllOrdersFromExchange(true);

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];

                if (order.State == OrderStateType.Active)
                {
                    CancelOrder(order);
                }
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            List<Order> orders = GetAllOrdersFromExchange(true);

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];

                if (order.State == OrderStateType.Active
                    && order.SecurityNameCode == security.Name)
                {
                    CancelOrder(order);
                }
            }
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllOrdersFromExchange(true);

            for (int i = 0; orders != null && i < orders.Count; i++)
            {
                if (orders[i] == null)
                {
                    continue;
                }

                if (orders[i].State != OrderStateType.Active
                    && orders[i].State != OrderStateType.Partial
                    && orders[i].State != OrderStateType.Pending)
                {
                    continue;
                }

                orders[i].TimeCreate = orders[i].TimeCallBack;

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(orders[i]);
                }
            }
        }

        public OrderStateType GetOrderStatusWithTrades(Order order, bool processTrades)
        {
            lock (_rageGateOrdersLocker)
            {
                _rateGateOrders.WaitToProceed();
            }

            try
            {
                // запрашиваем состояние ордера
                GetOrderStateRequest getOrderStateRequest = new GetOrderStateRequest();
                getOrderStateRequest.OrderId = order.NumberMarket;
                getOrderStateRequest.AccountId = order.PortfolioNumber;

                OrderState state = null;
                try
                {
                    state = _ordersClient.GetOrderState(getOrderStateRequest, _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error getting order state. Info: {message}", LogMessageType.System);

                    Thread.Sleep(1);
                    return OrderStateType.None;
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error getting order state " + order.SecurityNameCode + " exception: " + ex.ToString(), LogMessageType.System);
                    SendLogMessage("Server data was: " + state.ToString(), LogMessageType.System);

                    Thread.Sleep(1);
                    return OrderStateType.None;
                }
                Order newOrder = new Order();

                Security security = _securities.FirstOrDefault(s => s.Name == order.SecurityNameCode);
                if (security == null)
                {
                    SendLogMessage($"Error getting security for {order.SecurityNameCode} in GetOrderStatusWithTrades", LogMessageType.System);
                    return OrderStateType.None;
                }

                lock (_orderNumbersLocker)
                {
                    if (!_orderNumbers.ContainsKey(state.OrderRequestId))
                    {
                        order.NumberUser = order.NumberUser != 0 ? order.NumberUser : NumberGen.GetNumberOrder(StartProgram.IsOsTrader);
                        _orderNumbers.Add(state.OrderRequestId, order.NumberUser);
                    }
                    newOrder.NumberUser = _orderNumbers[state.OrderRequestId];
                }
               
                newOrder.NumberMarket = state.OrderId;
                newOrder.SecurityNameCode = order.SecurityNameCode;
                newOrder.PortfolioNumber = order.PortfolioNumber;
                newOrder.Side = state.Direction == OrderDirection.Buy ? Side.Buy : Side.Sell;
                newOrder.TypeOrder = state.OrderType == OrderType.Limit
                    ? OrderPriceType.Limit
                    : OrderPriceType.Market;

                newOrder.Volume = state.LotsRequested;
                newOrder.VolumeExecute = state.LotsExecuted;
                newOrder.Price = order.TypeOrder == OrderPriceType.Limit ? GetValue(state.InitialSecurityPrice) / security.PriceStepCost * security.PriceStep : 0;
                newOrder.TimeCallBack = TimeZoneInfo.ConvertTimeFromUtc(state.OrderDate.ToDateTime(), _mskTimeZone);// convert to MSK
                newOrder.SecurityClassCode = order.SecurityClassCode;

                if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusUnspecified)
                {
                    newOrder.State = OrderStateType.None;
                }
                else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusFill)
                {
                    newOrder.State = OrderStateType.Done;
                }
                else if (state.ExecutionReportStatus ==
                         OrderExecutionReportStatus.ExecutionReportStatusRejected)
                {
                    newOrder.State = OrderStateType.Fail;
                }
                else if (state.ExecutionReportStatus ==
                         OrderExecutionReportStatus.ExecutionReportStatusCancelled)
                {
                    newOrder.State = OrderStateType.Cancel;
                }
                else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusNew)
                {
                    newOrder.State = OrderStateType.Active;
                }
                else if (state.ExecutionReportStatus ==
                         OrderExecutionReportStatus.ExecutionReportStatusPartiallyfill)
                {
                    newOrder.State = OrderStateType.Partial;
                }

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(newOrder);
                }

                if (processTrades && (newOrder.State == OrderStateType.Done || newOrder.State == OrderStateType.Partial))
                {
                    // add all trades for this order
                    for (int i = 0; i < state.Stages.Count; i++)
                    {
                        OrderStage stage = state.Stages[i];

                        MyTrade trade = new MyTrade();

                        trade.SecurityNameCode = order.SecurityNameCode;
                        trade.Price = GetValue(stage.Price) / security.PriceStepCost * security.PriceStep;
                        trade.Volume = stage.Quantity;
                        trade.NumberOrderParent = state.OrderId;
                        trade.NumberTrade = stage.TradeId;
                        trade.Time = TimeZoneInfo.ConvertTimeFromUtc(stage.ExecutionTime.ToDateTime(), _mskTimeZone);// convert to MSK
                        trade.Side = state.Direction == OrderDirection.Buy
                            ? Side.Buy
                            : Side.Sell;

                        MyTradeEvent?.Invoke(trade);
                    }
                }

                return newOrder.State;
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error getting order state. Info: {message}", LogMessageType.System);
            }
            catch (Exception exception)
            {
                SendLogMessage("Get order state request error. " + exception.ToString(), LogMessageType.System);
            }

            return OrderStateType.None;
        }

        public OrderStateType GetOrderStatus(Order order)
        {
           return GetOrderStatusWithTrades(order, true);
        }

        private List<Order> GetAllOrdersFromExchange(bool onlyActive)
        {
            List<Order> orders = new List<Order>();

            for (int i = 0; i < _myPortfolios.Count; i++)
            {
                List<Order> newOrders = GetAllOrdersFromExchangeByPortfolio(_myPortfolios[i].Number,onlyActive);
                if (newOrders != null && newOrders.Count > 0)
                {
                    orders.AddRange(newOrders);
                }
            }

            return orders;
        }

        private List<Order> GetAllOrdersFromExchangeByPortfolio(string accountId, bool onlyActive)
        {
            lock (_rageGateOrdersLocker)
            {
                _rateGateOrders.WaitToProceed();
            }

            if (_securities == null 
                || _securities.Count == 0)
            {
                return null;
            }

            try
            {
                GetOrdersRequest getOrdersRequest = new GetOrdersRequest();
                getOrdersRequest.AccountId = accountId;

                if(onlyActive == false)
                {
                    getOrdersRequest.AdvancedFilters = new GetOrdersRequest.Types.GetOrdersRequestFilters();
                    getOrdersRequest.AdvancedFilters.ExecutionStatus.Add(OrderExecutionReportStatus.ExecutionReportStatusCancelled);
                    getOrdersRequest.AdvancedFilters.ExecutionStatus.Add(OrderExecutionReportStatus.ExecutionReportStatusRejected);
                    getOrdersRequest.AdvancedFilters.ExecutionStatus.Add(OrderExecutionReportStatus.ExecutionReportStatusFill);

                    getOrdersRequest.AdvancedFilters.From = DateTime.UtcNow.Date.ToTimestamp();
                    getOrdersRequest.AdvancedFilters.To = DateTime.UtcNow.ToTimestamp();
                }

                GetOrdersResponse response = _ordersClient.GetOrders(getOrdersRequest, _gRpcMetadata);

                if (response != null)
                {
                    List<Order> osEngineOrders = new List<Order>();

                    for (int i = 0; i < response.Orders.Count; i++)
                    {
                        OrderState state = response.Orders[i];
                        Security security = GetSecurity(state.InstrumentUid);

                        if(security == null)
                        {
                            continue;
                        }

                        Order newOrder = new Order();

                        newOrder.SecurityNameCode = security.Name;
                        newOrder.Volume = state.LotsRequested;
                        newOrder.VolumeExecute = state.LotsExecuted;
                        newOrder.PortfolioNumber = accountId;
                        newOrder.TypeOrder = state.OrderType == OrderType.Limit
                            ? OrderPriceType.Limit
                            : OrderPriceType.Market;

                        if (state.OrderType == OrderType.Limit)
                        {
                            newOrder.Price = GetValue(state.InitialSecurityPrice) / security.PriceStepCost * security.PriceStep;
                        }

                        string orderId = state.OrderRequestId;

                        lock(_orderNumbersLocker)
                        {
                            if (_orderNumbers.ContainsKey(orderId))
                            {
                                newOrder.NumberUser = _orderNumbers[orderId];
                            }
                            else
                            {
                                return null;
                            }

                        }

                        newOrder.NumberMarket = state.OrderId;
                        newOrder.TimeCallBack = TimeZoneInfo.ConvertTimeFromUtc(state.OrderDate.ToDateTime(), _mskTimeZone);// convert to MSK
                        newOrder.Side = state.Direction == OrderDirection.Buy ? Side.Buy : Side.Sell;

                        if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusUnspecified)
                        {
                            newOrder.State = OrderStateType.None;
                        }
                        else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusFill)
                        {
                            newOrder.State = OrderStateType.Done;
                            newOrder.TimeDone = TimeZoneInfo.ConvertTimeFromUtc(state.OrderDate.ToDateTime(), _mskTimeZone);// convert to MSK
                        }
                        else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusRejected)
                        {
                            newOrder.State = OrderStateType.Fail;
                        }
                        else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusCancelled)
                        {
                            newOrder.State = OrderStateType.Cancel;
                            newOrder.TimeCancel = TimeZoneInfo.ConvertTimeFromUtc(state.OrderDate.ToDateTime(), _mskTimeZone);// convert to MSK
                        }
                        else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusNew)
                        {
                            newOrder.State = OrderStateType.Active;
                        }
                        else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusPartiallyfill)
                        {
                            newOrder.State = OrderStateType.Partial;
                        }

                        osEngineOrders.Add(newOrder);
                    }

                    return osEngineOrders;
                }
                else
                {
                    SendLogMessage("Get all orders request error. ", LogMessageType.System);
                }
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);

                if(message.Contains("no server message") == false)
                {
                    SendLogMessage($"Error getting all orders. Info: {message}", LogMessageType.System);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Get all orders request error. " + exception.ToString(), LogMessageType.System);
            }

            return null;
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            // 1 берём все ордера

            List<Order> orders = new List<Order>();

            for (int i = 0; i < _myPortfolios.Count; i++)
            {
                List<Order> newOrders = GetAllOrdersFromExchangeByPortfolio(_myPortfolios[i].Number, true);
                if (newOrders != null && newOrders.Count > 0)
                {
                    orders.AddRange(newOrders);
                }
            }

            // 2 оставляем только активные

            List<Order> ordersActive = new List<Order>();

            for(int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];

                if(order.State != OrderStateType.Active
                    && order.State != OrderStateType.Pending
                    && order.State != OrderStateType.Partial)
                {
                    continue;
                }

                ordersActive.Add(order);
            }

            if(ordersActive.Count > 1)
            {
                ordersActive = ordersActive.OrderBy(x => x.TimeCallBack).ToList();
            }

            // 3 берём из массива по индексам

            List<Order> resultExit = new List<Order>();

            if (ordersActive.Count !=  0
                && startIndex < ordersActive.Count)
            {
                if (startIndex + count < ordersActive.Count)
                {
                    resultExit = ordersActive.GetRange(startIndex, count);
                }
                else
                {
                    resultExit = ordersActive.GetRange(startIndex, ordersActive.Count - startIndex);
                }
            }

            return resultExit;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            // 1 берём все ордера

            List<Order> orders = new List<Order>();

            for (int i = 0; i < _myPortfolios.Count; i++)
            {
                List<Order> newOrders = GetAllOrdersFromExchangeByPortfolio(_myPortfolios[i].Number, false);
                if (newOrders != null && newOrders.Count > 0)
                {
                    orders.AddRange(newOrders);
                }
            }

            // 2 оставляем только исторические, не активные ордера

            List<Order> ordersDontActive = new List<Order>();

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];

                if (order.State == OrderStateType.Active
                    || order.State == OrderStateType.Pending
                    || order.State == OrderStateType.Partial)
                {
                    continue;
                }
                ordersDontActive.Add(order);
            }

            if (ordersDontActive.Count > 1)
            {
                ordersDontActive = ordersDontActive.OrderBy(x => x.TimeCallBack).ToList();
            }

            // 3 берём из массива по индексам

            List<Order> resultExit = new List<Order>();

            if (ordersDontActive.Count != 0
                && startIndex < ordersDontActive.Count)
            {
                if (startIndex + count < ordersDontActive.Count)
                {
                    resultExit = ordersDontActive.GetRange(startIndex, count);
                }
                else
                {
                    resultExit = ordersDontActive.GetRange(startIndex, ordersDontActive.Count - startIndex);
                }
            }

            return resultExit;
        }

        #endregion

        #region 10 Helpers

        private string GetGRPCErrorMessage(RpcException exception)
        {
            string message = "no server message";
            string trackingId = "";

            if (exception.Trailers == null)
                return message;

            for (int i = 0; i < exception.Trailers.Count; i++)
            {
                if (exception.Trailers[i].Key == "x-tracking-id")
                    trackingId = exception.Trailers[i].Value;

                if (exception.Trailers[i].Key == "message")
                    message = exception.Trailers[i].Value;
            }

            if (trackingId.Length > 0)
            {
                message = "Tracking id: " + trackingId + "; Message: " + message;
            }

            return message;
        }
        private Security GetSecurity(string instrumentId)
        {
            for (int i = 0; i < _securities.Count; i++)
            {
                if (_securities[i].NameId == instrumentId)
                {
                    return _securities[i];
                }
            }

            return null;
        }

        private Quotation ConvertToQuotation(decimal value)
        {
            const decimal nanoFactor = 1_000_000_000;
            long wholePart = (long)value;

            Quotation quotation = new Quotation();

            quotation.Units = wholePart;
            quotation.Nano = (int)((value - wholePart) * nanoFactor);

            return quotation;
        }

        public decimal GetValue(Quotation quotation)
        {
            if (quotation == null)
                return 0.0m;

            if (quotation.Units == 0 && quotation.Nano == 0)
                return 0.0m;

            decimal bigDecimal = Convert.ToDecimal(quotation.Units);
            bigDecimal += Convert.ToDecimal(quotation.Nano) / 1000000000;

            return bigDecimal;
        }

        public decimal GetValue(MoneyValue moneyValue)
        {
            if (moneyValue == null)
                return 0.0m;

            if (moneyValue.Units == 0 && moneyValue.Nano == 0)
                return 0.0m;

            decimal bigDecimal = Convert.ToDecimal(moneyValue.Units);
            bigDecimal += Convert.ToDecimal(moneyValue.Nano) / 1000000000;

            return bigDecimal;
        }

        #endregion

        #region 11 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        #endregion
    }
}