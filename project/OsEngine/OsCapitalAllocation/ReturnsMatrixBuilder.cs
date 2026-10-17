/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using OsEngine.Entity;

namespace OsEngine.OsCapitalAllocation
{
    /// <summary>
    /// Тип периода агрегации доходности.
    /// </summary>
    public enum ReturnPeriodType
    {
        Week,
        Month
    }

    /// <summary>
    /// Построение матрицы доходностей из позиций ботов.
    /// </summary>
    public static class ReturnsMatrixBuilder
    {
        /// <summary>
        /// Построить матрицу доходностей «боты × периоды» по списку ботов.
        /// Доходность каждого периода — сумма ProfitOperationPercent по позициям.
        /// startTime/endTime ограничивают диапазон периодов (null — без ограничения).
        /// </summary>
        public static ReturnsMatrix Build(List<BotFile> bots, ReturnPeriodType periodType,
            DateTime? startTime = null, DateTime? endTime = null)
        {
            ReturnsMatrix matrix = new ReturnsMatrix();

            int botsCount = bots.Count;

            matrix.BotNames = new string[botsCount];

            List<Dictionary<DateTime, double>> profitsByPeriod = new List<Dictionary<DateTime, double>>(botsCount);

            List<Dictionary<DateTime, double>> profitsAbsByPeriod = new List<Dictionary<DateTime, double>>(botsCount);

            SortedSet<DateTime> allPeriods = new SortedSet<DateTime>();

            for (int i = 0; i < botsCount; i++)
            {
                BotFile bot = bots[i];
                matrix.BotNames[i] = bot.BotName;

                Dictionary<DateTime, double> profit = new Dictionary<DateTime, double>();
                Dictionary<DateTime, double> profitAbs = new Dictionary<DateTime, double>();

                for (int j = 0; j < bot.Positions.Count; j++)
                {
                    Position position = bot.Positions[j];

                    DateTime periodStart = GetPeriodStart(position.TimeCreate, periodType);

                    if (startTime.HasValue && periodStart < startTime.Value)
                    {
                        continue;
                    }

                    if (endTime.HasValue && periodStart > endTime.Value)
                    {
                        continue;
                    }

                    if (profit.ContainsKey(periodStart))
                    {
                        profit[periodStart] += (double)position.ProfitOperationPercent;
                    }
                    else
                    {
                        profit[periodStart] = (double)position.ProfitOperationPercent;
                    }

                    if (profitAbs.ContainsKey(periodStart))
                    {
                        profitAbs[periodStart] += (double)position.ProfitOperationAbs;
                    }
                    else
                    {
                        profitAbs[periodStart] = (double)position.ProfitOperationAbs;
                    }

                    allPeriods.Add(periodStart);
                }

                profitsByPeriod.Add(profit);
                profitsAbsByPeriod.Add(profitAbs);
            }

            matrix.PeriodStarts = new DateTime[allPeriods.Count];
            allPeriods.CopyTo(matrix.PeriodStarts);

            int periodsCount = allPeriods.Count;

            matrix.Returns = new double[botsCount, periodsCount];
            matrix.ReturnsAbs = new double[botsCount, periodsCount];

            for (int i = 0; i < botsCount; i++)
            {
                Dictionary<DateTime, double> profit = profitsByPeriod[i];
                Dictionary<DateTime, double> profitAbs = profitsAbsByPeriod[i];

                for (int j = 0; j < periodsCount; j++)
                {
                    DateTime periodStart = matrix.PeriodStarts[j];

                    if (profit.ContainsKey(periodStart))
                    {
                        matrix.Returns[i, j] = profit[periodStart];
                    }

                    if (profitAbs.ContainsKey(periodStart))
                    {
                        matrix.ReturnsAbs[i, j] = profitAbs[periodStart];
                    }
                }
            }

            return matrix;
        }

        private static DateTime GetPeriodStart(DateTime time, ReturnPeriodType periodType)
        {
            if (periodType == ReturnPeriodType.Month)
            {
                return new DateTime(time.Year, time.Month, 1);
            }

            // неделя начинается с понедельника
            int dayOffset = ((int)time.DayOfWeek + 6) % 7;
            return time.Date.AddDays(-dayOffset);
        }
    }
}
