/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace OsEngine.Charts.CandleChart.Indicators
{
    public class UltimateOscillator : IIndicator
    {
        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="uniqName">unique name/уникальное имя</param>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public UltimateOscillator(string uniqName, bool canDelete)
        {
            Name = uniqName;
            Period1 = 7;
            Period2 = 14;
            Period3 = 28;
            TypeIndicator = IndicatorChartPaintType.Line;
            ColorBase = Color.Red;
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
        public UltimateOscillator(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            Period1 = 7;
            Period2 = 14;
            Period3 = 28;

            TypeIndicator = IndicatorChartPaintType.Line;
            ColorBase = Color.Red;
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
        public IndicatorChartPaintType TypeIndicator
        { get; set; }

        /// <summary>
        /// name of data series on which indicator will be drawn
        /// имя серии на которой индикатор прорисовывается
        /// </summary>
        public string NameSeries
        { get; set; }

        /// <summary>
        /// name of data area where indicator will be drawn
        /// имя области на котророй индикатор прорисовывается
        /// </summary>
        public string NameArea
        { get; set; }

        /// <summary>
        /// Adx
        /// </summary>
        public List<decimal> Values
        { get; set; }

        /// <summary>
        /// unique indicator name
        /// уникальное имя индикатора
        /// </summary>
        public string Name
        { get; set; }

        /// <summary>
        /// color of the central data series 
        /// цвет для прорисовки базовой точки данных
        /// </summary>
        public Color ColorBase
        { get; set; }

        /// <summary>
        /// period length to calculate indicator
        /// длинна периода для рассчёта индикатора
        /// </summary>
        public int Period1;

        /// <summary>
        /// period length to calculate indicator
        /// длинна периода для рассчёта индикатора
        /// </summary>
        public int Period2;

        /// <summary>
        /// period length to calculate indicator
        /// длинна периода для рассчёта индикатора
        /// </summary>
        public int Period3;

        /// <summary>
        /// is indicator tracing enabled
        /// включена ли прорисовка индикатора
        /// </summary>
        public bool PaintOn
        { get; set; }

        /// <summary>
        /// save settings to file
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return;
            }
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @".txt", false))
                {
                    writer.WriteLine(ColorBase.ToArgb());
                    writer.WriteLine(Period1);
                    writer.WriteLine(Period2);
                    writer.WriteLine(Period3);
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
        /// загрузить настройки
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
                    Period1 = Convert.ToInt32(reader.ReadLine());
                    Period2 = Convert.ToInt32(reader.ReadLine());
                    Period3 = Convert.ToInt32(reader.ReadLine());

                    PaintOn = Convert.ToBoolean(reader.ReadLine());

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
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            UltimateOscillatorUi ui = new UltimateOscillatorUi(this);
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
        ///  indicator needs to be redrawn
        /// индикатор нужно перерисовать
        /// </summary>
        public event Action<IIndicator> NeadToReloadEvent;

        /// <summary>
        /// candles to calculate indicator
        /// свечки для рассчёта индикатора
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// recalculate indicator
        /// пересчитать индикатор
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
            }

            Values.Add(GetValue(candles, candles.Count - 1));

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

        /// <summary>
        /// calculate new value
        /// рассчитать новое значение
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        /// <param name="index">index for which value is required/индекс по которому нужно значение</param>
        /// <returns>indicator value/значение индикатора</returns>
        public decimal GetValue(List<Candle> candles, int index)
        {
            if(index == 0)
            {
                _bp = new List<decimal>();
                _tr = new List<decimal>();
            }

            if(index < Period1 ||
                index < Period2 ||
                index < Period3)
            {
                return 0;
            }

            ReloadBuyingPressure(candles, index);
            ReloadTrueRange(candles, index);

            decimal bpPer1 = SummList(index - Period1, index, _bp);
            decimal trPer1 = SummList(index - Period1, index, _tr);

            decimal bpPer2 = SummList(index - Period2, index, _bp);
            decimal trPer2 = SummList(index - Period2, index, _tr);

            decimal bpPer3 = SummList(index - Period3, index, _bp);
            decimal trPer3 = SummList(index - Period3, index, _tr);

            if(trPer1 == 0 ||
                trPer2 == 0 ||
                trPer3 == 0)
            {
                return 0;
            }

            decimal average7 = bpPer1 / trPer1;
            decimal average14 = bpPer2 / trPer2;
            decimal average28 = bpPer3 / trPer3;

            return 100 * ((4 * average7)+(2* average14)+average28) / (4+3+2);
        }

        private decimal SummList(int indxStart, int indxEnd, List<decimal> array)
        {
            decimal result = 0;

            for (int i = indxStart; i < array.Count && i < indxEnd + 1;i++)
            {
                result += array[i];
            }

            return result;
        }


        List<decimal> _bp = new List<decimal>();

        private void ReloadBuyingPressure(List<Candle> candles, int index)
        {
            // Buying Pressure(BP) = Close - Minimum(Lowest between Current Low or Previous Close) 
            //=Закрытие - Минимальное (минимальное между текущим или предыдущим закрытием)

            decimal result = candles[index].Close - Math.Min(candles[index].Low,candles[index-1].Close);

            while(_bp.Count <= index)
            {
                _bp.Add(0);
            }

            _bp[index] = result;
        }

        List<decimal> _tr = new List<decimal>();

        private void ReloadTrueRange(List<Candle> candles, int index)
        {
            decimal hiToLow = Math.Abs(candles[index].High - candles[index].Low);
            decimal closeToHigh = Math.Abs(candles[index - 1].Close - candles[index].High);
            decimal closeToLow = Math.Abs(candles[index - 1].Close - candles[index].Low);

            decimal result = Math.Max(Math.Max(hiToLow, closeToHigh), closeToLow);

            while (_tr.Count <= index)
            {
                _tr.Add(0);
            }

            _tr[index] = result;
        }

        private decimal MovingAverage(List<decimal> valuesSeries, int length, int index)
        {
            decimal lastMoving = 0;

            for (int i = index; i > -1 && i > valuesSeries.Count - 1 - length; i--)
            {
                lastMoving += valuesSeries[i];
            }

            if (lastMoving != 0)
            {
                return lastMoving / length;
            }

            return 0;
        }

    }
}
