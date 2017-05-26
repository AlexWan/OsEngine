using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsEngine.Entity;

namespace OsEngine.Charts.CandleChart.Indicators
{
    public class PivotPoints: IIndicatorCandle
    {
        /// <summary>
        /// конструктор с параметрами, индикатор сохраняется
        /// </summary>
        /// <param name="uniqName"></param>
        /// <param name="canDelete"></param>
        public PivotPoints(string uniqName, bool canDelete)
        {
            Name = uniqName;

            TypeIndicator = IndicatorOneCandleChartType.Line;

            ColorP = Color.LawnGreen;

            ColorS1 = Color.DarkRed;
            ColorS2 = Color.DarkRed;
            ColorS3 = Color.DarkRed;


            ColorR1 = Color.DodgerBlue;
            ColorR2 = Color.DodgerBlue;
            ColorR3 = Color.DodgerBlue;

            PaintOn = true;
            CanDelete = canDelete;
            Load();
        }
        /// <summary>
        ///  конструктор без параметра, индикатор не сохраняется
        /// </summary>
        /// <param name="canDelete"></param>
        public PivotPoints(bool canDelete)
        {
            TypeIndicator = IndicatorOneCandleChartType.Line;

            ColorP = Color.LawnGreen;

            ColorS1 = Color.DarkRed;
            ColorS2 = Color.DarkRed;
            ColorS3 = Color.DarkRed;

            ColorR1 = Color.DodgerBlue;
            ColorR2 = Color.DodgerBlue;
            ColorR3 = Color.DodgerBlue;

            PaintOn = true;
            CanDelete = canDelete;
        }
        /// <summary>
        /// можно ли удалить индикатор с графика. Это нужно для того чтобы у роботов нельзя было удалить 
        /// индикаторы которые ему нужны в торговле
        /// </summary>
        public bool CanDelete { get; set; }

        /// <summary>
        /// все значения индикатора
        /// </summary>
        List<List<decimal>> IIndicatorCandle.ValuesToChart
        {
            get
            {
                List<List<decimal>> list = new List<List<decimal>>();
                list.Add(ValuesS1);
                list.Add(ValuesS2);
                list.Add(ValuesS3);

                list.Add(ValuesPivot);

                list.Add(ValuesR1);
                list.Add(ValuesR2);
                list.Add(ValuesR3);
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
                colors.Add(ColorS1);
                colors.Add(ColorS2);
                colors.Add(ColorS3);

                colors.Add(ColorP);

                colors.Add(ColorR1);
                colors.Add(ColorR2);
                colors.Add(ColorR3);

                return colors;
            }
        }

        #region Values
        
        /// <summary>
        /// основной уровень индикатора
        /// </summary>
        public List<decimal> ValuesPivot { get; set; }

        /// <summary>
        /// далее идут уровни сопротивления, чем меньше цифра в конце имени тем уровень
        /// ближе к основному
        /// </summary>
        public List<decimal> ValuesR1 { get; set; }
        public List<decimal> ValuesR2 { get; set; }
        public List<decimal> ValuesR3 { get; set; }

        /// <summary>
        /// далее идут уровни поддержки, чем меньше цифра в конце имени тем уровень
        /// ближе к основному
        /// </summary>
        public List<decimal> ValuesS1 { get; set; }
        public List<decimal> ValuesS2 { get; set; }
        public List<decimal> ValuesS3 { get; set; }
        #endregion


        /// <summary>
        /// уникальное имя индикатора
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// имя области данных на которой прорисовывается индикатор
        /// </summary>
        public string NameArea { get; set; }

        /// <summary>
        /// имя серии данных на которой прорисовывается индикатор
        /// </summary>
        public string NameSeries { get; set; }

        /// <summary>
        /// включена ли прорисовка индикатора
        /// </summary>
        public bool PaintOn { get; set; }

        /// <summary>
        /// тип индикатора
        /// </summary>
        public IndicatorOneCandleChartType TypeIndicator { get; set; }

        #region Colors
        public Color ColorS1 { get; set; }

        public Color ColorS3 { get; set; }

        public Color ColorS2 { get; set; }

        public Color ColorR1 { get; set; }

        public Color ColorR2 { get; set; }

        public Color ColorR3 { get; set; }

        public Color ColorP { get; set; }
        #endregion
        
        /// <summary>
        /// нужно перерисовать индикатор
        /// </summary>
        public event Action<IIndicatorCandle> NeadToReloadEvent;

        /// <summary>
        /// свечи для которых рассчитывается индикатор
        /// </summary>
        private List<Candle> _myCandles;

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

                    writer.WriteLine(ColorP);

