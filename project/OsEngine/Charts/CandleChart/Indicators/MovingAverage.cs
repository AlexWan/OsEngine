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
    /// Тип рассчёта индикаторм Скользящая средняя
    /// </summary>
    public enum MovingAverageTypeCalculation
    {
        /// <summary>
        /// Simple
        /// простой
        /// </summary>
        Simple,

        /// <summary>
        /// Exponential
        /// Экспоненциальный
        /// </summary>
        Exponential,

        /// <summary>
        /// Weighted
        /// Взвешенный
        /// </summary>
        Weighted,

        /// <summary>
        /// Moving designed by Sergey Radchenko
        /// Requires additional control from strategy code
        /// Скользящая разработана Сергеем Радченко.
        /// Требует дополнительного управления из кода стратегии
        /// </summary>
        Radchenko,

        /// <summary>
        /// Kaufman adaptive moving average 
        /// Адаптивная скользящая Кауфмана
        /// </summary>
        Adaptive,

        /// <summary>
        /// VolumeWeighted moving average
        /// взвешенная по объёму
        /// </summary>
        VolumeWeighted,

        /// <summary>
        /// Smoothed Moving Average (SSMA) 
        /// Сглаженное скользящее среднее
        /// </summary>
        Smoofed
    }

    /// <summary>
    /// what price of candle taken when building
    /// какая цена свечи берётся при построении
    /// </summary>
    public enum PriceTypePoints
    {
        /// <summary>
        /// Open
        /// открытие
        /// </summary>
        Open,

        /// <summary>
        /// High
        /// максимум
        /// </summary>
        High,

        /// <summary>
        /// Low
        /// минимум
        /// </summary>
        Low,

        /// <summary>
        /// Close
        /// закрытие
        /// </summary>
        Close,

        /// <summary>
        /// Median. (High + Low) / 2
        /// медиана. (High + Low) / 2
        /// </summary>
        Median,

        /// <summary>
        /// Typical price (High + Low + Close) / 3
        /// типичная цена (High + Low + Close) / 3
        /// </summary>
        Typical
    }

    /// <summary>
    /// MA. Simple Moving Average
    /// MA. Moving Average. Индикатор скользящая средняя
    /// </summary>
    public class MovingAverage : IIndicator
    {

        /// <summary>
        /// constructor with parameters. Indicator will save settings
        /// конструктор с параметрами. Индикатор будет сохранять настройки
        /// </summary>
        /// <param name="uniqName">unique name/уникальное имя</param>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public MovingAverage(string uniqName,bool canDelete)
        {
            Name = uniqName;
            KaufmanFastEma = 2;
            KaufmanSlowEma = 30;
            TypeCalculationAverage = MovingAverageTypeCalculation.Simple;
            TypeIndicator = IndicatorChartPaintType.Line;
            TypePointsToSearch = PriceTypePoints.Close;
            ColorBase = Color.DeepSkyBlue;
            Lenght = 12;
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
        public MovingAverage(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();
            KaufmanFastEma = 2;
            KaufmanSlowEma = 30;
            TypeCalculationAverage = MovingAverageTypeCalculation.Simple;
            TypeIndicator = IndicatorChartPaintType.Line;
            TypePointsToSearch = PriceTypePoints.Close;
            ColorBase = Color.DeepSkyBlue;
            Lenght = 12;
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
        /// type of moving average for indicator calculation
        /// тип скользящей средней
        /// </summary>
        public MovingAverageTypeCalculation TypeCalculationAverage;

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
                    PaintOn = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out TypeCalculationAverage);
                    Enum.TryParse(reader.ReadLine(), true, out TypePointsToSearch);
                    KaufmanFastEma = Convert.ToInt32(reader.ReadLine());
                    KaufmanSlowEma = Convert.ToInt32(reader.ReadLine());
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
                    writer.WriteLine(PaintOn);
                    writer.WriteLine(TypeCalculationAverage);
                    writer.WriteLine(TypePointsToSearch);
                    writer.WriteLine(KaufmanFastEma);
                    writer.WriteLine(KaufmanSlowEma);
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
            MovingAverageUi ui = new MovingAverageUi(this);
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

            if (TypeCalculationAverage == MovingAverageTypeCalculation.Simple)
            {
                Values.Add(GetValueSimple(candles, candles.Count - 1));
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Exponential)
            {
                Values.Add(GetValueExponential(candles, candles.Count - 1));
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Weighted)
            {
                Values.Add(GetValueWeighted(candles, candles.Count - 1));
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Radchenko)
            {
                Values.Add(GetValueRadchenko(Values, candles, candles.Count - 1));
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Adaptive)
            {
                Values.Add(GetValueKaufmanAdaptive(candles, candles.Count - 1));
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.VolumeWeighted)
            {
                Values.Add(GetValueVolumeWeighted(candles, candles.Count - 1));
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Smoofed)
            {
                Values.Add(GetValueSmma(candles, candles.Count - 1));
            }
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
                if (TypeCalculationAverage == MovingAverageTypeCalculation.Simple)
                {
                    Values.Add(GetValueSimple(candles, i));
                }
                if (TypeCalculationAverage == MovingAverageTypeCalculation.Exponential)
                {
                    Values.Add(GetValueExponential(candles, i));
                }
                if (TypeCalculationAverage == MovingAverageTypeCalculation.Weighted)
                {
                    Values.Add(GetValueWeighted(candles, i));
                }
                if (TypeCalculationAverage == MovingAverageTypeCalculation.Radchenko)
                {
                    Values.Add(GetValueRadchenko(Values, candles, i));
                }
                if (TypeCalculationAverage == MovingAverageTypeCalculation.Adaptive)
                {
                    Values.Add(GetValueKaufmanAdaptive(candles, i));
                }
                if (TypeCalculationAverage == MovingAverageTypeCalculation.VolumeWeighted)
                {
                    Values.Add(GetValueVolumeWeighted(candles, i));
                }
                if (TypeCalculationAverage == MovingAverageTypeCalculation.Smoofed)
                {
                    Values.Add(GetValueSmma(candles, i));
                }
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
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Simple)
            {
                Values[Values.Count - 1] = GetValueSimple(candles, candles.Count - 1);
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Exponential)
            {
                Values[Values.Count - 1] = GetValueExponential(candles, candles.Count - 1);
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Weighted)
            {
               Values[Values.Count - 1] = GetValueWeighted(candles, candles.Count - 1);
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Radchenko)
            {
               Values[Values.Count - 1] = GetValueRadchenko(Values, candles, candles.Count - 2);
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Adaptive)
            {
                Values[Values.Count - 1] = GetValueKaufmanAdaptive(candles, candles.Count - 1);
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.VolumeWeighted)
            {
                Values[Values.Count - 1] = GetValueVolumeWeighted(candles, candles.Count - 1);
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Smoofed)
            {
                Values[Values.Count - 1] = GetValueSmma(candles, candles.Count - 1);
            }
        }

        /// <summary>
        /// take indicator value by index
        /// взять значение индикатора по индексу
        /// </summary>
        private decimal GetValueSimple(List<Candle> candles, int index)
        {
            if (index - Lenght <= 0)
            {
                return 0;
            }

            decimal average = 0;

            for (int i = index; i > index - Lenght; i--)
            {
                average += GetPoint(candles,i);
            }

            average = average/Lenght;

            return Math.Round(average, 8);
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

        /// <summary>
        /// exponent
        /// экспонента
        /// </summary>
        private decimal GetValueExponential(List<Candle> candles, int index)
        {
            decimal result = 0;

            if (index == Lenght)
            {
                // it's the first value. Calculate as simple ma
                // это первое значение. Рассчитываем как простую машку
                decimal lastMoving = 0;

                for (int i = index - Lenght +1; i < index + 1; i++)
                {
                    lastMoving += GetPoint(candles, i);
                }

                lastMoving = lastMoving / Lenght;

                result = lastMoving;
            }
            else if (index > Lenght)
            {

                // decimal a = 2.0m / (length * 2 - 0.15m);

                decimal a = Math.Round(2.0m / (Lenght + 1), 8);

                decimal emaLast = Values[index-1];

                decimal p = GetPoint(candles, index);
                //ЕМА(i) = ЕМА(i - 1) + ( К • [ Close(i) - ЕМА (i -1) ] ), 
                result = emaLast + (a * (p - emaLast));
                //result = a*p + (1 - a)*emaLast;
            }

            return Math.Round(result,8);
        }

        /// <summary>
        /// weighted
        /// взвешенная
        /// </summary>
        private decimal GetValueWeighted(List<Candle> candles, int index)
        {
            if (index - Lenght <= 0)
            {
                return 0;
            }
            decimal average = 0;

            int weights = 0;

            for (int i = index, weight = Lenght; i > index - Lenght; i--, weight--)
            {
                average += GetPoint(candles, i) * weight;
                weights += weight;
            }

            average = average / weights;

            return Math.Round(average, 8);

        }

        /// <summary>
        /// radchenko
        /// радченко
        /// </summary>
        private decimal GetValueRadchenko(List<decimal> lastValues, List<Candle> candles, int index)
        {
            if (index - Lenght <= 0)
            {
                return 0;
            }

            decimal average = 0;

            for (int i = index; i > index - Lenght; i--)
            {
                average += GetPoint(candles, i);
            }

            average = average / Lenght;

            int radchenkoFaze = 0; // 0 - nothing/ничего -1 - just down/только вниз +1 just up/только вверх

            if (candles[index].Close > average)
            {
                radchenkoFaze = 1;
            }
            else if (candles[index].Close < average)
            {
                radchenkoFaze = -1;
            }

            if (radchenkoFaze == 0)
            {
                return average;
            }

            if (lastValues == null || lastValues.Count == 0 || lastValues[lastValues.Count - 1] == 0)
            {
                return average;
            }

            if (radchenkoFaze == -1)
            {
                decimal lastPoint = lastValues[lastValues.Count - 1];

                if (average < lastPoint)
                {
                    return average;
                }
                else
                {
                    return lastPoint;
                }
            }

            if (radchenkoFaze == 1)
            {
                decimal lastPoint = lastValues[lastValues.Count - 1];

                if (average > lastPoint)
                {
                    return average;
                }
                else
                {
                    return lastPoint;
                }
            }

            return Math.Round(average, 8);
        }

        /// <summary>
        /// volumeweighted
        /// взвешенная по объёму
        /// </summary>
        private decimal GetValueVolumeWeighted(List<Candle> candles, int index)
        {
            if (index - Lenght <= 0)
            {
                return 0;
            }
            decimal average = 0;

            decimal weights = 0;

            for (int i = index; i > index - Lenght; i--)
            {
                average += GetPoint(candles, i) * candles[i].Volume;
                weights += candles[i].Volume;
            }

            average = average / weights;

            return Math.Round(average, 8);

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

            if (TypeCalculationAverage == MovingAverageTypeCalculation.VolumeWeighted)
            {
                // ...it's not gonna weigh volume of arrays. Put another attribute
                // по объёму взвесить массив данных не выйдет. Ставим другой признак
                TypeCalculationAverage = MovingAverageTypeCalculation.Simple;
            }

            if (TypeCalculationAverage == MovingAverageTypeCalculation.Simple)
            {
                Values.Add(GetValueSimple(values, values.Count - 1));
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Exponential)
            {
                Values.Add(GetValueExponential(values, values.Count - 1));
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Weighted)
            {
                Values.Add(GetValueWeighted(values, values.Count - 1));
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Radchenko)
            {
                Values.Add(GetValueRadchenko(Values, values, values.Count - 1));
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Adaptive)
            {
                Values.Add(GetValueKaufmanAdaptive(values, values.Count - 1));
            }
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
                if (TypeCalculationAverage == MovingAverageTypeCalculation.Simple)
                {
                    Values.Add(GetValueSimple(values, i));
                }
                if (TypeCalculationAverage == MovingAverageTypeCalculation.Exponential)
                {
                    Values.Add(GetValueExponential(values, i));
                }
                if (TypeCalculationAverage == MovingAverageTypeCalculation.Weighted ||
                    TypeCalculationAverage == MovingAverageTypeCalculation.VolumeWeighted)
                {
                    Values.Add(GetValueWeighted(values, i));
                }
                if (TypeCalculationAverage == MovingAverageTypeCalculation.Radchenko)
                {
                    Values.Add(GetValueRadchenko(Values, values, i));
                }
                if (TypeCalculationAverage == MovingAverageTypeCalculation.Adaptive)
                {
                    Values.Add(GetValueKaufmanAdaptive(values, i));
                }
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
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Simple)
            {
                Values[Values.Count - 1] = GetValueSimple(values, values.Count - 1);
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Exponential)
            {
                Values[Values.Count - 1] = GetValueExponential(values, values.Count - 1);
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Weighted)
            {
                Values[Values.Count - 1] = GetValueWeighted(values, values.Count - 1);
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Radchenko)
            {
                Values[Values.Count - 1] = GetValueRadchenko(Values, values, values.Count - 2);
            }
            if (TypeCalculationAverage == MovingAverageTypeCalculation.Adaptive)
            {
                Values.Add(GetValueKaufmanAdaptive(values, values.Count - 1));
            }
        }

        /// <summary>
        /// take indicator value by index
        /// взять значение индикаторм по индексу
        /// </summary>
        private decimal GetValueSimple(List<decimal> values, int index)
        {
            if (index - Lenght <= 0)
            {
                return 0;
            }

            decimal average = 0;

            for (int i = index; i > index - Lenght; i--)
            {
                average += values[i];
            }

            average = average / Lenght;

            return Math.Round(average, 8);
        }

        /// <summary>
        /// exponent
        /// экспонента
        /// </summary>
        private decimal GetValueExponential(List<decimal> values, int index)
        {
            decimal result = 0;

            if (index == Lenght)
            {
                // it's first value. Calculate as simple ma 
                // это первое значение. Рассчитываем как простую машку
                decimal lastMoving = 0;

                for (int i = index - Lenght + 1; i < index + 1; i++)
                {
                    lastMoving += values[i];
                }

                lastMoving = lastMoving / Lenght;

                result = lastMoving;
            }
            else if (index > Lenght)
            {
                // decimal a = 2.0m / (length * 2 - 0.15m);

                decimal a = Math.Round(2.0m / (Lenght + 1), 8);

                decimal emaLast = Values[index - 1];

                decimal p = values[index];
                //ЕМА(i) = ЕМА(i - 1) + ( К • [ Close(i) - ЕМА (i -1) ] ), 
                result = emaLast + (a * (p - emaLast));
                //result = a*p + (1 - a)*emaLast;
            }

            return Math.Round(result, 8);
        }

        /// <summary>
        /// weighted
        /// взвешенная
        /// </summary>
        private decimal GetValueWeighted(List<decimal> values, int index)
        {
            if (index - Lenght <= 0)
            {
                return 0;
            }
            decimal average = 0;

            int weights = 0;

            for (int i = index, weight = Lenght; i > index - Lenght; i--, weight--)
            {
                average += values[i] *weight;
                weights += weight;
            }
            if (weights == 0)
            {
                return 0;
            }
            average = average / weights;

            return Math.Round(average,8);

        }

        /// <summary>
        /// radchenko
        /// радченко
        /// </summary>
        private decimal GetValueRadchenko(List<decimal> lastValues, List<decimal> values, int index)
        {
            if (index - Lenght <= 0)
            {
                return 0;
            }

            decimal average = 0;

            for (int i = index; i > index - Lenght; i--)
            {
                average += values[i];
            }

            average = average / Lenght;

            int radchenkoFaze = 0; // 0 - nothing/ничего -1 - just down/только вниз +1 just up/только вверх

            if (values[index] > average)
            {
                radchenkoFaze = 1;
            }
            else if (values[index] < average)
            {
                radchenkoFaze = -1;
            }

            if (radchenkoFaze == 0)
            {
                return average;
            }

            if (values.Count == 0 || values[values.Count - 1] == 0)
            {
                return average;
            }

            if (radchenkoFaze == -1)
            {
                decimal lastPoint = lastValues[index - 1];

                if (average < lastPoint)
                {
                    return average;
                }
                else
                {
                    return lastPoint;
                }
            }

            if (radchenkoFaze == 1)
            {
                decimal lastPoint = lastValues[index - 1];

                if (average > lastPoint)
                {
                    return average;
                }
                else
                {
                    return lastPoint;
                }
            }

            return Math.Round(average, 8);
        }


        // Kaufman  Adaptive

        /// <summary>
        /// Fast EMA length for calculating Kaufmann adaptive moving average
        /// длинна быстрой EMA для рассчёта адаптивной Кауфмана
        /// </summary>
        public int KaufmanFastEma;

        /// <summary>
        /// Slow EMA length for calculating Kaufmann adaptive moving average
        /// длинна медленной EMA для рассчёта адаптивной Кауфмана
        /// </summary>
        public int KaufmanSlowEma;

        private decimal GetValueKaufmanAdaptive(List<Candle> candles, int index)
        {
            decimal result = 0;

            if (index == Lenght)
            {
                // it's first value. Calculate as simple moving
                // это первое значение. Рассчитываем как простую машку
                result = GetPoint(candles, index);
            }
            else if (index > Lenght)
            {
                // 1 calculating ER
                // 1 высчитываем ER

                decimal signal = Math.Abs(GetPoint(candles, index) - GetPoint(candles, index-Lenght));

                decimal noise = 0;

                for (int i = index; i > 0 && i >= index - Lenght + 1; i--)
                {
                    noise += Math.Abs(GetPoint(candles, i) - GetPoint(candles, i - 1));
                }

                decimal er = 1;
                
                if(noise != 0)
                {
                    er = signal / noise;
                }


                // 2 calculate the coefficient
                // 2 высчитываем коэффициент

                decimal aFast = Math.Round(2.0m / (KaufmanFastEma + 1), 8);
                decimal aSlow = Math.Round(2.0m / (KaufmanSlowEma + 1), 8);

                decimal aDunamic = er*(aFast - aSlow) + aSlow;
                
                //decimal a = Math.Round(2.0m / (Lenght + 1), 6);

                decimal emaLast = Values[index-1];

                decimal p = GetPoint(candles, index);

                result = emaLast + (aDunamic *aDunamic)*(p - emaLast);

                //result = p*aDunamic + emaLast * (1-aDunamic);
                //result = emaLast + (aDunamic * (p - emaLast));
            }

            return Math.Round(result, 8);
        }

        private decimal GetValueKaufmanAdaptive(List<decimal> values, int index)
        {
            decimal result = 0;

            if (index == Lenght)
            {
                // it's first value. Calculate as simple moving
                // это первое значение. Рассчитываем как простую машку
                result = values[index];
            }
            else if (index > Lenght)
            {
                // 1 calculating ER
                // 1 высчитываем ER

                decimal signal = Math.Abs(values[index] - values[index-Lenght]);

                decimal noise = 0;

                for (int i = index; i > 0 && i >= index - Lenght + 1; i--)
                {
                    noise += Math.Abs(values[i] - values[i-1]);
                }

                decimal er = 1;

                if (noise != 0)
                {
                    er = signal / noise;
                }


                // 2 calculate the coefficient
                // 2 высчитываем коэффициент

                decimal aFast = Math.Round(2.0m / (KaufmanFastEma + 1), 8);
                decimal aSlow = Math.Round(2.0m / (KaufmanSlowEma + 1), 8);

                decimal aDunamic = er * (aFast - aSlow) + aSlow;

                //decimal a = Math.Round(2.0m / (Lenght + 1), 6);

                decimal emaLast = Values[index-1];

                decimal p = values[index];

                result = emaLast + (aDunamic * aDunamic) * (p - emaLast);

                //result = p*aDunamic + emaLast * (1-aDunamic);
                //result = emaLast + (aDunamic * (p - emaLast));
            }

            return Math.Round(result, 8);
        }

        private decimal GetValueSmma(List<Candle> candles, int index)
        {
            decimal result = 0;

            if (index == Lenght)
            {
                // it's the first value. Calculate as simple ma
                // это первое значение. Рассчитываем как простую машку
                decimal lastMoving = 0;

                for (int i = index - Lenght + 1; i < index + 1; i++)
                {
                    lastMoving += GetPoint(candles, i);
                }

                lastMoving = lastMoving / Lenght;

                result = lastMoving;
            }
            else if (index > Lenght)
            {
                // (smma[1] * (length - 1) + src) / length

                decimal emaLast = Values[index - 1];

                decimal p = GetPoint(candles, index);

                result = (emaLast * (Lenght - 1) + p) / Lenght;

            }

            return Math.Round(result, 10);
        }
    }
}
