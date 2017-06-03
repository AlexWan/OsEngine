/*
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Entity;

namespace OsEngine.Charts.CandleChart.Indicators
{

    /// <summary>
    /// Bollinger. Индикатор Боллинджер
    /// </summary>
    public class Bollinger: IIndicatorCandle
    {
        /// <summary>
        /// конструктор с параметром. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">уникальное имя индикатора</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public Bollinger(string uniqName,bool canDelete)
        {
            Name = uniqName;

            TypeIndicator = IndicatorOneCandleChartType.Line;
            ColorUp = Color.DodgerBlue;
            ColorDown = Color.DarkRed;
            Deviation = 2;
            Lenght = 12;
            PaintOn = true;
            CanDelete = canDelete;
            Load();
        }

        /// <summary>
        /// конструктор без параметров. Индикатор не будет сохраняться
        /// </summary>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public Bollinger(bool canDelete)
        {
            Name = "";

            TypeIndicator = IndicatorOneCandleChartType.Line;
            ColorUp = Color.DodgerBlue;
            ColorDown = Color.DarkRed;
            Deviation = 2;
            Lenght = 12;
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
                list.Add(ValuesUp);
                list.Add(ValuesDown);
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
        /// тип индикатора
        /// </summary>
        public IndicatorOneCandleChartType TypeIndicator
        { get; set; }

        /// <summary>
        /// имя серии данных на которой индикатор будет прорисовываться
        /// </summary>
        public string NameSeries
        { get; set; }

        /// <summary>
        /// имя области данных на которой индикатор будет прорисовываться
        /// </summary>
        public string NameArea
        { get; set; }

        /// <summary>
        /// верхняя линия боллинжера
        /// </summary>
        public List<decimal> ValuesUp
        { get; set; }

        /// <summary>
        /// нижняя линия боллинджера
        /// </summary>
        public List<decimal> ValuesDown
        { get; set; }

        /// <summary>
        /// уникальное имя индикатора
        /// </summary>
        public string Name
        { get; set; }

        /// <summary>
        /// длина расчёта индикатора
        /// </summary>
        public int Lenght
        { get; set; }

        /// <summary>
        /// отклонение
        /// </summary>
        public int Deviation
        { get; set; }

        /// <summary>
        /// цвет верхней серии данных
        /// </summary>
        public Color ColorUp
        { get; set; }

        /// <summary>
        /// цвет нижней серии данных
        /// </summary>
        public Color ColorDown
        { get; set; }

        /// <summary>
        /// включена ли прорисовка индикатора
        /// </summary>
        public bool PaintOn
        { get; set; }

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
                    Lenght = Convert.ToInt32(reader.ReadLine());
                    Deviation = Convert.ToInt32(reader.ReadLine());
                    PaintOn = Convert.ToBoolean(reader.ReadLine());
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
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @".txt", false))
                {
                    writer.WriteLine(ColorUp.ToArgb());
                    writer.WriteLine(ColorDown.ToArgb());
                    writer.WriteLine(Lenght);
                    writer.WriteLine(Deviation);
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
        /// удалить файл настроек
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
            if (ValuesUp != null)
            {
                ValuesUp.Clear();
                ValuesDown.Clear();
            }
            _myCandles = null;
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            BollingerUi ui = new BollingerUi(this);
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
        /// прогрузить новыми свечками
        /// </summary>
        public void Process(List<Candle> candles)
        {
            _myCandles = candles;
            if (ValuesDown != null &&
                ValuesDown.Count + 1 == candles.Count)
            {
                ProcessOne(candles);
            }
            else if (ValuesDown != null &&
                ValuesDown.Count == candles.Count)
            {
                ProcessLast(candles);
            }
            else
            {
                ProcessAll(candles);
            }
        }

        /// <summary>
        /// необходимо перерисовать индикатор
        /// </summary>
        public event Action<IIndicatorCandle> NeadToReloadEvent;

        /// <summary>
        /// свечи по которым строиться индикатор
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// прогрузить только последнюю свечку
        /// </summary>
        private void ProcessOne(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }
            if (ValuesDown == null)
            {
                ValuesUp = new List<decimal>();
                ValuesDown = new List<decimal>();

                decimal[] value = GetValueSimple(candles, candles.Count - 1);

                ValuesUp.Add(value[0]);
                ValuesDown.Add(value[1]);
            }
            else
            {
                decimal[] value = GetValueSimple(candles, candles.Count - 1);

                ValuesUp.Add(value[0]);
                ValuesDown.Add(value[1]);
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
            ValuesUp = new List<decimal>();
            ValuesDown = new List<decimal>();

            decimal[][] newValues = new decimal[candles.Count][];

            for (int i = 0; i < candles.Count; i++)
            {
                newValues[i] = GetValueSimple(candles, i);
            }

            for (int i = 0; i < candles.Count; i++)
            {
                ValuesUp.Add(newValues[i][0]);
            }

            for (int i = 0; i < candles.Count; i++)
            {
                ValuesDown.Add(newValues[i][1]);
            }
        }

        /// <summary>
        /// перегрузить последнюю ячейку
        /// </summary>
        private void ProcessLast(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }
            decimal[] value = GetValueSimple(candles, candles.Count - 1);
            ValuesUp[ValuesUp.Count - 1] = value[0];
            ValuesDown[ValuesDown.Count - 1] = value[1];
        }

        /// <summary>
        /// взять значение индикатора по индексу
        /// </summary>
        private decimal[] GetValueSimple(List<Candle> candles, int index)
        {
            if (index - Lenght <= 0)
            {
                return new decimal[2];
            }

            decimal [] bollinger = new decimal[2];

// 1 считаем СМА

            decimal valueSma = 0;

            for (int i = index - Lenght + 1; i < index + 1; i++)
            {// бежим по прошлым периодам и собираем значения
                valueSma += candles[i].Close;
            }

            valueSma = valueSma / Lenght;

// 2 считаем среднее отклонение

            // находим массив отклонений от средней
            decimal[] valueDev = new decimal[Lenght];
            for (int i = index - Lenght + 1, i2 = 0; i < index + 1; i++, i2++)
            {
                // бежим по прошлым периодам и собираем значения
                valueDev[i2] = candles[i].Close - valueSma;
            }

            // возводим этот массив в квадрат
            for (int i = 0; i < valueDev.Length; i++)
            {
                valueDev[i] = Convert.ToDecimal(Math.Pow(Convert.ToDouble(valueDev[i]), 2));
            }

            // складываем

            double summ = 0;

            for (int i = 0; i < valueDev.Length; i++)
            {
                summ += Convert.ToDouble(valueDev[i]);
            }

            //делим полученную сумму на количество элементов в выборке (или на n-1, если n>30)
            if (Lenght > 30)
            {
                summ = summ/(Lenght - 1);
            }
            else
            {
                summ = summ/Lenght;
            }
            // вычисляем корень

            summ = Math.Sqrt(summ);

// 3 считаем линии боллинжера

            bollinger[0] = Math.Round(valueSma + Convert.ToDecimal(summ) * Deviation,6);

            bollinger[1] = Math.Round(valueSma -Convert.ToDecimal(summ) * Deviation,6);

            return bollinger;
        }
    }
}
