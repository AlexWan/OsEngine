/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Entity;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Adaptive Look Back. Индикатор волатильности от Gene Geren
    /// </summary>
    public class AdaptiveLookBack : IIndicatorCandle
    {

        /// <summary>
        /// конструктор с параметрами. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">уникальное имя</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public AdaptiveLookBack(string uniqName, bool canDelete)
        {
            Name = uniqName;
            Lenght = 5;
            TypeIndicator = IndicatorOneCandleChartType.Line;
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
        public AdaptiveLookBack(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            Lenght = 5;
            TypeIndicator = IndicatorOneCandleChartType.Line;
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
        /// имя серии данных на которой будет прорисован индикатор
        /// </summary>
        public string NameSeries { get; set; }

        /// <summary>
        /// имя области данных на которой будет прорисовываться индикатор
        /// </summary>
        public string NameArea { get; set; }

        /// <summary>
        /// Atr
        /// </summary>
        public List<decimal> Values
        { get; set; }

        /// <summary>
        /// уникальное имя индикатора
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// цвет центрально серии данных (ATR)
        /// </summary>
        public Color ColorBase { get; set; }


        /// <summary>
        /// длинна периода для рассчёта индикатора
        /// </summary>
        public int Lenght;

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
                    writer.WriteLine(Lenght);
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
                    ColorBase = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    Lenght = Convert.ToInt32(reader.ReadLine());
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
            AdaptiveLookBackUi ui = new AdaptiveLookBackUi(this);
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
            if (Values == null)
            {
                Values = new List<decimal>
                {
                    GetValue(candles, candles.Count - 1, false)
                };
            }
            else
            {
                Values.Add(GetValue(candles, candles.Count - 1, false));
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

            _swingBarArray = new List<int>();

            for (int i = 0; i < candles.Count; i++)
            {
                Values.Add(GetValue(candles, i, false));
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
            Values[Values.Count - 1] = GetValue(candles, candles.Count - 1, true);
        }

        private List<int> _swingBarArray;

        /// <summary>
        /// взять значение индикатора по индексу
        /// </summary>
        /// <param name="candles">свечи</param>
        /// <param name="index">индекс</param>
        /// <param name="updateOnly">нужно обновить только последнее значение</param>
        /// <returns>значение индикатора по индексу</returns>
        private decimal GetValue(List<Candle> candles,int index, bool updateOnly)
        {
            if (index< 5 ||
                index < Lenght + 3)
            {
                return 0;
            }

            if (_swingBarArray == null)
            {
                _swingBarArray = new List<int>();
            }

            // SwingLo - true, если: index - 2 => падали больше одного бара подряд. && [index] => И к текущему моменту уже растём две подряд
            // SwingHi - true, если: index - 2 => росли больше одного бара подряд. && [index] => И к текущему моменту уже падаем две подряд

            bool swingLo = candles[index - 4].Low > candles[index - 3].Low &&
                           candles[index - 3].Low > candles[index - 2].Low &&
                           candles[index - 3].High >= candles[index - 2].High &&
                           candles[index - 2].High < candles[index - 1].High &&
                           candles[index - 1].High < candles[index].High;

            bool swingHi = candles[index - 4].High < candles[index - 3].High &&
                           candles[index - 3].High < candles[index - 2].High &&
                           candles[index - 3].Low <= candles[index - 2].Low &&
                           candles[index - 2].Low > candles[index - 1].Low &&
                           candles[index - 1].Low > candles[index].Low;

            int so = swingLo ? -1 : swingHi ? 1 : 0;

            if (so != 0)
            {
                // если у нас разворот, добавляем свечу в массив свечей с разворотами
                if (!updateOnly)
                {
                    _swingBarArray.Add(index);
                }
                else
                {
                    if (_swingBarArray.Count > 1)
                    {
                        _swingBarArray[_swingBarArray.Count - 1] = index;
                    }
                    else
                    {
                        _swingBarArray.Add(index);
                    }
                }
                
                
            }

            int lastSwingInCalc = (_swingBarArray.Count - Lenght);

            if (lastSwingInCalc >= 0)
            {
                return (index - _swingBarArray[lastSwingInCalc])/Lenght;
            }

            return 0;
        }

    }
}