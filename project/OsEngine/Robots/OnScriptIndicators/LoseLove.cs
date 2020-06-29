using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
//using OkonkwoOandaV20.TradeLibrary.DataTypes.Position;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.OnScriptIndicators
{
    /// <summary>
    /// робот лоселов
    /// </summary>
    public class LoseLove : BotPanel
    {
        #region Entities
        public struct ZPoint
        {
            public decimal Price;
            public string DataString;
            public bool isRise;
        }
        #endregion

        #region Tabs
        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab1;
        #endregion

        #region Indicators
        private Aindicator _zigZag;
        private Aindicator _bearsP;
        private Aindicator _bullsP;
        private Aindicator _pc;
        #endregion

        #region Settings
        /// <summary>
        /// regime
        /// режим работы
        /// </summary>
        public StrategyParameterString Regime;

        /// <summary>
        /// Trade side
        /// Куда торгуем
        /// </summary>
        public StrategyParameterString TradeSide;

        /// <summary>
        /// ZigZag Period
        /// Период ZigZag
        /// </summary>
        public StrategyParameterInt ZigZagPeriod;

        /// <summary>
        /// Power side
        /// Сила зверей (не включая лосей)
        /// </summary>
        public StrategyParameterInt PowerPeriod;

        /// <summary>
        /// Index Lenght
        /// период индекса
        /// </summary>
        public StrategyParameterInt IndLenght;

        /// <summary>
        /// Risk
        /// Нагрузка на депозит в каждой сделке в процентах
        /// </summary>
        public StrategyParameterDecimal Risk;

        /// <summary>
        /// How much points get from past
        /// Сколько значений взять из истории
        /// </summary>
        public StrategyParameterInt LookBack;

        /// <summary>
        /// ATR Period
        /// Период ATR
        /// </summary>
        public StrategyParameterInt ATRperiod;

        /// <summary>
        /// ATR Multiplier
        /// Мультипликатор ATR
        /// </summary>
        public StrategyParameterDecimal ATRmultiplier;

        /// <summary>
        /// Data directory path
        /// Путь к папке с данными
        /// </summary>
        public StrategyParameterString DataDir;
        #endregion

        #region Fields
        private const string IpAdress = "127.0.0.1";

        private const int Port = 8020;

        private string fname_data_multi = "";

        private List<string> table_multi = new List<string>();

        private string dataToString_multi = "";

        private List<string> valueList_multi = new List<string>();

        private string delim = ";";

        private IPEndPoint ipPoint;

        private List<ZPoint> zPoints = new List<ZPoint>();

        private bool isRise = false;

        private bool zPointDirect = true;
        #endregion

        public LoseLove(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab1 = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "Client", "Parser" });
            TradeSide = CreateParameter("Trade Side", "Both", new[] { "Both", "Buy", "Sell" });
            PowerPeriod = CreateParameter("Power Period", 13, 1, 100, 1);
            IndLenght = CreateParameter("IndLenght", 10, 1, 100, 1);
            ZigZagPeriod = CreateParameter("ZigZag Period", 100, 10, 1000, 10);
            Risk = CreateParameter("Risk", 1m, 0.1m, 100, 0.1m);
            LookBack = CreateParameter("LookBack", 10, 10, 1000, 10);
            ATRperiod = CreateParameter("ATR Period", 13, 1, 100, 1);
            ATRmultiplier = CreateParameter("ATR Multiplier", 2m, 0.1m, 10m, 0.1m);
            DataDir = CreateParameter("Data Dir", @"D:\DataSource", new[] { @"C:\DataSource", @"D:\DataSource" });

            _tab1.CandleFinishedEvent += _tab1_CandleFinishedEvent;

            _zigZag = IndicatorsFactory.CreateIndicatorByName("ZigZag", name + "ZigZag", false);
            _zigZag.ParametersDigit[0].Value = ZigZagPeriod.ValueInt;
            _zigZag.PaintOn = false;
            _zigZag = (Aindicator)_tab1.CreateCandleIndicator(_zigZag, "Prime");
            _zigZag.Save();

            _bearsP = IndicatorsFactory.CreateIndicatorByName("BearsPower", name + "BearsPower", false);
            _bearsP = (Aindicator)_tab1.CreateCandleIndicator(_bearsP, "TreshArea");
            _bearsP.ParametersDigit[0].Value = PowerPeriod.ValueInt;
            _bearsP.Save();

            _bullsP = IndicatorsFactory.CreateIndicatorByName("BullsPower", name + "BullsPower", false);
            _bullsP = (Aindicator)_tab1.CreateCandleIndicator(_bullsP, "TreshArea");
            _bullsP.ParametersDigit[0].Value = PowerPeriod.ValueInt;
            _bullsP.Save();

            _pc = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PriceChannel", false);
            _pc = (Aindicator)_tab1.CreateCandleIndicator(_pc, "Prime");
            _pc.ParametersDigit[0].Value = IndLenght.ValueInt;
            _pc.ParametersDigit[1].Value = IndLenght.ValueInt;
            _pc.Save();

            ParametrsChangeByUser += LoseLove_ParametrsChangeByUser;
            _tab1.PositionOpeningSuccesEvent += _tab1_PositionOpeningSuccesEvent;

            LoseLove_ParametrsChangeByUser();
        }

        private void _tab1_PositionOpeningSuccesEvent(Position pos)
        {
            
        }

        private void LoseLove_ParametrsChangeByUser()
        {
            if (_zigZag.ParametersDigit[0].Value != ZigZagPeriod.ValueInt)
            {
                _zigZag.ParametersDigit[0].Value = ZigZagPeriod.ValueInt;
                _zigZag.Reload();
            }

            if (_bearsP.ParametersDigit[0].Value != PowerPeriod.ValueInt)
            {
                _bearsP.ParametersDigit[0].Value = PowerPeriod.ValueInt;
                _bearsP.Reload();
            }

            if (_bullsP.ParametersDigit[0].Value != PowerPeriod.ValueInt)
            {
                _bullsP.ParametersDigit[0].Value = PowerPeriod.ValueInt;
                _bullsP.Reload();
            }

            if (IndLenght.ValueInt != _pc.ParametersDigit[0].Value ||
            IndLenght.ValueInt != _pc.ParametersDigit[1].Value)
            {
                _pc.ParametersDigit[0].Value = IndLenght.ValueInt;
                _pc.ParametersDigit[1].Value = IndLenght.ValueInt;

                _pc.Reload();
            }

            #region ML init
            if (Regime.ValueString == "Parser")
            {
                if (!Directory.Exists(DataDir.ValueString))
                    Directory.CreateDirectory(DataDir.ValueString);

                fname_data_multi = string.Format("{0}", DataDir.ValueString + "\\data-multi.csv");

                string[] dataSaver = new string[1]
                {
                    fname_data_multi
                };

                for (int x = 0; x < dataSaver.GetLength(0); x++)
                {
                    if (!File.Exists(dataSaver[x]))
                        File.Create(dataSaver[x]);

                    else if (File.Exists(dataSaver[x]))
                    {
                        File.Delete(dataSaver[x]);
                        File.Create(dataSaver[x]);
                    }
                }
            }
            #endregion
        }

        private void _tab1_CandleFinishedEvent(List<Candle> obj)
        {
            ZpointsUpdate();

            if (zPoints.Count <= LookBack.ValueInt + ATRmultiplier.ValueDecimal)
                return;

            if (Regime.ValueString == "Parser")
            {
                SaveData(fname_data_multi, table_multi);
                CollectData();
            }

            if (Regime.ValueString == "Client")
            {
                ClosePositions();
                OpenPositions();
            }
        }

        private void SaveData(string fname_data, List<string> table_tab)
        {
            if (table_tab.Count >= 1000)
            {
                File.AppendAllLines(fname_data, table_tab);
                table_tab.Clear();
            }
        }

        int counter = 0;

        private void CollectData()
        {
            if (zPoints.Count == counter)
                return;
            counter = zPoints.Count;

            int marker = 0;
            decimal lastDelta = zPoints[zPoints.Count - 1].Price - zPoints[zPoints.Count - 2].Price;
            decimal ARTvalue = GetAverage(ATRperiod.ValueInt) * ATRmultiplier.ValueDecimal;

            if (lastDelta > ARTvalue)
                marker = 1;
            else if (lastDelta < ARTvalue && Math.Abs(lastDelta) > ARTvalue)
                marker = 2;

            table_multi.Add(marker.ToString() + delim + zPoints[zPoints.Count - 2].DataString);
        }


        private void ZpointsUpdate()
        {
            if (GetZigZagValue(_tab1.CandlesAll, _zigZag.DataSeries[0].Values, 0) != 0)
            {
                if (GetZigZagValue(_tab1.CandlesAll, _zigZag.DataSeries[0].Values, 1) < GetZigZagValue(_tab1.CandlesAll, _zigZag.DataSeries[0].Values, 2) && zPointDirect == true)
                {
                    zPointDirect = false;
                    zPoints.Add(new ZPoint { Price = GetZigZagValue(_tab1.CandlesAll, _zigZag.DataSeries[0].Values, 0), DataString = GetDataString(valueList_multi, 0), isRise = zPointDirect });
                }
                else if (GetZigZagValue(_tab1.CandlesAll, _zigZag.DataSeries[0].Values, 1) < GetZigZagValue(_tab1.CandlesAll, _zigZag.DataSeries[0].Values, 2) && zPointDirect == false)
                {
                    if (zPoints.Count > 0)
                        zPoints.Remove(zPoints.Last());
                    zPoints.Add(new ZPoint { Price = GetZigZagValue(_tab1.CandlesAll, _zigZag.DataSeries[0].Values, 0), DataString = GetDataString(valueList_multi, 0), isRise = zPointDirect });
                }
                else if (GetZigZagValue(_tab1.CandlesAll, _zigZag.DataSeries[0].Values, 1) > GetZigZagValue(_tab1.CandlesAll, _zigZag.DataSeries[0].Values, 2) && zPointDirect == false)
                {
                    zPointDirect = true;
                    zPoints.Add(new ZPoint { Price = GetZigZagValue(_tab1.CandlesAll, _zigZag.DataSeries[0].Values, 0), DataString = GetDataString(valueList_multi, 0), isRise = zPointDirect });
                }
                else if (GetZigZagValue(_tab1.CandlesAll, _zigZag.DataSeries[0].Values, 1) > GetZigZagValue(_tab1.CandlesAll, _zigZag.DataSeries[0].Values, 2) && zPointDirect == true)
                {
                    if (zPoints.Count > 0)
                        zPoints.Remove(zPoints.Last());
                    zPoints.Add(new ZPoint { Price = GetZigZagValue(_tab1.CandlesAll, _zigZag.DataSeries[0].Values, 0), DataString = GetDataString(valueList_multi, 0), isRise = zPointDirect });
                }
            }
        }

        private decimal GetAverage(int countInt)
        {
            decimal[] cArray = new decimal[countInt];
            for (int i = 0; i < countInt; i++)
            {
                cArray[i] = Math.Abs(zPoints[zPoints.Count - 1 - i].Price - zPoints[zPoints.Count - 2 - i].Price);
            }

            return cArray.Average();
        }

        private string GetDataString(List<string> valueList, int backStep)
        {
            valueList.Clear();

            for (int i = 0 + backStep; i < LookBack.ValueInt + backStep ; i++)
            {
                valueList.Add(Math.Round(GetZigZagValue(_tab1.CandlesAll, _zigZag.DataSeries[0].Values, 0 + i) - GetZigZagValue(_tab1.CandlesAll, _zigZag.DataSeries[0].Values, 1 + i), 6).ToString());
                valueList.Add(Math.Round(GetZigZagCount(_tab1.CandlesAll, _zigZag.DataSeries[0].Values, 0 + i) - GetZigZagCount(_tab1.CandlesAll, _zigZag.DataSeries[0].Values, 1 + i), 6).ToString());
                valueList.Add(Math.Round(_pc.DataSeries[0].Values[_pc.DataSeries[0].Values.Count - 1] - _tab1.CandlesFinishedOnly[_tab1.CandlesFinishedOnly.Count - 1].Close, 6).ToString());
                valueList.Add(Math.Round(_tab1.CandlesFinishedOnly[_tab1.CandlesFinishedOnly.Count - 1].Close - _pc.DataSeries[1].Values[_pc.DataSeries[1].Values.Count - 1], 6).ToString());
                valueList.Add(Math.Round(_bearsP.DataSeries[0].Values[_bearsP.DataSeries[0].Values.Count - 1], 6).ToString());
                valueList.Add(Math.Round(_bullsP.DataSeries[0].Values[_bullsP.DataSeries[0].Values.Count - 1], 6).ToString());
                valueList.Add(Math.Round(_tab1.CandlesFinishedOnly[_tab1.CandlesFinishedOnly.Count - 1].Volume, 6).ToString());
                valueList.Add(Math.Round(_tab1.CandlesFinishedOnly[_tab1.CandlesFinishedOnly.Count - 1].ShadowBody, 6).ToString());
                valueList.Add(Math.Round(_tab1.CandlesFinishedOnly[_tab1.CandlesFinishedOnly.Count - 1].ShadowBottom, 6).ToString());
                valueList.Add(Math.Round(_tab1.CandlesFinishedOnly[_tab1.CandlesFinishedOnly.Count - 1].ShadowTop, 6).ToString());
                valueList.Add(Convert.ToByte(_tab1.CandlesFinishedOnly[_tab1.CandlesFinishedOnly.Count - 1].IsUp).ToString());
            }

            return string.Join(delim, valueList.ToArray());
        }


        public override string GetNameStrategyType()
        {
            return "LoseLove";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        public string WebClient(string message)
        {
            try
            {
                ipPoint = new IPEndPoint(IPAddress.Parse(IpAdress), Port);
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                // подключаемся к удаленному хосту
                socket.Connect(ipPoint);
                byte[] data = Encoding.Unicode.GetBytes(message);
                socket.Send(data);

                // получаем ответ
                data = new byte[1024];
                // буфер для ответа
                StringBuilder builder = new StringBuilder();
                int bytes = 0;
                // количество полученных байт
                do
                {
                    bytes = socket.Receive(data, data.Length, 0);
                    builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
                } while (socket.Available > 0);

                // закрываем сокет
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                //Print(builder.ToString());
                return builder.ToString();

            }
            catch
            {
                return "";
            }
        }

        private void ClosePositions()
        {
            Position[] Positions = _tab1.PositionsAll.Where(x => x.State == PositionStateType.Open).ToArray();

            foreach(var pos in Positions)
            {
                decimal takeProfit = Convert.ToDecimal(pos.SignalTypeOpen.Split(';')[0]);
                decimal stopLoss = Convert.ToDecimal(pos.SignalTypeOpen.Split(';')[1]);

                if (pos.Direction == Side.Buy)
                {
                    if (_tab1.PriceBestBid >= takeProfit)
                        _tab1.CloseAtMarket(pos, pos.OpenVolume);
                    else if (_tab1.PriceBestAsk <= stopLoss)
                        _tab1.CloseAtMarket(pos, pos.OpenVolume);
                } else if(pos.Direction == Side.Sell)
                {
                    if (_tab1.PriceBestAsk <= takeProfit)
                        _tab1.CloseAtMarket(pos, pos.OpenVolume);
                    else if (_tab1.PriceBestBid >= stopLoss)
                        _tab1.CloseAtMarket(pos, pos.OpenVolume);
                }
            }
        }

        private void OpenPositions()
        {
            if (isRise == zPoints.Last().isRise)
                return;
            else
                isRise = zPoints.Last().isRise;

            Position[] BuyPositions = _tab1.PositionsAll.Where(x => (x.State == PositionStateType.Open || x.State == PositionStateType.Opening || x.State == PositionStateType.Closing) && x.Direction == Side.Buy).ToArray();
            Position[] SellPositions = _tab1.PositionsAll.Where(x => (x.State == PositionStateType.Open || x.State == PositionStateType.Opening || x.State == PositionStateType.Closing) && x.Direction == Side.Sell).ToArray();

            dataToString_multi = GetDataString(valueList_multi, 0);
            string answer = WebClient(dataToString_multi + delim + "multi");

            decimal ARTvalue = GetAverage(ATRperiod.ValueInt) * ATRmultiplier.ValueDecimal;

            if (answer == "1" && BuyPositions.Length == 0 && TradeSide.ValueString != "Sell")
                _tab1.BuyAtMarket(GetVolume(_tab1), (_tab1.PriceBestAsk + ARTvalue).ToString() + delim + (_tab1.PriceBestBid - ARTvalue).ToString());

            if (answer == "2" && SellPositions.Length == 0 && TradeSide.ValueString != "Buy")
                _tab1.SellAtMarket(GetVolume(_tab1), (_tab1.PriceBestBid - ARTvalue).ToString() + delim + (_tab1.PriceBestAsk + ARTvalue).ToString());
        }

        public decimal GetVolume(BotTabSimple tab)
        {
            decimal depo = tab.Portfolio.ValueCurrent;
            decimal minVolume = tab.Securiti.PriceLimitLow;
            decimal value = tab.CandlesFinishedOnly.Last().Close;

            decimal pipsCount = value / tab.Securiti.PriceStep;

            decimal volume = (depo * Risk.ValueDecimal) / (tab.Securiti.PriceStepCost * pipsCount * 100);

            if (volume < minVolume)
                volume = minVolume;

            return Math.Round(volume, tab.Securiti.Decimals);
        }

        //---------------------------------------------------------------
        //---ПОЛУЧАЕМ НОМЕР СВЕЧИ ЭКСТРЕММУМА----------------------------
        //---------------------------------------------------------------
        decimal GetZigZagCount(List<Candle> candles, List<decimal> results, int indexFromEnd)
        {
            for (var i = candles.Count - 1; i >= 0; i--)
            {
                if (results[i] != 0)
                {
                    if (indexFromEnd == 0)
                        return i;
                    indexFromEnd--;
                }
            }
            return 0;
        }

        //---------------------------------------------------------------
        //---ПОЛУЧАЕМ ЗНАЧЕНИЯ ЦЕНЫ В ТОЧКАХ ЭКСТРЕММУМА-----------------
        //---------------------------------------------------------------
        decimal GetZigZagValue(List<Candle> candles, List<decimal> results, int indexFromEnd)
        {
            for (var i = candles.Count - 1; i >= 0; i--)
            {
                if (results[i] != 0)
                {
                    if (indexFromEnd == 0)
                        return results[i];
                    indexFromEnd--;
                }
            }
            return 0;
        }
    }
}
