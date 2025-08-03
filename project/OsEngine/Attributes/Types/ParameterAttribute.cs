using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using System;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OsEngine.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class ParameterAttribute : ParameterElementAttribute
    {
        /// <summary> Displays a custom type or button parameter in the parameters </summary>
        public ParameterAttribute() { }

        /// <summary> Displays a decimal parameter in the bot parameters </summary>
        public ParameterAttribute(double value, double start, double stop, double step, string name = null, string tabControlName = null)
        {
            Data = ToString(value, start, stop, step);
            _name = name;
            TabControlName = tabControlName;
        }
        /// <summary> Displays a decimal parameter in the bot or indicator parameters </summary>
        public ParameterAttribute(double value, string name = null, string tabControlName = null)
        {
            Data = ToString(value);
            _name = name;
            TabControlName = tabControlName;
        }

        /// <summary> Displays a decimal with CheckBox parameter in the bot parameters  </summary>
        public ParameterAttribute(double value, double start, double stop, double step, bool isChecked, string name = null, string tabControlName = null)
        {
            Data = ToString(value, start, stop, step, isChecked);
            _name = name;
            TabControlName = tabControlName;
        }

        /// <summary> Displays an int parameter in the bot parameters </summary>
        public ParameterAttribute(int value, int start, int stop, int step, string name = null, string tabControlName = null)
        {
            Data = ToString(value, start, stop, step);
            _name = name;
            TabControlName = tabControlName;
        }
        /// <summary> Displays an int parameter in the bot or indicator parameters </summary>
        public ParameterAttribute(int value, string name = null, string tabControlName = null)
        {
            Data = ToString(value);
            _name = name;
            TabControlName = tabControlName;
        }

        /// <summary> Displays a string parameter in the bot or indicator parameters </summary>
        public ParameterAttribute(string value, string name = null, string tabControlName = null)
        {
            Data = value;
            _name = name;
            TabControlName = tabControlName;
        }
        /// <summary> Displays a string parameter in the bot or indicator parameters </summary>
        public ParameterAttribute(string value, string[] collection, string name = null, string tabControlName = null)
        {
            Data = ToString(value, collection);
            _name = name;
            TabControlName = tabControlName;
        }

        /// <summary> Displays a bool or checkbox parameter in the bot or indicator parameters </summary>
        public ParameterAttribute(bool value, string name = null, string tabControlName = null)
        {
            Data = value.ToString();
            _name = name;
            TabControlName = tabControlName;
        }

        /// <summary> Displays a TimeOfDay parameter in the bot parameters from milliseconds. 
        /// Example:
        /// 16 * 60 * 60 * 1000 equals 16:00:00.000 Time of Day</summary>
        public ParameterAttribute(long milliseconds, string name = null, string tabControlName = null)
        {
            TimeSpan timeSpan = TimeSpan.FromMilliseconds(milliseconds);

            Data = ToString(timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds, timeSpan.Milliseconds);
            _name = name;
            TabControlName = tabControlName;
        }
        /// <summary> Displays a TimeOfDay parameter in the bot parameters </summary>
        public ParameterAttribute(uint hours, uint minuts, uint seconds, uint milliseconds, string name = null, string tabControlName = null)
        {
            Data = ToString(hours, minuts, seconds, milliseconds);
            _name = name;
            TabControlName = tabControlName;
        }

        /// <summary> Displays a label parameter in the bot parameters </summary>
        public ParameterAttribute(string label, string value, int rowHeight, int textHeight, KnownColor color = default, string name = null, string tabControlName = null)
        {
            Data = ToString(label, value, rowHeight, textHeight, color);
            _name = name;
            TabControlName = tabControlName;
        }

        public override void BindToIndicator(Aindicator indicator, AttributeInitializer.AttributeMember member, AttributeInitializer initializer)
        {
            Type type = member.Type;
            object parameter = null;
            string[] arguments = Data.Split(Separator, StringSplitOptions.RemoveEmptyEntries);

            if (type == typeof(IndicatorParameterInt))
            {
                int value = int.Parse(arguments[0]);
                parameter = indicator.CreateParameterInt(Name, value);
            }
            else if (type == typeof(IndicatorParameterDecimal))
            {
                decimal value = arguments[0].ToDecimal();
                parameter = indicator.CreateParameterDecimal(Name, value);
            }
            else if (type == typeof(IndicatorParameterString))
            {
                parameter = arguments.Length > 1
                    ? indicator.CreateParameterStringCollection(Name, arguments[0], arguments[1..].ToList())
                    : indicator.CreateParameterString(Name, arguments[0]);
            }
            else if (type == typeof(IndicatorParameterBool))
            {
                bool value = bool.Parse(arguments[0]);
                parameter = indicator.CreateParameterBool(Name, value);
            }

            if (parameter != null)
                member.SetValue(parameter);
        }

        public override void BindToBot(BotPanel bot, AttributeInitializer.AttributeMember member, AttributeInitializer initializer)
        {
            Type type = member.Type;
            object parameter = null;
            string[] arguments = Data.Split(Separator, StringSplitOptions.RemoveEmptyEntries);

            if (type == typeof(StrategyParameterDecimal))
            {
                if (arguments.Length == 1)
                {
                    decimal value = arguments[0].ToDecimal();
                    parameter = bot.CreateParameter(Name, value, value, value, value, TabControlName);
                }
                else
                {
                    decimal value = arguments[0].ToDecimal();
                    decimal start = arguments[1].ToDecimal();
                    decimal stop = arguments[2].ToDecimal();
                    decimal step = arguments[3].ToDecimal();
                    parameter = bot.CreateParameter(Name, value, start, stop, step, TabControlName);
                }
            }
            else if (type == typeof(StrategyParameterInt))
            {
                if (arguments.Length == 1)
                {
                    int value = int.Parse(arguments[0]);
                    parameter = bot.CreateParameter(Name, value, value, value, value, TabControlName);
                }
                else
                {
                    int value = int.Parse(arguments[0]);
                    int start = int.Parse(arguments[1]);
                    int stop = int.Parse(arguments[2]);
                    int step = int.Parse(arguments[3]);
                    parameter = bot.CreateParameter(Name, value, start, stop, step, TabControlName);
                }
            }
            else if (type == typeof(StrategyParameterString))
            {
                parameter = arguments.Length > 1
                    ? bot.CreateParameter(Name, arguments[0], arguments[1..], TabControlName)
                    : bot.CreateParameter(Name, arguments[0], TabControlName);
            }
            else if (type == typeof(StrategyParameterBool))
            {
                parameter = bot.CreateParameter(Name, bool.Parse(arguments[0]), TabControlName);
            }
            else if (type == typeof(StrategyParameterCheckBox))
            {
                parameter = bot.CreateParameterCheckBox(Name, bool.Parse(arguments[0]), TabControlName);
            }
            else if (type == typeof(StrategyParameterDecimalCheckBox))
            {
                if (arguments.Length == 1)
                {
                    decimal value = arguments[0].ToDecimal();
                    parameter = bot.CreateParameterDecimalCheckBox(Name, value, value, value, value, default, TabControlName);
                }
                else
                {
                    decimal value = arguments[0].ToDecimal();
                    decimal start = arguments[1].ToDecimal();
                    decimal stop = arguments[2].ToDecimal();
                    decimal step = arguments[3].ToDecimal();
                    bool isChecked = bool.Parse(arguments[4]);
                    parameter = bot.CreateParameterDecimalCheckBox(Name, value, start, stop, step, isChecked, TabControlName);
                }
            }
            else if (type == typeof(StrategyParameterTimeOfDay))
            {
                int hours = (int)arguments[0].ToDecimal();
                int minutes = (int)arguments[1].ToDecimal();
                int seconds = (int)arguments[2].ToDecimal();
                int milliseconds = (int)arguments[3].ToDecimal();
                parameter = bot.CreateParameterTimeOfDay(Name, hours, minutes, seconds, milliseconds, TabControlName);

            }
            else if (type == typeof(StrategyParameterButton))
            {
                parameter = bot.CreateParameterButton(Name, TabControlName);
            }
            else if (type == typeof(StrategyParameterLabel))
            {
                int rowHeight = int.Parse(arguments[2]);
                int textHeight = int.Parse(arguments[3]);
                Color color = Color.FromName(arguments[4]);
                parameter = bot.CreateParameterLabel(Name, arguments[0], arguments[1], rowHeight, textHeight, color, TabControlName);
            }
            else
            {
                if (member.IsParameter || member.IsMethod)
                    return;

                if (type.IsAbstract || type.IsInterface || type.IsGenericType)
                    throw new ArgumentException($"Parameter attribute cannot be applied to interfaces, generics, or abstract classes: {type}");

                object instance = RuntimeHelpers.GetUninitializedObject(type);
                initializer.InitBotAttribute(type, instance, Name);
                member.SetValue(instance);
            }

            if (parameter != null)
                member.SetValue(parameter);
        }
    }
}
