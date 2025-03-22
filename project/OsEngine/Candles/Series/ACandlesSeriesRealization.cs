/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Candles.Factory;
using OsEngine.Entity;
using System;
using System.Collections.Generic;


namespace OsEngine.Candles.Series
{
    public abstract class ACandlesSeriesRealization
    {
        #region Constructor and initialization

        public void Init(StartProgram startProgram)
        {
            StartProgram = startProgram;

            OnStateChange(CandleSeriesState.Configure);

            for (int i = 0; i < Parameters.Count; i++)
            {
                LoadParameterValues(Parameters[i]);
            }
        }

        public StartProgram StartProgram;

        public void Delete()
        {
            OnStateChange(CandleSeriesState.Dispose);
            CandlesAll = null;
        }

        public Security Security;

        #endregion

        #region Candles

        public List<Candle> CandlesAll = new List<Candle>();

        public void UpdateChangeCandle()
        {
            if (CandleUpdateEvent != null)
            {
                CandleUpdateEvent(CandlesAll);
            }
        }

        public void UpdateFinishCandle()
        {
            if (CandleFinishedEvent != null)
            {
                CandleFinishedEvent(CandlesAll);
            }
        }

        public event Action<List<Candle>> CandleUpdateEvent;

        public event Action<List<Candle>> CandleFinishedEvent;

        #endregion

        #region Abstract part

        public abstract void OnStateChange(CandleSeriesState state);

        public abstract void UpDateCandle(DateTime time, decimal price,
            decimal volume, bool canPushUp, Side side);

        #endregion

        #region Parameters

        public CandlesParameterDecimal CreateParameterDecimal(string name, string label, decimal value)
        {
            ICandleSeriesParameter newParameter = Parameters.Find(p => p.SysName == name);

            if (newParameter != null)
            {
                return (CandlesParameterDecimal)newParameter;
            }

            newParameter = new CandlesParameterDecimal(name, label, value);
            Parameters.Add(newParameter);
            LoadParameterValues(newParameter);

            return (CandlesParameterDecimal)newParameter;
        }

        public CandlesParameterInt CreateParameterInt(string name, string label, int value)
        {
            ICandleSeriesParameter newParameter = Parameters.Find(p => p.SysName == name);

            if (newParameter != null)
            {
                return (CandlesParameterInt)newParameter;
            }

            newParameter = new CandlesParameterInt(name, label, value);
            Parameters.Add(newParameter);
            LoadParameterValues(newParameter);

            return (CandlesParameterInt)newParameter;
        }

        public CandlesParameterString CreateParameterStringCollection(string name, string label, 
                                                                      string value, List<string> collection)
        {
            ICandleSeriesParameter newParameter = Parameters.Find(p => p.SysName == name);

            if (newParameter != null)
            {
                return (CandlesParameterString)newParameter;
            }

            newParameter = new CandlesParameterString(name, label, value, collection);
            Parameters.Add(newParameter);
            LoadParameterValues(newParameter);

            return (CandlesParameterString)newParameter;
        }

        public CandlesParameterBool CreateParameterBool(string name, string label, bool value)
        {
            ICandleSeriesParameter newParameter = Parameters.Find(p => p.SysName == name);

            if (newParameter != null)
            {
                return (CandlesParameterBool)newParameter;
            }

            newParameter = new CandlesParameterBool(name, label, value);
            Parameters.Add(newParameter);
            LoadParameterValues(newParameter);

            return (CandlesParameterBool)newParameter;
        }

        private ICandleSeriesParameter LoadParameterValues(ICandleSeriesParameter newParameter)
        {
            newParameter.ValueChange += Parameter_ValueChange;

            return newParameter;
        }

        public List<ICandleSeriesParameter> Parameters = new List<ICandleSeriesParameter>();

        public void Parameter_ValueChange()
        {
            if (ParametersChangeByUser != null)
            {
                ParametersChangeByUser();
            }

            OnStateChange(CandleSeriesState.ParametersChange);
        }

        public event Action ParametersChangeByUser;

        public string GetSaveString()
        {
            string result = "";

            for (int i = 0; i < Parameters.Count; i++)
            {
                result += Parameters[i].GetStringToSave();

                if (i + 1 != Parameters.Count)
                {
                    result += "$";
                }
            }

            return result;
        }

        public void SetSaveString(string value)
        {
            if (value == null ||
                value == "")
            {
                return;
            }
            string[] parametersInArray = value.Split('$');

            for (int i = 0; i < parametersInArray.Length; i++)
            {
                string[] curParam = parametersInArray[i].Split('#');

                for (int j = 0; j < Parameters.Count; j++)
                {
                    if (curParam[0] == Parameters[j].SysName)
                    {
                        Parameters[j].LoadParamFromString(curParam[1]);
                        break;
                    }
                }
            }

            if(ParametersChangeByUser != null)
            {
                ParametersChangeByUser();
            }
        }

        #endregion
    }

    public enum CandleSeriesState
    {
        Configure,
        Dispose,
        ParametersChange
    }
}