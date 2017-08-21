using System;
using System.Collections.Generic;

namespace OsEngine.Entity
{
    /// <summary>
    /// параметр стратегии
    /// </summary>
    public class StrategyParameter
    {

        /// <summary>
        /// конструктор для создания параметра хранящего переменные типа Decimal
        /// </summary>
        /// <param name="name">Имя параметра</param>
        /// <param name="value">Значение по умолчанию</param>
        /// <param name="start">Первое значение при оптимизации</param>
        /// <param name="stop">Последнее значение при оптимизации</param>
        /// <param name="step">Шаг изменения при оптимизации</param>
        public StrategyParameter(string name, decimal value, decimal start, decimal stop, decimal step)
        {
            if (start > stop)
            {
                throw new Exception("Начальное значение параметра не может быть больше последнему");
            }

            _name = name;
            _valueDecimal = value;
            _valueDecimalDefolt = value;
            _valueDecimalStart = start;
            _valueDecimalStop = stop;
            _valueDecimalStep = step;
            _type = StrategyParameterType.Decimal;
        }

        /// <summary>
        /// конструктор для создания параметра хранящего переменные типа Int
        /// </summary>
        /// <param name="name">Имя параметра</param>
        /// <param name="value">Значение по умолчанию</param>
        /// <param name="start">Первое значение при оптимизации</param>
        /// <param name="stop">Последнее значение при оптимизации</param>
        /// <param name="step">Шаг изменения при оптимизации</param>
        public StrategyParameter(string name, int value, int start, int stop, int step)
        {
            if (start > stop)
            {
                throw new Exception("Начальное значение параметра не может быть больше последнему");
            }

            _name = name;
            _valueInt = value;
            _valueIntDefolt = value;
            _valueIntStart = start;
            _valueIntStop = stop;
            _valueIntStep = step;
            _type = StrategyParameterType.Int;
        }

        /// <summary>
        /// конструктор для создания параметра хранящего переменные типа String
        /// </summary>
        /// <param name="name">Имя параметра</param>
        /// <param name="value">Значение по умолчанию</param>
        /// <param name="collection">Возможные варианты значений</param>
        public StrategyParameter(string name, string value, List<string> collection)
        {
            _name = name;
            _valueString = value;
            _setStringValues = collection;
            _type = StrategyParameterType.String;
        }

        /// <summary>
        /// конструктор для создания параметра хранящего переменные типа bool
        /// </summary>
        /// <param name="name">Имя параметра</param>
        /// <param name="value">Значение по умолчанию</param>
        public StrategyParameter(string name, bool value)
        {
            _name = name;
            _valueBoolDefolt = value;
            _type = StrategyParameterType.Bool;
        }

        /// <summary>
        /// заглушка. нельзя создать переменную типа StrategyParameter с пустым конструктором
        /// </summary>
        private StrategyParameter()
        {

        }

        /// <summary>
        /// взять строку для сохранения
        /// </summary>
        public string GetStringToSave()
        {
            string save = _name + "#";
            if (Type == StrategyParameterType.Decimal)
            {
                save += ValueDecimal + "#";
            }
            if (Type == StrategyParameterType.Bool)
            {
                save += ValueBool + "#";
            }
            if (Type == StrategyParameterType.Int)
            {
                save += ValueInt + "#";
            }
            if (Type == StrategyParameterType.String)
            {
                save += ValueString + "#";
            }

            return save;
        }

        /// <summary>
        /// загрузить настройки из файла сохранения
        /// </summary>
        /// <param name="save"></param>
        public void LoadParamFromString(string[] save)
        {
            if (Type == StrategyParameterType.Decimal)
            {
                _valueDecimal = Convert.ToDecimal(save[1]);
            }
            if (Type == StrategyParameterType.Bool)
            {
                _valueBool = Convert.ToBoolean(save[1]);
            }
            if (Type == StrategyParameterType.Int)
            {
                _valueInt = Convert.ToInt32(save[1]);
            }
            if (Type == StrategyParameterType.String)
            {
                _valueString = save[1];
            }
        }

        /// <summary>
        /// Название параметра. Используется для идентификации параметра в окнах настроек
        /// </summary>
        public string Name
        {
            get { return _name; }   
        }
        private string _name;

        /// <summary>
        /// тип параметра
        /// </summary>
        public StrategyParameterType Type
        {
            get { return _type; }
        }
        private StrategyParameterType _type;

