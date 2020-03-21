using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OsEngine.Indicators;

namespace OsEngine.Charts.CandleChart.Indicators
{
    public class Vwap : IIndicator
    {
        #region options

        private DateTime _timeStart;
        private DateTime _timeEnd;

        /// <summary>
        /// whether the indicator is on by date
        /// включен ли индикатор по дате
        /// </summary>
        public bool UseDate;

        public DateTime DatePickerStart = DateTime.Now;
        public DateTime TimePickerStart = DateTime.Now;

        public bool ToEndTicks;

        public DateTime DatePickerEnd = DateTime.Now;
        public DateTime TimePickerEnd = DateTime.Now;

        public bool DateDev2;
        public bool DateDev3;
        public bool DateDev4;

        /// <summary>
        /// Is the indicator turned on in one day?
        /// включен ли индикатор за один день
        /// </summary>
        public bool UseDay;

        public bool DayDev2;
        public bool DayDev3;
        public bool DayDev4;

        /// <summary>
        /// is the weekly indicator on
        /// включен ли индикатор за неделю
        /// </summary>
        public bool UseWeekly;

        public bool WeekDev2;
        public bool WeekDev3;
        public bool WeekDev4;

        /// <summary>
        /// color vwap by date
        /// цвет вивап по дате
        /// </summary>
        public Color ColorDate { get; set; }

        /// <summary>
        /// day vwap color
        /// цвет дневного vwap
        /// </summary>
        public Color ColorDay { get; set; }

        /// <summary>
        /// color weekly vwap
        /// цвет недельного vwap
        /// </summary>
        public Color ColorWeek { get; set; }

        /// <summary>
        /// date deviation color
        /// цвет отклонений по дате
        /// </summary>
        public Color ColorDateDev { get; set; }

        /// <summary>
        /// daytime color deviations
        /// цвет дневных отклонений
        /// </summary>
        public Color ColorDayDev { get; set; }

        /// <summary>
        /// weekly color deviations
        /// цвет недельных отклонений
        /// </summary>
        public Color ColorWeekDev { get; set; }

        #endregion

        public Vwap(string uniqName, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorChartPaintType.Line;
            PaintOn = true;
            CanDelete = canDelete;

            ColorDate = Color.BlueViolet;
            ColorDay = Color.CornflowerBlue;
            ColorWeek = Color.CornflowerBlue;

            ColorDateDev = Color.AntiqueWhite;
            ColorDayDev = Color.BurlyWood;
            ColorWeekDev = Color.Cornsilk;

            ResetValues();

            Load();
        }

        public IndicatorChartPaintType TypeIndicator { get; set; }

        private List<Color> _colors;

        /// <summary>
        /// indicator colors
        /// цвета для индикатора
        /// </summary>
        List<Color> IIndicator.Colors
        {
            get
            {
                return _colors;
            }
        }
        public bool CanDelete { get; set; }
        public string NameSeries { get; set; }
        public string NameArea { get; set; }
        public string Name { get; set; }
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
                    writer.WriteLine(UseDate);
                    writer.WriteLine(DatePickerStart);
                    writer.WriteLine(TimePickerStart);
                    writer.WriteLine(ToEndTicks);
                    writer.WriteLine(DatePickerEnd);
                    writer.WriteLine(TimePickerEnd);

                    writer.WriteLine(DateDev2);
                    writer.WriteLine(DateDev3);
                    writer.WriteLine(DateDev4);

                    writer.WriteLine(ColorDate.ToArgb());
                    writer.WriteLine(ColorDateDev.ToArgb());

                    writer.WriteLine(UseDay);

                    writer.WriteLine(DayDev2);
                    writer.WriteLine(DayDev3);
                    writer.WriteLine(DayDev4);

                    writer.WriteLine(ColorDay.ToArgb());
                    writer.WriteLine(ColorDayDev.ToArgb());

                    writer.WriteLine(UseWeekly);

                    writer.WriteLine(WeekDev2);
                    writer.WriteLine(WeekDev3);
                    writer.WriteLine(WeekDev4);

                    writer.WriteLine(ColorWeek.ToArgb());
                    writer.WriteLine(ColorWeekDev.ToArgb());

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
        /// load settings from file
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
                    UseDate = Convert.ToBoolean(reader.ReadLine());
                    DatePickerStart = DateTime.Parse(reader.ReadLine());
                    TimePickerStart = DateTime.Parse(reader.ReadLine());
                    ToEndTicks = Convert.ToBoolean(reader.ReadLine());
                    DatePickerEnd = DateTime.Parse(reader.ReadLine());
                    TimePickerEnd = DateTime.Parse(reader.ReadLine());

