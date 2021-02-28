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
    /// Simple VWAP 
    /// Simple VWAP. Средняя цена, взвешенная по объему
    /// </summary>
    public class SimpleVWAP : IIndicator
    {

        /// <summary>
        /// constructor with parameters. Indicator will save settings
        /// конструктор с параметрами. Индикатор будет сохранять настройки
        /// </summary>
        /// <param name="uniqName">unique name/уникальное имя</param>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public SimpleVWAP(string uniqName, bool canDelete)
        {
            Name = uniqName;
            ColorBase = Color.PaleVioletRed;
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
        public SimpleVWAP(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();
            ColorBase = Color.DeepSkyBlue;
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
        /// имя серии данных на которой индикатор прорисовывается
        /// </summary>
        public string NameSeries { get; set; }

        /// <summary>
        /// name of data area where indicator will be drawn
        /// имя области данных на которой индикатор прорисовывается
        /// </summary>
        public string NameArea { get; set; }

        /// <summary>
        /// moving average
        /// скользящая средняя
        /// </summary>
        public List<decimal> Values { get; set; }

        /// <summary>
        /// unique indicator name
        /// уникальное имя индикатора
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// ma color
        /// цвет машки
        /// </summary>
        public Color ColorBase { get; set; }

        /// <summary>
        /// is indicator tracing enabled
        /// включена ли прорисовка серии на чарте
        /// </summary>
        public bool PaintOn { get; set; }

        public decimal cumTypVol = 0;

        public decimal cumVol = 0;

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
        /// upload settings from file
        /// сохранить настройки в файл
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @".txt", false))
                {
                    writer.WriteLine(ColorBase.ToArgb());
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
            SimpleVWAPUi ui = new SimpleVWAPUi(this);
            ui.ShowDialog();

            if (ui.IsChange)
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
            if (_myCandles != null)
            {
                ProcessAll(_myCandles);
            }

            if (NeadToReloadEvent != null)
            {
                NeadToReloadEvent(this);
            }
        }

        /// <summary>
        /// it's necessary to redraw the indicator on the chart
        /// необходимо перерисовать индикатор на графике
        /// </summary>
        public event Action<IIndicator> NeadToReloadEvent;
        // calculating using candles
        // расчёт на свечках

        /// <summary>
        /// candles to calculate indicator
        /// свечи по которым строится индикатор
        /// </summary>
        private List<Candle> _myCandles;

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
            _myCandles = candles;
            if (Values != null &&
                Values.Count + 1 == candles.Count)
            {
                ProcessOne(candles); // one candle (history)
            }
            else if (Values != null &&
                     Values.Count == candles.Count)
            {
                ProcessLast(candles); // last candle realtime
            }
            else
            {
                ProcessAll(candles); // batch processing
            }
        }

        /// <summary>
        /// overload last candle
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
            }

            Candle current = candles[candles.Count - 1];
            decimal curTypical = Math.Round((current.High + current.Low + current.Close) / 3, 2);
            if (Values.Count == 0 || (current.TimeStart.Hour == 19 && current.TimeStart.Minute < 15))
            {
                Values.Add(curTypical);
                cumVol = current.Volume;
                cumTypVol = (curTypical * current.Volume);
            }
            else
            {
                cumVol += current.Volume;
                cumTypVol += (curTypical * current.Volume);

                Values.Add(Math.Round(cumTypVol / cumVol, 2));
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
                Candle current = candles[i];
                decimal curTypical = Math.Round((current.High + current.Low + current.Close) / 3, 2);
                decimal curVolume = current.Volume;

                if (i == 0 || (current.TimeStart.Hour == 19 && current.TimeStart.Minute < 15))
                {
                    Values.Add(curTypical);

                    cumVol = curVolume;
                    cumTypVol = (curTypical * curVolume);
                    continue;
                }

                cumVol += curVolume;
                cumTypVol += (curTypical * curVolume);

                Values.Add(Math.Round(cumTypVol / cumVol, 2));
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

            Candle current = candles[candles.Count - 1];
            decimal curTypical = Math.Round(current.High + current.Low + current.Close) / 3;
            decimal curVolume = current.Volume;


            if ((current.TimeStart.Hour == 19 && current.TimeStart.Minute < 15))
            {
                Values.Add(curTypical);
                cumVol = current.Volume;
                cumTypVol = (curTypical * current.Volume);
            }
            else
            {
                cumVol += curVolume;
                cumTypVol += (curTypical * curVolume);

                Values.Add(Math.Round(cumTypVol / cumVol, 2));
            }
        }
    }
}
