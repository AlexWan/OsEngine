/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace OsEngine.Charts.CandleChart.Indicators
{

    /// <summary>
    /// LRC. Linear regression curve
    /// LRC. Linear regression curve. Кривая линейной регрессии
    /// </summary>
    public class LinearRegressionCurve : IIndicator
    {

        /// <summary>
        /// constructor with parameters. Indicator will save settings
        /// конструктор с параметрами. Индикатор будет сохранять настройки
        /// </summary>
        /// <param name="uniqName">unique name/уникальное имя</param>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public LinearRegressionCurve(string uniqName, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorChartPaintType.Line;
            TypePointsToSearch = PriceTypePoints.Close;
            ColorBase = Color.DeepSkyBlue;
            Lenght = 30;
            Lag = 0;
            PaintOn = true;
            CanDelete = canDelete;
            Load();
        }

        /// <summary>
        /// constructor without parameters.Indicator will not saved/конструктор без параметров. Индикатор не будет сохраняться
        /// used ONLY to create composite indicators/используется ТОЛЬКО для создания составных индикаторов
        /// Don't use it from robot creation layer/не используйте его из слоя создания роботов!
        /// </summary>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public LinearRegressionCurve(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();
            TypeIndicator = IndicatorChartPaintType.Line;
            TypePointsToSearch = PriceTypePoints.Close;
            ColorBase = Color.DeepSkyBlue;
            Lenght = 12;
            Lag = 0;
            PaintOn = true;
            CanDelete = canDelete;
        }

        /// <summary>
        /// all indicator values
        /// все значения индикатора
        /// </summary>
        List<List<decimal>> IIndicator.ValuesToChart
        {
            get
            {
                List<List<decimal>> list = new List<List<decimal>>();
                list.Add(Values);
                return list;
            }
        }

        /// <summary>
        /// indicator colors
        /// цвета для индикатора
        /// </summary>
        List<Color> IIndicator.Colors
        {
            get
            {
                List<Color> colors = new List<Color>();
                colors.Add(ColorBase);
                return colors;
            }

        }

        /// <summary>
        /// whether indicator can be removed from chart. This is necessary so that robots can't be removed /можно ли удалить индикатор с графика. Это нужно для того чтобы у роботов нельзя было удалить 
        /// indicators he needs in trading/индикаторы которые ему нужны в торговле
        /// </summary>
        public bool CanDelete { get; set; }

        /// <summary>
        /// indicator drawing type
        /// тип прорисовки индикатора
        /// </summary>
        public IndicatorChartPaintType TypeIndicator { get; set; }

        /// <summary>
        /// at what point average will be built. By Open Close...
        /// по какой точке средняя будет строиться. По Open Close ...
        /// </summary>
        public PriceTypePoints TypePointsToSearch;

        /// <summary>
        /// name of data series on which indicator will be drawn
        /// имя серии данных на которой индикатор прорисовывается
        /// </summary>
        public string NameSeries { get; set; }

        /// <summary>
        /// name of data area where indicator will be drawn
        /// имя области данных на которой индикатор прорисовывается
        /// </summary>
        public string NameArea { get; set; }

        /// <summary>
        /// moving average
        /// скользящая средняя
        /// </summary>
        public List<decimal> Values { get; set; }

        /// <summary>
        /// unique indicator name
        /// уникальное имя индикатора
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// period length to calculate indicator
        /// длинна рассчёта индикатора
        /// </summary>
        public int Lenght { get; set; }

        /// <summary>
        /// lag in bars qty to calculate indicator
        /// задержка расчёта индикатора
        /// </summary>
        public int Lag { get; set; }

        /// <summary>
        /// ma color
        /// цвет машки
        /// </summary>
        public Color ColorBase { get; set; }

        /// <summary>
        /// is indicator tracing enabled
        /// включена ли прорисовка серии на чарте
        /// </summary>
        public bool PaintOn { get; set; }

        /// <summary>
        /// upload settings from file
        /// загрузить настройки из файла
        /// </summary>
        public void Load()
        {
            if (!File.Exists(@"Engine\" + Name + @".txt"))
            {
                return;
            }
            try
            {

                using (StreamReader reader = new StreamReader(@"Engine\" + Name + @".txt"))
                {
                    ColorBase = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    Lenght = Convert.ToInt32(reader.ReadLine());
                    Lag = Convert.ToInt32(reader.ReadLine());
                    PaintOn = Convert.ToBoolean(reader.ReadLine());

                    Enum.TryParse(reader.ReadLine(), true, out TypePointsToSearch);

                    reader.ReadLine();

                    reader.Close();
                }


            }
            catch (Exception)
            {
                // send to log
                // отправить в лог
            }
        }

        /// <summary>
        /// upload settings from file
        /// сохранить настройки в файл
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @".txt", false))
                {
                    writer.WriteLine(ColorBase.ToArgb());
                    writer.WriteLine(Lenght);
                    writer.WriteLine(Lag);
                    writer.WriteLine(PaintOn);
                    writer.WriteLine(TypePointsToSearch);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // send to log
                // отправить в лог
            }
        }

        /// <summary>
        /// delete file with settings
        /// удалить файл с настройками
        /// </summary>
        public void Delete()
        {
            if (File.Exists(@"Engine\" + Name + @".txt"))
            {
                File.Delete(@"Engine\" + Name + @".txt");
            }
        }

        /// <summary>
        /// delete data
        /// удалить данные
        /// </summary>
        public void Clear()
        {
            if (Values != null)
            {
                Values.Clear();
            }
            _myCandles = null;
        }

        /// <summary>
        /// display settings window
        /// показать окно с настройками
        /// </summary>
        public void ShowDialog()
        {
            LinearRegressionCurveUi ui = new LinearRegressionCurveUi(this);
            ui.ShowDialog();

            if (ui.IsChange)
            {
                Reload();
            }
        }

        /// <summary>
        /// reload indicator
        /// перезагрузить индикатор
        /// </summary>
        public void Reload()
        {
            if (_myValues != null)
            {
                ProcessAll(_myValues);
            }
            if (_myCandles != null)
            {
                ProcessAll(_myCandles);
            }

            if (NeadToReloadEvent != null)
            {
                NeadToReloadEvent(this);
            }
        }

        /// <summary>
        /// it's necessary to redraw the indicator on the chart
        /// необходимо перерисовать индикатор на графике
        /// </summary>
        public event Action<IIndicator> NeadToReloadEvent;
        // calculating using candles
        // расчёт на свечках

        /// <summary>
        /// candles to calculate indicator
        /// свечи по которым строится индикатор
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// to upload new candles
        /// прогрузить новыми свечками
        /// </summary>
        public void Process(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }
            _myCandles = candles;
            if (Values != null &&
                Values.Count + 1 == candles.Count)
            {
                ProcessOne(candles);
            }
            else if (Values != null &&
                     Values.Count == candles.Count)
            {
                ProcessLast(candles);
            }
            else
            {
                ProcessAll(candles);
            }
        }

        /// <summary>
        /// overload last candle
        /// прогрузить только последнюю свечку
        /// </summary>
        private void ProcessOne(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }
            if (Values == null)
            {
                Values = new List<decimal>();
            }

            Values.Add(CalcRegression(candles, candles.Count - 1));
        }

        /// <summary>
        /// to upload from the beginning
        /// прогрузить с самого начала
        /// </summary>
        private void ProcessAll(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }
            Values = new List<decimal>();

            for (int i = 0; i < candles.Count; i++)
            {
                Values.Add(CalcRegression(candles, i));
            }
        }

        /// <summary>
        /// overload last value
        /// перегрузить последнее значение
        /// </summary>
        private void ProcessLast(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }

            Values[Values.Count - 1] = CalcRegression(candles, candles.Count - 1);
        }

        /// <summary>
        /// take indicator value by index
        /// взять значение индикатора по индексу
        /// </summary>
        private decimal CalcRegression(List<Candle> candles, int index)
        {
            if (index - Lenght <= 0)
            {
                return 0;
            }

            decimal[] xVals = new decimal[Lenght];
            decimal[] yVals = new decimal[Lenght];

            int counter = 0;

            for (int i = index; i > index - Lenght; i--)
            {
                xVals[Lenght - 1 - counter] = counter + 1;
                yVals[Lenght - 1 - counter] = GetPoint(candles, i);
                counter++;
            }

            return LinearRegression(xVals, yVals);
        }

        private decimal LinearRegression(decimal[] xVals, decimal[] yVals)
        {
            decimal linreg = 0;

            decimal sumOfX = 0;
            decimal sumOfY = 0;
            decimal sumOfXSq = 0;
            decimal sumOfYSq = 0;
            decimal sumCodeviates = 0;

            for (var i = 0; i < xVals.Length; i++)
            {
                var x = xVals[i];
                var y = yVals[i];
                sumCodeviates += x * y;
                sumOfX += x;
                sumOfY += y;
                sumOfXSq += x * x;
                sumOfYSq += y * y;
            }

            var count = xVals.Length;
            var ssX = sumOfXSq - ((sumOfX * sumOfX) / count);
            var ssY = sumOfYSq - ((sumOfY * sumOfY) / count);

            var rNumerator = (count * sumCodeviates) - (sumOfX * sumOfY);
            var rDenom = (count * sumOfXSq - (sumOfX * sumOfX)) * (count * sumOfYSq - (sumOfY * sumOfY));
            var sCo = sumCodeviates - ((sumOfX * sumOfY) / count);

            var meanX = sumOfX / count;
            var meanY = sumOfY / count;
            var dblR = (double)rNumerator / Math.Sqrt((double)rDenom);

            var rSquared = dblR * dblR;
            var yIntercept = meanY - ((sCo / ssX) * meanX);
            var slope = sCo / ssX;

            linreg = yIntercept + slope * (1 - Lag);

            return Math.Round(linreg, 2);
        }

        /// <summary>
        /// take values of point for calculating data
        /// взять значения точки для рассчёта данных
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        /// <param name="index">index/индекс</param>
        /// <returns>index value/значение индикатора по индексу</returns>
        private decimal GetPoint(List<Candle> candles, int index)
        {

            if (TypePointsToSearch == PriceTypePoints.Close)
            {
                return candles[index].Close;
            }
            else if (TypePointsToSearch == PriceTypePoints.High)
            {
                return candles[index].High;
            }
            else if (TypePointsToSearch == PriceTypePoints.Low)
            {
                return candles[index].Low;
            }
            else if (TypePointsToSearch == PriceTypePoints.Open)
            {
                return candles[index].Open;
            }
            else if (TypePointsToSearch == PriceTypePoints.Median)
            {
                return (candles[index].High + candles[index].Low) / 2;
            }
            else if (TypePointsToSearch == PriceTypePoints.Typical)
            {
                return (candles[index].High + candles[index].Low + candles[index].Close) / 3;
            }
            return 0;
        }


        // calculation on decimal arrays. This blog mainly is for other indicators
        // рассчёт на массивах decimal. Этот блог для других индикаторов в основном

        /// <summary>
        /// candles used to build indicator
        /// свечи по которым строится индикатор
        /// </summary>
        private List<decimal> _myValues;

        /// <summary>
        /// to upload from the beginning
        /// прогрузить новыми свечками
        /// </summary>
        public void Process(List<decimal> values)
        {
            if (values == null)
            {
                return;
            }
            _myValues = values;
            if (Values != null &&
                Values.Count + 1 == values.Count)
            {
                ProcessOne(values);
            }
            else if (Values != null &&
                     Values.Count == values.Count)
            {
                ProcessLast(values);
            }
            else
            {
                ProcessAll(values);
            }
        }

        /// <summary>
        /// overload last value
        /// прогрузить только последнюю свечку
        /// </summary>
        private void ProcessOne(List<decimal> values)
        {
            if (values == null)
            {
                return;
            }
            if (Values == null)
            {
                Values = new List<decimal>();
            }

            Values.Add(GetRegressionValue(values, values.Count - 1));

        }

        /// <summary>
        /// to upload from the beginning
        /// прогрузить с самого начала
        /// </summary>
        private void ProcessAll(List<decimal> values)
        {
            if (values == null)
            {
                return;
            }
            Values = new List<decimal>();


            for (int i = 0; i < values.Count; i++)
            {
                Values.Add(GetRegressionValue(values, i));
            }
        }

        /// <summary>
        /// overload last value
        /// перегрузить последнее значение
        /// </summary>
        private void ProcessLast(List<decimal> values)
        {
            if (values == null)
            {
                return;
            }

            Values[Values.Count - 1] = GetRegressionValue(values, values.Count - 1);
        }

        /// <summary>
        /// take indicator value by index
        /// взять значение индикаторм по индексу
        /// </summary>
        private decimal GetRegressionValue(List<decimal> values, int index)
        {
            if (index - Lenght <= 0)
            {
                return 0;
            }

            decimal[] xVals = new decimal[Lenght];
            decimal[] yVals = new decimal[Lenght];

            int counter = 0;

            for (int i = index; i > index - Lenght; i--)
            {
                xVals[Lenght - 1 - counter] = counter + 1;
                yVals[Lenght - 1 - counter] = values[i];
                counter++;
            }

            var linreg = LinearRegression(xVals, yVals);

            return Math.Round(linreg, 8);
        }

    }
}
