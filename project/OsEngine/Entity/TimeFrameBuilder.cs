/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.IO;
using System.Text;

namespace OsEngine.Entity
{

    /// <summary>
    /// time frame settings storage
    /// </summary>
    public class TimeFrameBuilder
    {

        public TimeFrameBuilder(string name, StartProgram startProgram)
        {
            _name = name;

            _candleCreateType = CandleMarketDataType.Tick;
            _seriesCreateMethodType = CandleCreateMethodType.Simple;
            TimeFrame = TimeFrame.Min1;
            TradeCount = 100;
            _volumeToCloseCandleInVolumeType = 1000;
            _rencoPunktsToCloseCandleInRencoType = 100;
            _deltaPeriods = 1000;

            if(startProgram != StartProgram.IsOsOptimizer)
            {
                Load();
                _canSave = true;
            }
        }

        public TimeFrameBuilder()
        {
            _candleCreateType = CandleMarketDataType.Tick;
            _seriesCreateMethodType = CandleCreateMethodType.Simple;
            TimeFrame = TimeFrame.Min1;
            TradeCount = 100;
            _volumeToCloseCandleInVolumeType = 1000;
            _rencoPunktsToCloseCandleInRencoType = 100;
            _deltaPeriods = 1000;
            _canSave = true;
        }

        private string _name;

        /// <summary>
        /// upload settings from file system
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

                    Enum.TryParse(reader.ReadLine(), out _seriesCreateMethodType);
                    _volumeToCloseCandleInVolumeType = reader.ReadLine().ToDecimal();
                    _rencoPunktsToCloseCandleInRencoType = reader.ReadLine().ToDecimal();
                    _deltaPeriods = reader.ReadLine().ToDecimal();
                    _rencoIsBuildShadows = Convert.ToBoolean(reader.ReadLine());
                    _reversCandlesPunktsMinMove = reader.ReadLine().ToDecimal();
                    _reversCandlesPunktsBackMove = reader.ReadLine().ToDecimal();
                    _rangeCandlesPunkts = reader.ReadLine().ToDecimal();
                    _saveTradesInCandles = Convert.ToBoolean(reader.ReadLine());

                    reader.Close();
                }
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// save the object settings to a file
        /// </summary>
        public void Save()
        {
            _neadToRebuildSpecification = true;

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
                    writer.WriteLine(_seriesCreateMethodType);
                    writer.WriteLine(_volumeToCloseCandleInVolumeType);
                    writer.WriteLine(_rencoPunktsToCloseCandleInRencoType);
                    writer.WriteLine(_deltaPeriods);
                    writer.WriteLine(_rencoIsBuildShadows);
                    writer.WriteLine(_reversCandlesPunktsMinMove);
                    writer.WriteLine(_reversCandlesPunktsBackMove);
                    writer.WriteLine(_rangeCandlesPunkts);
                    writer.WriteLine(_saveTradesInCandles);
                    writer.Close();
                }
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// is it possible to save the data
        /// </summary>
        private bool _canSave;

