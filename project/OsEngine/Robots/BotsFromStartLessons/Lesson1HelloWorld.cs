using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

namespace OsEngine.Robots.BotsFromStartLessons
{
    [Bot("Lesson1HelloWorld")]
    public class Lesson1HelloWorld : BotPanel
    {
        public Lesson1HelloWorld(string name, StartProgram startProgram) : base(name, startProgram)
        {
            StrategyParameterButton button = CreateParameterButton("Hello world button");

            button.UserClickOnButtonEvent += Button_UserClickOnButtonEvent;
        }

        private void Button_UserClickOnButtonEvent()
        {
            SendNewLogMessage("Hello world", Logging.LogMessageType.Error);
        }

        public override string GetNameStrategyType()
        {
            return "Lesson1HelloWorld";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}