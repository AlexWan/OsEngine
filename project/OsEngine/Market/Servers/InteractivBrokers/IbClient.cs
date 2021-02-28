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

namespace OsEngine.Market.Servers.InteractivBrokers
{
    /// <summary>
    /// class implements the client for TCP server TWS
    /// класс реализующий клиента для TCP сервера TWS
    /// </summary>
    public class IbClient
    {

        /// <summary>
        /// client
        /// клиент
        /// </summary>
        private TcpClient _tcpClient;

        /// <summary>
        /// class to read data from stream
        /// класс для чтения данных из потока
        /// </summary>
        private BinaryWriter _tcpWriter;

        /// <summary>
        /// class to write data to stream
        /// класс для записи данных в поток
        /// </summary>
        private BinaryReader _tcpReader;

        // connect / коннект

        private bool _isConnected;

        /// <summary>
        /// establish a connection to TCP server of TWS
        /// установить соединение с TСP сервером TWS
        /// </summary>
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

                try
                {
                    TcpWrite(63);
                    TcpSendMessage();
                }
                catch (IOException error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    throw;

                }

                int serverVersion = TcpReadInt();
                SendLogMessage("Server TCP Activ. Version TWS server: " + serverVersion, LogMessageType.System);


                string twsTime = TcpReadString();
                SendLogMessage("TWS time: " + twsTime, LogMessageType.System);

                _isConnected = true;

                TcpWrite("71");
                TcpWrite("1");
                TcpWrite("0");
                TcpSendMessage();

                if (_listenThread == null)
                {
                    _listenThread = new Thread(ListenThreadSpace);
                    _listenThread.CurrentCulture = CultureInfo.InvariantCulture;
                    _listenThread.IsBackground = false;
                    _listenThread.Start();
                }

                if (ConnectionSucsess != null)
                {
                    ConnectionSucsess();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// disconnect from TCP server of TWS
        /// отключиться от TCP сервера TWS
        /// </summary>
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

            _namesSecuritiesWhoOptOnTrades = new List<string>();

            _namesSecuritiesWhoOptOnMarketDepth = new List<string>();

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
            if (ConnectionFail != null)
            {
                ConnectionFail();
            }

        }

        // portfolios
        // Портфели

        /// <summary>
        /// take portfolios
        /// взять портфели
        /// </summary>
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
                TcpWrite(62);
                TcpWrite(1);
                TcpWrite(50000001);
                TcpWrite("All");
                TcpWrite("AccountType,NetLiquidation");
                TcpSendMessage();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

        }

