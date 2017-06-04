/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Entity;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// 
    /// </summary>
    public class ForceIndex : IIndicatorCandle
    {
        /// <summary>
        /// период N
        /// </summary>
        public int Period;

        public PriceTypePoints TypePoint;

        /// <summary>
        /// конструктор с параметрами. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">уникальное имя</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public ForceIndex(string uniqName, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorOneCandleChartType.Line;
            TypeCalculationAverage = MovingAverageTypeCalculation.Exponential;
            TypePoint = PriceTypePoints.Close;
            Period = 13;
            ColorBase = Color.DodgerBlue;
            PaintOn = true;
            CanDelete = canDelete;

            Load();
        }

        /// <summary>
        /// конструктор без параметров. Индикатор не будет сохраняться
        /// </summary>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public ForceIndex(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            TypeIndicator = IndicatorOneCandleChartType.Line;
            TypeCalculationAverage = MovingAverageTypeCalculation.Exponential;
            TypePoint = PriceTypePoints.Close;
            Period = 13;
            ColorBase = Color.DodgerBlue;
            PaintOn = true;
            CanDelete = canDelete;

        }

        /// <summary>
        /// все значения индикатора
        /// </summary>
        List<List<decimal>> IIndicatorCandle.ValuesToChart
        {
            get
            {
                List<List<decimal>> list = new List<List<decimal>>();
                list.Add(Values);
                return list;
            }
        }

        /// <summary>
        /// цвета для индикатора
        /// </summary>
        List<Color> IIndicatorCandle.Colors
        {
            get
            {
                List<Color> colors = new List<Color>();
                colors.Add(ColorBase);
                return colors;
            }

        }

        /// <summary>
        /// можно ли удалить индикатор с графика. Это нужно для того чтобы у роботов нельзя было удалить 
        /// индикаторы которые ему нужны в торговле
        /// </summary>
        public bool CanDelete { get; set; }

        /// <summary>
        /// тип прорисовки индикатора
        /// </summary>
        public IndicatorOneCandleChartType TypeIndicator { get; set; }

        /// <summary>
        /// имя серии данных на которой будет прорисован индикатор
        /// </summary>
        public string NameSeries { get; set; }

        /// <summary>
        /// имя области данных на которой будет прорисовываться индикатор
        /// </summary>
        public string NameArea { get; set; }

        /// <summary>
        /// значения индикатора
        /// </summary>
        public List<decimal> Values
        { get; set; }

        /// <summary>
        /// уникальное имя индикатора
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// цвет центрально серии данных
        /// </summary>
        public Color ColorBase { get; set; }

        /// <summary>
        /// свечи для рассчёта индикатора
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// включена ли прорисовка индикатора
        /// </summary>
        public bool PaintOn { get; set; }

        /// <summary>
        /// сохранить настройки в файл
        /// </summary>
        public void Save()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Name))
                {
                    return;
                }

                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @".txt", false))
                {
                    writer.WriteLine(Period);
                    writer.WriteLine(TypePoint);
                    writer.WriteLine(TypeCalculationAverage);
                    writer.WriteLine(ColorBase.ToArgb());
                    writer.WriteLine(PaintOn);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
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
                    Period = Convert.ToInt32(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out TypePoint);
                    Enum.TryParse(reader.ReadLine(), true, out TypeCalculationAverage);
                    ColorBase = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    PaintOn = Convert.ToBoolean(reader.ReadLine());
                    reader.ReadLine();

                    reader.Close();
                }


            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
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
        /// показать окно с настройками
        /// </summary>
        public void ShowDialog()
        {
            ForceIndexUi ui = new ForceIndexUi(this);
            ui.ShowDialog();

            if (ui.IsChange && _myCandles != null)
            {
                ProcessAll(_myCandles);

                if (NeadToReloadEvent != null)
                {
                    NeadToReloadEvent(this);
                }
            }
        }

        /// <summary>
        /// рассчитать индикатор
        /// </summary>
        /// <param name="candles">свечи</param>
        public void Process(List<Candle> candles)
        {

            _myCandles = candles;

            if (_movingAverage == null)
            {
                _fi = new List<decimal>();
                _movingAverage = new MovingAverage(false);
                _movingAverage.Lenght = Period;
                _movingAverage.TypePointsToSearch = TypePoint;
                _movingAverage.TypeCalculationAverage = TypeCalculationAverage;

            }

            _movingAverage.Process(candles);

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
        /// индикатор нужно перерисовать
        /// </summary>
        public event Action<IIndicatorCandle> NeadToReloadEvent;

        /// <summary>
        /// прогрузить только последнюю свечку
        /// </summary>
        private void ProcessOne(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }
            _fi.Add(GetRange(candles, candles.Count - 1));
            _movingAverage.Process(_fi);

            if (Values == null)
            {
                Values = new List<decimal>();
                Values.Add(GetValue(candles, candles.Count - 1));
            }
            else
            {
                Values.Add(GetValue(candles, candles.Count - 1));
            }
        }

        /// <summary>
        /// прогрузить с самого начала
        /// </summary>
        private void ProcessAll(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }
            Values = new List<decimal>();
            _fi = new List<decimal>();

            _movingAverage = new MovingAverage(false);
            _movingAverage.Lenght = Period;
            _movingAverage.TypePointsToSearch = TypePoint;
            _movingAverage.TypeCalculationAverage = TypeCalculationAverage;


            for (int i = 0; i < candles.Count; i++)
            {
                _fi.Add(GetRange(candles, i));
                _movingAverage.Process(_fi);
                Values.Add(GetValue(candles, i));
            }
        }

        /// <summary>
        /// перегрузить последнее значение
        /// </summary>
        private void ProcessLast(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }

            _fi[_fi.Count - 1] = GetRange(candles, candles.Count - 1);
            _movingAverage.Process(_fi);

            Values[Values.Count - 1] = GetValue(candles, candles.Count - 1);
        }

        /// <summary>
        /// взять значение индикатора по индексу
        /// </summary>
        /// <param name="candles">свечи</param>
        /// <param name="index">индекс</param>
        /// <returns>значение индикатора по индексу</returns>
        private decimal GetValue(List<Candle> candles, int index)
        {

            if (index < Period || _movingAverage.Values.Count < Period)
            {
                return 0;
            }

            if (_movingAverage.Values[index] == 0 || _movingAverage.Values[index - 1] == 0)
            {
                return 0;
            }

            return Math.Round(_movingAverage.Values[index],2);
        }

        private decimal GetRange(List<Candle> candles, int index)
        {
            decimal value = 0;

            if (index == 0)
            {
                return 0;
            }

            if (TypePoint == PriceTypePoints.Close)
            {
                value = (1 - candles[index - 1].Close / candles[index].Close) * candles[index].Volume;
            }
            if (TypePoint == PriceTypePoints.Open)
            {
                value = (1 - candles[index - 1].Open / candles[index].Open) * candles[index].Volume;
            }
            if (TypePoint == PriceTypePoints.High)
            {
                value = (1 - candles[index - 1].High / candles[index].High) * candles[index].Volume;
            }
            if (TypePoint == PriceTypePoints.Low)
            {
                value = (1 - candles[index - 1].Low / candles[index].Low) * candles[index].Volume;
            }

            return value;
        }

        /// <summary>
        /// скользящая средняя
        /// </summary>
        private MovingAverage _movingAverage;

        /// <summary>
        /// тип скользящей средней для рассчёта индикатора
        /// </summary>
        public MovingAverageTypeCalculation TypeCalculationAverage;

        private List<decimal> _fi;

    }
}