/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System.Collections.Generic;
using static OsEngine.Language.OsLocalization;

namespace OsEngine.Instructions
{
    public class BotStationLightInstructions
    {
        public string AllInstructionsInClassDescription
        {
            get
            {
                OsLocalType currentLanguage = OsLocalization.CurLocalization;

                if (currentLanguage == OsLocalType.Ru)
                {
                    return "Роботы Light. Сборник статей";
                }
                else if (currentLanguage == OsLocalType.Eng)
                {
                    return "Bot Station Light. A collection of posts";
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
                result.Add(Link14);
                result.Add(Link15);
                result.Add(Link16);
                result.Add(Link17);
                result.Add(Link18);
                result.Add(Link19);
                result.Add(Link20);
                result.Add(Link21);
                result.Add(Link22);
                result.Add(Link23);
                result.Add(Link24);
                result.Add(Link25);
                result.Add(Link26);

                result.Add(Link28);

                return result;
            }
        }

        public Instruction Link1 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Роботы Light в OsEngine",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub28&items%5B%5D=item96"
            }
        };

        public Instruction Link2 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Риск менеджер",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub28&items%5B%5D=item381"
            }
        };

        public Instruction Link3 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Коннекторы к MOEX",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub65"
            }
        };

        public Instruction Link4 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Коннекторы к криптобиржам",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub17"
            }
        };

        public Instruction Link5 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Международные подключения",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub66"
            }
        };

        public Instruction Link6 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "MOEX FixFast Spot/Currency",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub69"
            }
        };

        public Instruction Link7 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Коннекторы для подключения новостного потока.",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub72"
            }
        };

        public Instruction Link8 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "MOEX PLAZA II",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub70"
            }
        };

        public Instruction Link9 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "MoexFixFastTwimeFuture",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub71"
            }
        };

        public Instruction Link10 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Удаленные серверы",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub32"
            }
        };

        public Instruction Link11 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Торговля",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub34"
            }
        };

        public Instruction Link12 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Мультиконнект",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub75"
            }
        };

        public Instruction Link13 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Копитрейдинг",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub104"
            }
        };

        public Instruction Link14 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Курс лекций «Введение в алготрейдинг»",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub89"
            }
        };

        public Instruction Link15 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Курс лекций «Тренд через Walk-Forwards»",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub90"
            }
        };

        public Instruction Link16 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Курс лекций «Ротация бумаг между алгоритмами по стадиям волатильности»",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub91"
            }
        };

        public Instruction Link17 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Свечи и преобразование ленты сделок",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub56"
            }
        };

        public Instruction Link18 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Курс лекций «Свечи и преобразование ленты сделок»",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub97"
            }
        };

        public Instruction Link19 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Парный арбитраж",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub49"
            }
        };

        public Instruction Link20 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Индексный арбитраж",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55"
            }
        };

        public Instruction Link21 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Валютный арбитраж",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub52"
            }
        };

        public Instruction Link22 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Скринеры",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub99"
            }
        };

        public Instruction Link23 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Курс лекций «Кросс-тестирование через Скринеры. Роботы для всех рынков»",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub100"
            }
        };

        public Instruction Link24 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Сеточные роботы",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub101"
            }
        };

        public Instruction Link25 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Открытый интерес",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub102"
            }
        };

        public Instruction Link26 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Алго по новостям",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub103"
            }
        };

        public Instruction Link27 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Опицоны",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub116"
            }
        };

        public Instruction Link28 = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Модуль просмотра нагрузки на систему",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab76&subcats%5B%5D=sub34&items%5B%5D=item451"
            }
        };
    }
}
