/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using System;
using System.Collections.Generic;

namespace OsEngine.Indicators
{
    /// <summary>
    /// Indicator parameter interface
    /// </summary>
    public abstract class IndicatorParameter
    {
        /// <summary>
        /// set the default value of the parameter
        /// </summary>
        public void DoDefault()
        {
            if (this.Type == IndicatorParameterType.Bool)
            {
                ((IndicatorParameterBool)this).ValueBool = ((IndicatorParameterBool)this).ValueBoolDefault;
            }
            if (this.Type == IndicatorParameterType.Decimal)
            {
                ((IndicatorParameterDecimal)this).ValueDecimal = ((IndicatorParameterDecimal)this).ValueDecimalDefault;
            }
            if (this.Type == IndicatorParameterType.Int)
            {
                ((IndicatorParameterInt)this).ValueInt = ((IndicatorParameterInt)this).ValueIntDefault;
            }
            if (this.Type == IndicatorParameterType.String)
            {
                ((IndicatorParameterString)this).ValueString = ((IndicatorParameterString)this).ValueStringDefault;
            }
        }

        /// <summary>
        /// link a parameter value to another
        /// </summary>
        public void Bind(IndicatorParameter parameter)
        {
            if (parameter.Type != Type)
            {
                throw new Exception("Can`t bind parameter with not equals types");
            }

            if (parameter.Type == IndicatorParameterType.Bool)
            {
                ((IndicatorParameterBool)this).ValueBool = ((IndicatorParameterBool)parameter).ValueBool;
            }
            if (parameter.Type == IndicatorParameterType.Decimal)
            {
                ((IndicatorParameterDecimal)this).ValueDecimal = ((IndicatorParameterDecimal)parameter).ValueDecimal;
            }
            if (parameter.Type == IndicatorParameterType.Int)
            {
                ((IndicatorParameterInt)this).ValueInt = ((IndicatorParameterInt)parameter).ValueInt;
            }
            if (parameter.Type == IndicatorParameterType.String)
            {
                ((IndicatorParameterString)this).ValueString = ((IndicatorParameterString)parameter).ValueString;
            }

            parameter.ValueChange += delegate
            {
                if (parameter.Type == IndicatorParameterType.Bool)
                {
                    ((IndicatorParameterBool)this).ValueBool = ((IndicatorParameterBool)parameter).ValueBool;
                }
                if (parameter.Type == IndicatorParameterType.Decimal)
                {
                    ((IndicatorParameterDecimal)this).ValueDecimal = ((IndicatorParameterDecimal)parameter).ValueDecimal;
                }
                if (parameter.Type == IndicatorParameterType.Int)
                {
                    ((IndicatorParameterInt)this).ValueInt = ((IndicatorParameterInt)parameter).ValueInt;
                }
                if (parameter.Type == IndicatorParameterType.String)
                {
                    ((IndicatorParameterString)this).ValueString = ((IndicatorParameterString)parameter).ValueString;
                }
            };

            this.ValueChange += delegate
            {
                if (parameter.Type == IndicatorParameterType.Bool)
                {
                    ((IndicatorParameterBool)parameter)._valueBool = ((IndicatorParameterBool)this).ValueBool;
                }
                if (parameter.Type == IndicatorParameterType.Decimal)
                {
                    ((IndicatorParameterDecimal)parameter)._valueDecimal = ((IndicatorParameterDecimal)this).ValueDecimal;
                }
                if (parameter.Type == IndicatorParameterType.Int)
                {
                    ((IndicatorParameterInt)parameter)._valueInt = ((IndicatorParameterInt)this).ValueInt;
                }
                if (parameter.Type == IndicatorParameterType.String)
                {
                    ((IndicatorParameterString)parameter)._valueString = ((IndicatorParameterString)this).ValueString;
                }
            };
        }

        /// <summary>
        /// unique parameter name
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// get save string
        /// </summary>
        public abstract string GetStringToSave();

        /// <summary>
        /// load a parameter from the string
        /// </summary>
        public abstract void LoadParamFromString(string[] save);

        /// <summary>
        /// parameter type
        /// </summary>
        public abstract IndicatorParameterType Type { get; }

