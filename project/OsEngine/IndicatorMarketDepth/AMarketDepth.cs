/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Logging;
using OsEngine.Market;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace OsEngine.IndicatorMarketDepth
{
    /// <summary>
    /// Базовый класс стаканного индикатора. Аналог Aindicator, но обрабатывает MarketDepth.
    /// </summary>
    public abstract class AMarketDepth
    {
        #region Abstract methods

        public abstract void OnStateChange(MarketDepthIndicatorState state);

        public abstract void OnProcess(MarketDepth depth);

        #endregion

        #region Service

        public void Init(string name, StartProgram startProgram)
        {
            Name = name;
            StartProgram = startProgram;
            CanDelete = true;

            if (startProgram != StartProgram.IsOsOptimizer)
            {
                Load();
            }

            _historyDepth = CreateParameterInt("HistoryDepth", 60);

            try
            {
                OnStateChange(MarketDepthIndicatorState.Configure);
            }
            catch (Exception error)
            {
                RegisterError(error);
            }
        }

        public void Clear()
        {
            _lastStoreTime = DateTime.MinValue;
            _lastTradeSecond = DateTime.MinValue;

            if (DataSeries != null)
            {
                for (int i = 0; i < DataSeries.Count; i++)
                {
                    DataSeries[i].Clear();
                }
            }
        }

        public void Delete()
        {
            if (StartProgram != StartProgram.IsOsOptimizer)
            {
                DeleteFile(@"Engine\" + Name + @"MarketDepthParametrs.txt");
                DeleteFile(@"Engine\" + Name + @"MarketDepthValues.txt");
            }

            if (Parameters != null)
            {
                for (int i = 0; i < Parameters.Count; i++)
                {
                    Parameters[i].ValueChange -= Parameter_ValueChange;
                }
                Parameters.Clear();
                Parameters = null;
            }

            if (DataSeries != null)
            {
                for (int i = 0; i < DataSeries.Count; i++)
                {
                    DataSeries[i].Clear();
                    DataSeries[i].Delete();
                }
                DataSeries.Clear();
                DataSeries = null;
            }
        }

        public void Load()
        {
            if (Name == "")
            {
                return;
            }
        }

        public void Save()
        {
            if (Name == "")
            {
                return;
            }

            if (StartProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            SaveParameters();
            SaveSeries();
        }

        public void ShowDialog()
        {
            MarketDepthIndicatorUi ui = new MarketDepthIndicatorUi(this);
            ui.ShowDialog();
        }

        private void DeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Properties

        public StartProgram StartProgram;

        public string Name;

        public virtual MarketDepthIndicatorDataSource DataSource
        {
            get { return MarketDepthIndicatorDataSource.MarketDepth; }
        }

        public bool CanDelete;

        public bool IsOn = true;

        public bool PaintOn = true;

        public virtual bool RecalculateByLastDepth
        {
            get { return true; }
        }

        public virtual string Description
        {
            get { return string.Empty; }
        }

        #endregion

        #region Parameters

        public ParameterMarketDepthInt CreateParameterInt(string name, int value)
        {
            IParameterMarketDepth oldParameter = Parameters.Find(p => p.Name == name);

            if (oldParameter != null)
            {
                return (ParameterMarketDepthInt)oldParameter;
            }

            ParameterMarketDepthInt newParameter = new ParameterMarketDepthInt(name, value);
            return (ParameterMarketDepthInt)LoadParameterValues(newParameter);
        }

        public ParameterMarketDepthDecimal CreateParameterDecimal(string name, decimal value)
        {
            IParameterMarketDepth oldParameter = Parameters.Find(p => p.Name == name);

            if (oldParameter != null)
            {
                return (ParameterMarketDepthDecimal)oldParameter;
            }

            ParameterMarketDepthDecimal newParameter = new ParameterMarketDepthDecimal(name, value);
            return (ParameterMarketDepthDecimal)LoadParameterValues(newParameter);
        }

        public ParameterMarketDepthString CreateParameterString(string name, string value)
        {
            IParameterMarketDepth oldParameter = Parameters.Find(p => p.Name == name);

            if (oldParameter != null)
            {
                return (ParameterMarketDepthString)oldParameter;
            }

            ParameterMarketDepthString newParameter = new ParameterMarketDepthString(name, value);
            return (ParameterMarketDepthString)LoadParameterValues(newParameter);
        }

        public ParameterMarketDepthString CreateParameterStringCollection(string name, string value, List<string> collection)
        {
            IParameterMarketDepth oldParameter = Parameters.Find(p => p.Name == name);

            if (oldParameter != null)
            {
                return (ParameterMarketDepthString)oldParameter;
            }

            ParameterMarketDepthString newParameter = new ParameterMarketDepthString(name, value, collection);
            return (ParameterMarketDepthString)LoadParameterValues(newParameter);
        }

        public ParameterMarketDepthBool CreateParameterBool(string name, bool value)
        {
            IParameterMarketDepth oldParameter = Parameters.Find(p => p.Name == name);

            if (oldParameter != null)
            {
                return (ParameterMarketDepthBool)oldParameter;
            }

            ParameterMarketDepthBool newParameter = new ParameterMarketDepthBool(name, value);
            return (ParameterMarketDepthBool)LoadParameterValues(newParameter);
        }

        private IParameterMarketDepth LoadParameterValues(IParameterMarketDepth newParameter)
        {
            GetValueParameterSaveByUser(newParameter);

            newParameter.ValueChange += Parameter_ValueChange;

            Parameters.Add(newParameter);

            return newParameter;
        }

        private void GetValueParameterSaveByUser(IParameterMarketDepth parameter)
        {
            if (Name == "")
            {
                return;
            }

            string path = @"Engine\" + Name + @"MarketDepthParametrs.txt";

            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    while (!reader.EndOfStream)
                    {
                        string[] save = reader.ReadLine().Split('#');

                        if (save[0] == parameter.Name)
                        {
                            parameter.LoadParamFromString(save);
                        }
                    }
                    reader.Close();
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public List<IParameterMarketDepth> Parameters = new List<IParameterMarketDepth>();

        private void Parameter_ValueChange()
        {
            if (ParametersChangeByUser != null)
            {
                ParametersChangeByUser();
            }
        }

        public event Action ParametersChangeByUser;

        private void SaveParameters()
        {
            if (Name == "")
            {
                return;
            }

            if (Parameters == null || Parameters.Count == 0)
            {
                return;
            }

            if (StartProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @"MarketDepthParametrs.txt", false))
                {
                    for (int i = 0; i < Parameters.Count; i++)
                    {
                        writer.WriteLine(Parameters[i].GetStringToSave());
                    }

                    writer.Close();
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Data series

        public IndicatorDataSeries CreateSeries(string name, Color color,
            IndicatorChartPaintType chartPaintType, bool isPaint)
        {
            IndicatorDataSeries oldSeries = DataSeries.Find(val => val.Name == name);

            if (oldSeries != null)
            {
                return oldSeries;
            }

            IndicatorDataSeries newSeries = new IndicatorDataSeries(color, name, chartPaintType, isPaint);
            DataSeries.Add(newSeries);
            CheckSeriesParametersInSaveData(newSeries);

            return newSeries;
        }

        public List<IndicatorDataSeries> DataSeries = new List<IndicatorDataSeries>();

        private void CheckSeriesParametersInSaveData(IndicatorDataSeries series)
        {
            if (Name == "")
            {
                return;
            }

            string path = @"Engine\" + Name + @"MarketDepthValues.txt";

            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    while (!reader.EndOfStream)
                    {
                        string[] save = reader.ReadLine().Split('&');

                        if (save[0] == series.Name)
                        {
                            series.LoadFromStr(save);
                        }
                    }
                    reader.Close();
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void SaveSeries()
        {
            if (Name == "")
            {
                return;
            }

            if (DataSeries == null || DataSeries.Count == 0)
            {
                return;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @"MarketDepthValues.txt", false))
                {
                    for (int i = 0; i < DataSeries.Count; i++)
                    {
                        writer.WriteLine(DataSeries[i].GetSaveStr());
                    }

                    writer.Close();
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Записать значение в последний слот серии (после того как базовый класс добавил слот).
        /// </summary>
        protected void SetValue(IndicatorDataSeries series, decimal value)
        {
            if (series == null || series.Values.Count == 0)
            {
                return;
            }

            series.Values[series.Values.Count - 1] = value;
        }

        #endregion

        #region Processing

        private ParameterMarketDepthInt _historyDepth;

        /// <summary>
        /// Глубина хранения значений (в секундах). По умолчанию 60.
        /// </summary>
        public int HistoryDepth
        {
            get { return _historyDepth != null ? _historyDepth.ValueInt : 60; }
            set { if (_historyDepth != null) { _historyDepth.ValueInt = value; } }
        }

        private DateTime _lastStoreTime = DateTime.MinValue;

        private DateTime _lastTradeSecond = DateTime.MinValue;

        private MarketDepth _lastDepth;

        /// <summary>
        /// Вход стакана. Фиксирует значение не чаще одного раза в секунду,
        /// хранит кольцевой буфер глубиной HistoryDepth.
        /// </summary>
        public void Process(MarketDepth depth)
        {
            if (depth == null)
            {
                return;
            }

            if (_lastStoreTime != DateTime.MinValue
                && depth.Time < _lastStoreTime.AddSeconds(1))
            {
                return;
            }

            _lastStoreTime = depth.Time;
            _lastDepth = depth.GetCopy();

            int depthLimit = _historyDepth.ValueInt;

            if (depthLimit <= 0)
            {
                depthLimit = 60;
            }

            for (int i = 0; i < DataSeries.Count; i++)
            {
                IndicatorDataSeries series = DataSeries[i];

                if (series.Values.Count == 0)
                {
                    series.Values.Add(0);
                }
                else
                {
                    series.Values.Add(series.Values[series.Values.Count - 1]);
                }

                series.Times.Add(depth.Time);

                while (series.Values.Count > depthLimit)
                {
                    series.Values.RemoveAt(0);
                }

                while (series.Times.Count > depthLimit)
                {
                    series.Times.RemoveAt(0);
                }
            }

            try
            {
                OnProcess(depth);
            }
            catch (Exception error)
            {
                RegisterError(error);
            }
        }

        public void ProcessTrade(Trade trade)
        {
            if (trade == null)
            {
                return;
            }

            if (_lastTradeSecond == DateTime.MinValue
                || trade.Time >= _lastTradeSecond.AddSeconds(1))
            {
                _lastTradeSecond = trade.Time;

                int depthLimit = _historyDepth.ValueInt;

                if (depthLimit <= 0)
                {
                    depthLimit = 60;
                }

                for (int i = 0; i < DataSeries.Count; i++)
                {
                    IndicatorDataSeries series = DataSeries[i];

                    if (series.Values.Count == 0)
                    {
                        series.Values.Add(0);
                    }
                    else
                    {
                        series.Values.Add(series.Values[series.Values.Count - 1]);
                    }

                    series.Times.Add(trade.Time);

                    while (series.Values.Count > depthLimit)
                    {
                        series.Values.RemoveAt(0);
                    }

                    while (series.Times.Count > depthLimit)
                    {
                        series.Times.RemoveAt(0);
                    }
                }
            }

            try
            {
                OnProcessTrade(trade);
            }
            catch (Exception error)
            {
                RegisterError(error);
            }
        }

        public virtual void OnProcessTrade(Trade trade)
        {
        }

        public void Recalculate()
        {
            int depthLimit = _historyDepth.ValueInt;

            if (depthLimit <= 0)
            {
                depthLimit = 60;
            }

            for (int i = 0; i < DataSeries.Count; i++)
            {
                while (DataSeries[i].Values.Count > depthLimit)
                {
                    DataSeries[i].Values.RemoveAt(0);
                }

                while (DataSeries[i].Times.Count > depthLimit)
                {
                    DataSeries[i].Times.RemoveAt(0);
                }
            }

            if (_lastDepth != null && RecalculateByLastDepth)
            {
                try
                {
                    OnProcess(_lastDepth);
                }
                catch (Exception error)
                {
                    RegisterError(error);
                }
            }

            RePaint();
        }

        public event Action NeedToReloadEvent;

        public void RePaint()
        {
            if (NeedToReloadEvent != null)
            {
                NeedToReloadEvent();
            }
        }

        #endregion

        #region Error handling

        public string LastError = string.Empty;

        public int ErrorCount;

        private void RegisterError(Exception error)
        {
            if (ErrorCount == 0)
            {
                LastError = error.ToString();
                ServerMaster.SendNewLogMessage("Indicator '" + Name + "' failed: " + error, LogMessageType.Error);
            }

            ErrorCount++;
        }

        #endregion
    }

    public enum MarketDepthIndicatorState
    {
        Configure,
        Dispose
    }

    public enum MarketDepthIndicatorDataSource
    {
        MarketDepth,
        Trades,
        Both
    }
}
