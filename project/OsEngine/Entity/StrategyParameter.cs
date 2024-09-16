/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace OsEngine.Entity
{
    /// <summary>
    /// Parameter interface
    /// </summary>
    public interface IIStrategyParameter
    {
        /// <summary>
        /// Uniq parameter name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Get formatted string to save to file
        /// </summary>
        string GetStringToSave();

        /// <summary>
        /// Load parameter from string
        /// </summary>
        /// <param name="save">line with saved parameters</param>
        void LoadParamFromString(string[] save);

        /// <summary>
        /// Parameter type
        /// </summary>
        StrategyParameterType Type { get; }

        /// <summary>
        /// Owner tab name
        /// </summary>
        string TabName { get; set; }

        /// <summary>
        /// Event: parameter state changed
        /// </summary>
        event Action ValueChange;
    }

    /// <summary>
    /// Parameter for label type strategy
    /// </summary>
    public class StrategyParameterLabel : IIStrategyParameter
    {
        /// <summary>
        ///  Constructor to create a parameter storing Int variables
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <param name="label">Displayed label</param>
        /// <param name="value">Displayed value</param>
        /// <param name="rowHeight">Row height</param>
        /// <param name="textHeight">Text height</param>
        /// <param name="color">Displayed color</param>
        /// <param name="tabName">Owner tab name</param>
        /// <exception cref="Exception">the parameter name of the robot contains a special character. This will cause errors. Take it away</exception>
        public StrategyParameterLabel(string name, string label, string value, int rowHeight, int textHeight, 
            System.Drawing.Color color, string tabName = null)
        {
            if (name.HaveExcessInString())
            {
                throw new Exception("The parameter name of the robot contains a special character. This will cause errors. Take it away");
            }

            _name = name;
            Label = label;
            Value = value;
            TabName = tabName;
            RowHeight = rowHeight;
            TextHeight = textHeight;
            Color = color;
        }

        /// <summary>
        /// Uniq parameter name
        /// </summary>
        public string Name { get { return _name; } }

        private string _name;

        /// <summary>
        /// Displayed label
        /// </summary>
        public string Label;

        /// <summary>
        /// Displayed value
        /// </summary>
        public string Value;

        /// <summary>
        /// Row height
        /// </summary>
        public int RowHeight;

        /// <summary>
        /// Text height
        /// </summary>
        public int TextHeight;

        /// <summary>
        /// Displayed color
        /// </summary>
        public System.Drawing.Color Color;

        /// <summary>
        /// Parameter type
        /// </summary>
        public StrategyParameterType Type { get { return StrategyParameterType.Label; } }

        /// <summary>
        /// Owner tab name
        /// </summary>
        public string TabName { get; set; }

        /// <summary>
        /// Event: parameter state changed
        /// </summary>
        public event Action ValueChange;

        /// <summary>
        /// Get formatted string to save to file
        /// </summary>
        public string GetStringToSave()
        {
            string save = _name + "#";

            save += Label + "#";
            save += Value + "#";
            save += RowHeight + "#";
            save += TextHeight + "#";
            save += Color.ToArgb() + "#";

            return save;
        }

        /// <summary>
        /// Load parameter from string
        /// </summary>
        /// <param name="save">line with saved parameters</param>
        public void LoadParamFromString(string[] save)
        {
            try
            {
                Label = save[1];
                Value = save[2];
                RowHeight = Convert.ToInt32(save[3]);
                TextHeight = Convert.ToInt32(save[4]);
                Color = System.Drawing.Color.FromArgb(Convert.ToInt32(save[5]));
            }
            catch
            {
                // ignore 
            }
        }
    }

    /// <summary>
    /// Parameter for an Int strategy
    /// </summary>
    public class StrategyParameterInt : IIStrategyParameter
    {
        /// <summary>
        /// Constructor to create a parameter storing Int variables
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <param name="value">Default value</param>
        /// <param name="start">First value in optimization</param>
        /// <param name="stop">Last value during optimization</param>
        /// <param name="step">Step change in optimization</param>
        public StrategyParameterInt(string name, int value, int start, int stop, int step, string tabName = null)
        {
            if (name.HaveExcessInString())
            {
                throw new Exception("The parameter name of the robot contains a special character. This will cause errors. Take it away");
            }

            if (start > stop)
            {
                throw new Exception("The initial value of the parameter cannot be greater than the last");
            }

            _name = name;
            _valueInt = value;
            _valueIntDefolt = value;
            _valueIntStart = start;
            _valueIntStop = stop;
            _valueIntStep = step;
            TabName = tabName;
        }

        /// <summary>
        /// Closed constructor
        /// </summary>
        private StrategyParameterInt()
        {

        }

        /// <summary>
        /// Uniq parameter name
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        private string _name;

        /// <summary>
        /// Owner tab name
        /// </summary>
        public string TabName
        {
            get; set;
        }

        /// <summary>
        /// Get formatted string to save to file
        /// </summary>
        public string GetStringToSave()
        {
            string save = _name + "#";

            save += _valueInt + "#";
            save += _valueIntDefolt + "#";
            save += _valueIntStart + "#";
            save += _valueIntStop + "#";
            save += _valueIntStep + "#";

            return save;
        }

        /// <summary>
        /// Load parameter from string
        /// </summary>
        /// <param name="save">line with saved parameters</param>
        public void LoadParamFromString(string[] save)
        {
            _valueInt = Convert.ToInt32(save[1]);

            try
            {
                _valueIntDefolt = Convert.ToInt32(save[2]);
                _valueIntStart = Convert.ToInt32(save[3]);
                _valueIntStop = Convert.ToInt32(save[4]);
                _valueIntStep = Convert.ToInt32(save[5]);
            }
            catch
            {
                // ignore 
            }

        }

        /// <summary>
        /// Parameter type
        /// </summary>
        public StrategyParameterType Type
        {
            get { return StrategyParameterType.Int; }
        }

        /// <summary>
        /// Current value of the parameter of Int type
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

        private int _valueInt;

        /// <summary>
        /// Default value for the Int type parameter Int
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
        /// Starting value during optimization for the parameter of Int
        /// </summary>
        public int ValueIntStart
        {
            get
            {
                return _valueIntStart;
            }
        }

        private int _valueIntStart;

        /// <summary>
        /// The last value during optimization for the parameter of Int type
        /// </summary>
        public int ValueIntStop
        {
            get
            {
                return _valueIntStop;
            }
        }

        private int _valueIntStop;

        /// <summary>
        /// Incremental step for the Int type parameter 
        /// </summary>
        public int ValueIntStep
        {
            get
            {
                return _valueIntStep;
            }
        }

        private int _valueIntStep;

        /// <summary>
        /// Event: parameter state changed
        /// </summary>
        public event Action ValueChange;
    }

    /// <summary>
    /// The parameter of the Decimal type strategy
    /// </summary>
    public class StrategyParameterDecimal : IIStrategyParameter
    {
        /// <summary>
        /// Designer for creating a parameter storing Decimal type variables
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <param name="value">Default value</param>
        /// <param name="start">First value in optimization</param>
        /// <param name="stop">last value in optimization</param>
        /// <param name="step">Step change in optimization</param>
        public StrategyParameterDecimal(string name, decimal value, decimal start, decimal stop, decimal step, string tabName = null)
        {
            if (name.HaveExcessInString())
            {
                throw new Exception("The parameter name of the robot contains a special character. This will cause errors. Take it away");
            }
            if (start > stop)
            {
                throw new Exception("The initial value of the parameter cannot be greater than the last");
            }

            _name = name;
            _valueDecimal = value;
            _valueDecimalDefolt = value;
            _valueDecimalStart = start;
            _valueDecimalStop = stop;
            _valueDecimalStep = step;
            _type = StrategyParameterType.Decimal;
            TabName = tabName;
        }

        /// <summary>
        /// Blank. it is impossible to create a variable of StrategyParameter type with an empty constructor
        /// </summary>
        private StrategyParameterDecimal()
        {

        }

        /// <summary>
        /// Get formatted string to save to file
        /// </summary>
        public string GetStringToSave()
        {
            string save = _name + "#";
            save += _valueDecimal + "#";
            save += _valueDecimalDefolt + "#";
            save += _valueDecimalStart + "#";
            save += _valueDecimalStop + "#";
            save += _valueDecimalStep + "#";

            return save;
        }

        /// <summary>
        /// Load parameter from string
        /// </summary>
        /// <param name="save">line with saved parameters</param>
        public void LoadParamFromString(string[] save)
        {
            _valueDecimal = save[1].ToDecimal();

            try
            {
                _valueDecimalDefolt = save[2].ToDecimal();
                _valueDecimalStart = save[3].ToDecimal();
                _valueDecimalStop = save[4].ToDecimal();
                _valueDecimalStep = save[5].ToDecimal();
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Uniq parameter name
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        private string _name;

        /// <summary>
        /// Owner tab name
        /// </summary>
        public string TabName
        {
            get; set;
        }

        /// <summary>
        /// Parameter type
        /// </summary>
        public StrategyParameterType Type
        {
            get { return _type; }
        }

        private StrategyParameterType _type;

        /// <summary>
        /// Current value of the Decimal parameter
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

        private decimal _valueDecimal;

        /// <summary>
        /// Default value for the Decimal type
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
        /// Initial value of the Decimal type parameter
        /// </summary>
        public decimal ValueDecimalStart
        {
            get
            {
                return _valueDecimalStart;
            }
        }

        private decimal _valueDecimalStart;

        /// <summary>
        /// The last value of the Decimal type parameter
        /// </summary>
        public decimal ValueDecimalStop
        {
            get
            {
                return _valueDecimalStop;
            }
        }

        private decimal _valueDecimalStop;

        /// <summary>
        /// Incremental step of the Decimal type parameter
        /// </summary>
        public decimal ValueDecimalStep
        {
            get
            {
                return _valueDecimalStep;
            }
        }

        private decimal _valueDecimalStep;

        /// <summary>
        /// Event: parameter state changed
        /// </summary>
        public event Action ValueChange;
    }

    /// <summary>
    /// Bool type strategy parameter
    /// </summary>
    public class StrategyParameterBool : IIStrategyParameter
    {
        /// <summary>
        /// Designer for creating a parameter storing Bool type variables
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <param name="value">Default value</param>
        /// <param name="tabName">Owner tab name</param>
        /// <exception cref="Exception">The parameter name of the robot contains a special character. This will cause errors. Take it away</exception>
        public StrategyParameterBool(string name, bool value, string tabName = null)
        {
            if (name.HaveExcessInString())
            {
                throw new Exception("The parameter name of the robot contains a special character. This will cause errors. Take it away");
            }
            _name = name;
            _valueBoolDefolt = value;
            _valueBool = value;
            _type = StrategyParameterType.Bool;
            TabName = tabName;
        }

        /// <summary>
        /// Blank. it is impossible to create a variable of StrategyParameter type with an empty constructor
        /// </summary>
        private StrategyParameterBool()
        {

        }

        /// <summary>
        /// Owner tab name
        /// </summary>
        public string TabName
        {
            get; set;
        }

        /// <summary>
        /// Get formatted string to save to file
        /// </summary>
        public string GetStringToSave()
        {
            string save = _name + "#";
            save += _valueBool + "#";
            save += _valueBoolDefolt + "#";

            return save;
        }

        /// <summary>
        /// Load parameter from string
        /// </summary>
        /// <param name="save">line with saved parameters</param>
        public void LoadParamFromString(string[] save)
        {
            _name = save[0];
            _valueBool = Convert.ToBoolean(save[1]);

            try
            {
                _valueBoolDefolt = Convert.ToBoolean(save[2]);
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Uniq parameter name
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        private string _name;

        /// <summary>
        /// Parameter type
        /// </summary>
        public StrategyParameterType Type
        {
            get { return _type; }
        }

        private StrategyParameterType _type;

        /// <summary>
        /// Parameter Boolean value
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

        private bool _valueBool;

        /// <summary>
        /// Default setting for the parameter boolean
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
        /// Event: parameter state changed
        /// </summary>
        public event Action ValueChange;
    }

    /// <summary>
    /// A strategy parameter that stores a collection of strings
    /// </summary>
    public class StrategyParameterString : IIStrategyParameter
    {
        /// <summary>
        /// Constructor to create a parameter storing variables of String type
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <param name="value">Default value</param>
        /// <param name="collection">Possible value options</param>
        public StrategyParameterString(string name, string value, List<string> collection, string tabName = null)
        {
            if (name.HaveExcessInString())
            {
                throw new Exception("The parameter name of the robot contains a special character. This will cause errors. Take it away");
            }
            bool isInArray = false;

            if (collection == null)
            {
                collection = new List<string>();
            }

            for (int i = 0; i < collection.Count; i++)
            {
                if (collection[i] == value)
                {
                    isInArray = true;
                    break;
                }
            }

            if (isInArray == false)
            {
                collection.Add(value);
            }

            _name = name;
            _valueString = value;
            _setStringValues = collection;
            _type = StrategyParameterType.String;
            TabName = tabName;
        }

        /// <summary>
        /// Constructor to create a parameter storing variables of String type
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <param name="value">Default value</param>
        public StrategyParameterString(string name, string value, string tabName = null)
        {
            if (name.HaveExcessInString())
            {
                throw new Exception("The parameter name of the robot contains a special character. This will cause errors. Take it away");
            }
            if (value == null)
            {
                value = "";
            }

            _name = name;
            _valueString = value;
            _setStringValues = new List<string>() { value };
            _type = StrategyParameterType.String;
            TabName = tabName;
        }

        /// <summary>
        /// Blank. it is impossible to create a variable of StrategyParameter type with an empty constructor
        /// </summary>
        private StrategyParameterString()
        {

        }

        /// <summary>
        /// Get formatted string to save to file
        /// </summary>
        public string GetStringToSave()
        {
            string save = _name + "#";
            save += _valueString + "#";

            for (int i = 0; i < _setStringValues.Count; i++)
            {
                save += _setStringValues[i] + "#";
            }

            return save;
        }

        /// <summary>
        /// Load parameter from string
        /// </summary>
        /// <param name="save">line with saved parameters</param>
        public void LoadParamFromString(string[] save)
        {
            _valueString = save[1];

            _setStringValues = new List<string>() { };

            for (int i = 2; i < save.Length; i++)
            {
                if (string.IsNullOrEmpty(save[i]))
                {
                    continue;
                }

                _setStringValues.Add(save[i]);
            }
        }

        /// <summary>
        /// Uniq parameter name
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        private string _name;

        /// <summary>
        /// Owner tab name
        /// </summary>
        public string TabName
        {
            get; set;
        }


        /// <summary>
        /// Parameter type
        /// </summary>
        public StrategyParameterType Type
        {
            get { return _type; }
        }

        private StrategyParameterType _type;

        /// <summary>
        /// Current value of the string type parameter
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

        private string _valueString;

        /// <summary>
        /// Parameter string value
        /// </summary>
        public List<string> ValuesString
        {
            get
            {
                if (_type != StrategyParameterType.String)
                {
                    throw new Exception("Attempt to request a parameter with type String, a field " + _type);
                }
                return _setStringValues;
            }
            set { _setStringValues = value; }
        }

        private List<string> _setStringValues;

        /// <summary>
        /// Event: parameter state changed
        /// </summary>
        public event Action ValueChange;
    }

    /// <summary>
    /// Parameter for an TimeOfDay strategy
    /// </summary>
    public class StrategyParameterTimeOfDay : IIStrategyParameter
    {
        /// <summary>
        /// Constructor to create a parameter storing TimeOfDay variables
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <param name="hour">Meaning hours time of day</param>
        /// <param name="minute">Meaning minute time of day</param>
        /// <param name="second">Meaning second time of day</param>
        /// <param name="millisecond">Meaning millisecond time of day</param>
        /// <param name="tabName">Owner tab name</param>
        /// <exception cref="Exception">The parameter name of the robot contains a special character. This will cause errors. Take it away</exception>
        public StrategyParameterTimeOfDay(string name, int hour, int minute, int second, int millisecond, string tabName = null)
        {
            if (name.HaveExcessInString())
            {
                throw new Exception("The parameter name of the robot contains a special character. This will cause errors. Take it away");
            }
            _name = name;
            Value = new TimeOfDay();
            Value.Hour = hour;
            Value.Minute = minute;
            Value.Second = second;
            Value.Millisecond = millisecond;
            _type = StrategyParameterType.TimeOfDay;
            TabName = tabName;
        }

        /// <summary>
        /// Uniq parameter name
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        private string _name;

        /// <summary>
        /// Owner tab name
        /// </summary>
        public string TabName { get; set; }

        /// <summary>
        /// Current parameter value
        /// </summary>
        public TimeOfDay Value;

        /// <summary>
        /// Get formatted string to save to file
        /// </summary>
        public string GetStringToSave()
        {
            string save = _name + "#";
            save += Value.ToString() + "#";

            return save;
        }

        /// <summary>
        /// Load parameter from string
        /// </summary>
        /// <param name="save">line with saved parameters</param>
        public void LoadParamFromString(string[] save)
        {
            if (Value.LoadFromString(save[1]) &&
                ValueChange != null)
            {
                ValueChange();
            }
        }

        /// <summary>
        /// Parameter type
        /// </summary>
        public StrategyParameterType Type
        {
            get { return _type; }
        }

        private StrategyParameterType _type;

        /// <summary>
        /// Event: parameter state changed
        /// </summary>
        public event Action ValueChange;

        /// <summary>
        /// Time span
        /// </summary>
        public TimeSpan TimeSpan
        {
            get
            {
                return Value.TimeSpan;
            }
        }
    }

    /// <summary>
    /// Represents a time of day without a date
    /// </summary>
    public class TimeOfDay
    {
        public int Hour;

        public int Minute;

        public int Second;

        public int Millisecond;

        public override string ToString()
        {
            string result = Hour + ":";
            result += Minute + ":";
            result += Second + ":";
            result += Millisecond;

            return result;
        }

        /// <summary>
        /// Download settings from the save file
        /// </summary>
        /// <param name="save">Data array from storage</param>
        public bool LoadFromString(string save)
        {
            string[] array = save.Split(':');

            bool paramUpdated = false;

            if (Hour != Convert.ToInt32(array[0]))
            {
                Hour = Convert.ToInt32(array[0]);
                paramUpdated = true;
            }
            if (Minute != Convert.ToInt32(array[1]))
            {
                Minute = Convert.ToInt32(array[1]);
                paramUpdated = true;
            }
            if (Second != Convert.ToInt32(array[2]))
            {
                Second = Convert.ToInt32(array[2]);
                paramUpdated = true;
            }
            if (Millisecond != Convert.ToInt32(array[3]))
            {
                Millisecond = Convert.ToInt32(array[3]);
                paramUpdated = true;
            }

            return paramUpdated;
        }

        /// <summary>
        /// Operator overloading allows you to compare instances of this class with structures DateTime
        /// </summary>
        /// <param name="c1">An instance of the TimeOfDay class</param>
        /// <param name="c2">Structure DateTime</param>
        /// <returns></returns>
        public static bool operator >(TimeOfDay c1, DateTime c2)
        {
            if (c1.Hour > c2.Hour)
            {
                return true;
            }

            if (c1.Hour >= c2.Hour
                && c1.Minute > c2.Minute)
            {
                return true;
            }

            if (c1.Hour >= c2.Hour
                && c1.Minute >= c2.Minute
                && c1.Second > c2.Second)
            {
                return true;
            }

            if (c1.Hour >= c2.Hour
                && c1.Minute >= c2.Minute
                && c1.Second >= c2.Second
                && c1.Millisecond > c2.Millisecond)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Operator overloading allows you to compare instances of this class with structures DateTime
        /// </summary>
        /// <param name="c1">An instance of the TimeOfDay class</param>
        /// <param name="c2"></param>
        /// <returns></returns>
        public static bool operator <(TimeOfDay c1, DateTime c2)
        {
            if (c1.Hour < c2.Hour)
            {
                return true;
            }

            if (c1.Hour == c2.Hour
                && c1.Minute < c2.Minute)
            {
                return true;
            }

            if (c1.Hour == c2.Hour
                && c1.Minute == c2.Minute
                && c1.Second < c2.Second)
            {
                return true;
            }

            if (c1.Hour == c2.Hour
                && c1.Minute == c2.Minute
                && c1.Second == c2.Second
                && c1.Millisecond < c2.Millisecond)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Represents the time interval since the beginning of the day
        /// </summary>
        public TimeSpan TimeSpan
        {
            get
            {
                TimeSpan time = new TimeSpan(0, Hour, Minute, Second);

                return time;
            }
        }
    }

    /// <summary>
    /// A strategy parameter to button click
    /// </summary>
    public class StrategyParameterButton : IIStrategyParameter
    {
        /// <summary>
        /// Designer for creating a parameter storing Button type variables
        /// </summary>
        /// <param name="buttonLabel">Button content</param>
        /// <param name="tabName">Owner tab name</param>
        /// <exception cref="Exception">The parameter name of the robot contains a special character. This will cause errors. Take it away</exception>
        public StrategyParameterButton(string buttonLabel, string tabName = null)
        {
            if (buttonLabel.HaveExcessInString())
            {
                throw new Exception("The parameter name of the robot contains a special character. This will cause errors. Take it away");
            }
            _name = buttonLabel;
            _type = StrategyParameterType.Button;
            TabName = tabName;
        }

        /// <summary>
        /// Owner tab name
        /// </summary>
        public string TabName { get; set; }

        /// <summary>
        /// Blank. it is impossible to create a variable of StrategyParameter type with an empty constructor
        /// </summary>
        private StrategyParameterButton()
        {

        }

        /// <summary>
        /// Get formatted string to save to file
        /// </summary>
        public string GetStringToSave()
        {
            string save = _name + "#";

            return save;
        }

        /// <summary>
        /// Load parameter from string
        /// </summary>
        /// <param name="save">line with saved parameters</param>
        public void LoadParamFromString(string[] save)
        {
        }

        /// <summary>
        /// Uniq parameter name
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        private string _name;

        /// <summary>
        /// Parameter type
        /// </summary>
        public StrategyParameterType Type
        {
            get { return _type; }
        }

        private StrategyParameterType _type;

        /// <summary>
        /// Event: parameter state changed
        /// </summary>
        public event Action ValueChange;

        /// <summary>
        /// Trigger a button click event
        /// </summary>
        public void Click()
        {
            UserClickOnButtonEvent?.Invoke();
        }

        /// <summary>
        /// Event: click on button
        /// </summary>
        public event Action UserClickOnButtonEvent;
    }

    /// <summary>
    /// A strategy parameter to check box
    /// </summary>
    public class StrategyParameterCheckBox : IIStrategyParameter
    {
        /// <summary>
        /// Designer for creating a parameter storing CheckBox type variables
        /// </summary>
        /// <param name="checkBoxLabel">Displayed name</param>
        /// <param name="isChecked">Current value</param>
        /// <param name="tabName">Owner tab name</param>
        /// <exception cref="Exception">The parameter name of the robot contains a special character. This will cause errors. Take it away</exception>
        public StrategyParameterCheckBox(string checkBoxLabel, bool isChecked, string tabName = null)
        {

            if (checkBoxLabel.HaveExcessInString())
            {
                throw new Exception("The parameter name of the robot contains a special character. This will cause errors. Take it away");
            }
            _name = checkBoxLabel;
            _type = StrategyParameterType.CheckBox;

            if (isChecked == true)
            {
                _checkState = CheckState.Checked;
            }
            else
            {
                _checkState = CheckState.Unchecked;
            }

            TabName = tabName;
        }

        /// <summary>
        /// Owner tab name
        /// </summary>
        public string TabName { get; set; }

        /// <summary>
        /// Blank. it is impossible to create a variable of StrategyParameter type with an empty constructor
        /// </summary>
        private StrategyParameterCheckBox()
        {

        }

        /// <summary>
        /// Get formatted string to save to file
        /// </summary>
        public string GetStringToSave()
        {
            string save = _name + "#";

            if (_checkState == CheckState.Checked)
            {
                save += "true" + "#";
            }
            else
            {
                save += "false" + "#";
            }

            return save;
        }

        /// <summary>
        /// Load parameter from string
        /// </summary>
        /// <param name="save">line with saved parameters</param>
        public void LoadParamFromString(string[] save)
        {
            _name = save[0];

            try
            {
                if (save[1] == "true")
                {
                    _checkState = CheckState.Checked;
                }
                else
                {
                    _checkState = CheckState.Unchecked;
                }
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Uniq parameter name
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        private string _name;

        /// <summary>
        /// Parameter state
        /// </summary>
        public CheckState CheckState
        {
            get
            {
                return _checkState;
            }
            set
            {
                if (_checkState == value)
                {
                    return;
                }
                _checkState = value;
                if (ValueChange != null)
                {
                    ValueChange();
                }
            }
        }

        private CheckState _checkState;

        /// <summary>
        /// Parameter type
        /// </summary>
        public StrategyParameterType Type
        {
            get { return _type; }
        }

        private StrategyParameterType _type;

        /// <summary>
        /// Event: parameter state changed
        /// </summary>
        public event Action ValueChange;
    }

    /// <summary>
    /// The parameter of the Decimal type strategy with CheckBox
    /// </summary>
    public class StrategyParameterDecimalCheckBox : IIStrategyParameter
    {
        /// <summary>
        /// Designer for creating a parameter storing Decimal type variables
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <param name="value">Default value</param>
        /// <param name="start">First value in optimization</param>
        /// <param name="stop">last value in optimization</param>
        /// <param name="step">Step change in optimization</param>
        /// <param name="isChecked">is it active</param>
        public StrategyParameterDecimalCheckBox(string name, decimal value, decimal start, decimal stop, decimal step, 
            bool isChecked, string tabName = null)
        {
            if (name.HaveExcessInString())
            {
                throw new Exception("The parameter name of the robot contains a special character. This will cause errors. Take it away");
            }
            if (start > stop)
            {
                throw new Exception("The initial value of the parameter cannot be greater than the last");
            }

            _name = name;
            _valueDecimal = value;
            _valueDecimalDefolt = value;
            _valueDecimalStart = start;
            _valueDecimalStop = stop;
            _valueDecimalStep = step;

            if (isChecked == true)
            {
                _checkState = CheckState.Checked;
            }
            else
            {
                _checkState = CheckState.Unchecked;
            }

            _type = StrategyParameterType.DecimalCheckBox;
            TabName = tabName;
        }

        /// <summary>
        /// Blank. it is impossible to create a variable of StrategyParameter type with an empty constructor
        /// </summary>
        private StrategyParameterDecimalCheckBox()
        {

        }

        /// <summary>
        /// Get formatted string to save to file
        /// </summary>
        public string GetStringToSave()
        {
            string save = _name + "#";
            save += _valueDecimal + "#";
            save += _valueDecimalDefolt + "#";
            save += _valueDecimalStart + "#";
            save += _valueDecimalStop + "#";
            save += _valueDecimalStep + "#";

            if (_checkState == CheckState.Checked)
            {
                save += "true" + "#";
            }
            else
            {
                save += "false" + "#";
            }

            return save;
        }

        /// <summary>
        /// Load parameter from string
        /// </summary>
        /// <param name="save">line with saved parameters</param>
        public void LoadParamFromString(string[] save)
        {
            _valueDecimal = save[1].ToDecimal();

            try
            {
                _valueDecimalDefolt = save[2].ToDecimal();
                _valueDecimalStart = save[3].ToDecimal();
                _valueDecimalStop = save[4].ToDecimal();
                _valueDecimalStep = save[5].ToDecimal();

                if (save[6] == "true")
                {
                    _checkState = CheckState.Checked;
                }
                else
                {
                    _checkState = CheckState.Unchecked;
                }
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Uniq parameter name
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        private string _name;

        /// <summary>
        /// Owner tab name
        /// </summary>
        public string TabName
        {
            get; set;
        }

        /// <summary>
        /// Parameter type
        /// </summary>
        public StrategyParameterType Type
        {
            get { return _type; }
        }

        private StrategyParameterType _type;

        /// <summary>
        /// Current value of the Decimal parameter
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

        private decimal _valueDecimal;

        /// <summary>
        /// Default value for the Decimal type
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
        /// Initial value of the Decimal type parameter
        /// </summary>
        public decimal ValueDecimalStart
        {
            get
            {
                return _valueDecimalStart;
            }
        }

        private decimal _valueDecimalStart;

        /// <summary>
        /// The last value of the Decimal type parameter
        /// </summary>
        public decimal ValueDecimalStop
        {
            get
            {
                return _valueDecimalStop;
            }
        }

        private decimal _valueDecimalStop;

        /// <summary>
        /// Incremental step of the Decimal type parameter
        /// </summary>
        public decimal ValueDecimalStep
        {
            get
            {
                return _valueDecimalStep;
            }
        }

        private decimal _valueDecimalStep;

        /// <summary>
        /// CheckBox is it active
        /// </summary>
        public CheckState CheckState
        {
            get
            {
                return _checkState;
            }
            set
            {
                if (_checkState == value)
                {
                    return;
                }
                _checkState = value;
                if (ValueChange != null)
                {
                    ValueChange();
                }
            }
        }

        private CheckState _checkState;

        /// <summary>
        /// Event: parameter state changed
        /// </summary>
        public event Action ValueChange;
    }

    /// <summary>
    /// Parameter type
    /// </summary>
    public enum StrategyParameterType
    {
        /// <summary>
        /// An integer number with the type Int
        /// </summary>
        Int,

        /// <summary>
        /// A floating point number of the decimal type
        /// </summary>
        Decimal,

        /// <summary>
        /// String
        /// </summary>
        String,

        /// <summary>
        /// Boolean value
        /// </summary>
        Bool,

        /// <summary>
        /// Time of day
        /// </summary>
        TimeOfDay,

        /// <summary>
        /// Pressing a button
        /// </summary>
        Button,

        /// <summary>
        /// inscription in the parameters window
        /// </summary>
        Label,

        /// <summary>
        /// checkbox in the parameters window
        /// </summary>
        CheckBox,

        /// <summary>
        /// A floating point number of the decimal type with CheckBox
        /// </summary>
        DecimalCheckBox
		
    }
}