/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System.Collections.Generic;
using static OsEngine.Language.OsLocalization;

namespace OsEngine.Instructions
{
    public class IndexInstructions
    {
        public string AllInstructionsInClassDescription
        {
            get
            {
                OsLocalType currentLanguage = OsLocalization.CurLocalization;

                if (currentLanguage == OsLocalType.Ru)
                {
                    return "Индекс. Сборник статей";
                }
                else if (currentLanguage == OsLocalType.Eng)
                {
                    return "Index. A collection of posts";
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
                Description = "Индекс в OsEngine. Собираем по своей формуле",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item291"
            }
        };

        public Instruction Link3 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Индекс в OsEngine. Автоформула",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item292"
            }
        };

        public Instruction Link4 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Индексный арбитраж. Введение",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item282"
            }
        };

        public Instruction Link5 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Возможные алгоритмы роботов",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item283"
            }
        };

        public Instruction Link6 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Волатильность. Торговля от индекса",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item285"
            }
        };

        public Instruction Link7 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Корреляция. Торговля от индекса",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item286"
            }
        };

        public Instruction Link8 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Минимальные остатки от разницы двух ценовых рядов с оптимальным мультипликатором",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item289"
            }
        };

        public Instruction Link9 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Выбор бумаг в индекс",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item290"
            }
        };

        public Instruction Link10 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Торговля от индекса. О выравнивании наборов данных",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item314"
            }
        };

        public Instruction Link11 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Данные для межбиржевых алгоритмов",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item315"
            }
        };

        public Instruction Link12 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "BotTabIndex. Обзор кода",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item774"
            }
        };

        public Instruction Link13 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот 1. ArbitrageSimple",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item704"
            }
        };

        public Instruction Link14 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот 2. Индексный одноногий на возврат к среднему",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item722"
            }
        };

        public Instruction Link15 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот 3. Индексный одноногий в тренд",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item730"
            }
        };

        public Instruction Link16 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот 4. Парный межбиржевой от индекса",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item804"
            }
        };

        public Instruction Link17 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот 5. Классический индексный арбитраж",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item731"
            }
        };

        public Instruction Link18 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Взвешивание индекса по цене. Price Weighted Index",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item325"
            }
        };

        public Instruction Link19 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Равновзвешенные индексы. Equal Weighted Index",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item326"
            }
        };

        public Instruction Link20 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Взвешивание индекса по объёму. Volume Weighted Index",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item327"
            }
        };

        public Instruction Link21 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Взвешивание индекса в пару по коинтеграции. Cointegration Weighted Index",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item328"
            }
        };

        public Instruction Link22 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Автоподбор бумаг для собственного индекса. Исторические объёмы и волатильность",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item329"
            }
        };

        public Instruction Link23 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Учёт лотности в Индекс-билдере во время тестирования на MOEX",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item341"
            }
        };

        public Instruction Link24 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Нормирование бумаг индекса в %",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&cats%5B%5D=tab76&subcats%5B%5D=sub55&items%5B%5D=item734"
            }
        };
    }
}
