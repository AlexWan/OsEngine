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
    /// RSI. Relative Strength Index. Индикатор
    /// </summary>
    public class Rsi:IIndicatorCandle
    {

        /// <summary>
        /// конструктор с параметрами. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">уникальное имя</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public Rsi(string uniqName,bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorOneCandleChartType.Line;
            Lenght = 5;
            ColorBase = Color.Green;
            PaintOn = true;
            CanDelete = canDelete;
            Load();
        }

        /// <summary>
        /// конструктор без параметров. Индикатор не будет сохраняться
        /// используется ТОЛЬКО для создания составных индикаторов
        /// не используйте его из слоя создания роботов!
        /// </summary>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public Rsi(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();
            TypeIndicator = IndicatorOneCandleChartType.Line;
            Lenght = 5;
            ColorBase = Color.Green;
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
        /// тип индикатора
        /// </summary>
        public IndicatorOneCandleChartType TypeIndicator { get; set; }

        /// <summary>
        /// уникальное имя индикатор
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// имя серии данных на которой индикатор прорисовывается
        /// </summary>
        public string NameSeries
        { get; set; }

        /// <summary>
        /// имя области данных на которой индикатор прорисовывается
        /// </summary>
        public string NameArea
        { get; set; }

        /// <summary>
        /// длинна расчёта индикатора
        /// </summary>
        public int Lenght { get; set; }

        /// <summary>
        /// цвет индикатора
        /// </summary>
        public Color ColorBase { get; set; }

        /// <summary>
        /// включена ли прорисовка индикатора
        /// </summary>
        public bool PaintOn
        { get; set; }

        /// <summary>
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
                // отправить в лог
            }
        }

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
                    writer.WriteLine(ColorBase.ToArgb());
                    writer.WriteLine(Lenght);

                    writer.Close();
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
        /// данные индикатора
        /// </summary>
        public List<decimal> Values { get; set; }

        /// <summary>
        /// свечи по которым строиться индикатор
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// требуется перерисовать индикатор
        /// </summary>
        public event Action<IIndicatorCandle> NeadToReloadEvent;

// вычисления

        /// <summary>
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
        /// взять экспоненциальную среднюю по индексу
        /// </summary>
        /// <param name="valuesSeries">сирия данных для рассчёта индекса</param>
        /// <param name="moving">предыдущие значения средней</param>
        /// <param name="length">длинна машки</param>
        /// <param name="index">индекс</param>
        private void MovingAverageHard(decimal[] valuesSeries, decimal[] moving, int length, int index)
        {
            if (index == length)
            { // это первое значение. Рассчитываем как простую машку

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
