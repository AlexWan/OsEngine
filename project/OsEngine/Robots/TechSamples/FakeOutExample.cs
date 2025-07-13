/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Language;

/* Description
TechSample robot for OsEngine

An example of a robot going short after a false upside breakout.
 */

namespace OsEngine.Robots
{
    [Bot("FakeOutExample")] // We create an attribute so that we don't write anything to the BotFactory
    internal class FakeOutExample : BotPanel
    {
        // Simple tab
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _candlesForPcLevel;

        // Indicator setting 
        private StrategyParameterInt _periodPC;

        // Indicator
        private Aindicator _PC;

        // Exit setting
        private StrategyParameterInt _minutsForExit;

        private bool _signalSell;
        private decimal _hLevelOne;

        public FakeOutExample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create tabs
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyClosePosition" }, "Base");
            _candlesForPcLevel = CreateParameter("CandlesForPcLevel", 10, 3, 24, 1, "Base");
           
            // Indicator settings
            _periodPC = CreateParameter("PeriodPC", 10, 5, 40, 1, "Base");
            
            // Exit setting
            _minutsForExit = CreateParameter("MinutsForExit", 75, 5, 240, 1, "Base");

            // Create indicator PC
            _PC = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PC", false);
            _PC = (Aindicator)_tab.CreateCandleIndicator(_PC, "Prime");
            ((IndicatorParameterInt)_PC.Parameters[0]).ValueInt = _periodPC.ValueInt;
            ((IndicatorParameterInt)_PC.Parameters[1]).ValueInt = _periodPC.ValueInt;
            _PC.Save();

            // Subscribe to the candle finished event     
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += FakeOutExample_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel106;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "FakeOutExample";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Indicator Update event
        private void FakeOutExample_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_PC.Parameters[0]).ValueInt = _periodPC.ValueInt;
            ((IndicatorParameterInt)_PC.Parameters[1]).ValueInt = _periodPC.ValueInt;
            _PC.Save();
            _PC.Reload();
        }

        // Line
        LineHorisontal _lineOnPrimeChart;

        // Lists of Extremums

        List<decimal> methodsH = new List<decimal>();

        List<decimal> localHighV = new List<decimal>();

        List<int> localHighI = new List<int>();

        // Logic
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _periodPC.ValueInt + 30)
            {
                return;
            }

            if (_candlesForPcLevel.ValueInt == 0)
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
            if (_regime.ValueString == "OnlyClosePosition")
            {
                return;
            }
            //If there are no positions, then go to the position opening method
            if (openPositions == null || openPositions.Count == 0)
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

        // Level from PriceChannel
        private void LevelsFromPc()
        {
            List<decimal> pcHigh = _PC.DataSeries[0].Values;

            var t = _candlesForPcLevel.ValueInt;

            if (pcHigh.Count - _candlesForPcLevel.ValueInt <= 1)
            {
                return;
            }

            // PC value 
            decimal LastPcH = pcHigh[pcHigh.Count - 1];
            decimal LastPcHPlus = pcHigh[pcHigh.Count - _candlesForPcLevel.ValueInt];

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

        // Local extremums
        private void LocalExtremums(List<Candle> candles)
        {
            localHighV.Clear();
            localHighI.Clear();

            int _candlesCountMinus = candles.Count - _candlesForPcLevel.ValueInt;

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
        
        // Chart Visual Elements //
        
        // Local High Level line
        private void LineH1() 
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
            line.LineWidth = 2; // Line thickness

            _tab.SetChartElement(line);

            _lineOnPrimeChart = line;

            if (_lineOnPrimeChart != null)
            {
                _lineOnPrimeChart.TimeEnd = candles[candles.Count - 1].TimeStart;
                _lineOnPrimeChart.Refresh();
            }
        }
        
        // Local High Marker
        private void PointH1() 
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

        // Opening position logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            // The last value of the indicators
            Candle lastCandle = candles[candles.Count - 1];
            decimal lastCandleClose = lastCandle.Close;
            decimal _localHighV = localHighV[localHighV.Count - 1];

            // Short
            _signalSell = lastCandleClose < _hLevelOne && _localHighV > _hLevelOne;

            if (_signalSell)
            {
                _tab.SellAtMarket(1);
            }
        }

        // Close position logic
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            Position pos = openPositions[0];
            
            if (pos.TimeOpen.AddMinutes(_minutsForExit.ValueInt) <= candles[candles.Count - 1].TimeStart)
            {
                _tab.CloseAllAtMarket();
            }
        }
    }
}