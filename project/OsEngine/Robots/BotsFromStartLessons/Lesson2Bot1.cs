using System;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

namespace OsEngine.Robots.BotsFromStartLessons
{
    [Bot("Lesson2Bot1")]
    public class Lesson2Bot1 : BotPanel
    {
        public Lesson2Bot1(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            StrategyParameterButton button1 = CreateParameterButton("Button1. String");
            button1.UserClickOnButtonEvent += Button1_UserClickOnButtonEvent;

            StrategyParameterButton button2 = CreateParameterButton("Button2. Int");
            button2.UserClickOnButtonEvent += Button2_UserClickOnButtonEvent;

            StrategyParameterButton button3 = CreateParameterButton("Button3. Decimal");
            button3.UserClickOnButtonEvent += Button3_UserClickOnButtonEvent;

            StrategyParameterButton button4 = CreateParameterButton("Button4. Bool");
            button4.UserClickOnButtonEvent += Button4_UserClickOnButtonEvent;

            StrategyParameterButton button5 = CreateParameterButton("Button5. DateTime");
            button5.UserClickOnButtonEvent += Button5_UserClickOnButtonEvent;
        }

        private void Button5_UserClickOnButtonEvent()
        {
            DateTime dateTime = DateTime.Now;

            SendNewLogMessage(dateTime.ToString(), Logging.LogMessageType.Error);
        }

        private void Button4_UserClickOnButtonEvent()
        {
            bool valueTrue = true;
            bool valueFalse = false;

            SendNewLogMessage(valueTrue.ToString(), Logging.LogMessageType.Error);
            SendNewLogMessage(valueFalse.ToString(), Logging.LogMessageType.Error);
        }

        private void Button3_UserClickOnButtonEvent()
        {
            decimal num = 0.75m;

            SendNewLogMessage(num.ToString(), Logging.LogMessageType.Error);

            decimal num2 = 0.25m;

            decimal result = num / num2;

            SendNewLogMessage(result.ToString(), Logging.LogMessageType.Error);
        }

        private void Button2_UserClickOnButtonEvent()
        {
            int num = 10;

            SendNewLogMessage(num.ToString(), Logging.LogMessageType.Error);

            int num2 = 3;

            int result = num - num2;

            SendNewLogMessage(result.ToString(), Logging.LogMessageType.Error);
        }

        private void Button1_UserClickOnButtonEvent()
        {
            string str1 = "Hello ";
            string str2 = "World";

            string result = str1 + str2;

            SendNewLogMessage(result, Logging.LogMessageType.Error);
        }

        public override string GetNameStrategyType()
        {
            return "Lesson2Bot1";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}