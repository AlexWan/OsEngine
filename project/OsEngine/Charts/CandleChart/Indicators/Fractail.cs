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

    /// <summary>
    /// Fractal. индикатор фрактал. В интерпритации Билла Вильямса
    /// </summary>
    public class Fractal : IIndicatorCandle
    {

        /// <summary>
        /// конструктор с параметрами. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">уникальное имя индикатора</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public Fractal(string uniqName,bool canDelete)
        {
            Name = uniqName;

            TypeIndicator = IndicatorOneCandleChartType.Point;
            ColorUp = Color.DodgerBlue;
            ColorDown = Color.DarkRed;
            PaintOn = true;
            CanDelete = canDelete;
            Load();
        }

        /// <summary>
        /// конструктор без параметров. Индикатор не будет сохраняться
        /// </summary>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public Fractal(bool canDelete) 
        {
            Name = Guid.NewGuid().ToString();

            TypeIndicator = IndicatorOneCandleChartType.Point;
            ColorUp = Color.DodgerBlue;
            ColorDown = Color.DarkRed;

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
        /// тип индикатора
        /// </summary>
        public IndicatorOneCandleChartType TypeIndicator { get; set; }

        /// <summary>
        /// имя серии данных на которой будет прорисовываться индикатор
        /// </summary>
        public string NameSeries { get; set; }

        /// <summary>
        /// имя области данных на которой будет прорисовываться индикатор
        /// </summary>
        public string NameArea
        { get; set; }

        /// <summary>
        /// имя индикатора
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
        /// включена ли прорисовка индикаторов
        /// </summary>
        public bool PaintOn { get; set; }

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
                    PaintOn = Convert.ToBoolean(reader.ReadLine());
                    ColorUp = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorDown = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));

                    reader.Close();
                }


            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

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
                    writer.WriteLine(PaintOn);
                    writer.WriteLine(ColorUp.ToArgb());
                    writer.WriteLine(ColorDown.ToArgb());
                    writer.Close();
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
            if (ValuesUp != null)
            {
                ValuesUp.Clear();
                ValuesDown.Clear();
            }
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            FractalUi ui = new FractalUi(this);
            ui.ShowDialog();

            if (ui.IsChange)
            {
                if (NeadToReloadEvent != null)
                {
                    NeadToReloadEvent(this);
                }
            }
        }

        /// <summary>
        /// верхние фракталы
        /// </summary>
        public List<decimal> ValuesUp { get; set; }

        /// <summary>
        /// нижние фракталы
        /// </summary>
        public List<decimal> ValuesDown { get; set; }

        /// <summary>
        /// прогрузить новыми свечками
        /// </summary>
        public void Process(List<Candle> candles)
        {
            if (candles.Count <= 5 || ValuesUp == null)
            {
                ValuesUp = new List<decimal>();
                ValuesDown = new List<decimal>();

                for (int i = 0; i < candles.Count; i++)
                {
                    ValuesUp.Add(0);
                    ValuesDown.Add(0);
                }
                ProcessAll(candles);
                return;
            }

            if (ValuesUp != null &&
                ValuesUp.Count + 1 == candles.Count)
            {
                ProcessOne(candles);
            }
            else if (ValuesUp != null && ValuesUp.Count != candles.Count)
            {
                ProcessAll(candles);
            }
        }

        /// <summary>
        /// необходимо перерисовать индикатор
        /// </summary>
        public event Action<IIndicatorCandle> NeadToReloadEvent;

        /// <summary>
        /// прогрузить только последнюю свечку
        /// </summary>
        private void ProcessOne(List<Candle> candles)
        {
            if (ValuesUp == null)
            {
                ValuesUp = new List<decimal>();
                ValuesDown = new List<decimal>();
                ValuesUp.Add(GetValueUp(candles, candles.Count - 1));
                ValuesDown.Add(GetValueDown(candles, candles.Count - 1));
            }
            else
            {
                ValuesUp.Add(0);
                ValuesDown.Add(0);

                ValuesUp[ValuesUp.Count -3] = (GetValueUp(candles, candles.Count - 1));
                ValuesDown[ValuesDown.Count - 3] = (GetValueDown(candles, candles.Count - 1));

                if (ValuesDown[ValuesDown.Count - 3] != 0)
                {
                    ValuesDown[ValuesDown.Count - 4] = 0;
                    ValuesDown[ValuesDown.Count - 5] = 0;
                }

                if (ValuesUp[ValuesUp.Count - 3] != 0)
                {
                    ValuesUp[ValuesUp.Count - 4] = 0;
                    ValuesUp[ValuesUp.Count - 5] = 0;
                }
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
            ValuesUp = new List<decimal>();
            ValuesDown= new List<decimal>();
            for (int i = 0; i < candles.Count; i++)
            {
                ValuesUp.Add(0);
                ValuesDown.Add(0);
            }

            for (int i = 2; i < candles.Count; i++)
            {

                    ValuesUp[i - 2] = GetValueUp(candles, i);
                    if (ValuesUp[i - 2] != 0)
                    {
                        ValuesUp[i - 2 - 1] = 0;
                        ValuesUp[i - 2 - 2] = 0;
                    }

                    ValuesDown[i - 2] = GetValueDown(candles, i);
                    if (ValuesDown[i - 2] != 0)
                    {
                        ValuesDown[i - 2 - 1] = 0;
                        ValuesDown[i - 2 - 2] = 0;
                    }
                
            }
        }

        /// <summary>
        /// взять верхнее значение индикатора по индексу
        /// </summary>
        private decimal GetValueUp(List<Candle> candles, int index)
        {
            // фрактал у нас считается сформированным только после прошедших уже двух свечей
            // т.ч. смотрим трейтью свечу от индекса
            if (index - 5 <= 0)
            {
                return 0;
            }

            if (candles[index - 2].High >= candles[index - 1].High &&
                candles[index - 2].High >= candles[index].High &&
                candles[index - 2].High >= candles[index - 3].High &&
                candles[index - 2].High >= candles[index - 4].High)
            {
                return candles[index - 2].High;
            }



            return 0;
        }

        /// <summary>
        /// взять нижнее значение индикатора по индексу
        /// </summary>
        private decimal GetValueDown(List<Candle> candles, int index)
        {
            // фрактал у нас считается сформированным только после прошедших уже двух свечей
            // т.ч. смотрим трейтью свечу от индекса
            if (index - 5 <= 0)
            {
                return 0;
            }

            if (candles[index - 2].Low <= candles[index - 1].Low &&
                candles[index - 2].Low <= candles[index].Low &&
                candles[index - 2].Low <= candles[index - 3].Low &&
                candles[index - 2].Low <= candles[index - 4].Low)
            {
                return candles[index - 2].Low;
            }



            return 0;
        }

    }
}
