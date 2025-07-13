/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using System.Drawing;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Language;

/* Description
TechSample robot for OsEngine

An example of a robot going short after a false upside breakout.
 */

namespace OsEngine.Robots.TechSamples
{
    [Bot("ElementsOnChartSampleBot")] // We create an attribute so that we don't write anything to the BotFactory
    public class ElementsOnChartSampleBot : BotPanel
    {
        // Simple tab
        BotTabSimple _tab;

        // Buttons
        StrategyParameterButton _buttonAddPointOnPrimeArea;

        StrategyParameterButton _buttonAddLineOnPrimeArea;

        StrategyParameterButton _buttonAddSegmentOnPrimeArea;

        StrategyParameterButton _buttonAddLineOnSecondArea;

        StrategyParameterButton _buttonAddInclinedLineOnPrimeArea;

        StrategyParameterButton _buttonClearAllElementsButton;
        
        // Indicator
        private Aindicator _macd;

        public ElementsOnChartSampleBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // creating a source
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // create an indicator on the second chart area (MacdArea)
            _macd = IndicatorsFactory.CreateIndicatorByName("MACD", name + "MacdArea", false);
            _macd = (Aindicator)_tab.CreateCandleIndicator(_macd, "MacdArea");
            _macd.Save();

            // create buttons and subscribe to the event of clicking on them
            _buttonAddPointOnPrimeArea = CreateParameterButton("Point on prime chart");
            _buttonAddPointOnPrimeArea.UserClickOnButtonEvent += _buttonAddPointOnPrimeArea_UserClickOnButtonEvent;

            _buttonAddLineOnPrimeArea = CreateParameterButton("Line on prime chart");
            _buttonAddLineOnPrimeArea.UserClickOnButtonEvent += _buttonAddLineOnPrimeArea_UserClickOnButtonEvent;

            _buttonAddSegmentOnPrimeArea = CreateParameterButton("A segment on prime chart");
            _buttonAddSegmentOnPrimeArea.UserClickOnButtonEvent += _buttonAddSegmentOnPrimeArea_UserClickOnButtonEvent;

            _buttonAddLineOnSecondArea = CreateParameterButton("The line to the extra area on the chart");
            _buttonAddLineOnSecondArea.UserClickOnButtonEvent += _buttonAddLineOnSecondArea_UserClickOnButtonEvent;

            _buttonAddInclinedLineOnPrimeArea = CreateParameterButton("The slanted line to the main chart");
            _buttonAddInclinedLineOnPrimeArea.UserClickOnButtonEvent += _buttonAddInclinedLineOnPrimeArea_UserClickOnButtonEvent;

            _buttonClearAllElementsButton = CreateParameterButton("Remove all elements");
            _buttonClearAllElementsButton.UserClickOnButtonEvent += _buttonClearAllElementsButton_UserClickOnButtonEvent;

            Description = OsLocalization.Description.DescriptionLabel105;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "ElementsOnChartSampleBot";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Logic
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // at the end of the candle, you need to update the end time of the line and refresh the lines
            // otherwise the line will not be updated. By default, all segments
            if (_lineOnPrimeChart != null)
            {
                _lineOnPrimeChart.TimeEnd = candles[candles.Count - 1].TimeStart;
                _lineOnPrimeChart.Refresh();
            }

            if (_lineOnSecondChart != null)
            {
                _lineOnSecondChart.TimeEnd = candles[candles.Count - 1].TimeStart;
                _lineOnSecondChart.Refresh();
            }
        }

        // button handlers

        PointElement _point;

        private void _buttonAddPointOnPrimeArea_UserClickOnButtonEvent()
        {
            if (_tab.IsConnected == false)
            {// if the source isn't ready. Go out
                return;
            }

            List<Candle> candles = _tab.CandlesFinishedOnly;

            if(candles.Count == 0 ||
                candles.Count < 10)
            {// if there are too few candles. Go out
                return;
            }

            if (_point != null)
            {
                _tab.DeleteChartElement(_point);
            }

            PointElement point = new PointElement("Some label", "Prime");

            point.Y = candles[candles.Count - 2].Close;
            point.TimePoint = candles[candles.Count - 2].TimeStart;
            point.Label = "Some label";
            point.Font = new Font("Arial", 10);
            point.LabelTextColor = Color.White;
            point.LabelBackColor = Color.Blue;
            point.Color = Color.Red;
            point.Style = System.Windows.Forms.DataVisualization.Charting.MarkerStyle.Star4;
            point.Size = 12;

            _point = point;

            _tab.SetChartElement(point);
        }

