/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
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
    /// индикатор AO. AwesomeOscillator
    /// </summary>
    public class AwesomeOscillator:IIndicatorCandle
    {
        /// <summary>
        /// конструктор с параметрами. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">уникальное имя индикатора</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public AwesomeOscillator(string uniqName, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorOneCandleChartType.Column;
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
        /// конструктор без параметров. Индикатор не будет сохраняться
        /// используется ТОЛЬКО для создания составных индикаторов
        /// не используйте его из слоя создания роботов!
        /// </summary>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public AwesomeOscillator(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            TypeIndicator = IndicatorOneCandleChartType.Column;
            LenghtShort = 5;
            LenghtLong = 32;
            ColorUp = Color.DodgerBlue;
            ColorDown = Color.DarkRed;
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
                List<List<decimal>> list = new List<List<decimal>>
                {
                    Values
                };
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
                List<Color> colors = new List<Color>
                {
                    ColorUp,
                    ColorDown
                };
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
        public IndicatorOneCandleChartType TypeIndicator
        { get; set; }

        private MovingAverageTypeCalculation _movingAverageType;

        /// <summary>
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
        /// имя серии на которой будет рисоваться индикатор
        /// </summary>
        public string NameSeries
        { get; set; }

        /// <summary>
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
        /// уникальное имя индикатора
        /// </summary>
        public string Name
        { get; set; }

        /// <summary>
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
        /// не используется
        /// </summary>
        public Color ColorDown
        { get; set; }
        
        /// <summary>
        /// не используется
        /// </summary>
        public Color ColorUp
        { get; set; }

        /// <summary>
        /// включена ли прорисовка индикатора
        /// </summary>
        public bool PaintOn
        { get; set; }

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
            AwesomeOscillatorUi ui = new AwesomeOscillatorUi(this);
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
            _longSma = new MovingAverage(false) { Lenght = LenghtLong, TypeCalculationAverage = TypeCalculationAverage, TypePointsToSearch = PriceTypePoints.Median };
            _shortSma = new MovingAverage(false) { Lenght = LenghtShort, TypeCalculationAverage = TypeCalculationAverage, TypePointsToSearch = PriceTypePoints.Median };
            Values = null;
            ProcessAll(_myCandles);

            NeadToReloadEvent?.Invoke(this);
        }

        /// <summary>
        /// свечи по которым строится индикатор
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// короткая СМА
        /// </summary>
        private MovingAverage _shortSma;

        /// <summary>
        /// длинная СМА
        /// </summary>
        private MovingAverage _longSma;

        /// <summary>
        /// прогрузить индикатор
        /// </summary>
        /// <param name="candles">свечи</param>
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
        /// индикатор перезагрузился
        /// </summary>
        public event Action<IIndicatorCandle> NeadToReloadEvent;
    }
}
