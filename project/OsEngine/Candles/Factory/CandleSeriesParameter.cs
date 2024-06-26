/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using System;
using System.Collections.Generic;

namespace OsEngine.Candles.Factory
{
    public abstract class ICandleSeriesParameter
    {
        public void DoDefault()
        {
            if (this.Type == CandlesParameterType.Bool)
            {
                ((CandlesParameterBool)this).ValueBool = ((CandlesParameterBool)this).ValueBoolDefault;
            }
            if (this.Type == CandlesParameterType.Decimal)
            {
                ((CandlesParameterDecimal)this).ValueDecimal = ((CandlesParameterDecimal)this).ValueDecimalDefault;
            }
            if (this.Type == CandlesParameterType.Int)
            {
                ((CandlesParameterInt)this).ValueInt = ((CandlesParameterInt)this).ValueIntDefault;
            }
            if (this.Type == CandlesParameterType.StringCollection)
            {
                ((CandlesParameterString)this).ValueString = ((CandlesParameterString)this).ValueStringDefault;
            }
        }

        public abstract string SysName { get; }

        public abstract string Label { get; }

        public abstract CandlesParameterType Type { get; }

        public abstract string GetStringToSave();

        public abstract void LoadParamFromString(string saveStr);

        public abstract event Action ValueChange;

    }

    public class CandlesParameterInt : ICandleSeriesParameter
    {
        public CandlesParameterInt(string sysName, string label, int value)
        {
            _name = sysName;
            _valueInt = value;
            _valueIntDefault = value;
            _label = label;
        }

        private CandlesParameterInt()
        {

        }

        public override string SysName
        {
            get { return _name; }
        }
        private string _name;

        public override string Label
        {
            get { return _label; }
        }
        private string _label;

        public override CandlesParameterType Type
        {
            get { return CandlesParameterType.Int; }
        }

        public int ValueInt
        {
            get
            {
                return _valueInt;
            }
            set
            {
                if (_valueInt == value)
                {
                    return;
                }
                _valueInt = value;
                if (ValueChange != null)
                {
                    ValueChange();
                }
            }
        }
        internal int _valueInt;

        public int ValueIntDefault
        {
            get
            {
                return _valueIntDefault;
            }
        }
        private int _valueIntDefault;

        public override string GetStringToSave()
        {
            string save = _name + "#";
            save += ValueInt;

            return save;
        }

        public override void LoadParamFromString(string save)
        {
            _valueInt = Convert.ToInt32(save);
        }

        public override event Action ValueChange;
    }

    public class CandlesParameterDecimal : ICandleSeriesParameter
    {
        public CandlesParameterDecimal(string sysName, string label, decimal value)
        {
            _name = sysName;
            _valueDecimal = value;
            _valueDecimalDefault = value;
            _type = CandlesParameterType.Decimal;
            _label = label;
        }

        private CandlesParameterDecimal()
        {

        }

        public override string SysName
        {
            get { return _name; }
        }
        private string _name;

        public override string Label
        {
            get { return _label; }
        }
        private string _label;

        public override CandlesParameterType Type
        {
            get { return _type; }
        }
        private CandlesParameterType _type;

        public decimal ValueDecimal
        {
            get
            {
                return _valueDecimal;
            }
            set
            {
                if (_valueDecimal == value)
                {
                    return;
                }
                _valueDecimal = value;
                if (ValueChange != null)
                {
                    ValueChange();
                }
            }
        }
        internal decimal _valueDecimal;

        public decimal ValueDecimalDefault
        {
            get
            {
                return _valueDecimalDefault;
            }
        }
        private decimal _valueDecimalDefault;

        public override string GetStringToSave()
        {
            string save = _name + "#";
            save += ValueDecimal;
            return save;
        }

        public override void LoadParamFromString(string save)
        {
            _valueDecimal = save.ToDecimal();
        }

        public override event Action ValueChange;
    }

    public class CandlesParameterBool : ICandleSeriesParameter
    {
        public CandlesParameterBool(string sysName, string label, bool value)
        {
            _name = sysName;
            _valueBoolDefault = value;
            _valueBool = value;
            _type = CandlesParameterType.Bool;
            _label = label;
        }

        private CandlesParameterBool()
        {

        }

        public override string SysName
        {
            get { return _name; }
        }
        private string _name;

        public override string Label
        {
            get { return _label; }
        }
        private string _label;

        public override CandlesParameterType Type
        {
            get { return _type; }
        }
        private CandlesParameterType _type;

        public bool ValueBool
        {
            get
            {
                return _valueBool;
            }
            set
            {
                if (_valueBool == value)
                {
                    return;
                }
                _valueBool = value;
                if (ValueChange != null)
                {
                    ValueChange();
                }
            }
        }
        internal bool _valueBool;

        public bool ValueBoolDefault
        {
            get
            {
                return _valueBoolDefault;
            }
        }
        private bool _valueBoolDefault;

        public override string GetStringToSave()
        {
            string save = _name + "#";
            save += _valueBool;
            return save;
        }

        public override void LoadParamFromString(string save)
        {
            _valueBool = Convert.ToBoolean(save);
        }

        public override event Action ValueChange;
    }

    public class CandlesParameterString : ICandleSeriesParameter
    {

        public CandlesParameterString(string sysName, string label, string value, List<string> collection)
        {
            _name = sysName;
            _valueString = value;
            _valueStringDefault = value;
            _setStringValues = collection;
            _type = CandlesParameterType.StringCollection;
            _label = label;
        }

        private CandlesParameterString()
        {

        }

        public override string SysName
        {
            get { return _name; }
        }
        private string _name;

        public override string Label
        {
            get { return _label; }
        }
        private string _label;

        public override CandlesParameterType Type
        {
            get { return _type; }
        }
        private CandlesParameterType _type;

        public string ValueString
        {
            get
            {
                return _valueString;
            }
            set
            {
                if (_valueString == value)
                {
                    return;
                }
                _valueString = value;
                if (ValueChange != null)
                {
                    ValueChange();
                }
            }
        }
        internal string _valueString;

        public string ValueStringDefault
        {
            get { return _valueStringDefault; }
        }
        private string _valueStringDefault;

        public List<string> ValuesString
        {
            get
            {
                if (_type != CandlesParameterType.StringCollection)
                {
                    throw new Exception("Попытка запросить у параметра с типом String, поле " + _type);
                }
                return _setStringValues;
            }
        }
        private List<string> _setStringValues;

        public override string GetStringToSave()
        {
            string save = _name + "#";
            save += _valueString;
            return save;
        }

        public override void LoadParamFromString(string save)
        {
            _valueString = save;
        }

        public override event Action ValueChange;
    }

    /// <summary>
    /// Candle parameter type
    /// </summary>
    public enum CandlesParameterType
    {
        Int,

        Decimal,

        StringCollection,

        Bool
    }
}