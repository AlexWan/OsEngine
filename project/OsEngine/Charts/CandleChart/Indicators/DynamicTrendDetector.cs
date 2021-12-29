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
    /// Indicator DTD. Dynamic Trend Detector by K.Kopyrkin
    /// индикатор DTD. Динамический трендследящий канал К.Копыркина
    /// </summary>
    public class DynamicTrendDetector : IIndicator
    {

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="uniqName">unique name/уникальное имя</param>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public DynamicTrendDetector(string uniqName, bool canDelete)
        {
            Name = uniqName;
            Lenght = 14;
            CorrectionCoeff = 3;
            TypeIndicator = IndicatorChartPaintType.Line;
            ColorBase = Color.DodgerBlue;
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
        public DynamicTrendDetector(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            Lenght = 14;
            CorrectionCoeff = 3;
            TypeIndicator = IndicatorChartPaintType.Line;
            ColorBase = Color.DodgerBlue;
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
        /// Atr
        /// </summary>
        public List<decimal> Values
        { get; set; }

        /// <summary>
        /// unique indicator name
        /// уникальное имя индикатора
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// color of central data series (ATR)
        /// цвет центрально серии данных (ATR)
        /// </summary>
        public Color ColorBase { get; set; }

        /// <summary>
        /// period length to calculate indicator
        /// длинна периода для рассчёта индикатора
        /// </summary>
        public int Lenght { get; set; }
        
        /// <summary>
        /// atr channel multiplier
        /// множитель для построения канала
        /// </summary>
        public decimal CorrectionCoeff { get; set; }

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
                    writer.WriteLine(ColorBase.ToArgb());
                    writer.WriteLine(Lenght);
                    writer.WriteLine(CorrectionCoeff);
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
                    ColorBase = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    Lenght = Convert.ToInt32(reader.ReadLine());
                    CorrectionCoeff = Convert.ToDecimal(reader.ReadLine());
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
            DynamicTrendDetectorUi ui = new DynamicTrendDetectorUi(this);
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
            if (Values != null && Values.Count > 0)
                Values.Clear();
            
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
            if (candles == null)
            {
                return;
            }
            
            if (Values == null)
            {
                Values = new List<decimal>();
                Values.Add(GetValue(candles, candles.Count - 1));
            }
            else
            {
                Values.Add(GetValue(candles, candles.Count - 1));
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

            for (int i = 0; i < candles.Count; i++)
            {
                Values.Add(GetValue(candles, i));
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
            
            Values[Values.Count - 1] = GetValue(candles, candles.Count - 1);
        }

        private int Period;

        private Side currentSide = Side.None;

        /// <summary>
        /// take indicator value by index
        /// взять значение индикатора по индексу
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        /// <param name="index">index/индекс</param>
        /// <returns>index value/значение индикатора по индексу</returns>
        private decimal GetValue(List<Candle> candles,int index)
        {
            if (index <= 2)
            {
                Period = 0;
            }
            decimal currentCandleClose = candles[index].Close;

            if (Values == null || Values.Count == 0)
            {
                return currentCandleClose;
            }
            decimal previousTrendClose = Values[Math.Max(0, index - 2)];
            decimal currentTrendClose = Values[Math.Max(0, index - 1)];

            decimal previousCandleClose = candles[Math.Max(0, index - 1)].Close;
            
            if (index >= 1)
            {
                if (currentTrendClose < currentCandleClose)
                {
                    if (currentSide != Side.Buy)
                    {
                        Period = 0;
                    }
                    currentSide = Side.Buy;
                } else if (currentTrendClose >= currentCandleClose)
                {
                    if (currentSide != Side.Sell)
                    {
                        Period = 0;
                    }
                    currentSide = Side.Sell;
                } else
                {
                    currentSide = Side.None;
                    Period = 0;
                }
            }

            List<Candle> subList;
            List<decimal> highs;
            List<decimal> lows;
            int startIdx;
            int numItems;

            decimal value;
            decimal highest;
            decimal lowest;
            decimal correctionPercent = Decimal.Divide(CorrectionCoeff, 100);
            decimal coeffUp = 1m - correctionPercent;
            decimal coeffDown = 1m + correctionPercent;
            if (Period < Lenght)
            {
                if (currentSide != Side.None)
                {
                    startIdx = Math.Max(0, index - Period-1);
                    numItems = Math.Max(1, Period - 1);
                    subList = candles.GetRange(startIdx, numItems);
                    highs = subList.Select(o => o.High).ToList();
                    lows = subList.Select(o => o.Low).ToList();
                    Period += 1;
                    if (currentSide == Side.Buy)
                    {
                        highest = highs.Max();

                        value = highest * coeffUp;
                    } else
                    {
                        lowest = lows.Min();
                        value = lowest * coeffDown;
                    }
                    return value;
                } else
                {
                    return currentTrendClose;
                }
            } else
            {
                startIdx = Math.Max(0, index - Lenght -1);
                numItems = Math.Max(1, Lenght - 1);
                subList = candles.GetRange(startIdx, numItems);
                highs = subList.Select(o => o.High).ToList();
                lows = subList.Select(o => o.Low).ToList();
                return currentSide == Side.Buy ? highs.Max() * coeffUp : lows.Min() * coeffDown;
            }
        }
    }
}