using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
/* Description
Robot-example from the course of lectures "C# for algotreader".
This script creates a button in the parameters of the robot. When you press the button, the Log error with the text "Hello world!"
 */
namespace OsEngine.Robots.BotsFromStartLessons
{
    [Bot("Lesson1HelloWorld")]
    public class Lesson1HelloWorld : BotPanel
    {

        public Lesson1HelloWorld(string name, StartProgram startProgram) : base(name, startProgram)
        {
            StrategyParameterButton button = CreateParameterButton("Hello world button");

            button.UserClickOnButtonEvent += Button_UserClickOnButtonEvent;
            Description = "Robot-example from the course of lectures C# for algotreader." +
                "This script creates a button in the parameters of the robot." +
                "When you press the button, the Log error with the text \"Hello world!\"";
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