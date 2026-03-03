/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System.Collections.Generic;
using static OsEngine.Language.OsLocalization;

namespace OsEngine.Instructions
{
    public class PolygonInstructions
    {
        public string AllInstructionsInClassDescription
        {
            get
            {
                OsLocalType currentLanguage = OsLocalization.CurLocalization;

                if (currentLanguage == OsLocalType.Ru)
                {
                    return "Валютный арбитраж. Сборник статей";
                }
                else if (currentLanguage == OsLocalType.Eng)
                {
                    return "Polygon. A collection of posts";
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
                result.Add(Link3);
                result.Add(Link4);
                result.Add(Link5);
                result.Add(Link6);
                result.Add(Link7);
                result.Add(Link8);

                return result;
            }
        }

        public Instruction Link1 = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Концепция источников в OsEngine",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub64&items%5B%5D=item311"
            }
        };

        public Instruction Link2 = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Валютный арбитраж. Пользовательские интерфейсы в OsEngine",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub52&items%5B%5D=item264"
            }
        };

        public Instruction Link3 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Теория по классическому валютному арбитражу",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub52&items%5B%5D=item262"
            }
        };

        public Instruction Link4 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Фронтранниг медленных роботов",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub52&items%5B%5D=item263"
            }
        };

        public Instruction Link5 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот для классического валютного арбитража",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub52&items%5B%5D=item266"
            }
        };

        public Instruction Link6 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот для исследования прибыльности после сигнала в валютном арбитраже.",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub52&items%5B%5D=item267"
            }
        };

        public Instruction Link7 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Валютный арбитраж и связанные с ним проблемы.",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub52&items%5B%5D=item268"
            }
        };

        public Instruction Link8 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Исходный код BotTabPoligon",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub52&items%5B%5D=item735"
            }
        };
    }
}
