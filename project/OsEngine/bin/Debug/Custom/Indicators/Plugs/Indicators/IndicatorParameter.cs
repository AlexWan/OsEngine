/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;

namespace OsEngine.Indicators
{
    /// <summary>
    /// parameter interface
    /// интерфейс для параметра
    /// </summary>
    public abstract class IndicatorParameter
    {
        public void DoDefault()
        {
            if (this.Type == IndicatorParameterType.Bool)
            {
                ((IndicatorParameterBool)this).ValueBool = ((IndicatorParameterBool)this).ValueBoolDefolt;
            }
            if (this.Type == IndicatorParameterType.Decimal)
            {
                ((IndicatorParameterDecimal)this).ValueDecimal = ((IndicatorParameterDecimal)this).ValueDecimalDefolt;
            }
            if (this.Type == IndicatorParameterType.Int)
            {
                ((IndicatorParameterInt)this).ValueInt = ((IndicatorParameterInt)this).ValueIntDefolt;
            }
            if (this.Type == IndicatorParameterType.String)
            {
                ((IndicatorParameterString)this).ValueString = ((IndicatorParameterString)this).ValueStringDefault;
            }
        }

        public void Bind(IndicatorParameter param)
        {
            if (param.Type != Type)
            {
                throw new Exception("Can`t bind param with not equals types");
            }

            if (param.Type == IndicatorParameterType.Bool)
            {
                ((IndicatorParameterBool)this).ValueBool = ((IndicatorParameterBool)param).ValueBool;
            }
            if (param.Type == IndicatorParameterType.Decimal)
            {
                ((IndicatorParameterDecimal)this).ValueDecimal = ((IndicatorParameterDecimal)param).ValueDecimal;
            }
            if (param.Type == IndicatorParameterType.Int)
            {
                ((IndicatorParameterInt)this).ValueInt = ((IndicatorParameterInt)param).ValueInt;
            }
            if (param.Type == IndicatorParameterType.String)
            {
                ((IndicatorParameterString)this).ValueString = ((IndicatorParameterString)param).ValueString;
            }

            param.ValueChange += delegate
            {
                if (param.Type == IndicatorParameterType.Bool)
                {
                    ((IndicatorParameterBool)this).ValueBool = ((IndicatorParameterBool)param).ValueBool;
                }
                if (param.Type == IndicatorParameterType.Decimal)
                {
                    ((IndicatorParameterDecimal)this).ValueDecimal = ((IndicatorParameterDecimal)param).ValueDecimal;
                }
                if (param.Type == IndicatorParameterType.Int)
                {
                    ((IndicatorParameterInt)this).ValueInt = ((IndicatorParameterInt)param).ValueInt;
                }
                if (param.Type == IndicatorParameterType.String)
                {
                    ((IndicatorParameterString)this).ValueString = ((IndicatorParameterString)param).ValueString;
                }
            };

            this.ValueChange += delegate
            {
                if (param.Type == IndicatorParameterType.Bool)
                {
                    ((IndicatorParameterBool)param)._valueBool = ((IndicatorParameterBool)this).ValueBool;
                }
                if (param.Type == IndicatorParameterType.Decimal)
                {
                    ((IndicatorParameterDecimal)param)._valueDecimal = ((IndicatorParameterDecimal)this).ValueDecimal;
                }
                if (param.Type == IndicatorParameterType.Int)
                {
                    ((IndicatorParameterInt)param)._valueInt = ((IndicatorParameterInt)this).ValueInt;
                }
                if (param.Type == IndicatorParameterType.String)
                {
                    ((IndicatorParameterString)param)._valueString = ((IndicatorParameterString)this).ValueString;
                }
            };
        }

        /// <summary>
        /// уникальное имя параметра
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// unique parameter name
        /// взять строку для сохранения
        /// </summary>
        public abstract string GetStringToSave();

        /// <summary>
        /// загрузить параметр из строки
        /// загрузить параметр из строки
        /// </summary>
        /// <param name="save">line with saved parameters/строка с сохранёнными параметрами</param>
        public abstract void LoadParamFromString(string[] save);

        /// <summary>
        /// parameter type
        /// тип параметра
        /// </summary>
        public abstract IndicatorParameterType Type { get; }

        /// <summary>
        /// the parameter state has changed
        /// изменилось состояние параметра
        /// </summary>
        public abstract event Action ValueChange;
    }

    /// <summary>
    /// Parameter for an Int strategy
    /// параметр для стратегии типа Int
    /// </summary>
    public class IndicatorParameterInt : IndicatorParameter
    {
        /// <summary>
        /// constructor to create a parameter storing Int variables
        /// конструктор для создания параметра хранящего переменные типа Int
        /// </summary>
        /// <param name="name">Parameter name/Имя параметра</param>
        /// <param name="value">Default value/Значение по умолчанию</param>
        /// <param name="start">First value in optimization/Первое значение при оптимизации</param>
        /// <param name="stop">Last value during optimization/Последнее значение при оптимизации</param>
        /// <param name="step">Step change in optimization/Шаг изменения при оптимизации</param>
        public IndicatorParameterInt(string name, int value)
        {
            _name = name;
            _valueInt = value;
            _valueIntDefolt = value;
        }

