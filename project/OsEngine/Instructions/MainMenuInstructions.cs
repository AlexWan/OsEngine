/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System.Collections.Generic;
using static OsEngine.Language.OsLocalization;

namespace OsEngine.Instructions
{
    public class MainMenuInstructions
    {
        public string AllInstructionsInClassDescription
        {
            get
            {
                OsLocalType currentLanguage = OsLocalization.CurLocalization;

                if (currentLanguage == OsLocalType.Ru)
                {
                    return "Главное меню OsEngine. Сборник статей";
                }
                else if (currentLanguage == OsLocalType.Eng)
                {
                    return "OsEngine Main Menu. Post Collection";
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
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Главное меню OsEngine",
                PostLink = "https://o-s-a.net/posts/glavnoe-menyu-osengine.html"
            }
        };

        public Instruction Link2 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Главное меню OsEngine. Общие настройки",
                PostLink = "https://o-s-a.net/posts/osengine-main-menu-general-settings.html"
            }
        };
    }
}
