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
    /// метод рассчёта PO
    /// </summary>
    public enum PriceOscillatorSerchType
    {
        /// <summary>
        /// процент
        /// </summary>
        Persent,

        /// <summary>
        /// пункты
        /// </summary>
        Punkt
    }

    /// <summary>
    /// индикатор PO. Price Oscillator
    /// </summary>
    public class PriceOscillator : IIndicatorCandle
    {
        /// <summary>
        /// конструктор с уникальным именем. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">уникальное имя</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public PriceOscillator(string uniqName,bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorOneCandleChartType.Line;
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
        /// конструктор без параметров. Индикатор не будет сохраняться
        /// </summary>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public PriceOscillator(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            TypeIndicator = IndicatorOneCandleChartType.Line;
            ColorBase = Color.DodgerBlue;
            PaintOn = true;
            TypeSerch = PriceOscillatorSerchType.Punkt;
            _maShort = new MovingAverage(false) {Lenght = 10};
            _maLong = new MovingAverage(false) { Lenght = 20 };
            CanDelete = canDelete;
        }

        /// <summary>
        /// конструктор с машками. Будет рассчитываться исходя из входящих МА
        /// </summary>
        /// <param name="uniqName">уникальное имя</param>
        /// <param name="maShort">коротка МА</param>
        /// <param name="maLong">длинная МА</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public PriceOscillator(string uniqName, MovingAverage maShort, MovingAverage maLong, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorOneCandleChartType.Line;
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

        private PriceOscillatorSerchType _typeSerch;

        /// <summary>
        /// тип рассчёта 
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
        /// имя серии данных на которой будет прорисован индикатор
        /// </summary>
        public string NameSeries { get; set; }

        /// <summary>
        /// имя области данных на которой будет прорисовываться индикатор
        /// </summary>
        public string NameArea { get; set; }

        /// <summary>
        /// значения индикатора
        /// </summary>
        public List<decimal> Values { get; set; }

        /// <summary>
        /// уникальное имя индикатора
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// цвет центрально серии данных (ATR)
        /// </summary>
        public Color ColorBase { get; set; }

        /// <summary>
        /// включена ли прорисовка индикатора
        /// </summary>
        public bool PaintOn { get; set; }

        /// <summary>
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
                    ColorBase = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    PaintOn = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out _typeSerch);
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
        /// показать окно с настройками
        /// </summary>
        public void ShowDialog()
        {
            PriceOscillatorUi ui = new PriceOscillatorUi(this);
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

// расчёт

        /// <summary>
        /// свечи для рассчёта индикатора
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// короткая машка для подсчёта индикатора
        /// </summary>
        private MovingAverage _maShort;

        /// <summary>
        /// длинная машка для подсчёта индикатора
        /// </summary>
        private MovingAverage _maLong;

        /// <summary>
        /// рассчитать индикатор
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
        /// индикатор нужно перерисовать
        /// </summary>
        public event Action<IIndicatorCandle> NeadToReloadEvent;

        /// <summary>
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
        /// взять значение индикатора по индексу
        /// </summary>
        /// <param name="index">индекс</param>
        /// <returns>значение индикатора по индексу</returns>
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