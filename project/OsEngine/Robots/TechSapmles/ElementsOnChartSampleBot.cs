using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using System.Drawing;
using OsEngine.Charts.CandleChart.Elements;


namespace OsEngine.Robots.TechSapmles
{
    [Bot("ElementsOnChartSampleBot")]
    public class ElementsOnChartSampleBot : BotPanel
    {
        public ElementsOnChartSampleBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // создаём источник
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // создаём индикатор на второй области чарта (MacdArea)
            _macd = IndicatorsFactory.CreateIndicatorByName("MACD", name + "MacdArea", false);
            _macd = (Aindicator)_tab.CreateCandleIndicator(_macd, "MacdArea");
            _macd.Save();

            // создаём кнопки и подписываемся на событие клика по ним
            _buttonAddPointOnPrimeArea = CreateParameterButton("Точка на чарт");
            _buttonAddPointOnPrimeArea.UserClickOnButtonEvent += _buttonAddPointOnPrimeArea_UserClickOnButtonEvent;

            _buttonAddLineOnPrimeArea = CreateParameterButton("Линия на чарт");
            _buttonAddLineOnPrimeArea.UserClickOnButtonEvent += _buttonAddLineOnPrimeArea_UserClickOnButtonEvent;

            _buttonAddSegmentOnPrimeArea = CreateParameterButton("Отрезок на чарт");
            _buttonAddSegmentOnPrimeArea.UserClickOnButtonEvent += _buttonAddSegmentOnPrimeArea_UserClickOnButtonEvent;

            _buttonAddLineOnSecondArea = CreateParameterButton("Линию на доп область на чарте");
            _buttonAddLineOnSecondArea.UserClickOnButtonEvent += _buttonAddLineOnSecondArea_UserClickOnButtonEvent;

            _buttonAddInclinedLineOnPrimeArea = CreateParameterButton("Наклонная линия на главный чарт");
            _buttonAddInclinedLineOnPrimeArea.UserClickOnButtonEvent += _buttonAddInclinedLineOnPrimeArea_UserClickOnButtonEvent;

        }


        public override string GetNameStrategyType()
        {
            return "ElementsOnChartSampleBot";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        BotTabSimple _tab;

        private Aindicator _macd;

        StrategyParameterButton _buttonAddPointOnPrimeArea;

        StrategyParameterButton _buttonAddLineOnPrimeArea;

        StrategyParameterButton _buttonAddSegmentOnPrimeArea;

        StrategyParameterButton _buttonAddLineOnSecondArea;

        StrategyParameterButton _buttonAddInclinedLineOnPrimeArea;

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // на завершении свечи - нужно обновить время конца линии и обновить линии
            // иначе обновляться линия не будет. По умолчанию - всё отрезки
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

// обработчики для кнопок

        private void _buttonAddPointOnPrimeArea_UserClickOnButtonEvent()
        {
            if (_tab.IsConnected == false)
            {// если источник не готов. Выходим
                return;
            }

            List<Candle> candles = _tab.CandlesFinishedOnly;

            if(candles.Count == 0 ||
                candles.Count < 10)
            {// если свечек слишком мало. Выходим
                return;
            }

            PointElement point = new PointElement("Some label", "Prime");

            point.Y = candles[candles.Count - 2].Close;
            point.TimePoint = candles[candles.Count - 2].TimeStart;
            point.Label = "Some label";
            point.Color = Color.Red;
            point.Style = System.Windows.Forms.DataVisualization.Charting.MarkerStyle.Star4;
            point.Size = 12;

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

            LineHorisontal line = new LineHorisontal("Some line","Prime", false);

            line.Value = candles[candles.Count - 1].Close;
            line.TimeStart = candles[0].TimeStart;
            line.TimeEnd = candles[candles.Count-1].TimeStart;
            line.CanResize = true;
            line.Color = Color.White;
            line.LineWidth = 3; // Толщина линии

            line.Label = "Some label on Line";
            _tab.SetChartElement(line);

            _lineOnPrimeChart = line;
        }

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

            LineHorisontal line = new LineHorisontal("Some segment", "Prime", false);

            line.Value = candles[candles.Count - 5].Close;
            line.TimeStart = candles[candles.Count - 10].TimeStart;
            line.TimeEnd = candles[candles.Count - 5].TimeStart;
            line.Color = Color.Green;
            line.LineWidth = 1; // Толщина линии

            line.Label = "Some label on segment";

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
                                 // второй параметр - имя области на чарте для линии 
            LineHorisontal line = new LineHorisontal("Some line on second area", "MacdArea", false);

            line.Value = _macd.DataSeries[0].Last;
            line.TimeStart = candles[0].TimeStart;
            line.TimeEnd = candles[candles.Count - 1].TimeStart;
            line.Color = Color.White;
            line.LineWidth = 5; // Толщина линии

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

            Line line = new Line("Inclined line", "Prime");

            line.ValueYStart = candles[candles.Count - 11].Close;
            line.TimeStart = candles[candles.Count - 11].TimeStart;

            line.ValueYEnd = candles[candles.Count - 1].Close;
            line.TimeEnd = candles[candles.Count - 1].TimeStart;

            line.Color = Color.Bisque;
            line.LineWidth = 3; // Толщина линии

            line.Label = "Some label on Line Inclined";
            _tab.SetChartElement(line);

            _lineInclinedOnPrimeChart = line;
        }

    }
}