                    DateDev2 = Convert.ToBoolean(reader.ReadLine());
                    DateDev3 = Convert.ToBoolean(reader.ReadLine());
                    DateDev4 = Convert.ToBoolean(reader.ReadLine());

                    ColorDate = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorDateDev = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));

                    UseDay = Convert.ToBoolean(reader.ReadLine());

                    DayDev2 = Convert.ToBoolean(reader.ReadLine());
                    DayDev3 = Convert.ToBoolean(reader.ReadLine());
                    DayDev4 = Convert.ToBoolean(reader.ReadLine());

                    ColorDay = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorDayDev = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));

                    UseWeekly = Convert.ToBoolean(reader.ReadLine());

                    WeekDev2 = Convert.ToBoolean(reader.ReadLine());
                    WeekDev3 = Convert.ToBoolean(reader.ReadLine());
                    WeekDev4 = Convert.ToBoolean(reader.ReadLine());

                    ColorWeek = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorWeekDev = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));

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

        public void Clear()
        {
            ResetValues();
            _myCandles = null;
            _lastCandlesCount = 1;
        }

        /// <summary>
        /// show settings window
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            VwapUi ui = new VwapUi(this);
            ui.ShowDialog();

            if (ui.IsChange && _myCandles != null)
            {
                Reload();
            }
        }

        /// <summary>
        /// all indicator values
        /// все значения индикатора
        /// </summary>
        List<List<decimal>> IIndicator.ValuesToChart
        {
            get
            {
                while (_calculationInProgress)
                {
                    Thread.Sleep(10);
                }

                List<List<decimal>> list = new List<List<decimal>>();
                _colors = new List<Color>();


                list.Add(VwapDate);
                _colors.Add(ColorDate);

                list.Add(VwapDay);
                _colors.Add(ColorDay);

                list.Add(VwapWeekly);
                _colors.Add(ColorWeek);

                foreach (var dateDeviationsValue in DateDeviationsValues)
                {
                    list.Add(dateDeviationsValue);
                    _colors.Add(ColorDateDev);
                }
                foreach (var dayDeviationsValue in DayDeviationsValues)
                {
                    list.Add(dayDeviationsValue);
                    _colors.Add(ColorDayDev);
                }
                foreach (var weeklyDeviationsValue in WeeklyDeviationsValues)
                {
                    list.Add(weeklyDeviationsValue);
                    _colors.Add(ColorWeekDev);
                }

                return list;
            }
        }

        public List<decimal> VwapDate;
        public List<List<decimal>> DateDeviationsValues;

        public List<decimal> VwapDay;
        public List<List<decimal>> DayDeviationsValues;

        public List<decimal> VwapWeekly;
        public List<List<decimal>> WeeklyDeviationsValues;

        public event Action<IIndicator> NeadToReloadEvent;

        /// <summary>
        /// candles to calculate the indicator
        /// свечи для рассчёта индикатора
        /// </summary>
        private List<Candle> _myCandles;

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

        private int _lastCandlesCount = 1;

        /// <summary>
        /// calculate indicator
        /// рассчитать индикатор
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        public void Process(List<Candle> candles)
        {
            if (_lastCandlesCount == candles.Count)
            {
                return;
            }
            
            _lastCandlesCount = candles.Count;
            
            _myCandles = candles;

            _myCandles.RemoveAt(candles.Count - 1);

            if ((VwapDate != null &&
                VwapDate.Count + 1 == candles.Count) ||
                (VwapDay != null &&
                VwapDay.Count + 1 == candles.Count) ||
                (VwapWeekly != null &&
                 VwapWeekly.Count + 1 == candles.Count)
                )
            {
                ProcessOne(_myCandles);
            }
            else if ((VwapDate != null &&
                      VwapDate.Count == candles.Count) ||
                     (VwapDay != null &&
                      VwapDay.Count == candles.Count) ||
                     (VwapWeekly != null &&
                      VwapWeekly.Count == candles.Count)
            )
            {
                // ignore
            }
            else
            {
                ProcessAll(_myCandles);
            }
        }

        /// <summary>
        /// load only the last candle
        /// прогрузить только последнюю свечку
        /// </summary>
        private void ProcessOne(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }
            if (_decimals == 0 && candles.Count != 0)
            {
                var price = candles[0].Open;
                _decimals = BitConverter.GetBytes(decimal.GetBits(price)[3])[2];
            }
            if (VwapDay == null)
            {
                ResetValues();
            }

            if (UseDate)
            {
                ProcessOneCandleDate(candles[candles.Count - 1]);
            }
            if (UseDay)
            {
                ProcessOneCandleDay(candles[candles.Count - 1]);
            }
            if (UseWeekly)
            {
                ProcessOneCandleWeek(candles[candles.Count - 1]);
            }
        }

        /// <summary>
        /// number of decimal places
        /// кол-во знаков после запятой
        /// </summary>
        private int _decimals;

        /// <summary>
        /// to load from the very beginning
        /// прогрузить с самого начала
        /// </summary>
        private void ProcessAll(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }

            if (_decimals == 0 && candles.Count != 0)
            {
                var price = candles[0].Open;
                _decimals = BitConverter.GetBytes(decimal.GetBits(price)[3])[2];
            }

            ResetValues();

            _calculationInProgress = true;
            var tasks = new Task[3];
            try
            {
                if (UseDate)
                {
                    var task = Task.Run(delegate { ProcessDate(candles); });
                    tasks[0] = task;
                }
                else
                {
                    var task = Task.Run(() => Task.Delay(1));
                    tasks[0] = task;
                }

                if (UseDay)
                {
                    var task = Task.Run(delegate { ProcessOneDay(candles); });
                    tasks[1] = task;
                }
                else
                {
                    var task = Task.Run(() => Task.Delay(1));
                    tasks[1] = task;
                }

                if (UseWeekly)
                {
                    var task = Task.Run(delegate { ProcessWeek(candles); });
                    tasks[2] = task;
                }
                else
                {
                    var task = Task.Run(() => Task.Delay(1));
                    tasks[2] = task;
                }

                Task.WaitAll(tasks);
            }
            finally
            {
                _calculationInProgress = false;
            }
        }

        private bool _calculationInProgress = false;

        decimal _allTicksVolume;
        decimal _lastVwapValue;
        decimal _currentVwapValue;

        /// <summary>
        /// calculate vwap by date for one candle
        /// рассчитать vwap по дате для одной свечи
        /// </summary>
        private void ProcessOneCandleDate(Candle candle)
        {
            if (candle.TimeStart >= _timeStart &&
                candle.TimeStart < _timeEnd)
            {
                foreach (var trade in candle.Trades)
                {
                    if (trade.Volume == 0)
                    {
                        continue;
                    }
                    _currentVwapValue = (trade.Price * trade.Volume + _lastVwapValue * _allTicksVolume) / (_allTicksVolume + trade.Volume);

                    _allTicksVolume += trade.Volume;
                    _lastVwapValue = _currentVwapValue;
                }

                VwapDate.Add(Math.Round(_currentVwapValue, _decimals));
            }
            else
            {
                VwapDate.Add(0);
            }
            ProcDev(VwapDate[VwapDate.Count - 1]);
        }

        double _moveX;
        double _valCount;
        double _lastMedian;
        double _lastPow;

        private void ProcDev(decimal vwapValue)
        {
            decimal prosakDeviation = 0;

            if (vwapValue != 0)
            {
                _valCount++;
                _moveX += Convert.ToDouble(vwapValue);
                _lastMedian = _moveX / _valCount;
                _lastPow += Math.Pow(Convert.ToDouble(vwapValue) - _lastMedian, 2);
                prosakDeviation = Convert.ToDecimal(Math.Sqrt(_lastPow / _valCount));
            }

            if (DateDev2)
            {
                DateDeviationsValues?[0].Add(Math.Round(vwapValue + prosakDeviation * 2, _decimals));
                DateDeviationsValues?[1].Add(Math.Round(vwapValue - prosakDeviation * 2, _decimals));
            }
            if (DateDev3)
            {
                DateDeviationsValues?[2].Add(Math.Round(vwapValue + prosakDeviation * 3, _decimals));
                DateDeviationsValues?[3].Add(Math.Round(vwapValue - prosakDeviation * 3, _decimals));
            }
            if (DateDev4)
            {
                DateDeviationsValues?[4].Add(Math.Round(vwapValue + prosakDeviation * 4, _decimals));
                DateDeviationsValues?[5].Add(Math.Round(vwapValue - prosakDeviation * 4, _decimals));
            }
        }

        /// <summary>
        /// calculate indicator by date
        /// рассчитать индикатор по дате
        /// </summary>
        private void ProcessDate(List<Candle> candles)
        {
            for (int i = 0; i <= candles.Count - 1; i++)
            {
                ProcessOneCandleDate(candles[i]);
            }
        }


        decimal _allTicksVolumeD = 0;
        decimal _lastVwapValueD = 0;
        decimal _currentVwapValueD = 0;
        bool _needZeroD = false;
        int _lastDayD = 0;

        /// <summary>
        /// calculate daily vivap for one candle
        /// рассчитать дневной вивап для одной свечи
        /// </summary>
        private void ProcessOneCandleDay(Candle candle)
        {
            DayOfWeek currentDayOfWeek = candle.TimeStart.DayOfWeek;
            int currentDay = candle.TimeStart.Day;

            if ((currentDayOfWeek == DayOfWeek.Sunday ||
                currentDayOfWeek == DayOfWeek.Tuesday ||
                currentDayOfWeek == DayOfWeek.Wednesday ||
                currentDayOfWeek == DayOfWeek.Thursday ||
                currentDayOfWeek == DayOfWeek.Friday) &&
                currentDay != _lastDayD)
            {
                _lastDayD = candle.TimeStart.Day;
                _allTicksVolumeD = 0;
                _lastVwapValueD = 0;
                _currentVwapValueD = 0;
                _needZeroD = true;
            }

            foreach (var trade in candle.Trades)
            {
                if (trade.Volume == 0)
                {
                    continue;
                }
                _currentVwapValueD = (trade.Price * trade.Volume + _lastVwapValueD * _allTicksVolumeD) / (_allTicksVolumeD + trade.Volume);

                _allTicksVolumeD += trade.Volume;
                _lastVwapValueD = _currentVwapValueD;
            }

            if (_needZeroD)
            {
                VwapDay.Add(0);
                _needZeroD = false;
            }
            else
            {
                VwapDay.Add(Math.Round(_currentVwapValueD, _decimals));
            }

            ProcDevDay(VwapDay[VwapDay.Count - 1]);
        }

        double _moveXd;
        double _valCountD;
        double _lastMedianD;
        double _lastPowD;

        private void ProcDevDay(decimal vwapValue)
        {
            decimal prosakDeviation = 0;

            if (vwapValue != 0)
            {
                _valCountD++;
                _moveXd += Convert.ToDouble(vwapValue);
                _lastMedianD = _moveXd / _valCountD;
                _lastPowD += Math.Pow(Convert.ToDouble(vwapValue) - _lastMedianD, 2);
                prosakDeviation = Convert.ToDecimal(Math.Sqrt(_lastPowD / _valCountD));
            }
            else
            {
                _moveXd = 0;
                _valCountD = 0;
                _lastMedianD = 0;
                _lastPowD = 0;
            }

            if (DayDev2)
            {
                DayDeviationsValues?[0].Add(Math.Round(vwapValue + prosakDeviation * 2, _decimals));
                DayDeviationsValues?[1].Add(Math.Round(vwapValue - prosakDeviation * 2, _decimals));
            }
            if (DayDev3)
            {
                DayDeviationsValues?[2].Add(Math.Round(vwapValue + prosakDeviation * 3, _decimals));
                DayDeviationsValues?[3].Add(Math.Round(vwapValue - prosakDeviation * 3, _decimals));
            }
            if (DayDev4)
            {
                DayDeviationsValues?[4].Add(Math.Round(vwapValue + prosakDeviation * 4, _decimals));
                DayDeviationsValues?[5].Add(Math.Round(vwapValue - prosakDeviation * 4, _decimals));
            }
        }

        /// <summary>
        /// calculate daily vwap
        /// рассчитать дневной vwap
        /// </summary>
        private void ProcessOneDay(List<Candle> candles)
        {
            for (int i = 0; i < candles.Count; i++)
            {
                ProcessOneCandleDay(candles[i]);
            }
        }

        /// <summary>
        /// calculate weekly vwap
        /// рассчитать недельный vwap
        /// </summary>
        private void ProcessWeek(List<Candle> candles)
        {
            for (int i = 0; i < candles.Count; i++)
            {
                ProcessOneCandleWeek(candles[i]);
            }
        }

        decimal _allTicksVolumeW = 0;
        decimal _lastVwapValueW = 0;
        decimal _currentVwapValueW = 0;
        bool _needZeroW = false;
        bool _started = false;
        int _lastDayW = 0;

        /// <summary>
        /// calculate daily vivap for one candle
        /// рассчитать дневной вивап для одной свечи
        /// </summary>
        private void ProcessOneCandleWeek(Candle candle)
        {
            DayOfWeek currentDay = candle.TimeStart.DayOfWeek;

            var currentDayNum = candle.TimeStart.Day;

            if (currentDay == DayOfWeek.Sunday)
            {
                _started = true;
            }

            if (!_started)
            {
                VwapWeekly.Add(0);
                ProcDevWeek(0);
                return;
            }

            if (currentDay == DayOfWeek.Sunday && currentDayNum != _lastDayW)
            {
                _allTicksVolumeW = 0;
                _lastVwapValueW = 0;
                _currentVwapValueW = 0;
                _needZeroW = true;
                _lastDayW = currentDayNum;
            }

            foreach (var trade in candle.Trades)
            {
                if (trade.Volume == 0)
                {
                    continue;
                }
                _currentVwapValueW = (trade.Price * trade.Volume + _lastVwapValueW * _allTicksVolumeW) / (_allTicksVolumeW + trade.Volume);

                _allTicksVolumeW += trade.Volume;
                _lastVwapValueW = _currentVwapValueW;
            }

            if (_needZeroW)
            {
                VwapWeekly.Add(0);
                _needZeroW = false;
            }
            else
            {
                VwapWeekly.Add(Math.Round(_currentVwapValueW, _decimals));
            }

            ProcDevWeek(VwapWeekly[VwapWeekly.Count - 1]);
        }

        double _moveXw;
        double _valCountW;
        double _lastMedianW;
        double _lastPowW;

        private void ProcDevWeek(decimal vwapValue)
        {
            decimal prosakDeviation = 0;

            if (vwapValue != 0)
            {
                _valCountW++;
                _moveXw += Convert.ToDouble(vwapValue);
                _lastMedianW = _moveXw / _valCountW;
                _lastPowW += Math.Pow(Convert.ToDouble(vwapValue) - _lastMedianW, 2);
                prosakDeviation = Convert.ToDecimal(Math.Sqrt(_lastPowW / _valCountW));
            }
            else
            {
                _moveXw = 0;
                _valCountW = 0;
                _lastMedianW = 0;
                _lastPowW = 0;
            }

            if (WeekDev2)
            {
                WeeklyDeviationsValues[0].Add(Math.Round(vwapValue + prosakDeviation * 2, _decimals));
                WeeklyDeviationsValues[1].Add(Math.Round(vwapValue - prosakDeviation * 2, _decimals));
            }
            if (WeekDev3)
            {
                WeeklyDeviationsValues[2].Add(Math.Round(vwapValue + prosakDeviation * 3, _decimals));
                WeeklyDeviationsValues[3].Add(Math.Round(vwapValue - prosakDeviation * 3, _decimals));
            }
            if (WeekDev4)
            {
                WeeklyDeviationsValues[4].Add(Math.Round(vwapValue + prosakDeviation * 4, _decimals));
                WeeklyDeviationsValues[5].Add(Math.Round(vwapValue - prosakDeviation * 4, _decimals));
            }
        }

        /// <summary>
        /// reset all indicator values
        /// сбросить все значения индикатора
        /// </summary>
        private void ResetValues()
        {
            _timeStart = new DateTime(DatePickerStart.Year, DatePickerStart.Month, DatePickerStart.Day,
                                      TimePickerStart.Hour, 0, 0);
            _timeEnd = ToEndTicks ? DateTime.MaxValue : new DateTime(DatePickerEnd.Year, DatePickerEnd.Month, DatePickerEnd.Day,
                                                                TimePickerEnd.Hour, 0, 0);


            _allTicksVolume = 0;
            _lastVwapValue = 0;
            _currentVwapValue = 0;

            _moveX = 0;
            _valCount = 0;
            _lastMedian = 0;
            _lastPow = 0;

            _allTicksVolumeD = 0;
            _lastVwapValueD = 0;
            _currentVwapValueD = 0;
            _needZeroD = false;
            _lastDayD = 0;

            _moveXd = 0;
            _valCountD = 0;
            _lastMedianD = 0;
            _lastPowD = 0;

            _allTicksVolumeW = 0;
            _lastVwapValueW = 0;
            _currentVwapValueW = 0;
            _needZeroW = true;
            _started = false;
            _lastDayW = 0;

            _moveXw = 0;
            _valCountW = 0;
            _lastMedianW = 0;
            _lastPowW = 0;

            VwapDate = new List<decimal>();
            VwapDay = new List<decimal>();
            VwapWeekly = new List<decimal>();

            DateDeviationsValues = new List<List<decimal>>();
            DayDeviationsValues = new List<List<decimal>>();
            WeeklyDeviationsValues = new List<List<decimal>>();

            for (int i = 0; i < 6; i++)
            {
                DateDeviationsValues.Add(new List<decimal>());
                DayDeviationsValues.Add(new List<decimal>());
                WeeklyDeviationsValues.Add(new List<decimal>());
            }
        }
    }
}
