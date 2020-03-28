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
    public class Rvi : IIndicator
    {
        /// <summary>
        /// Period N
        /// период N
        /// </summary>
        public int Period;

        public MovingAverageTypeCalculation TypeCalculationAverage;

        /// <summary>
        /// constructor with parameters. Indicator will be saved
        /// конструктор с параметрами. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">unique name/уникальное имя</param>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public Rvi(string uniqName, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorChartPaintType.Line;
            Period = 10;
            ColorUp = Color.DarkRed;
            ColorDown = Color.DodgerBlue;
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
        public Rvi(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            TypeIndicator = IndicatorChartPaintType.Line;
            Period = 4;
            ColorUp = Color.DarkRed;
            ColorDown = Color.DodgerBlue;
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
        /// RVI1
        /// </summary>
        public List<decimal> ValuesUp
        { get; set; }

        /// <summary>
        /// RVI2
        /// </summary>
        public List<decimal> ValuesDown
        { get; set; }

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
        /// candles to calculate indicator
        /// свечи для рассчёта индикатора
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// candles to calculate indicator
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
                    writer.WriteLine(Period);
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

        /// <summary>.
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
                    Period = Convert.ToInt32(reader.ReadLine());
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
            RviUi ui = new RviUi(this);
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
        /// calculate indicator
        /// рассчитать индикатор
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        public void Process(List<Candle> candles)
        {
            _myCandles = candles;

            if (_moveAverage == null || _rangeAverage == null)
            {
                _moveAverage = new List<decimal>();
                _rangeAverage = new List<decimal>();
                _rvi = new List<decimal>();

            }

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

            _moveAverage.Add(GetMoveAverage(candles, candles.Count - 1));
            _rangeAverage.Add(GetRangeAverage(candles, candles.Count - 1));
            _rvi.Add(GetRvi(candles.Count - 1));


            if (ValuesUp == null)
            {
                ValuesUp = new List<decimal>();
                ValuesUp.Add(GetValue(candles, candles.Count - 1));

                ValuesDown = new List<decimal>();
                ValuesDown.Add((GetValueSecond(candles, candles.Count - 1)));
            }
            else
            {
                ValuesUp.Add(GetValue(candles, candles.Count - 1));
                ValuesDown.Add(GetValueSecond(candles, candles.Count - 1));
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
            ValuesUp = new List<decimal>();
            ValuesDown = new List<decimal>();

            _moveAverage = new List<decimal>();
            _rangeAverage = new List<decimal>();
            _rvi = new List<decimal>();

            for (int i = 0; i < candles.Count; i++)
            {
                _moveAverage.Add(GetMoveAverage(candles, i));
                _rangeAverage.Add(GetRangeAverage(candles, i));
                _rvi.Add(GetRvi(i));

                ValuesUp.Add(GetValue(candles, i));
                ValuesDown.Add(GetValueSecond(candles, i));
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

            _moveAverage[_moveAverage.Count-1] = GetMoveAverage(candles, candles.Count - 1);
            _rangeAverage[_rangeAverage.Count - 1] = GetRangeAverage(candles, candles.Count - 1);
            _rvi[_rvi.Count-1] = (GetRvi(candles.Count - 1));

            ValuesUp[ValuesUp.Count - 1] = GetValue(candles, candles.Count - 1);
            ValuesDown[ValuesDown.Count - 1] = GetValueSecond(candles, candles.Count - 1);
        }

        /// <summary>
        /// take indicator value by index
        /// взять значение индикатора по индексу
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        /// <param name="index">index/индекс</param>
        /// <returns>index value/значение индикатора по индексу</returns>
        private decimal GetValue(List<Candle> candles, int index)
        {
            decimal value = 0;

            value = GetRvi(index);

            return value;
        }

        private decimal GetValueSecond(List<Candle> candles, int index)
        {
            if (index >= Period + 6)
            {
                return Math.Round((_rvi[index] + 2 * _rvi[index - 1] + 2 * _rvi[index - 2] + _rvi[index - 3]) / 6,2);
            }
            else
            {
                return 0;
            }

        }

        private decimal GetMoveAverage(List<Candle> candles, int index)
        {
            if (index > 3)
            {
                return (candles[index].Close - candles[index].Open) +
                   2 * (candles[index - 1].Close - candles[index - 1].Open) +
                   2 * (candles[index - 2].Close - candles[index - 2].Open) +
                     (candles[index - 3].Close - candles[index - 3].Open);
            }
            else
            {
                return 0;
            }

        }

        private decimal GetRangeAverage(List<Candle> candles, int index)
        {
            if (index > 3)
            {
                return (candles[index].High - candles[index].Low) +
                   2 * (candles[index - 1].High - candles[index - 1].Low) +
                   2 * (candles[index - 2].High - candles[index - 2].Low) +
                     (candles[index - 3].High - candles[index - 3].Low);
            }
            else
            {
                return 0;
            }
        }

        private decimal GetRvi(int index)
        {

            if (index - Period + 1 <= 0)
            {
                return 0;
            }


            decimal sumMa = 0;
            decimal sumRa = 0;

            for (int i = index - Period + 1; i < index+1; i++)
            {
                sumMa = sumMa + _moveAverage[i];
                sumRa = sumRa + _rangeAverage[i]; 
            }

            if (sumRa == 0 || sumMa == 0)
            {
                return 0;
            }

            return Math.Round(sumMa/sumRa,2);
        }

        private List<decimal> _moveAverage;
        private List<decimal> _rangeAverage;
        private List<decimal> _rvi;

    }
}