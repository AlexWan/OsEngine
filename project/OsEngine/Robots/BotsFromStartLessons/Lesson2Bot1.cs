/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

/* Description
Robot-example from the course of lectures "C# for algotreader".
In this code 5 buttons are created in the parameters of the robot.
Different buttons are responsible for the interaction with different types of data.
button #1 brings out the error log of which text is line-based.
button #2 outputs 2 error logs with values of type int.
button #3 outputs 2 error logs with values of type Decimal.
button #4 output 2 error logs with Bool values.
button #5 output error log with DateTime value.
 */

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

            Description = OsLocalization.Description.DescriptionLabel6;
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