/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;

namespace OsEngine.OsCapitalAllocation
{
    /// <summary>
    /// Матрица доходностей ботов по периодам.
    /// Строки — боты, колонки — периоды. Значения — доходность за период в процентах.
    /// Пропущенные у бота периоды заполнены нулями.
    /// </summary>
    public class ReturnsMatrix
    {
        /// <summary>
        /// Имена ботов (строки матрицы).
        /// </summary>
        public string[] BotNames;

        /// <summary>
        /// Начала периодов (колонки матрицы), отсортированы по возрастанию.
        /// </summary>
        public DateTime[] PeriodStarts;

        /// <summary>
        /// Матрица доходностей [бот, период].
        /// </summary>
        public double[,] Returns;

        /// <summary>
        /// Матрица прибыли в абсолютных единицах [бот, период]. Для графика эквити.
        /// </summary>
        public double[,] ReturnsAbs;

        /// <summary>
        /// Количество ботов.
        /// </summary>
        public int BotsCount
        {
            get { return BotNames.Length; }
        }

        /// <summary>
        /// Количество периодов.
        /// </summary>
        public int PeriodsCount
        {
            get { return PeriodStarts.Length; }
        }

        /// <summary>
        /// Ряд доходностей одного бота.
        /// </summary>
        public double[] GetBotReturns(int botIndex)
        {
            double[] result = new double[PeriodsCount];

            for (int i = 0; i < PeriodsCount; i++)
            {
                result[i] = Returns[botIndex, i];
            }

            return result;
        }
    }
}
