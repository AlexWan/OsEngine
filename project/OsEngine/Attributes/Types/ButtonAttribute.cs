using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using System;
using System.Linq;

namespace OsEngine.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class ButtonAttribute : ParameterElementAttribute
    {
        /// <summary> Displays a button parameter in the bot parameters </summary>
        public ButtonAttribute(string name = null, string tabControlName = null)
        {
            _name = name;
            TabControlName = tabControlName;
        }

        public override void BindToBot(BotPanel bot, AttributeInitializer.AttributeMember member, AttributeInitializer initializer)
        {
            ParameterElementAttribute[] attributs = member.CustomAttributes;

            if (attributs.OfType<ParameterAttribute>().Count() > 0)
                return;

            StrategyParameterButton parameter = bot.CreateParameterButton(Name, TabControlName);

            if (member.Type == typeof(StrategyParameterButton))
                member.SetValue(parameter);
            else
                parameter.UserClickOnButtonEvent += member.InvokeIfMethod;
        }
    }
}