        /// <summary>
        /// closed constructor
        /// закрытый конструктор
        /// </summary>
        private IndicatorParameterInt()
        {

        }

        /// <summary>
        /// unique parameter name
        /// уникальное имя параметра
        /// </summary>
        public override string Name
        {
            get { return _name; }
        }
        private string _name;

        /// <summary>
        /// save the line
        /// взять строку сохранения
        /// </summary>
        public override string GetStringToSave()
        {
            string save = _name + "#";

            save += ValueInt + "#";
            return save;
        }

        /// <summary>
        /// Load the parameter from the saved file
        /// загрузить параметр из сохранённого файла
        /// </summary>
        public override void LoadParamFromString(string[] save)
        {
            _valueInt = Convert.ToInt32(save[1]);
        }

        /// <summary>
        /// parameter type
        /// тип параметра
        /// </summary>
        public override IndicatorParameterType Type
        {
            get { return IndicatorParameterType.Int; }
        }

        /// <summary>
        /// current value of the parameter of Int type
        /// текущее значение параметра типа Int
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
        /// default value for the Int type parameter
        /// значение по умолчанию для параметра типа Int
        /// </summary>
        public int ValueIntDefolt
        {
            get
            {
                return _valueIntDefolt;
            }
        }
        private int _valueIntDefolt;


        /// <summary>
        /// the parameter state has changed
        /// изменилось состояние параметра
        /// </summary>
        public override event Action ValueChange;
    }

    /// <summary>
    /// The parameter of the Decimal type strategy
    /// параметр стратегии типа Decimal
    /// </summary>
    public class IndicatorParameterDecimal : IndicatorParameter
    {

        /// <summary>
        /// Designer for creating a parameter storing Decimal type variables
        /// конструктор для создания параметра хранящего переменные типа Decimal
        /// </summary>
        /// <param name="name">Parameter name/Имя параметра</param>
        /// <param name="value">Default value/Значение по умолчанию</param>
        public IndicatorParameterDecimal(string name, decimal value)
        {
            _name = name;
            _valueDecimal = value;
            _valueDecimalDefolt = value;
            _type = IndicatorParameterType.Decimal;
        }

        /// <summary>
        /// blank. it is impossible to create a variable of StrategyParameter type with an empty constructor
        /// заглушка. нельзя создать переменную типа StrategyParameter с пустым конструктором
        /// </summary>
        private IndicatorParameterDecimal()
        {

        }

        /// <summary>
        /// to take a line to save
        /// взять строку для сохранения
        /// </summary>
        public override string GetStringToSave()
        {
            string save = _name + "#";
            save += ValueDecimal + "#";
            return save;
        }

        /// <summary>
        /// download settings from the save file
        /// загрузить настройки из файла сохранения
        /// </summary>
        /// <param name="save"></param>
        public override void LoadParamFromString(string[] save)
        {
            _valueDecimal = Convert.ToDecimal(save[1]);
        }

        /// <summary>
        /// Parameter name. Used to identify a parameter in the settings windows
        /// Название параметра. Используется для идентификации параметра в окнах настроек
        /// </summary>
        public override string Name
        {
            get { return _name; }
        }
        private string _name;

        /// <summary>
        /// parameter type
        /// тип параметра
        /// </summary>
        public override IndicatorParameterType Type
        {
            get { return _type; }
        }
        private IndicatorParameterType _type;

        /// <summary>
        /// current value of the Decimal parameter
        /// текущее значение параметра Decimal
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
        /// default value for the Decimal type
        /// значение по умолчанию для параметра типа Decimal
        /// </summary>
        public decimal ValueDecimalDefolt
        {
            get
            {
                return _valueDecimalDefolt;
            }
        }
        private decimal _valueDecimalDefolt;


        /// <summary>
        /// event: the parameter has changed
        /// событие: параметр изменился
        /// </summary>
        public override event Action ValueChange;
    }

    /// <summary>
    /// Bool type strategy parameter
    /// параметр стратегии типа Bool
    /// </summary>
    public class IndicatorParameterBool : IndicatorParameter
    {
        public IndicatorParameterBool(string name, bool value)
        {
            _name = name;
            _valueBoolDefolt = value;
            _valueBool = value;
            _type = IndicatorParameterType.Bool;
        }

        /// <summary>
        /// blank. it is impossible to create a variable of StrategyParameter type with an empty constructor
        /// заглушка. нельзя создать переменную типа StrategyParameter с пустым конструктором
        /// </summary>
        private IndicatorParameterBool()
        {

        }

