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
    /// Индикатор Alligator. Билла Вильямса
    /// </summary>
    public class Alligator: IIndicatorCandle
    {
        /// <summary>
        /// конструктор с параметром. Сохраняется
        /// </summary>
        /// <param name="uniqName">уникальное имя</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public Alligator(string uniqName, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorOneCandleChartType.Line;

            LenghtBase = 8;
            ShiftBase = 5;
            ColorBase = Color.DarkRed;

            LenghtUp = 5;
            ShiftUp = 3;
            ColorUp = Color.LawnGreen;

            LenghtDown = 13;
            ShiftDown = 8;
            ColorDown = Color.DodgerBlue;
            
            PaintOn = true;
            TypeCalculationAverage = MovingAverageTypeCalculation.Simple;
            CanDelete = canDelete;
            Load();
        }
        
        /// <summary>
        /// конструктор без параметра. Не сохраняется
        /// используется ТОЛЬКО для создания составных индикаторов
        /// не используйте его из слоя создания роботов!
        /// </summary>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public Alligator(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            TypeIndicator = IndicatorOneCandleChartType.Line;

            LenghtBase = 8;
            ShiftBase = 5;
            ColorBase = Color.DarkRed;

            LenghtUp = 5;
            ShiftUp = 3;
            ColorUp = Color.LawnGreen;

            LenghtDown = 13;
            ShiftDown = 8;
            ColorDown = Color.DodgerBlue;

            PaintOn = true;
            TypeCalculationAverage = MovingAverageTypeCalculation.Simple;
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
                list.Add(Values);
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
                colors.Add(ColorBase);
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
        /// тип скользящей средней для рассчёта индикатора
        /// </summary>
        private MovingAverageTypeCalculation _typeCalculationAverage;

        /// <summary>
        /// тип скользящей средней для рассчёта индикатора
        /// </summary>
        public MovingAverageTypeCalculation TypeCalculationAverage
        {
            get { return _typeCalculationAverage; }
            set
            {
                _typeCalculationAverage = value;

                Values = null;
                ValuesUp = null;
                ValuesDown = null;

                _maUp = new MovingAverage(false)
                {
                    TypeCalculationAverage = value,
                    Lenght = LenghtUp,
                };
                _maDown = new MovingAverage(false)
                {
                    TypeCalculationAverage = value,
                    Lenght = LenghtDown,
                };
                _maBase = new MovingAverage(false)
                {
                    TypeCalculationAverage = value,
                    Lenght = LenghtBase,
                };
            }
        }

        /// <summary>
        /// имя серии на графике для прорисовки индикатора
        /// </summary>
        public string NameSeries { get; set; }

        /// <summary>
        /// имя области на графике для прорисовки индикатора
        /// </summary>
        public string NameArea { get; set; }

        /// <summary>
        /// средняя линия. Зубы
        /// </summary>
        public List<decimal> Values
        { get; set; }

        /// <summary>
        /// быстрая линия. Губы
        /// </summary>
        public List<decimal> ValuesUp 
        { get; set; }

        /// <summary>
        /// медленная линия. Челюсть
        /// </summary>
        public List<decimal> ValuesDown 
        { get; set; }

        /// <summary>
        /// уникальное имя
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// цвет нижней линии
        /// </summary>
        public Color ColorDown { get; set; }

        /// <summary>
        /// цвет верхней линии
        /// </summary>
        public Color ColorUp { get; set; }

        /// <summary>
        /// цвет центральной линии
        /// </summary>
        public Color ColorBase { get; set; }

        /// <summary>
        /// длинна скоростной линии
        /// </summary>
        public int LenghtUp;

        /// <summary>
        /// длинна центральной линии
        /// </summary>
        public int LenghtBase;

        /// <summary>
        /// длинна медленной линии
        /// </summary>
        public int LenghtDown;

        /// <summary>
        /// сдвиг верхней линии
        /// </summary>
        public int ShiftUp;

        /// <summary>
        /// сдвиг центральной линии
        /// </summary>
        public int ShiftBase;

        /// <summary>
        /// сдвиг нижней линии
        /// </summary>
        public int ShiftDown;

        /// <summary>
        /// вкллючена ли прорисовка индикатора на графике
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

                    writer.WriteLine(LenghtBase);
                    writer.WriteLine(ShiftBase);
                    writer.WriteLine(ColorBase.ToArgb());

                    writer.WriteLine(LenghtUp);
                    writer.WriteLine(ShiftUp);
                    writer.WriteLine(ColorUp.ToArgb());

                    writer.WriteLine(LenghtDown);
                    writer.WriteLine(ShiftDown);
                    writer.WriteLine(ColorDown.ToArgb());
                    
                    writer.WriteLine(PaintOn);
                    writer.WriteLine(TypeCalculationAverage);
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
                    LenghtBase = Convert.ToInt32(reader.ReadLine());
                    ShiftBase = Convert.ToInt32(reader.ReadLine());
                    ColorBase = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));

                    LenghtUp = Convert.ToInt32(reader.ReadLine());
                    ShiftUp = Convert.ToInt32(reader.ReadLine());
                    ColorUp = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));

                    LenghtDown = Convert.ToInt32(reader.ReadLine());
                    ShiftDown = Convert.ToInt32(reader.ReadLine());
                    ColorDown = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    
                    PaintOn = Convert.ToBoolean(reader.ReadLine());
                    MovingAverageTypeCalculation type;
                    Enum.TryParse(reader.ReadLine(), true,out type);

                    TypeCalculationAverage = type;

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
                ValuesDown.Clear();
                ValuesUp.Clear();
            }
            _myCandles = null;
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            AlligatorUi ui = new AlligatorUi(this);
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
                Values = new List<decimal>();
                ValuesUp = new List<decimal>();
                ValuesDown = new List<decimal>();
            }

            Values.Add(GetValueBase(candles, candles.Count - 1));
            ValuesUp.Add(GetValueUp(candles, candles.Count - 1));
            ValuesDown.Add(GetValueDown(candles, candles.Count - 1));
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
            ValuesUp = new List<decimal>();
            ValuesDown = new List<decimal>();


            List<Candle> newCandles = new List<Candle>();

            for (int i = 0; i < candles.Count; i++)
            {
                newCandles.Add(candles[i]);
                Values.Add(GetValueBase(newCandles, i));
                ValuesUp.Add(GetValueUp(newCandles, i));
                ValuesDown.Add(GetValueDown(newCandles, i));
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
            Values[Values.Count - 1] = GetValueBase(candles, candles.Count - 1);
            ValuesUp[ValuesUp.Count - 1] = GetValueUp(candles, candles.Count - 1);
            ValuesDown[ValuesDown.Count - 1] = GetValueDown(candles, candles.Count - 1);
        }

        /// <summary>
        /// верхняя машка
        /// </summary>
        private MovingAverage _maUp;

        /// <summary>
        /// центральная
        /// </summary>
        private MovingAverage _maBase;

        /// <summary>
        /// нижняя
        /// </summary>
        private MovingAverage _maDown;

        /// <summary>
        /// взять значение верхнее
        /// </summary>
        /// <param name="candles">свечи</param>
        /// <param name="index">индекс</param>
        /// <returns>верхняя линия аллигатора по индексу</returns>
        private decimal GetValueUp(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                _maUp = new MovingAverage(false)
                {
                    TypeCalculationAverage = TypeCalculationAverage,
                    Lenght = LenghtUp,
                    TypePointsToSearch = PriceTypePoints.Median
                };
            }

            _maUp.Process(candles);

            if (_maUp.Values.Count - 1 <= ShiftUp)
            {
                return 0;
            }

            return _maUp.Values[_maUp.Values.Count - 1 - ShiftUp];

        }

        /// <summary>
        /// взять значение центральное
        /// </summary>
        /// <param name="candles">свечи</param>
        /// <param name="index">индекс</param>
        /// <returns>центальная линия аллигатора по индексу</returns>
        private decimal GetValueBase(List<Candle> candles,int index)
        {
            if (index == 0)
            {
                _maBase = new MovingAverage(false)
                {TypeCalculationAverage = TypeCalculationAverage,
                 Lenght = LenghtBase,
                 TypePointsToSearch = PriceTypePoints.Median
                };   
            }

            _maBase.Process(candles);

            if (_maBase.Values.Count - 1 <= ShiftBase)
            {
                return 0;
            }

            return _maBase.Values[_maBase.Values.Count - 1 - ShiftBase];
            
        }

        /// <summary>
        /// взять значение нижнее
        /// </summary>
        /// <param name="candles">свечи</param>
        /// <param name="index">индекс</param>
        /// <returns>нижняя линия аллигатора по индексу</returns>
        private decimal GetValueDown(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                _maDown = new MovingAverage(false)
                {
                    TypeCalculationAverage = TypeCalculationAverage,
                    Lenght = LenghtDown,
                    TypePointsToSearch = PriceTypePoints.Median
                };
            }

            _maDown.Process(candles);

            if (_maDown.Values.Count - 1 <= ShiftDown)
            {
                return 0;
            }

            return _maDown.Values[_maDown.Values.Count - 1 - ShiftDown];
        }

    }
}