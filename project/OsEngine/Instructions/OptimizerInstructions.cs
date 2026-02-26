/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System.Collections.Generic;
using static OsEngine.Language.OsLocalization;

namespace OsEngine.Instructions
{
    public class OptimizerInstructions
    {
        public string AllInstructionsInClassDescription
        {
            get
            {
                OsLocalType currentLanguage = OsLocalization.CurLocalization;

                if (currentLanguage == OsLocalType.Ru)
                {
                    return "Оптимизатор. Сборник статей";
                }
                else if (currentLanguage == OsLocalType.Eng)
                {
                    return "Optimizer. A collection of posts";
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
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Оптимизатор 1. Простой перебор параметров",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab11&cats%5B%5D=tab88&subcats%5B%5D=sub28&items%5B%5D=item93"
            }
        };

        public Instruction Link2 = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Оптимизатор 2. О робастности",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab11&cats%5B%5D=tab88&subcats%5B%5D=sub28&items%5B%5D=item300"
            }
        };

        public Instruction Link3 = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Оптимизатор 3. Walk-Forwards оптимизация",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab11&cats%5B%5D=tab88&subcats%5B%5D=sub28&items%5B%5D=item301"
            }
        };

        public Instruction Link4 = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Оптимизатор 4. Численный показатель робастности при Walk-Forward оптимизации",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab11&cats%5B%5D=tab88&subcats%5B%5D=sub28&items%5B%5D=item302"
            }
        };

        public Instruction Link5 = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Оптимизатор 5. Ограничения оптимизатора",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab11&cats%5B%5D=tab88&subcats%5B%5D=sub28&items%5B%5D=item303"
            }
        };

        public Instruction Link6 = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Оптимизатор 6. Выгрузка результатов оптимизации в Excel",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab11&cats%5B%5D=tab88&subcats%5B%5D=sub28&items%5B%5D=item800"
            }
        };

        public Instruction Link7 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Добавить готового робота в OsEngine",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub28&items%5B%5D=item849"
            }
        };

    }
}