        LineHorisontal _lineOnPrimeChart;

        private void _buttonAddLineOnPrimeArea_UserClickOnButtonEvent()
        {
            if (_tab.IsConnected == false)
            {
                return;
            }

            List<Candle> candles = _tab.CandlesFinishedOnly;

            if (candles.Count == 0 ||
                candles.Count < 10)
            {
                return;
            }

            if (_lineOnPrimeChart != null)
            {
                _tab.DeleteChartElement(_lineOnPrimeChart);
            }

            LineHorisontal line = new LineHorisontal("Some line","Prime", false);

            line.Value = candles[candles.Count - 1].Close;
            line.TimeStart = candles[0].TimeStart;
            line.TimeEnd = candles[candles.Count-1].TimeStart;
            line.CanResize = true;
            line.Color = Color.White;
            line.LineWidth = 3; // line thickness

            line.Label = "Some label on Line";
            line.Font = new Font("Arial", 10);
            line.LabelTextColor = Color.White;
            line.LabelBackColor = Color.Green;

            _tab.SetChartElement(line);

            _lineOnPrimeChart = line;
        }

        LineHorisontal _lineSegment;

        private void _buttonAddSegmentOnPrimeArea_UserClickOnButtonEvent()
        {
            if (_tab.IsConnected == false)
            {
                return;
            }

            List<Candle> candles = _tab.CandlesFinishedOnly;

            if (candles.Count == 0 ||
                candles.Count < 10)
            {
                return;
            }

            if (_lineSegment != null)
            {
                _tab.DeleteChartElement(_lineSegment);
            }

            LineHorisontal line = new LineHorisontal("Some segment", "Prime", false);

            line.Value = candles[candles.Count - 5].Close;
            line.TimeStart = candles[candles.Count - 10].TimeStart;
            line.TimeEnd = candles[candles.Count - 5].TimeStart;
            line.Color = Color.Green;
            line.LineWidth = 1; // line thickness

            line.Label = "Some label on segment";

            _lineSegment = line;

            _tab.SetChartElement(line);
        }

        LineHorisontal _lineOnSecondChart;

        private void _buttonAddLineOnSecondArea_UserClickOnButtonEvent()
        {
            if (_tab.IsConnected == false)
            {
                return;
            }

            List<Candle> candles = _tab.CandlesFinishedOnly;

            if (candles.Count == 0 ||
                candles.Count < 10)
            {
                return;
            }

            if (_lineOnSecondChart != null)
            {
                _tab.DeleteChartElement(_lineOnSecondChart);
            }

            // second parameter -  имя области на графике для линии / name of the area on the chart for the line
            LineHorisontal line = new LineHorisontal("Some line on second area", "MacdArea", false);

            line.Value = _macd.DataSeries[0].Last;
            line.TimeStart = candles[0].TimeStart;
            line.TimeEnd = candles[candles.Count - 1].TimeStart;
            line.Color = Color.White;
            line.LineWidth = 5; // Толщина линии / line thickness

            line.Label = "Some label on second chart";
            _tab.SetChartElement(line);

            _lineOnSecondChart = line;
        }

        Line _lineInclinedOnPrimeChart;

        private void _buttonAddInclinedLineOnPrimeArea_UserClickOnButtonEvent()
        {
            if (_tab.IsConnected == false)
            {
                return;
            }

            List<Candle> candles = _tab.CandlesFinishedOnly;

            if (candles.Count == 0 ||
                candles.Count < 12)
            {
                return;
            }

            if (_lineInclinedOnPrimeChart != null)
            {
                _tab.DeleteChartElement(_lineInclinedOnPrimeChart);
            }

            Line line = new Line("Inclined line", "Prime");

            line.ValueYStart = candles[candles.Count - 11].Close;
            line.TimeStart = candles[candles.Count - 11].TimeStart;

            line.ValueYEnd = candles[candles.Count - 1].Close;
            line.TimeEnd = candles[candles.Count - 1].TimeStart;

            line.Color = Color.Bisque;
            line.LineWidth = 3; // Толщина линии / line thickness

            line.Label = "Some label on Line Inclined";
            _tab.SetChartElement(line);

            _lineInclinedOnPrimeChart = line;
        }

        private void _buttonClearAllElementsButton_UserClickOnButtonEvent()
        {
            _tab.DeleteAllChartElement();
            _lineInclinedOnPrimeChart = null;
            _lineOnSecondChart = null;
            _lineSegment = null;
            _lineOnPrimeChart = null;
            _point = null;
        }
    }
}