        /// <summary>
        /// delete object settings from file system
        /// </summary>
        public void Delete()
        {
            if (File.Exists(@"Engine\" + _name + @"TimeFrameBuilder.txt"))
            {
                File.Delete(@"Engine\" + _name + @"TimeFrameBuilder.txt");
            }
        }

        /// <summary>
        /// unique object settings string
        /// </summary>
        public string Specification
        {
            get
            {
                if(_lastSpecification != null &&
                    _neadToRebuildSpecification == false)
                {
                    return _lastSpecification;
                }

                _neadToRebuildSpecification = false;

                StringBuilder result = new StringBuilder();

                result.Append(_timeFrame + "_");
                result.Append(TimeFrame + "_");
                result.Append(_candleCreateType + "_");
                result.Append(_setForeign + "_");
                result.Append(_tradeCount + "_");
                result.Append(_seriesCreateMethodType + "_");
                result.Append(_volumeToCloseCandleInVolumeType + "_");
                result.Append(_rencoPunktsToCloseCandleInRencoType + "_");
                result.Append(_deltaPeriods + "_");
                result.Append(_rencoIsBuildShadows + "_");
                result.Append(_reversCandlesPunktsMinMove + "_");
                result.Append(_reversCandlesPunktsBackMove + "_");
                result.Append(_rangeCandlesPunkts);

                _lastSpecification = result.ToString();

                return _lastSpecification;
            }
        }
        private bool _neadToRebuildSpecification;

        private string _lastSpecification;

        /// <summary>
        /// timeframe of candlesticks on which the connector is signed
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
                        else if (value == TimeFrame.Hour4)
                        {
                            _timeFrameSpan = new TimeSpan(0, 4, 0, 0);
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
        /// timeFrame of candles in the form of TimeSpan on which the connector is signed
        /// ТаймФрейм свечек в виде TimeSpan на который подписан коннектор
        /// </summary>
        public TimeSpan TimeFrameTimeSpan
        {
            get { return _timeFrameSpan; }
        }
        private TimeSpan _timeFrameSpan;

        /// <summary>
        /// delta period to close the candle in the delta
        /// переод дельты для закрытия свечи по дельте
        /// </summary>
        public decimal DeltaPeriods
        {
            get { return _deltaPeriods; }
            set
            {
                if (_deltaPeriods == value)
                {
                    return;
                }
                _deltaPeriods = value;
                Save();
            }
        }
        private decimal _deltaPeriods;

        /// <summary>
        /// how many trades we pack candlesticks when the candlestick closing mode is enabled
        /// по сколько трейдов пакуем свечи когда включен режим закрытия свечи по кол-ву трейдов
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
        /// is it worth the non-trading periods
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
        /// data from which we collect candles: from ticks or market depths
        /// данные из которых собираем свечи: из тиков или из стаканов
        /// </summary>
        public CandleMarketDataType CandleMarketDataType
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
        private CandleMarketDataType _candleCreateType;

        /// <summary>
        /// the type of candle assembly: normal, Renco, delta, 
        /// тип сборки свечей: обычный, ренко, дельта, 
        /// </summary>
        public CandleCreateMethodType CandleCreateMethodType
        {
            get { return _seriesCreateMethodType; }
            set
            {
                if (value != _seriesCreateMethodType)
                {
                    _seriesCreateMethodType = value;
                    Save();
                }
            }
        }
        private CandleCreateMethodType _seriesCreateMethodType;

        /// <summary>
        /// the volume required to close the candlestick when the mode of closing the candlestick by volume is selected
        /// объём необходимый для закрытия свечи, когда выбран режим закрытия свечи по объёму
        /// </summary>
        public decimal VolumeToCloseCandleInVolumeType
        {
            get { return _volumeToCloseCandleInVolumeType; }
            set
            {
                _volumeToCloseCandleInVolumeType = value;
                Save();
            }
        }
        private decimal _volumeToCloseCandleInVolumeType;

        /// <summary>
        /// the movement required to close the candle when the candle mode is set to Renko
        /// движение необходимое для закрытия свечи, когда выбран режим свечей ренко
        /// </summary>
        public decimal RencoPunktsToCloseCandleInRencoType
        {
            get { return _rencoPunktsToCloseCandleInRencoType; }
            set
            {
                _rencoPunktsToCloseCandleInRencoType = value;
                Save();
            }
        }
        private decimal _rencoPunktsToCloseCandleInRencoType;

        /// <summary>
        /// if we're watching the shadows at the candle when we've chosen Renko. true - build
        /// стороим ли мы тени у свечи когда выбран ренко. true - строим
        /// </summary>
        public bool RencoIsBuildShadows
        {
            get { return _rencoIsBuildShadows; }
            set
            {
                _rencoIsBuildShadows = value;
                Save();
            }
        }
        private bool _rencoIsBuildShadows;

        /// <summary>
        /// Minimum movement for riverse bars
        /// минимальное движение для риверсивных баров
        /// </summary>
        public decimal ReversCandlesPunktsMinMove
        {
            get { return _reversCandlesPunktsMinMove; }
            set
            {
                if (value == _reversCandlesPunktsMinMove)
                {
                    return;
                }
                _reversCandlesPunktsMinMove = value;
                Save();
            }
        }
        private decimal _reversCandlesPunktsMinMove;

        /// <summary>
        /// rollback value for reverse bars
        /// величина отката для риверсивных баров
        /// </summary>
        public decimal ReversCandlesPunktsBackMove
        {
            get { return _reversCandlesPunktsBackMove; }
            set
            {
                if (value == _reversCandlesPunktsBackMove)
                {
                    return;
                }
                _reversCandlesPunktsBackMove = value;
                Save();
            }
        }
        private decimal _reversCandlesPunktsBackMove;

        /// <summary>
        /// value of rage bars
        /// величина рейдж баров
        /// </summary>
        public decimal RangeCandlesPunkts
        {
            get { return _rangeCandlesPunkts; }
            set
            {
                if (value == _rangeCandlesPunkts)
                {
                    return;
                }
                _rangeCandlesPunkts = value;
                Save();
            }
        }
        private decimal _rangeCandlesPunkts;

        public bool SaveTradesInCandles
        {
            get { return _saveTradesInCandles; }
            set
            {
                if (value == _saveTradesInCandles)
                {
                    return;
                }
                _saveTradesInCandles = value;
                Save();
            }
        }

        private bool _saveTradesInCandles;
    }

    /// <summary>
    /// Os.Engine timeframes
    /// </summary>
    public enum TimeFrame
    {
        /// <summary>
        /// one second
        /// </summary>
        Sec1,
        /// <summary>
        /// two seconds
        /// </summary>
        Sec2,
        /// <summary>
        /// five seconds
        /// </summary>
        Sec5,
        /// <summary>
        /// ten seconds
        /// десять секунд
        /// </summary>
        Sec10,
        /// <summary>
        /// fifteen seconds
        /// </summary>
        Sec15,
        /// <summary>
        /// twenty seconds
        /// </summary>
        Sec20,
        /// <summary>
        /// thirty seconds
        /// </summary>
        Sec30,
        /// <summary>
        /// one minute
        /// </summary>
        Min1,
        /// <summary>
        /// two minutes
        /// </summary>
        Min2,
        /// <summary>
        /// three minutes
        /// </summary>
        Min3,
        /// <summary>
        /// five minutes
        /// </summary>
        Min5,
        /// <summary>
        /// ten minutes
        /// </summary>
        Min10,
        /// <summary>
        /// fifteen minutes
        /// </summary>
        Min15,
        /// <summary>
        /// twenty minutes
        /// </summary>
        Min20,
        /// <summary>
        /// thirty minutes
        /// </summary>
        Min30,
        /// <summary>
        /// Forty-five minutes.
        /// </summary>
        Min45,
        /// <summary>
        /// one hour
        /// </summary>
        Hour1,
        /// <summary>
        /// two hours
        /// </summary>
        Hour2,
        /// <summary>
        /// four hours
        /// </summary>
        Hour4,
        /// <summary>
        /// day
        /// </summary>
        Day,
        /// <summary>
        /// trade
        /// </summary>
        Tick,
        /// <summary>
        /// market depth
        /// </summary>
        MarketDepth
    }
}