        /// <summary>
        /// to take a line to save
        /// взять строку для сохранения
        /// </summary>
        public override string GetStringToSave()
        {
            string save = _name + "#";
            save += ValueBool + "#";
            return save;
        }

        /// <summary>
        ///  download settings from the save file
        /// загрузить настройки из файла сохранения
        /// </summary>
        /// <param name="save"></param>
        public override void LoadParamFromString(string[] save)
        {
            _valueBool = Convert.ToBoolean(save[1]);
        }

        /// <summary>
        /// Parameter name. Used to identify a parameter in the settings windows
        /// Название параметра. Используется для идентификации параметра в окнах настроек
        /// </summary>
        public override string Name
        {
            get { return _name; }
        }
        private string _name;

        /// <summary>
        /// parameter type
        /// тип параметра
        /// </summary>
        public override IndicatorParameterType Type
        {
            get { return _type; }
        }
        private IndicatorParameterType _type;

        /// <summary>
        /// parameter Boolean value
        /// значение булева параметра
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
        /// default setting for the parameter boolean
        /// значение по умолчанию для булева параметра
        /// </summary>
        public bool ValueBoolDefolt
        {
            get
            {
                return _valueBoolDefolt;
            }
        }
        private bool _valueBoolDefolt;

        /// <summary>
        /// event: the parameter has changed
        /// событие: параметр изменился
        /// </summary>
        public override event Action ValueChange;
    }

    /// <summary>
    /// A strategy parameter that stores a collection of strings
    /// параметр стратегии хранящий в себе коллекцию строк
    /// </summary>
    public class IndicatorParameterString : IndicatorParameter
    {
        /// <summary>
        /// constructor to create a parameter storing variables of String type
        /// конструктор для создания параметра хранящего переменные типа String
        /// </summary>
        /// <param name="name">Parameter name/Имя параметра</param>
        /// <param name="value">Default value/Значение по умолчанию</param>
        /// <param name="collection">Possible value options/Возможные варианты значений</param>
        public IndicatorParameterString(string name, string value, List<string> collection)
        {
            _name = name;
            _valueString = value;
            _valueStringDefault = value;
            _setStringValues = collection;
            _type = IndicatorParameterType.String;
        }

        /// <summary>
        /// constructor to create a parameter storing variables of String type
        /// конструктор для создания параметра хранящего переменные типа String
        /// </summary>
        /// <param name="name">Parameter name/Имя параметра</param>
        /// <param name="value">Default value/Значение по умолчанию</param>
        public IndicatorParameterString(string name, string value)
        {
            if (value == null)
            {
                value = "";
            }

            _name = name;
            _valueString = value;
            _valueStringDefault = value;
            _setStringValues = new List<string>() { value };
            _type = IndicatorParameterType.String;
        }

        /// <summary>
        /// blank. it is impossible to create a variable of StrategyParameter type with an empty constructor
        /// заглушка. нельзя создать переменную типа StrategyParameter с пустым конструктором
        /// </summary>
        private IndicatorParameterString()
        {

        }

        /// <summary>
        /// to take a line to save
        /// взять строку для сохранения
        /// </summary>
        public override string GetStringToSave()
        {
            string save = _name + "#";
            save += ValueString + "#";

            return save;
        }

        /// <summary>
        /// download settings from the save file
        /// загрузить настройки из файла сохранения
        /// </summary>
        /// <param name="save"></param>
        public override void LoadParamFromString(string[] save)
        {
            _valueString = save[1];
        }

        /// <summary>
        /// Parameter name. Used to identify a parameter in the settings windows
        /// Название параметра. Используется для идентификации параметра в окнах настроек
        /// </summary>
        public override string Name
        {
            get { return _name; }
        }
        private string _name;

        /// <summary>
        /// parameter type
        /// тип параметра
        /// </summary>
        public override IndicatorParameterType Type
        {
            get { return _type; }
        }
        private IndicatorParameterType _type;

        /// <summary>
        /// current value of the string type parameter
        /// текущее значение параметра типа string
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
        /// event: the parameter has changed
        /// событие: параметр изменился
        /// </summary>
        public override event Action ValueChange;
    }

    /// <summary>
    /// parameter type
    /// тип параметра
    /// </summary>
    public enum IndicatorParameterType
    {
        /// <summary>
        /// an integer number with the type Int
        /// целое число с типом Int
        /// </summary>
        Int,

        /// <summary>
        /// a floating point number of the decimal type
        /// число с плавающей точкой типа decimal
        /// </summary>
        Decimal,

        /// <summary>
        /// string
        /// строка
        /// </summary>
        String,

        /// <summary>
        /// Boolean value
        /// булево значение
        /// </summary>
        Bool
    }
}