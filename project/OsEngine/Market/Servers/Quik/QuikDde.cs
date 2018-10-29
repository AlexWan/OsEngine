/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Logging;

namespace OsEngine.Market.Servers.Quik
{

    /// <summary>
    /// штука, наследник ДДЕ сервера, отвечает за подписку на определённые данные, сортировку данных из ДДЕ, приведение данных к виду массивов Си Шарп, и потом к их приведению к классам OsApi
    /// </summary>
    internal class QuikDde
    {

// работа с подпиской на каналы

        public QuikDde(string service)
        {
            _portfolios = new List<Portfolio>();
            _portfoliosQuik = new List<QuikPortfolio>();
        }

        private qC.QuikCon _server;

// доступ к Квик и активация ДДЕ

        public bool IsRegistered
        {
            get
            {
                if (_server == null)
                {
                    return false;
                }
                return true;
            }
        }

        public void StartServer()
        {
            _server = new qC.QuikCon();
            _server.Connect("OSA_DDE");
            _server.Message += _server_Message;
        }

        void _server_Message(string messageType, long l, object[,] table)
        {
            if (messageType == "portfolioSpot")
            {
                PortfolioSpotUpdated(l, table);
            }
            if (messageType == "portfolioSpotNumber")
            {
                PortfolioSpotNumberUpdated(l, table);
            }
            if (messageType == "portfolioDerivative")
            {
                PortfolioDerivativeUpdated(l, table);
            }
            if (messageType == "trade")
            {
                TradesUpdated(l, table);
            }
            if (messageType == "security")
            {
                SecuritiesUpdated(l, table);
            }
            if (messageType == "positionDerivative")
            {
                PositionDerivativeUpdated(l, table);
            }
            if (messageType == "positionSpot")
            {
                PositionSpotUpdated(l, table);
            }
            if (messageType.Split('_')[0] == "marketDepth")
            {
                GlassUpdated(l, table, messageType.Split('_')[1]);
            }
            if (messageType.Split('_')[0] == "message")
            {
                SendLogMessage(messageType.Split('_')[1],LogMessageType.System);
            }
            if (messageType.Split('_')[0] == "status")
            {
                DdeServerStatus state;
                Enum.TryParse(messageType.Split('_')[1], out state);

                if (StatusChangeEvent != null)
                {
                    StatusChangeEvent(state);
                }

            }
        }

        public void StopDdeInQuik()
        {
            if (_server != null)
            {
                _server.Disconnect();
            }
        }

        public event Action<DdeServerStatus> StatusChangeEvent;

  // 1) Таблица инструментов
        private void SecuritiesUpdated(long id, object[,] table)
        {
            int countElem = table.GetLength(0);

            if (countElem == 0)
            {
                return;
            }

            Security [] securities = new Security[countElem];

            decimal bestBid = 0;
            decimal bestAsk = 0;

                for (int i = 0; i < countElem; i++)
                {
                    try
                    {
                        securities[i] = new Security();
                        securities[i].NameFull = table[i, 0].ToString();
                        securities[i].Name = table[i, 1].ToString();
                        securities[i].NameClass = table[i, 2].ToString();

                        if (!string.IsNullOrEmpty(table[i, 3].ToString()))
                        {
                            string state = table[i, 3].ToString().ToLower();

                            if (state == "торгуется")
                            {
                                securities[i].State = SecurityStateType.Activ;
                            }
                            else if (state == "заморожена" || state == "приостановлена")
                            {
                                securities[i].State = SecurityStateType.Close;
                            }
                            else
                            {
                                securities[i].State = securities[i].State = SecurityStateType.UnKnown;
                            }
                        }
                        else
                        {
                            securities[i].State = SecurityStateType.UnKnown;
                        }

                        if (!string.IsNullOrEmpty(table[i, 4].ToString()))
                        {
                            securities[i].Lot = ToDecimal(table[i, 4]);
                        }

                        if (!string.IsNullOrEmpty(table[i, 5].ToString()))
                        {
                            securities[i].PriceStep = ToDecimal(table[i, 5]);
                        }

                        bestAsk = ToDecimal(table[i, 6]);
                        bestBid = ToDecimal(table[i, 7]);

                        if (!string.IsNullOrEmpty(table[i, 9].ToString()))
                        {
                            securities[i].PriceStepCost = ToDecimal(table[i, 9]);
                        }
                        else
                        {
                            securities[i].PriceStepCost = securities[i].PriceStep;
                        }

                        try
                        {
                            if (!string.IsNullOrEmpty(table[i, 10].ToString()))
                            {
                                DateTime time = Convert.ToDateTime(table[i, 10].ToString());

                                if (UpdateTimeSecurity != null)
                                {
                                    UpdateTimeSecurity(time);
                                }
                            }
                        }
                        catch (Exception error)
                        {
                            SendLogMessage(error.ToString(), LogMessageType.Error);
                        }

                        try
                        {
                            if (table.GetLength(1) > 11)
                                if (
                                    !string.IsNullOrEmpty(table[i, 11].ToString()))
                                {
                                    string type = table[i, 11].ToString();

                                    if (type == "Ценные бумаги")
                                    {
                                        securities[i].SecurityType = SecurityType.Stock;
                                    }
                                    else if (type == "Фьючерсы")
                                    {
                                        securities[i].SecurityType = SecurityType.Futures;
                                        securities[i].Lot = 1;
                                    }
                                    else if (type == "Опционы")
                                    {
                                        securities[i].SecurityType = SecurityType.Option;
                                        securities[i].Lot = 1;
                                    }
                                }

                        }
                        catch (Exception error)
                        {
                            SendLogMessage(error.ToString(), LogMessageType.Error);
                        }
                    }
                    catch (Exception)
                    { // здесь убираем элемент по индексу, и уменьшаем массив, т.к. в строке кака
                        if (securities.Length == 1)
                        { // если битым является единственный элемент массива
                            return;
                        }

                        Security[] newArraySecurities = new Security[securities.Length-1];

                        for (int i2 = 0; i2 < i; i++)
                        {
                            newArraySecurities[i2] = securities[i2];
                        }
                        securities = newArraySecurities;
                    }

                    securities[i].PriceLimitHigh = 0;
                    securities[i].PriceLimitLow = 0;

                    if (UpdateSecurity != null)
                    {
                        UpdateSecurity(securities[i], bestBid, bestAsk);
                    }
                }
        }

