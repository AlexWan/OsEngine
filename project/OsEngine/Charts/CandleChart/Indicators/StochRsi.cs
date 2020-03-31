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
    public class StochRsi : IIndicator
    {

        /// <summary>
        /// constructor with parameters. Indicator will be saved
        /// конструктор с параметрами. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">unique name/уникальное имя</param>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public StochRsi(string uniqName, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorChartPaintType.Line;
            RsiLenght = 14;
            StochasticLength = 14;
            K = 3;
            D = 3;

            ColorK = Color.Aqua;
            ColorD = Color.OrangeRed;
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
        public StochRsi(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();
            TypeIndicator = IndicatorChartPaintType.Line;
            RsiLenght = 14;
            StochasticLength = 14;
            K = 3;
            D = 3;

            ColorK = Color.Aqua;
            ColorD = Color.OrangeRed;

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
                list.Add(ValuesK);
                list.Add(ValuesD);
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
                colors.Add(ColorK);
                colors.Add(ColorD);
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
        /// тип индикатора
        /// </summary>
        public IndicatorChartPaintType TypeIndicator { get; set; }

        /// <summary>
        /// unique indicator name
        /// уникальное имя индикатор
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// name of data series on which indicator will be drawn
        /// имя серии данных на которой индикатор прорисовывается
        /// </summary>
        public string NameSeries { get; set; }

        /// <summary>
        /// name of data area where indicator will be drawn
        /// имя области данных на которой индикатор прорисовывается
        /// </summary>
        public string NameArea { get; set; }

        /// <summary>
        /// indicator calculation length
        /// длинна расчёта индикатора
        /// </summary>
        public int RsiLenght
        {
            get { return _rsiLength;}
            set
            {
                if (value == _rsiLength)
                {
                    return;
                }
                _rsiLength = value;
                _rsi.Lenght = value;
            }
        }
        private int _rsiLength;

        /// <summary>
        /// stochastic indicator calculation length
        /// длинна расчёта стохастика
        /// </summary>
        public int StochasticLength { get; set; }

        public int K { get; set; }

        public int D { get; set; }

        /// <summary>
        /// indicator color
        /// цвет индикатора
        /// </summary>
        public Color ColorK { get; set; }

        /// <summary>
        /// indicator color
        /// цвет индикатора
        /// </summary>
        public Color ColorD { get; set; }

        /// <summary>
        /// is indicator tracing enabled
        /// включена ли прорисовка индикатора
        /// </summary>
        public bool PaintOn { get; set; }

        /// <summary>
        /// upload settings from file
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
                    ColorK = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    RsiLenght = Convert.ToInt32(reader.ReadLine());
                    StochasticLength = Convert.ToInt32(reader.ReadLine());
                    K = Convert.ToInt32(reader.ReadLine());
                    D = Convert.ToInt32(reader.ReadLine());
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
                    writer.WriteLine(ColorK.ToArgb());
                    writer.WriteLine(RsiLenght);
                    writer.WriteLine(StochasticLength);
                    writer.WriteLine(K);
                    writer.WriteLine(D);
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
            if (ValuesK != null)
            {
                ValuesK.Clear();
                ValuesD.Clear();
                _kValues.Clear();
            }

            _myCandles = null;
        }

        /// <summary>
        /// display settings window
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            StochRsiUi ui = new StochRsiUi(this);
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
        /// indicator values
        /// данные индикатора
        /// </summary>
        public List<decimal> ValuesK { get; set; }

        /// <summary>
        /// indicator values
        /// данные индикатора
        /// </summary>
        public List<decimal> ValuesD { get; set; }

        /// <summary>
        /// candles to calculate indicator
        /// свечи по которым строиться индикатор
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// indicator needs to be redrawn
        /// требуется перерисовать индикатор
        /// </summary>
        public event Action<IIndicator> NeadToReloadEvent;

        // calculation вычисления

        /// <summary>
        /// to upload new candles
        /// прогрузить новыми свечками
        /// </summary> 
        public void Process(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }

            lock (_locker)
            {
                _myCandles = candles;

                _rsi.Process(candles);

                if (ValuesK != null &&
                    ValuesK.Count + 1 == candles.Count)
                {
                    ProcessOne(candles);
                }
                else if (ValuesK != null &&
                         ValuesK.Count == candles.Count)
                {
                    ProcessLast(candles);
                }
                else
                {
                    ProcessAll(candles);
                }
            }
        }

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

            if (ValuesK == null)
            {
                ValuesK = new List<decimal>();
                ValuesK.Add(GetValue(candles, candles.Count - 1));

                ValuesD = new List<decimal>();
                ValuesD.Add(GetMoving(ValuesK, candles.Count - 1, D));
            }
            else
            {
                ValuesK.Add(GetValue(candles, candles.Count - 1));
                ValuesD.Add(GetMoving(ValuesK, candles.Count - 1, D));
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

            _rsi.Clear();
            _rsi.Process(candles);
            _kValues.Clear();

            ValuesK = new List<decimal>();
            ValuesD = new List<decimal>();
            for (int i = 0; i < candles.Count; i++)
            {
                if (i == 73)
                {

                }

                ValuesK.Add(GetValue(candles, i));
                ValuesD.Add(GetMoving(ValuesK, i, D));
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

            lock (_locker)
            {
                ValuesK[ValuesK.Count - 1] = GetValue(candles, candles.Count - 1);
                ValuesD[ValuesD.Count - 1] = GetMoving(ValuesK, candles.Count - 1, D);
            }
        }

        private object _locker = new object();

        private Rsi _rsi = new Rsi(false);

        private List<decimal> _kValues = new List<decimal>();

        /// <summary>
        /// take indicator value by index
        /// взять значение индикаторм по индексу
        /// </summary>
        private decimal GetValue(List<Candle> candles, int index)
        {
            if (index < _rsiLength+3 ||
                index < K ||
                index < D)
            {
                if (_kValues.Count <= index)
                {
                    _kValues.Add(0);
                }
                return 0;
            }

            decimal high = GetHigh(_rsi.Values, index, StochasticLength);
            decimal low = GetLow(_rsi.Values, index, StochasticLength);

            decimal k;

            if (high - low != 0)
            {
                k = 100 * (_rsi.Values[index] - low) / (high - low);
            }
            else
            {
                k = _kValues[index - 1];
            }

            if (_kValues.Count <= index)
            {
                _kValues.Add(k);
            }
            else
            {
                _kValues[index] = k;
            }

            decimal smoofK = GetMoving(_kValues, index, K);

            return smoofK;
        }

        private decimal GetHigh(List<decimal> rsi, int index, int length)
        {
            if (index - length + 1 <= 0 ||
                index > rsi.Count)
            {
                return 0;
            }

            decimal max = 0;

            for (int i = index - length + 1; i < index + 1; i++)
            {
                try
                {
                    if (rsi[i] > max)
                    {
                        max = rsi[i];
                    }
                }
                catch 
                {

                }

            }

            return max;
        }

        private decimal GetLow(List<decimal> rsi, int index, int length)
        {
            if (index - length + 1 <= 0 ||
                index > rsi.Count)
            {
                return 0;
            }
            decimal min = Decimal.MaxValue;

            for (int i = index - length + 1; i < index + 1; i++)
            {
                if (rsi[i] < min)
                {
                    min = rsi[i];
                }
            }

            return min;
        }

        private decimal GetMoving(List<decimal> values, int index, int Lenght)
        {
            if (index - Lenght <= 0 ||
                index >= values.Count)
            {
                return 0;
            }

            decimal average = 0;

            for (int i = index; i > index - Lenght && i > 0; i--)
            {
                try
                {
                    average += values[i];
                }
                catch (Exception e)
                {

                }
            }

            average = average / Lenght;

            return Math.Round(average, 2);
        }
    }
}
