using OsEngine.Entity;
using OsEngine.Indicators;
using System;
using System.Collections.Generic;

namespace OsEngine.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class ParameterAttribute : ParameterElementAttribute
    {
        public ParameterAttribute() { }

        /// <summary> Displays a decimal parameter in the indicator parameters </summary>
        public ParameterAttribute(double value, string name = null)
        {
            Data = ToString(value);
            _name = name;
        }
        /// <summary> Displays an int parameter in the indicator parameters </summary>
        public ParameterAttribute(int value, string name = null)
        {
            Data = ToString(value);
            _name = name;
        }

        /// <summary> Displays a string parameter in the indicator parameters </summary>
        public ParameterAttribute(string value, string name = null)
        {
            Data = value;
            _name = name;
        }
        /// <summary> Displays a string parameter in the indicator parameters </summary>
        public ParameterAttribute(string value, string[] collection, string name = null)
        {
            Data = ToString(value, collection);
            _name = name;
        }

        /// <summary> Displays a bool parameter in the indicator parameters </summary>
        public ParameterAttribute(bool value, string name = null)
        {
            Data = value.ToString();
            _name = name;
        }

        public override void BindToIndicator(Aindicator indicator, AttributeInitializer.AttributeMember member, AttributeInitializer initializer)
        {
            Type type = member.Type;
            object parameter = null;
            string[] arguments = Data.Split(new char[] { Separator }, options: StringSplitOptions.RemoveEmptyEntries);

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
                if(arguments.Length > 1)
                {
                    List<string> collection = new List<string>();

                    for (int i = 1; i < arguments.Length; i++)
                        collection[i] = arguments[i];

                    indicator.CreateParameterStringCollection(Name, arguments[0], collection);
                }
                else
                {
                    indicator.CreateParameterString(Name, arguments[0]);
                }
            }
            else if (type == typeof(IndicatorParameterBool))
            {
                bool value = bool.Parse(arguments[0]);
                parameter = indicator.CreateParameterBool(Name, value);
            }

            if (parameter != null)
                member.SetValue(parameter);
        }
    }
}
