/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System.Collections.Generic;
using static OsEngine.Language.OsLocalization;

namespace OsEngine.Instructions
{
    public class GridInstructions
    {
        public string AllInstructionsInClassDescription
        {
            get
            {
                OsLocalType currentLanguage = OsLocalization.CurLocalization;

                if (currentLanguage == OsLocalType.Ru)
                {
                    return "Сборник статей по стандартному слою сеток";
                }
                else if (currentLanguage == OsLocalType.Eng)
                {
                    return "A collection of posts on the standard grid layer";
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

                return result;
            }
        }

        public Instruction Link1 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Введение в сеточную торговлю. О чём этот тип торговли",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item754"
            }
        };

        public Instruction Link2 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Все настройки сетки",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item757"
            }
        };

        public Instruction Link3 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Обзор исходного кода стандартной сетки OsEngine",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item758"
            }
        };

        public Instruction Link4 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Режим маркет-мейкинга в настройках сетки. Что это и как настроить",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item762"
            }
        };

        public Instruction Link5 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Режим единой позиции в настройках сетки. Зачем нужен и как настроить",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item763"
            }
        };

        public Instruction Link6 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Трейлинг Ап. Прогрессия. Постепенное смещение сетки в процессе работы",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item764"
            }
        };

        public Instruction Link7 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Сетка - ресурсоёмкий робот. Требования к ПК для запуска",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item765"
            }
        };

        public Instruction Link8 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Тестер и Сетка. Как сделать так чтобы работало?",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item767"
            }
        };

        public Instruction Link9 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Прогрессия между линиями сетки",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item768"
            }
        };

        public Instruction Link10 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот 1. Автосетка по пробою канала линейной регрессии",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item769"
            }
        };

        public Instruction Link11 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот 2. Автосетка по Боллинджеру. Снизу покупаем, сверху продаём",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item771"
            }
        };

        public Instruction Link12 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот 3. Автосетка в обе стороны. По падению волатильности через ATR",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item775"
            }
        };

        public Instruction Link13 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот 4. Пример выбрасывающих две сетки по разным сигналам",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item776"
            }
        };

        public Instruction Link14 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот 5. Скринер, торгующий сетками на множестве бумаг одновременно. По боллинджеру",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item777"
            }
        };

        public Instruction Link15 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот 6. Скринер, торгующий сетками на множестве бумаг одновременно. По ускорению бумаги в тренд",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item778"
            }
        };

        public Instruction Link16 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Робот 7. Сетка для торговли пары бумаг. Друг к другу. Сетки выбрасываются на ускорении бумаг одна к другой",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab88&subcats%5B%5D=sub101&items%5B%5D=item780"
            }
        };

    }
}
