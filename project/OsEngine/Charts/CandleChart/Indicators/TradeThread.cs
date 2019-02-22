/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows;
using OsEngine.Entity;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    ///  объемно-тиковый осциллятор потока контрактов за интервал
    /// </summary>
    public class TradeThread: IIndicatorCandle
    {

        /// <summary>
        /// конструктор с параметрами. Индикатор будет сохранять настройки
        /// </summary>
        /// <param name="uniqName">уникальное имя</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public TradeThread(string uniqName, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorOneCandleChartType.Line;
            TypePointsToSearch = PriceTypePoints.Typical;
            ColorBase = Color.DeepSkyBlue;
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
        public TradeThread(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();
            TypeIndicator = IndicatorOneCandleChartType.Line;
            TypePointsToSearch = PriceTypePoints.Typical;
            ColorBase = Color.DeepSkyBlue;
            Lenght = 20;
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
        /// по какой точке будет строиться индикатор по: Open, Close ...
        /// </summary>
        public PriceTypePoints TypePointsToSearch;

        /// <summary>
        /// имя серии данных на которой индикатор прорисовывается
        /// </summary>
        public string NameSeries { get; set; }

        /// <summary>
        /// имя области данных на которой индикатор прорисовывается
        /// </summary>
        public string NameArea { get; set; }

        /// <summary>
        /// значение CCI
        /// </summary>
        public List<decimal> Values { get; set; }

        /// <summary>
        /// уникальное имя индикатора
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// длинна рассчёта индикатора
        /// </summary>
        public int Lenght { get; set; }

        /// <summary>
        /// цвет линии индикатора
        /// </summary>
        public Color ColorBase { get; set; }

        /// <summary>
        /// включена ли прорисовка серии на чарте
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
                    ColorBase = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    Lenght = Convert.ToInt32(reader.ReadLine());
                    PaintOn = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out TypePointsToSearch);
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
        /// сохранить настройки в файл
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @".txt", false))
                {
                    writer.WriteLine(ColorBase.ToArgb());
                    writer.WriteLine(Lenght);
                    writer.WriteLine(PaintOn);
                    writer.WriteLine(TypePointsToSearch);
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
            if (Values != null)
            {
                Values.Clear();
            }
        }

        /// <summary>
        /// показать окно с настройками
        /// </summary>
        public void ShowDialog()
        {
            MessageBox.Show("У данного индикатора нет настроек");
        }

        /// <summary>
        /// необходимо перерисовать индикатор на графике
        /// </summary>
        public event Action<IIndicatorCandle> NeadToReloadEvent;

        /// <summary>
        /// прогрузить новыми свечками
        /// </summary>        
        public void Process(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }
            if (Values != null && Values.Count + 1 == candles.Count)
            {
                ProcessOne(candles);
            }
            else if (Values != null && Values.Count == candles.Count)
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
            if (candles == null) return;

            if (Values == null) Values = new List<decimal>();

            Values.Add(GetValue(candles, candles.Count - 1));
        }

        /// <summary>
        /// прогрузить с самого начала
        /// </summary>
        private void ProcessAll(List<Candle> candles)
        {
            if (candles == null) return;

            Values = new List<decimal>();

            for (int i = 0; i < candles.Count; i++)
            {
                Values.Add(GetValue(candles, i));

            }
        }

        /// <summary>
        /// перегрузить последнее значение
        /// </summary>
        private void ProcessLast(List<Candle> candles)
        {
            if (candles == null) return;

            Values[Values.Count - 1] = GetValue(candles, candles.Count - 1);
        }

        /// <summary>
        /// взять значение индикаторм по индексу
        /// </summary>
        private decimal GetValue(List<Candle> candles, int index)
        {
            if (index - Lenght <= 0)
            {
                return 0;
            }

            List<Trade> trades = candles[index].Trades;

            if (trades == null ||
                trades.Count == 0)
            {
                return 0;
            }

            decimal nBuy = 0;

            decimal vBuy = 0;

            decimal nSell = 0;

            decimal vSell = 0;

            for (int i = 0; i < trades.Count; i++)
            {
                if (trades[i].Side == Side.Buy)
                {
                    nBuy++;
                    vBuy += trades[i].Volume;
                }
                if (trades[i].Side == Side.Sell)
                {
                    nSell++;
                    vSell += trades[i].Volume;
                }
            }

            decimal vto = (nBuy*vBuy - nSell*vSell)/(nBuy*vBuy + nSell*vSell);

            return Math.Round(vto, 5);
        }
    }
}
