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
    /// Тип рассчёта индикаторм Скользящая средняя
    /// </summary>

    /// <summary>
    /// какая цена свечи берётся при построении
    /// </summary>
    public enum StandardDeviationTypePoints
    {
        /// <summary>
        /// открытие
        /// </summary>
        Open,

        /// <summary>
        /// максимум
        /// </summary>
        High,

        /// <summary>
        /// минимум
        /// </summary>
        Low,

        /// <summary>
        /// закрытие
        /// </summary>
        Close,

        /// <summary>
        /// медиана. (High + Low) / 2
        /// </summary>
        Median,

        /// <summary>
        /// типичная цена (High + Low + Close) / 3
        /// </summary>
        Typical
    }

    /// <summary>
    /// Standard Deviation. Индикатор Среднеквадратическое отклонение
    /// </summary>
    public class StandardDeviation : IIndicatorCandle
    {

        /// <summary>
        /// конструктор с параметрами. Индикатор будет сохранять настройки
        /// </summary>
        /// <param name="uniqName">уникальное имя</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public StandardDeviation(string uniqName, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorOneCandleChartType.Line;
            TypePointsToSearch = StandardDeviationTypePoints.Close;
            ColorBase = Color.DeepSkyBlue;
            Lenght = 20;
            PaintOn = true;
            CanDelete = canDelete;
            Load();
        }

        /// <summary>
        /// конструктор без параметров. Индикатор не будет сохраняться
        /// </summary>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public StandardDeviation(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();
            TypeIndicator = IndicatorOneCandleChartType.Line;
            TypePointsToSearch = StandardDeviationTypePoints.Close;
            ColorBase = Color.DeepSkyBlue;
            Lenght = 20;
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
        /// по какой точке будет строиться индикатор по: Open, Close ...
        /// </summary>
        public StandardDeviationTypePoints TypePointsToSearch;

        /// <summary>
        /// имя серии данных на которой индикатор прорисовывается
        /// </summary>
        public string NameSeries { get; set; }

        /// <summary>
        /// имя области данных на которой индикатор прорисовывается
        /// </summary>
        public string NameArea { get; set; }

        /// <summary>
        /// значение Standard Deviation
        /// </summary>
        public List<decimal> Values { get; set; }

        /// <summary>
        /// уникальное имя индикатора
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// длинна рассчёта индикатора
        /// </summary>
        public int Lenght { get; set; }

        /// <summary>
        /// цвет линии индикатора Standard Deviation
        /// </summary>
        public Color ColorBase { get; set; }

        /// <summary>
        /// включена ли прорисовка серии на чарте
        /// </summary>
        public bool PaintOn { get; set; }

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
                    ColorBase = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    Lenght = Convert.ToInt32(reader.ReadLine());
                    PaintOn = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out TypePointsToSearch);
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
                    writer.WriteLine(TypePointsToSearch);
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
        /// свечи по которым строится индикатор
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// показать окно с настройками
        /// </summary>
        public void ShowDialog()
        {
            StandardDeviationUi ui = new StandardDeviationUi(this);
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
        /// необходимо перерисовать индикатор на графике
        /// </summary>
        public event Action<IIndicatorCandle> NeadToReloadEvent;

        /// <summary>
        /// взять значение индикаторм по индексу
        /// </summary>
        private decimal GetValueStandardDeviation(List<Candle> candles, int index)
        {

            //int Lenght3 = 20;

            if (index - Lenght <= 0)
            {
                return 0;
            }

            decimal sd = 0;

            int lenght2;
            if (index - Lenght <= Lenght) lenght2 = index - Lenght; else lenght2 = Lenght;

            decimal sum = 0;
            for (int j = index; j > index - Lenght; j--)
            {
                sum += GetPoint(candles, j);
            }

            var m = sum / Lenght;

            for (int i = index; i > index - lenght2; i--)
            {
                decimal x = GetPoint(candles, i) - m;  // разница между значениями за период и средней
                double g = Math.Pow((double)x, 2.0);   // квадрат зницы
                sd += (decimal)g;   // сумма квадратов
            }
            sd = (decimal)Math.Sqrt((double)sd / lenght2);  // находим корень из суммы/период 

            return Math.Round(sd, 5);

        }

        /// <summary>
        /// взять значения точки для рассчёта данных
        /// </summary>
        /// <param name="candles">свечи</param>
        /// <param name="index">индекс</param>
        /// <returns>значение точки по индексу</returns>
        private decimal GetPoint(List<Candle> candles, int index)
        {
            if (TypePointsToSearch == StandardDeviationTypePoints.Close)
            {
                return candles[index].Close;
            }
            else if (TypePointsToSearch == StandardDeviationTypePoints.High)
            {
                return candles[index].High;
            }
            else if (TypePointsToSearch == StandardDeviationTypePoints.Low)
            {
                return candles[index].Low;
            }
            else if (TypePointsToSearch == StandardDeviationTypePoints.Open)
            {
                return candles[index].Open;
            }
            else if (TypePointsToSearch == StandardDeviationTypePoints.Median)
            {
                return (candles[index].High + candles[index].Low) / 2;
            }
            else if (TypePointsToSearch == StandardDeviationTypePoints.Typical)
            {
                return (candles[index].High + candles[index].Low + candles[index].Close) / 3;
            }
            return 0;
        }

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
            if (Values != null && Values.Count + 1 == candles.Count)
            {
                ProcessOne(candles);
            }
            else if (Values != null && Values.Count == candles.Count)
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
            if (candles == null) return;

            if (Values == null) Values = new List<decimal>();

            Values.Add(GetValueStandardDeviation(candles, candles.Count - 1));
        }

        /// <summary>
        /// прогрузить с самого начала
        /// </summary>
        private void ProcessAll(List<Candle> candles)
        {
            if (candles == null) return;

            Values = new List<decimal>();

            for (int i = 0; i < candles.Count; i++)
            {
                Values.Add(GetValueStandardDeviation(candles, i));

            }
        }

        /// <summary>
        /// перегрузить последнее значение
        /// </summary>
        private void ProcessLast(List<Candle> candles)
        {
            if (candles == null) return;

            Values[Values.Count - 1] = GetValueStandardDeviation(candles, candles.Count - 1);
        }

    }
}
