/*
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
    /// Индикатор Adx. Average Directional Index
    /// </summary>
    public class Adx : IIndicatorCandle
    {
        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="uniqName">уникальное имя</param>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public Adx(string uniqName, bool canDelete)
        {
            Name = uniqName;
            Lenght = 14;
            TypeIndicator = IndicatorOneCandleChartType.Line;
            ColorBase = Color.DodgerBlue;
            PaintOn = true;
            CanDelete = canDelete;
            Load();
        }

        /// <summary>
        /// индикатор без параметнов. Не будет сохраняться
        /// используется ТОЛЬКО для создания составных индикаторов
        /// не используйте его из слоя создания роботов!
        /// </summary>
        /// <param name="canDelete">можно ли пользователю удалить индикатор с графика вручную</param>
        public Adx(bool canDelete)
        {
            Name = Guid.NewGuid().ToString();

            Lenght = 14;
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
        public IndicatorOneCandleChartType TypeIndicator
        { get; set; }

        /// <summary>
        /// имя серии на которой индикатор прорисовывается
        /// </summary>
        public string NameSeries
        { get; set; }

        /// <summary>
        /// имя области на котророй индикатор прорисовывается
        /// </summary>
        public string NameArea
        { get; set; }

        /// <summary>
        /// Adx
        /// </summary>
        public List<decimal> Values 
        { get; set; }

        /// <summary>
        /// уникальное имя индикатора
        /// </summary>
        public string Name
        { get; set; }

        /// <summary>
        /// цвет для прорисовки базовой точки данных
        /// </summary>
        public Color ColorBase
        { get; set; }

        /// <summary>
        /// длинна периода для рассчёта индикатора
        /// </summary>
        public int Lenght;

        /// <summary>
        /// включена ли прорисовка индикатора
        /// </summary>
        public bool PaintOn
        { get; set; }

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return;
            }
            try
            {
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
        /// загрузить настройки
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
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            AdxUi ui = new AdxUi(this);
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
        /// индикатор нужно перерисовать
        /// </summary>
        public event Action<IIndicatorCandle> NeadToReloadEvent;

        /// <summary>
        /// свечки для рассчёта индикатора
        /// </summary>
        private List<Candle> _myCandles;

        /// <summary>
        /// пересчитать индикатор
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
            }

            Values.Add(GetValueStandart(candles, candles.Count - 1));

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

            for (int i = 0; i < candles.Count; i++)
            {
                 Values.Add(GetValueStandart(candles, i));
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

            Values[Values.Count - 1] = GetValueStandart(candles, candles.Count - 1);

        }

// 1 часть переменных. Рассчёт движения за последние свечки

        /// <summary>
        /// положительное направленное движение за свечку
        /// </summary>
        private List<decimal> _dmjPlus;
        private List<decimal> _dmjPlusAverage;

        /// <summary>
        /// отрицательное направленное движение за свечку
        /// </summary>
        private List<decimal> _dmjMinus;
        private List<decimal> _dmjMinusAverage; 

        /// <summary>
        /// истинный диапазон за свечку
        /// </summary>
        private List<decimal> _trueRange;

        private List<decimal> _trueRangeAverage; 

        /// <summary>
        /// движение, через истинный диапазон за свечку
        /// </summary>
        private List<decimal> _sDIjPlus;

        /// <summary>
        /// движение через истинный диапазон за свечку
        /// </summary>
        private List<decimal> _sDIjMinus;

// 2 часть. Рассчёт АДХ наконецто...

        private List<decimal> _dX;

        private List<decimal> _adX;

// 3 расчёт стандартного АДХ

        /// <summary>
        /// рассчитать новое значение
        /// </summary>
        /// <param name="candles">свечи</param>
        /// <param name="index">индекс по которому нужно значение</param>
        /// <returns>значение индикатора</returns>
        public decimal GetValueStandart(List<Candle> candles, int index) 
        {
            if (index == 0)
            {
                _dmjPlus = null;
                _dmjMinus = null;
                _trueRange = null;
                _sDIjPlus = null;
                _sDIjMinus = null;
                _dX = null;
                _adX = null; 
            }
            // 1 рассчитываем новые направленные движения
            DmjReload(candles, index);

            _dmjPlusAverage = MovingAverageWild(_dmjPlus, _dmjPlusAverage, Lenght, index);
            _dmjMinusAverage = MovingAverageWild(_dmjMinus, _dmjMinusAverage, Lenght, index);

            // 2 рассчитываем истинный диапазон

            TrueRangeReload(candles, index);

            _trueRangeAverage = MovingAverageWild(_trueRange, _trueRangeAverage, Lenght, index);

            // 3 сглаживаем движение через истинный диапазон 

            SdijReload(index);


            //_mdiPlus = MovingAverageWild(_sDIjPlus, _mdiPlus, Lenght, index);
            //_mdiMinus = MovingAverageWild(_sDIjMinus, _mdiMinus, Lenght, index);


            // 5 делаем массив DX

            DxReload(index);

            if (Lenght == 0 || Lenght > _dX.Count)
            {
                // если рассчёт не возможен
                return 0;
            }
            else
            { // рассчитываем
                _adX = MovingAverageWild(_dX, _adX, Lenght, index);
                return Math.Round(_adX[_adX.Count - 1],4);
            }
        }

        private void DmjReload(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                _dmjMinus = new List<decimal>();
                _dmjPlus = new List<decimal>();
                _dmjMinus.Add(0);
                _dmjPlus.Add(0);
                return;
            }

            if (index > _dmjMinus.Count - 1)
            {
                _dmjMinus.Add(0);
                _dmjPlus.Add(0);
            }

            decimal upMove = candles[index].High - candles[index - 1].High;
            decimal downMove = candles[index - 1].Low - candles[index].Low;
            if (candles[index].High >= candles[index - 1].High
                &&
                candles[index].High - candles[index - 1].High >= candles[index - 1].Low - candles[index].Low
                )
            {
                _dmjPlus[_dmjPlus.Count - 1] = candles[index].High - candles[index - 1].High;
            }

            if (candles[index].Low <= candles[index-1].Low
                &&
                candles[index].High - candles[index - 1].High <= candles[index - 1].Low - candles[index].Low
                )
            {
                _dmjMinus[_dmjMinus.Count - 1] = candles[index - 1].Low - candles[index].Low;
            }

        }

        private void TrueRangeReload(List<Candle> candles, int index)
        {

            //Истинный диапазон (True Range) есть наибольшая из следующих трех величин:
            //разность между текущими максимумом и минимумом;
            //разность между предыдущей ценой закрытия и текущим максимумом;
            //разность между предыдущей ценой закрытия и текущим минимумом.

            if (index == 0)
            {
                _trueRange = new List<decimal>();
                _trueRange.Add(0);
                return;
            }

            if (index > _trueRange.Count - 1)
            {
                _trueRange.Add(0);
            }

            decimal hiToLow = Math.Abs(candles[index].High - candles[index].Low);
            decimal closeToHigh = Math.Abs(candles[index-1].Close - candles[index].High);
            decimal closeToLow = Math.Abs(candles[index - 1].Close - candles[index].Low);

            _trueRange[_trueRange.Count-1] = Math.Max(Math.Max(hiToLow,closeToHigh),closeToLow);
        }

        private void SdijReload(int index)
        {
            //если TRj не = 0, то +SDIj = +DMj / TRj; -SDIj = -DMj / TRj,
            // если TRj = 0, то +SDIj = 0, — SDIj = 0.

            if (index == 0)
            {
                _sDIjMinus = new List<decimal>();
                _sDIjPlus = new List<decimal>();
                _sDIjMinus.Add(0);
                _sDIjPlus.Add(0);
                return;
            }

            if (index > _sDIjMinus.Count - 1)
            {
                _sDIjMinus.Add(0);
                _sDIjPlus.Add(0);
            }

            decimal trueRange = _trueRange[index];
            decimal dmjiPlus = _dmjPlus[index];
            decimal dmjiMinus = _dmjMinus[index];

            trueRange = _trueRangeAverage[index];
            dmjiPlus = _dmjPlusAverage[index];
            dmjiMinus = _dmjMinusAverage[index];



            if (trueRange == 0)
            {
                _sDIjPlus[_sDIjPlus.Count - 1] = 0;
                _sDIjMinus[_sDIjMinus.Count - 1] = 0;
            }
            else
            {
                _sDIjPlus[_sDIjPlus.Count - 1] = Math.Round(100 *dmjiPlus / trueRange,0);
                _sDIjMinus[_sDIjMinus.Count - 1] = Math.Round(100 * dmjiMinus / trueRange, 0);
            }
        }

        private List<decimal> MovingAverageWild(List<decimal> valuesSeries, List<decimal> moving, int length, int index)
        {
            if (moving == null || length > valuesSeries.Count)
            {
                moving = new List<decimal>();
                for (int i = 0; i < index + 1; i++)
                {
                    moving.Add(0);
                }
            }
            else if (length == valuesSeries.Count)
            { // это первое значение. Рассчитываем как простую машку

                decimal lastMoving = 0;

                for (int i = index; i > -1 && i > valuesSeries.Count - 1 - length; i--)
                {
                    lastMoving += valuesSeries[i];
                }
                if (lastMoving != 0)
                {
                    moving.Add(lastMoving / length);
                }
                else
                {
                    moving.Add(0);
                }

            }
            else
            {

                decimal lastValueMoving;
                decimal lastValueSeries = valuesSeries[valuesSeries.Count - 1];

                if (index > moving.Count - 1)
                {
                    lastValueMoving = moving[moving.Count - 1];
                    moving.Add(0);
                }
                else
                {
                    lastValueMoving = moving[moving.Count - 2];
                }

                moving[moving.Count - 1] = (lastValueMoving * (Lenght - 1) + lastValueSeries)/Lenght;

            }

            return moving;
        }

        private void DxReload(int index)
        {
            if (index == 0)
            {
                _dX = new List<decimal>();
                _dX.Add(0);
                return;
            }

            if (index > _dX.Count - 1)
            {
                _dX.Add(0);
            }

            if (_sDIjPlus[_sDIjPlus.Count - 1] == 0 ||
                _sDIjMinus[_sDIjMinus.Count - 1] == 0)
            {
                _dX[_dX.Count - 1] = 0;
            }
            else
            {
                _dX[_dX.Count - 1] = Math.Round((100 * Math.Abs(_sDIjPlus[_sDIjPlus.Count - 1] - _sDIjMinus[_sDIjMinus.Count - 1])) /
                                     Math.Abs(_sDIjPlus[_sDIjPlus.Count - 1] + _sDIjMinus[_sDIjMinus.Count - 1]));
                
            }
        }

    }

    /// <summary>
    /// тип расчёта АДХ
    /// </summary>
    public enum AdxType
    {
        /// <summary>
        /// академический расчёт
        /// </summary>
        Standart,
        /// <summary>
        /// рассчёт как в велслаб
        /// </summary>
        WealthLab
    }
}
