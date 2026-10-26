/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using System;
using System.Collections.Generic;

namespace OsEngine.IndicatorMarketDepth
{
    /// <summary>
    /// Тип параметра индикатора по стакану
    /// </summary>
    public enum MarketDepthParameterType
    {
        Int,
        Decimal,
        String,
        Bool
    }

    /// <summary>
    /// Интерфейс параметра индикатора по стакану
    /// </summary>
    public interface IParameterMarketDepth
    {
        string Name { get; }

        MarketDepthParameterType Type { get; }

        event Action ValueChange;

        string GetStringToSave();

        void LoadParamFromString(string[] save);

        void DoDefault();
    }

    /// <summary>
    /// Параметр типа Int
    /// </summary>
    public class ParameterMarketDepthInt : IParameterMarketDepth
    {
        public ParameterMarketDepthInt(string name, int value)
        {
            _name = name;
            _valueInt = value;
            _valueIntDefault = value;
        }

        private ParameterMarketDepthInt()
        {
        }

        public string Name
        {
            get { return _name; }
        }
        private string _name;

        public MarketDepthParameterType Type
        {
            get { return MarketDepthParameterType.Int; }
        }

        public string GetStringToSave()
        {
            return _name + "#" + ValueInt + "#";
        }

        public void LoadParamFromString(string[] save)
        {
            _valueInt = Convert.ToInt32(save[1]);
        }

        public void DoDefault()
        {
            _valueInt = _valueIntDefault;
        }

        public int ValueInt
        {
            get { return _valueInt; }
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
        private int _valueInt;

        public int ValueIntDefault
        {
            get { return _valueIntDefault; }
        }
        private int _valueIntDefault;

        public event Action ValueChange;
    }

    /// <summary>
    /// Параметр типа Decimal
    /// </summary>
    public class ParameterMarketDepthDecimal : IParameterMarketDepth
    {
        public ParameterMarketDepthDecimal(string name, decimal value)
        {
            _name = name;
            _valueDecimal = value;
            _valueDecimalDefault = value;
        }

        private ParameterMarketDepthDecimal()
        {
        }

        public string Name
        {
            get { return _name; }
        }
        private string _name;

        public MarketDepthParameterType Type
        {
            get { return MarketDepthParameterType.Decimal; }
        }

        public string GetStringToSave()
        {
            return _name + "#" + ValueDecimal + "#";
        }

        public void LoadParamFromString(string[] save)
        {
            _valueDecimal = save[1].ToDecimal();
        }

        public void DoDefault()
        {
            _valueDecimal = _valueDecimalDefault;
        }

        public decimal ValueDecimal
        {
            get { return _valueDecimal; }
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
        private decimal _valueDecimal;

        public decimal ValueDecimalDefault
        {
            get { return _valueDecimalDefault; }
        }
        private decimal _valueDecimalDefault;

        public event Action ValueChange;
    }

    /// <summary>
    /// Параметр типа String (может хранить коллекцию возможных значений)
    /// </summary>
    public class ParameterMarketDepthString : IParameterMarketDepth
    {
        public ParameterMarketDepthString(string name, string value, List<string> collection)
        {
            _name = name;
            _valueString = value;
            _valueStringDefault = value;
            _setStringValues = collection;
        }

        public ParameterMarketDepthString(string name, string value)
        {
            if (value == null)
            {
                value = "";
            }

            _name = name;
            _valueString = value;
            _valueStringDefault = value;
            _setStringValues = new List<string> { value };
        }

        private ParameterMarketDepthString()
        {
        }

        public string Name
        {
            get { return _name; }
        }
        private string _name;

        public MarketDepthParameterType Type
        {
            get { return MarketDepthParameterType.String; }
        }

        public string GetStringToSave()
        {
            return _name + "#" + ValueString + "#";
        }

        public void LoadParamFromString(string[] save)
        {
            _valueString = save[1];
        }

        public void DoDefault()
        {
            _valueString = _valueStringDefault;
        }

        public string ValueString
        {
            get { return _valueString; }
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
        private string _valueString;

        public string ValueStringDefault
        {
            get { return _valueStringDefault; }
        }
        private string _valueStringDefault;

        public List<string> ValuesString
        {
            get { return _setStringValues; }
        }
        private List<string> _setStringValues;

        public event Action ValueChange;
    }

    /// <summary>
    /// Параметр типа Bool
    /// </summary>
    public class ParameterMarketDepthBool : IParameterMarketDepth
    {
        public ParameterMarketDepthBool(string name, bool value)
        {
            _name = name;
            _valueBool = value;
            _valueBoolDefault = value;
        }

        private ParameterMarketDepthBool()
        {
        }

        public string Name
        {
            get { return _name; }
        }
        private string _name;

        public MarketDepthParameterType Type
        {
            get { return MarketDepthParameterType.Bool; }
        }

        public string GetStringToSave()
        {
            return _name + "#" + ValueBool + "#";
        }

        public void LoadParamFromString(string[] save)
        {
            _valueBool = Convert.ToBoolean(save[1]);
        }

        public void DoDefault()
        {
            _valueBool = _valueBoolDefault;
        }

        public bool ValueBool
        {
            get { return _valueBool; }
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
        private bool _valueBool;

        public bool ValueBoolDefault
        {
            get { return _valueBoolDefault; }
        }
        private bool _valueBoolDefault;

        public event Action ValueChange;
    }
}
