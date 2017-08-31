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
    /// PriceChannel Индикатор
    /// </summary>
    public class PriceChannel: IIndicatorCandle
    {
        /// <summary>
        /// конструктор с параметрами. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">уникальное имя индикатора</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public PriceChannel(string uniqName,bool canDelete)
        {
            Name = uniqName;

            TypeIndicator = IndicatorOneCandleChartType.Line;
            ColorUp = Color.DodgerBlue;
            ColorDown = Color.DarkRed;
            LenghtUpLine = 12;
            LenghtDownLine = 12;
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
        public PriceChannel(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            TypeIndicator = IndicatorOneCandleChartType.Line;
            ColorUp = Color.DodgerBlue;
            ColorDown = Color.DarkRed;
            LenghtUpLine = 12;
            LenghtDownLine = 12;
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
        public IndicatorOneCandleChartType TypeIndicator
        { get; set; }

        /// <summary>
        /// имя серии данных на которой прорисовывается индикатор
        /// </summary>
        public string NameSeries
        { get; set; }

        /// <summary>
        /// имя области данных на которой прорисовывается индикатор
        /// </summary>
        public string NameArea
        { get; set; }

        /// <summary>
        /// верхний канал
        /// </summary>
        public List<decimal> ValuesUp
        { get; set; }

        /// <summary>
        /// нижний канал
        /// </summary>
        public List<decimal> ValuesDown
        { get; set; }

        /// <summary>
        /// уникальное имя индикатора
        /// </summary>
        public string Name
        { get; set; }

        /// <summary>
        /// длинна рассчёта верхнего канала
        /// </summary>
        public int LenghtUpLine
        { get; set; }

        /// <summary>
        /// длинна рассчёта нижнего канала
        /// </summary>
        public int LenghtDownLine
        { get; set; }

        /// <summary>
        /// цвет верхней границы канала
        /// </summary>
        public Color ColorUp
        { get; set; }

        /// <summary>
        /// цвет нижней границы канала
        /// </summary>
        public Color ColorDown
        { get; set; }

        /// <summary>
        /// включена ли прорисовка индикатора
        /// </summary>
        public bool PaintOn
        { get; set; }

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
                    LenghtUpLine = Convert.ToInt32(reader.ReadLine());
                    LenghtDownLine = Convert.ToInt32(reader.ReadLine());
                    PaintOn = Convert.ToBoolean(reader.ReadLine());
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
                    writer.WriteLine(ColorUp.ToArgb());
                    writer.WriteLine(ColorDown.ToArgb());
                    writer.WriteLine(LenghtUpLine);
                    writer.WriteLine(LenghtDownLine);
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

            _myCandles = null;
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            PriceChannelUi ui = new PriceChannelUi(this);
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


            if (NeadToReloadEvent != null)
            {
                NeadToReloadEvent(this);
            }
        }

        /// <summary>
        /// нужно перерисовать индикатор
        /// </summary>
        public event Action<IIndicatorCandle> NeadToReloadEvent;

        /// <summary>
        /// свечи для которых рассчитывается индикатор
        /// </summary>
        private List<Candle> _myCandles;

// вычисления

        /// <summary>
        /// прогрузить новыми свечками
        /// </summary>
        public void Process(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }
            _myCandles = candles;
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
        /// прогрузить только последнюю свечку
        /// </summary>
        private void ProcessOne(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }
            if (ValuesDown == null)
            {
                ValuesUp = new List<decimal>();
                ValuesDown = new List<decimal>();

                decimal[] value = GetValueSimple(candles, candles.Count - 1);

                ValuesUp.Add(value[0]);
                ValuesDown.Add(value[1]);
            }
            else
            {
                decimal[] value = GetValueSimple(candles, candles.Count - 1);

                ValuesUp.Add(value[0]);
                ValuesDown.Add(value[1]);
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
            ValuesDown = new List<decimal>();

            decimal[][] newValues = new decimal[candles.Count][];

            for (int i = 0; i < candles.Count; i++)
            {
                newValues[i] = GetValueSimple(candles, i);
            }

            for (int i = 0; i < candles.Count; i++)
            {
                ValuesUp.Add(newValues[i][0]);
            }

            for (int i = 0; i < candles.Count; i++)
            {
                ValuesDown.Add(newValues[i][1]);
            }
        }

        /// <summary>
        /// пересчитать последнюю
        /// </summary>
        private void ProcessLast(List<Candle> candles)
        {
            decimal[] value = GetValueSimple(candles, candles.Count - 1);
            ValuesUp[ValuesUp.Count - 1] = value[0];
            ValuesDown[ValuesDown.Count - 1] = value[1];
        }

        /// <summary>
        /// взять значение индикатора по индексу
        /// </summary>
        private decimal[] GetValueSimple(List<Candle> candles, int index)
        {

// считаем верхнее значение
            decimal [] lines = new decimal[2];

            if (index - LenghtUpLine <= 0 || 
                candles.Count <= LenghtUpLine)
            {
                lines[0] = 0;
            }
            else
            {
                decimal upLine = 0;

                for (int i = index; i > -1 && i > index - LenghtUpLine; i--)
                {
                    if (upLine < candles[i].High)
                    {
                        upLine = candles[i].High;
                    }
                }

                lines[0] = upLine;
            }

// считаем верхнее значение

            if (index - LenghtDownLine <= 0 ||
                candles.Count <= LenghtDownLine)
            {
                lines[1] = 0;
            }
            else
            {
                decimal downLine = decimal.MaxValue;

                for (int i = index; i > -1 && i > index - LenghtDownLine; i--)
                {
                    if (downLine > candles[i].Low)
                    {
                        downLine = candles[i].Low;
                    }
                }

                lines[1] = downLine;
            }

            return lines;
        }
    }
}
