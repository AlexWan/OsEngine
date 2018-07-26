/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;

namespace OsEngine.Entity
{

    /// <summary>
    /// класс хранящий настройки таймФрейма для робота
    /// </summary>
    public class TimeFrameBuilder
    {
        private string _name;
        public TimeFrameBuilder(string name)
        {
            _name = name;
            DeltaPeriods = new CumulativeDeltaPeriods();
            _candleCreateType = CandleSeriesCreateDataType.Tick;
            TimeFrame = TimeFrame.Min1;
            TradeCount = 100;
            Load();
            _canSave = true;
        }

        public TimeFrameBuilder()
        {
            DeltaPeriods = new CumulativeDeltaPeriods();
            TimeFrame = TimeFrame.Min1;
            _candleCreateType = CandleSeriesCreateDataType.Tick;
            _canSave = true;
        }

        /// <summary>
        /// загрузить
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + _name + @"TimeFrameBuilder.txt"))
            {
                TimeFrame = TimeFrame.Min1;
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + _name + @"TimeFrameBuilder.txt"))
                {

                    TimeFrame frame;
                    Enum.TryParse(reader.ReadLine(), out frame);
                    TimeFrame = frame;

                    Enum.TryParse(reader.ReadLine(), true, out _candleCreateType);

                    _setForeign = Convert.ToBoolean(reader.ReadLine());
                    _tradeCount = Convert.ToInt32(reader.ReadLine());

                    if (!reader.EndOfStream)
                    {
                        DeltaPeriods.Periods = new List<CumulativeDeltaPeriod>();

                        for (int i = 0; i < 24; i++)
                        {
                            string currentPeriod = reader.ReadLine();
                            if (currentPeriod == null)
                            {
                                return;
                            }
                            DeltaPeriods.Periods.Add(new CumulativeDeltaPeriod()
                            {
                                DeltaStep = Convert.ToInt32(currentPeriod.Split('%')[0]),
                                HourStart = Convert.ToInt32(currentPeriod.Split('%')[1]),
                                HourEnd = Convert.ToInt32(currentPeriod.Split('%')[2])
                            });
                        }
                    }




                    reader.Close();
                }
            }
            catch 
            {
              // ignore
            }
        }

        /// <summary>
        /// сохранить настройки объекта в файл
        /// </summary>
        public void Save()
        {
            if (_canSave == false)
            {
                return;
            }
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _name + @"TimeFrameBuilder.txt", false))
                {
                    writer.WriteLine(TimeFrame);
                    writer.WriteLine(_candleCreateType);
                    writer.WriteLine(_setForeign);
                    writer.WriteLine(_tradeCount);

                    for (int i = 0; i < DeltaPeriods.Periods.Count; i++)
                    {
                        writer.WriteLine(DeltaPeriods.Periods[i].DeltaStep + "%" + DeltaPeriods.Periods[i].HourStart + "%" + DeltaPeriods.Periods[i].HourEnd);
                    }

                    writer.Close();
                }
            }
            catch
            {
                 // ignore
            }
        }

        /// <summary>
        /// можно ли сохранять данные
        /// </summary>
        private bool _canSave;

        /// <summary>
        /// удалить настройки объекта
        /// </summary>
        public void Delete()
        {
            if (File.Exists(@"Engine\" + _name + @"TimeFrameBuilder.txt"))
            {
                File.Delete(@"Engine\" + _name + @"TimeFrameBuilder.txt");
            }
        }

        /// <summary>
        /// ТаймФрейм свечек на который подписан коннектор
        /// </summary>
        public TimeFrame TimeFrame
        {
            get { return _timeFrame; }
            set
            {
                try
                {
                    if (value != _timeFrame)
                    {
                        _timeFrame = value;
                        if (value == TimeFrame.Sec1)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 0, 1);
                        }
                        else if (value == TimeFrame.Sec2)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 0, 2);
                        }
                        else if (value == TimeFrame.Sec5)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 0, 5);
                        }
                        else if (value == TimeFrame.Sec10)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 0, 10);
                        }
                        else if (value == TimeFrame.Sec15)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 0, 15);
                        }
                        else if (value == TimeFrame.Sec20)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 0, 20);
                        }
                        else if (value == TimeFrame.Sec30)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 0, 30);
                        }
                        else if (value == TimeFrame.Min1)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 1, 0);
                        }
                        else if (value == TimeFrame.Min2)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 2, 0);
                        }
                        else if (value == TimeFrame.Min3)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 3, 0);
                        }
                        else if (value == TimeFrame.Min5)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 5, 0);
                        }
                        else if (value == TimeFrame.Min10)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 10, 0);
                        }
                        else if (value == TimeFrame.Min15)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 15, 0);
                        }
                        else if (value == TimeFrame.Min20)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 20, 0);
                        }
                        else if (value == TimeFrame.Min30)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 30, 0);
                        }
                        else if (value == TimeFrame.Min45)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 45, 0);
                        }
                        else if (value == TimeFrame.Hour1)
                        {
                            _timeFrameSpan = new TimeSpan(0, 1, 0, 0);
                        }
                        else if (value == TimeFrame.Hour2)
                        {
                            _timeFrameSpan = new TimeSpan(0, 2, 0, 0);
                        }
                        else if (value == TimeFrame.Day)
                        {
                            _timeFrameSpan = new TimeSpan(0, 24, 0, 0);
                        }
                        Save();
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }
        private TimeFrame _timeFrame;

        /// <summary>
        /// ТаймФрейм свечек в виде TimeSpan на который подписан коннектор
        /// </summary>
        public TimeSpan TimeFrameTimeSpan
        {
            get { return _timeFrameSpan; }
        }
        private TimeSpan _timeFrameSpan;

        public CumulativeDeltaPeriods DeltaPeriods;

        /// <summary>
        /// по сколько трейдов пакуем тики когда включен таймФрейм Трейды
        /// </summary>
        public int TradeCount
        {
            get { return _tradeCount; }
            set
            {
                if (value != _tradeCount)
                {
                    _tradeCount = value;
                    Save();
                }
            }
        }
        private int _tradeCount;

        /// <summary>
        /// нужно ли стоить неторговые периоды
        /// </summary>
        public bool SetForeign
        {
            get { return _setForeign; }
            set
            {
                if (value != _setForeign)
                {
                    _setForeign = value;
                    Save();
                }
            }
        }
        private bool _setForeign;

        /// <summary>
        /// способ создания свечей: из тиков или из стаканов
        /// </summary>
        public CandleSeriesCreateDataType CandleCreateType
        {
            get { return _candleCreateType; }
            set
            {
                if (value != _candleCreateType)
                {
                    _candleCreateType = value;
                    Save();
                }
            }
        }
        private CandleSeriesCreateDataType _candleCreateType;
    }

    /// <summary>
    /// таймФреймы Os.Engine
    /// </summary>
    public enum TimeFrame
    {
        /// <summary>
        /// тиковый таймФрейм
        /// </summary>
        Tick,
        /// <summary>
        /// таймФрейм основанный на изменении дельты потока покупок и продаж
        /// </summary>
        Delta,
        /// <summary>
        /// одна секунда
        /// </summary>
        Sec1,
        /// <summary>
        /// две секунды
        /// </summary>
        Sec2,
        /// <summary>
        /// пять секунд
        /// </summary>
        Sec5,
        /// <summary>
        /// десять секунд
        /// </summary>
        Sec10,
        /// <summary>
        /// пятнадцать секунд
        /// </summary>
        Sec15,
        /// <summary>
        /// двадцать секунд
        /// </summary>
        Sec20,
        /// <summary>
        /// тридцать секунд
        /// </summary>
        Sec30,
        /// <summary>
        /// одна минута
        /// </summary>
        Min1,
        /// <summary>
        /// две минуты
        /// </summary>
        Min2,
        /// <summary>
        /// три минуты
        /// </summary>
        Min3,
        /// <summary>
        /// пять минут
        /// </summary>
        Min5,
        /// <summary>
        /// десять минут
        /// </summary>
        Min10,
        /// <summary>
        /// пятнадцать минут
        /// </summary>
        Min15,
        /// <summary>
        /// двадцать минут
        /// </summary>
        Min20,
        /// <summary>
        /// тридцать минут
        /// </summary>
        Min30,
        /// <summary>
        /// сорок пять минут
        /// </summary>
        Min45,
        /// <summary>
        /// один час
        /// </summary>
        Hour1,
        /// <summary>
        /// два часа
        /// </summary>
        Hour2,
        /// <summary>
        /// день
        /// </summary>
        Day
    }

    /// <summary>
    /// объект помогающий строить свечи по изменению дельты открытых позиций
    /// </summary>
    public class CumulativeDeltaPeriods
    {
        public CumulativeDeltaPeriods()
        {
            Periods = new List<CumulativeDeltaPeriod>();

            for (int i = 0; i < 24; i++)
            {
                Periods.Add(new CumulativeDeltaPeriod() { DeltaStep = 1000, HourStart = i, HourEnd = i });
            }
        }

        /// <summary>
        /// периоды
        /// </summary>
        public List<CumulativeDeltaPeriod> Periods;

        /// <summary>
        /// текущая дельта
        /// </summary>
        private decimal _commulativDelta;

        /// <summary>
        /// значение дельты в последний раз когда мы закрывали свечу
        /// </summary>
        private decimal _deltaOnLastCandleClose;

        /// <summary>
        /// проверить наступление события закрытия свечи
        /// </summary>
        public bool CheckCloseCandle(DateTime time, decimal price, decimal volume, bool canPushUp, Side side)
        {
            if (side == Side.Buy)
            {
                _commulativDelta += volume;
            }
            else if (side == Side.Sell)
            {
                _commulativDelta -= volume;
            }

            CumulativeDeltaPeriod myPeriod =
                Periods.Find(period => period.HourStart <= time.Hour && period.HourEnd >= time.Hour);

            if (_commulativDelta > _deltaOnLastCandleClose + myPeriod.DeltaStep ||
                _commulativDelta < _deltaOnLastCandleClose - myPeriod.DeltaStep)
            {
                _deltaOnLastCandleClose = _commulativDelta;
                return true;
            }

            return false;
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            CumulativeDeltaTimeFrameUi ui = new CumulativeDeltaTimeFrameUi(this);
            ui.ShowDialog();
        }
    }

    /// <summary>
    /// дельта на временном уровне
    /// </summary>
    public class CumulativeDeltaPeriod
    {
        /// <summary>
        /// начало периода
        /// </summary>
        public int HourStart;

        /// <summary>
        /// конец периода
        /// </summary>
        public int HourEnd;

        /// <summary>
        /// шаг свечей по дельте покупок и продаж
        /// </summary>
        public int DeltaStep;
    }
}
