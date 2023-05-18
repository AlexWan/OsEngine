using System;

namespace OsEngine.Indicators
{
    /// <summary>
    /// Attribute for applying indicators to terminal
    /// </summary>
    [AttributeUsage(System.AttributeTargets.Class)]
    internal class IndicatorAttribute : Attribute
    {
        public string Name { get; }

        public IndicatorAttribute(string name)
        {
            Name = name;
        }
    }
}