        /// <summary>
        /// текущее значение параметра Decimal
        /// </summary>
        public decimal ValueDecimal
        {
            get
            {
                if (_type != StrategyParameterType.Decimal)
                {
                    throw new Exception("Попытка запросить у параметра с типом Decimal, поле " + _type);
                } 
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
        private decimal _valueDecimal;

        /// <summary>
        /// значение по умолчанию для параметра типа Decimal
        /// </summary>
        public decimal ValueDecimalDefolt
        {
            get
            {
                if (_type != StrategyParameterType.Decimal)
                {
                    throw new Exception("Попытка запросить у параметра с типом Decimal, поле " + _type);
                }
                return _valueDecimalDefolt;
            }
        }
        private decimal _valueDecimalDefolt;

        /// <summary>
        /// начальное значение параметра типа Decimal
        /// </summary>
        public decimal ValueDecimalStart
        {
            get
            {
                if (_type != StrategyParameterType.Decimal)
                {
                    throw new Exception("Попытка запросить у параметра с типом Decimal, поле " + _type);
                }
                return _valueDecimalStart;
            }
        }
        private decimal _valueDecimalStart;

        /// <summary>
        /// последнее значение параметра типа Decimal
        /// </summary>
        public decimal ValueDecimalStop
        {
            get
            {
                if (_type != StrategyParameterType.Decimal)
                {
                    throw new Exception("Попытка запросить у параметра с типом Decimal, поле " + _type);
                }
                return _valueDecimalStop;
            }
        }
        private decimal _valueDecimalStop;

        /// <summary>
        /// шаг приращения параметра типа Decimal
        /// </summary>
        public decimal ValueDecimalStep
        {
            get
            {
                if (_type != StrategyParameterType.Decimal)
                {
                    throw new Exception("Попытка запросить у параметра с типом Decimal, поле " + _type);
                } 
                return _valueDecimalStep;
            }
        }
        private decimal _valueDecimalStep;


        /// <summary>
        /// текущее значение параметра типа Int
        /// </summary>
        public int ValueInt
        {
            get
            {
                if (_type != StrategyParameterType.Int)
                {
                    throw new Exception("Попытка запросить у параметра с типом Int, поле " + _type);
                } 
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
        private int _valueInt;
        
        /// <summary>
        /// значение по умолчанию для параметра типа Int
        /// </summary>
        public int ValueIntDefolt
        {
            get
            {
                if (_type != StrategyParameterType.Int)
                {
                    throw new Exception("Попытка запросить у параметра с типом Int, поле " + _type);
                } 
                return _valueIntDefolt;
            }
        }
        private int _valueIntDefolt;

        /// <summary>
        /// стартовое значение при оптимизации для параметра типа Int
        /// </summary>
        public int ValueIntStart
        {
            get
            {
                if (_type != StrategyParameterType.Int)
                {
                    throw new Exception("Попытка запросить у параметра с типом Int, поле " + _type);
                } 
                return _valueIntStart;
            }
        }
        private int _valueIntStart;

        /// <summary>
        /// последнее значение при оптимизации для параметра типа Int
        /// </summary>
        public int ValueIntStop
        {
            get
            {
                if (_type != StrategyParameterType.Int)
                {
                    throw new Exception("Попытка запросить у параметра с типом Int, поле " + _type);
                } 
                return _valueIntStop;
            }
        }
        private int _valueIntStop;

        /// <summary>
        /// шаг приращения для параметра типа Int 
        /// </summary>
        public int ValueIntStep
        {
            get
            {
                if (_type != StrategyParameterType.Int)
                {
                    throw new Exception("Попытка запросить у параметра с типом Int, поле " + _type);
                } 
                return _valueIntStep;
            }
        }
        private int _valueIntStep;

        /// <summary>
        /// значение булева параметра
        /// </summary>
        public bool ValueBool
        {
            get
            {
                if (_type != StrategyParameterType.Bool)
                {
                    throw new Exception("Попытка запросить у параметра с типом Bool, поле " + _type);
                } 
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
        private bool _valueBool;

        /// <summary>
        /// значение по умолчанию для булева параметра
        /// </summary>
        public bool ValueBoolDefolt
        {
            get
            {
                if (_type != StrategyParameterType.Bool)
                {
                    throw new Exception("Попытка запросить у параметра с типом Bool, поле " + _type);
                } 
                return _valueBoolDefolt;
            }
        }
        private bool _valueBoolDefolt;

        /// <summary>
        /// текущее значение параметра типа string
        /// </summary>
        public string ValueString
        {
            get
            {
                if (_type != StrategyParameterType.String)
                {
                    throw new Exception("Попытка запросить у параметра с типом String, поле " + _type);
                } 
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
        private string _valueString;
        public List<string> ValuesString
        {
            get
            {
                if (_type != StrategyParameterType.String)
                {
                    throw new Exception("Попытка запросить у параметра с типом String, поле " + _type);
                } 
                return _setStringValues;
            }
        }

        private List<string> _setStringValues;

        /// <summary>
        /// событие: параметр изменился
        /// </summary>
        public event Action ValueChange;
    }

    /// <summary>
    /// тип параметра
    /// </summary>
    public enum StrategyParameterType
    {
        /// <summary>
        /// целое число с типом Int
        /// </summary>
        Int,

        /// <summary>
        /// число с плавающей точкой типа decimal
        /// </summary>
        Decimal,

        /// <summary>
        /// строка
        /// </summary>
        String,

        /// <summary>
        /// булево значение
        /// </summary>
        Bool
    }

}
