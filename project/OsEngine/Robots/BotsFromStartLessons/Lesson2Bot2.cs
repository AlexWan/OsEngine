/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

/* Description
Robot-example from the course of lectures "C# for algotreader".
This code describes the creation of 5 different parameters for the robot.
1)Mode is an example of the parameter of the mode of operation of the robot. It includes a set of possible string values "On", "Off".
2)SmaLen is an example of the parameter number of candles for building Sma, in which values of type int are used.
3)isUpCandleToEntry is an example parameter that uses true or false values.
4)bollingerDeviation is an example parameter that uses Decimal values.
5)startToTrade is an example of a parameter that uses the time of day values.
 */

namespace OsEngine.Robots.BotsFromStartLessons
{
    [Bot("Lesson2Bot2")]
    public class Lesson2Bot2 : BotPanel
    {   
        
        public Lesson2Bot2(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // 1 creates string parameter

            StrategyParameterString Mode = CreateParameter("Mode", "Off", new[] { "Off", "On" });

            // 2 creates an Int parameter

            StrategyParameterInt smaLen = CreateParameter("Sma len", 15, 1, 20, 1);

            // 3 creates a bool parameter

            StrategyParameterBool isUpCandleToEntry = CreateParameter("Is up candle", true);

            // 4 creates the Decimal parameter

            StrategyParameterDecimal bollingerDeviation = CreateParameter("Bollinger deviation", 1.4m, 1, 2, 0.1m);

            // 5 creates TimeOfDay parameter

            StrategyParameterTimeOfDay startToTrade = CreateParameterTimeOfDay("Start to trade", 11, 00, 00, 00);
            Description = "Robot-example from the course of lectures \"C# for algotreader\"." +
                "This code describes the creation of 5 different parameters for the robot." +
                "1)Mode is an example of the parameter of the mode of operation of the robot. It includes a set of possible string values \"On\", \"Off\"." +
                "2)SmaLen is an example of the parameter number of candles for building Sma, in which values of type int are used." +
                "3)isUpCandleToEntry is an example parameter that uses true or false values." +
                "4)bollingerDeviation is an example parameter that uses Decimal values." +
                "5)startToTrade is an example of a parameter that uses the time of day values.";
        }

        public override string GetNameStrategyType()
        {
            return "Lesson2Bot2";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}