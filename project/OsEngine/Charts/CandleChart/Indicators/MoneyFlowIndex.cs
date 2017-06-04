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
    public class MoneyFlowIndex : IIndicatorCandle
    {
        /// <summary>
        /// период N
        /// </summary>
        public int Nperiod;

        /// <summary>
        /// конструктор с параметрами. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">уникальное имя</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public MoneyFlowIndex(string uniqName, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorOneCandleChartType.Line;
            Nperiod = 3;
            ColorBase = Color.DodgerBlue;
            PaintOn = true;
            CanDelete = canDelete;
            Load();
        }

        /// <summary>
        /// конструктор без параметров. Индикатор не будет сохраняться
        /// </summary>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public MoneyFlowIndex(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            TypeIndicator = IndicatorOneCandleChartType.Line;
            Nperiod = 3;
            ColorBase = Color.DodgerBlue;
            PaintOn = true;
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

        /// <summary>
        /// имя серии данных на которой будет прорисован индикатор
        /// </summary>
        public string NameSeries { get; set; }

        /// <summary>
        /// имя области данных на которой будет прорисовываться индикатор
        /// </summary>
        public string NameArea { get; set; }

        /// <summary>
        /// индикатор
        /// </summary>
        public List<decimal> Values
        { get; set; }

        /// <summary>
        /// уникальное имя индикатора
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// цвет центрально серии данных
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
                if (string.IsNullOrWhiteSpace(Name))
                {
                    return;
                }

                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @".txt", false))
                {
                    writer.WriteLine(Nperiod);
                    writer.WriteLine(ColorBase.ToArgb());
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
                    Nperiod = Convert.ToInt32(reader.ReadLine());
                    ColorBase = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    PaintOn = Convert.ToBoolean(reader.ReadLine());
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
            MoneyFlowIndexUi ui = new MoneyFlowIndexUi(this);
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
        /// свечи для рассчёта индикатора
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// рассчитать индикатор
        /// </summary>
        /// <param name="candles">свечи</param>
        public void Process(List<Candle> candles)
        {
            _myCandles = candles;
            if (_tp == null || _fn == null || _fp == null)
            {
                _tp = new List<decimal>();
                _fn = new List<decimal>();
                _fp = new List<decimal>();

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

            _tp.Add(GetTypicalPrice(candles, candles.Count - 1));
            _fn.Add(0);
            _fp.Add(0);

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
        /// прогрузить с самого начала
        /// </summary>
        private void ProcessAll(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }
            Values = new List<decimal>();

            _tp = new List<decimal>();
            _fn = new List<decimal>();
            _fp = new List<decimal>();


            for (int i = 0; i < candles.Count; i++)
            {
                _tp.Add(GetTypicalPrice(candles, i));
                _fp.Add(0);
                _fn.Add(0);
                Values.Add(GetValue(candles, i));
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
            _tp[_tp.Count - 1] = GetTypicalPrice(candles, candles.Count - 1);
            Values[Values.Count - 1] = GetValue(candles, candles.Count - 1);
        }

        /// <summary>
        /// взять значение индикатора по индексу
        /// </summary>
        /// <param name="candles">свечи</param>
        /// <param name="index">индекс</param>
        /// <returns>значение индикатора по индексу</returns>
        private decimal GetValue(List<Candle> candles, int index)
        {
            decimal mf;

            if (index < Nperiod)
            {
                return 0;
            }


            decimal value = 0;


            mf = _tp[index] * candles[index].Volume;

            if (_tp.Count == 30)
            {

            }

            if (index > 1)
            {


                if (_tp[index] > _tp[index - 1])
                {
                    _fp[index] = mf;
                }
                if (_tp[index] < _tp[index - 1])
                {
                    _fn[index] = mf;
                }
            }

            if (index > Nperiod)
            {
                decimal sumFp = 0;
                decimal sumFn = 0;
                decimal ratio;

                for (int i = index - Nperiod + 1; i < index; i++)
                {
                    sumFp = sumFp + _fp[i];
                    sumFn = sumFn + _fn[i];
                }
                if (sumFn == 0)
                {
                    ratio = 0;
                }
                else
                {
                    ratio = sumFp / sumFn;
                }

                value = 100 - 100 / (1 + ratio);

            }

            return Math.Round(value,2);
        }

        private decimal GetTypicalPrice(List<Candle> candles, int index)
        {

            if (index == 0)
            {
                return 0;
            }
            return (candles[index].High + candles[index].Low + candles[index].Close) / 3;
        }

        /// <summary>
        /// pozitive flow
        /// </summary>
        private List<decimal> _fp;

        /// <summary>
        /// negative flow
        /// </summary>
        private List<decimal> _fn;

        /// <summary>
        /// typical prive
        /// </summary>
        private List<decimal> _tp;

    }
}