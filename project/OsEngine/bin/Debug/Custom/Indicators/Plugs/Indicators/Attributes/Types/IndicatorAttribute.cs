using System;

namespace OsEngine.Indicators
{
    /// <summary>
    /// Attribute for applying indicators to terminal
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class IndicatorAttribute : Attribute
    {
        public string Name { get; }

        public IndicatorAttribute(string name)
        {
            Name = name;
        }
    }
}