        public event Action<Security,decimal,decimal> UpdateSecurity;

        public event Action<DateTime> UpdateTimeSecurity;

  // 2) таблица всех сделок
        private void TradesUpdated(long id, object[,] table)
        {
            try
            {
                int countElem = table.GetLength(0);

                if (countElem == 0)
                {
                    return;
                }

                Trade[] trades = new Trade[countElem];

                for (int i = 0; i < countElem; i++)
                {
                    trades[i] = new Trade();
                    trades[i].Id = table[i, 0].ToString();

                    string[] date = DateTime.Parse(table[i, 1].ToString()).ToString("dd.MM.yyyy").Split('.');
                    string[] time = DateTime.Parse(table[i, 2].ToString()).ToString("HH:mm:ss").Split(':');

                    trades[i].Time = new DateTime(Convert.ToInt32(date[2]),
                        Convert.ToInt32(date[1]), Convert.ToInt32(date[0]),
                        Convert.ToInt32(time[0]), Convert.ToInt32(time[1]),
                        Convert.ToInt32(time[2])
                        );

                    trades[i].SecurityNameCode = table[i, 3].ToString();
                    trades[i].Price = ToDecimal(table[i, 4]);
                    trades[i].Volume = Convert.ToInt32(table[i, 5]);

                    string operation = table[i, 6].ToString().ToLower();

                    if (!string.IsNullOrWhiteSpace(operation))
                    {
                        if (operation == "купля" || operation == "b" || operation == "buy")
                        {
                            trades[i].Side = Side.Buy;
                        }
                        else if (operation == "продажа" || operation == "s" || operation == "sell")
                        {
                            trades[i].Side = Side.Sell;
                        }
                        else
                        {
                            trades[i].Side = Side.None;
                        }
                    }
                    else
                    {
                        trades[i].Side = Side.None;
                    }

                }

                List<Trade> newTrade = new List<Trade>(trades);

                if (UpdateTrade != null)
                {
                    UpdateTrade(newTrade);
                }
            }
            catch (Exception error)
            {

                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public event Action<List<Trade>> UpdateTrade;

  // 3) порфели и спот и деривативы
        private void PortfolioSpotUpdated(long id, object[,] table)
        {
             int countElem = table.GetLength(0);

            if (countElem == 0)
            {
                return;
            }

            for (int i = 0; i < countElem; i++)
            {
                string firm = table[i, 0].ToString();

                if (firm.Length != firm.Replace("SPBFUT", "").Length)
                {
                    return;
                }
                
                decimal valueBegin = ToDecimal(table[i, 1]);

                decimal profitLoss = ToDecimal(table[i, 3]);
                decimal valueCurrent = valueBegin + profitLoss;

                decimal valueBlock = valueCurrent - ToDecimal(table[i, 2]);

                if (valueBegin == 0 &&
                    valueBlock == 0 &&
                    profitLoss == 0)
                {
                    return;
                }

                string portfolioClient = table[i, 4].ToString();

                QuikPortfolio data = _portfoliosQuik.Find(port => port.Firm == firm);

                if (data == null)
                {
                    PortfolioReturner r = new PortfolioReturner();
                    r.Long = id;
                    r.Objects = table;
                    r.PortfolioUpdate += PortfolioSpotUpdated;
                    Thread worker = new Thread(r.Start);
                    worker.IsBackground = true;
                    worker.Start();
                    return;
                }

                Portfolio portfolioWithoutClient = _portfolios.Find(portfolio => portfolio.Number == data.Number);

                if (portfolioWithoutClient != null)
                {
                    portfolioWithoutClient.Number = data.Number + "@" + portfolioClient;
                }

                Portfolio myPortfolio = _portfolios.Find(portfolio => portfolio.Number == data.Number + "@" + portfolioClient);

                if (myPortfolio == null)
                {
                    continue;
                }

                myPortfolio.ValueBegin = valueBegin;
                myPortfolio.ValueCurrent = valueCurrent;
                myPortfolio.ValueBlocked = valueBlock;
                myPortfolio.Profit = profitLoss;

                if (UpdatePortfolios != null)
                {
                    UpdatePortfolios(_portfolios);
                }
            }
        }

        private void PortfolioSpotNumberUpdated(long id, object[,] table)
        {
            int countElem = table.GetLength(0);

            if (countElem == 0)
            {
                return;
            }

            for (int i = 0; i < countElem; i++)
            {
                string firm = table[i, 0].ToString();

                if (firm.Length != firm.Replace("SPBFUT", "").Length)
                {
                    return;
                }

                string numberPortfolio = table[i, 1].ToString();

                Portfolio myPortfolio = _portfolios.Find(portfolio => portfolio.Number == numberPortfolio);

                if (myPortfolio == null)
                {
                    myPortfolio = new Portfolio();
                    myPortfolio.Number = numberPortfolio;
                    _portfolios.Add(myPortfolio);

                    QuikPortfolio data = new QuikPortfolio();
                    data.Firm = firm;
                    data.Number = numberPortfolio;
                    _portfoliosQuik.Add(data);
                }

                if (UpdatePortfolios != null)
                {
                    UpdatePortfolios(_portfolios);
                }
            }
        }

        private void PortfolioDerivativeUpdated(long id, object[,] table)
        {
            int countElem = table.GetLength(0);

            if (countElem == 0)
            {
                return;
            }

            for (int i = 0; i < countElem; i++)
            {
                string numberPortfolio =table[i, 0].ToString();
                decimal valueBegin = ToDecimal(table[i, 1]);
                decimal valueBlock = ToDecimal(table[i, 2]);
                decimal profitLoss = ToDecimal(table[i, 3]) + ToDecimal(table[i, 4]);
                decimal valueCurrent = valueBegin + profitLoss;

                Portfolio myPortfolio = _portfolios.Find(portfolio => portfolio.Number == numberPortfolio);

                if (myPortfolio == null)
                {
                    myPortfolio = new Portfolio();
                    myPortfolio.Number = numberPortfolio;
                    _portfolios.Add(myPortfolio);
                }

                myPortfolio.ValueBegin = valueBegin;
                myPortfolio.ValueCurrent = valueCurrent;
                myPortfolio.ValueBlocked = valueBlock;
                myPortfolio.Profit = profitLoss;

                if (UpdatePortfolios != null)
                {
                    UpdatePortfolios(_portfolios);
                }
            }
        }

        private List<Portfolio> _portfolios;

        private List<QuikPortfolio> _portfoliosQuik; 

        public event Action<List<Portfolio>> UpdatePortfolios;

  // 4) позиция спот и деривативы

        private void PositionDerivativeUpdated(long id, object[,] table)
        {
            int countElem = table.GetLength(0);

            if (countElem == 0)
            {
                return;
            }

            for (int i = 0; i < countElem; i++)
            {
                PositionOnBoard position = new PositionOnBoard();

                position.PortfolioName = table[i, 0].ToString();
                position.SecurityNameCode = table[i, 1].ToString();
                position.ValueBegin = Convert.ToDecimal(table[i, 2]);
                position.ValueCurrent = Convert.ToDecimal(table[i, 3]);
                position.ValueBlocked = Math.Abs(Convert.ToDecimal(table[i, 4])) +
                                        Math.Abs(Convert.ToDecimal(table[i, 5]));

                UpDatePosition(position);
            }
        }

        private void PositionSpotUpdated(long id, object[,] table)
        {
            int countElem = table.GetLength(0);

            if (countElem == 0)
            {
                return;
            }

            for (int i = 0; i < countElem; i++)
            {
                PositionOnBoard position = new PositionOnBoard();

                position.PortfolioName = table[i, 0].ToString();
                position.SecurityNameCode = table[i, 1].ToString();
                position.ValueBegin = Convert.ToDecimal(table[i, 2]);
                position.ValueCurrent = Convert.ToDecimal(table[i, 3]);
                position.ValueBlocked = Math.Abs(Convert.ToDecimal(table[i, 4]));

                UpDatePosition(position);
            }
        }

        private object _lockerUpDatePosition = new object();

        private void UpDatePosition(PositionOnBoard position)
        {
            lock (_lockerUpDatePosition)
            {
                Portfolio myPortfolio = null;

                if (_portfolios != null)
                {
                    myPortfolio = _portfolios.Find(portfolio => portfolio.Number.Split('@')[0] == position.PortfolioName);
                }
                if (myPortfolio == null)
                {
                    return;
                }

                myPortfolio.SetNewPosition(position);

                if (UpdatePortfolios != null)
                {
                    UpdatePortfolios(_portfolios);
                }
            }
        }

  // 5) стаканы
        private void GlassUpdated(long id, object[,] table, string nameSecurity)
        {
            int countElem = table.GetLength(0);

            if (countElem == 0)
            {
                return;
            }

            // в тайблах у нас уровни сверху вниз, как в таблице из Квик. 
            // первый индекс это номер строки, второй столбца

            List<MarketDepthLevel> asks = new List<MarketDepthLevel>();

            List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

            for (int i = 0; i < countElem; i++)
            {
                if (ToDecimal(table[i, 0]) == 0)
                {
                    asks.Add(new MarketDepthLevel() {
                        Bid = ToDecimal(table[i, 2]),
                        Price = ToDecimal(table[i, 1]),
                        Ask = 0
                    });
                }
                else
                {
                    bids.Add(new MarketDepthLevel()
                    {
                        Bid = 0,
                        Price = ToDecimal(table[i, 1]),
                        Ask = ToDecimal(table[i, 0])
                    }); 
                }
            }

            if (asks.Count > 1 && asks[0].Price < (asks[1].Price))
            {
                List<MarketDepthLevel> asksSort = new List<MarketDepthLevel>();
                for (int i = asks.Count - 1; i > -1; i--)
                {
                    asksSort.Add(asks[i]);
                }
                asks = asksSort;
            }

            if (bids.Count > 1 && bids[0].Price > (bids[1].Price))
            {
                List<MarketDepthLevel> bidsSort = new List<MarketDepthLevel>();
                for (int i = bids.Count - 1; i > -1; i--)
                {
                    bidsSort.Add(bids[i]);
                }

                bids = bidsSort;
            }

            MarketDepth glass = new MarketDepth();
            glass.Bids = asks;
            glass.Asks = bids;
            glass.SecurityNameCode = nameSecurity;

            if (UpdateGlass != null)
            {
                UpdateGlass(glass);
            }
        }

        public event Action<MarketDepth> UpdateGlass;

        private decimal ToDecimal(object str)
        {
            decimal result;
            decimal.TryParse(Convert.ToString(str), NumberStyles.Any, new CultureInfo("ru-RU"), out result);

            if (result == 0)
            {
                decimal.TryParse(Convert.ToString(str), NumberStyles.Any, new CultureInfo("en-US"), out result);
            }

            return result;
        }

// работа с логами

        private void SendLogMessage(string message,LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message,type);
            }
        }

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }

    /// <summary>
    /// класс для промежуточного хранения портфелей спот для сервера Квик
    /// </summary>
    public class QuikPortfolio
    {
        /// <summary>
        /// портфель
        /// </summary>
        public string Number;

        /// <summary>
        /// фирма
        /// </summary>
        public string Firm;
    }


    /// <summary>
    /// типы состояния ДДЕ сервера
    /// </summary>
    public enum DdeServerStatus
    {
        Connected,
        Disconnected
    }

    /// <summary>
    /// штука отправляющая таблицу портфеля на второй круг
    /// </summary>
    public class PortfolioReturner
    {
        public long Long;

        public object[,] Objects;

        public void Start()
        {
            Thread.Sleep(3000);

            if (PortfolioUpdate != null)
            {
                PortfolioUpdate(Long, Objects);
            }
        }

        public event Action<long, object[,]> PortfolioUpdate;
    }

}