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
using System.Threading;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Tinkoff.InvestApi.V1;
using Option = Tinkoff.InvestApi.V1.Option;
using Candle = OsEngine.Entity.Candle;
using Order = OsEngine.Entity.Order;
using Trade = OsEngine.Entity.Trade;
using Security = OsEngine.Entity.Security;
using Portfolio = OsEngine.Entity.Portfolio;

namespace OsEngine.Market.Servers.TinkoffInvestments
{
    public class TinkoffInvestmentsServer : AServer
    {
        public TinkoffInvestmentsServer()
        {
            TinkoffInvestmentsServerRealization realization = new TinkoffInvestmentsServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamToken, "");
            CreateParameterBoolean(OsLocalization.Market.UseStock, true);
            CreateParameterBoolean(OsLocalization.Market.UseFutures, true);
            CreateParameterBoolean(OsLocalization.Market.UseOptions, false); // с некоторого времени торговля опционами не доступна по API Т-Инвестиций
            CreateParameterBoolean(OsLocalization.Market.UseOther, false);
            CreateParameterString("Custom unique terminal id (order prefix <= 3 symbols)", "000");
            CreateParameterBoolean("Filter out non-market data (holiday trading)", true);
            CreateParameterBoolean("Filter out dealer trades", false);
        }
    }

    public class TinkoffInvestmentsServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public TinkoffInvestmentsServerRealization()
        {
            ServerTime = DateTime.UtcNow;
            
            Thread worker = new Thread(ConnectionCheckThread);
            worker.Name = "CheckAliveTinkoff";
            worker.Start();

            Thread worker2 = new Thread(DataMessageReader);
            worker2.Name = "DataMessageReaderTinkoff";
            worker2.Start();

            Thread worker3 = new Thread(PortfolioMessageReader);
            worker3.Name = "PortfolioMessageReaderTinkoff";
            worker3.Start();

            Thread worker4 = new Thread(PositionsMessageReader);
            worker4.Name = "PositionsMessageReaderTinkoff";
            worker4.Start();

            Thread worker5 = new Thread(MyTradesMessageReader);
            worker5.Name = "MyTradesAndOrdersMessageReaderTinkoff";
            worker5.Start();

            Thread worker6 = new Thread(LastPricesPoller);
            worker6.Name = "LastPricesPollingTinkoff";
            worker6.Start();
        }

        public void Connect()
        {
            try
            {
                _myPortfolios.Clear();
                _subscribedSecurities.Clear();
             
                SendLogMessage("Start TinkoffInvestments Connection", LogMessageType.System);

                _accessToken = ((ServerParameterString)ServerParameters[0]).Value;
                _customTerminalId = ((ServerParameterString)ServerParameters[5]).Value;
                _filterOutNonMarketData = ((ServerParameterBool)ServerParameters[6]).Value;
                _filterOutDealerTrades = ((ServerParameterBool)ServerParameters[7]).Value;

                if (string.IsNullOrEmpty(_accessToken))
                {
                    SendLogMessage("Connection terminated. You must specify the api token. You can get it on the TinkoffInvestments website",
                        LogMessageType.Error);
                    return;
                }

                CreateStreamsConnection();
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
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

                    bool shitHappenedWithStreams = false;

                    if (_marketDataStream != null && _lastMarketDataTime.AddMinutes(3) < DateTime.UtcNow)
                    {
                        shitHappenedWithStreams = true;
                    }

                    if (_portfolioDataStream != null && _lastPortfolioDataTime.AddMinutes(3) < DateTime.UtcNow)
                    {
                        shitHappenedWithStreams = true;
                    }

                    if (_myTradesDataStream != null && _lastMyTradesDataTime.AddMinutes(3) < DateTime.UtcNow)
                    {
                        shitHappenedWithStreams = true;
                    }

                    if (shitHappenedWithStreams)
                    {
                        if (ServerStatus == ServerConnectStatus.Connect)
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                        }
                    }
                }
                catch(Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }
        
        public void Dispose()
        {
            // останавливаем чтение всех потоков
            if (_marketDataStream != null)
            {
                try
                {
                    _marketDataStream.RequestStream.CompleteAsync().Wait();
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error cancelling stream", LogMessageType.Error);
                }

                SendLogMessage("Completed exchange with market data stream", LogMessageType.System);
            }

            if (_cancellationTokenSource != null)
            {
                try
                {
                    _cancellationTokenSource.Cancel();
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error disposing stream", LogMessageType.Error);
                }
            }

            if (_marketDataStream != null)
            {
                try
                {
                    _marketDataStream.Dispose();
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error disposing stream", LogMessageType.Error);
                }
            }

            if (_portfolioDataStream != null)
            {
                try
                {
                    _portfolioDataStream.Dispose();
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error disposing stream", LogMessageType.Error);
                }
            }

            if (_positionsDataStream != null)
            {
                try
                {
                    _positionsDataStream.Dispose();
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error disposing stream", LogMessageType.Error);
                }
            }

            if (_myTradesDataStream != null)
            {
                try
                {
                    _myTradesDataStream.Dispose();
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error disposing stream", LogMessageType.Error);
                }
            }

            _marketDataStream = null;
            _portfolioDataStream = null;
            _positionsDataStream = null;
            _myTradesDataStream = null;
            
            SendLogMessage("Connection Closed by TinkoffInvestments. Data streams Closed Event", LogMessageType.System);

            _subscribedSecurities.Clear();
            _myPortfolios.Clear();
            _lastMarketDataTime = DateTime.UtcNow;
            _lastMdTime = DateTime.UtcNow;
            _lastMyTradesDataTime = DateTime.UtcNow;
            _lastPortfolioDataTime = DateTime.UtcNow;

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public DateTime ServerTime { get; set; }

        public ServerType ServerType => ServerType.TinkoffInvestments;

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

        private string _customTerminalId; // id to avoid duplicate order error
        private bool _filterOutNonMarketData; // отфльтровать кухню выходного дня
        private bool _filterOutDealerTrades; // отфльтровать кухонные сделки (дилерские, внутренние)
        private string _accessToken;

        private Dictionary<string, int> _orderNumbers = new Dictionary<string, int>();

        #endregion

        #region 3 Securities

        private RateGate _rateGateInstruments = new RateGate(200, TimeSpan.FromMinutes(1));

        public void GetSecurities()
        {
            _useStock = ((ServerParameterBool)ServerParameters[1]).Value;
            _useFutures = ((ServerParameterBool)ServerParameters[2]).Value;
            _useOptions = ((ServerParameterBool)ServerParameters[3]).Value;
            _useOther = ((ServerParameterBool)ServerParameters[4]).Value;

            _rateGateInstruments.WaitToProceed();
            CurrenciesResponse currenciesResponse = null;
            try
            {
                currenciesResponse = _instrumentsClient.Currencies(new InstrumentsRequest(), headers: _gRpcMetadata);
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error loading currencies. Info: {message}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage("Error loading securities", LogMessageType.Error);
            }

            UpdateCurrenciesFromServer(currenciesResponse);

            if (_useStock || _useOther)
            {
                _rateGateInstruments.WaitToProceed();
                SharesResponse result = null;
                try
                {
                    result = _instrumentsClient.Shares(new InstrumentsRequest(), headers: _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error getting shares data. Info: {message}", LogMessageType.Error);
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error loading securities", LogMessageType.Error);
                }

                UpdateSharesFromServer(result);
            }
            
            if (_useFutures)
            {
                _rateGateInstruments.WaitToProceed();
                FuturesResponse result = null;
                try
                {
                    result = _instrumentsClient.Futures(new InstrumentsRequest(), headers: _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error getting futures data. Info: {message}", LogMessageType.Error);
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error loading securities", LogMessageType.Error);
                }

                UpdateFuturesFromServer(result);
            }

            if (_useOptions)
            {
                // https://russianinvestments.github.io/investAPI/faq_instruments/ v1.23
                // Сейчас торговля опционами через API недоступна. 
                SendLogMessage("Options trading not supported by T-Invest API", LogMessageType.Error);

                //_rateGateInstruments.WaitToProceed();

                //OptionsResponse result = null;
                //try
                //{
                //    result = _instrumentsClient.Options(new InstrumentsRequest(), headers: _gRpcMetadata);
                //}
                //catch (RpcException ex)
                //{
                //    string message = GetGRPCErrorMessage(ex);
                //    SendLogMessage($"Error getting options data. Info: {message}", LogMessageType.Error);
                //}
                //catch (Exception ex)
                //{
                //    SendLogMessage("Error loading securities", LogMessageType.Error);
                //}

                //UpdateOptionsFromServer(result);
            }

            if (_useOther)
            {
                _rateGateInstruments.WaitToProceed();
                BondsResponse result = null;
                try
                {
                    result = _instrumentsClient.Bonds(new InstrumentsRequest(), headers: _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error getting bonds data. Info: {message}", LogMessageType.Error);
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error loading securities", LogMessageType.Error);
                }

                UpdateBondsFromServer(result);

                _rateGateInstruments.WaitToProceed();
                EtfsResponse etfs = null;

                try
                {
                    etfs = _instrumentsClient.Etfs(new InstrumentsRequest(), headers: _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error getting Etfs data. Info: {message}", LogMessageType.Error);
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error loading securities", LogMessageType.Error);
                }

                UpdateEtfsFromServer(etfs);

                _rateGateInstruments.WaitToProceed();
                IndicativesResponse indicatives = null;

                try
                {
                    indicatives = _instrumentsClient.Indicatives(new IndicativesRequest(), headers: _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error getting indicatives data. Info: {message}", LogMessageType.Error);
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error loading securities", LogMessageType.Error);
                }

                UpdateIndicativesFromServer(indicatives);
            }

            if (_securities.Count > 0)
            {
                SendLogMessage("Securities loaded. Count: " + _securities.Count, LogMessageType.System);

                if (SecurityEvent != null)
                {
                    SecurityEvent.Invoke(_securities);
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
                    
                    
                    newSecurity.State = SecurityStateType.Activ;
                    _securities.Add(newSecurity);
                }

            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading stocks: {e.Message}", LogMessageType.Error);
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


                    newSecurity.State = SecurityStateType.Activ;
                    _securities.Add(newSecurity);
                }

            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading bonds: {e.Message}", LogMessageType.Error);
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


                    newSecurity.NameClass = SecurityType.Index.ToString() + " " + item.Currency;

                    newSecurity.SecurityType = SecurityType.Index;
                    newSecurity.Lot = item.Lot;


                    newSecurity.State = SecurityStateType.Activ;
                    _securities.Add(newSecurity);
                }

            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading ETFs: {e.Message}", LogMessageType.Error);
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

                    if (item.BuyAvailableFlag == false)
                        continue;

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


                    newSecurity.State = SecurityStateType.Activ;
                    _securities.Add(newSecurity);
                }

            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading indicatives: {e.Message}", LogMessageType.Error);
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


                    newSecurity.State = SecurityStateType.Activ;
                    _securities.Add(newSecurity);
                }

            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading currency pairs: {e.Message}", LogMessageType.Error);
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


                    newSecurity.NameClass = SecurityType.Futures.ToString();

                    newSecurity.SecurityType = SecurityType.Futures;
                    newSecurity.Lot = item.Lot;
                    newSecurity.Go = GetValue(item.InitialMarginOnBuy); // есть еще при продаже (одинаковые?)

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
                SendLogMessage($"Error loading futures: {e.Message}", LogMessageType.Error);
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
                    
                    newSecurity.State = SecurityStateType.Activ;
                    _securities.Add(newSecurity);
                }
            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading options: {e.Message}", LogMessageType.Error);
            }
        }

        private List<Security> _securities = new List<Security>();
       
        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

        public void GetPortfolios()
        {
            GetAccountsResponse accountsResponse = null;
            try
            {
                accountsResponse = _usersClient.GetAccounts(new GetAccountsRequest(), _gRpcMetadata);
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error getting user portfolios. Info: {message}", LogMessageType.Error);
                return;
            }
            catch (Exception e)
            {                
                SendLogMessage($"Error getting user portfolios: {e.Message}", LogMessageType.Error);
            }

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
                    catch (RpcException ex)
                    {
                        string message = GetGRPCErrorMessage(ex);
                        SendLogMessage($"Error getting user portfolios. Info: {message}", LogMessageType.Error);
                    }
                    catch (Exception ex)
                    {
                        SendLogMessage("Error getting portfolio.", LogMessageType.Error);
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
                if(PortfolioEvent != null)
                {
                    PortfolioEvent(_myPortfolios);
                }
            }

            ActivateCurrentPortfolioListening();
        }

        private void GetPortfolios(PortfolioResponse portfolioResponse)
        {
            Portfolio myPortfolio = _myPortfolios.Find(p => p.Number == portfolioResponse.AccountId);

            if (myPortfolio == null)
            {
                myPortfolio = new Portfolio();
                myPortfolio.Number = portfolioResponse.AccountId;
                myPortfolio.ValueCurrent = portfolioResponse.TotalAmountPortfolio != null ? GetValue(portfolioResponse.TotalAmountPortfolio) : 1;
                myPortfolio.ValueBegin = myPortfolio.ValueCurrent;
                _myPortfolios.Add(myPortfolio);
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
                SendLogMessage($"Error getting positions in portfolio. Info: {message}", LogMessageType.Error);
            }
            catch
            {
                SendLogMessage("Error getting positions in portfolio", LogMessageType.Error);
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
                    SendLogMessage($"Error getting instrument data. Info: {message}", LogMessageType.Error);
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error getting instrument data for " + pos.Figi + " " + ex.ToString(), LogMessageType.Error);
                }

                PositionOnBoard newPos = new PositionOnBoard();

                newPos.PortfolioName = portf.Number;
                newPos.ValueCurrent = pos.Balance/instrument.Instrument.Lot;
                newPos.ValueBlocked = pos.Blocked/instrument.Instrument.Lot;
                newPos.ValueBegin = newPos.ValueCurrent;
                newPos.SecurityNameCode = instrument.Instrument.Ticker;

                sectionPoses.Add(newPos);
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
                    SendLogMessage($"Error getting instrument data. Info: {message}", LogMessageType.Error);
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error getting instrument data for " + pos.Figi + " " + ex.ToString(), LogMessageType.Error);
                }

                PositionOnBoard newPos = new PositionOnBoard();

                newPos.PortfolioName = portf.Number;
                newPos.ValueCurrent = pos.Balance / instrument.Instrument.Lot;
                newPos.ValueBlocked = pos.Blocked / instrument.Instrument.Lot;
                newPos.ValueBegin = newPos.ValueCurrent;
                newPos.SecurityNameCode = instrument.Instrument.Ticker;

                sectionPoses.Add(newPos);
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
                    SendLogMessage($"Error getting instrument data. Info: {message}", LogMessageType.Error);
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error getting instrument data for " + pos.InstrumentUid + " " + ex.ToString(), LogMessageType.Error);
                }

                PositionOnBoard newPos = new PositionOnBoard();

                newPos.PortfolioName = portf.Number;
                newPos.ValueCurrent = pos.Balance / instrument.Instrument.Lot;
                newPos.ValueBlocked = pos.Blocked / instrument.Instrument.Lot;
                newPos.ValueBegin = newPos.ValueCurrent;
                newPos.SecurityNameCode = instrument.Instrument.Ticker;

                sectionPoses.Add(newPos);
            }
            
            for (int i = 0; i < posData.Money.Count; i++) // posData.Blocked обработать отдельно?
            {
                MoneyValue posMoney = posData.Money[i];
           
                PositionOnBoard newPos = new PositionOnBoard();

                newPos.PortfolioName = portf.Number;
                newPos.ValueCurrent = GetValue(posMoney);
                newPos.ValueBegin = newPos.ValueCurrent;
                
                newPos.SecurityNameCode = posMoney.Currency;

                sectionPoses.Add(newPos);
            }

            for (int i = 0; i < sectionPoses.Count; i++)
            {
                portf.SetNewPosition(sectionPoses[i]);
            }
        }
        
        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        // https://russianinvestments.github.io/investAPI/limits/
        private RateGate _rateGateMarketData = new RateGate(300, TimeSpan.FromMinutes(1));
        
        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            DateTime timeStart = DateTime.UtcNow - TimeSpan.FromMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * candleCount);
            DateTime timeEnd = DateTime.UtcNow;

            List<Candle> candles = GetCandleDataToSecurity(security, timeFrameBuilder, timeStart, timeEnd, timeStart);
        
            return candles;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            if (startTime != actualTime)
            {
                startTime = actualTime;
            }

            List<Candle> candles = new List<Candle>();
            TimeFrame tf = timeFrameBuilder.TimeFrame;

            int days = 1; // период, за который запрашивать свечи 

            if (tf == TimeFrame.Hour1 ||
                tf == TimeFrame.Hour2 ||
                tf == TimeFrame.Hour4)
            {
                days = 7; // Tinkoff api позволяет запрашивать большие интервалы данных для таймфреймов более 1 часа
            }
            else if (tf == TimeFrame.Day)
            {
                days = 35;
            }
            
            while (startTime < endTime)
            {
                DateTime endDateTime = startTime.AddDays(days);
                if (endDateTime > endTime) // не заказываем лишних данных
                    endDateTime = endTime;

                List<Candle> range = GetCandleHistoryFromDays(startTime, endDateTime, security, tf);

                if (range == null)
                { // Если запрошен некорректный таймфрейм, то возвращает null
                    return null;
                }

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
                    continue;

                filtered.Add(curCandle);
            }

            return filtered;
        }

        private List<Candle> GetCandleHistoryFromDays(DateTime fromDateTime, DateTime toDateTime, Security security, TimeFrame tf)
        {
            CandleInterval requestedCandleInterval = CreateTimeFrameInterval(tf);

            if (requestedCandleInterval == CandleInterval.Unspecified)
                return null;
            
            Timestamp from = Timestamp.FromDateTime(fromDateTime.ToUniversalTime());
            Timestamp to = Timestamp.FromDateTime(toDateTime.ToUniversalTime());

            _rateGateMarketData.WaitToProceed();
            
            GetCandlesResponse candlesResp = null;
            try
            {
                GetCandlesRequest getCandlesRequest = new GetCandlesRequest();
                getCandlesRequest.InstrumentId = security.NameId;
                getCandlesRequest.From = from;
                getCandlesRequest.To = to;
                getCandlesRequest.Interval = requestedCandleInterval;
                
                candlesResp = _marketDataServiceClient.GetCandles(getCandlesRequest, _gRpcMetadata);
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error getting candles. Info: {message}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
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

            if (_tradingSchedules.ContainsKey(DateTime.UtcNow.Date))
            {
                thisDaySchedules = _tradingSchedules[DateTime.UtcNow.Date];
            }
            else
            {
                Timestamp from = Timestamp.FromDateTime(DateTime.UtcNow.Date);
                Timestamp to = Timestamp.FromDateTime(DateTime.UtcNow.Date.AddHours(23));

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
                    SendLogMessage($"Error getting trading schedules. Info: {message}", LogMessageType.Error);
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error fetching trading schedules", LogMessageType.Error);
                }

                _tradingSchedules[DateTime.UtcNow.Date] = thisDaySchedules;
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
                HistoricCandle canTin = response.Candles[i];

                Candle candle = new Candle();
                candle.Open = GetValue(canTin.Open);
                candle.Close = GetValue(canTin.Close);
                candle.High = GetValue(canTin.High);
                candle.Low = GetValue(canTin.Low);
                candle.Volume = canTin.Volume;
                
                candle.TimeStart = canTin.Time.ToDateTime();

                if (_filterOutNonMarketData) // пока без учета календаря
                {  
                    bool isTradingDay = true;
                    
                    // брокер не дает расписание для исторических данных, поэтому для сегодняшних данных можно использовать расписание
                    if (candle.TimeStart.Date.Equals(DateTime.UtcNow.Date))
                    {
                        isTradingDay = isTodayATradingDayForSecurity(security);
                    }
                    else
                    {
                        // а для исторических просто считаем, что выходные - неторговые дни
                        if (candle.TimeStart.DayOfWeek == DayOfWeek.Saturday ||
                            candle.TimeStart.DayOfWeek == DayOfWeek.Sunday)
                            isTradingDay = false;
                    }
                    
                    if (isTradingDay == false)   
                        continue;
                }

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

        //private readonly string _gRPCHost = "sandbox-invest-public-api.tinkoff.ru:443"; // sandbox 
        private readonly string _gRPCHost = "invest-public-api.tinkoff.ru:443"; // prod 
        private Metadata _gRpcMetadata;
        private CancellationTokenSource _cancellationTokenSource;
        
        private UsersService.UsersServiceClient _usersClient;
        private OperationsService.OperationsServiceClient _operationsClient;
        private OperationsStreamService.OperationsStreamServiceClient _operationsStreamClient;
        private InstrumentsService.InstrumentsServiceClient _instrumentsClient;
        private MarketDataService.MarketDataServiceClient _marketDataServiceClient;
        private MarketDataStreamService.MarketDataStreamServiceClient _marketDataStreamClient;
        private OrdersService.OrdersServiceClient _ordersClient;
        private OrdersStreamService.OrdersStreamServiceClient _ordersStreamClient;
        
        private void CreateStreamsConnection()
        {
            try
            {
                // заполняем метаданные (заголовок запроса)
                _gRpcMetadata = new Metadata();

                _gRpcMetadata.Add("Authorization", $"Bearer {_accessToken}");
                _gRpcMetadata.Add("x-app-name", "OsEngine");

                // создаем новый токен для отмены (отключения от потоков)
                _cancellationTokenSource = new CancellationTokenSource();

                // подключаемся к потокам gRPC
                Channel channel = new Channel("invest-public-api.tinkoff.ru:443", ChannelCredentials.SecureSsl);
                
                // инициализируем клиенты
                _usersClient = new UsersService.UsersServiceClient(channel);
                _operationsClient = new OperationsService.OperationsServiceClient(channel);
                _operationsStreamClient = new OperationsStreamService.OperationsStreamServiceClient(channel);
                _instrumentsClient = new InstrumentsService.InstrumentsServiceClient(channel);
                _ordersClient = new OrdersService.OrdersServiceClient(channel);
                _ordersStreamClient = new OrdersStreamService.OrdersStreamServiceClient(channel);
                _marketDataServiceClient = new MarketDataService.MarketDataServiceClient(channel);
                _marketDataStreamClient = new MarketDataStreamService.MarketDataStreamServiceClient(channel);

                try
                {
                    SendLogMessage("All streams activated. Connect State", LogMessageType.System);
                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent();
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
        
        private void ReconnectGRPCStreams()
        {
            RepeatedField<string> accountsList = new RepeatedField<string>();
            for (int i = 0; i < _myPortfolios.Count; i++)
            {
                accountsList.Add(_myPortfolios[i].Number);
            }
            
            _myTradesDataStream = _ordersStreamClient.TradesStream(new TradesStreamRequest
            {
                Accounts = { accountsList }
            }, _gRpcMetadata, cancellationToken: _cancellationTokenSource.Token);

            
            _portfolioDataStream =
                _operationsStreamClient.PortfolioStream(new PortfolioStreamRequest { Accounts = { accountsList } },
                    _gRpcMetadata, cancellationToken: _cancellationTokenSource.Token);

            _positionsDataStream =
                _operationsStreamClient.PositionsStream(new PositionsStreamRequest { Accounts = { accountsList } },
                    _gRpcMetadata, cancellationToken: _cancellationTokenSource.Token);

            _lastMyTradesDataTime = DateTime.UtcNow;
            _lastPortfolioDataTime = DateTime.UtcNow;
        }
        
        private void ActivateCurrentPortfolioListening()
        {
            ReconnectGRPCStreams();
        }

        #endregion
        
        #region 7 Security subscribe
        
        // Для всех типов подписок в методе установлены ограничения максимального количества запросов на подписку. Если количество запросов за минуту превысит 100, то для всех элементов будет установлен статус SUBSCRIPTION_STATUS_TOO_MANY_REQUESTS.
        // мы подписываемся на стаканы+сделки, поэтому лимит пополам
        private RateGate _rateGateSubscribe = new RateGate(50, TimeSpan.FromMinutes(1));
        List<Security> _subscribedSecurities = new List<Security>();

        private bool _useStreamForMarketData = true; // if we are over the limits, then stop using stream and turn to data polling (300+ subscribed secs)
        private AsyncDuplexStreamingCall<MarketDataRequest, MarketDataResponse> _marketDataStream;
        private AsyncServerStreamingCall<TradesStreamResponse> _myTradesDataStream;
        private AsyncServerStreamingCall<PortfolioStreamResponse> _portfolioDataStream;
        private AsyncServerStreamingCall<PositionsStreamResponse> _positionsDataStream;

        private DateTime _lastMarketDataTime = DateTime.MinValue;
        private DateTime _lastPortfolioDataTime = DateTime.MinValue;
        private DateTime _lastMyTradesDataTime = DateTime.MinValue;

        public void Subscrible(Security security)
        {
            try
            {
                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if (_subscribedSecurities[i].Name == security.Name)
                    {
                        return;
                    }
                }
                
                if (_subscribedSecurities.Count == 300) // 300 - max marketdata subscriptions (600 = 300 trades + 300 orderbooks )
                {
                    _useStreamForMarketData = false;
                }

                _subscribedSecurities.Add(security);

                // if we don't use stream for market data then nothing more to do
                if (_useStreamForMarketData == false)
                {
                    return;
                }
                
                if (_marketDataStream == null)
                {
                    _marketDataStream = _marketDataStreamClient.MarketDataStream(_gRpcMetadata,
                        cancellationToken: _cancellationTokenSource.Token);
                    SendLogMessage("Created market data stream", LogMessageType.System);
                }

                _rateGateSubscribe.WaitToProceed();

                // подписываемся на сделки
                MarketDataRequest marketDataRequestTrades = new MarketDataRequest();

                TradeInstrument tradeInstrument = new TradeInstrument();
                tradeInstrument.InstrumentId = security.NameId;

                SubscribeTradesRequest subscribeTradesRequest = new SubscribeTradesRequest
                {
                    SubscriptionAction = SubscriptionAction.Subscribe, 
                    Instruments = { tradeInstrument }, 
                    TradeType = _filterOutDealerTrades ? TradeSourceType.TradeSourceExchange : TradeSourceType.TradeSourceAll
                };
                marketDataRequestTrades.SubscribeTradesRequest = subscribeTradesRequest;
                
                _marketDataStream.RequestStream.WriteAsync(marketDataRequestTrades).Wait();

                // подписываемся на стаканы
                MarketDataRequest marketDataRequestOrderBooks = new MarketDataRequest();

                OrderBookInstrument orderBookInstrument = new OrderBookInstrument();
                orderBookInstrument.InstrumentId = security.NameId;
                orderBookInstrument.Depth = 10;
                orderBookInstrument.OrderBookType = OrderBookType.Unspecified;

                SubscribeOrderBookRequest subscribeOrderBookRequest = new SubscribeOrderBookRequest { SubscriptionAction = SubscriptionAction.Subscribe, Instruments = { orderBookInstrument } };
                marketDataRequestOrderBooks.SubscribeOrderBookRequest = subscribeOrderBookRequest;

                _marketDataStream.RequestStream.WriteAsync(marketDataRequestOrderBooks).Wait();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 Reading messages from data streams
        
        private async void DataMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (_marketDataStream == null)
                    {
                        Thread.Sleep(1);
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

                    _lastMarketDataTime  = DateTime.UtcNow;

                    if (marketDataResponse.Ping != null)
                    {
                        Thread.Sleep(1);
                        continue;
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
                        trade.Time = marketDataResponse.Trade.Time.ToDateTime();
                        trade.Id = trade.Time.Ticks.ToString();
                        trade.Side = marketDataResponse.Trade.Direction == TradeDirection.Buy ? Side.Buy : Side.Sell;
                        trade.Volume = marketDataResponse.Trade.Quantity;

                        if (NewTradesEvent != null)
                        {
                            NewTradesEvent(trade);
                        }
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
                        depth.Time = marketDataResponse.Orderbook.Time.ToDateTime();


                        for (int i = 0; i < marketDataResponse.Orderbook.Bids.Count; i++)
                        {
                            MarketDepthLevel newBid = new MarketDepthLevel();
                            newBid.Price = GetValue(marketDataResponse.Orderbook.Bids[i].Price);
                            newBid.Bid = marketDataResponse.Orderbook.Bids[i].Quantity;
                            depth.Bids.Add(newBid);
                        }

                        for (int i = 0; i < marketDataResponse.Orderbook.Asks.Count; i++)
                        {
                            MarketDepthLevel newAsk = new MarketDepthLevel();
                            newAsk.Price = GetValue(marketDataResponse.Orderbook.Asks[i].Price);
                            newAsk.Ask = marketDataResponse.Orderbook.Asks[i].Quantity;
                            depth.Asks.Add(newAsk);
                        }

                        if (_lastMdTime != DateTime.MinValue &&
                            _lastMdTime >= depth.Time)
                        {
                            depth.Time = _lastMdTime.AddMilliseconds(1);
                        }

                        _lastMdTime = depth.Time;

                        if (MarketDepthEvent != null)
                        {
                            MarketDepthEvent(depth);
                        }
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    // Handle the cancellation gracefully
                    SendLogMessage("Market data stream was cancelled", LogMessageType.System);
                    Thread.Sleep(5000);
                }
                catch (RpcException exception)
                {
                    SendLogMessage("Market data stream was disconnected", LogMessageType.Error);

                    // need to reconnect everything
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                    Thread.Sleep(5000);
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
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
                    Thread.Sleep(500);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        continue;
                    }


                    bool usePollingForMarketData = !_useStreamForMarketData;
                    bool isWeekend = DateTime.Now.DayOfWeek == DayOfWeek.Sunday || DateTime.Now.DayOfWeek == DayOfWeek.Saturday;

                    // по выходным маркетдата с внебиржевого рынока не транслируется (сделок нет, а "дилерский" стакан только для некоторых инструментов) и тогда включаем опрос и стакан L1 принудительно
                    if (!_filterOutNonMarketData && isWeekend && _lastMarketDataTime.AddMilliseconds(1000) < DateTime.UtcNow)
                    {
                        usePollingForMarketData = true;
                    }

                    if (usePollingForMarketData)
                    {
                        if(_subscribedSecurities == null ||
                            _subscribedSecurities.Count == 0)
                        {
                            _useStreamForMarketData = true;
                            continue;
                        }

                        if (_filterOutNonMarketData)
                        {
                            if (isTodayATradingDayForSecurity(_subscribedSecurities[0]) == false)
                                continue;
                        }

                        UpdateLastPrices();
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        public void UpdateLastPrices()
        {
            if (_subscribedSecurities.Count == 0)
            {
                return;
            }

            List<string> instrumentIds = new List<string>();

            // Количество инструментов в списке не может быть больше 3000.
            // https://russianinvestments.github.io/investAPI/errors/
            // Поэтому разбиваем обновления на дозы по 3000 штуки
            for (int i = 0; i < _subscribedSecurities.Count; i++)
            {
                instrumentIds.Add(_subscribedSecurities[i].NameId);

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
                SendLogMessage($"Error getting last prices. Info: {message}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }

            if (priceResp == null)
                return;

            for (int i = 0; i < priceResp.LastPrices.Count; i++)
            {
                LastPrice price = priceResp.LastPrices[i];
                Security mySec = GetSecurity(price.InstrumentUid);

                if (price.Price == null)
                    continue;

                if (mySec == null)
                {
                    continue;
                }

                Trade newTrade = new Trade();

                newTrade.SecurityNameCode = mySec.Name;
                newTrade.Time = price.Time.ToDateTime();
                newTrade.Price = GetValue(price.Price);
                newTrade.Volume = 1;
                newTrade.Id = newTrade.Time.Ticks.ToString();

                if (NewTradesEvent != null)
                {
                    NewTradesEvent(newTrade);
                }
                
                CreateFakeMdByTrade(newTrade);
            }
        }
        
        private void CreateFakeMdByTrade(Trade trade)
        {
            List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

            MarketDepthLevel newBid = new MarketDepthLevel();
            newBid.Bid = trade.Volume;
            newBid.Price = trade.Price;
            bids.Add(newBid);
            
            MarketDepth depth = new MarketDepth();

            depth.SecurityNameCode = trade.SecurityNameCode;
            depth.Time = DateTime.UtcNow;
            depth.Bids = bids;

            List<MarketDepthLevel> asks = new List<MarketDepthLevel>();

            MarketDepthLevel newAsk = new MarketDepthLevel();
            newAsk.Ask = trade.Volume;
            newAsk.Price = trade.Price;
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
                            return;
                        }
                        
                        if (portfolioResponse.Portfolio.TotalAmountPortfolio != null)
                        {
                            portf.ValueCurrent = GetValue(portfolioResponse.Portfolio.TotalAmountPortfolio);
                        }
                        else
                        {
                            portf.ValueCurrent = 0;
                            portf.ValueCurrent += GetValue(portfolioResponse.Portfolio.TotalAmountBonds);
                            portf.ValueCurrent += GetValue(portfolioResponse.Portfolio.TotalAmountCurrencies);
                            portf.ValueCurrent += GetValue(portfolioResponse.Portfolio.TotalAmountEtf);
                            portf.ValueCurrent += GetValue(portfolioResponse.Portfolio.TotalAmountFutures);
                            portf.ValueCurrent += GetValue(portfolioResponse.Portfolio.TotalAmountOptions);
                            portf.ValueCurrent += GetValue(portfolioResponse.Portfolio.TotalAmountShares);
                            portf.ValueCurrent += GetValue(portfolioResponse.Portfolio.TotalAmountSp);
                        }
                        
                        if (PortfolioEvent != null)
                        {
                            PortfolioEvent(_myPortfolios);
                        }
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    // Handle the cancellation gracefully
                    SendLogMessage("Portfolio data stream was cancelled", LogMessageType.System);
                    Thread.Sleep(5000);
                }
                catch (RpcException exception)
                {
                    SendLogMessage("Portfolio data stream was disconnected. " + exception.ToString(), LogMessageType.Error);

                    // need to reconnect everything
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                    Thread.Sleep(5000);
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
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
                            return;
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
                                SendLogMessage($"Error getting instrument data. Info: {message}", LogMessageType.Error);
                            }
                            catch (Exception ex)
                            {
                                SendLogMessage("Error getting instrument data for " + pos.Figi + " " + ex.ToString(), LogMessageType.Error);
                            }

                            PositionOnBoard newPos = new PositionOnBoard();

                            newPos.PortfolioName = portf.Number;
                            newPos.ValueCurrent = pos.Balance / instrument.Instrument.Lot;
                            newPos.ValueBlocked = pos.Blocked / instrument.Instrument.Lot;
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
                                SendLogMessage($"Error getting instrument data. Info: {message}", LogMessageType.Error);
                            }
                            catch (Exception ex)
                            {
                                SendLogMessage("Error getting instrument data for " + pos.Figi + " " + ex.ToString(), LogMessageType.Error);
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
                                SendLogMessage($"Error getting instrument data. Info: {message}", LogMessageType.Error);
                            }
                            catch (Exception ex)
                            {
                                SendLogMessage("Error getting instrument data for " + pos.InstrumentUid + " " + ex.ToString(), LogMessageType.Error);
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
                            
                            PositionOnBoard newPos = new PositionOnBoard();

                            newPos.PortfolioName = portf.Number;
                            newPos.ValueCurrent = GetValue(pos.AvailableValue);
                            newPos.ValueBlocked = GetValue(pos.BlockedValue);
                            newPos.SecurityNameCode = pos.AvailableValue.Currency;

                            portf.SetNewPosition(newPos);
                        }

                        if (PortfolioEvent != null)
                        {
                            PortfolioEvent(_myPortfolios);
                        }
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    // Handle the cancellation gracefully
                    SendLogMessage("Positions data stream was cancelled", LogMessageType.System);
                    Thread.Sleep(5000);
                }
                catch (RpcException exception)
                {
                    SendLogMessage("Positions data stream was disconnected. " + exception.ToString(), LogMessageType.Error);

                    // need to reconnect everything
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                    Thread.Sleep(5000);
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private async void MyTradesMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (_myTradesDataStream == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (await _myTradesDataStream.ResponseStream.MoveNext() == false)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (_myTradesDataStream == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    TradesStreamResponse tradesResponse = _myTradesDataStream.ResponseStream.Current;
                    if (tradesResponse == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    _lastMyTradesDataTime = DateTime.UtcNow;

                    if (tradesResponse.Ping != null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }
                    
                    if (tradesResponse.OrderTrades != null)
                    {
                        Security security = GetSecurity(tradesResponse.OrderTrades.InstrumentUid);

                        if (security == null)
                        {
                            Thread.Sleep(1);
                            continue;
                        }

                        // запрашиваем состояние ордера
                        GetOrderStateRequest getOrderStateRequest = new GetOrderStateRequest();
                        getOrderStateRequest.OrderId = tradesResponse.OrderTrades.OrderId;
                        getOrderStateRequest.AccountId = tradesResponse.OrderTrades.AccountId;

                        OrderState state = null;
                        try
                        {
                            _rateGateOrders.WaitToProceed();
                            state = _ordersClient.GetOrderState(getOrderStateRequest, _gRpcMetadata);
                        }
                        catch (RpcException ex)
                        {
                            string message = GetGRPCErrorMessage(ex);
                            SendLogMessage($"Error getting order state. Info: {message}", LogMessageType.Error);

                            Thread.Sleep(1);
                            continue;
                        }
                        catch (Exception ex)
                        {
                            SendLogMessage("Error getting order state " + security.Name + " exception: " + ex.ToString(), LogMessageType.Error);
                            SendLogMessage("Server data was: " + tradesResponse.ToString(), LogMessageType.Error);

                            Thread.Sleep(1);
                            continue;
                        }

                        Order order = new Order();

                        if (!_orderNumbers.ContainsKey(state.OrderRequestId)) // значит сделка была вручную и это не наш ордер
                        {
                            continue;
                        }

                        order.NumberUser = _orderNumbers[state.OrderRequestId];
                        order.NumberMarket = state.OrderId;
                        order.SecurityNameCode = security.Name;
                        order.PortfolioNumber = tradesResponse.OrderTrades.AccountId;
                        order.Side = state.Direction == OrderDirection.Buy ? Side.Buy : Side.Sell;
                        order.TypeOrder = state.OrderType == OrderType.Limit
                            ? OrderPriceType.Limit
                            : OrderPriceType.Market;

                        order.Volume = state.LotsRequested;
                        order.VolumeExecute = state.LotsExecuted;
                        order.Price = order.TypeOrder == OrderPriceType.Limit ? GetValue(state.InitialSecurityPrice) : 0;
                        order.TimeCallBack = state.OrderDate.ToDateTime();
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
                            order.State = OrderStateType.Activ;
                        }
                        else if (state.ExecutionReportStatus ==
                                 OrderExecutionReportStatus.ExecutionReportStatusPartiallyfill)
                        {
                            order.State = OrderStateType.Patrial;
                        }

                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(order);
                        }

                        for (int i = 0; i < tradesResponse.OrderTrades.Trades.Count; i++)
                        {
                            MyTrade trade = new MyTrade();

                            trade.SecurityNameCode = security.Name;
                            trade.Price = GetValue(tradesResponse.OrderTrades.Trades[i].Price);
                            trade.Volume = tradesResponse.OrderTrades.Trades[i].Quantity/security.Lot;
                            trade.NumberOrderParent = tradesResponse.OrderTrades.OrderId;
                            trade.NumberTrade = tradesResponse.OrderTrades.Trades[i].TradeId;
                            trade.Time = tradesResponse.OrderTrades.Trades[i].DateTime.ToDateTime();
                            trade.Side = tradesResponse.OrderTrades.Direction == OrderDirection.Buy
                                ? Side.Buy
                                : Side.Sell;

                            if (MyTradeEvent != null)
                            {
                                MyTradeEvent(trade);
                            }
                        }
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    // Handle the cancellation gracefully
                    SendLogMessage("My trades data stream was cancelled", LogMessageType.System);
                    Thread.Sleep(5000);
                }
                catch (RpcException exception)
                {
                    SendLogMessage("My trades data stream was disconnected", LogMessageType.Error);

                    // need to reconnect everything
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                    Thread.Sleep(5000);
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }
        
        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        #endregion

        #region 9 Trade

        private RateGate _rateGateOrders = new RateGate(100, TimeSpan.FromMinutes(1)); // https://russianinvestments.github.io/investAPI/limits/

        public void SendOrder(Order order)
        {
            _rateGateOrders.WaitToProceed();

            try
            {
                Security security = _subscribedSecurities.Find((sec) =>
                    sec.Name == order.SecurityNameCode);

                PostOrderRequest request = new PostOrderRequest();
                request.Direction = order.Side == Side.Buy ? OrderDirection.Buy : OrderDirection.Sell;
                request.OrderType = order.TypeOrder == OrderPriceType.Limit ? OrderType.Limit : OrderType.Market; // еще есть BestPrice
                request.Quantity = Convert.ToInt32(order.Volume);
                request.Price = ConvertToQuotation(order.Price);
                request.InstrumentId = security.NameId;
                request.AccountId = order.PortfolioNumber;

                // генерируем новый номер ордера и добавляем его в словарь
                Guid newUid = Guid.NewGuid();
                string orderId = newUid.ToString();

                _orderNumbers.Add(orderId, order.NumberUser);

                request.OrderId = orderId;

                PostOrderResponse response = null;

                try
                {
                    response = _ordersClient.PostOrder(request, _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error posting order. Info: {message}", LogMessageType.Error);

                    order.State = OrderStateType.Fail;
                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }

                    return;
                }
                catch (Exception exception)
                {
                    SendLogMessage("Error on order Execution \n" + exception.Message, LogMessageType.Error);
                    
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
                    order.State = OrderStateType.Activ;
                    order.NumberMarket = response.OrderId;
                }

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(order);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order send error " + exception.ToString(), LogMessageType.Error);
            }
        }
        
        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            try
            {
                _rateGateOrders.WaitToProceed();

                if (order.TypeOrder == OrderPriceType.Market)
                {
                    SendLogMessage("Can`t change price to market order", LogMessageType.Error);
                    return;
                }

                int newOrderNumber = NumberGen.GetNumberOrder(StartProgram.IsOsTrader);

                // Первым делом меняем номер ордера у старого
                order.NumberUser = newOrderNumber;

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(order);
                }

                ReplaceOrderRequest request = new ReplaceOrderRequest();
                request.AccountId = order.PortfolioNumber;
                request.OrderId = order.NumberMarket;

                Guid newUid = Guid.NewGuid();
                string orderId = newUid.ToString();
                _orderNumbers.Add(orderId, order.NumberUser);
                
                request.IdempotencyKey = orderId;
                request.Quantity = Convert.ToInt32(order.Volume - order.VolumeExecute);

                if (request.Quantity <= 0 || order.State != OrderStateType.Activ)
                {
                    SendLogMessage("Can`t change order price because it`s not in Active state", LogMessageType.Error);
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
                    SendLogMessage($"Error replacing order. Info: {message}", LogMessageType.Error);

                    order.State = OrderStateType.Fail;
                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }

                    return;
                }
                catch (Exception exception)
                {
                    SendLogMessage("Error on order Execution \n" + exception.Message, LogMessageType.Error);

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
                    order.State = OrderStateType.Activ;
                    order.NumberMarket = response.OrderId;
                    order.NumberUser = newOrderNumber;
                    order.Price = newPrice;
                    order.Volume = request.Quantity;
                    order.VolumeExecute = 0;
                    order.TimeCallBack = response.ResponseMetadata.ServerTime.ToDateTime();
                }
                
                if (MyOrderEvent != null)
                {
                    MyOrderEvent(order);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        List<string> _cancelOrderNums = new List<string>();

        public void CancelOrder(Order order)
        {
            _rateGateOrders.WaitToProceed();
            
            try
            {
                int countTryRevokeOrder = 0;

                for(int i = 0; i< _cancelOrderNums.Count;i++)
                {
                    if (_cancelOrderNums[i].Equals(order.NumberMarket))
                    {
                        countTryRevokeOrder++;
                    }
                }

                if(countTryRevokeOrder >= 2)
                {
                    SendLogMessage("Order cancel request error. The order has already been revoked " + order.SecurityClassCode, LogMessageType.Error);
                    return;
                }

                _cancelOrderNums.Add(order.NumberMarket);

                while(_cancelOrderNums.Count > 100)
                {
                    _cancelOrderNums.RemoveAt(0);
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
                    SendLogMessage($"Error cancelling order. Info: {message}", LogMessageType.Error);
                }
                catch (Exception exception)
                {
                    SendLogMessage("Order cancel request error. "
                        + exception.Message + "  " + order.SecurityClassCode, LogMessageType.Error);
                }

                if (response != null)
                {
                    order.State = OrderStateType.Cancel;

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order cancel request error " + exception.ToString(), LogMessageType.Error);
            }
        }
        
        public void CancelAllOrders()
        {
            List<Order> orders = GetAllOrdersFromExchange();

            for (int i = 0; i < orders.Count;i++)
            {
                Order order = orders[i];

                if(order.State == OrderStateType.Activ)
                {
                    CancelOrder(order);
                }
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            List<Order> orders = GetAllOrdersFromExchange();

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];

                if (order.State == OrderStateType.Activ
                    && order.SecurityNameCode == security.Name)
                {
                    CancelOrder(order);
                }
            }
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllOrdersFromExchange();

            for (int i = 0; orders != null && i < orders.Count; i++)
            {
                if (orders[i] == null)
                {
                    continue;
                }

                if (orders[i].State != OrderStateType.Activ
                    && orders[i].State != OrderStateType.Patrial
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

        public void GetOrderStatus(Order order)
        {
            _rateGateOrders.WaitToProceed();
            
            try
            {
                // запрашиваем состояние ордера
                GetOrderStateRequest getOrderStateRequest = new GetOrderStateRequest();
                getOrderStateRequest.OrderId = order.NumberMarket;
                getOrderStateRequest.AccountId = order.PortfolioNumber;

                OrderState state = null;
                try
                {
                    _rateGateOrders.WaitToProceed();
                    state = _ordersClient.GetOrderState(getOrderStateRequest, _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error getting order state. Info: {message}", LogMessageType.Error);

                    Thread.Sleep(1);
                    return;
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error getting order state " + order.SecurityNameCode + " exception: " + ex.ToString(), LogMessageType.Error);
                    SendLogMessage("Server data was: " + state.ToString(), LogMessageType.Error);

                    Thread.Sleep(1);
                    return;
                }
                Order newOrder = new Order();

                if (!_orderNumbers.ContainsKey(state.OrderRequestId))
                {
                    order.NumberUser = order.NumberUser != 0 ? order.NumberUser : NumberGen.GetNumberOrder(StartProgram.IsOsTrader);
                    _orderNumbers.Add(state.OrderRequestId, order.NumberUser);
                }

                newOrder.NumberUser = _orderNumbers[state.OrderRequestId];
                newOrder.NumberMarket = state.OrderId;
                newOrder.SecurityNameCode = order.SecurityNameCode;
                newOrder.PortfolioNumber = order.PortfolioNumber;
                newOrder.Side = state.Direction == OrderDirection.Buy ? Side.Buy : Side.Sell;
                newOrder.TypeOrder = state.OrderType == OrderType.Limit
                    ? OrderPriceType.Limit
                    : OrderPriceType.Market;

                newOrder.Volume = state.LotsRequested;
                newOrder.VolumeExecute = state.LotsExecuted;
                newOrder.Price = order.TypeOrder == OrderPriceType.Limit ? GetValue(state.InitialSecurityPrice) : 0;
                newOrder.TimeCallBack = state.OrderDate.ToDateTime();
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
                    newOrder.State = OrderStateType.Activ;
                }
                else if (state.ExecutionReportStatus ==
                         OrderExecutionReportStatus.ExecutionReportStatusPartiallyfill)
                {
                    newOrder.State = OrderStateType.Patrial;
                }

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(newOrder);
                }

                if (newOrder.State == OrderStateType.Done || newOrder.State == OrderStateType.Patrial)
                {
                    // add all trades for this order
                    for (int i = 0; i < state.Stages.Count; i++)
                    {
                        OrderStage stage = state.Stages[i];

                        MyTrade trade = new MyTrade();

                        trade.SecurityNameCode = order.SecurityNameCode;
                        trade.Price = GetValue(stage.Price);
                        trade.Volume = stage.Quantity;
                        trade.NumberOrderParent = state.OrderId;
                        trade.NumberTrade = stage.TradeId;
                        trade.Time = stage.ExecutionTime.ToDateTime();
                        trade.Side = state.Direction == OrderDirection.Buy
                            ? Side.Buy
                            : Side.Sell;

                        if (MyTradeEvent != null)
                        {
                            MyTradeEvent(trade);
                        }
                    }
                }
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error getting order state. Info: {message}", LogMessageType.Error);
            }
            catch (Exception exception)
            {
                SendLogMessage("Get order state request error. " + exception.ToString(), LogMessageType.Error);
            }
        }

        private List<Order> GetAllOrdersFromExchange()
        {
            List<Order> orders = new List<Order>();

            for (int i = 0; i < _myPortfolios.Count; i++)
            {
                List<Order> newOrders = GetAllOrdersFromExchangeByPortfolio(_myPortfolios[i].Number);
                if (newOrders != null && newOrders.Count > 0)
                {
                    orders.AddRange(newOrders);
                }
            }
            
            return orders;
        }

        private List<Order> GetAllOrdersFromExchangeByPortfolio(string accountId)
        {
            _rateGateOrders.WaitToProceed();

            try
            {
                GetOrdersRequest getOrdersRequest = new GetOrdersRequest();
                getOrdersRequest.AccountId = accountId;

                GetOrdersResponse response = _ordersClient.GetOrders(getOrdersRequest, _gRpcMetadata);

                if (response != null)
                {
                    List<Order> osEngineOrders = new List<Order>();

                    for (int i = 0; i < response.Orders.Count; i++)
                    {
                        OrderState state = response.Orders[i];
                        Security security = GetSecurity(state.InstrumentUid);

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
                            newOrder.Price = GetValue(state.InitialSecurityPrice);
                        }
                        
                        string orderId = state.OrderRequestId;
                        
                        if (_orderNumbers.ContainsKey(orderId))
                        {
                            newOrder.NumberUser = _orderNumbers[orderId];
                        }
                        else
                        {
                            return null;
                        }
                        
                        newOrder.NumberMarket = state.OrderId;
                        newOrder.TimeCallBack = state.OrderDate.ToDateTime();
                        newOrder.Side = state.Direction == OrderDirection.Buy ? Side.Buy : Side.Sell;

                        if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusUnspecified)
                        {
                            newOrder.State = OrderStateType.None;
                        }
                        else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusFill)
                        {
                            newOrder.State = OrderStateType.Done;
                        }
                        else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusRejected)
                        {
                            newOrder.State = OrderStateType.Fail;
                        }
                        else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusCancelled)
                        {
                            newOrder.State = OrderStateType.Cancel;
                        }
                        else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusNew)
                        {
                            newOrder.State = OrderStateType.Activ;
                        }
                        else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusPartiallyfill)
                        {
                            newOrder.State = OrderStateType.Patrial;
                        }

                        osEngineOrders.Add(newOrder);
                    }

                    return osEngineOrders;
                }
                else
                {
                    SendLogMessage("Get all orders request error. ", LogMessageType.Error);
                }
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error getting all orders. Info: {message}", LogMessageType.Error);
            }
            catch (Exception exception)
            {
                SendLogMessage("Get all orders request error. " + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }
        
        #endregion

        #region 10 Helpers
        
        private string GetGRPCErrorMessage(RpcException exception)
        {
            string message = "no server message";
            string trackingId = "";

            if (exception.Trailers == null)
                return message;

            for(int i = 0; i < exception.Trailers.Count; i++)
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
            for(int i = 0;i < _securities.Count;i++)
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

        #endregion
    }
}