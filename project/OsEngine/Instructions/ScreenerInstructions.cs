/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System.Collections.Generic;
using static OsEngine.Language.OsLocalization;

namespace OsEngine.Instructions
{
    public class ScreenerInstructions
    {
        public string AllInstructionsInClassDescription
        {
            get
            {
                OsLocalType currentLanguage = OsLocalization.CurLocalization;

                if (currentLanguage == OsLocalType.Ru)
                {
                    return "Скринер. Сборник статей";
                }
                else if (currentLanguage == OsLocalType.Eng)
                {
                    return "Screener. A collection of posts";
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
                result.Add(Link9);
                result.Add(Link10);
                result.Add(Link11);
                result.Add(Link12);
                result.Add(Link13);

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
                Description = "Визуал BotTabScreener",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub64&items%5B%5D=item308"
            }
        };

        public Instruction Link3 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Введение. Робастность и Кросс-тесты",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub99&items%5B%5D=item738"
            }
        };

        public Instruction Link4 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Кросс-тестирование – способ создавать роботов, работающих одинаково хорошо на всех рынках",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub99&items%5B%5D=item739"
            }
        };

        public Instruction Link5 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Качаем данные для тестов скринеров",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub99&items%5B%5D=item740"
            }
        };

        public Instruction Link6 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Скринеры в тестере",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub99&items%5B%5D=item741"
            }
        };

        public Instruction Link7 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Скринеры в оптимизаторе",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub99&items%5B%5D=item743"
            }
        };

        public Instruction Link8 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "BotTabScreener. Концептуально",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub99&items%5B%5D=item742"
            }
        };

        public Instruction Link9 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Самый простой скринер на скользящей средней",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub99&items%5B%5D=item747"
            }
        };

        public Instruction Link10 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Скринер ложного пробоя на PinBar, привязанном к внутридневной волатильности",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub99&items%5B%5D=item751"
            }
        };

        public Instruction Link11 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Скринер на RSI и адаптирующемся ценовом канале",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub99&items%5B%5D=item753"
            }
        };

        public Instruction Link12 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Скринер, анализирующий ленту сделок. PumpDetector",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub99&items%5B%5D=item755"
            }
        };

        public Instruction Link13 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Скринер, анализирующий стакан котировок. PlateDetector",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub99&items%5B%5D=item756"
            }
        };
    }
}
