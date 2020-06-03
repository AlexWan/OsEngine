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
    public class Pivot: IIndicator
    {
        /// <summary>
        /// constructor with parameters. Indicator will be saved
        /// конструктор с параметрами. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">unique name/уникальное имя</param>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public Pivot(string uniqName, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorChartPaintType.Line;

            ColorP = Color.LawnGreen;

            ColorS1 = Color.DarkRed;
            ColorS2 = Color.DarkRed;
            ColorS3 = Color.DarkRed;
            ColorS4 = Color.DarkRed;

            ColorR1 = Color.DodgerBlue;
            ColorR2 = Color.DodgerBlue;
            ColorR3 = Color.DodgerBlue;
            ColorR4 = Color.DodgerBlue;

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
        public Pivot(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            TypeIndicator = IndicatorChartPaintType.Line;

            ColorP = Color.LawnGreen;

            ColorS1 = Color.DarkRed;
            ColorS2 = Color.DarkRed;
            ColorS3 = Color.DarkRed;
            ColorS4 = Color.DarkRed;

            ColorR1 = Color.DodgerBlue;
            ColorR2 = Color.DodgerBlue;
            ColorR3 = Color.DodgerBlue;
            ColorR4 = Color.DodgerBlue;

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
                list.Add(ValuesS1);
                list.Add(ValuesS2);
                list.Add(ValuesS3);
                list.Add(ValuesS4);
                list.Add(ValuesR1);
                list.Add(ValuesR2);
                list.Add(ValuesR3);
                list.Add(ValuesR4);
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
                colors.Add(ColorS1);
                colors.Add(ColorS2);
                colors.Add(ColorS3);
                colors.Add(ColorS4);
                colors.Add(ColorR1);
                colors.Add(ColorR2);
                colors.Add(ColorR3);
                colors.Add(ColorR4);

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
        /// имя серии на графике для прорисовки индикатора
        /// </summary>
        public string NameSeries { get; set; }

        /// <summary>
        /// name of data area where indicator will be drawn
        /// имя области на графике для прорисовки индикатора
        /// </summary>
        public string NameArea { get; set; }

        /// <summary>
        /// first resistance
        ///   первое сопротивление
        /// </summary> 
        public List<decimal> ValuesR1 
        { get; set; }

        /// <summary>
        /// first support
        /// первая поддержка
        /// </summary>
        public List<decimal> ValuesS1 
        { get; set; }

        /// <summary>
        /// second resistance
        ///   второе сопротивление
        /// </summary> 
        public List<decimal> ValuesR2
        { get; set; }

        /// <summary>
        /// second support
        /// вторая поддержка
        /// </summary>
        public List<decimal> ValuesS2
        { get; set; }

        /// <summary>
        /// third resistance
        ///   третье сопротивление
        /// </summary> 
        public List<decimal> ValuesR3
        { get; set; }

        /// <summary>
        /// fourth resistance
        ///   четвёртое сопротивление
        /// </summary> 
        public List<decimal> ValuesR4
        { get; set; }

        /// <summary>
        /// third support
        /// третья поддержка
        /// </summary>
        public List<decimal> ValuesS3
        { get; set; }

        /// <summary>
        /// fourth support
        /// четвёртая поддержка
        /// </summary>
        public List<decimal> ValuesS4
        { get; set; }

        /// <summary>
        /// unique indicator name
        /// уникальное имя
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// color of first resistance
        /// цвет сопротивления 1
        /// </summary>
        public Color ColorR1 { get; set; }

        /// <summary>
        /// color of second resistance
        /// цвет сопротивления 2
        /// </summary>
        public Color ColorR2 { get; set; }

        /// <summary>
        /// color of third resistance
        /// цвет сопротивления 3
        /// </summary>
        public Color ColorR3 { get; set; }

        /// <summary>
        /// color of fourth resistance
        /// цвет сопротивления 4
        /// </summary>
        public Color ColorR4 { get; set; }

        /// <summary>
        /// top line color
        /// цвет верхней линии
        /// </summary>
        public Color ColorP { get; set; }

        /// <summary>
        /// color of first support
        /// цвет поддержки 1
        /// </summary>
        public Color ColorS1 { get; set; }

        /// <summary>
        /// color of second support
        /// цвет поддержки 2
        /// </summary>
        public Color ColorS2 { get; set; }

        /// <summary>
        /// color of third support
        /// цвет поддержки 3
        /// </summary>
        public Color ColorS3 { get; set; }

        /// <summary>
        /// color of fourth support
        /// цвет поддержки 4
        /// </summary>
        public Color ColorS4 { get; set; }

        /// <summary>
        /// candles to calculate indicator
        /// вкллючена ли прорисовка индикатора на графике
        /// </summary>
        public bool PaintOn { get; set; }

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

                    writer.WriteLine(ColorP);

                    writer.WriteLine(ColorS1);
                    writer.WriteLine(ColorS2);
                    writer.WriteLine( ColorS3);
                    writer.WriteLine(ColorS4);

                    writer.WriteLine(ColorR1);
                    writer.WriteLine(ColorR2);
                    writer.WriteLine(ColorR3);
                    writer.WriteLine(ColorR4);

                    writer.WriteLine(PaintOn);
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
                    ColorP = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));

                    ColorS1 = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorS2 = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorS3 = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorS4 = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));

                    ColorR1 = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorR2 = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorR3 = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorR4 = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));

                    PaintOn = Convert.ToBoolean(reader.ReadLine());
                    MovingAverageTypeCalculation type;
                    Enum.TryParse(reader.ReadLine(), true,out type);


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
            if (ValuesS1 != null)
            {
                ValuesS1.Clear();
                ValuesS2.Clear();
                ValuesS3.Clear();
                ValuesS4.Clear();
                ValuesR1.Clear();
                ValuesR2.Clear();
                ValuesR3.Clear();
                ValuesR4.Clear();
            }
            _myCandles = null;
        }

        /// <summary>
        /// display settings window
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            PivotUi ui = new PivotUi(this);
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

            if (ValuesS1 != null &&
                ValuesS1.Count + 1 == candles.Count)
            {
                ProcessOne(candles);
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
            if (ValuesS1 == null)
            {
                ValuesS1 = new List<decimal>();
                ValuesR1 = new List<decimal>();
                ValuesR2 = new List<decimal>();
                ValuesR3 = new List<decimal>();
                ValuesR4 = new List<decimal>();
                ValuesS1 = new List<decimal>();
                ValuesS2 = new List<decimal>();
                ValuesS3 = new List<decimal>();
                ValuesS4 = new List<decimal>();
            }

            if (candles.Count != 1 &&
                candles[candles.Count - 1].TimeStart.Day != candles[candles.Count - 2].TimeStart.Day &&
                _lastTimeUpdete != candles[candles.Count - 1].TimeStart)
            {
                _lastTimeUpdete = candles[candles.Count - 1].TimeStart;
                Reload(candles);
            }

            ValuesR1.Add(_r1);
            ValuesR2.Add(_r2);
            ValuesR3.Add(_r3);
            ValuesR4.Add(_r4);
            ValuesS1.Add(_s1);
            ValuesS2.Add(_s2);
            ValuesS3.Add(_s3);
            ValuesS4.Add(_s4);
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

            ValuesR1 = new List<decimal>();
            ValuesR2 = new List<decimal>();
            ValuesR3 = new List<decimal>();
            ValuesR4 = new List<decimal>();
            ValuesS1 = new List<decimal>();
            ValuesS2 = new List<decimal>();
            ValuesS3 = new List<decimal>();
            ValuesS4 = new List<decimal>();

            List<Candle> newCandles = new List<Candle>();

            for (int i = 0; i < candles.Count; i++)
            {
                newCandles.Add(candles[i]);
                if (newCandles.Count != 1 &&
                    newCandles[newCandles.Count - 1].TimeStart.Day != newCandles[newCandles.Count - 2].TimeStart.Day &&
                    _lastTimeUpdete != newCandles[newCandles.Count - 1].TimeStart)
                {
                    _lastTimeUpdete = newCandles[newCandles.Count - 1].TimeStart;
                    Reload(newCandles);
                }

                ValuesR1.Add(_r1);
                ValuesR2.Add(_r2);
                ValuesR3.Add(_r3);
                ValuesR4.Add(_r4);
                ValuesS1.Add(_s1);
                ValuesS2.Add(_s2);
                ValuesS3.Add(_s3);
                ValuesS4.Add(_s4);
            }

        }


        private void Reload(List<Candle> candles)
        {
            /*
             
RANGE = H — L

R1 = C + RANGE * 1.1/12

R2 = C + RANGE * 1.1/6

R3 = C + RANGE * 1.1/4

R4 = C + RANGE * 1.1/2

S1 = C — RANGE * 1.1/12

S2 = C — RANGE * 1.1/6

S3 = C — RANGE * 1.1/4

S4 = C — RANGE * 1.1/2
            
             */

            decimal H = 0;

            decimal L = decimal.MaxValue;

            decimal C = 0;

            for (int i = candles.Count - 2; i > 0; i++)
            {
                if (C == 0)
                {
                    C = candles[i].Close;
                }

                if (candles[i].High > H)
                {
                    H = candles[i].High;
                }

                if (candles[i].Low < L)
                {
                    L = candles[i].Low;
                }

                if (i != 1 && candles[i].TimeStart.Day != candles[i - 1].TimeStart.Day)
                {
                    break;
                }

            }

            decimal range = H - L;

            _r1 = C + range * 1.1m / 12;
            _r2 = C + range * 1.1m / 6;
            _r3 = C + range * 1.1m / 4;
            _r4 = C + range * 1.1m / 2;

            _s1 = C - range * 1.1m / 12;
            _s2 = C - range * 1.1m / 6;
            _s3 = C - range * 1.1m / 4;
            _s4 = C - range * 1.1m / 2;
        }

        private DateTime _lastTimeUpdete;

        private decimal _r1;
        private decimal _r2;
        private decimal _r3;
        private decimal _r4;

        private decimal _s1;
        private decimal _s2;
        private decimal _s3;
        private decimal _s4;
    }
}