        /// <summary>
        /// the parameter state has changed
        /// </summary>
        public abstract event Action ValueChange;
    }

    /// <summary>
    /// Parameter for an Int value
    /// </summary>
    public class IndicatorParameterInt : IndicatorParameter
    {
        /// <summary>
        /// constructor to create a parameter storing Int variables
        /// </summary>
        /// <param name="name">parameter name</param>
        /// <param name="value">default value</param>
        public IndicatorParameterInt(string name, int value)
        {
            _name = name;
            _valueInt = value;
            _valueIntDefault = value;
        }

        /// <summary>
        /// closed constructor
        /// </summary>
        private IndicatorParameterInt()
        {

        }

        /// <summary>
        /// unique parameter name
        /// </summary>
        public override string Name
        {
            get { return _name; }
        }
        private string _name;

        /// <summary>
        /// get save string
        /// </summary>
        public override string GetStringToSave()
        {
            string save = _name + "#";

            save += ValueInt + "#";
            return save;
        }

        /// <summary>
        /// load a parameter from the string
        /// </summary>
        public override void LoadParamFromString(string[] save)
        {
            _valueInt = Convert.ToInt32(save[1]);
        }

        /// <summary>
        /// parameter type
        /// </summary>
        public override IndicatorParameterType Type
        {
            get { return IndicatorParameterType.Int; }
        }

        /// <summary>
        /// current value
        /// </summary>
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

        /// <summary>
        /// default value
        /// </summary>
        public int ValueIntDefault
        {
            get
            {
                return _valueIntDefault;
            }
        }
        private int _valueIntDefault;

        /// <summary>
        /// the parameter state has changed
        /// </summary>
        public override event Action ValueChange;

        public static implicit operator int(IndicatorParameterInt parameter) => parameter.ValueInt;
    }

    /// <summary>
    /// Parameter for a Decimal value
    /// </summary>
    public class IndicatorParameterDecimal : IndicatorParameter
    {

        /// <summary>
        /// constructor for creating a parameter storing Decimal variables
        /// </summary>
        /// <param name="name">parameter name</param>
        /// <param name="value">default value</param>
        public IndicatorParameterDecimal(string name, decimal value)
        {
            _name = name;
            _valueDecimal = value;
            _valueDecimalDefault = value;
            _type = IndicatorParameterType.Decimal;
        }

        /// <summary>
        /// closed constructor
        /// </summary>
        private IndicatorParameterDecimal()
        {

        }

        /// <summary>
        /// unique parameter name
        /// </summary>
        public override string Name
        {
            get { return _name; }
        }
        private string _name;

        /// <summary>
        /// get save string
        /// </summary>
        public override string GetStringToSave()
        {
            string save = _name + "#";
            save += ValueDecimal + "#";
            return save;
        }

        /// <summary>
        /// load a parameter from the string
        /// </summary>
        public override void LoadParamFromString(string[] save)
        {
            _valueDecimal = save[1].ToDecimal();
        }

        /// <summary>
        /// parameter type
        /// </summary>
        public override IndicatorParameterType Type
        {
            get { return _type; }
        }
        private IndicatorParameterType _type;

        /// <summary>
        /// current value
        /// </summary>
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

        /// <summary>
        /// default value
        /// </summary>
        public decimal ValueDecimalDefault
        {
            get
            {
                return _valueDecimalDefault;
            }
        }
        private decimal _valueDecimalDefault;

        /// <summary>
        /// the parameter state has changed
        /// </summary>
        public override event Action ValueChange;

        public static implicit operator decimal(IndicatorParameterDecimal parameter) => parameter.ValueDecimal;
    }

    /// <summary>
    /// Parameter for a Bool value
    /// </summary>
    public class IndicatorParameterBool : IndicatorParameter
    {
        /// <summary>
        /// constructor for creating a parameter storing Bool variables
        /// </summary>
        /// <param name="name">parameter name</param>
        /// <param name="value">default value</param>
        public IndicatorParameterBool(string name, bool value)
        {
            _name = name;
            _valueBoolDefault = value;
            _valueBool = value;
            _type = IndicatorParameterType.Bool;
        }

