/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;

namespace OsEngine.Entity
{
    /// <summary>
    /// Position record can not be parsed: structurally broken save string
    /// </summary>
    public class PositionParseException : Exception
    {
        /// <summary>
        /// Number of fields found in the broken record
        /// </summary>
        public int FieldsCount { get; private set; }

        public PositionParseException(int fieldsCount)
            : base("Position record is broken. Fields count: " + fieldsCount)
        {
            FieldsCount = fieldsCount;
        }
    }
}
