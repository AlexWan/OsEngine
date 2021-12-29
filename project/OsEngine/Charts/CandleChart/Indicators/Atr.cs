/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
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
    /// Indicator ATR. Average True Range
    /// индикатор ATR. Average True Range
    /// </summary>
    public class Atr : IIndicator
    {

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="uniqName">unique name/уникальное имя</param>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public Atr(string uniqName, bool canDelete)
        {
            Name = uniqName;
            Lenght = 14;
            TypeIndicator = IndicatorChartPaintType.Line;
            ColorBase = Color.DodgerBlue;
            PaintOn = true;
            IsWatr = false;
            CanDelete = canDelete;
            Load();
        }

        /// <summary>
        /// constructor without parameters.Indicator will not saved/конструктор без параметров. Индикатор не будет сохраняться
        /// used ONLY to create composite indicators/используется ТОЛЬКО для создания составных индикаторов
        /// Don't use it from robot creation layer/не используйте его из слоя создания роботов!
        /// </summary>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public Atr(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            Lenght = 14;
            TypeIndicator = IndicatorChartPaintType.Line;
            ColorBase = Color.DodgerBlue;
            PaintOn = true;
            IsWatr = false;
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
        /// name of data series on which indicator will be drawn
        /// имя серии данных на которой будет прорисован индикатор
        /// </summary>
        public string NameSeries { get; set; }

        /// <summary>
        /// name of data area where indicator will be drawn
        /// имя области данных на которой будет прорисовываться индикатор
        /// </summary>
        public string NameArea { get; set; }

        /// <summary>
        /// Atr
        /// </summary>
        public List<decimal> Values
        { get; set; }

        /// <summary>
        /// unique indicator name
        /// уникальное имя индикатора
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// color of central data series (ATR)
        /// цвет центрально серии данных (ATR)
        /// </summary>
        public Color ColorBase { get; set; }

        /// <summary>
        /// period length to calculate indicator
        /// длинна периода для рассчёта индикатора
        /// </summary>
        public int Lenght;

        /// <summary>
        /// is indicator tracing enabled
        /// включена ли прорисовка индикатора
        /// </summary>
        public bool PaintOn { get; set; }

        /// <summary>
        /// is indicator exponentially weighted
        /// включено ли экспоненциальное взвешивание значения
        /// </summary>
        public bool IsWatr { get; set; }

        /// <summary>
        /// save settings to file
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
                    writer.WriteLine(ColorBase.ToArgb());
                    writer.WriteLine(Lenght);
                    writer.WriteLine(PaintOn);
                    writer.WriteLine(IsWatr);
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
                    IsWatr = Convert.ToBoolean(reader.ReadLine());

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
            AtrUi ui = new AtrUi(this);
            ui.ShowDialog();

            if (ui.IsChange && _myCandles != null)
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
            if (_myCandles == null)
            {
                return;
            }
            ProcessAll(_myCandles);

            if (NeadToReloadEvent != null)
            {
                NeadToReloadEvent(this);
            }
        }

        /// <summary>
        /// candles to calculate indicator
        /// свечи для рассчёта индикатора
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// calculate indicator
        /// рассчитать индикатор
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        public void Process(List<Candle> candles)
        {
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
        /// indicator needs to be redrawn
        /// индикатор нужно перерисовать
        /// </summary>
        public event Action<IIndicator> NeadToReloadEvent;

        /// <summary>
        /// load only last candle
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
                Values.Add(GetValue(candles, candles.Count - 1));
            }
            else
            {
                Values.Add(GetValue(candles, candles.Count - 1));
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
                Values.Add(GetValue(candles, i));
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
            Values[Values.Count - 1] = GetValue(candles, candles.Count - 1);
        }

        /// <summary>
        /// take indicator value by index
        /// взять значение индикатора по индексу
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        /// <param name="index">index/индекс</param>
        /// <returns>index value/значение индикатора по индексу</returns>
        public decimal GetValue(List<Candle> candles,int index)
        {
            TrueRangeReload(candles, index);
            if (!IsWatr)
            {
                _moving = MovingAverageWild(_trueRange, _moving, Lenght, index);
            } else
            {
                _moving = MovingAverageExponentiallyWeighted(_trueRange, _moving, Lenght, index);
            }
            

            return Math.Round(_moving[_moving.Count-1],7);
        }

        private List<decimal> _moving;

        /// <summary>
        /// true range
        /// истинный диапазон
        /// </summary>
        private List<decimal> _trueRange;

        private void TrueRangeReload(List<Candle> candles, int index)
        {
            //True Range is the largest of following three:/Истинный диапазон (True Range) есть наибольшая из следующих трех величин:
            //difference between current maximum and minimum;/разность между текущими максимумом и минимумом;
            //difference between previous closing price and current maximum;/разность между предыдущей ценой закрытия и текущим максимумом;
            //difference between previous closing price and current minimum./разность между предыдущей ценой закрытия и текущим минимумом.

            if (index == 0 || _trueRange == null)
            {
                _trueRange = new List<decimal>();
                _trueRange.Add(0);
                return;
            }

            if (index > _trueRange.Count - 1)
            {
                _trueRange.Add(0);
            }

            decimal hiToLow = Math.Abs(candles[index].High - candles[index].Low);
            decimal closeToHigh = Math.Abs(candles[index - 1].Close - candles[index].High);
            decimal closeToLow = Math.Abs(candles[index - 1].Close - candles[index].Low);

            _trueRange[_trueRange.Count - 1] = Math.Max(Math.Max(hiToLow, closeToHigh), closeToLow);
        }

        private List<decimal> MovingAverageWild(List<decimal> valuesSeries, List<decimal> moving, int length, int index)
        {
            if (moving == null || length > valuesSeries.Count)
            {
                moving = new List<decimal>();
                for (int i = 0; i < index + 1; i++)
                {
                    moving.Add(0);
                }
            }
            else if (length == valuesSeries.Count)
            {
                // it's first value. Calculate as MA
                // это первое значение. Рассчитываем как простую машку

                decimal lastMoving = 0;

                for (int i = index; i > -1 && i > valuesSeries.Count - 1 - length; i--)
                {
                    lastMoving += valuesSeries[i];
                }
                if (lastMoving != 0)
                {
                    moving.Add(lastMoving / length);
                }
                else
                {
                    moving.Add(0);
                }

            }
            else
            {

                decimal lastValueMoving;
                decimal lastValueSeries = Math.Round(valuesSeries[valuesSeries.Count - 1], 7);

                if (index > moving.Count - 1)
                {
                    lastValueMoving = moving[moving.Count - 1];
                    moving.Add(0);
                }
                else
                {
                    lastValueMoving = moving[moving.Count - 2];
                }

                moving[moving.Count - 1] = Math.Round((lastValueMoving * (Lenght - 1) + lastValueSeries) / Lenght, 7);

            }

            return moving;
        }

        private List<decimal> MovingAverageExponentiallyWeighted(List<decimal> valuesSeries, List<decimal> moving, int length, int index)
        {
            decimal lambda = Convert.ToDecimal(2.0 / (Lenght + 1));

            if (moving == null || length > valuesSeries.Count)
            {
                moving = new List<decimal>();
                for (int i = 0; i < index + 1; i++)
                {
                    moving.Add(0);
                }
            }
            else if (length == valuesSeries.Count)
            {
                // it's first value. Calculate as MA
                // это первое значение. Рассчитываем как простую машку
                decimal lastMoving = 0;

                for (int i = index; i > -1 && i > valuesSeries.Count - 1 - length; i--)
                {
                    lastMoving += valuesSeries[i];
                }
                if (lastMoving != 0)
                {
                    moving.Add(lastMoving / length);
                }
                else
                {
                    moving.Add(0);
                }

            }
            else
            {

                decimal lastValueMoving;
                decimal lastValueSeries = Math.Round(valuesSeries[valuesSeries.Count - 1], 7);

                if (index > moving.Count - 1)
                {
                    lastValueMoving = moving[moving.Count - 1];
                    moving.Add(0);
                }
                else
                {
                    lastValueMoving = moving[moving.Count - 2];
                }

                moving[moving.Count - 1] = Math.Round(lastValueMoving + (lastValueSeries-lastValueMoving) * lambda, 7);

            }

            return moving;
        }
    }
}