        /// <summary>
        /// closed constructor
        /// </summary>
        private IndicatorParameterBool()
        {

        }

        /// <summary>
        /// unique parameter name
        /// </summary>
        public override string Name
        {
            get { return _name; }
        }
        private string _name;

        /// <summary>
        /// get save string
        /// </summary>
        public override string GetStringToSave()
        {
            string save = _name + "#";
            save += ValueBool + "#";
            return save;
        }

        /// <summary>
        /// load a parameter from the string
        /// </summary>
        public override void LoadParamFromString(string[] save)
        {
            _valueBool = Convert.ToBoolean(save[1]);
        }

        /// <summary>
        /// parameter type
        /// </summary>
        public override IndicatorParameterType Type
        {
            get { return _type; }
        }
        private IndicatorParameterType _type;

        /// <summary>
        /// current value
        /// </summary>
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

        /// <summary>
        /// default value
        /// </summary>
        public bool ValueBoolDefault
        {
            get
            {
                return _valueBoolDefault;
            }
        }
        private bool _valueBoolDefault;

        /// <summary>
        /// the parameter state has changed
        /// </summary>
        public override event Action ValueChange;

        public static implicit operator bool(IndicatorParameterBool parameter) => parameter.ValueBool;
    }

    /// <summary>
    /// Parameter for a String or string collection values
    /// </summary>
    public class IndicatorParameterString : IndicatorParameter
    {
        /// <summary>
        /// constructor for creating a parameter storing String collection variables
        /// </summary>
        /// <param name="name">parameter name</param>
        /// <param name="value">default value</param>
        /// <param name="collection">possible values collection</param>
        public IndicatorParameterString(string name, string value, List<string> collection)
        {
            _name = name;
            _valueString = value;
            _valueStringDefault = value;
            _setStringValues = collection;
            _type = IndicatorParameterType.String;
        }

        /// <summary>
        /// constructor for creating a parameter storing single String variable
        /// </summary>
        /// <param name="name">parameter name</param>
        /// <param name="value">default value</param>
        public IndicatorParameterString(string name, string value)
        {
            if (value == null)
            {
                value = "";
            }

            _name = name;
            _valueString = value;
            _valueStringDefault = value;
            _setStringValues = new List<string>(){value};
            _type = IndicatorParameterType.String;
        }

        /// <summary>
        /// closed constructor
        /// </summary>
        private IndicatorParameterString()
        {

        }

        /// <summary>
        /// unique parameter name
        /// </summary>
        public override string Name
        {
            get { return _name; }
        }
        private string _name;

        /// <summary>
        /// get save string
        /// </summary>
        public override string GetStringToSave()
        {
            string save = _name + "#";
            save += ValueString + "#";

            return save;
        }

        /// <summary>
        /// load a parameter from the string
        /// </summary>
        public override void LoadParamFromString(string[] save)
        {
            _valueString = save[1];
        }

        /// <summary>
        /// parameter type
        /// </summary>
        public override IndicatorParameterType Type
        {
            get { return _type; }
        }
        private IndicatorParameterType _type;

        /// <summary>
        /// current value
        /// </summary>
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

        /// <summary>
        /// default value
        /// </summary>
        public string ValueStringDefault
        {
            get { return _valueStringDefault; }
        }
        private string _valueStringDefault;
        public List<string> ValuesString
        {
            get
            {
                if (_type != IndicatorParameterType.String)
                {
                    throw new Exception("Попытка запросить у параметра с типом String, поле " + _type);
                }
                return _setStringValues;
            }
        }

        private List<string> _setStringValues;

        /// <summary>
        /// the parameter state has changed
        /// </summary>
        public override event Action ValueChange;

        public static implicit operator string(IndicatorParameterString parameter) => parameter.ValueString;
    }

    /// <summary>
    /// Parameter type
    /// </summary>
    public enum IndicatorParameterType
    {
        /// <summary>
        /// Int parameter
        /// </summary>
        Int,

        /// <summary>
        /// Decimal parameter
        /// </summary>
        Decimal,

        /// <summary>
        /// String collection parameter
        /// </summary>
        String,

        /// <summary>
        /// Boolean parameter
        /// </summary>
        Bool
    }
}
