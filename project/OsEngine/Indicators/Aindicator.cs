/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Entity;
using OsEngine.Attributes;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Threading;

namespace OsEngine.Indicators
{
    public abstract class Aindicator : IIndicator
    {
        #region Mandatory overload members

        public abstract void OnStateChange(IndicatorState state);

        public abstract void OnProcess(List<Candle> source, int index);

        #endregion

        #region Service

        public void Init(string name, StartProgram startProgram)
        {
            Name = name;
            CanDelete = true;

            if (startProgram != StartProgram.IsOsOptimizer)
            {
                Load();
            }
            
            AttributeInitializer attributeInitializer = new(this);
            attributeInitializer.InitAttributes();

            OnStateChange(IndicatorState.Configure);
        }

        public void Clear()
        {
            _myCandles = new List<Candle>();

            if (DataSeries != null)
            {
                for (int i = 0; i < DataSeries.Count; i++)
                {
                    DataSeries[i].Values.Clear();
                }
            }

            if (IncludeIndicators != null)
            {
                for (int i = 0; i < IncludeIndicators.Count; i++)
                {
                    IncludeIndicators[i].Clear();
                }
            }
        }

        public void Delete()
        {
            if (StartProgram != StartProgram.IsOsOptimizer)
            {
                if (File.Exists(@"Engine\" + Name + @"Values.txt"))
                {
                    File.Delete(@"Engine\" + Name + @"Values.txt");
                }

                if (File.Exists(@"Engine\" + Name + @"Parametrs.txt"))
                {
                    File.Delete(@"Engine\" + Name + @"Parametrs.txt");
                }

                if (File.Exists(@"Engine\" + Name + @"Base.txt"))
                {
                    File.Delete(@"Engine\" + Name + @"Base.txt");
                }
            }

            if (IncludeIndicators != null)
            {
                for (int i = 0; i < IncludeIndicators.Count; i++)
                {
                    IncludeIndicators[i].Clear();
                    IncludeIndicators[i].Delete();
                }
                IncludeIndicators.Clear();
                IncludeIndicators = null;
            }

            if (_parameters != null)
            {
                for (int i = 0; i < _parameters.Count; i++)
                {
                    _parameters[i].ValueChange -= Parameter_ValueChange;
                }
                _parameters.Clear();
                _parameters = null;
            }

            if (ParametersDigit != null)
            {
                ParametersDigit.Clear();
                ParametersDigit = null;
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

            _myCandles = null;
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
            AIndicatorUi ui = new AIndicatorUi(this);
            ui.ShowDialog();

            if (ui.IsAccepted)
            {
                Reload();

                Save();
            }
        }

        /// <summary>
        /// Creates a new indicator of the specified type and configures its parameters.<br/>
        /// The method combines creation via a factory, setting parameters and include in built-in.
        /// </summary>
        /// <param name="typeName">Indicator type (e.g. "Sma", "ATR"). Must match the indicator class name.</param>
        /// <param name="name">Indicator name in parameters</param>
        /// <param name="parameters">Array of indicator parameters. The order should match the expected parameters.</param>
        public Aindicator CreateIndicator(string typeName, string name, bool canDelete, params IndicatorParameter[] parameters)
        {
            var indicator = IndicatorsFactory.CreateIndicatorByName(typeName, $"{Name}{typeName}", canDelete);

            var parametersLength = parameters.Length;

            for (int i = 0; i < parametersLength; i++)
            {
                indicator.Parameters[i].Bind(parameters[i]);
            }

            ProcessIndicator(name, indicator);

            return indicator;
        }

        /// <summary>
        /// Creates a new indicator of the specified type and configures its parameters.<br/>
        /// The method combines creation via a factory, setting parameters and include in built-in.<br/><br/>
        /// Can't be deleted from the chart.
        /// </summary>
        /// <param name="typeName">Indicator type (e.g. "Sma", "ATR"). Must match the indicator class name.</param>
        /// <param name="name">Indicator name in parameters</param>
        /// <param name="parameters">Array of indicator parameters. The order should match the expected parameters.</param>
        public Aindicator CreateIndicator(string typeName, string name, params IndicatorParameter[] parameters)
        {
            return CreateIndicator(typeName, name, false, parameters);
        }

        /// <summary>
        /// Creates a new indicator of the specified type and configures its parameters.<br/>
        /// The method combines creation via a factory, setting parameters and include in built-in.<br/><br/>
        /// Can't be deleted from the chart.
        /// </summary>
        /// <param name="typeName">Indicator type (e.g. "Sma", "ATR"). Must match the indicator class name.</param>
        /// <param name="parameters">Array of indicator parameters. The order should match the expected parameters.</param>
        public Aindicator CreateIndicator(string typeName, params IndicatorParameter[] parameters)
        {
            return CreateIndicator(typeName, typeName, false, parameters);
        }

        public StartProgram StartProgram;

        public IndicatorChartPaintType TypeIndicator { get; set; }

        public bool CanDelete { get; set; }

        public string NameSeries { get; set; }

        public string NameArea { get; set; }

        public string Name { get; set; }

        public bool PaintOn { get; set; }

        public bool IsOn { get; set; } = true;

        #endregion

        #region Parameters. Working with strategy parameters

        /// <summary>
        /// create a Decimal type parameter
        /// </summary>
        /// <param name="name">parameter name</param>
        /// <param name="value">default value</param>
        public IndicatorParameterDecimal CreateParameterDecimal(string name, decimal value)
        {
            IndicatorParameter newParameter = _parameters.Find(p => p.Name == name);

            if (newParameter != null)
            {
                return (IndicatorParameterDecimal)newParameter;
            }

            newParameter = new IndicatorParameterDecimal(name, value);

            ParameterDigit param = new ParameterDigit(newParameter);
            ParametersDigit.Add(param);

            return (IndicatorParameterDecimal)LoadParameterValues(newParameter);
        }

        /// <summary>
        /// create int parameter 
        /// </summary>
        /// <param name="name">parameter name</param>
        /// <param name="value">default value</param>
        public IndicatorParameterInt CreateParameterInt(string name, int value)
        {
            IndicatorParameter newParameter = _parameters.Find(p => p.Name == name);

            if (newParameter != null)
            {
                return (IndicatorParameterInt)newParameter;
            }

            newParameter = new IndicatorParameterInt(name, value);

            ParameterDigit param = new ParameterDigit(newParameter);
            ParametersDigit.Add(param);

            return (IndicatorParameterInt)LoadParameterValues(newParameter);
        }

        /// <summary>
        /// create string collection parameter
        /// </summary>
        /// <param name="name">parameter name</param>
        /// <param name="value">default value</param>
        /// <param name="collection">possible enumeration parameters</param>
        public IndicatorParameterString CreateParameterStringCollection(string name, string value, List<string> collection)
        {
            IndicatorParameter newParameter = _parameters.Find(p => p.Name == name);

            if (newParameter != null)
            {
                return (IndicatorParameterString)newParameter;
            }

            newParameter = new IndicatorParameterString(name, value, collection);

            return (IndicatorParameterString)LoadParameterValues(newParameter);
        }

        /// <summary>
        /// create string parameter
        /// </summary>
        /// <param name="name">parameter name</param>
        /// <param name="value">default value</param>
        public IndicatorParameterString CreateParameterString(string name, string value)
        {
            IndicatorParameter newParameter = _parameters.Find(p => p.Name == name);

            if (newParameter != null)
            {
                return (IndicatorParameterString)newParameter;
            }

            newParameter = new IndicatorParameterString(name, value);

            return (IndicatorParameterString)LoadParameterValues(newParameter);
        }

        /// <summary>
        /// create bool type parameter
        /// </summary>
        /// <param name="name">parameter name</param>
        /// <param name="value">default value</param>
        public IndicatorParameterBool CreateParameterBool(string name, bool value)
        {
            IndicatorParameter newParameter = _parameters.Find(p => p.Name == name);

            if (newParameter != null)
            {
                return (IndicatorParameterBool)newParameter;
            }

            newParameter = new IndicatorParameterBool(name, value);
            return (IndicatorParameterBool)LoadParameterValues(newParameter);
        }

        /// <summary>
        /// load parameter settings
        /// </summary>
        private IndicatorParameter LoadParameterValues(IndicatorParameter newParameter)
        {
            GetValueParameterSaveByUser(newParameter);

            newParameter.ValueChange += Parameter_ValueChange;

            _parameters.Add(newParameter);

            return newParameter;
        }

        /// <summary>
        /// load parameter settings from file
        /// </summary>
        private void GetValueParameterSaveByUser(IndicatorParameter parameter)
        {
            if (Name == "")
            {
                return;
            }

            if (!File.Exists(@"Engine\" + Name + @"Parametrs.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + Name + @"Parametrs.txt"))
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
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// the list of options available in the panel
        /// </summary>
        public List<IndicatorParameter> Parameters
        {
            get { return _parameters; }
        }
        private List<IndicatorParameter> _parameters = new List<IndicatorParameter>();

        /// <summary>
        /// digital parameters of the indicator
        /// </summary>
        public List<ParameterDigit> ParametersDigit = new List<ParameterDigit>();

        /// <summary>
        /// parameter has changed settings
        /// </summary>
        private void Parameter_ValueChange()
        {
            if (ParametersChangeByUser != null)
            {
                ParametersChangeByUser();
            }
        }

        /// <summary>
        /// save parameter values
        /// </summary>
        private void SaveParameters()
        {
            if (Name == "")
            {
                return;
            }
            if (_parameters == null ||
                _parameters.Count == 0)
            {
                return;
            }

            if(StartProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @"Parametrs.txt", false)
                    )
                {
                    for (int i = 0; i < _parameters.Count; i++)
                    {
                        writer.WriteLine(_parameters[i].GetStringToSave());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// parameter has changed state
        /// </summary>
        public event Action ParametersChangeByUser;

        #endregion

        #region Built-in indicators

        public List<Aindicator> IncludeIndicators = new List<Aindicator>();

        public List<string> IncludeIndicatorsName = new List<string>();

        public void ProcessIndicator(string indicatorName, Aindicator indicator)
        {
            IncludeIndicators.Add(indicator);
            IncludeIndicatorsName.Add(indicatorName);
        }

        #endregion

        #region Data series

        public IndicatorDataSeries CreateSeries(string name, Color color,
            IndicatorChartPaintType chartPaintType, bool isPaint)
        {
            if (DataSeries.Find(val => val.Name == name) != null)
            {
                return DataSeries.Find(val => val.Name == name);
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

            if (!File.Exists(@"Engine\" + Name + @"Values.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + Name + @"Values.txt"))
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
            catch (Exception)
            {
                // ignore
            }
        }

        public void SaveSeries()
        {
            if (Name == "")
            {
                return;
            }
            if (DataSeries == null ||
                DataSeries.Count == 0)
            {
                return;
            }
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @"Values.txt", false)
                )
                {
                    for (int i = 0; i < DataSeries.Count; i++)
                    {
                        writer.WriteLine(DataSeries[i].GetSaveStr());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        List<List<decimal>> IIndicator.ValuesToChart
        {
            get { return null; }
        }

        List<Color> IIndicator.Colors
        {
            get { return null; }
        }

        #endregion

        #region Candles loading

        private List<Candle> _myCandles = new List<Candle>();

        private Candle _lastFirstCandle = null;

        public int UpdateIntervalInSeconds = 0;

        private DateTime _nextUpdateIndicatorsTime;

        private DateTime _lastUpdateCandleTime;

        public void Process(List<Candle> candles)
        {
            ProcessLoop(candles, 1);
        }

        private void ProcessLoop(List<Candle> candles, int attemptNumber)
        {
            try
            {
                if (StartProgram == StartProgram.IsOsTrader
                  && UpdateIntervalInSeconds != 0)
                {
                    if (_nextUpdateIndicatorsTime > DateTime.Now
                        && _lastUpdateCandleTime == candles[^1].TimeStart)
                    {
                        return;
                    }
                    _nextUpdateIndicatorsTime = DateTime.Now.AddSeconds(UpdateIntervalInSeconds);
                    _lastUpdateCandleTime = candles[^1].TimeStart;
                }

                if (candles.Count == 0)
                {
                    return;
                }

                if (DataSeries == null || DataSeries.Count == 0)
                {
                    return;
                }

                if (_myCandles == null ||
                candles.Count < _myCandles.Count ||
                candles.Count > _myCandles.Count + 1 ||
                (_lastFirstCandle != null && _lastFirstCandle.TimeStart != candles[0].TimeStart))
                {
                    ProcessAll(candles);
                }
                else if (candles.Count < DataSeries[0].Values.Count)
                {
                    foreach (var ds in DataSeries)
                    {
                        ds.Values.Clear();
                    }
                    ProcessAll(candles);
                }
                else if (_myCandles.Count == candles.Count)
                {
                    ProcessLast(candles);
                }
                else if (_myCandles.Count + 1 == candles.Count)
                {
                    ProcessNew(candles, candles.Count - 1);
                }

                _myCandles = candles;
                _lastFirstCandle = candles[0];
            }
            catch
            {
                if(StartProgram == StartProgram.IsOsTrader
                    && attemptNumber < 3)
                {
                    Thread.Sleep(10);
                    ProcessLoop(candles, attemptNumber + 1);
                }
                else
                {
                    throw;
                }
            }
        }

        private void ProcessAll(List<Candle> candles)
        {
            for (int i = 0; i < IncludeIndicators.Count; i++)
            {
                IncludeIndicators[i].Clear();
                IncludeIndicators[i].Process(candles);
            }

            for (int i = 0; i < DataSeries.Count; i++)
            {
                DataSeries[i].Values.Clear();
            }

            for (int i = 0; i < candles.Count; i++)
            {
                ProcessNew(candles, i);
            }
        }

        private void ProcessLast(List<Candle> candles)
        {
            for (int i = 0; i < DataSeries.Count; i++)
            {
                while (DataSeries[i].Values.Count < candles.Count)
                {
                    if (DataSeries[i].Values.Count == 0)
                    {
                        DataSeries[i].Values.Add(0);
                    }
                    else
                    {
                        DataSeries[i].Values.Add(0);
                        // DataSeries[i].Values.Add(DataSeries[i].Values[DataSeries[i].Values.Count-1]);
                    }
                }
            }

            for (int i = 0; i < IncludeIndicators.Count; i++)
            {
                if (IncludeIndicators[i].IsOn == true &&
                    IsOn == false)
                {
                    IncludeIndicators[i].IsOn = false;
                }
                if (IncludeIndicators[i].IsOn == false &&
                    IsOn == true)
                {
                    IncludeIndicators[i].IsOn = true;
                }
            }

            if (candles.Count <= 0)
            {
                return;
            }
            for (int i = 0; i < IncludeIndicators.Count; i++)
            {
                IncludeIndicators[i].Process(candles);
            }

            if (IsOn == false)
            {
                return;
            }

            OnProcess(candles, candles.Count - 1);
        }

        private void ProcessNew(List<Candle> candles, int index)
        {
            if (candles.Count <= 0 ||
                index < 0)
            {
                return;
            }

            for (int i = 0; i < DataSeries.Count; i++)
            {
                while (DataSeries[i].Values.Count < candles.Count)
                {
                    if (DataSeries[i].Values.Count == 0)
                    {
                        DataSeries[i].Values.Add(0);
                    }
                    else
                    {
                        DataSeries[i].Values.Add(DataSeries[i].Values[DataSeries[i].Values.Count - 1]);
                    }
                }
            }

            for (int i = 0; i < IncludeIndicators.Count; i++)
            {
                if (IncludeIndicators[i].IsOn == true &&
                    IsOn == false)
                {
                    IncludeIndicators[i].IsOn = false;
                }
                if (IncludeIndicators[i].IsOn == false &&
                    IsOn == true)
                {
                    IncludeIndicators[i].IsOn = true;
                }
            }

            for (int i = 0; i < IncludeIndicators.Count; i++)
            {
                IncludeIndicators[i].ProcessNew(candles,index);
            }

            if (IsOn == false)
            {
                return;
            }

            OnProcess(candles, index);
        }

        public void Reload()
        {
            if (_myCandles == null)
            {
                return;
            }

            //lock(_indicatorUpdateLocker)
            //{
            ProcessAll(_myCandles);
            //}

            if (NeedToReloadEvent != null)
            {
                NeedToReloadEvent(this);
            }
        }

        public void RePaint()
        {
            if(NeedToReloadEvent != null)
            {
                NeedToReloadEvent(this);
            }
        }

        public event Action<IIndicator> NeedToReloadEvent;

        #endregion

        #region  Loading of data arrays into the indicator

        public void Process(List<decimal> values)
        {
            //lock(_indicatorUpdateLocker)
            //{
                if (values.Count == 0)
                {
                    return;
                }
                if (_myCandles == null ||
                    values.Count < _myCandles.Count ||
                    values.Count > _myCandles.Count + 1)
                {
                    ProcessAll(values);
                }
                else if (_myCandles.Count == values.Count)
                {
                    ProcessLast(values);
                }
                else if (_myCandles.Count + 1 == values.Count)
                {
                    ProcessNew(values, values.Count);
                }
           // }
        }

        private void ProcessAll(List<decimal> values)
        {
            for (int i = 0; i < IncludeIndicators.Count; i++)
            {
                IncludeIndicators[i].Clear();
                IncludeIndicators[i].Process(values);
            }

            for (int i = 0; i < DataSeries.Count; i++)
            {
                DataSeries[i].Values.Clear();
            }

            for (int i = 0; i < values.Count; i++)
            {
                ProcessNew(values, i);
            }
        }

        private void ProcessLast(List<decimal> values)
        {
            if (values.Count <= 0)
            {
                return;
            }

            for (int i = 0; i < DataSeries.Count; i++)
            {
                while (DataSeries[i].Values.Count < values.Count)
                {
                    DataSeries[i].Values.Add(0);
                }
            }

            for (int i = 0; i < IncludeIndicators.Count; i++)
            {
                if (IncludeIndicators[i].IsOn == true &&
                    IsOn == false)
                {
                    IncludeIndicators[i].IsOn = false;
                }
                if (IncludeIndicators[i].IsOn == false &&
                    IsOn == true)
                {
                    IncludeIndicators[i].IsOn = true;
                }
            }

            for (int i = 0; i < IncludeIndicators.Count; i++)
            {
                IncludeIndicators[i].Process(values);
            }

            if (IsOn == false)
            {
                return;
            }

            while (_myCandles.Count < values.Count)
            {
                _myCandles.Add(new Candle());
            }

            _myCandles[values.Count - 1].Open = values[values.Count - 1];
            _myCandles[values.Count - 1].High = values[values.Count - 1];
            _myCandles[values.Count - 1].Low = values[values.Count - 1];
            _myCandles[values.Count - 1].Close = values[values.Count - 1];

            OnProcess(_myCandles, values.Count - 1);
        }

        private void ProcessNew(List<decimal> values, int index)
        {
            if (values.Count <= 0 ||
                index <= 0)
            {
                return;
            }

            for (int i = 0; i < DataSeries.Count; i++)
            {
                while (DataSeries[i].Values.Count < values.Count)
                {
                    DataSeries[i].Values.Add(0);
                }
            }

            for (int i = 0; i < IncludeIndicators.Count; i++)
            {
                if(IncludeIndicators[i].IsOn == true &&
                    IsOn == false)
                {
                    IncludeIndicators[i].IsOn = false;
                }
                if (IncludeIndicators[i].IsOn == false &&
                    IsOn == true)
                {
                    IncludeIndicators[i].IsOn = true;
                }
            }

            for (int i = 0; i < IncludeIndicators.Count; i++)
            {
                IncludeIndicators[i].Process(values);
            }

            if (IsOn == false)
            {
                return;
            }

            while (_myCandles.Count < index)
            {
                _myCandles.Add(new Candle());
            }

            _myCandles[index].Open = values[_myCandles.Count];
            _myCandles[index].High = values[_myCandles.Count];
            _myCandles[index].Low = values[_myCandles.Count];
            _myCandles[index].Close = values[_myCandles.Count];

            OnProcess(_myCandles, index);
        }

        #endregion
    }

    public enum IndicatorState
    {
        Configure,
        Dispose,
    }

    public class ParameterDigit
    {
        public ParameterDigit(IndicatorParameter parameter)
        {
            _parameter = parameter;
        }

        private IndicatorParameter _parameter;

        public string Name
        {
            get { return _parameter.Name; }
        }

        public decimal Value
        {
            get
            {
                if (_parameter.Type == IndicatorParameterType.Decimal)
                {
                    return ((IndicatorParameterDecimal)_parameter).ValueDecimal;
                }
                else //if (_parameter.Type == IndicatorParameterType.Int)
                {
                    return ((IndicatorParameterInt)_parameter).ValueInt;
                }
            }
            set
            {
                if (_parameter.Type == IndicatorParameterType.Decimal)
                {
                    ((IndicatorParameterDecimal)_parameter).ValueDecimal = value;
                }
                else //if (_parameter.Type == IndicatorParameterType.Int)
                {
                    ((IndicatorParameterInt)_parameter).ValueInt = (int)value;
                }
            }
        }
    }
}
