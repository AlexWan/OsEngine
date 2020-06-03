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
    /// calculation method of Price Oscillator
    /// метод рассчёта PO
    /// </summary>
    public enum PriceOscillatorSerchType
    {
        /// <summary>
        /// Percent
        /// процент
        /// </summary>
        Persent,

        /// <summary>
        /// Points
        /// пункты
        /// </summary>
        Punkt
    }

    /// <summary>
    /// Price Oscillator indicator
    /// индикатор PO. Price Oscillator
    /// </summary>
    public class PriceOscillator : IIndicator
    {
        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="uniqName">unique name/уникальное имя</param>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public PriceOscillator(string uniqName,bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorChartPaintType.Line;
            ColorBase = Color.DodgerBlue;
            TypeSerch = PriceOscillatorSerchType.Punkt;
            PaintOn = true;
            _maShort = new MovingAverage(uniqName + "ma1", false) { Lenght = 12 };
            _maShort.Save();
            _maLong = new MovingAverage(uniqName + "ma2",false){Lenght = 26};
            _maLong.Save();
            CanDelete = canDelete;
            Load();
        }

        /// <summary>
        /// constructor without parameters.Indicator will not saved/конструктор без параметров. Индикатор не будет сохраняться
        /// used ONLY to create composite indicators/используется ТОЛЬКО для создания составных индикаторов
        /// Don't use it from robot creation layer/не используйте его из слоя создания роботов!
        /// </summary>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public PriceOscillator(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            TypeIndicator = IndicatorChartPaintType.Line;
            ColorBase = Color.DodgerBlue;
            PaintOn = true;
            TypeSerch = PriceOscillatorSerchType.Punkt;
            _maShort = new MovingAverage(false) {Lenght = 10};
            _maLong = new MovingAverage(false) { Lenght = 20 };
            CanDelete = canDelete;
        }

        /// <summary>
        /// constructor with moving averages. It will be calculated on the basis of incoming MA
        /// конструктор с машками. Будет рассчитываться исходя из входящих МА
        /// </summary>
        /// <param name="uniqName">unique name/уникальное имя</param>
        /// <param name="maShort">short MA/коротка МА</param>
        /// <param name="maLong">long MA/длинная МА</param>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public PriceOscillator(string uniqName, MovingAverage maShort, MovingAverage maLong, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorChartPaintType.Line;
            ColorBase = Color.DodgerBlue;
            TypeSerch = PriceOscillatorSerchType.Punkt;
            PaintOn = true;
            _maShort = maShort;
            _maLong = maLong;
            _maShort.Name = uniqName + "ma1";
            _maLong.Name = uniqName + "ma2";
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

        private PriceOscillatorSerchType _typeSerch;

        /// <summary>
        /// type of calculation
        /// тип расчета
        /// </summary>
        public PriceOscillatorSerchType TypeSerch
        {
            get { return _typeSerch; }
            set
            {
                if (value != _typeSerch)
                {
                    _typeSerch = value;
                    ProcessAll(_myCandles);

                    if (NeadToReloadEvent != null)
                    {
                        NeadToReloadEvent(this);
                    }
                }
            }
        }

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
        ///indicator values
        /// значения индикатора
        /// </summary>
        public List<decimal> Values { get; set; }

        /// <summary>
        /// unique indicator name
        /// уникальное имя индикатора
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///  color of central data series (ATR)
        /// цвет центрально серии данных (ATR)
        /// </summary>
        public Color ColorBase { get; set; }

        /// <summary>
        /// is indicator tracing enabled
        /// включена ли прорисовка индикатора
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
                _maLong.Save();
                _maShort.Save();
                if (string.IsNullOrWhiteSpace(Name))
                {
                    return;
                }

                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @".txt", false))
                {
                    writer.WriteLine(ColorBase.ToArgb());
                    writer.WriteLine(PaintOn);
                    writer.WriteLine(TypeSerch);
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
                    PaintOn = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out _typeSerch);
                    reader.ReadLine();

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
            _maShort.Delete();
            _maLong.Delete();
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
            PriceOscillatorUi ui = new PriceOscillatorUi(this);
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
        /// show settings of short ma
        /// показать настройки короткого мувинга
        /// </summary>
        public void ShowMaShortDialog()
        {
            _maShort.ShowDialog();

            ProcessAll(_myCandles);

            if (NeadToReloadEvent != null)
            {
                NeadToReloadEvent(this);
            }
        }

        /// <summary>
        /// show settings of long ma
        /// показать настройки длинного мувинга
        /// </summary>
        public void ShowMaLongDialog()
        {
            _maLong.ShowDialog();

            ProcessAll(_myCandles);

            if (NeadToReloadEvent != null)
            {
                NeadToReloadEvent(this);
            }
        }
        // calculation
        // расчёт

        /// <summary>
        /// candles to calculate indicator
        /// свечи для рассчёта индикатора
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// short ma for indicator calculation
        /// короткая машка для подсчёта индикатора
        /// </summary>
        private MovingAverage _maShort;

        /// <summary>
        /// long ma for indicator calculation
        /// длинная машка для подсчёта индикатора
        /// </summary>
        private MovingAverage _maLong;

        /// <summary>
        /// calculate indicator
        /// расчитать индикатор
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
            _maShort.Process(candles);
            _maLong.Process(candles);

            if (candles == null)
            {
                return;
            }

            if (Values == null)
            {
                Values = new List<decimal>();
                Values.Add(GetValue( candles.Count - 1));
            }
            else
            {
                Values.Add(GetValue(candles.Count - 1));
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

            _maShort.Values = new List<decimal>();
            _maLong.Values = new List<decimal>();

            _maShort.Process(candles);
            _maLong.Process(candles);

            for (int i = 0; i < candles.Count; i++)
            {
                Values.Add(GetValue( i));
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

            _maShort.Process(candles);
            _maLong.Process(candles);
            Values[Values.Count - 1] = GetValue(candles.Count - 1);
        }

        /// <summary>
        /// take indicator value by index
        /// взять значение индикатора по индексу
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        /// <param name="index">index/индекс</param>
        /// <returns>index value/значение индикатора по индексу</returns>
        private decimal GetValue(int index)
        {
            if (_maShort != null && index >= _maShort.Values.Count)
            {
                return Values[Values.Count - 1];
            }

            if (_maShort == null || _maShort.Values[index] == 0 ||
                _maShort.Values.Count - 1 < index)
            {
                return 0;
            }

            if (_maLong == null || _maLong.Values[index] == 0 ||
                _maLong.Values.Count - 1 < index)
            {
                return 0;
            }

            decimal difference = _maShort.Values[index] - _maLong.Values[index];

            if (TypeSerch == PriceOscillatorSerchType.Punkt)
            {
                return Math.Round(difference,8);
            }

            if (TypeSerch == PriceOscillatorSerchType.Persent)
            {
                decimal result = Math.Round(difference / (_maShort.Values[index] / 100), 8);
                return result;
            }

            return 0;
        }

    }
}