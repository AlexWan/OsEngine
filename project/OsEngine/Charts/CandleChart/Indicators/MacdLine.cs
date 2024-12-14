﻿/*
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
    /// MACD. Moving Average Convergence Divergence. Indicator for analysis of convergence and divergence of moving averages, classic indicator.
    /// MACD. Moving Average Convergence Divergence. Индикатор для анализа схождения-расхождения скользящих средних, классический
    /// </summary>
    public class MacdLine: IIndicator
    {
        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="uniqName">unique name/уникальное имя</param>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public MacdLine(string uniqName, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorChartPaintType.Line;
            ColorUp = Color.DodgerBlue;
            ColorDown = Color.DarkRed;

            PaintOn = true;

            if (!File.Exists(@"Engine\" + Name + @".txt"))
            {// если у нас первая загрузка
                _maShort = new MovingAverage(uniqName + "ma1", false) { Length = 12, TypeCalculationAverage = MovingAverageTypeCalculation.Exponential };
                _maLong = new MovingAverage(uniqName + "ma2", false) { Length = 26, TypeCalculationAverage = MovingAverageTypeCalculation.Exponential };
                _maSignal = new MovingAverage(uniqName + "maSignal", false) { Length = 9, TypeCalculationAverage = MovingAverageTypeCalculation.Simple };
                _maShort.Save();
                _maLong.Save();
                _maSignal.Save();
            } 
            else
            {
                _maShort = new MovingAverage(uniqName + "ma1", false);
                _maLong = new MovingAverage(uniqName + "ma2", false);
                _maSignal = new MovingAverage(uniqName + "maSignal", false);
            }
            CanDelete = canDelete;
            Load();
        }

        /// <summary>
        /// constructor without parameters.Indicator will not saved/конструктор без параметров. Индикатор не будет сохраняться
        /// used ONLY to create composite indicators/используется ТОЛЬКО для создания составных индикаторов
        /// Don't use it from robot creation layer/не используйте его из слоя создания роботов!
        /// </summary>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public MacdLine(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            TypeIndicator = IndicatorChartPaintType.Line;
            ColorUp = Color.DodgerBlue;
            ColorDown = Color.DarkRed;
            PaintOn = true;
            _maShort = new MovingAverage(false) {Length = 12,TypeCalculationAverage = MovingAverageTypeCalculation.Exponential};
            _maLong = new MovingAverage(false) { Length = 26, TypeCalculationAverage = MovingAverageTypeCalculation.Exponential };
            _maSignal = new MovingAverage(false){Length = 9,TypeCalculationAverage = MovingAverageTypeCalculation.Simple};
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
        /// Macd 
        /// </summary>
        public List<decimal> ValuesUp { get; set; }

        /// <summary>
        /// Signal Line
        /// </summary>
        public List<decimal> ValuesDown { get; set; }

        /// <summary>
        /// unique indicator name
        /// уникальное имя индикатора
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// color of upper data series
        /// цвет верхней серии данных 
        /// </summary>
        public Color ColorUp { get; set; }

        /// <summary>
        /// color of lower data series
        /// цвет нижней серии данных 
        /// </summary>
        public Color ColorDown { get; set; }

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
                if (string.IsNullOrWhiteSpace(Name))
                {
                    return;
                }

                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @".txt", false))
                {
                    writer.WriteLine(ColorUp.ToArgb());
                    writer.WriteLine(ColorDown.ToArgb());
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
                    ColorUp = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorDown = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    PaintOn = Convert.ToBoolean(reader.ReadLine());
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
            _maSignal.Delete();
        }

        /// <summary>
        /// delete data
        /// удалить данные
        /// </summary>
        public void Clear()
        {
            if (ValuesUp != null)
            {
                ValuesUp.Clear();
                ValuesDown.Clear();
            }
            _myCandles = null;
        }

        /// <summary>
        /// display settings window
        /// показать окно с настройками
        /// </summary>
        public void ShowDialog()
        {
            MacdLineUi ui = new MacdLineUi(this);
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

            if (NeedToReloadEvent != null)
            {
                NeedToReloadEvent(this);
            }
        }

        public int SmaShortLen
        {
            set
            {
                _maShort.Length = value;
                ProcessAll(_myCandles);
            }
            get 
            {
                return _maShort.Length; 
            }
        }

        public int SmaLongLen
        {
            set
            {
                _maLong.Length = value;
                ProcessAll(_myCandles);
            }
            get 
            { 
                return _maLong.Length; 
            }
        }

        public int SmaSignalLen
        {
            set
            {
                _maSignal.Length = value;
                ProcessAll(_myCandles);
            }
            get
            {
                return _maSignal.Length;
            }
        }

        /// <summary>
        /// show short ma settings
        /// показать настройки короткой машки
        /// </summary>
        public void ShowMaShortDialog()
        {
            _maShort.ShowDialog();

            ProcessAll(_myCandles);

            if (NeedToReloadEvent != null)
            {
                NeedToReloadEvent(this);
            }
        }

        /// <summary>
        /// show long ma settings
        /// показать настройки длинной машки
        /// </summary>
        public void ShowMaLongDialog()
        {
            _maLong.ShowDialog();

            ProcessAll(_myCandles);

            if (NeedToReloadEvent != null)
            {
                NeedToReloadEvent(this);
            }
        }

        /// <summary>
        /// show signal ma settings
        /// показать настройки сигнальной машки
        /// </summary>
        public void ShowMaSignalDialog()
        {
            _maSignal.ShowDialog();

            ProcessAll(_myCandles);

            if (NeedToReloadEvent != null)
            {
                NeedToReloadEvent(this);
            }
        }
        // calculating
        // расчёт

        /// <summary>
        /// candles to calculate indicator
        /// свечи для рассчёта индикатора
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// short ma
        /// короткая машка
        /// </summary>
        private MovingAverage _maShort;
        
        /// <summary>
        /// long ma
        /// длинная машка
        /// </summary>
        private MovingAverage _maLong;

        /// <summary>
        /// signal ma
        /// сигнальная машка
        /// </summary>
        private MovingAverage _maSignal;

        /// <summary>
        /// calculate indicator
        /// рассчитать индикатор
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        public void Process(List<Candle> candles)
        {
            _myCandles = candles;

            _maShort.Process(candles);
            _maLong.Process(candles);


            if (ValuesUp != null &&
                ValuesUp.Count + 1 == candles.Count)
            {
                ProcessOne(candles);
            }
            else if (ValuesUp != null &&
                     ValuesUp.Count == candles.Count)
            {
                ProcessLast(candles);
            }
            else
            {
                ProcessAll(candles);
            }

            ValuesDown = _maSignal.Values;
        }

        /// <summary>
        /// indicator needs to be redrawn
        /// индикатор нужно перерисовать
        /// </summary>
        public event Action<IIndicator> NeedToReloadEvent;

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

            if (ValuesUp == null)
            {
                ValuesUp = new List<decimal>();
                ValuesUp.Add(GetMacd(candles.Count - 1));
            }
            else
            {
                ValuesUp.Add(GetMacd(candles.Count - 1));
            }
            _maSignal.Process(ValuesUp);
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

            _maShort.Values = null;
            _maLong.Values = null;
            _maSignal.Values = null;

            _maShort.Process(candles);
            _maLong.Process(candles);

            ValuesUp = new List<decimal>();

            for (int i = 0; i < candles.Count; i++)
            {
                ValuesUp.Add(GetMacd(i));
                _maSignal.Process(ValuesUp);
            }
            ValuesDown = _maSignal.Values;
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
            ValuesUp[ValuesUp.Count - 1] = GetMacd(candles.Count - 1);
            _maSignal.Process(ValuesUp);
        }

        /// <summary>
        /// take indicator value by index
        /// взять значение индикатора по индексу
        /// </summary>
        /// <param name="index">index/индекс</param>
        /// <returns>index value/значение индикатора по индексу</returns>
        private decimal GetMacd(int index)
        {
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

            return Math.Round(_maShort.Values[index] - _maLong.Values[index],7);
        }
    }
}