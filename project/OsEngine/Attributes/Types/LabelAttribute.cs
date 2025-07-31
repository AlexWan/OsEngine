using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using System;
using System.Drawing;

namespace OsEngine.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public class LabelAttribute : ParameterElementAttribute
    {
        /// <summary> Displays a label parameter in the bot parameters </summary>
        public LabelAttribute(string label, string value, int rowHeight, int textHeight, KnownColor color = default, string name = null, string tabControlName = null)
        {
            if (name == null)
                name = label;

            Data = ToString(label, value, rowHeight, textHeight, color);
            _name = name;
            TabControlName = tabControlName;
        }

        public override void BindToBot(BotPanel bot, AttributeInitializer.AttributeMember member, AttributeInitializer initializer)
        {
            int attributesLength = member.CustomAttributes.Length;
            object parameter = null;
            string[] arguments = Data.Split(Separator);

            int rowHeight = int.Parse(arguments[2]);
            int textHeight = int.Parse(arguments[3]);
            Color color = Color.FromName(arguments[4]);

            parameter = bot.CreateParameterLabel(Name, arguments[0], arguments[1], rowHeight, textHeight, color, TabControlName);

            if (member.Type == typeof(StrategyParameterLabel) && attributesLength == 1)
                member.SetValue(parameter);
        }
    }
}
