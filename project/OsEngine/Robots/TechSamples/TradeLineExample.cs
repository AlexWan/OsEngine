using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System.Net;
using System.Threading.Tasks;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Charts.CandleChart.Elements;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms;
using Line = OsEngine.Charts.CandleChart.Elements.Line;

namespace OsEngine.Robots.TechSamples
{
    [Bot("TradeLineExample")]
    public class TradeLineExample : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;

        public StrategyParameterInt CandlesForPcLevel;
        public StrategyParameterDecimal GoodDif;

        private StrategyParameterTimeOfDay StartTradeTime;
        private StrategyParameterTimeOfDay EndTradeTime;

        private StrategyParameterDecimal ProfitPercent;
        private StrategyParameterDecimal StopPercent;

        private StrategyParameterCheckBox RegimeTAlerts;
        private StrategyParameterString BotToken;
        private StrategyParameterString TelegramId;

        // Indicator setting 
        public StrategyParameterInt PeriodPC;
        private StrategyParameterInt LengthZig;

        // Indicator
        Aindicator _PC;
        Aindicator _ZZ;


        private bool _signalBuy;
        private bool _signalBuyClose;


        ////////////////////////////////////////////////
        //MAIN BASE
        ////////////////////////////////////////////////
        ///
        /// 
        public TradeLineExample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic setting
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" },
                "Base");

            StartTradeTime = CreateParameterTimeOfDay("Start Trade Time", 9, 15, 0, 0, "Base");
            EndTradeTime = CreateParameterTimeOfDay("End Trade Time", 23, 40, 0, 0, "Base");

            ProfitPercent = CreateParameter("Profit percent", 1, 0.5m, 5, 0.1m);
            StopPercent = CreateParameter("Stop percent", 0.3m, 0.2m, 2, 0.1m);

            // Setting indicator 

            LengthZig = CreateParameter("Period Zig", 16, 1, 20, 1, "Indicators");


            // Create indicator ZigZag
            _ZZ = IndicatorsFactory.CreateIndicatorByName("ZigZag", name + "ZigZag", false);
            _ZZ = (Aindicator)_tab.CreateCandleIndicator(_ZZ, "Prime");
            ((IndicatorParameterInt)_ZZ.Parameters[0]).ValueInt = LengthZig.ValueInt;
            _ZZ.Save();

            // Events           
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            ParametrsChangeByUser += TradeLineExample_ParametrsChangeByUser;

            this.Description = "Example of trading on sloping levels";
        }

        public override string GetNameStrategyType()
        {
            return "TradeLineExample";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        //Indicator Update event
        private void TradeLineExample_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ZZ.Parameters[0]).ValueInt = LengthZig.ValueInt;
            _ZZ.Save();
            _ZZ.Reload();
        }

        //Lists of variable
        Line _lineInclinedOnPrimeChart;

        List<decimal> zzHighClear = new List<decimal>();

        List<int> zzHighClearI = new List<int>();

        List<decimal> PointEndList = new List<decimal>();

        ////////////////////////////
        // LOGIC
        ////////////////////////////
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {

            // If the robot is turned off, exit the event handler
            if (Regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < LengthZig.ValueInt + 50)
            {
                return;
            }


            // Charts Points and Line Methods call
            PointH1();
            PointH2(candles);
            PointH3(candles);
            ChartAlertLine();

            List<Position> openPositions = _tab.PositionsOpenAll;


            // If there are positions, then go to the position closing method
            if (openPositions != null && openPositions.Count != 0)
            {
                LogicClosePosition(candles);
            }

            // If the position closing mode, then exit the method
            if (Regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            // Check of OutOfRange
            if (candles.Count < 100)
            {
                return;
            }

            //If there are no positions, then go to the position opening method
            if (openPositions == null ||
                openPositions.Count == 0)
            {
                LogicOpenPosition(candles);
            }


        }

        // TradeLine Method
        void ChartAlertLine()
        {
            List<Candle> candles = _tab.CandlesFinishedOnly;

            List<decimal> zzHigh = _ZZ.DataSeries[2].Values;

            // Adding ZigZag HighPoints and Index to Lists without Zero Elements
            for (int i = 0; i <= zzHigh.Count - 1; i++)
            {
                if (zzHigh[i] != 0)
                {
                    zzHighClear.Add(zzHigh[i]);
                    zzHighClearI.Add(i);
                }
            }

            // Check for OutOfRange
            if (zzHighClear.Count <= 6)
            {
                return;
            }

            // HighPoints and Indexes from Lists
            decimal zzHighClearV2 = zzHighClear[zzHighClear.Count - 2];
            int zzHighClearIi2 = zzHighClearI[zzHighClearI.Count - 2];
            decimal zzHighClearV3 = zzHighClear[zzHighClear.Count - 3];
            int zzHighClearIi3 = zzHighClearI[zzHighClearI.Count - 3];
            decimal zzHighClearV4 = zzHighClear[zzHighClear.Count - 4];
            int zzHighClearIi4 = zzHighClearI[zzHighClearI.Count - 4];
            decimal zzHighClearV5 = zzHighClear[zzHighClear.Count - 5];
            int zzHighClearIi5 = zzHighClearI[zzHighClearI.Count - 5];

            if (zzHighClearIi2 == zzHighClearIi3)
            {
                return;
            }

            if (zzHighClearV3 < zzHighClearV2)
            {
                zzHighClearV3 = zzHighClearV4;
                zzHighClearIi3 = zzHighClearIi4;
            }

            else if (zzHighClearIi4 < zzHighClearV2)
            {
                zzHighClearV3 = zzHighClearV5;
                zzHighClearIi3 = zzHighClearIi5;
            }


            // 2 calculate indicator movement per candlestick on this TF
            // 2 рассчитываем движение индикатора за свечу на данном ТФ

            decimal stepCorner; // how long our line goes by candle //сколько наша линия проходит за свечку

            stepCorner = (zzHighClearV3 - zzHighClearV2) / (zzHighClearIi3 - zzHighClearIi2 + 1);
            // 3 now build an array of line values parallel to candlestick array
            // 3 теперь строим массив значений линии параллельный свечному массиву

            decimal point = zzHighClearV2;

            for (int i = zzHighClearIi2 + 1; i < candles.Count; i++)
            {
                // running ahead of array.
                // бежим вперёд по массиву
                //lineDecimals[i] = point;
                point += stepCorner;
                PointEndList.Add(point);
            }

            decimal endPoint1 = PointEndList[PointEndList.Count - 1];


            // Line on Chart
            Line line = new Line("Inclined line", "Prime");

            line.ValueYStart = zzHighClearV3;
            line.TimeStart = candles[zzHighClearIi3].TimeStart;

            line.ValueYEnd = endPoint1;
            line.TimeEnd = candles[candles.Count - 1].TimeStart;

            line.Color = Color.Bisque;
            line.LineWidth = 2; // Толщина линии

            //line.Label = "Some label on Line Inclined";

            _tab.SetChartElement(line);

            _lineInclinedOnPrimeChart = line;

            if (_lineInclinedOnPrimeChart != null)
            {
                _lineInclinedOnPrimeChart.TimeEnd = candles[candles.Count - 1].TimeStart;
                _lineInclinedOnPrimeChart.Refresh();
            }
        }

        // ChartPoints of Highs
        void PointH1()
        {
            if (zzHighClear.Count <= 7)
            {
                return;
            }

            List<Candle> candles = _tab.CandlesFinishedOnly;

            decimal zzHighClearV1 = zzHighClear[zzHighClear.Count - 1];
            int zzHighClearIi1 = zzHighClearI[zzHighClearI.Count - 1];



            PointElement point = new PointElement("PointH1", "Prime");

            point.Y = zzHighClearV1;
            point.TimePoint = candles[zzHighClearIi1].TimeStart;

            point.Color = Color.Blue;
            point.Style = MarkerStyle.Star4;
            point.Size = 12;

            _tab.SetChartElement(point);
        }

        void PointH2(List<Candle> candles)
        {
            if (zzHighClear.Count <= 7)
            {
                return;
            }

            decimal zzHighClearV2 = zzHighClear[zzHighClear.Count - 2];
            int zzHighClearIi2 = zzHighClearI[zzHighClearI.Count - 2];

            PointElement point = new PointElement("PointH2", "Prime");

            point.Y = zzHighClearV2;
            point.TimePoint = candles[zzHighClearIi2].TimeStart;

            point.Color = Color.Yellow;
            point.Style = MarkerStyle.Star4;
            point.Size = 12;

            _tab.SetChartElement(point);
        }

        void PointH3(List<Candle> candles)
        {
            if (zzHighClear.Count <= 7)
            {
                return;
            }

            decimal zzHighClearV3 = zzHighClear[zzHighClear.Count - 3];
            int zzHighClearIi3 = zzHighClearI[zzHighClearI.Count - 3];

            PointElement point = new PointElement("Point1H4", "Prime");

            point.Y = zzHighClearV3;
            point.TimePoint = candles[zzHighClearIi3].TimeStart;

            point.Color = Color.Red;
            point.Style = MarkerStyle.Star4;
            point.Size = 12;

            _tab.SetChartElement(point);
        }

        // Opening logic
        void LogicOpenPosition(List<Candle> candles)
        {
            // The last value of the indicators
            Candle lastCandle = candles[candles.Count - 1];
            Candle lastCandleMinus1 = candles[candles.Count - 5];

            if (PointEndList.Count == 0)
            {
                return;
            }

            decimal endPoint1 = PointEndList[PointEndList.Count - 1];

            _signalBuy = lastCandle.Close > endPoint1 && lastCandleMinus1.Close < endPoint1;

            if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
            {
                if (_signalBuy)
                {
                    _tab.BuyAtMarket(1);
                }
            }

        }

        void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            Position pos = openPositions[0];

            // The last value of the indicators
            Candle lastCandle = candles[candles.Count - 1];


            _signalBuyClose = lastCandle.Low > candles[candles.Count - 2].Low;


            if (pos.StopOrderIsActiv == true)
            {
                return;
            }

            if (pos.Direction == Side.Buy)
            {
                decimal priceStop = pos.EntryPrice - pos.EntryPrice * (StopPercent.ValueDecimal / 100);
                decimal priceProfit = pos.EntryPrice + pos.EntryPrice * (ProfitPercent.ValueDecimal / 100);
                _tab.CloseAtStopMarket(pos, priceStop);
                _tab.CloseAtProfitMarket(pos, priceProfit);

                if (_signalBuyClose)
                {
                    _tab.CloseAtMarket(pos, lastCandle.Close);
                }
            }

            else
            {
                decimal priceStop = pos.EntryPrice + pos.EntryPrice * (StopPercent.ValueDecimal / 100);
                decimal priceProfit = pos.EntryPrice - pos.EntryPrice * (ProfitPercent.ValueDecimal / 100);
                _tab.CloseAtStopMarket(pos, priceStop);
                _tab.CloseAtProfitMarket(pos, priceProfit);

            }
        }
    }
}