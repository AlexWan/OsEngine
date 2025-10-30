/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OsEngine.Candles;
using OsEngine.Candles.Factory;
using OsEngine.Candles.Series;

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
            _startProgram = startProgram;

            if (startProgram != StartProgram.IsOsOptimizer)
            {
                Load();
                _canSave = true;
            }
            else
            {
                CandleSeriesRealization = CandleFactory.CreateCandleSeriesRealization("Simple");
                CandleSeriesRealization.Init(startProgram);
                CandleSeriesRealization.ParametersChangeByUser += CandleSeriesRealization_ParametersChangeByUser;
                CandleSeriesRealization.CandleUpdateEvent += CandleSeriesRealization_CandleUpdateEvent;
                CandleSeriesRealization.CandleFinishedEvent += CandleSeriesRealization_CandleFinishedEvent;
                TimeFrame = TimeFrame.Min1;
            }
        }

        public TimeFrameBuilder(StartProgram startProgram)
        {
            _startProgram = startProgram;
            CandleSeriesRealization = CandleFactory.CreateCandleSeriesRealization("Simple");
            CandleSeriesRealization.Init(_startProgram);
            CandleSeriesRealization.ParametersChangeByUser += CandleSeriesRealization_ParametersChangeByUser;
            CandleSeriesRealization.CandleUpdateEvent += CandleSeriesRealization_CandleUpdateEvent;
            CandleSeriesRealization.CandleFinishedEvent += CandleSeriesRealization_CandleFinishedEvent;
            TimeFrame = TimeFrame.Min1;
            _canSave = true;
        }

        public ACandlesSeriesRealization CandleSeriesRealization;
         
        private string _name;

        private StartProgram _startProgram;

        private void Load()
        {
            if (!File.Exists(@"Engine\" + _name + @"TimeFrameBuilder.txt"))
            {
                CandleSeriesRealization = CandleFactory.CreateCandleSeriesRealization("Simple");
                CandleSeriesRealization.Init(_startProgram);
                CandleSeriesRealization.ParametersChangeByUser += CandleSeriesRealization_ParametersChangeByUser;
                CandleSeriesRealization.CandleUpdateEvent += CandleSeriesRealization_CandleUpdateEvent;
                CandleSeriesRealization.CandleFinishedEvent += CandleSeriesRealization_CandleFinishedEvent;
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + _name + @"TimeFrameBuilder.txt"))
                {

                    TimeFrame frame;
                    Enum.TryParse(reader.ReadLine(), out frame);
                    
                    _saveTradesInCandles = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out _candleCreateType);

                    string seriesName = reader.ReadLine();
                    CandleSeriesRealization = CandleFactory.CreateCandleSeriesRealization(seriesName);
                    CandleSeriesRealization.Init(_startProgram);
                    CandleSeriesRealization.SetSaveString(reader.ReadLine());
                    CandleSeriesRealization.OnStateChange(CandleSeriesState.ParametersChange);
                    TimeFrame = frame;

                    CandleSeriesRealization.ParametersChangeByUser += CandleSeriesRealization_ParametersChangeByUser;
                    CandleSeriesRealization.CandleUpdateEvent += CandleSeriesRealization_CandleUpdateEvent;
                    CandleSeriesRealization.CandleFinishedEvent += CandleSeriesRealization_CandleFinishedEvent;

                    if(reader.EndOfStream == false)
                    {
                        _marketDepthBuildMaxSpreadIsOn = Convert.ToBoolean(reader.ReadLine());
                    }
                    if (reader.EndOfStream == false)
                    {
                        _marketDepthBuildMaxSpread = reader.ReadLine().ToDecimal();
                    }

                    reader.Close();
                }
            }
            catch
            {
                // ignore
            }

            if (CandleSeriesRealization == null)
            {
                CandleSeriesRealization = CandleFactory.CreateCandleSeriesRealization("Simple");
                CandleSeriesRealization.Init(_startProgram);
                CandleSeriesRealization.ParametersChangeByUser += CandleSeriesRealization_ParametersChangeByUser;
                CandleSeriesRealization.CandleUpdateEvent += CandleSeriesRealization_CandleUpdateEvent;
                CandleSeriesRealization.CandleFinishedEvent += CandleSeriesRealization_CandleFinishedEvent;
            }
        }

        private void CandleSeriesRealization_ParametersChangeByUser()
        {
            Save();
        }

        public void Save()
        {
            _needToRebuildSpecification = true;

            if (_canSave == false)
            {
                return;
            }
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _name + @"TimeFrameBuilder.txt", false))
                {
                    writer.WriteLine(TimeFrame);
                    writer.WriteLine(_saveTradesInCandles);
                    writer.WriteLine(_candleCreateType);
                    writer.WriteLine(CandleSeriesRealization.GetType().Name);
                    writer.WriteLine(CandleSeriesRealization.GetSaveString());

                    writer.WriteLine(_marketDepthBuildMaxSpreadIsOn);
                    writer.WriteLine(_marketDepthBuildMaxSpread);

                    writer.Close();
                }
            }
            catch
            {
                // ignore
            }
        }

        private bool _canSave;

        public void Delete()
        {
            try
            {
                if (File.Exists(@"Engine\" + _name + @"TimeFrameBuilder.txt"))
                {
                    File.Delete(@"Engine\" + _name + @"TimeFrameBuilder.txt");
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                if (CandleSeriesRealization != null)
                {
                    CandleSeriesRealization.ParametersChangeByUser -= CandleSeriesRealization_ParametersChangeByUser;
                    CandleSeriesRealization.CandleUpdateEvent -= CandleSeriesRealization_CandleUpdateEvent;
                    CandleSeriesRealization.CandleFinishedEvent -= CandleSeriesRealization_CandleFinishedEvent;
                    CandleSeriesRealization.Delete();
                    CandleSeriesRealization = null;
                }
            }
            catch
            {
                // ignore
            }
        }

        public string CandleCreateMethodType
        {
            get
            {
                if (_candleCreateMethodType == null)
                {
                    _candleCreateMethodType = CandleSeriesRealization.GetType().Name;
                }

                return _candleCreateMethodType;
            }
            set
            {
                string newType = value;

                if (newType == _candleCreateMethodType)
                {
                    return;
                }

                if (CandleSeriesRealization != null)
                {
                    CandleSeriesRealization.ParametersChangeByUser -= CandleSeriesRealization_ParametersChangeByUser;
                    CandleSeriesRealization.CandleUpdateEvent -= CandleSeriesRealization_CandleUpdateEvent;
                    CandleSeriesRealization.CandleFinishedEvent -= CandleSeriesRealization_CandleFinishedEvent;
                    CandleSeriesRealization.Delete();
                    CandleSeriesRealization = null;
                }
                _candleCreateMethodType = newType;
                CandleSeriesRealization = CandleFactory.CreateCandleSeriesRealization(newType);
                CandleSeriesRealization.Init(_startProgram);
                CandleSeriesRealization.ParametersChangeByUser += CandleSeriesRealization_ParametersChangeByUser;
                CandleSeriesRealization.CandleUpdateEvent += CandleSeriesRealization_CandleUpdateEvent;
                CandleSeriesRealization.CandleFinishedEvent += CandleSeriesRealization_CandleFinishedEvent;

                Save();
            }
        }
        private string _candleCreateMethodType;

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

        public string Specification
        {
            get
            {
                if (_lastSpecification != null &&
                    _needToRebuildSpecification == false)
                {
                    return _lastSpecification;
                }

                if(CandleSeriesRealization == null)
                {
                    return null;
                }

                _needToRebuildSpecification = false;

                StringBuilder result = new StringBuilder();

                result.Append(_candleCreateType + "_");
                result.Append(_saveTradesInCandles + "_");

                string series = CandleSeriesRealization.GetType().Name + "_";
                series += CandleSeriesRealization.GetSaveString();

                result.Append(series);

                _lastSpecification = result.ToString().Replace(",",".");

                return _lastSpecification;
            }
        }
        private bool _needToRebuildSpecification;

        private string _lastSpecification;

        public TimeFrame TimeFrame
        {
            get { return _timeFrame; }
            set
            {
                try
                {
                    if (value != _timeFrame ||
                        value == TimeFrame.Sec1)
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

                        if (CandleSeriesRealization != null
                            && CandleSeriesRealization.GetType().Name == "Simple")
                        {
                            Simple simple = CandleSeriesRealization as Simple;
                            simple.TimeFrame = value;
                        }
                        else if (CandleSeriesRealization != null
                            && CandleSeriesRealization.GetType().Name == "HeikenAshi")
                        {
                            HeikenAshi simple = CandleSeriesRealization as HeikenAshi;
                            simple.TimeFrame = value;
                        }
                        else if (CandleSeriesRealization != null
                            && CandleSeriesRealization.GetType().Name == "TimeShiftCandle")
                        {
                            TimeShiftCandle simple = CandleSeriesRealization as TimeShiftCandle;
                            simple.TimeFrame = value;
                        }

                        if (CandleSeriesRealization != null)
                        {
                            for (int i = 0; i < CandleSeriesRealization.Parameters.Count; i++)
                            {
                                ICandleSeriesParameter param = CandleSeriesRealization.Parameters[i];

                                if (param.SysName == "TimeFrame"
                                    && param.Type == CandlesParameterType.StringCollection)
                                {
                                    ((CandlesParameterString)param).ValueString = value.ToString();
                                }
                            }
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

        public TimeSpan TimeFrameTimeSpan
        {
            get { return _timeFrameSpan; }
        }
        private TimeSpan _timeFrameSpan;

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

        public bool MarketDepthBuildMaxSpreadIsOn
        {
            get
            {
                return _marketDepthBuildMaxSpreadIsOn;
            }
            set
            {
                if(value == _marketDepthBuildMaxSpreadIsOn)
                {
                    return;
                }
                _marketDepthBuildMaxSpreadIsOn = value;
                Save();
            }
        }
        private bool _marketDepthBuildMaxSpreadIsOn;

        public decimal MarketDepthBuildMaxSpread
        {
            get
            {
                return _marketDepthBuildMaxSpread;
            }
            set
            {
                if (value == _marketDepthBuildMaxSpread)
                {
                    return;
                }
                _marketDepthBuildMaxSpread = value;
                Save();
            }
        }
        private decimal _marketDepthBuildMaxSpread = 0.2m;

        private void CandleSeriesRealization_CandleFinishedEvent(List<Candle> candles)
        {
            if (CandleFinishedEvent != null)
            {
                CandleFinishedEvent(candles);
            }
        }

        private void CandleSeriesRealization_CandleUpdateEvent(List<Candle> candles)
        {
            if (CandleUpdateEvent != null)
            {
                CandleUpdateEvent(candles);
            }
        }

        public event Action<List<Candle>> CandleUpdateEvent;

        public event Action<List<Candle>> CandleFinishedEvent;

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