﻿/*
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
    /// класс реализующий клиента для TCP сервера TWS
    /// </summary>
    public class IbClient
    {

        /// <summary>
        /// клиент
        /// </summary>
        private TcpClient _tcpClient;

        /// <summary>
        /// класс для чтения данных из потока
        /// </summary>
        private BinaryWriter _tcpWriter;

        /// <summary>
        /// класс для записи данных в поток
        /// </summary>
        private BinaryReader _tcpReader;

// коннект

        private bool _isConnected;

        /// <summary>
        /// установить соединение с TСP сервером TWS
        /// </summary>
        public void Connect(string host, int port)
        {
            if (_isConnected)
            {
                SendLogMessage("Запрошен повторный запуск клиента IB со статусом Connect", LogMessageType.Error);
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
                catch (IOException)
                {
                    SendLogMessage("Не получилось подключиться к серверу! ", LogMessageType.Error);
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

// Портфели

        /// <summary>
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
        /// все бумаги по которым нам пришла спицификация
        /// </summary>
        private List<SecurityIb> _serverSecurities;

        /// <summary>
        /// перечень бумаг по которым мы уже подписались на получение трейдов
        /// </summary>
        private List<string> _namesSecuritiesWhoOptOnTrades;

        /// <summary>
        /// перечень бумаг по которым мы уже подписались на получение стаканов
        /// </summary>
        private List<string> _namesSecuritiesWhoOptOnMarketDepth;

        /// <summary>
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
                TcpWrite("");
                TcpWrite(0);
                TcpWrite(null);
                TcpWrite("");
                TcpWrite(contract.Exchange);
                TcpWrite("");
                TcpWrite(contract.Currency);
                TcpWrite("");
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

        /// <summary>
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
        /// следующий правильный номер ордера
        /// </summary>
        private int _nextOrderNum;

        /// <summary>
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
                TcpWrite(order.Volume.ToString());
                TcpWrite(type);
                TcpWrite(order.Price.ToString(new NumberFormatInfo(){CurrencyDecimalSeparator = "."}));
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
        /// отозвать ордер
        /// </summary>
        public void CanselOrder(Order order)
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

// грузим данные в поток

        /// <summary>
        /// признак завершения сообщения для пакета
        /// </summary>
        private readonly byte _endOfMessage = 0;

        /// <summary>
        /// текущее сообщение
        /// </summary>
        private List<byte> _message;

        /// <summary>
        /// записать в сообщение новые данные
        /// </summary>
        private void TcpWrite(int value)
        {
            TcpWrite(value.ToString());
        }

        /// <summary>
        /// записать в сообщение новые данные
        /// </summary>
        private void TcpWrite(double value)
        {
            TcpWrite(value.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
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
        /// выслать ранее собранное сообщение TCP серверу TWS
        /// </summary>
        private void TcpSendMessage()
        {
            _tcpWriter.Write(_message.ToArray());
            _message = new List<byte>();
        }

// достаём данные из потока

        /// <summary>
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

// Прослушивание TCP на наличие в нём сообщений

        /// <summary>
        /// поток считывающий данные от TWS
        /// </summary>
        private Thread _listenThread;

        /// <summary>
        /// метод в котором работает поток прослушивающий TCP сервер TWS
        /// </summary>
        private void ListenThreadSpace()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(0);
                    int typeMessage = TcpReadInt();

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
                    else if (typeMessage == 12)
                    {
                        LoadMarketDepth();
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
                    else if (typeMessage == 62)
                    {
                        TcpReadInt();
                    }

// далее не нужная Os.Engine дата, от которой всё же поток нужно чистить

                    else if (typeMessage == 5)
                    { //OpenOrder
                        ClearOpenOrder();
                    }
                    else if (typeMessage == 2)
                    {// TickSize
                        TcpReadInt();
                        TcpReadInt();
                        TcpReadInt();
                        TcpReadInt();
                    }
                    else if (typeMessage == 45)
                    { // TickGeneric
                        TcpReadInt();
                        TcpReadInt();
                        TcpReadInt();
                        TcpReadDouble();
                    }
                    else if (typeMessage == 59)
                    {
                        ClearCommissionReport();
                    }
                    else if (typeMessage == 11)
                    {
                        ClearExecutionData();
                    }
                    else if (typeMessage == 52)
                    {
                        TcpReadInt();
                        TcpReadInt();
                    }
                    else if (typeMessage == 64)
                    {
                        TcpReadInt();
                        TcpReadString();
                    }
                    else
                    {
                        SendLogMessage("Неучтённое сообщение. Всё пропало! Номер: " + typeMessage, LogMessageType.Error);
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    return;
                }
            }
        }

        /// <summary>
        /// загрузить порфели
        /// </summary>
        private void LoadAccounts()
        {
            TcpReadInt();
            TcpReadString();
        }

        /// <summary>
        /// загрузить стакан
        /// </summary>
        private void LoadMarketDepth()
        {
            TcpReadInt();
            int requestId = TcpReadInt();
            int position = TcpReadInt();
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
        /// загрузить следующий валидный ID для ордера
        /// </summary>
        private void LoadValidId()
        {
            TcpReadInt();
            int orderId = TcpReadInt();

            _nextOrderNum = orderId;
        }

        /// <summary>
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
        /// загрузить портфели
        /// </summary>
        private void LoadAccount()
        {
            TcpReadInt();
            TcpReadInt();
            string portfolio = TcpReadString();
            string val1 = TcpReadString();
            string val2= TcpReadString();
            TcpReadString();

            if (val1 == "NetLiquidation" && NewAccauntValue != null)
            {
                NewAccauntValue(portfolio, Decimal.Parse(val2,NumberFormatInfo.InvariantInfo));
            }
        }

        /// <summary>
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

            if (NewTradeEvent != null)
            {
                NewTradeEvent(trade);
            }
        }

        /// <summary>
        /// загрузить ордер
        /// </summary>
        private void LoadOrder()
        {
            // разбираем сообщение
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

            // комплектуем ордер

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
                // надо сгенерить мои трейды

                int volume = newOsOrder.VolumeExecute - osOrder.VolumeExecute;

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
        /// список ордеров
        /// </summary>
        private List<Order> _orders;

        /// <summary>
        /// список созданных моих трейдов
        /// </summary>
        private List<MyTradeCreate> _myTradeCreate;

        /// <summary>
        /// событие обновления строки в стакане
        /// </summary>
        public event Action<int, int, int, int, decimal, int> NewMarketDepth;

        /// <summary>
        /// новая позиция по портфелю
        /// </summary>
        public event Action<SecurityIb, string, int> NewPortfolioPosition;

        /// <summary>
        /// новый трейд
        /// </summary>
        public event Action<Trade> NewTradeEvent;

        /// <summary>
        /// новый ордер в системе
        /// </summary>
        public event Action<Order> NewOrderEvent;

        /// <summary>
        /// новая моя сделка в системе
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

        /// <summary>
        /// новая бумага в системе
        /// </summary>
        public event Action<SecurityIb> NewContractEvent;

        /// <summary>
        /// новое состояние портфеля
        /// </summary>
        public event Action<string, decimal> NewAccauntValue;

        /// <summary>
        /// успешно подключились к серверу TWS
        /// </summary>
        public event Action ConnectionSucsess;

        /// <summary>
        /// соединение с TWS разорвано
        /// </summary>
        public event Action ConnectionFail;

// логирование работы

        /// <summary>
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
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }
}
