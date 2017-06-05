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
    public class StochasticOscillator : IIndicatorCandle
    {
        /// <summary>
        /// Переод 1
        /// </summary>
        public int P1;

        /// <summary>
        /// Период 2
        /// </summary>
        public int P2;

        /// <summary>
        /// период 3
        /// </summary>
        public int P3;

        public MovingAverageTypeCalculation TypeCalculationAverage;

        /// <summary>
        /// конструктор с параметрами. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">уникальное имя</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public StochasticOscillator(string uniqName, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorOneCandleChartType.Line;
            TypeCalculationAverage = MovingAverageTypeCalculation.Simple;
            P1 = 5;
            P2 = 3;
            P3 = 3;
            ColorUp = Color.DodgerBlue;
            ColorDown = Color.DarkRed;
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
        public StochasticOscillator(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            TypeIndicator = IndicatorOneCandleChartType.Line;
            TypeCalculationAverage = MovingAverageTypeCalculation.Simple;
            P1 = 5;
            P2 = 3;
            P3 = 3;
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
        /// пусто
        /// </summary>
        public List<decimal> ValuesUp
        { get; set; }

        /// <summary>
        /// пусто
        /// </summary>
        public List<decimal> ValuesDown
        { get; set; }

        /// <summary>
        /// уникальное имя индикатора
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// цвет верхней серии данных (не используется)
        /// </summary>
        public Color ColorUp { get; set; }

        /// <summary>
        /// цвет нижней серии данных (не используется)
        /// </summary>
        public Color ColorDown { get; set; }

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
                    TypeCalculationAverage = MovingAverageTypeCalculation.Simple;
                    writer.WriteLine(P1);
                    writer.WriteLine(P2);
                    writer.WriteLine(P3);
                    writer.WriteLine(TypeCalculationAverage);
                    writer.WriteLine();
                    writer.WriteLine(ColorUp.ToArgb());
                    writer.WriteLine(ColorDown.ToArgb());
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
                    Enum.TryParse(reader.ReadLine(), true, out TypeCalculationAverage);
                    P1 = Convert.ToInt32(reader.ReadLine());
                    P2 = Convert.ToInt32(reader.ReadLine());
                    P3 = Convert.ToInt32(reader.ReadLine());
                    ColorUp = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorDown = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
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
             StochasticOscillatorUi ui = new StochasticOscillatorUi(this);

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

            if (_t1 == null)
            {
                ValuesUp = new List<decimal>();
                ValuesDown = new List<decimal>();

                _t1 = new List<decimal>();
                _t2 = new List<decimal>();

                _tM1 = new MovingAverage(false);
                _tM1.Lenght = P2;
                _tM1.TypeCalculationAverage = TypeCalculationAverage;

                _tM2 = new MovingAverage(false);
                _tM2.Lenght = P2;
                _tM2.TypeCalculationAverage = TypeCalculationAverage;

                _k = new List<decimal>();

                _kM = new MovingAverage(false);
                _kM.Lenght = P3;
                _kM.TypeCalculationAverage = TypeCalculationAverage;
            }

            if (ValuesUp != null &&
                ValuesUp.Count + 1 == candles.Count)
            {
                ProcessOne(candles);
            }
            else if (ValuesUp != null &&
                     ValuesUp.Count == candles.Count)
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
                ValuesDown = new List<decimal>();
            }

            _t1.Add(GetT1(candles, candles.Count-1));
            _t2.Add(GetT2(candles, candles.Count - 1));

            _tM1.Process(_t1);
            _tM2.Process(_t2);

            _k.Add(GetK(candles.Count - 1));
            _kM.Process(_k);

            ValuesUp.Add(Math.Round(_k[_k.Count - 1],2));
            ValuesDown.Add(Math.Round(_kM.Values[_kM.Values.Count - 1],2));
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

            _t1 = new List<decimal>();
            _t2 = new List<decimal>();

            _tM1 = new MovingAverage(false);
            _tM1.Lenght = P2;
            _tM1.TypeCalculationAverage = TypeCalculationAverage;

            _tM2 = new MovingAverage(false);
            _tM2.Lenght = P2;
            _tM2.TypeCalculationAverage = TypeCalculationAverage;

            _k = new List<decimal>();

            _kM = new MovingAverage(false);
            _kM.Lenght = P3;
            _kM.TypeCalculationAverage = TypeCalculationAverage;

            for (int i = 0; i < candles.Count; i++)
            {
                _t1.Add(GetT1(candles,i));
                _t2.Add(GetT2(candles, i));

                _tM1.Process(_t1);
                _tM2.Process(_t2);

                _k.Add(GetK(i));
                _kM.Process(_k);

                ValuesUp.Add(Math.Round(_k[_k.Count-1],2));
                ValuesDown.Add(Math.Round(_kM.Values[_kM.Values.Count-1],2));
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
            _t1[_t1.Count-1] = GetT1(candles, candles.Count - 1);
            _t2[_t2.Count - 1] = GetT2(candles, candles.Count - 1);

            _tM1.Process(_t1);
            _tM2.Process(_t2);
            _k[_k.Count - 1] = GetK(candles.Count - 1);
            _kM.Process(_k);

            ValuesUp[ValuesUp.Count-1] = Math.Round(_k[_k.Count - 1],2) ;
            ValuesDown[ValuesDown.Count-1] = Math.Round(_kM.Values[_kM.Values.Count - 1],2);
        }


        private decimal GetT1(List<Candle> candles, int index)
        {
            if (index - P1 + 1 <= 0)
            {
                return 0;
            }

            decimal low = decimal.MaxValue;

            for (int i = index - P1 + 1; i < index + 1; i++)
            {
                if (candles[i].Low < low)
                {
                    low = candles[i].Low;
                }
            }

            return candles[index].Close - low;
        }

        private decimal GetT2(List<Candle> candles, int index)
        {
            if (index - P1 + 1 <= 0)
            {
                return 0;
            }

            decimal low = decimal.MaxValue;

            for (int i = index - P1 + 1; i < index + 1; i++)
            {
                if (candles[i].Low < low)
                {
                    low = candles[i].Low;
                }
            }

            decimal hi = 0;

            for (int i = index - P1 + 1; i < index + 1; i++)
            {
                if (candles[i].High > hi)
                {
                    hi = candles[i].High;
                }
            }
            return hi - low;
        }

        private decimal GetK(int index)
        {
            if (index < P2 + P3 +3 ||
                _tM2.Values[index] == 0 ||
                _tM1.Values[index] == 0)
            {
                return 0;
            }

            return 100 * _tM1.Values[index] / _tM2.Values[index];
        }

        /// <summary>
        /// для хранения разницы клоуз - лоу
        /// </summary>
        private List<decimal> _t1;

        /// <summary>
        /// для хранения разницы хай - лоу
        /// </summary>
        private List<decimal> _t2;

        /// <summary>
        /// машка для сглаживания клоуз - лоу
        /// </summary>
        private MovingAverage _tM1;

        /// <summary>
        /// машка для сглаживания хай - лоу
        /// </summary>
        private MovingAverage _tM2;

        /// <summary>
        /// первая линия
        /// </summary>
        private List<decimal> _k;

        /// <summary>
        /// машкая для сглаживания К
        /// </summary>
        private MovingAverage _kM;

// Три настройки
        // P1 - длинна на которую мы в прошлое смотрим хаи с лоями // 5
        // P2 - длинна на которую мы эти лои и хаи усредняем       // 3
        // P3 - длинна на которую усредняем последнюю машку        // 3

        // берём массив хаёв и лоёв
        //H_tmp[I]=Value(I,"High",ds)
        //L_tmp[I]=Value(I,"Low",ds)

//if I>=P then


// берём максимальный хай и минимальный лой за I-P до I
//	local HHV = math.max(unpack(H_tmp,I-P+1,I)) 
//	local LLV = math.min(unpack(L_tmp,I-P+1,I))
 
        // 1 рассчитываем _tkma1 _tkma2

//	t_K_MA1[I-P+1] = "Close" - LLV
//	t_K_MA2[I-P+1] = HHV - LLV

        // 2 усредняем найденные значения 

//	local v_K_MA1 = K_MA1(I-P+1, {Period=S, Metod = M, VType="Any", round=R}, t_K_MA1)
//	local v_K_MA2 = K_MA2(I-P+1, {Period=S, Metod = M, VType="Any", round=R}, t_K_MA2)

//	if I>=P+S-1 then
        // 3 находим первое значение
//		t_K[I-(P+S-2)] = 100 * v_K_MA1 / v_K_MA2

        // 4 усредняем и находим последнее значение
//		return rounding(t_K[I-(P+S-2)], R), rounding(D_MA(I-(P+S-2), {Period=PD, Metod = MD, VType="Any", round=R}, t_K), R),20,80
//	end
//end

    }
}
