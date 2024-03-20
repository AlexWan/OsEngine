using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms.VisualStyles;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

using OsEngine.Charts.CandleChart.Elements;


namespace OsEngine.Robots._MyRobots
{
    [Bot("FakeOutExample")]
    internal class FakeOutExample : BotPanel
    {
        private BotTabSimple _tab;

// Basic Settings
        private StrategyParameterString Regime;

        public StrategyParameterInt CandlesForPcLevel;

        public StrategyParameterInt MinutsForExit;

        private StrategyParameterTimeOfDay StartTradeTime;
        private StrategyParameterTimeOfDay EndTradeTime;


        // Indicator setting 
        public StrategyParameterInt PeriodPC;
        
// Indicator
        Aindicator _PC;

// The last value of the indicators
        //private decimal _lastPcUp;
        //private decimal _lastPcDown;
        //private List<int> index = new List<int>();


        private bool _signalBuy;
        private bool _signalSell;
        private bool _signalSellClose;
        private decimal _hLevelOne;
        
        
        //private decimal _lastPcUpMinusOne;


        ////////////////////////////////////////////////
        //MAIN BASE
        ////////////////////////////////////////////////
        public FakeOutExample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

 // Basic setting
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");

            MinutsForExit = CreateParameter("MinutsForExit", 75, 5, 240, 1, "Base");

            CandlesForPcLevel = CreateParameter("CandlesForPcLevel", 10, 3, 24, 1, "Base");

            StartTradeTime = CreateParameterTimeOfDay("Start Trade Time", 9, 15, 0, 0, "Base");
            EndTradeTime = CreateParameterTimeOfDay("End Trade Time", 23, 40, 0, 0, "Base");
           

            // Setting indicator 
            PeriodPC = CreateParameter("PeriodPC", 10, 5, 40, 1, "Base");


            // Create indicator PC
            _PC = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PC", false);
            _PC = (Aindicator)_tab.CreateCandleIndicator(_PC, "Prime");
            ((IndicatorParameterInt)_PC.Parameters[0]).ValueInt = PeriodPC.ValueInt;
            ((IndicatorParameterInt)_PC.Parameters[1]).ValueInt = PeriodPC.ValueInt;
            _PC.Save();


            // Events           
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            //_tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;

            ParametrsChangeByUser += FakeOutExample_ParametrsChangeByUser;

            //_tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
        }
        
        public override string GetNameStrategyType()
        {
            return "FakeOutExample";
        }
        public override void ShowIndividualSettingsDialog()
        {

        }

