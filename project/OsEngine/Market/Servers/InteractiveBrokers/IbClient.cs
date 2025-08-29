/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.InteractiveBrokers
{
    public class IbClient
    {
        #region Connect / Disconnect

        private TcpClient _tcpClient;

        private BinaryWriter _tcpWriter;

        private BinaryReader _tcpReader;

        private bool _isConnected;

        private string _sendMessageLocker = "sendMessageLocker";

        private int _serverVersion;

        public void Connect(string host, int port)
        {
            if (_isConnected)
            {
                return;
            }
            try
            {
                _tcpClient = new TcpClient(host, port);
                _tcpWriter = new BinaryWriter(_tcpClient.GetStream());
                _tcpReader = new BinaryReader(_tcpClient.GetStream());

                lock(_sendMessageLocker)
                {
                    try
                    {
                        TcpWrite(66);
                        TcpSendMessage();
                        
                    }
                    catch (IOException error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                        throw;

                    }

                    _serverVersion = TcpReadInt();
                    SendLogMessage("Server TCP Active. Version TWS server: " + _serverVersion, LogMessageType.System);

                    if(_serverVersion == 0)
                    {
                        SendLogMessage("Error on TCP server creation ", LogMessageType.Error);
                        return;
                    }

                    string twsTime = TcpReadString();
                    SendLogMessage("TWS time: " + twsTime, LogMessageType.System);

                    _isConnected = true;

                    TcpWrite("71");
                    TcpWrite("1");
                    TcpWrite("0");
                    TcpSendMessage();
                }


                if (_listenThread == null)
                {
                    _listenThread = new Thread(ListenThreadSpace);
                    _listenThread.CurrentCulture = CultureInfo.InvariantCulture;
                    _listenThread.IsBackground = false;
                    _listenThread.Start();
                }

                if (ConnectionSuccess != null)
                {
                    ConnectionSuccess();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void Disconnect()
        {
            if (_tcpWriter == null || _isConnected == false)
            {
                return;
            }

            _isConnected = false;

            if (_tcpClient != null)
            {
                _tcpClient.Close();
                _tcpClient = null;
            }

            if (_tcpWriter != null)
            {
                _tcpWriter.Close();
                _tcpReader.Close();

                _tcpWriter = null;
                _tcpReader = null;
            }

            if (ConnectionFail != null)
            {
                ConnectionFail();
            }

            if (_listenThread != null)
            {
                try
                {
                    _listenThread.Abort();
                }
                catch
                {
                    // ignore
                }
            }


        }

        #endregion

        #region Management methods

        public void GetPortfolios()
        {
            // string tags = "AccountType,NetLiquidation";
            // reqAccountSummary(50000001, "All", tags);
            if (_isConnected == false)
            {
                return;
            }
            try
            {
                lock (_sendMessageLocker)
                {
                    TcpWrite(62);
                    TcpWrite(1);
                    TcpWrite(50000001);
                    TcpWrite("All");
                    TcpWrite("AccountType,NetLiquidation");
                    TcpSendMessage();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

        }

        public void ListenPortfolio(string number)
        {
            // _twsServer.reqPositions();

            if (!_isConnected)
            {
                return;
            }

            try
            {
                lock (_sendMessageLocker)
                {
                    TcpWrite(61);
                    TcpWrite(1);
                    TcpSendMessage();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void GetSecurityDetail(SecurityIb contract)
        {
            if (!_isConnected)
                return;

            try
            {
                lock (_sendMessageLocker)
                {
                    TcpWrite(9);
                    TcpWrite(7);

                    TcpWrite(-1);

                    TcpWrite(contract.ConId);

                    TcpWrite(contract.Symbol);
                    TcpWrite(contract.SecType);
                    TcpWrite(contract.Expiry);
                    TcpWrite(contract.Strike);
                    TcpWrite(contract.Right);

                    TcpWrite(contract.Multiplier);

                    TcpWrite(contract.Exchange);
                    TcpWrite(contract.Currency);
                    TcpWrite(contract.LocalSymbol);

                    TcpWrite(contract.TradingClass);

                    TcpWrite(contract.IncludeExpired);

                    TcpWrite(contract.SecIdType);
                    TcpWrite(contract.SecId);

                    TcpSendMessage();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private List<SecurityIb> _serverSecurities;

        public void GetMarketDataToSecurity(SecurityIb contract)
        {
            if (!_isConnected)
                return;
            try
            {
                lock (_sendMessageLocker)
                {
                    TcpWrite(1);
                    TcpWrite(11);
                    TcpWrite(contract.ConId);
                    TcpWrite(0);
                    TcpWrite(contract.Symbol);
                    TcpWrite(contract.SecType);
                    TcpWrite(contract.Expiry);
                    TcpWrite(contract.Strike);
                    TcpWrite(null); // Right
                    TcpWrite(""); // Multiplier
                    TcpWrite(contract.Exchange);
                    TcpWrite(contract.PrimaryExch); // PrimaryEx
                    TcpWrite(contract.Currency);
                    TcpWrite(contract.LocalSymbol);
                    TcpWrite(null);
                    TcpWrite(false);
                    TcpWrite("");
                    TcpWrite(false);
                    TcpWrite("");
                    TcpSendMessage();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                throw;
            }

        }

        public void GetCandles(SecurityIb contract, DateTime endDateTime, DateTime startTime, string barSizeSetting, string candleType)
        {
            if (!_isConnected)
                return;

            try
            {
                lock (_sendMessageLocker)
                {
                    TcpWrite(20);
                    TcpWrite(6);
                    TcpWrite(contract.ConId.ToString());
                    TcpWrite(contract.ConId.ToString());
                    TcpWrite(contract.Symbol);
                    TcpWrite(contract.SecType);
                    TcpWrite(contract.Expiry);
                    TcpWrite(contract.Strike);
                    TcpWrite(null);
                    TcpWrite(null);
                    TcpWrite(contract.Exchange);
                    TcpWrite(null);
                    TcpWrite(contract.Currency);
                    TcpWrite(contract.LocalSymbol);
                    TcpWrite(contract.TradingClass);
                    TcpWrite(0);
                    string time = endDateTime.ToString("yyyyMMdd HH:mm:ss");// + " GMT";
                    TcpWrite(time);
                    TcpWrite(barSizeSetting);
                    string period = ConvertPeriodToIb(endDateTime, startTime);
                    TcpWrite(period);
                    TcpWrite(0);
                    TcpWrite(candleType);

                    TcpWrite(1);
                    TcpWrite(null);
                    TcpWrite(null);

                    TcpSendMessage();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                throw;
            }

        }

        private string ConvertPeriodToIb(DateTimeOffset startTime, DateTimeOffset endTime)
        {
            var period = endTime.Subtract(startTime);
            var secs = period.TotalSeconds;
            long unit;

            if (secs < 1)
                throw new ArgumentOutOfRangeException("endTime", "Period cannot be less than 1 second.");

            if (secs < 86400)
            {
                unit = (long)Math.Ceiling(secs);
                return unit + " S";
            }

            var days = secs / 86400;

            unit = (long)Math.Ceiling(days);

            if (unit <= 34)
                return unit + " D";

            var weeks = days / 7;
            unit = (long)Math.Ceiling(weeks);

            if (unit > 52)
            {
                return "2 Y";
            }

            return unit + " W";
        }

        public void GetMarketDepthToSecurity(SecurityIb contract)
        {

            // _twsServer.reqMktDepthEx(contractIb.ConId, contractIb, 10, new TagValueList());
            if (!_isConnected)
            {
                return;
            }

            try
            {
                lock (_sendMessageLocker)
                {
                    TcpWrite(10);
                    TcpWrite(5);
                    TcpWrite(contract.ConId.ToString());
                    TcpWrite(contract.ConId.ToString());

                    TcpWrite(contract.Symbol);
                    TcpWrite(contract.SecType);
                    TcpWrite(contract.Expiry);
                    TcpWrite(contract.Strike);
                    TcpWrite(contract.Right);

                    TcpWrite(contract.Multiplier);

                    TcpWrite(contract.Exchange);
                    TcpWrite(contract.Currency);
                    TcpWrite(contract.LocalSymbol);
                    TcpWrite(contract.TradingClass);


                    TcpWrite("10");

                    TcpWrite("");

                    TcpSendMessage();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private int _nextOrderNum;

        public void ExecuteOrder(Order order, SecurityIb contract)
        {
            //_twsServer.placeOrderEx(_nextOrderNum - 1, contractIb, orderIb);

            if (_isConnected == false)
            {
                return;
            }

            if (_orders == null)
            {
                _orders = new List<Order>();
            }

            lock (_sendMessageLocker)
            {
                try
                {

                    if (_orders.Find(o => o.NumberUser == order.NumberUser) == null)
                    {
                        _orders.Add(order);
                    }
                    _nextOrderNum++;
                    order.NumberMarket = order.NumberUser.ToString();

                    TcpWrite(3);
                    TcpWrite(43);
                    TcpWrite(order.NumberUser);
                    TcpWrite(contract.ConId);
                    TcpWrite(contract.Symbol);
                    TcpWrite(contract.SecType);
                    TcpWrite(contract.Expiry);
                    TcpWrite(contract.Strike);
                    TcpWrite(contract.Right);
                    TcpWrite(contract.Multiplier);
                    TcpWrite(contract.Exchange);
                    TcpWrite(contract.PrimaryExch);
                    TcpWrite(contract.Currency);
                    TcpWrite(contract.LocalSymbol);
                    TcpWrite(contract.TradingClass);
                    TcpWrite(contract.SecIdType);
                    TcpWrite(contract.SecId);


                    string action = "";

                    if (order.Side == Side.Buy)
                    {
                        action = "BUY";
                    }
                    else
                    {
                        action = "SELL";
                    }

                    string type = "";

                    if (order.TypeOrder == OrderPriceType.Limit)
                    {
                        type = "LMT";
                    }
                    else
                    {
                        type = "MKT";
                    }

                    // paramsList.AddParameter main order fields
                    TcpWrite(action);
                    TcpWrite(order.Volume.ToString(new NumberFormatInfo() { CurrencyDecimalSeparator = "." }));
                    TcpWrite(type);
                    TcpWrite(order.Price.ToString(new NumberFormatInfo() { CurrencyDecimalSeparator = "." }));
                    TcpWrite("");


                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }


                // paramsList.AddParameter extended order fields
                TcpWrite("GTC"); // life time
                TcpWrite("");
                TcpWrite(order.PortfolioNumber);
                TcpWrite("");
                TcpWrite(0);
                TcpWrite(null);
                TcpWrite(true);
                TcpWrite(0);
                TcpWrite(false);
                TcpWrite(false);
                TcpWrite(0);
                TcpWrite(0);
                TcpWrite(true); // можно ли исполнять ордер в не торговый период
                TcpWrite(false);
                TcpWrite("");


                TcpWrite(0);


                TcpWrite(null); // order.GoodAfterTime
                TcpWrite(null); // order.GoodTillDate
                TcpWrite(null); // order.FaGroup
                TcpWrite(null); // order.FaMethod
                TcpWrite(null); // order.FaPercentage
                TcpWrite(null); // order.FaProfile


                TcpWrite(null); // order.ShortSaleSlot   // 0 only for retail, 1 or 2 only for institution.
                TcpWrite("");   // order.DesignatedLocation// only populate when order.shortSaleSlot = 2.


                TcpWrite(-1); // order.ExemptCode


                TcpWrite(0);     // order.OcaType
                TcpWrite(null);  // order.Rule80A
                TcpWrite(null);  // order.SettlingFirm
                TcpWrite(0);     // order.AllOrNone
                TcpWrite(""); // order.MinQty
                TcpWrite(""); // order.PercentOffset
                TcpWrite(false); // order.ETradeOnly
                TcpWrite(false); // order.FirmQuoteOnly
                TcpWrite(""); //order.NbboPriceCap
                TcpWrite(0); // order.AuctionStrategy
                TcpWrite(""); // order.StartingPrice
                TcpWrite(""); // order.StockRefPrice
                TcpWrite(""); // order.Delta
                TcpWrite("");
                TcpWrite("");

                TcpWrite(false); // order.OverridePercentageConstraints

                // Volatility orders
                TcpWrite(""); // order.Volatility
                TcpWrite(""); // order.VolatilityType
                TcpWrite(""); // order.DeltaNeutralOrderType
                TcpWrite(""); // order.DeltaNeutralAuxPrice
                TcpWrite(0); // order.ContinuousUpdate
                TcpWrite(""); // order.ReferencePriceType
                TcpWrite(""); // order.TrailStopPrice
                TcpWrite(""); // order.TrailingPercent
                TcpWrite(""); // order.ScaleInitLevelSize
                TcpWrite(""); // order.ScaleSubsLevelSize
                TcpWrite(""); // order.ScalePriceIncrement
                TcpWrite(""); // order.ScaleTable
                TcpWrite(""); // order.ActiveStartTime
                TcpWrite(""); // order.ActiveStopTime
                TcpWrite(null);  //order.HedgeType
                TcpWrite(false); // order.OptOutSmartRouting
                TcpWrite(null); // order.ClearingAccount
                TcpWrite(null); // order.ClearingIntent
                TcpWrite(false); // order.NotHeld
                TcpWrite(false);
                TcpWrite(null); // order.AlgoStrategy
                TcpWrite(null); // order.AlgoId
                TcpWrite(false); // order.WhatIf
                TcpWrite(""); // TagValueListToString(order.OrderMiscOptions)


                TcpSendMessage();
            }
        }

        public void CancelOrder(Order order)
        {
            // _twsServer.cancelOrder(Convert.ToInt32(order.NumberMarket));
            try
            {
                lock (_sendMessageLocker)
                {
                    TcpWrite(4);
                    TcpWrite(1);
                    TcpWrite(order.NumberMarket);
                    TcpSendMessage();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private List<Order> _orders;

        private List<MyTradeCreate> _myTradeCreate;

        #endregion

        #region Write data

        private readonly byte _endOfMessage = 0;

        private List<byte> _message;

        private void TcpWrite(int value)
        {
            TcpWrite(value.ToString());
        }

        private void TcpWrite(double value)
        {
            TcpWrite(value.ToString(CultureInfo.InvariantCulture));
        }

        private void TcpWrite(string str)
        {
            if (_message == null)
            {
                _message = new List<byte>();
            }

            if (string.IsNullOrWhiteSpace(str))
            {
                _message.Add(_endOfMessage);
                return;
            }

            _message.AddRange(Encoding.UTF8.GetBytes(str));
            _message.Add(_endOfMessage);
        }

        private void TcpWrite(bool value)
        {
            if (value)
            {
                TcpWrite(1);
            }
            else
            {
                TcpWrite(0);
            }
        }

        RateGate _rateGate = new RateGate(1, TimeSpan.FromMilliseconds(1000));

        private void TcpSendMessage()
        {
            _rateGate.WaitToProceed();

            if(_tcpWriter == null ||
                _message == null ||
                _message.Count == 0)
            {
                return;
            }

            _tcpWriter.Write(_message.ToArray());
            _message = new List<byte>();
        }

        #endregion

        #region Read thread

        private Thread _listenThread;

        private void ListenThreadSpace()
        {
            int zeroMessagesCount = 0;

            int previousMessage;
            int typeMessage = -1;
            while (true)
            {
                try
                {
                    Thread.Sleep(0);
                    previousMessage = typeMessage;

                    typeMessage = TcpReadInt();

                    if (typeMessage == -1)
                    {
                        continue;
                    }

                    if (typeMessage == 1)
                    {
                        LoadTrade();
                    }
                    else if (typeMessage == 3)
                    {
                        LoadOrder();
                    }
                    else if (typeMessage == 4)
                    {
                        LoadError();
                    }
                    else if (typeMessage == 63)
                    {
                        LoadAccount();
                    }
                    else if (typeMessage == 7)
                    {
                        LoadPortfolioPosition();
                    }
                    else if (typeMessage == 9)
                    {
                        LoadValidId();
                    }
                    else if (typeMessage == 10)
                    {
                        LoadContractData();
                    }
                    else if (typeMessage == 12 ||
                             typeMessage == 13)
                    {
                        LoadMarketDepth(typeMessage);
                    }
                    else if (typeMessage == 15)
                    {
                        LoadAccounts();
                    }
                    else if (typeMessage == 65)
                    {
                        TcpReadInt();
                        string apiData = TcpReadString();
                        SendLogMessage(apiData, LogMessageType.System);
                    }
                    else if (typeMessage == 66)
                    {
                        TcpReadInt();
                        TcpReadString();
                        string errorText = TcpReadString();

                        SendLogMessage(errorText, LogMessageType.System);
                    }
                    else if (typeMessage == 61)
                    {
                        LoadPortfolioPosition2();
                    }
                    else if (typeMessage == 17)
                    {
                        HistoricalDataEvent();
                    }

                    // next, an unnecessary Os.Engine date from which the stream still needs to be cleaned
                    // далее не нужная Os.Engine дата, от которой всё же поток нужно чистить

                    else
                    {
                        if (SkipUnnecessaryData(typeMessage) == false)
                        {
                            if (typeMessage == 0)
                            {
                                zeroMessagesCount++;

                                if (zeroMessagesCount % 5 == 0)
                                {
                                    SendLogMessage("Unrecorded message Zero. Probably loss of communication with the server. Previous message: " + previousMessage,
                                    LogMessageType.Error);
                                    ReadToEnd();
                                }

                                if (zeroMessagesCount > 50)
                                {
                                    _listenThread = null;
                                    SendLogMessage("Number of messages is Zero, exceeds 50, reconnect", LogMessageType.Error);
                                    Disconnect();
                                    return;
                                }
                            }
                            else
                            {
                                SendLogMessage("Unrecorded message. Number: " + typeMessage,
                                    LogMessageType.Error);
                            }

                        }
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    return;
                }
            }
        }

        private void HistoricalDataEvent()
        {
            int msgVersion = TcpReadInt();
            int requestId = TcpReadInt();

            string startDateStr = "";
            string endDateStr = "";
            string completedIndicator = "finished";

            if (msgVersion >= 2)
            {
                startDateStr = TcpReadString();
                endDateStr = TcpReadString();
                completedIndicator += "-" + startDateStr + "-" + endDateStr;
            }

            int itemCount = TcpReadInt();

            Candles series = new Candles();
            series.ContractId = requestId;
            var format = "yyyyMMdd HH:mm:ss";

            for (int ctr = 0; ctr < itemCount; ctr++)
            {
                string date = TcpReadString();
                date = date.Replace("  ", " ");
                double open = TcpReadDouble();
                double high = TcpReadDouble();
                double low = TcpReadDouble();
                double close = TcpReadDouble();
                int volume = TcpReadInt();
                double WAP = TcpReadDouble();
                string hasGaps = TcpReadString();
                int barCount = -1;
                if (msgVersion >= 3)
                {
                    barCount = TcpReadInt();
                }

                try
                {
                    Candle candle = new Candle();
                    if (date.Length == 8)
                    {
                        candle.TimeStart = DateTime.ParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        date = date.Replace("  ", " ");
                        candle.TimeStart = DateTime.ParseExact(date, format, CultureInfo.InvariantCulture);
                    }

                    candle.Open = Convert.ToDecimal(open);
                    candle.High = Convert.ToDecimal(high);
                    candle.Low = Convert.ToDecimal(low);
                    candle.Close = Convert.ToDecimal(close);
                    candle.State = CandleState.Finished;

                    if (volume > 0)
                    {
                        candle.Volume = Convert.ToDecimal(volume);
                    }
                    else
                    {
                        candle.Volume = 1;
                    }

                    if (candle.TimeStart == DateTime.MinValue)
                    {
                        continue;
                    }

                    series.CandlesArray.Add(candle);
                }
                catch
                {
                    // ignore
                }

            }

            if (CandlesUpdateEvent != null)
            {
                if (series != null &&
                    series.CandlesArray != null &&
                    series.CandlesArray.Count > 1)
                {
                    series.CandlesArray[series.CandlesArray.Count - 1].State = CandleState.Started;
                }

                CandlesUpdateEvent(series);
            }
        }

        private bool SkipUnnecessaryData(int typeMessage)
        {
            // list of all message numbers that may occur in the system
            /* --- it is necessary for our API messages
             * *** messages that we read and skip
            // cписок всех номеров сообщений, которые могут возникнуть в системе
            /* --- это нужные нашему Апи сообщения
             * *** сообщения которые мы считываем и пропускаем
---     TickPrice = 1,
***		TickVolume = 2,
---		OrderStatus = 3,
---		ErrorMessage = 4,
---		OpenOrder = 5,
***		Portfolio = 6,
---		PortfolioPosition = 7,
***		PortfolioUpdateTime = 8,
---		NextOrderId = 9,
---		SecurityInfo = 10,
---		MyTrade = 11,
---		MarketDepth = 12,
---		MarketDepthL2 = 13,
***		NewsBulletins = 14,
---		ManagedAccounts = 15,
***		FinancialAdvice = 16,
    HistoricalData = 17,
        BondInfo = 18,
***		ScannerParameters = 19,
        ScannerData = 20,
        TickOptionComputation = 21,
***		TickGeneric = 45,
***		TickString = 46,
***		TickEfp = 47,
---		CurrentTime = 49,
        RealTimeBars = 50,
        FundamentalData = 51,
***		SecurityInfoEnd = 52,
        OpenOrderEnd = 53,
        AccountDownloadEnd = 54,
        MyTradeEnd = 55,
        DeltaNuetralValidation = 56,
        TickSnapshotEnd = 57,
        MarketDataType = 58,
---		CommissionReport = 59,
---		Position = 61,
***		PositionEnd = 62,
---		AccountSummary = 63,
***		AccountSummaryEnd = 64,
---		VerifyMessageApi = 65,
---		VerifyCompleted = 66,
        DisplayGroupList = 67,
        DisplayGroupUpdated = 68,
            */

            if (typeMessage == 2)
            {
                // TickSize
                TcpReadInt();
                TcpReadInt();
                TcpReadInt();
                TcpReadInt();
                return true;
            }
            else if (typeMessage == 5)
            {
                //OpenOrder
                SkipOrder();
                return true;
            }
            else if (typeMessage == 6)
            { //Portfolio
                TcpReadString();
                TcpReadString();
                TcpReadString();
                TcpReadString();
                return true;
            }
            else if (typeMessage == 8)
            { //PortfolioUpdateTime  
                TcpReadString();
                return true;
            }
            else if (typeMessage == 11)
            {
                SkipExecutionData();
                return true;
            }
            else if (typeMessage == 14)
            { //NewsBulletins
                TcpReadInt();
                TcpReadInt();
                TcpReadString();
                TcpReadString();
                return true;
            }
            else if (typeMessage == 16)
            { //FinancialAdvice
                TcpReadInt();
                return true;
            }
            else if (typeMessage == 19)
            { //ScannerParameters
                TcpReadString();
                return true;
            }
            else if (typeMessage == 45)
            {
                // TickGeneric
                TcpReadInt();
                TcpReadInt();
                TcpReadInt();
                TcpReadDouble();
                return true;
            }
            else if (typeMessage == 46)
            {
                //TickString
                int val1 = TcpReadInt();
                int val2 = TcpReadInt();
                int val3 = TcpReadInt();
                string str = TcpReadString();
                return true;
            }
            else if (typeMessage == 47)
            {
                // TickEfp
                TcpReadInt();
                TcpReadInt();
                TcpReadDouble();
                TcpReadString();
                TcpReadDouble();
                TcpReadInt();
                TcpReadString();
                TcpReadDouble();
                TcpReadDouble();
                return true;
            }
            else if (typeMessage == 49)
            {
                TcpReadInt();
                return true;
            }
            else if (typeMessage == 52)
            {
                TcpReadInt();
                TcpReadInt();
                return true;
            }
            else if (typeMessage == 58)
            {
                TcpReadInt();
                TcpReadInt();
                TcpReadInt();
                TcpReadInt();
                TcpReadInt();
                TcpReadInt();
                return true;
            }
            else if (typeMessage == 59)
            {
                // ClearCommissionReport
                TcpReadInt();

                TcpReadString();
                TcpReadDouble();
                TcpReadString();
                TcpReadDouble();
                TcpReadDouble();
                TcpReadInt();
                return true;
            }
            else if (typeMessage == 62)
            {
                TcpReadInt();
                return true;
            }
            else if (typeMessage == 64)
            {
                TcpReadInt();
                TcpReadString();
                return true;
            }

            return false;
        }

        /// <summary>
        /// upload portfolios
        /// </summary>
        private void LoadAccounts()
        {
            TcpReadInt();
            TcpReadString();
        }

        /// <summary>
        /// upload depths
        /// </summary>
        private void LoadMarketDepth(int numMessage)
        {
            TcpReadInt();
            int requestId = TcpReadInt();
            int position = TcpReadInt();

            if (numMessage == 13)
            {
                TcpReadString();
            }

            int operation = TcpReadInt();
            int side = TcpReadInt();
            decimal price = TcpReadDecimal();
            int size = TcpReadInt();

            //int id, int position, int operation, int side, double price, int size

            if (NewMarketDepth != null)
            {
                NewMarketDepth(requestId, position, operation, side, price, size);
            }
        }

        /// <summary>
        /// upload the next valid order ID
        /// </summary>
        private void LoadValidId()
        {
            TcpReadInt();
            int orderId = TcpReadInt();

            _nextOrderNum = orderId;
        }

        /// <summary>
        /// upload portfolio position
        /// </summary>
        private void LoadPortfolioPosition()
        {
            int msgVersion = TcpReadInt();
            SecurityIb contract = new SecurityIb();
            if (msgVersion >= 6)
                contract.ConId = TcpReadInt();
            contract.Symbol = TcpReadString();
            contract.SecType = TcpReadString();
            contract.Expiry = TcpReadString();
            contract.Strike = TcpReadDouble();
            contract.Right = TcpReadString();
            if (msgVersion >= 7)
            {
                contract.Multiplier = TcpReadString();
                contract.PrimaryExch = TcpReadString();
            }
            contract.Currency = TcpReadString();

            if (msgVersion >= 2)
            {
                contract.LocalSymbol = TcpReadString();
            }
            if (msgVersion >= 8)
            {
                contract.TradingClass = TcpReadString();
            }

            int position = TcpReadInt();
            TcpReadDouble();
            TcpReadDouble();

            if (msgVersion >= 3)
            {
                TcpReadDouble();
                TcpReadDouble();
                TcpReadDouble();
            }

            string accountName = null;
            if (msgVersion >= 4)
            {
                accountName = TcpReadString();
            }

            if (NewPortfolioPosition != null)
            {
                NewPortfolioPosition(contract, accountName, position);
            }
        }

        /// <summary>
        /// upload portfolio position
        /// </summary>
        private void LoadPortfolioPosition2()
        {
            int msgVersion = TcpReadInt();
            string account = TcpReadString();
            SecurityIb contract = new SecurityIb();
            contract.ConId = TcpReadInt();
            contract.Symbol = TcpReadString();
            contract.SecType = TcpReadString();
            contract.Expiry = TcpReadString();
            contract.Strike = TcpReadDouble();
            contract.Right = TcpReadString();
            contract.Multiplier = TcpReadString();
            contract.Exchange = TcpReadString();
            contract.Currency = TcpReadString();
            contract.LocalSymbol = TcpReadString();

            if (msgVersion >= 2)
            {
                contract.TradingClass = TcpReadString();
            }

            int pos = TcpReadInt();
            if (msgVersion >= 3)
                TcpReadDouble();

            if (NewPortfolioPosition != null)
            {
                NewPortfolioPosition(contract, account, pos);
            }
        }

        /// <summary>
        /// upload portfolios
        /// </summary>
        private void LoadAccount()
        {
            TcpReadInt();
            TcpReadInt();
            string portfolio = TcpReadString();
            string val1 = TcpReadString();
            string val2 = TcpReadString();
            TcpReadString();

            if (val1 == "NetLiquidation" && NewAccountValue != null)
            {
                NewAccountValue(portfolio, Decimal.Parse(val2, NumberFormatInfo.InvariantInfo));
            }
        }

        /// <summary>
        /// upload trades
        /// </summary>
        private void LoadTrade()
        {
            int msgVersion = TcpReadInt();
           
            int requestId = TcpReadInt();
            int tickType = TcpReadInt();
            decimal price = TcpReadDecimal();

            if(msgVersion == 1)
            {
                TcpReadInt();
                TcpReadInt();
                TcpReadInt();
                return;
            }

            if (msgVersion < 2)
            {
                return;
            }

            var size = TcpReadInt();

            if (msgVersion >= 3)
            {
                int eligible = TcpReadInt();

                if (eligible == 0)
                {
                    return;
                }
            }

            if (
                tickType != 2 && tickType != 1 &&
                tickType != 4)
            {
                return;
            }

            SecurityIb security = _serverSecurities.Find(sec => sec.ConId == requestId);

            if (security == null)
            {
                return;
            }

            Trade trade = new Trade();
            trade.Price = price;
            trade.Volume = size;
            trade.Time = DateTime.Now;
            trade.SecurityNameCode = security.Symbol + "_" + security.SecType + "_" + security.Exchange + "_" + security.LocalSymbol;
            

            if (tickType == 1)
            {
                trade.Side = Side.Buy;
            }
            else if (tickType == 2)
            {
                trade.Side = Side.Sell;
            }

            if (NewTradeEvent != null)
            {
                NewTradeEvent(trade, security);
            }
        }

        /// <summary>
        /// upload order
        /// </summary>
        private void LoadOrder()
        {
            // parse message / разбираем сообщение
            int msgVersion = TcpReadInt();
            int id = TcpReadInt();
            string status = TcpReadString();
            int filled = TcpReadInt();
            TcpReadInt();
            TcpReadDouble();

            if (msgVersion >= 2)
            {
                TcpReadInt();
            }

            if (msgVersion >= 3)
            {
                TcpReadInt();
            }

            double lasPrice = 0;
            if (msgVersion >= 4)
            {
                lasPrice = TcpReadDouble();
            }

            if (msgVersion >= 5)
            {
                TcpReadInt();
            }

            if (msgVersion >= 6)
            {
                TcpReadString();
            }

            // parent.Wrapper.orderStatus(id, status, filled, remaining, avgFillPrice, permId, parentId, lastFillPrice, clientId, whyHeld);

            // complect order / комплектуем ордер

            if (_orders == null)
            {
                _orders = new List<Order>();
            }

            Order osOrder = _orders.Find(order1 => Convert.ToInt32(order1.NumberMarket) == id);

            if (osOrder == null)
            {
                return;
            }

            Order newOsOrder = new Order();

            newOsOrder.IsStopOrProfit = osOrder.IsStopOrProfit;
            newOsOrder.LifeTime = osOrder.LifeTime;
            newOsOrder.NumberMarket = osOrder.NumberMarket;
            newOsOrder.NumberUser = osOrder.NumberUser;
            newOsOrder.PortfolioNumber = osOrder.PortfolioNumber;
            newOsOrder.Price = osOrder.Price;
            newOsOrder.SecurityNameCode = osOrder.SecurityNameCode;
            newOsOrder.ServerType = osOrder.ServerType;
            newOsOrder.Side = osOrder.Side;
            newOsOrder.TimeCallBack = osOrder.TimeCallBack;
            newOsOrder.TimeCancel = osOrder.TimeCancel;
            newOsOrder.TimeCreate = osOrder.TimeCreate;
            newOsOrder.TypeOrder = osOrder.TypeOrder;
            newOsOrder.Volume = osOrder.Volume;
            newOsOrder.Comment = osOrder.Comment;

            if (newOsOrder.TimeCallBack == DateTime.MinValue)
            {
                newOsOrder.TimeCallBack = DateTime.Now;
                osOrder.TimeCallBack = DateTime.Now;
            }

            if (status == "Inactive")
            {
                newOsOrder.State = OrderStateType.Fail;
            }
            else if (status == "PreSubmitted" ||
                     status == "Submitted")
            {
                newOsOrder.State = OrderStateType.Active;
                newOsOrder.TimeCallBack = DateTime.Now;
                osOrder.TimeCallBack = DateTime.Now;
            }
            else if (status == "Cancelled")
            {
                newOsOrder.State = OrderStateType.Fail;
            }
            else if (status == "Filled")
            {
                newOsOrder.State = OrderStateType.Done;
                newOsOrder.VolumeExecute = filled;
            }

            if (_myTradeCreate == null)
            {
                _myTradeCreate = new List<MyTradeCreate>();
            }

            if (status == "Filled" &&
                osOrder.VolumeExecute != newOsOrder.VolumeExecute)
            {
                // need to generate my trades / надо сгенерить мои трейды

                decimal volume = newOsOrder.VolumeExecute - osOrder.VolumeExecute;

                List<MyTradeCreate> myTradeCreates = _myTradeCreate.FindAll(create => create.idOrder == id);

                if (myTradeCreates.Count != 0)
                {
                    volume = newOsOrder.VolumeExecute -
                             myTradeCreates[myTradeCreates.Count - 1].FillOrderToCreateMyTrade;
                }

                if (volume == 0)
                {
                    return;
                }

                MyTradeCreate newTradeCreate = new MyTradeCreate
                {
                    idOrder = id,
                    FillOrderToCreateMyTrade = newOsOrder.VolumeExecute
                };
                _myTradeCreate.Add(newTradeCreate);

                MyTrade trade = new MyTrade();
                trade.NumberOrderParent = osOrder.NumberMarket;
                trade.Price = Convert.ToDecimal(lasPrice);
                trade.Time = DateTime.Now;
                trade.NumberTrade = trade.Time.ToBinary().ToString();
                trade.SecurityNameCode = osOrder.SecurityNameCode;
                trade.Volume = volume;
                trade.Side = osOrder.Side;

                if (NewMyTradeEvent != null)
                {
                    NewMyTradeEvent(trade);
                }
            }

            if (NewOrderEvent != null)
            {
                NewOrderEvent(newOsOrder);
            }
        }

        /// <summary>
        /// upload error
        /// </summary>
        private void LoadError()
        {
            if (TcpReadInt() < 2)
            {
                SendLogMessage(TcpReadString(), LogMessageType.System);
            }
            else
            {
                SendLogMessage(TcpReadInt() + TcpReadInt() + TcpReadString(), LogMessageType.System);
            }
        }

        /// <summary>
        /// upload contract specification 
        /// </summary>
        private void LoadContractData()
        {
            int msgVersion = TcpReadInt();
            int requestId = -1;
            if (msgVersion >= 3)
                requestId = TcpReadInt();
            SecurityIb contract = new SecurityIb();
            contract.Symbol = TcpReadString();
            contract.SecType = TcpReadString();
            contract.Expiry = TcpReadString();
            contract.Strike = TcpReadDouble();
            contract.Right = TcpReadString();
            contract.Exchange = TcpReadString();
            contract.Currency = TcpReadString();
            contract.LocalSymbol = TcpReadString();
            TcpReadString();
            contract.TradingClass = TcpReadString();
            contract.ConId = TcpReadInt();
            contract.MinTick = TcpReadDouble();
            contract.Multiplier = TcpReadString();
            TcpReadString();
            TcpReadString();

            if (msgVersion >= 2)
            {
                TcpReadInt();
            }
            if (msgVersion >= 4)
            {
                TcpReadInt();
            }
            if (msgVersion >= 5)
            {
                TcpReadString();
                TcpReadString();
            }
            if (msgVersion >= 6)
            {
                TcpReadString();
                TcpReadString();
                TcpReadString();
                TcpReadString();
                TcpReadString();
                TcpReadString();
                TcpReadString();
            }
            if (msgVersion >= 8)
            {
                TcpReadString();
                TcpReadDouble();
            }
            if (msgVersion >= 7)
            {
                int secIdListCount = TcpReadInt();
                if (secIdListCount > 0)
                {
                    for (int i = 0; i < secIdListCount; ++i)
                    {
                        TcpReadString();
                        TcpReadString();
                    }
                }
            }
            if (_serverSecurities == null)
            {
                _serverSecurities = new List<SecurityIb>();
            }

            _serverSecurities.Add(contract);

            if (NewContractEvent != null)
            {
                NewContractEvent(contract);
            }
        }

        /// <summary>
        /// clear message about the new opened order
        /// </summary>
        private void SkipOrder()
        {
            int msgVersion = TcpReadInt();
            // read order id
            TcpReadInt();

            // read contract fields

            if (msgVersion >= 17)
            {
                TcpReadInt();
            }
            TcpReadString();
            TcpReadString();
            TcpReadString();
            TcpReadDouble();
            TcpReadString();

            if (msgVersion >= 32)
            {
                TcpReadString();
            }

            TcpReadString();
            TcpReadString();

            if (msgVersion >= 2)
            {
                TcpReadString();
            }
            if (msgVersion >= 32)
            {
                TcpReadString();
            }

            // read order fields
            string action = TcpReadString();
            TcpReadInt();
            TcpReadString();

            if (msgVersion < 29)
            {
                TcpReadDouble();
            }
            else
            {
                TcpReadDouble();
            }
            if (msgVersion < 30)
            {
                TcpReadDouble();
            }
            else
            {
                TcpReadDouble();
            }
            TcpReadString();
            TcpReadString();
            TcpReadString();
            TcpReadString();
            TcpReadInt();
            TcpReadString();

            if (msgVersion >= 3)
            {
                TcpReadInt();
            }

            if (msgVersion >= 4)
            {
                TcpReadInt();
                if (msgVersion < 18)
                {
                    // will never happen
                    /* order.ignoreRth = */
                    TcpReadInt();
                }
                else
                {
                    TcpReadInt();
                }
                TcpReadInt();
                TcpReadDouble();
            }

            if (msgVersion >= 5)
            {
                TcpReadString();
            }

            if (msgVersion >= 6)
            {
                // skip deprecated sharesAllocation field
                TcpReadString();
            }

            if (msgVersion >= 7)
            {
                TcpReadString();
                TcpReadString();
                TcpReadString();
                TcpReadString();
            }

            if (msgVersion >= 8)
            {
                TcpReadString();
            }

            if (msgVersion >= 9)
            {
                TcpReadString();
                TcpReadDouble();
                TcpReadString();
                TcpReadInt();
                TcpReadString();

                if (_serverVersion == 51)
                {
                    TcpReadInt(); // exemptCode
                }
                else if (msgVersion >= 23)
                {
                    TcpReadInt();
                }
                TcpReadInt();
                TcpReadDouble();
                TcpReadDouble();
                TcpReadDouble();
                TcpReadDouble();
                TcpReadDouble();
                TcpReadInt();

                if (msgVersion < 18)
                {
                    // will never happen
                    /* order.rthOnly = */
                    TcpReadInt();
                }
                TcpReadInt();
                TcpReadInt();
                TcpReadInt();
                TcpReadInt();
                TcpReadInt();
                TcpReadInt();
                TcpReadInt();
                TcpReadDouble();
            }

            if (msgVersion >= 10)
            {
                TcpReadInt();
                TcpReadInt();
            }

            if (msgVersion >= 11)
            {
                TcpReadDouble();
                TcpReadInt();

                if (msgVersion == 11)
                {
                    TcpReadInt();
                }
                else
                { // msgVersion 12 and up
                    string deltaNeutralOrderType = TcpReadString();
                    TcpReadDouble();

                    if (msgVersion >= 27 && 
                        string.IsNullOrEmpty(deltaNeutralOrderType) == false)
                    {
                        TcpReadInt();
                        TcpReadString();
                        TcpReadString();
                        TcpReadString();
                    }

                    if (msgVersion >= 31 &&
                        string.IsNullOrEmpty(deltaNeutralOrderType) == false)
                    {
                        TcpReadString();
                        TcpReadInt();
                        TcpReadInt();
                        TcpReadString();
                    }
                }

                TcpReadInt();

                if (_serverVersion == 26)
                {
                   TcpReadDouble();
                   TcpReadDouble();
                }
                TcpReadInt();
            }

            if (msgVersion >= 13)
            {
               TcpReadDouble();
            }

            if (msgVersion >= 30)
            {
               TcpReadDouble();
            }

            if (msgVersion >= 14)
            {
                TcpReadDouble();
                TcpReadInt();
                TcpReadString();
            }

            if (msgVersion >= 29)
            {
                int comboLegsCount = TcpReadInt();
                if (comboLegsCount > 0)
                {
                    for (int i = 0; i < comboLegsCount; ++i)
                    {
                        TcpReadInt();
                        TcpReadInt();
                        TcpReadString();
                        TcpReadString();
                        TcpReadInt();
                        TcpReadInt();
                        TcpReadString();
                        TcpReadInt();
                    }
                }

                int orderComboLegsCount = TcpReadInt();

                if (orderComboLegsCount > 0)
                {
                    for (int i = 0; i < orderComboLegsCount; ++i)
                    {
                        TcpReadDouble();
                    }
                }
            }

            if (msgVersion >= 26)
            {
                int smartComboRoutingParamsCount = TcpReadInt();

                if (smartComboRoutingParamsCount > 0)
                {
                    for (int i = 0; i < smartComboRoutingParamsCount; ++i)
                    {
                        TcpReadString();
                        TcpReadString();
                    }
                }
            }

            double scalePriceIncrement = Double.MaxValue;

            if (msgVersion >= 15)
            {
                if (msgVersion >= 20)
                {
                    TcpReadInt();
                    TcpReadInt();
                }
                else
                {
                    TcpReadInt();
                    TcpReadInt();
                }
                scalePriceIncrement = TcpReadDouble();
            }

            if (msgVersion >= 28 
                && scalePriceIncrement > 0.0 
                && scalePriceIncrement != Double.MaxValue)
            {
                TcpReadDouble();
                TcpReadInt();
                TcpReadDouble();
                TcpReadInt();
                TcpReadInt();
                TcpReadInt();
                TcpReadInt();
            }

            if (msgVersion >= 24)
            {
                string hedgeType = TcpReadString();

                if (string.IsNullOrEmpty(hedgeType) == false)
                {
                    TcpReadString();
                }
            }

            if (msgVersion >= 25)
            {
                TcpReadInt();
            }

            if (msgVersion >= 19)
            {
                TcpReadString();
                TcpReadString();
            }

            if (msgVersion >= 22)
            {
                TcpReadInt();
            }

            if (msgVersion >= 20)
            {
                if (TcpReadInt() == 1)
                {
                    TcpReadInt();
                    TcpReadDouble();
                    TcpReadDouble();
                }
            }

            if (msgVersion >= 21)
            {
                string algoStrategy = TcpReadString();
                if (string.IsNullOrEmpty(algoStrategy) == false)
                {
                    int algoParamsCount = TcpReadInt();

                    if (algoParamsCount > 0)
                    {
                        for (int i = 0; i < algoParamsCount; ++i)
                        {
                            TcpReadString();
                            TcpReadString();
                        }
                    }
                }
            }

            if (msgVersion >= 16)
            {
                TcpReadInt();
                TcpReadString();
                TcpReadString();
                TcpReadString();
                TcpReadString();
                TcpReadDouble();
                TcpReadDouble();
                TcpReadDouble();
                TcpReadString();
                TcpReadString();
            }

            if(msgVersion >= 32 &&
                _serverVersion >= 76)
            {
                TcpReadString();
            }
        }

        /// <summary>
        /// clear the message about execution
        /// </summary>
        private void SkipExecutionData()
        {
            int msgVersion = TcpReadInt();

            if (msgVersion >= 7)
                TcpReadInt();

            TcpReadInt();
            new SecurityIb();
            if (msgVersion >= 5)
            {
                TcpReadInt();
            }
            TcpReadString();
            TcpReadString();
            TcpReadString();
            TcpReadDouble();
            TcpReadString();

            if (msgVersion >= 9)
            {
                TcpReadString();
            }
            TcpReadString();
            TcpReadString();
            TcpReadString();

            if (msgVersion >= 10)
            {
                TcpReadString();
            }

            TcpReadString();
            TcpReadString();
            TcpReadString();
            TcpReadString();
            TcpReadString();
            TcpReadInt();
            TcpReadDouble();

            if (msgVersion >= 2)
            {
                TcpReadInt();
            }
            if (msgVersion >= 3)
            {
                TcpReadInt();
            }
            if (msgVersion >= 4)
            {
                TcpReadInt();
            }
            if (msgVersion >= 6)
            {
                TcpReadInt();
                TcpReadDouble();
            }
            if (msgVersion >= 8)
            {
                TcpReadString();
            }
            if (msgVersion >= 9)
            {
                TcpReadString();
                TcpReadDouble();
            }
        }

        #endregion

        #region Final read methods

        private double TcpReadDouble()
        {
            try
            {
                string str = TcpReadString();
                if (string.IsNullOrEmpty(str) ||
                    str == "0")
                {
                    return 0;
                }
                else return Double.Parse(str, NumberFormatInfo.InvariantInfo);
            }
            catch
            {
                //SendLogMessage(error.ToString(),LogMessageType.Error);
                return 0;
            }

        }

        private decimal TcpReadDecimal()
        {
            try
            {
                string str = TcpReadString();
                if (string.IsNullOrEmpty(str) ||
                    str == "0")
                {
                    return 0;
                }
                else return Decimal.Parse(str, NumberFormatInfo.InvariantInfo);
            }
            catch
            {
                //SendLogMessage(error.ToString(), LogMessageType.Error);
                return 0;
            }

        }

        public int TcpReadInt()
        {
            try
            {
                string str = TcpReadString();
                if (string.IsNullOrEmpty(str))
                {
                    return 0;
                }
                else return Int32.Parse(str);
            }
            catch
            {
                //SendLogMessage(error.ToString(), LogMessageType.Error);
                return 0;
            }
        }

        private string TcpReadString()
        {
            try
            {
                byte b = _tcpReader.ReadByte();

                if (b == 0)
                {
                    return null;
                }
                else
                {
                    StringBuilder str = new StringBuilder();
                    str.Append((char)b);
                    while (true)
                    {
                        b = _tcpReader.ReadByte();
                        if (b == 0)
                        {
                            break;
                        }
                        else
                        {
                            str.Append((char)b);
                        }
                    }
                    return str.ToString();
                }
            }
            catch
            {
                //SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private void ReadToEnd()
        {
            try
            {
                while (_tcpClient.GetStream().DataAvailable)
                {
                    TcpReadString();
                }
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region Events

        public event Action<int, int, int, int, decimal, int> NewMarketDepth;

        public event Action<SecurityIb, string, int> NewPortfolioPosition;

        public event Action<Trade, SecurityIb> NewTradeEvent;

        public event Action<Order> NewOrderEvent;

        public event Action<MyTrade> NewMyTradeEvent;

        public event Action<SecurityIb> NewContractEvent;

        public event Action<string, decimal> NewAccountValue;

        public event Action ConnectionSuccess;

        public event Action ConnectionFail;

        public event Action<Candles> CandlesUpdateEvent;

        #endregion

        #region Log

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    public class Candles
    {
        public int ContractId;

        public List<Candle> CandlesArray = new List<Candle>();
    }

    public class MyTradeCreate
    {
        public int idOrder;

        public decimal FillOrderToCreateMyTrade;
    }

    public class SecurityIb
    {
        public bool CreateMarketDepthFromTrades = true;

        public int ConId;

        public string Symbol;

        public string LocalSymbol;

        public string Currency;

        public string Exchange;

        public string PrimaryExch;

        public double Strike;

        public string TradingClass;

        public double MinTick;

        public string Multiplier;

        public string Expiry;

        public bool IncludeExpired;

        public string ComboLegsDescription;

        public string Right;

        public string SecId;

        public string SecIdType;

        public string SecType;

    }
}