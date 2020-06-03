/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Indicator Alligator. Bill Williams
    /// Индикатор Alligator. Билла Вильямса
    /// </summary>
    public class Alligator : IIndicator
    {
        /// <summary>
        /// constructor with parameters.Indicator will be saved
        /// конструктор с параметрами. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">unique name/уникальное имя</param>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public Alligator(string uniqName, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorChartPaintType.Line;

            LenghtBase = 8;
            ShiftBase = 5;
            ColorBase = Color.DarkRed;

            LenghtUp = 5;
            ShiftUp = 3;
            ColorUp = Color.LawnGreen;

            LenghtDown = 13;
            ShiftDown = 8;
            ColorDown = Color.DodgerBlue;
            
            PaintOn = true;
            TypeCalculationAverage = MovingAverageTypeCalculation.Smoofed;
            CanDelete = canDelete;
            Load();
        }

        /// <summary>
        /// constructor without parameters.Indicator will not saved/конструктор без параметров. Индикатор не будет сохраняться
        /// used ONLY to create composite indicators/используется ТОЛЬКО для создания составных индикаторов
        /// Don't use it from robot creation layer/не используйте его из слоя создания роботов!
        /// </summary>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public Alligator(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            TypeIndicator = IndicatorChartPaintType.Line;

            LenghtBase = 8;
            ShiftBase = 5;
            ColorBase = Color.DarkRed;

            LenghtUp = 5;
            ShiftUp = 3;
            ColorUp = Color.LawnGreen;

            LenghtDown = 13;
            ShiftDown = 8;
            ColorDown = Color.DodgerBlue;

            PaintOn = true;
            TypeCalculationAverage = MovingAverageTypeCalculation.Smoofed;
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
                list.Add(ValuesUp);
                list.Add(Values);
                list.Add(ValuesDown);
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
                colors.Add(ColorUp);
                colors.Add(ColorBase);
                colors.Add(ColorDown);
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
        /// type of moving average for indicator calculation
        /// тип скользящей средней для рассчёта индикатора
        /// </summary>
        private MovingAverageTypeCalculation _typeCalculationAverage;

        /// <summary>
        /// type of moving average for indicator calculation
        /// тип скользящей средней для рассчёта индикатора
        /// </summary>
        public MovingAverageTypeCalculation TypeCalculationAverage
        {
            get { return _typeCalculationAverage; }
            set
            {
                _typeCalculationAverage = value;

                Values = null;
                ValuesUp = null;
                ValuesDown = null;

                _maUp = new MovingAverage(false)
                {
                    TypeCalculationAverage = value,
                    Lenght = LenghtUp,
                };
                _maDown = new MovingAverage(false)
                {
                    TypeCalculationAverage = value,
                    Lenght = LenghtDown,
                };
                _maBase = new MovingAverage(false)
                {
                    TypeCalculationAverage = value,
                    Lenght = LenghtBase,
                };
            }
        }

        /// <summary>
        /// series name on chart to draw indicator
        /// имя серии на графике для прорисовки индикатора
        /// </summary>
        public string NameSeries { get; set; }

        /// <summary>
        /// area name on chart for drawing indicator
        /// имя области на графике для прорисовки индикатора
        /// </summary>
        public string NameArea { get; set; }

        /// <summary>
        /// middle line. Teeth
        /// средняя линия. Зубы
        /// </summary>
        public List<decimal> Values
        { get; set; }

        /// <summary>
        /// fast line. Lips
        /// быстрая линия. Губы
        /// </summary>
        public List<decimal> ValuesUp 
        { get; set; }

        /// <summary>
        /// slow line. Jaw
        /// медленная линия. Челюсть
        /// </summary>
        public List<decimal> ValuesDown { get; set; }

        /// <summary>
        /// unique indicator name
        /// уникальное имя
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// bottom line color
        /// цвет нижней линии
        /// </summary>
        public Color ColorDown { get; set; }

        /// <summary>
        /// headline color
        /// цвет верхней линии
        /// </summary>
        public Color ColorUp { get; set; }

        /// <summary>
        /// centerline colour
        /// цвет центральной линии
        /// </summary>
        public Color ColorBase { get; set; }

        /// <summary>
        /// speed line length
        /// длинна скоростной линии
        /// </summary>
        public int LenghtUp;

        /// <summary>
        /// centerline length
        /// длинна центральной линии
        /// </summary>
        public int LenghtBase;

        /// <summary>
        /// slow line length
        /// длинна медленной линии
        /// </summary>
        public int LenghtDown;

        /// <summary>
        /// topline shift
        /// сдвиг верхней линии
        /// </summary>
        public int ShiftUp;

        /// <summary>
        /// centerline shift
        /// сдвиг центральной линии
        /// </summary>
        public int ShiftBase;

        /// <summary>
        /// bottom line shift
        /// сдвиг нижней линии
        /// </summary>
        public int ShiftDown;

        /// <summary>
        /// whether indicator repainting on chart enabled
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
                    writer.WriteLine(LenghtBase);
                    writer.WriteLine(ShiftBase);
                    writer.WriteLine(ColorBase.ToArgb());

                    writer.WriteLine(LenghtUp);
                    writer.WriteLine(ShiftUp);
                    writer.WriteLine(ColorUp.ToArgb());

                    writer.WriteLine(LenghtDown);
                    writer.WriteLine(ShiftDown);
                    writer.WriteLine(ColorDown.ToArgb());
                    
                    writer.WriteLine(PaintOn);
                    writer.WriteLine(TypeCalculationAverage);
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
                    LenghtBase = Convert.ToInt32(reader.ReadLine());
                    ShiftBase = Convert.ToInt32(reader.ReadLine());
                    ColorBase = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));

                    LenghtUp = Convert.ToInt32(reader.ReadLine());
                    ShiftUp = Convert.ToInt32(reader.ReadLine());
                    ColorUp = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));

                    LenghtDown = Convert.ToInt32(reader.ReadLine());
                    ShiftDown = Convert.ToInt32(reader.ReadLine());
                    ColorDown = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));

                    PaintOn = Convert.ToBoolean(reader.ReadLine());
                    MovingAverageTypeCalculation type;
                    Enum.TryParse(reader.ReadLine(), true, out type);

                    TypeCalculationAverage = type;

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
                ValuesDown.Clear();
                ValuesUp.Clear();
            }

            _myCandles = null;
        }

        /// <summary>
        /// display settings window
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            AlligatorUi ui = new AlligatorUi(this);
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
                Values.Count + 1 == candles.Count + ShiftBase)
            {
                ProcessOne(candles);
            }
            else if (Values != null &&
                     Values.Count == candles.Count + ShiftBase)
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
                ValuesUp = new List<decimal>();
                ValuesDown = new List<decimal>();
            }

            Values.Add(GetValueBase(candles, candles.Count - 1));
            ValuesUp.Add(GetValueUp(candles, candles.Count - 1));
            ValuesDown.Add(GetValueDown(candles, candles.Count - 1));
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

            Values = new List<decimal>(Enumerable.Repeat<decimal>(0, ShiftBase).ToList());
            ValuesUp = new List<decimal>(Enumerable.Repeat<decimal>(0, ShiftUp).ToList());
            ValuesDown = new List<decimal>(Enumerable.Repeat<decimal>(0, ShiftDown).ToList());

            List<Candle> newCandles = new List<Candle>();

            for (int i = 0; i < candles.Count; i++)
            {
                newCandles.Add(candles[i]);
                Values.Add(GetValueBase(newCandles, i));
                ValuesUp.Add(GetValueUp(newCandles, i));
                ValuesDown.Add(GetValueDown(newCandles, i));
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

            Values[Values.Count - 1] = GetValueBase(candles, candles.Count - 1);
            ValuesUp[ValuesUp.Count - 1] = GetValueUp(candles, candles.Count - 1);
            ValuesDown[ValuesDown.Count - 1] = GetValueDown(candles, candles.Count - 1);
        }

        /// <summary>
        /// upper MA
        /// верхняя машка
        /// </summary>
        private MovingAverage _maUp;

        /// <summary>
        /// central MA
        /// центральная
        /// </summary>
        private MovingAverage _maBase;

        /// <summary>
        /// bottom MA
        /// нижняя
        /// </summary>
        private MovingAverage _maDown;

        /// <summary>
        /// take upper value
        /// взять значение верхнее
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        /// <param name="index">index/индекс</param>
        /// <returns>alligator top line by index/верхняя линия аллигатора по индексу</returns>
        private decimal GetValueUp(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                _maUp = new MovingAverage(false)
                {
                    TypeCalculationAverage = TypeCalculationAverage,
                    Lenght = LenghtUp,
                    TypePointsToSearch = PriceTypePoints.Median
                };
            }

            _maUp.Process(candles);

            if (_maUp.Values.Count - 1 <= ShiftUp)
            {
                return 0;
            }

            return _maUp.Values[_maUp.Values.Count - 1];
        }

        /// <summary>
        /// take center value
        /// взять значение центральное
        /// </summary>
        /// <param name="candles">canldles/свечи</param>
        /// <param name="index">index/индекс</param>
        /// <returns>alligator centerline by index/центальная линия аллигатора по индексу</returns>
        private decimal GetValueBase(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                _maBase = new MovingAverage(false)
                {
                    TypeCalculationAverage = TypeCalculationAverage,
                    Lenght = LenghtBase,
                    TypePointsToSearch = PriceTypePoints.Median
                };
            }

            _maBase.Process(candles);

            if (_maBase.Values.Count - 1 <= ShiftBase)
            {
                return 0;
            }

            return _maBase.Values[_maBase.Values.Count - 1];
        }

        /// <summary>
        /// take lower value
        /// взять значение нижнее
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        /// <param name="index">index/индекс</param>
        /// <returns>alligator bottom line/нижняя линия аллигатора по индексу</returns>
        private decimal GetValueDown(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                _maDown = new MovingAverage(false)
                {
                    TypeCalculationAverage = TypeCalculationAverage,
                    Lenght = LenghtDown,
                    TypePointsToSearch = PriceTypePoints.Median
                };
            }

            _maDown.Process(candles);

            if (_maDown.Values.Count - 1 <= ShiftDown)
            {
                return 0;
            }

            return _maDown.Values[_maDown.Values.Count - 1];
        }
    }
}