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
    /// MACD Histogram. Moving Average Convergence Divergence. Индикатор для анализа схождения-расхождения скользящих средних, в виде Гистограммы
    /// </summary>
    public class MacdHistogram: IIndicatorCandle
    {
        /// <summary>
        /// конструктор с уникальным именем. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">уникальное имя</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public MacdHistogram(string uniqName,bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorOneCandleChartType.Column;
            ColorUp = Color.DodgerBlue;
            ColorDown = Color.DarkRed;
            PaintOn = true;

            if (!File.Exists(@"Engine\" + Name + @".txt"))
            {// если у нас первая загрузка
                _maShort = new MovingAverage(uniqName + "ma1", false) { Lenght = 12, TypeCalculationAverage = MovingAverageTypeCalculation.Exponential };
                _maLong = new MovingAverage(uniqName + "ma2", false) { Lenght = 26, TypeCalculationAverage = MovingAverageTypeCalculation.Exponential };
                _maSignal = new MovingAverage(uniqName + "maSignal", false) { Lenght = 9, TypeCalculationAverage = MovingAverageTypeCalculation.Simple };
            } 
            else
            {
                _maShort = new MovingAverage(uniqName + "ma1", false);
                _maLong = new MovingAverage(uniqName + "ma2", false);
                _maSignal = new MovingAverage(uniqName + "maSignal", false);
            }
            CanDelete = canDelete;
            Load();
        }

        /// <summary>
        /// конструктор с уникальным именем. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">уникальное имя</param>
        /// <param name="movingShort">короткий мувинг</param>
        /// <param name="movingLong">длинный мувинг</param>
        /// <param name="movingSignal">сигнальный мувинг</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public MacdHistogram(string uniqName, MovingAverage movingShort, MovingAverage movingLong, MovingAverage movingSignal, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorOneCandleChartType.Column;
            ColorUp = Color.DodgerBlue;
            ColorDown= Color.DarkRed;
            PaintOn = true;

            _maShort = movingShort;
            _maLong = movingLong;
            _maSignal = movingSignal;

            _maShort.Name = uniqName + "ma1";
            _maLong.Name = uniqName + "ma12";
            _maSignal.Name = uniqName + "maSignal";
            CanDelete = canDelete;
            Load();
        }

        /// <summary>
        /// конструктор без параметров. Индикатор не будет сохраняться
        /// </summary>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public MacdHistogram(bool canDelete)
        {
            TypeIndicator = IndicatorOneCandleChartType.Column;
            ColorUp = Color.DodgerBlue;
            ColorDown = Color.DarkRed;
            PaintOn = true;
            _maShort = new MovingAverage( false) {Lenght = 12,TypeCalculationAverage = MovingAverageTypeCalculation.Exponential};
            _maLong = new MovingAverage( false) { Lenght = 26, TypeCalculationAverage = MovingAverageTypeCalculation.Exponential };
            _maSignal = new MovingAverage( false){Lenght = 9,TypeCalculationAverage = MovingAverageTypeCalculation.Simple};
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
                colors.Add(ColorUp);
                colors.Add(ColorDown);
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
        /// Macd Histogram
        /// </summary>
        public List<decimal> Values { get; set; }

        /// <summary>
        /// уникальное имя индикатора
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// цвет растущего столбика
        /// </summary>
        public Color ColorUp { get; set; }

        /// <summary>
        /// цвет падуещего столбца
        /// </summary>
        public Color ColorDown { get; set; }

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
                _maSignal.Save();
                _maShort.Save();
                _maLong.Save();
                if (string.IsNullOrWhiteSpace(Name))
                {
                    return;
                }

                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @".txt", false))
                {
                    writer.WriteLine(ColorUp.ToArgb());
                    writer.WriteLine(ColorDown.ToArgb());
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
                    ColorUp = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorDown = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
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
            _maShort.Delete();
            _maLong.Delete();
            _maSignal.Delete();
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
            MacdHistogramUi ui = new MacdHistogramUi(this);
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
        /// показать настройки короткой машки
        /// </summary>
        public void ShowMaShortDialog()
        {
            _maShort.ShowDialog();

            ProcessAll(_myCandles);

            if (NeadToReloadEvent != null)
            {
                NeadToReloadEvent(this);
            }
        }

        /// <summary>
        /// показать настройки длинной машки
        /// </summary>
        public void ShowMaLongDialog()
        {
            _maLong.ShowDialog();

            ProcessAll(_myCandles);

            if (NeadToReloadEvent != null)
            {
                NeadToReloadEvent(this);
            }
        }

        /// <summary>
        /// показать настройки сигнальной машки
        /// </summary>
        public void ShowMaSignalDialog()
        {
            _maSignal.ShowDialog();

            ProcessAll(_myCandles);

            if (NeadToReloadEvent != null)
            {
                NeadToReloadEvent(this);
            }
        }

        // расчёт

        /// <summary>
        /// свечи для рассчёта индикатора
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// короткая машка
        /// </summary>
        private MovingAverage _maShort;

        /// <summary>
        /// длинная машка
        /// </summary>
        private MovingAverage _maLong;

        /// <summary>
        /// сигнальная машка
        /// </summary>
        private MovingAverage _maSignal;

        private List<decimal> _macd; 

        /// <summary>
        /// рассчитать индикатор
        /// </summary>
        /// <param name="candles">свечи</param>
        public void Process(List<Candle> candles)
        {
            _myCandles = candles;

            _maShort.Process(candles);
            _maLong.Process(candles);


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

           // Values = _maSignal.Values;
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

            if (_macd == null)
            {
                _macd = new List<decimal>();
                Values = new List<decimal>();
                _macd.Add(GetMacd(candles.Count - 1));
            }
            else
            {
                _macd.Add(GetMacd(candles.Count - 1));
            }
            _maSignal.Process(_macd);

            Values.Add(GetMacdHistogram(candles.Count - 1));
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

            _maShort.Values = null;
            _maLong.Values = null;
            _maSignal.Values = null;

            _maShort.Process(candles);
            _maLong.Process(candles);

            _macd = new List<decimal>();
            Values = new List<decimal>();

            for (int i = 0; i < candles.Count; i++)
            {
                _macd.Add(GetMacd(i));
                _maSignal.Process(_macd);
                Values.Add(GetMacdHistogram(i));
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
            _macd[_macd.Count - 1] = GetMacd(candles.Count - 1);
            _maSignal.Process(_macd);
            Values[Values.Count - 1] = GetMacdHistogram(Values.Count - 1);
        }

        /// <summary>
        /// взять значение индикатора по индексу
        /// </summary>
        /// <param name="index">индекс</param>
        /// <returns>значение индикатора по индексу</returns>
        private decimal GetMacd(int index)
        {
            if (_maShort == null || _maShort.Values[index] == 0 ||
                _maShort.Values.Count - 1 < index)
            {
                return 0;
            }

            if (_maLong == null || _maLong.Values[index] == 0 ||
                _maLong.Values.Count - 1 < index)
            {
                return 0;
            }

            return _maShort.Values[index] - _maLong.Values[index];
           
        }

        private decimal GetMacdHistogram(int index)
        {
            if (_maSignal.Values[index] == 0
                || _macd[index] == 0)
            {
                return 0;
            }
            else
            {
                return Math.Round(_macd[index] - _maSignal.Values[index], 7);
            }
        }

    }
}