/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;

namespace OsEngine.Entity
{
    public class ListTableSumm
    {
        public int Summ;
        public TypeValueTableSumm TypeValue;
        public decimal Rate;
    }

    public enum TypeValueTableSumm
    {
        Absolute,
        Percent
    }

    public class ListTablePeriods
    {
        public int Year;
        public decimal Rate;
    }

    public class ChargeInfo
    {
        public DateTime Date { get; set; }

        public string BotName { get; set; }

        public decimal Sum { get; set; }

        public string Comment { get; set; }
    }
}
