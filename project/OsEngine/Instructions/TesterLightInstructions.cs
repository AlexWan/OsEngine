/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System.Collections.Generic;
using static OsEngine.Language.OsLocalization;

namespace OsEngine.Instructions
{
    public class TesterLightInstructions
    {
        public string AllInstructionsInClassDescription
        {
            get
            {
                OsLocalType currentLanguage = OsLocalization.CurLocalization;

                if (currentLanguage == OsLocalType.Ru)
                {
                    return "Tester Light. Сборник статей";
                }
                else if (currentLanguage == OsLocalType.Eng)
                {
                    return "Tester Light. A collection of posts";
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

                return result;
            }
        }

        public Instruction Link1 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Tester Light в OsEngine",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub28&items%5B%5D=item98"
            }
        };

        public Instruction Link2 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Погрешности в тестировании",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub28&items%5B%5D=item257"
            }
        };

        public Instruction Link3 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Тестер. Хранение данных и настройки бумаг",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub28&items%5B%5D=item335"
            }
        };

        public Instruction Link4 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Тестер. Склеенные фьючерсы. Настройка неторговых периодов",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub28&items%5B%5D=item407"
            }
        };

        public Instruction Link5 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Тестер. Робот для уплаты комиссий брокеру за маржинальную торговлю",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub28&items%5B%5D=item833"
            }
        };

        public Instruction Link6 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Тестер. Робот для уплаты налогов в тестере",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub28&items%5B%5D=item831"
            }
        };

        public Instruction Link7 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "OsData и Тестер. Качаем слепки стаканов и запускаем на них тесты",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub28&items%5B%5D=item287"
            }
        };

        public Instruction Link8 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "OsData и Тестер. Качаем ленту сделок и запускаем на ней тесты",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub28&items%5B%5D=item310"
            }
        };

        public Instruction Link9 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Тестер. Добавляем инструменты для тестирования",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub28&items%5B%5D=item848"
            }
        };
    }
}
