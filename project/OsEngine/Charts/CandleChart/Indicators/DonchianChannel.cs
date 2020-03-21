/*
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

    /// <summary>
    /// DonchianChannel. Индикатор Канал Дончиана
    /// </summary>
    public class DonchianChannel: IIndicator
    {
        /// <summary>
        /// конструктор с параметром. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">уникальное имя индикатора</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public DonchianChannel(string uniqName,bool canDelete)
        {
            Name = uniqName;

            TypeIndicator = IndicatorChartPaintType.Line;
            ColorUp = Color.DodgerBlue;
            ColorAvg = Color.DarkRed;
            ColorDown = Color.DodgerBlue;
            Lenght = 20;
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
        public DonchianChannel(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();
            TypeIndicator = IndicatorChartPaintType.Line;
            ColorUp = Color.DodgerBlue;
            ColorAvg = Color.DarkRed;
            ColorDown = Color.DodgerBlue;
            Lenght = 20;
            PaintOn = true;
            CanDelete = canDelete;
        }

        /// <summary>
        /// все значения индикатора
        /// </summary>
        List<List<decimal>> IIndicator.ValuesToChart
        {
            get
            {
                List<List<decimal>> list = new List<List<decimal>>();
                list.Add(ValuesUp);
                list.Add(ValuesAvg);
                list.Add(ValuesDown);
                return list;
            }
        }

        /// <summary>
        /// цвета для индикатора
        /// </summary>
        List<Color> IIndicator.Colors
        {
            get
            {
                List<Color> colors = new List<Color>();
                colors.Add(ColorUp);
                colors.Add(ColorAvg);
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
        public IndicatorChartPaintType TypeIndicator
        { get; set; }

        /// <summary>
        /// имя серии данных на которой индикатор будет прорисовываться
        /// </summary>
        public string NameSeries
        { get; set; }

        /// <summary>
        /// имя области данных на которой индикатор будет прорисовываться
        /// </summary>
        public string NameArea
        { get; set; }

        /// <summary>
        /// верхняя линия канала
        /// </summary>
        public List<decimal> ValuesUp
        { get; set; }

        /// <summary>
        /// средняя линия канала 
        /// </summary>
        public List<decimal> ValuesAvg
        { get; set; }

        /// <summary>
        /// нижняя линия канала
        /// </summary>
        public List<decimal> ValuesDown
        { get; set; }

        /// <summary>
        /// уникальное имя индикатора
        /// </summary>
        public string Name
        { get; set; }

        /// <summary>
        /// длина расчёта индикатора
        /// </summary>
        public int Lenght
        { get; set; }

        /// <summary>
        /// цвет верхней серии данных
        /// </summary>
        public Color ColorUp
        { get; set; }

        /// <summary>
        /// цвет средней серии данных
        /// </summary>
        public Color ColorAvg
        { get; set; }

        /// <summary>
        /// цвет нижней серии данных
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
                    ColorAvg = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorDown = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    Lenght = Convert.ToInt32(reader.ReadLine());
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
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @".txt", false))
                {
                    writer.WriteLine(ColorUp.ToArgb());
                    writer.WriteLine(ColorAvg.ToArgb());
                    writer.WriteLine(ColorDown.ToArgb());
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
        /// удалить файл настроек
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
                ValuesAvg.Clear();
                ValuesDown.Clear();
            }
            _myCandles = null;
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            DonchianChannelUi ui = new DonchianChannelUi(this);
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
        /// прогрузить новыми свечками
        /// </summary>
        public void Process(List<Candle> candles)
        {
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
        /// необходимо перерисовать индикатор
        /// </summary>
        public event Action<IIndicator> NeadToReloadEvent;

        /// <summary>
        /// свечи по которым строиться индикатор
        /// </summary>
        private List<Candle> _myCandles;

        private List<decimal> GetIndicatorValues(List<Candle> candles, int lastCandleIndex)
        {
            decimal valueUp = 0;
            decimal valueDown = 1000000m;

            List<decimal> indicatorValues = new List<decimal>();

            if (lastCandleIndex-Lenght < 0)
            {
                indicatorValues.Add(0);
                indicatorValues.Add(0);
                indicatorValues.Add(0);
                return indicatorValues;
            }
            
            for(int i=Lenght; i>=0; i--)
            {
                int candleIndex = lastCandleIndex - i;

                if (candles[candleIndex].High > valueUp)
                {
                    valueUp = candles[candleIndex].High;
                }

                if (candles[candleIndex].Low < valueDown)
                {
                    valueDown = candles[candleIndex].Low;
                }
            }

            decimal valueAvg = valueDown + Math.Round((valueUp - valueDown) / 2, 0);

            indicatorValues.Add(valueUp);
            indicatorValues.Add(valueDown);
            indicatorValues.Add(valueAvg);
            return indicatorValues;
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
            List<decimal> values = GetIndicatorValues(candles, candles.Count-1);
            ValuesUp.Add(values[0]);
            ValuesDown.Add(values[1]);
            ValuesAvg.Add(values[2]);
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
            ValuesAvg = new List<decimal>();

            decimal[][] newValues = new decimal[candles.Count][];

            for (int i = 0; i < candles.Count; i++)
            {
                List<decimal> values = GetIndicatorValues(candles, i);
                ValuesUp.Add(values[0]);
                ValuesDown.Add(values[1]);
                ValuesAvg.Add(values[2]);
            }
        }

        /// <summary>
        /// перегрузить последнюю ячейку
        /// </summary>
        private void ProcessLast(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }
            List<decimal> values = GetIndicatorValues(candles, candles.Count - 1);
            ValuesUp.Add(values[0]);
            ValuesDown.Add(values[1]);
            ValuesAvg.Add(values[2]);
        }
    }
}
