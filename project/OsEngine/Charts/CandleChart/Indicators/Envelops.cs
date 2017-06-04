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
    public class Envelops: IIndicatorCandle
    {
        /// <summary>
        /// конструктор с уникальным именем. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">уникальное имя</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public Envelops(string uniqName,bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorOneCandleChartType.Line;
            ColorUp = Color.DodgerBlue;
            ColorDown = Color.DarkRed;
            PaintOn = true;
            Deviation = 2;

            if (!File.Exists(@"Engine\" + Name + @".txt"))
            {// если у нас первая загрузка
                _maSignal = new MovingAverage(uniqName + "maSignal", false) { Lenght = 9, TypeCalculationAverage = MovingAverageTypeCalculation.Simple };
            } 
            else
            {
                _maSignal = new MovingAverage(uniqName + "maSignal", false);
            }
            CanDelete = canDelete;
            Load();
        }

        /// <summary>
        /// конструктор без параметров. Индикатор не будет сохраняться
        /// </summary>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public Envelops(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            TypeIndicator = IndicatorOneCandleChartType.Line;
            ColorUp = Color.DodgerBlue;
            ColorDown = Color.DarkRed;
            PaintOn = true;
            Deviation = 2;
            _maSignal = new MovingAverage(false){Lenght = 9,TypeCalculationAverage = MovingAverageTypeCalculation.Simple};
            CanDelete = canDelete;
        }

        /// <summary>
        /// конструктор с готовой машкой. Будет выстраиваться исходя из заданных в ней параметров
        /// </summary>
        /// <param name="moving">скользящая средняя для расчёта</param>
        /// <param name="uniqName">уникальное имя</param>
        public Envelops(MovingAverage moving, string uniqName)
        {
            Name = uniqName;
            TypeIndicator = IndicatorOneCandleChartType.Line;
            ColorUp = Color.DodgerBlue;
            ColorDown = Color.DarkRed;
            PaintOn = true;
            Deviation = 2;

            _maSignal = moving;
            _maSignal.Name = uniqName + "maSignal";

            Load();
        }

        /// <summary>
        /// все значения индикатора
        /// </summary>
        List<List<decimal>> IIndicatorCandle.ValuesToChart
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
        /// цвета для индикатора
        /// </summary>
        List<Color> IIndicatorCandle.Colors
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
        /// можно ли удалить индикатор с графика. Это нужно для того чтобы у роботов нельзя было удалить 
        /// индикаторы которые ему нужны в торговле
        /// </summary>
        public bool CanDelete { get; set; }

        /// <summary>
        /// тип прорисовки индикатора
        /// </summary>
        public IndicatorOneCandleChartType TypeIndicator { get; set; }

        /// <summary>
        /// имя серии данных на которой будет прорисован индикатор
        /// </summary>
        public string NameSeries { get; set; }

        /// <summary>
        /// имя области данных на которой будет прорисовываться индикатор
        /// </summary>
        public string NameArea { get; set; }

        /// <summary>
        /// верхняя граница канала
        /// </summary>
        public List<decimal> ValuesUp { get; set; }

        /// <summary>
        /// нижняя граница канала
        /// </summary>
        public List<decimal> ValuesDown { get; set; }

        /// <summary>
        /// уникальное имя индикатора
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// цвет верхней серии данных
        /// </summary>
        public Color ColorUp { get; set; }

        /// <summary>
        /// цвет нижней серии данных
        /// </summary>
        public Color ColorDown { get; set; }

        /// <summary>
        /// включена ли прорисовка индикатора
        /// </summary>
        public bool PaintOn { get; set; }

        /// <summary>
        /// отклонение для расчёта индикатора
        /// </summary>
        public decimal Deviation;

        /// <summary>
        /// сохранить настройки в файл
        /// </summary>
        public void Save()
        {
            _maSignal.Save();
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
                    writer.WriteLine(Deviation);
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
                    PaintOn = Convert.ToBoolean(reader.ReadLine());
                    Deviation = Convert.ToDecimal(reader.ReadLine());
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
            _maSignal.Delete();
        }

        /// <summary>
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
        /// показать окно с настройками
        /// </summary>
        public void ShowDialog()
        {
            EnvelopsUi ui = new EnvelopsUi(this);
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
        /// показать настройки сигнальной машки
        /// </summary>
        public void ShowMaSignalDialog()
        {
            _maSignal.ShowDialog();

            ProcessAll(_myCandles);

            if (NeadToReloadEvent != null)
            {
                NeadToReloadEvent(this);
            }
            _maSignal.Save();
        }

// расчёт

        /// <summary>
        /// свечи для рассчёта индикатора
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// сигнальная машка
        /// </summary>
        private MovingAverage _maSignal;

        /// <summary>
        /// рассчитать индикатор
        /// </summary>
        /// <param name="candles">свечи</param>
        public void Process(List<Candle> candles)
        {

            _myCandles = candles;

            _maSignal.Process(candles);

            if (ValuesDown != null &&
                ValuesDown.Count + 1 == candles.Count)
            {
                ProcessOne(candles);
            }
            else if (ValuesDown != null &&
                     ValuesDown.Count == candles.Count)
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
            if (candles == null)
            {
                return;
            }

            if (ValuesUp == null)
            {
                ValuesUp = new List<decimal>();
                ValuesDown= new List<decimal>();
            }

            ValuesUp.Add(GetUpValue(candles.Count-1));
            ValuesDown.Add(GetDownValue(candles.Count - 1));
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

            _maSignal.Values = null;
            _maSignal.Process(candles);

            ValuesUp = new List<decimal>();
            ValuesDown= new List<decimal>();

            for (int i = 0; i < candles.Count; i++)
            {
                ValuesUp.Add(GetUpValue(i));
                ValuesDown.Add(GetDownValue(i));
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

            ValuesUp[ValuesUp.Count - 1] = GetUpValue(candles.Count-1);
            ValuesDown[ValuesDown.Count - 1] = GetDownValue(candles.Count - 1);
        }

        private decimal GetUpValue(int index)
        {
            if (_maSignal.Values.Count <= index)
            {
                index = _maSignal.Values.Count - 1;
            }
            return Math.Round(_maSignal.Values[index] + _maSignal.Values[index]*(Deviation/100),5);
        }

        private decimal GetDownValue(int index)
        {
            if (_maSignal.Values.Count <= index)
            {
                index = _maSignal.Values.Count - 1;
            }
            return Math.Round(_maSignal.Values[index] - _maSignal.Values[index] * (Deviation / 100),5);
        }
    }
}