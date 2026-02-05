/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System.Collections.Generic;
using static OsEngine.Language.OsLocalization;

namespace OsEngine.Instructions
{
    public class OsDataInstructions
    {
        public string AllInstructionsInClassDescription
        {
            get
            {
                OsLocalType currentLanguage = OsLocalization.CurLocalization;

                if (currentLanguage == OsLocalType.Ru)
                {
                    return "Os Data. Сборник статей";
                }
                else if (currentLanguage == OsLocalType.Eng)
                {
                    return "Os Data. A collection of posts";
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
                Description = "Os Data. Общая инструкция",
                PostLink = "https://o-s-a.net/posts/os-engine-os-data-2-0.html"
            }
        };

        public Instruction Link2 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Os Data. Лента сделок. Стаканы",
                PostLink = "https://o-s-a.net/posts/os-data-lenta-sdelok-stakany.html"
            }
        };

        public Instruction Link3 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "OsData и Тестер. Качаем слепки стаканов и запускаем на них тесты",
                PostLink = "https://o-s-a.net/posts/osdata-tester-marketdepth.html"
            }
        };

        public Instruction Link4 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "OsData и Тестер. Качаем ленту сделок и запускаем на ней тесты",
                PostLink = "https://o-s-a.net/posts/we-pump-transaction-tape-run-tests.html"
            }
        };

        public Instruction Link5 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "OsData – обрезаем лишние ценовые ряды по фильтрам",
                PostLink = "https://o-s-a.net/posts/osdata-trim-unnecessary-price-series-by-filters.html"
            }
        };

        public Instruction Link6 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "OsData — автоматическое обновление сета данных по таймеру",
                PostLink = "https://o-s-a.net/posts/osdata-automatic-update-of-the-data-set-by-timer.html"
            }
        };

        public Instruction Link7 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "OsData – Генерация данных денежных фондов TMON/LQDT (для MOEX, NYSE) для дальнейших тестов",
                PostLink = "https://o-s-a.net/posts/osdata-generates-tmon-lqdt-for-moex-nyse-cash-fund-data-for-further-testing.html"
            }
        };

        public Instruction Link8 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "OsData – дублируем данные сета в другую папку",
                PostLink = "https://o-s-a.net/posts/osdata-duplicate-dataset-to-another-folder.html"
            }
        };

        public Instruction Link9 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "OsData. Создание сета данных",
                PostLink = "https://o-s-a.net/posts/creating-data-set.html"
            }
        };
    }
}
