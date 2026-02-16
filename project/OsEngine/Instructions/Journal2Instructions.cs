/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System.Collections.Generic;
using static OsEngine.Language.OsLocalization;

namespace OsEngine.Instructions
{
    public class Journal2Instructions
    {
        public string AllInstructionsInClassDescription
        {
            get
            {
                OsLocalType currentLanguage = OsLocalization.CurLocalization;

                if (currentLanguage == OsLocalType.Ru)
                {
                    return "Журнал. Сборник статей";
                }
                else if (currentLanguage == OsLocalType.Eng)
                {
                    return "Journal. A collection of posts";
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

                return result;
            }
        }

        public Instruction Link1 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Журнал. Знакомство",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub28&items%5B%5D=item110"
            }
        };

        public Instruction Link2 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Журнал. Типы профитов в журналах OsEngine. P\\L и их различия",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub28&items%5B%5D=item95"
            }
        };

        public Instruction Link3 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Журнал. Ансамблирование объёмов",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub28&items%5B%5D=item299"
            }
        };

        public Instruction Link4 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Журнал. Учёт объёмов торгов",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub28&items%5B%5D=item826"
            }
        };

        public Instruction Link5 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Журнал. Коэффициент Шарпа. Sharpe ratio",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub28&items%5B%5D=item827"
            }
        };

        public Instruction Link6 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Журнал. Профит фактор. Profit Factor",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub28&items%5B%5D=item828"
            }
        };

        public Instruction Link7 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Журнал. Фактор восстановления. Recovery Factor",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub28&items%5B%5D=item829"
            }
        };
    }
}
