using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
/*using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Newtonsoft.Json;
using System.Collections.Generic;
using Telegram.Bot.Types.ReplyMarkups;*/

namespace OsEngine.Robots.HomeWork
{
    [Bot("TelegramBot")]
    public class TelegramBot : BotPanel
    {
        public TelegramBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
        }

        public override string GetNameStrategyType()
        {
            return "TelegramBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
            
        }
    }
}