        //Indicator Update event
        private void FakeOutExample_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_PC.Parameters[0]).ValueInt = PeriodPC.ValueInt;
            ((IndicatorParameterInt)_PC.Parameters[1]).ValueInt = PeriodPC.ValueInt;
            _PC.Save();
            _PC.Reload();
        }



        //Line
        LineHorisontal _lineOnPrimeChart;

        //Lists of Extremums

        List<decimal> methodsH = new List<decimal>();

        List<decimal> localHighV = new List<decimal>();
        List<int> localHighI = new List<int>();



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
            if (candles.Count < PeriodPC.ValueInt + 30)
            {
                return;
            }

            if (CandlesForPcLevel.ValueInt == 0)
            {
                return;
            }
            
            // Method for Local Extremums
            LocalExtremums(candles);
            
            // Levels from PC Method Initialization 

            LevelsFromPc();
            

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
            //If there are no positions, then go to the position opening method
            if (openPositions == null ||
                openPositions.Count == 0)
            {
                LogicOpenPosition(candles);
            }

            //Elements on chart  
            LineH1(); // Local High Level line
            PointH1(); // Local High Marker


            // Lines refresh
            if (_lineOnPrimeChart != null)
            {
                _lineOnPrimeChart.TimeEnd = candles[candles.Count - 1].TimeStart;
                _lineOnPrimeChart.Refresh();
            }

        }

        private void LevelsFromPc()
        {
            List<decimal> pcHigh = _PC.DataSeries[0].Values;

            var t = CandlesForPcLevel.ValueInt;

            if (pcHigh.Count - CandlesForPcLevel.ValueInt <= 1)
            {
                return;
            }

            // PC value 
            decimal LastPcH = pcHigh[pcHigh.Count - 1];
            decimal LastPcHPlus = pcHigh[pcHigh.Count - CandlesForPcLevel.ValueInt];

            // If Last PC value and PC value minus variable equal - adding to List
            if (LastPcH == LastPcHPlus)
            {
                if (methodsH.Count < 1)
                {
                    methodsH.Add(LastPcH);
                }

                else
                {
                    if (LastPcH != methodsH[methodsH.Count - 1])
                    {
                        methodsH.Add(LastPcH);
                    }
                    else
                    {
                        return;
                    }
                }

                try
                {
                    if (methodsH.Count > 1)
                    {
                        _hLevelOne = methodsH[methodsH.Count - 1];
                    }
                }
                catch
                {

                }
            }
        }

        private void LocalExtremums(List<Candle> candles)
        {
            localHighV.Clear();
            localHighI.Clear();

            int _candlesCountMinus = candles.Count - CandlesForPcLevel.ValueInt;

            decimal _localHighV = candles[candles.Count - 1].High;

            int j = 1;

            for (int i = candles.Count - 1; i > _candlesCountMinus - 1; i--)
            {
                if (candles[i].High > _localHighV)
                {
                    _localHighV = candles[i].High;
                    j = i;
                }
            }

            if (j != 1)
            {
                localHighI.Add(j);
            }

            else
            {
                localHighI.Add(candles.Count - 1);
            }

            localHighV.Add(_localHighV);
        }
        
        // Chart Visual Elements

        #region Lines

        private void LineH1() // Local High Level line
        {
            List<Candle> candles = _tab.CandlesFinishedOnly;

            if (candles.Count == 0 ||
                candles.Count < 31)
            {
                return;
            }

            LineHorisontal line = new LineHorisontal("Line2", "Prime", false);

            line.Value = _hLevelOne;
            line.TimeStart = candles[0].TimeStart;
            line.TimeEnd = candles[candles.Count - 1].TimeStart;
            line.Color = Color.Green;
            line.LineWidth = 2; // Толщина линии

            _tab.SetChartElement(line);

            _lineOnPrimeChart = line;

            if (_lineOnPrimeChart != null)
            {
                _lineOnPrimeChart.TimeEnd = candles[candles.Count - 1].TimeStart;
                _lineOnPrimeChart.Refresh();
            }
        }

        
        private void PointH1() //Local High Marker
        {
            List<Candle> candles = _tab.CandlesFinishedOnly;

            decimal _localHighV = localHighV[localHighV.Count - 1];
            int _localHighI = localHighI[localHighI.Count - 1];


            PointElement point = new PointElement("Some label", "Prime");

            point.Y = _localHighV;
            point.TimePoint = candles[_localHighI].TimeStart;

            point.Color = Color.Red;
            point.Style = System.Windows.Forms.DataVisualization.Charting.MarkerStyle.Star4;
            point.Size = 12;

            _tab.SetChartElement(point);
        }

        #endregion

        //Opening logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            // The last value of the indicators
            Candle lastCandle = candles[candles.Count - 1];
            decimal lastCandleClose = lastCandle.Close;
            decimal lastCandleHigh = lastCandle.High;


            decimal _localHighV = localHighV[localHighV.Count - 1];


            //Short
            _signalSell = lastCandle.Close < _hLevelOne &&
                          _localHighV > _hLevelOne;

            
            if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
            {
                if (_signalSell)
                {
                    _tab.SellAtMarket(1);
                }
            }
        }

        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            Position pos = openPositions[0];
            
            
            if (pos.TimeOpen.AddMinutes(MinutsForExit.ValueInt) <= candles[candles.Count - 1].TimeStart)
            {
                _tab.CloseAllAtMarket();
            }
        }
    }
}