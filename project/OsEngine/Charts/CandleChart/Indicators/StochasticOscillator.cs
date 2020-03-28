/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace OsEngine.Charts.CandleChart.Indicators
{
    public class StochasticOscillator : IIndicator
    {
        /// <summary>
        /// Period 1
        /// Переод 1
        /// </summary>
        public int P1;

        /// <summary>
        /// period 2
        /// Период 2
        /// </summary>
        public int P2;

        /// <summary>
        /// period 3
        /// период 3
        /// </summary>
        public int P3;

        public MovingAverageTypeCalculation TypeCalculationAverage;

        /// <summary>
        /// constructor with parameters. Indicator will be saved
        /// конструктор с параметрами. Индикатор будет сохраняться
        /// </summary>
        /// <param name="uniqName">unique name/уникальное имя</param>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public StochasticOscillator(string uniqName, bool canDelete)
        {
            Name = uniqName;
            TypeIndicator = IndicatorChartPaintType.Line;
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
        /// constructor without parameters.Indicator will not saved/конструктор без параметров. Индикатор не будет сохраняться
        /// used ONLY to create composite indicators/используется ТОЛЬКО для создания составных индикаторов
        /// Don't use it from robot creation layer/не используйте его из слоя создания роботов!
        /// </summary>
        /// <param name="canDelete">whether user can remove indicator from chart manually/можно ли пользователю удалить индикатор с графика вручную</param>
        public StochasticOscillator(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            TypeIndicator = IndicatorChartPaintType.Line;
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
        /// all indicator values
        /// все значения индикатора
        /// </summary>
        List<List<decimal>> IIndicator.ValuesToChart
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
        /// indicator colors
        /// цвета для индикатора
        /// </summary>
        List<Color> IIndicator.Colors
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
        /// whether indicator can be removed from chart. This is necessary so that robots can't be removed /можно ли удалить индикатор с графика. Это нужно для того чтобы у роботов нельзя было удалить 
        /// indicators he needs in trading/индикаторы которые ему нужны в торговле
        /// </summary>
        public bool CanDelete { get; set; }

        /// <summary>
        /// indicator drawing type
        /// тип прорисовки индикатора
        /// </summary>
        public IndicatorChartPaintType TypeIndicator { get; set; }

        /// <summary>
        /// name of data series on which indicator will be drawn
        /// имя серии данных на которой будет прорисован индикатор
        /// </summary>
        public string NameSeries { get; set; }

        /// <summary>
        /// name of data area where indicator will be drawn
        /// имя области данных на которой будет прорисовываться индикатор
        /// </summary>
        public string NameArea { get; set; }

        /// <summary>
        /// empty
        /// пусто
        /// </summary>
        public List<decimal> ValuesUp
        { get; set; }

        /// <summary>
        /// empty
        /// пусто
        /// </summary>
        public List<decimal> ValuesDown
        { get; set; }

        /// <summary>
        /// unique indicator name
        /// уникальное имя индикатора
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// color of central data series (not used)
        /// цвет верхней серии данных (не используется)
        /// </summary>
        public Color ColorUp { get; set; }

        /// <summary>
        /// color of lower data series(not used)
        /// цвет нижней серии данных (не используется)
        /// </summary>
        public Color ColorDown { get; set; }

        /// <summary>
        /// is indicator tracing enabled
        /// включена ли прорисовка индикатора
        /// </summary>
        public bool PaintOn { get; set; }

        /// <summary>
        /// save settings to file
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
                // send to log
                // отправить в лог
            }
        }

        /// <summary>
        /// upload settings from file
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
            if (ValuesUp != null)
            {
                ValuesUp.Clear();
                ValuesDown.Clear();
            }
            _myCandles = null;
        }

        /// <summary>
        /// display settings window
        /// показать окно с настройками
        /// </summary>
        public void ShowDialog()
        {
             StochasticOscillatorUi ui = new StochasticOscillatorUi(this);

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
        /// candles to calculate indicator
        /// свечи для рассчёта индикатора
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// calculate indicator
        /// рассчитать индикатор
        /// </summary>
        /// <param name="candles">candles/свечи</param>
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
        /// indicator needs to be redrawn
        /// индикатор нужно перерисовать
        /// </summary>
        public event Action<IIndicator> NeadToReloadEvent;

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
        /// to upload from the beginning
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
        /// overload last value
        /// перегрузить последнее значение
        /// </summary>
        private void ProcessLast(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }
            _t1[_t1.Count - 1] = GetT1(candles, candles.Count - 1);
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
        /// to keep the difference Close- Low
        /// для хранения разницы клоуз - лоу
        /// </summary>
        private List<decimal> _t1;

        /// <summary>
        /// to keep the difference High - Low
        /// для хранения разницы хай - лоу
        /// </summary>
        private List<decimal> _t2;

        /// <summary>
        /// ma for smoothing Close - Low
        /// машка для сглаживания клоуз - лоу
        /// </summary>
        private MovingAverage _tM1;

        /// <summary>
        /// ma for smoothing High - low
        /// машка для сглаживания хай - лоу
        /// </summary>
        private MovingAverage _tM2;

        /// <summary>
        /// first line
        /// первая линия
        /// </summary>
        private List<decimal> _k;

        /// <summary>
        /// ma for smoothing K
        /// машкая для сглаживания К
        /// </summary>
        private MovingAverage _kM;

        // Three settings
        // P1 - length of which we look back in time with Low and High // 5
        // P2 - length of which we average these Low and High         // 3
        // P3 -  length of which we average last ma                  // 3
        // Три настройки
        // P1 - длинна на которую мы в прошлое смотрим хаи с лоями // 5
        // P2 - длинна на которую мы эти лои и хаи усредняем       // 3
        // P3 - длинна на которую усредняем последнюю машку        // 3
        // take an array of Highs and Lows
        // берём массив хаёв и лоёв
        //H_tmp[I]=Value(I,"High",ds)
        //L_tmp[I]=Value(I,"Low",ds)

        //if I>=P then

        // taking highest high and lowest low for I-P to I
        // берём максимальный хай и минимальный лой за I-P до I
        //	local HHV = math.max(unpack(H_tmp,I-P+1,I)) 
        //	local LLV = math.min(unpack(L_tmp,I-P+1,I))
        // 1 calculating  _tkma1 _tkma2
        // 1 рассчитываем _tkma1 _tkma2

        //	t_K_MA1[I-P+1] = "Close" - LLV
        //	t_K_MA2[I-P+1] = HHV - LLV
        // 2 average the values found
        // 2 усредняем найденные значения 

        //	local v_K_MA1 = K_MA1(I-P+1, {Period=S, Metod = M, VType="Any", round=R}, t_K_MA1)
        //	local v_K_MA2 = K_MA2(I-P+1, {Period=S, Metod = M, VType="Any", round=R}, t_K_MA2)

        //	if I>=P+S-1 then
        // 3 find first value
        // 3 находим первое значение
        //		t_K[I-(P+S-2)] = 100 * v_K_MA1 / v_K_MA2
        // 4 averaging and finding last value
        // 4 усредняем и находим последнее значение
        //		return rounding(t_K[I-(P+S-2)], R), rounding(D_MA(I-(P+S-2), {Period=PD, Metod = MD, VType="Any", round=R}, t_K), R),20,80
        //	end
        //end

    }
}