        /// <summary>
        /// subcribe to change portfolios
        /// подписаться на изменения портфеля
        /// </summary>
        /// <param name="number"></param>
        public void ListenPortfolio(string number)
        {
            // _twsServer.reqPositions();

            if (!_isConnected)
            {
                return;
            }

            try
            {
                TcpWrite(61);
                TcpWrite(1);
                TcpSendMessage();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// take details of security
        /// взять детали по бумаге
        /// </summary>
        public void GetSecurityDetail(SecurityIb contract)
        {
            if (!_isConnected)
                return;

            try
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
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// all security with specification
        /// все бумаги по которым нам пришла спицификация
        /// </summary>
        private List<SecurityIb> _serverSecurities;

        /// <summary>
        /// list of securities on which we have already subscribed to receive trades
        /// перечень бумаг по которым мы уже подписались на получение трейдов
        /// </summary>
        private List<string> _namesSecuritiesWhoOptOnTrades;

        /// <summary>
        /// list of securities on which we have already subscribed to receive depths
        /// перечень бумаг по которым мы уже подписались на получение стаканов
        /// </summary>
        private List<string> _namesSecuritiesWhoOptOnMarketDepth;

        /// <summary>
        /// subscribe to ticks
        /// подписываемся на тики
        /// </summary>
        public void GetMarketDataToSecurity(SecurityIb contract)
        {
            if (_namesSecuritiesWhoOptOnTrades == null)
            {
                _namesSecuritiesWhoOptOnTrades = new List<string>();
            }

            if (_namesSecuritiesWhoOptOnTrades.Find(
                    s => s == contract.Symbol + "_" + contract.SecType + "_" + contract.Exchange) != null)
            {
                return;
            }

            _namesSecuritiesWhoOptOnTrades.Add(contract.Symbol + "_" + contract.SecType + "_" + contract.Exchange);

            if (!_isConnected)
                return;
            try
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
                TcpWrite(""); // PrimaryEx
                TcpWrite(contract.Currency);
                TcpWrite(contract.LocalSymbol);
                TcpWrite(null);
                TcpWrite(false);
                TcpWrite("");
                TcpWrite(false);
                TcpWrite("");
                TcpSendMessage();
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
                string time = endDateTime.ToString("yyyyMMdd HH:mm:ss") + " GMT";
                TcpWrite(time);
                TcpWrite(barSizeSetting);
                string period = ConvertPeriodtoIb(endDateTime, startTime);
                TcpWrite(period);
                TcpWrite(0);
                TcpWrite(candleType);

                TcpWrite(1);
                TcpWrite(null);
                TcpWrite(null);

                TcpSendMessage();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                throw;
            }

        }

        private string ConvertPeriodtoIb(DateTimeOffset startTime, DateTimeOffset endTime)
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

        /// <summary>
        /// subscribe to depths
        /// подписываемся на стаканы
        /// </summary>
        public void GetMarketDepthToSecurity(SecurityIb contract)
        {
            if (_namesSecuritiesWhoOptOnMarketDepth == null)
            {
                _namesSecuritiesWhoOptOnMarketDepth = new List<string>();
            }

            if (_namesSecuritiesWhoOptOnMarketDepth.Find(
                    s => s == contract.Symbol + "_" + contract.SecType + "_" + contract.Exchange) != null)
            {
                return;
            }

            _namesSecuritiesWhoOptOnMarketDepth.Add(contract.Symbol + "_" + contract.SecType + "_" + contract.Exchange);

            // _twsServer.reqMktDepthEx(contractIb.ConId, contractIb, 10, new TagValueList());
            if (!_isConnected)
            {
                return;
            }

            try
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
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// the next right order number
        /// следующий правильный номер ордера
        /// </summary>
        private int _nextOrderNum;

        /// <summary>
        /// execute order on the exchange
        /// исполнить ордер на бирже
        /// </summary>
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

            try
            {
                if (_orders.Find(o => o.NumberUser == order.NumberUser) == null)
                {
                    _orders.Add(order);
                }
                _nextOrderNum++;
                order.NumberMarket = _nextOrderNum.ToString();

                TcpWrite(3);
                TcpWrite(43);
                TcpWrite(_nextOrderNum);
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
            TcpWrite(null); // null
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
            TcpWrite(false);
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

        /// <summary>
        /// cancel order
        /// отозвать ордер
        /// </summary>
        public void CancelOrder(Order order)
        {
            // _twsServer.cancelOrder(Convert.ToInt32(order.NumberMarket));
            try
            {
                TcpWrite(4);
                TcpWrite(1);
                TcpWrite(order.NumberMarket);
                TcpSendMessage();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        // load data into thread
        // грузим данные в поток

        /// <summary>
        /// sign of a complete message for the package
        /// признак завершения сообщения для пакета
        /// </summary>
        private readonly byte _endOfMessage = 0;

        /// <summary>
        /// current message
        /// текущее сообщение
        /// </summary>
        private List<byte> _message;

        /// <summary>
        /// write new data to message
        /// записать в сообщение новые данные
        /// </summary>
        private void TcpWrite(int value)
        {
            TcpWrite(value.ToString());
        }

        /// <summary>
        /// write new data to message
        /// записать в сообщение новые данные
        /// </summary>
        private void TcpWrite(double value)
        {
            TcpWrite(value.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// write new data to message
        /// записать в сообщение новые данные
        /// </summary>
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

        /// <summary>
        /// write new data to message
        /// записать в сообщение новые данные
        /// </summary>
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

        /// <summary>
        /// send a previously collected message to TCP server of TWS
        /// выслать ранее собранное сообщение TCP серверу TWS
        /// </summary>
        private void TcpSendMessage()
        {
            _tcpWriter.Write(_message.ToArray());
            _message = new List<byte>();
        }

        // getting data from the thread
        // достаём данные из потока

        /// <summary>
        /// read value from the thread
        /// прочитать значение из потока
        /// </summary>
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

        /// <summary>
        /// read value from the thread
        /// прочитать значение из потока
        /// </summary>
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

        /// <summary>
        /// read value from the thread
        /// прочитать значение из потока
        /// </summary>
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

        /// <summary>
        /// read value from the thread
        /// прочитать значение из потока
        /// </summary>
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

        // Listen to TCP for messages
        // Прослушивание TCP на наличие в нём сообщений

        /// <summary>
        /// stream reading data from TWS
        /// поток считывающий данные от TWS
        /// </summary>
        private Thread _listenThread;

        /// <summary>
        /// method for working thread that listens TCP server of TWS
        /// метод в котором работает поток прослушивающий TCP сервер TWS
        /// </summary>
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


                    // next, an unnecessary Os.Engine date from which the stream still needs to be cleaned / далее не нужная Os.Engine дата, от которой всё же поток нужно чистить

                    else if (typeMessage == 5)
                    {
                        //OpenOrder
                        ClearOpenOrder();
                    }

                    else if (typeMessage == 59)
                    {
                        ClearCommissionReport();
                    }
                    else if (typeMessage == 58)
                    {
                        TcpReadInt();
                        TcpReadInt();
                        TcpReadInt();
                        TcpReadInt();
                        TcpReadInt();
                        TcpReadInt();
                    }
                    else if (typeMessage == 11)
                    {
                        ClearExecutionData();
                    }

                    else if (typeMessage == 64 || typeMessage == 52)
                    {
                        TcpReadInt();
                        TcpReadInt();
                    }
                    else if (typeMessage == 45)
                    {
                        int val = TcpReadInt();
                        int val2 = TcpReadInt();
                        int val3 = TcpReadInt();
                        double val4 = TcpReadDouble();
                        // TcpReadString();
                        //  TcpReadString();
                    }
                    else if (typeMessage == 17)
                    {
                        //HistoricalData
                        HistoricalDataEvent();
                    }
                    else
                    {
                        if (SkipUnnecessaryData(typeMessage) == false)
                        {
                            if (typeMessage == 0)
                            {
                                zeroMessagesCount++;

                                if (zeroMessagesCount % 5 == 0)
                                {
                                    SendLogMessage("Неучтённое сообщение НОЛЬ. Возможно потеря связи с сервером. Номер: " + typeMessage,
                                    LogMessageType.Error);
                                }

                                if (zeroMessagesCount > 50)
                                {
                                    _listenThread = null;
                                    SendLogMessage("Кол-во сообщений НОЛЬ, превысило 50, переподключаемся", LogMessageType.Error);
                                    Disconnect();
                                    return;
                                }
                            }
                            else
                            {
                                SendLogMessage("Неучтённое сообщение. Номер: " + typeMessage,
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
            var format = "yyyyMMdd  HH:mm:ss";

            for (int ctr = 0; ctr < itemCount; ctr++)
            {
                string date = TcpReadString();
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

                Candle candle = new Candle();
                candle.TimeStart = DateTime.ParseExact(date, format, CultureInfo.CurrentCulture);
                candle.Open = Convert.ToDecimal(open);
                candle.High = Convert.ToDecimal(high);
                candle.Low = Convert.ToDecimal(low);
                candle.Close = Convert.ToDecimal(close);

                if (volume > 0)
                {
                    candle.Volume = Convert.ToDecimal(volume);
                }
                else
                {
                    candle.Volume = 1;
                }
                series.CandlesArray.Add(candle);
            }

            if (CandlesUpdateEvent != null)
            {
                CandlesUpdateEvent(series);
            }
        }

        /// <summary>
        /// skip unnecessary data
        /// пропустить не нужные данные
        /// </summary>
        /// <param name="typeMessage">message number / номер сообщения</param>
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
            if (typeMessage == 6)
            { //Portfolio
                TcpReadString();
                TcpReadString();
                TcpReadString();
                TcpReadString();
                return true;
            }
            if (typeMessage == 62)
            {
                TcpReadInt();
                return true;
            }

            if (typeMessage == 19)
            { //ScannerParameters
                TcpReadString();
                return true;
            }
            else if (typeMessage == 16)
            { //FinancialAdvice
                TcpReadInt();
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
            else if (typeMessage == 8)
            { //PortfolioUpdateTime  
                TcpReadString();
                return true;
            }
            else if (typeMessage == 52)
            {
                TcpReadInt();
                TcpReadInt();
                return true;
            }
            else if (typeMessage == 64)
            {
                TcpReadInt();
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
                TcpReadInt();
                TcpReadInt();
                TcpReadInt();
                TcpReadString();
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
            else if (typeMessage == 62)
            {
                TcpReadInt();
                return true;
            }
            else if (typeMessage == 49)
            {
                TcpReadInt();
                return true;
            }

            return false;
        }

        /// <summary>
        /// upload portfolios
        /// загрузить порфели
        /// </summary>
        private void LoadAccounts()
        {
            TcpReadInt();
            TcpReadString();
        }

        /// <summary>
        /// upload depths
        /// загрузить стакан
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
        /// загрузить следующий валидный ID для ордера
        /// </summary>
        private void LoadValidId()
        {
            TcpReadInt();
            int orderId = TcpReadInt();

            _nextOrderNum = orderId;
        }

        /// <summary>
        /// upload portfolio position
        /// загрузить позицию по портфелю
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
        /// загрузить позицию по портфелю
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
        /// загрузить портфели
        /// </summary>
        private void LoadAccount()
        {
            TcpReadInt();
            TcpReadInt();
            string portfolio = TcpReadString();
            string val1 = TcpReadString();
            string val2 = TcpReadString();
            TcpReadString();

            if (val1 == "NetLiquidation" && NewAccauntValue != null)
            {
                NewAccauntValue(portfolio, Decimal.Parse(val2, NumberFormatInfo.InvariantInfo));
            }
        }

        /// <summary>
        /// upload trades
        /// загрузить трэйд
        /// </summary>
        private void LoadTrade()
        {
            int msgVersion = TcpReadInt();
            int requestId = TcpReadInt();
            int tickType = TcpReadInt();
            decimal price = TcpReadDecimal();

            if (msgVersion < 2)
            {
                return;
            }

            var size = TcpReadInt();

            if (msgVersion >= 3)
                TcpReadInt();

            if (tickType != 1 &&
                tickType != 2 &&
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
            trade.SecurityNameCode = security.Symbol + "_" + security.SecType + "_" + security.Exchange;

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
        /// загрузить ордер
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
                newOsOrder.State = OrderStateType.Activ;
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
        /// загрузить ошибку
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
        /// загрузить спецификацию по контракту
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
        /// очистить сообщение о только что открытом ордере
        /// </summary>
        private void ClearOpenOrder()
        {
            TcpReadInt();
            TcpReadInt();
            TcpReadInt();
            TcpReadString();
            TcpReadString();
            TcpReadString();
            TcpReadDouble();
            TcpReadString();
            TcpReadString();
            TcpReadString();
            TcpReadString();
            TcpReadString();
            TcpReadString();
            TcpReadString();
            TcpReadInt();
            TcpReadString();
            TcpReadDouble();
            TcpReadDouble();

            TcpReadString();
            TcpReadString();
            TcpReadString();
            TcpReadString();
            TcpReadInt();
            TcpReadString();
            TcpReadInt();
            TcpReadInt();
            TcpReadInt();
            TcpReadInt();
            TcpReadDouble();
            TcpReadString();
            TcpReadString();

            TcpReadString();
            TcpReadString();
            TcpReadString();
            TcpReadString();

            TcpReadString();

            TcpReadString();
            TcpReadDouble();
            TcpReadString();
            TcpReadInt();
            TcpReadString();
            TcpReadInt();

            TcpReadInt();
            TcpReadDouble();
            TcpReadDouble();
            TcpReadDouble();
            TcpReadDouble();
            TcpReadDouble();
            TcpReadInt();
            TcpReadInt();
            TcpReadInt();
            TcpReadInt();
            TcpReadInt();
            TcpReadInt();
            TcpReadInt();
            TcpReadInt();
            TcpReadDouble();

            TcpReadInt();
            TcpReadInt();

            TcpReadDouble();
            TcpReadInt();


            TcpReadString();
            TcpReadDouble();

            TcpReadInt();
            TcpReadInt();



            TcpReadDouble();


            TcpReadDouble();

            TcpReadDouble();
            TcpReadInt();
            TcpReadString();
            TcpReadInt();
            TcpReadInt();
            TcpReadInt();
            TcpReadInt();
            TcpReadInt();
            TcpReadDouble();
            TcpReadString();
            TcpReadInt();
            TcpReadString();
            TcpReadString();

            TcpReadInt();

            TcpReadInt();

            TcpReadString();


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

            TcpReadInt();
            TcpReadInt();
            TcpReadInt();
            TcpReadInt();
            TcpReadInt();
            TcpReadInt();
            TcpReadInt();
            TcpReadInt();
        }

        /// <summary>
        /// clear the message about execution
        /// очистить сообщение об исполнении 
        /// </summary>
        private void ClearExecutionData()
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

        /// <summary>
        /// clear the message about commissions
        /// очистить сообщение о комиссиях
        /// </summary>
        private void ClearCommissionReport()
        {
            TcpReadInt();

            TcpReadString();
            TcpReadDouble();
            TcpReadString();
            TcpReadDouble();
            TcpReadDouble();
            TcpReadInt();
        }

        /// <summary>
        /// order list
        /// список ордеров
        /// </summary>
        private List<Order> _orders;

        /// <summary>
        /// my trades list
        /// список созданных моих трейдов
        /// </summary>
        private List<MyTradeCreate> _myTradeCreate;

        /// <summary>
        /// event row updates in depth
        /// событие обновления строки в стакане
        /// </summary>
        public event Action<int, int, int, int, decimal, int> NewMarketDepth;

        /// <summary>
        /// new portfolio position
        /// новая позиция по портфелю
        /// </summary>
        public event Action<SecurityIb, string, int> NewPortfolioPosition;

        /// <summary>
        /// new trade
        /// новый трейд
        /// </summary>
        public event Action<Trade, SecurityIb> NewTradeEvent;

        /// <summary>
        /// new order in the system
        /// новый ордер в системе
        /// </summary>
        public event Action<Order> NewOrderEvent;

        /// <summary>
        /// my new trade in the system
        /// новая моя сделка в системе
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

        /// <summary>
        /// new security in the system
        /// новая бумага в системе
        /// </summary>
        public event Action<SecurityIb> NewContractEvent;

        /// <summary>
        /// new portfolio state
        /// новое состояние портфеля
        /// </summary>
        public event Action<string, decimal> NewAccauntValue;

        /// <summary>
        /// successfully connected to TWS server
        /// успешно подключились к серверу TWS
        /// </summary>
        public event Action ConnectionSucsess;

        /// <summary>
        /// connection to TWS server lost
        /// соединение с TWS разорвано
        /// </summary>
        public event Action ConnectionFail;

        public event Action<Candles> CandlesUpdateEvent;

        // logging / логирование работы

        /// <summary>
        /// add a new log message
        /// добавить в лог новое сообщение
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// outgoing log message
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }

    public class Candles
    {
        public int ContractId;

        public List<Candle> CandlesArray = new List<Candle>();
    }

    /// <summary>
    /// crutch class working in the process of creating my trades
    /// класс костыль работающий в процессе создания моих трейдов
    /// </summary>
    public class MyTradeCreate
    {
        /// <summary>
        /// parent's order number
        /// номер ордера родителя
        /// </summary>
        public int idOrder;

        /// <summary>
        /// parent's order volume at the time of my trade
        /// объём ордера родителя в момент выставления моего трейда
        /// </summary>
        public decimal FillOrderToCreateMyTrade;

    }

    /// <summary>
    /// security in IB format
    /// бумага в представлении Ib
    /// </summary>
    public class SecurityIb
    {
        /// <summary>
        /// создавать для этой бумаги бид с аском по последнему трейду
        /// и не ждать стакана
        /// </summary>
        public bool CreateMarketDepthFromTrades;

        /// <summary>
        /// number
        /// номер
        /// </summary>
        public int ConId;

        /// <summary>
        /// full name
        /// название полное
        /// </summary>
        public string Symbol;

        /// <summary>
        /// name
        /// название
        /// </summary>
        public string LocalSymbol;

        /// <summary>
        /// contract currency
        /// валюта контракта
        /// </summary>
        public string Currency;

        /// <summary>
        /// exchange
        /// биржа
        /// </summary>
        public string Exchange;

        /// <summary>
        /// main exchange
        /// основная биржа
        /// </summary>
        public string PrimaryExch;

        /// <summary>
        /// strike
        /// страйк
        /// </summary>
        public double Strike;

        /// <summary>
        /// instrument class
        /// класс инструмента
        /// </summary>
        public string TradingClass;

        /// <summary>
        /// minimum price step
        /// минимальный шаг цены
        /// </summary>
        public double MinTick;

        /// <summary>
        /// multiplier
        /// мультипликатор?
        /// </summary>
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