                    writer.WriteLine(ColorS1);
                    writer.WriteLine(ColorS2);
                    writer.WriteLine(ColorS3);

                    writer.WriteLine(ColorR1);
                    writer.WriteLine(ColorR2);
                    writer.WriteLine(ColorR3);

                    writer.WriteLine(PaintOn);
                    writer.Close();
                }
            }
            catch (Exception)
            {

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
                    ColorP = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));

                    ColorS1 = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorS2 = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorS3 = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));

                    ColorR1 = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorR2 = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorR3 = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));

                    PaintOn = Convert.ToBoolean(reader.ReadLine());
                    MovingAverageTypeCalculation type;
                    Enum.TryParse(reader.ReadLine(), true, out type);


                    reader.Close();
                }


            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удалить данные
        /// </summary>
        public void Clear()
        {
            if (ValuesPivot != null)
            {
                ValuesS1.Clear();
                ValuesS2.Clear();
                ValuesS3.Clear();

                ValuesPivot.Clear();

                ValuesR1.Clear();
                ValuesR2.Clear();
                ValuesR3.Clear();

            }
            _myCandles = null;
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            PivotPointsUi ui = new PivotPointsUi(this);
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
        /// рассчитать индикатор
        /// </summary>
        public void Process(List<Candle> candles)
        {
            _myCandles = candles;

            if (ValuesPivot != null &&
                ValuesPivot.Count + 1 == candles.Count && index.Count >= 2)
            {
                ProcessOne(candles);
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

            int _timeUpdateH = candles[candles.Count - 1].TimeStart.Hour;
            int _timeUpdateM = candles[candles.Count - 1].TimeStart.Minute;

            if (candles.Count != 1 && _timeUpdateH == 10 && _timeUpdateM == 00)
            {
                index.Add(candles.Count - 1);
                Reload(candles, index);
            }


            ValuesR1.Add(_r1);
            ValuesR2.Add(_r2);
            ValuesR3.Add(_r3);

            ValuesPivot.Add(_pivot);

            ValuesS1.Add(_s1);
            ValuesS2.Add(_s2);
            ValuesS3.Add(_s3);



        }

        private List<int> index;

        /// <summary>
        /// прогрузить с самого начала
        /// </summary>
        private void ProcessAll(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }

            ValuesR1 = new List<decimal>();
            ValuesR2 = new List<decimal>();
            ValuesR3 = new List<decimal>();

            ValuesPivot = new List<decimal>();

            ValuesS1 = new List<decimal>();
            ValuesS2 = new List<decimal>();
            ValuesS3 = new List<decimal>();

            // индексы свечей, начинающих торговый день
            index = new List<int>();

            List<Candle> newCandles = new List<Candle>();

            int count = 0;

            for (int i = 0; i < candles.Count; i++)
            {
                newCandles.Add(candles[i]);

                int _timeUpdateH = candles[i].TimeStart.Hour;
                int _timeUpdateM = candles[i].TimeStart.Minute;

                if (_timeUpdateH == 10 && _timeUpdateM == 00)
                {
                    index.Add(i);

                    count++;

                    if (count >= 2)
                       Reload(newCandles, index);
                }
                
                
                ValuesR1.Add(_r1);
                ValuesR2.Add(_r2);
                ValuesR3.Add(_r3);

                ValuesPivot.Add(_pivot);

                ValuesS1.Add(_s1);
                ValuesS2.Add(_s2);
                ValuesS3.Add(_s3);

            }
        }

        /// <summary>
        /// переменные для расчета индикатора
        /// </summary>
        private decimal _r1;
        private decimal _r2;
        private decimal _r3;

        private decimal _pivot;

        private decimal _s1;
        private decimal _s2;
        private decimal _s3;

        /// <summary>
        /// обновить значения индикатора
        /// </summary>
        /// <param name="newCandles"></param>
        /// <param name="index"></param>
        private void Reload(List<Candle> newCandles, List<int> index)
        {
            decimal H = 0;

            decimal L = decimal.MaxValue;

            decimal C = 0;

            

            for (int i = index[index.Count-2]; i < index[index.Count - 1]; i++)
            {
                if (H < newCandles[i].High)
                    H = newCandles[i].High;

                if (L > newCandles[i].Low)
                    L = newCandles[i].Low;

                C = newCandles[i].Close;
            }

            // расчет уровней индикатора
            _pivot = (H + L + C) / 3;

            _r1 = 2 * _pivot - L;
            _s1 = 2 * _pivot - H;
            _r2 = _pivot + (_r1 - _s1);
            _s2 = _pivot - (_r1 - _s1);
            _r3 = H + 2 * (_pivot - L);
            _s3 = L - 2 * (H - _pivot);
        }

    }
}
