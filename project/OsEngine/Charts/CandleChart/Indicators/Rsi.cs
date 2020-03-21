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
    /// RSI indicator
    /// RSI. Relative Strength Index. Индикатор
    /// </summary>
    public class Rsi:IIndicator
    {

        /// <summary>
        /// constructor with parameters. Indicator will be saved
        /// конструктор с параметрами. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">unique name/уникальное имя</param>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public Rsi(string uniqName,bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorChartPaintType.Line;
            Lenght = 5;
            ColorBase = Color.Green;
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
        public Rsi(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();
            TypeIndicator = IndicatorChartPaintType.Line;
            Lenght = 5;
            ColorBase = Color.Green;
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
        /// тип индикатора
        /// </summary>
        public IndicatorChartPaintType TypeIndicator { get; set; }

        /// <summary>
        /// unique indicator name
        /// уникальное имя индикатор
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// name of data series on which indicator will be drawn
        /// имя серии данных на которой индикатор прорисовывается
        /// </summary>
        public string NameSeries
        { get; set; }

        /// <summary>
        /// name of data area where indicator will be drawn
        /// имя области данных на которой индикатор прорисовывается
        /// </summary>
        public string NameArea
        { get; set; }

        /// <summary>
        /// indicator calculation length
        /// длинна расчёта индикатора
        /// </summary>
        public int Lenght { get; set; }

        /// <summary>
        /// indicator color
        /// цвет индикатора
        /// </summary>
        public Color ColorBase { get; set; }

        /// <summary>
        /// is indicator tracing enabled
        /// включена ли прорисовка индикатора
        /// </summary>
        public bool PaintOn
        { get; set; }

        /// <summary>
        /// upload settings from file
        ///  загрузить настройки из файла
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
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            RsiUi ui = new RsiUi(this);
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
        /// indicator values
        /// данные индикатора
        /// </summary>
        public List<decimal> Values { get; set; }

        /// <summary>
        /// candles to calculate indicator
        /// свечи по которым строиться индикатор
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// indicator needs to be redrawn
        /// требуется перерисовать индикатор
        /// </summary>
        public event Action<IIndicator> NeadToReloadEvent;
        // calculation
        // вычисления

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
        /// взять значение индикаторм по индексу
        /// </summary>
        private decimal GetValue(List<Candle> candles, int index)
        {
            if (index - Lenght - 1 <= 0)
            {
                return 0;
            }

            int startIndex = 1;

            if (index > 150)
            {
                startIndex = index - 150;
            }

            decimal[] priceChangeHigh = new decimal[candles.Count];
            decimal[] priceChangeLow = new decimal[candles.Count];

            decimal[] priceChangeHighAverage = new decimal[candles.Count];
            decimal[] priceChangeLowAverage = new decimal[candles.Count];

            for (int i = startIndex; i < candles.Count; i++)
            {
                if (candles[i].Close - candles[i - 1].Close > 0)
                {
                    priceChangeHigh[i] = candles[i].Close - candles[i - 1].Close;
                    priceChangeLow[i] = 0;
                }
                else
                {
                    priceChangeLow[i] = candles[i - 1].Close - candles[i].Close;
                    priceChangeHigh[i] = 0;
                }

                MovingAverageHard(priceChangeHigh, priceChangeHighAverage, Lenght, i);
                MovingAverageHard(priceChangeLow, priceChangeLowAverage, Lenght, i);
            }

            decimal averageHigh = priceChangeHighAverage[index];
            decimal averageLow = priceChangeLowAverage[index];

            decimal rsi;

            if (averageHigh != 0 &&
                averageLow != 0)
            {
                rsi = 100 * (1 - averageLow / (averageLow + averageHigh));
                //rsi = 100 - 100 / (1 + averageHigh / averageLow);
            }
            else
            {
                rsi = 100;
            }

            return Math.Round(rsi, 2);
        }

        /// <summary>
        /// take exponential average by index
        /// взять экспоненциальную среднюю по индексу
        /// </summary>
        /// <param name="valuesSeries">index data series/серия данных для расчета индекса</param>
        /// <param name="moving">previous average values/предыдущие значения средней</param>
        /// <param name="length">length of ma/длинна машки</param>
        /// <param name="index">index/индекс</param>
        private void MovingAverageHard(decimal[] valuesSeries, decimal[] moving, int length, int index)
        {
            if (index == length)
            {
                // it's first value. Calculate as simple ma
                // это первое значение. Рассчитываем как простую машку

                decimal lastMoving = 0;

                for (int i = index; i > index - 1 - length; i--)
                {
                    lastMoving += valuesSeries[i];
                }
                lastMoving = lastMoving / length;

                moving[index] = lastMoving;
            }
            else if (index > length)
            {
                // decimal a = 2.0m / (length * 2 - 0.15m);

                decimal a = Math.Round(2.0m / (length * 2), 7);

                decimal lastValueMoving = moving[index - 1];

                decimal lastValueSeries = Math.Round(valuesSeries[index], 7);

                decimal nowValueMoving;

                //if (lastValueSeries != 0)
                // {
                nowValueMoving = Math.Round(lastValueMoving + a * (lastValueSeries - lastValueMoving), 7);
                // }
                // else
                // {
                //     nowValueMoving = lastValueMoving;
                // }

                moving[index] = nowValueMoving;
            }
        }
    }
}
