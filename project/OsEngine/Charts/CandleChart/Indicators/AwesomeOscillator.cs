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
    /// Indicator AO. AwesomeOscillator
    /// индикатор AO. AwesomeOscillator
    /// </summary>
    public class AwesomeOscillator:IIndicator
    {
        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="uniqName">unique name/уникальное имя</param>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public AwesomeOscillator(string uniqName, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorChartPaintType.Column;
            TypeCalculationAverage = MovingAverageTypeCalculation.Exponential;
            LenghtShort = 5;
            LenghtLong = 32;
            ColorUp = Color.DodgerBlue;
            ColorDown = Color.DarkRed;
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
        public AwesomeOscillator(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            TypeIndicator = IndicatorChartPaintType.Column;
            LenghtShort = 5;
            LenghtLong = 32;
            ColorUp = Color.DodgerBlue;
            ColorDown = Color.DarkRed;
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
                colors.Add(ColorUp);
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
        public IndicatorChartPaintType TypeIndicator
        { get; set; }

        private MovingAverageTypeCalculation _movingAverageType;

        /// <summary>
        /// type of moving average for indicator construction
        /// тип скользящей средней для построения индикатора
        /// </summary>
        public MovingAverageTypeCalculation TypeCalculationAverage
        {
            get { return _movingAverageType; }
            set
            {
                _movingAverageType = value;

                if (Values != null)
                {
                    Values.Clear();
                }

                _longSma = new MovingAverage(false) { Lenght = LenghtLong, TypeCalculationAverage = TypeCalculationAverage, TypePointsToSearch = PriceTypePoints.Median };
                _shortSma = new MovingAverage (false){ Lenght = LenghtShort, TypeCalculationAverage = TypeCalculationAverage, TypePointsToSearch = PriceTypePoints.Median };
            }
        }

        /// <summary>
        /// name of data series on which indicator will be drawn
        /// имя серии на которой будет рисоваться индикатор
        /// </summary>
        public string NameSeries
        { get; set; }

        /// <summary>
        /// name of data area where indicator will be drawn
        /// имя области данных на которой будет рисоваться индикатор
        /// </summary>
        public string NameArea
        { get; set; }

        /// <summary>
        /// AO
        /// </summary>
        public List<decimal> Values
        { get; set; }

        /// <summary>
        /// unique indicator name
        /// уникальное имя индикатора
        /// </summary>
        public string Name
        { get; set; }

        /// <summary>
        /// long period length
        /// длинна длинного периода
        /// </summary>
        public int LenghtLong
        {
            get
            {
                if (_longSma == null)
                {
                    _longSma = new MovingAverage (false){ Lenght = 34, TypeCalculationAverage = TypeCalculationAverage, TypePointsToSearch = PriceTypePoints.Median };
                    
                }
                return _longSma.Lenght;
            }
            set
            {
                if (_longSma == null)
                {
                    _longSma = new MovingAverage(false) { Lenght = 34, TypeCalculationAverage = TypeCalculationAverage, TypePointsToSearch = PriceTypePoints.Median };
                }
                _longSma.Lenght = value;
            }
        }

        /// <summary>
        /// length of a short period
        /// длинна короткого периода
        /// </summary>
        public int LenghtShort
        {
            get
            {
                if (_shortSma == null)
                {
                    _shortSma = new MovingAverage(false) { Lenght = 5, TypeCalculationAverage = TypeCalculationAverage, TypePointsToSearch = PriceTypePoints.Median };
                }
                return _shortSma.Lenght;
            }
            set
            {
                if (_shortSma == null)
                {
                    _shortSma = new MovingAverage(false) { Lenght = 5, TypeCalculationAverage = TypeCalculationAverage, TypePointsToSearch = PriceTypePoints.Median };
                }
                _shortSma.Lenght = value;
            } 
        }

        /// <summary>
        /// not used
        /// не используется
        /// </summary>
        public Color ColorDown
        { get; set; }

        /// <summary>
        /// not used
        /// не используется
        /// </summary>
        public Color ColorUp
        { get; set; }

        /// <summary>
        /// is indicator tracing enabled
        /// включена ли прорисовка индикатора
        /// </summary>
        public bool PaintOn
        { get; set; }

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
                    writer.WriteLine(ColorUp.ToArgb());
                    writer.WriteLine(ColorDown.ToArgb());
                    writer.WriteLine(LenghtShort);
                    writer.WriteLine(LenghtLong);
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
                    ColorUp = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorDown = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    LenghtShort = Convert.ToInt32(reader.ReadLine());
                    LenghtLong = Convert.ToInt32(reader.ReadLine());
                    PaintOn = Convert.ToBoolean(reader.ReadLine());
                    MovingAverageTypeCalculation typeCalculation;
                    Enum.TryParse(reader.ReadLine(), true, out typeCalculation);
                    TypeCalculationAverage = typeCalculation;

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
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            AwesomeOscillatorUi ui = new AwesomeOscillatorUi(this);
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
            _longSma = new MovingAverage(false) { Lenght = LenghtLong, TypeCalculationAverage = TypeCalculationAverage, TypePointsToSearch = PriceTypePoints.Median };
            _shortSma = new MovingAverage(false) { Lenght = LenghtShort, TypeCalculationAverage = TypeCalculationAverage, TypePointsToSearch = PriceTypePoints.Median };
            Values = null;
            ProcessAll(_myCandles);

            if (NeadToReloadEvent != null)
            {
                NeadToReloadEvent(this);
            }
        }

        /// <summary>
        /// candles used to build indicator
        /// свечи по которым строится индикатор
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// short MA
        /// короткая СМА
        /// </summary>
        private MovingAverage _shortSma;

        /// <summary>
        /// long MA
        /// длинная СМА
        /// </summary>
        private MovingAverage _longSma;

        /// <summary>
        /// load indicator
        /// прогрузить индикатор
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
        /// to upload from the beginning
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
                _longSma = new MovingAverage(false) { Lenght = LenghtLong, TypeCalculationAverage = TypeCalculationAverage, TypePointsToSearch = PriceTypePoints.Median };
                _shortSma = new MovingAverage(false) { Lenght = LenghtShort, TypeCalculationAverage = TypeCalculationAverage, TypePointsToSearch = PriceTypePoints.Median };

                Values.Add(GetValueSimple(candles, candles.Count - 1));
            }
            else
            {
                Values.Add(GetValueSimple(candles, candles.Count - 1));
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

            if (_shortSma == null)
            {
                _longSma = new MovingAverage(false) { Lenght = LenghtLong, TypeCalculationAverage = TypeCalculationAverage, TypePointsToSearch = PriceTypePoints.Median };
                _shortSma = new MovingAverage(false) { Lenght = LenghtShort, TypeCalculationAverage = TypeCalculationAverage, TypePointsToSearch = PriceTypePoints.Median };
            }
            else
            {
                if (_shortSma.Values != null)
                {
                    _shortSma.Values.Clear();
                    _longSma.Values.Clear();
                }
            }

            List<Candle> newCandles = new List<Candle>();

            for (int i = 0; i < candles.Count; i++)
            {
                newCandles.Add(candles[i]);
                Values.Add(GetValueSimple(newCandles, newCandles.Count-1));
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
           Values[Values.Count - 1] = GetValueSimple(candles, candles.Count - 1);
        }

        /// <summary>
        /// take the indicator value by index
        /// взять значение индикаторм по индексу
        /// </summary>
        private decimal GetValueSimple(List<Candle> candles, int index)
        {
            _longSma.Process(candles);
            _shortSma.Process(candles);

            if (index - LenghtLong <= 0 ||
                index - LenghtShort <= 0)
            {
                return 0;
            }

            return Math.Round(_shortSma.Values[index] - _longSma.Values[index],6);
        }

        /// <summary>
        /// indicator rebooted
        /// индикатор перезагрузился
        /// </summary>
        public event Action<IIndicator> NeadToReloadEvent;
    }
}
