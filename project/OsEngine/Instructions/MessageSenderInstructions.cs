/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System.Collections.Generic;
using static OsEngine.Language.OsLocalization;

namespace OsEngine.Instructions
{
    public class MessageSenderInstructions
    {
        public string AllInstructionsInClassDescription
        {
            get
            {
                OsLocalType currentLanguage = OsLocalization.CurLocalization;

                if (currentLanguage == OsLocalType.Ru)
                {
                    return "Сервер рассылки сообщений. Сборник статей";
                }
                else if (currentLanguage == OsLocalType.Eng)
                {
                    return "Message Sender. A collection of posts";
                }

                return "";
            }
        }

        public List<Instruction> AllInstructionsInClass
        {
            get
            {
                List<Instruction> result = new List<Instruction>();

                result.Add(Link1);
                result.Add(Link2);

                return result;
            }
        }

        public Instruction Link1 = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Telegram рассылка из роботов для Os Engine",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub34&items%5B%5D=item331"
            }
        };

        public Instruction Link2 = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Робот для рассылки сообщений в Telegram",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub34&items%5B%5D=item295"
            }
        };
    }
}
