/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Entity;

namespace OsEngine.Charts.CandleChart.Indicators
{
    public class IvashovRange : IIndicatorCandle
    {

        /// <summary>
        /// конструктор с параметрами. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">уникальное имя</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public IvashovRange(string uniqName, bool canDelete)
        {
            Name = uniqName;
            LenghtMa = 100;
            LenghtAverage = 100;
            TypeIndicator = IndicatorOneCandleChartType.Line;
            TypeCalculationAverage = MovingAverageTypeCalculation.Simple;
            ColorBase = Color.DodgerBlue;
            PaintOn = true;
            CanDelete = canDelete;
            Load();
        }

        /// <summary>
        /// конструктор без параметров. Индикатор не будет сохраняться
        /// используется ТОЛЬКО для создания составных индикаторов
        /// не используйте его из слоя создания роботов!
        /// </summary>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public IvashovRange(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            LenghtMa = 100;
            LenghtAverage = 100;
            TypeIndicator = IndicatorOneCandleChartType.Line;
            TypeCalculationAverage = MovingAverageTypeCalculation.Simple;
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
                List<List<decimal>> list = new List<List<decimal>>
                {
                    Values
                };
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
                List<Color> colors = new List<Color>
                {
                    ColorBase
                };
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
        /// тип скользящей средней для рассчёта индикатора
        /// </summary>
        public MovingAverageTypeCalculation TypeCalculationAverage;

        /// <summary>
        /// имя серии данных на которой будет прорисован индикатор
        /// </summary>
        public string NameSeries { get; set; }

        /// <summary>
        /// имя области данных на которой будет прорисовываться индикатор
        /// </summary>
        public string NameArea { get; set; }

        /// <summary>
        /// данные индикатора
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
        /// длинна периода для рассчёта машки
        /// </summary>
        public int LenghtMa;

        /// <summary>
        /// длинна периода для сглаживания раздвижки
        /// </summary>
        public int LenghtAverage;

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
                    writer.WriteLine(ColorBase.ToArgb());
                    writer.WriteLine(LenghtMa);
                    writer.WriteLine(PaintOn);
                    writer.WriteLine(TypeCalculationAverage);
                    writer.WriteLine(LenghtAverage);
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
                    ColorBase = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    LenghtMa = Convert.ToInt32(reader.ReadLine());
                    PaintOn = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out TypeCalculationAverage);
                    LenghtAverage = Convert.ToInt32(reader.ReadLine());
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
            IvashovRangeUi ui = new IvashovRangeUi(this);
            ui.ShowDialog();

            if (ui.IsChange && _myCandles != null)
            {
                Reload();
            }
        }

        /// <summary>
        /// перезагрузить индикатор
        /// </summary>
        public void Reload()
        {
            if (_myCandles == null)
            {
                return;
            }
            ProcessAll(_myCandles);

            NeadToReloadEvent?.Invoke(this);
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

            if (_movingCandle == null)
            {
                _movingCandle = new MovingAverage(false);
                _movingCandle.Lenght = LenghtMa;

                _movingAverage = new MovingAverage(false);
                _movingAverage.Lenght = LenghtAverage;

                _range = new List<decimal>();
            }

            _movingCandle.Process(candles);

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

            _range.Add(GetRange(candles, _movingCandle.Values, candles.Count - 1));
            _movingAverage.Process(_range);

            if (Values == null)
            {
                Values = new List<decimal>
                {
                    GetValue(candles, candles.Count - 1)
                };
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
            _range = new List<decimal>();

            _movingCandle = new MovingAverage(false);
            _movingCandle.Lenght = LenghtMa;
            _movingCandle.Process(candles);

            _movingAverage = new MovingAverage(false);
            _movingAverage.Lenght = LenghtAverage;

            for (int i = 0; i < candles.Count; i++)
            {
                _range.Add(GetRange(candles, _movingCandle.Values, i));
                _movingAverage.Process(_range);
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

            _range[_range.Count - 1] = (GetRange(candles, _movingCandle.Values, candles.Count - 1));
            _movingAverage.Process(_range);
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
            if (index < LenghtAverage ||
                index < LenghtMa ||
               _movingCandle.Values[index] == 0)
            {
                return 0;
            }
            return _movingAverage.Values[index];
        }

        private decimal GetRange(List<Candle> candles, List<decimal> moving, int index)
        {
            if (moving[index] == 0)
            {
                return 0;
            }
            return Math.Abs(moving[index] - candles[index].Close);
        }

        /// <summary>
        /// отклонения
        /// </summary>
        private List<decimal> _range;

        /// <summary>
        /// средняя для рассчёта классической машки
        /// </summary>
        private MovingAverage _movingCandle;

        /// <summary>
        /// средняя для сглаживания раздвижки
        /// </summary>
        private MovingAverage _movingAverage;
    }
}