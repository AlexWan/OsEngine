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
    /// Indicator TRIX
    /// Индикатор Trix
    /// </summary>
    public class Trix : IIndicator
    {

        /// <summary>
        /// Period N
        /// период N
        /// </summary>
        public int Period;

        public PriceTypePoints TypePoint;

        public MovingAverageTypeCalculation TypeCalculationAverage;

        /// <summary>
        /// constructor with parameters. Indicator will be saved
        /// конструктор с параметрами. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">unique name/уникальное имя</param>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public Trix(string uniqName, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorChartPaintType.Line;
            TypePoint = PriceTypePoints.Close;
            TypeCalculationAverage = MovingAverageTypeCalculation.Exponential;
            Period = 9;
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
        public Trix(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            TypeIndicator = IndicatorChartPaintType.Line;
            TypePoint = PriceTypePoints.Close;
            TypeCalculationAverage = MovingAverageTypeCalculation.Exponential;
            Period = 9;
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
        /// candles to calculate indicator
        /// свечи для рассчёта индикатора
        /// </summary>
        private List<Candle> _myCandles;

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
                    writer.WriteLine(Period);
                    writer.WriteLine(TypePoint);
                    writer.WriteLine(TypeCalculationAverage);
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
                    Enum.TryParse(reader.ReadLine(), true, out TypePoint);
                    Enum.TryParse(reader.ReadLine(), true, out TypeCalculationAverage);
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
            TrixUi ui = new TrixUi(this);
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

            if (_trixMa1 == null || _trixMa2 == null)
            {
                _vtrixMa1 = new List<decimal>();
                _vtrixMa2 = new List<decimal>();
                _vtrixMa3 = new List<decimal>();

                _trixMa1 = new MovingAverage(false);
                _trixMa1.Lenght = Period;
                _trixMa1.TypePointsToSearch = TypePoint;
                _trixMa1.TypeCalculationAverage = TypeCalculationAverage;

                _trixMa2 = new MovingAverage(false);
                _trixMa2.Lenght = Period;
                _trixMa2.TypePointsToSearch = TypePoint;
                _trixMa2.TypeCalculationAverage = TypeCalculationAverage;

                _trixMa3 = new MovingAverage(false);
                _trixMa3.Lenght = Period;
                _trixMa3.TypePointsToSearch = TypePoint;
                _trixMa3.TypeCalculationAverage = TypeCalculationAverage;

            }

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

            _trixMa1.Process(candles);
            _vtrixMa1.Add(GetMa(candles.Count - 1));
            _trixMa2.Process(_vtrixMa1);
            _vtrixMa2.Add(GetMa2(candles.Count - 1));
            _trixMa3.Process(_vtrixMa2);
            _vtrixMa3.Add(GetMa3(candles.Count - 1));

            if (Values == null)
            {
                Values = new List<decimal>();
                Values.Add(GetValue(candles.Count - 1));


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

            _vtrixMa1 = new List<decimal>();
            _vtrixMa2 = new List<decimal>();
            _vtrixMa3 = new List<decimal>();

            _trixMa1 = new MovingAverage(false);
            _trixMa1.Lenght = Period;
            _trixMa1.TypePointsToSearch = TypePoint;
            _trixMa1.TypeCalculationAverage = TypeCalculationAverage;

            _trixMa2 = new MovingAverage(false);
            _trixMa2.Lenght = Period;
            _trixMa2.TypePointsToSearch = TypePoint;
            _trixMa2.TypeCalculationAverage = TypeCalculationAverage;

            _trixMa3 = new MovingAverage(false);
            _trixMa3.Lenght = Period;
            _trixMa3.TypePointsToSearch = TypePoint;
            _trixMa3.TypeCalculationAverage = TypeCalculationAverage;

            _trixMa1.Process(candles);

            for (int i = 0; i < candles.Count; i++)
            {
                _vtrixMa1.Add(GetMa(i));
                _trixMa2.Process(_vtrixMa1);
                _vtrixMa2.Add(GetMa2(i));
                _trixMa3.Process(_vtrixMa2);
                _vtrixMa3.Add(GetMa3(i));
                Values.Add(GetValue(i));

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

            _trixMa1.Process(candles);
            _vtrixMa1[_vtrixMa1.Count-1] = GetMa(candles.Count - 1);
            _trixMa2.Process(_vtrixMa1);
            _vtrixMa2[_vtrixMa2.Count-1] = GetMa2(candles.Count - 1);
            _trixMa3.Process(_vtrixMa2);
            _vtrixMa3[_vtrixMa3.Count-1] = GetMa3(candles.Count - 1);

            Values[Values.Count - 1] = GetValue(candles.Count - 1);
        }

        /// <summary>
        /// take indicator value by index
        /// взять значение индикатора по индексу
        /// </summary>
        /// <param name="index">index/индекс</param>
        /// <returns>index value/значение индикатора по индексу</returns>
        private decimal GetValue(int index)
        {
            if ( index < 2 || index >= _vtrixMa3.Count || _vtrixMa3[index - 1] == 0 || _vtrixMa3[index] == 0)
            {
                return 0;
            }
            decimal value = (_vtrixMa3[index] - _vtrixMa3[index - 1])*100/_vtrixMa3[index - 1];

            return Math.Round(value,4);
        }

        /// <summary>
        /// Method of calculation of 1st ma
        /// Метод расчета 1ой машки
        /// </summary>
        private decimal GetMa(int index)
        {
            if (index < Period && 
                index < 2 || index >= _trixMa1.Values.Count)
            {
                return 0;
            }

            return _trixMa1.Values[index];
        }

        /// <summary>
        /// Method of calculation of 2 ma
        /// Метод расчета 2ой машки
        /// </summary>
        private decimal GetMa2(int index)
        {
            if (index < Period * 2 -1 ||
                index < 2 || index >= _trixMa2.Values.Count)
            {
                return 0;
            }

            return _trixMa2.Values[index];
        }

        /// <summary>
        /// Method of calculation of 3 ma
        /// Метод расчета 3й машки
        /// </summary>
        private decimal GetMa3(int index)
        {
            if (index < Period*7-1 || index < 2 || index >= _trixMa3.Values.Count)
            {
                return 0;
            }

            return _trixMa3.Values[index];
        }

        private List<decimal> _vtrixMa1;
        private List<decimal> _vtrixMa2;
        private List<decimal> _vtrixMa3;

        /// <summary>
        /// moving average
        /// скользящая средняя
        /// </summary>
        private MovingAverage _trixMa1;
        private MovingAverage _trixMa2;
        private MovingAverage _trixMa3;
    }
}