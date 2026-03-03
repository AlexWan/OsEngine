/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System.Collections.Generic;
using static OsEngine.Language.OsLocalization;

namespace OsEngine.Instructions
{
    public class PairInstructions
    {
        public string AllInstructionsInClassDescription
        {
            get
            {
                OsLocalType currentLanguage = OsLocalization.CurLocalization;

                if (currentLanguage == OsLocalType.Ru)
                {
                    return "Пары. Сборник статей";
                }
                else if (currentLanguage == OsLocalType.Eng)
                {
                    return "Pair. A collection of posts";
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
                Description = "Визуальные интерфейсы и настройки парного арбитража",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub49&items%5B%5D=item248"
            }
        };

        public Instruction Link3 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "О корреляции. Что это и зачем нужно в торговле",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub49&items%5B%5D=item244"
            }
        };

        public Instruction Link4 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "О стационарности и коинтеграции",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub49&items%5B%5D=item245"
            }
        };

        public Instruction Link5 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот для парного арбитража на основе коинтеграции. PairCointegrationSideTrader\r\n",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub49&items%5B%5D=item686"
            }
        };

        public Instruction Link6 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот для парного арбитража на разрыв. PairCorrelationNegative",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub49&items%5B%5D=item689"
            }
        };

        public Instruction Link7 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот для классического парного стат арбитража. PairCorrelationTrader",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub49&items%5B%5D=item690"
            }
        };

        public Instruction Link8 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Обзор слоя создания роботов для парного трейдинга",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub49&items%5B%5D=item242"
            }
        };

        public Instruction Link9 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Использование корреляции и коинтеграции из общих слоёв создания роботов",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub49&items%5B%5D=item249"
            }
        };

        public Instruction Link10 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "О базовой идее и прибыли в парном арбитраже",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub49&items%5B%5D=item316"
            }
        }; 
    }
}
