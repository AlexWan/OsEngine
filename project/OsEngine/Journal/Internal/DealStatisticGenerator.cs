/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LiteDB;
using OsEngine.Entity;

namespace OsEngine.Journal.Internal
{
    public class PositionStatisticGenerator
    {
        public static List<string> GetStatisticNew(List<Position> positions)
        {
            if (positions == null)
            {
                return null;
            }

            List<Position> positionsNew = positions.FindAll(
                position => position.State != PositionStateType.OpeningFail);

            if (positionsNew.Count == 0)
            {
                return null;
            }

            Position[] deals = positionsNew.ToArray();

            List<string> report = new List<string>();
            /*
                 Чистый П\У
                 Чистый П\У %
                 Количество сделок
                 Среднее время удержания
                 Шарп

                 Сред. П\У по движению
                 Сред. П\У % по движению
                 Сред. П\У на капитал
                 Сред. П\У % на капитал

                 Прибыльных  сделок
                 Прибыльных %
                 Сред. П\У по сделке
                 Сред. П\У % по сделке
                 Сред. П\У на капитал
                 Сред. П\У % на капитал
                 Максимум подряд

                 Убыточных сделок
                 Убыточных  %
                 Сред. П\У по сделке
                 Сред. П\У % по сделке
                 Сред. П\У на капитал
                 Сред. П\У % на капитал
                 Максимум подряд

                 Макс просадка %
                 Размер комиссии
            */


            report.Add(Convert.ToDouble(GetAllProfitInAbsolute(deals, false)).ToString(new CultureInfo("ru-RU"))); //Net profit
            report.Add(Math.Round(GetAllProfitPercent(deals, false), 6).ToString(new CultureInfo("ru-RU")));//Net profti %
            report.Add(GetAllDealsCount(deals).ToString(new CultureInfo("ru-RU")));// Number of transactions
            report.Add(GetAverageTimeOnPoses(deals));
            report.Add(GetSharpRatio(deals, 7).ToString());

            report.Add(Math.Round(GetProfitFactor(deals), 6).ToString(new CultureInfo("ru-RU")));   //Profit Factor
            report.Add(Math.Round(GetRecovery(deals), 6).ToString(new CultureInfo("ru-RU")));   // Recovery
            report.Add("");

            report.Add(Convert.ToDouble(GetMiddleProfitInAbsolute(deals)).ToString(new CultureInfo("ru-RU"))); //average profit in 1 contract
            report.Add(Math.Round(GetMiddleProfitInPercentOneContract(deals), 6).ToString(new CultureInfo("ru-RU"))); //average profit in % 1 contract
            report.Add(Convert.ToDouble(GetMiddleProfitInAbsoluteToDeposit(deals)).ToString(new CultureInfo("ru-RU"))); //average profit
            report.Add(Math.Round(GetMiddleProfitInPercentToDeposit(deals), 6).ToString(new CultureInfo("ru-RU"))); //average profit in %

            report.Add(""); // 11
            report.Add(GetProfitDeal(deals).ToString(new CultureInfo("ru-RU"))); //wining trades/выигрышных сделок
            report.Add(Math.Round(GetProfitDialPercent(deals), 6).ToString(new CultureInfo("ru-RU")));//wining trade in %/выигрышных сделок в %
            //report += Convert.ToDouble(GetAllProfitInProfitInPunkt(deals)).ToString(new CultureInfo("ru-RU")) + "\r\n"; //total profit margins/общий профит выигрышных сделок
            report.Add(Convert.ToDouble(GetAllMiddleProfitInProfitInAbsolute(deals)).ToString(new CultureInfo("ru-RU"))); //Average profit in winning trades/средний профит в выигрышных сделках
            report.Add(Math.Round(GetAllMiddleProfitInProfitInPercent(deals), 6).ToString(new CultureInfo("ru-RU"))); //Average profit as a percentage of winning trades/средний профит в процентах в выигрышных сделках
            report.Add(Convert.ToDouble(GetAllMiddleProfitInProfitInAbsoluteOnDeposit(deals)).ToString(new CultureInfo("ru-RU"))); //Average profit in winning trades/средний профит в выигрышных сделках
            report.Add(Math.Round(GetAllMiddleProfitInProfitInPercentOnDeposit(deals), 6).ToString(new CultureInfo("ru-RU")));//Average profit as a percentage of winning trades/средний профит в процентах в выигрышных сделках
            report.Add(GetMaxProfitSeries(deals).ToString(new CultureInfo("ru-RU"))); //maximum series of winning trades/максимальная серия выигрышных сделок

            report.Add("");
            report.Add(GetLossDial(deals).ToString(new CultureInfo("ru-RU"))); //losing trades/проигрышных сделок
            report.Add(Math.Round(GetLossDialPercent(deals), 6).ToString(new CultureInfo("ru-RU"))); //losing deals in/проигрышных сделок в %
            //report += Convert.ToDouble(GetAllLossInLossInPunkt(deals)).ToString(new CultureInfo("ru-RU")) + "\r\n"; //loss-making total profit/общий профит проигрышных сделок
            report.Add(Convert.ToDouble(GetAllMiddleLossInLossInAbsolute(deals)).ToString(new CultureInfo("ru-RU")));//average profit in losing trades/средний профит в проигрышных сделках
            report.Add(Math.Round(GetAllMiddleLossInLossInPercent(deals), 6).ToString(new CultureInfo("ru-RU")));//Average profit as a percentage in losing trades/средний профит в процентах в проигрышных сделках
            report.Add(Convert.ToDouble(GetAllMiddleLossInLossInAbsoluteOnDeposit(deals)).ToString(new CultureInfo("ru-RU"))); //Average profit in winning trades/средний профит в выигрышных сделках
            report.Add(Math.Round(GetAllMiddleLossInLossInPercentOnDeposit(deals), 6).ToString(new CultureInfo("ru-RU")));//Average profit as a percentage of winning trades/средний профит в процентах в выигрышных сделках
            report.Add(GetMaxLossSeries(deals).ToString(new CultureInfo("ru-RU")));//maximum series of winning trades/максимальная серия выигрышных сделок
            report.Add("");
            report.Add(Math.Round(GetMaxDownPercent(deals), 6).ToString(new CultureInfo("ru-RU"))); //maximum drawdown in percent/максимальная просадка в процентах
            report.Add(Math.Round(GetCommissionAmount(deals), 6).ToString(new CultureInfo("ru-RU"))); //maximum drawdown in percent/максимальная просадка в процентах

            /*report += Math.Round(GetSharp(), 2).ToString(new CultureInfo("ru-RU"));
            */
            return report;
        }

        #region Profit

        public static decimal GetAllProfitInAbsolute(Position[] deals, bool ignoreTax)
        {
            decimal profit = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (ignoreTax == true)
                {
                    if (deals[i].NameBotClass == "TaxPayer"
                     || deals[i].NameBotClass == "PayOfMarginBot")
                    {
                        continue;
                    }
                }

                profit += deals[i].ProfitPortfolioAbs * (deals[i].MultToJournal / 100);
            }

            return Round(profit);
        }

        public static decimal GetAllProfitPercent(Position[] deals, bool ignoreTax)
        {
            if (deals == null || deals.Length == 0)
            {
                return 0;
            }

            decimal start = 0;
            int i = 0;

            while (start == 0)
            {
                if(ignoreTax == true)
                {
                    if (deals[i].NameBotClass == "TaxPayer"
                     || deals[i].NameBotClass == "PayOfMarginBot")
                    {
                        continue;
                    }
                }

                start = deals[i].PortfolioValueOnOpenPosition;

                i++;

                if (i >= deals.Length)
                {
                    break;
                }
            }

            if (start == 0)
            {
                return 0;
            }

            decimal end = start + GetAllProfitInAbsolute(deals, ignoreTax);

            decimal profit = end / start * 100 - 100;
            return profit;
        }

        public static int GetAllDealsCount(Position[] deals)
        {
            if (deals.Length == 0)
            {
                return 0;
            }

            int result = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }

                result++;
            }

            return result;

        }

        public static decimal GetMiddleProfitInPercentOneContract(Position[] deals)
        {
            if (deals.Length == 0)
            {
                return 0;
            }

            decimal profit = 0;

            int divider = deals.Length;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    divider--;
                    continue;
                }

                decimal enter = deals[i].EntryPrice;
                decimal exit = deals[i].ClosePrice;

                if (exit == 0)
                {
                    divider--;
                    continue;
                }

                if (enter == 0) continue;

                decimal value = (exit / enter * 100 - 100);

                if (deals[i].Direction == Side.Buy)
                {
                    profit += value;
                }
                else if (deals[i].Direction == Side.Sell)
                {
                    profit += -(value);
                }
            }

            if (divider <= 0)
            {
                return 0;
            }

            return Round(profit / divider);
        }

        public static decimal GetMiddleProfitInAbsolute(Position[] deals)
        {
            if (deals.Length == 0)
            {
                return 0;
            }
            decimal profit = 0;
            int pointsCount = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }

                decimal curProfit = deals[i].ProfitOperationAbs * (deals[i].MultToJournal / 100);

                profit += curProfit;

                pointsCount++;
            }

            try
            {
                if(pointsCount != 0)
                {
                    return Math.Round(profit / pointsCount, 6);
                }
            }
            catch (Exception)
            {
                // ignore
            }

            return Math.Round(profit, 6);
        }

        private static decimal GetMiddleProfitInAbsoluteToDeposit(Position[] deals)
        {
            if (deals.Length == 0)
            {
                return 0;
            }
            decimal profit = 0;
            int pointsCount = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }
                pointsCount++;
                profit += deals[i].ProfitPortfolioAbs * (deals[i].MultToJournal / 100);
            }

            if(pointsCount != 0)
            {
                return Math.Round(profit / pointsCount, 6);
            }

            return 0;
        }

        private static decimal GetMiddleProfitInPercentToDeposit(Position[] deals)
        {
            if (deals.Length == 0)
            {
                return 0;
            }
            decimal profit = 0;
            int pointsCount = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }

                pointsCount++;
                profit += deals[i].ProfitPortfolioPercent * (deals[i].MultToJournal / 100);
            }

            if(pointsCount != 0)
            {
                return Math.Round(profit / pointsCount, 6);
            }

            return 0;
        }

        public static decimal GetSharpRatio(Position[] deals, decimal riskFreeProfitInYear)
        {
            /*

            Sharpe Ratio = (AHPR - RFR) / SD

            AHPR - усреднённая прибыль в % к портфелю со всех сделок за всё время (Average Holding Period Return (AHPR) as the arithmetic mean of individual trade returns)
            RFR - безрисковая ставка, рассчитанная за всё время которое мог получить инвестор от открытия первой сделки до открытия последней
            SD - стандартное отклонение массива прибылей всех сделок в отдельности

            */

            if (deals == null ||
                deals.Length == 0)
            {
                return 0;
            }

            List<decimal> tradeReturns = new List<decimal>();
          
            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                    || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }
                decimal returnDecimal = deals[i].ProfitPortfolioAbs;
                decimal scaledReturn = returnDecimal * (deals[i].MultToJournal / 100m);
                tradeReturns.Add(returnDecimal);
            }

            decimal ahpr = GetAllProfitPercent(deals, true);

            //decimal ahpr = tradeReturns.Average();

            DateTime timeFirstDeal = DateTime.MaxValue;
            DateTime timeEndDeal = DateTime.MinValue;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                   || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }

                if (deals[i].TimeOpen < timeFirstDeal)
                {
                    timeFirstDeal = deals[i].TimeOpen;
                }
           
                if (deals[i].TimeClose > timeEndDeal)
                {
                    timeEndDeal = deals[i].TimeClose;
                }
            }

            decimal rfr = 0;
            if (timeFirstDeal != DateTime.MaxValue && timeEndDeal != DateTime.MinValue && riskFreeProfitInYear != 0)
            {
                int daysCountInPoses = (int)(timeEndDeal - timeFirstDeal).TotalDays;
                decimal riskFreeProfitInYearDecimal = riskFreeProfitInYear; // Convert to decimal
                rfr = ((decimal)daysCountInPoses / 365) * riskFreeProfitInYearDecimal; // average risk-free return from holding time 
            }

            decimal sd = GetValueStandardDeviation(tradeReturns);

            if (sd == 0)
            {
                return 0;
            }
                
            decimal sharp = (ahpr - rfr) / sd;
            //decimal sharp = (ahpr - rfr) / sd * (decimal)Math.Sqrt(365);

            return Math.Round(sharp, 4);
        }

        private static decimal GetValueStandardDeviation(List<decimal> returns)
        {
            int length = returns.Count;

            if (length < 2)
            {
                return 0;
            }

            decimal mean = returns.Average();
            decimal sumSquaredDifferences = 0;

            foreach (decimal value in returns)
            {
                if (value == 0)
                {
                    continue;
                }

                decimal difference = value - mean;
                sumSquaredDifferences += difference * difference;
            }

            // Sample standard deviation (divide by n-1)
            decimal variance = sumSquaredDifferences / (length);
            decimal sd = (decimal)Math.Sqrt((double)variance) / 100;
            //decimal sd = (decimal)Math.Sqrt((double)variance);

            return Math.Round(sd, 5);
        }

        private static int GetProfitDeal(Position[] deals)
        {
            int profitDeal = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }

                if (deals[i].ProfitOperationPercent > 0)
                {
                    profitDeal++;
                }
            }

            return profitDeal;
        }

        private static decimal GetProfitDialPercent(Position[] deals)
        {
            decimal profitDeal = GetProfitDeal(deals);

            if (profitDeal == 0)
            {
                return profitDeal;
            }

            int allDealsCount = 0;

            for(int i = 0;i < deals.Length;i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }

                allDealsCount++;
            }

            return profitDeal / allDealsCount * 100;

        }

        private static decimal GetAllMiddleProfitInProfitInAbsolute(Position[] deals)
        {
            decimal profit = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }
                if (deals[i].ProfitOperationAbs > 0)
                {
                    profit += deals[i].ProfitOperationAbs * (deals[i].MultToJournal / 100);
                }
            }

            if (profit == 0)
            {
                return profit;
            }

            if (GetProfitDeal(deals) == 0)
            {
                return 0;
            }

            return Math.Round(profit / GetProfitDeal(deals), 6);
        }

        private static decimal GetAllMiddleProfitInProfitInPercent(Position[] deals)
        {
            decimal profit = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }
                if (deals[i].ProfitOperationPercent > 0)
                {
                    profit += deals[i].ProfitOperationPercent * (deals[i].MultToJournal / 100);
                }
            }
            if (profit == 0)
            {
                return profit;
            }

            if (GetProfitDeal(deals) == 0)
            {
                return 0;
            }

            return profit / GetProfitDeal(deals);
        }

        private static decimal GetAllMiddleProfitInProfitInAbsoluteOnDeposit(Position[] deals)
        {
            decimal profit = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }
                if (deals[i].ProfitPortfolioAbs > 0)
                {
                    profit += deals[i].ProfitPortfolioAbs * (deals[i].MultToJournal / 100);
                }
            }

            if (profit == 0)
            {
                return profit;
            }

            if (GetProfitDeal(deals) == 0)
            {
                return 0;
            }

            return Math.Round(profit / GetProfitDeal(deals), 6);
        }

        private static decimal GetAllMiddleProfitInProfitInPercentOnDeposit(Position[] deals)
        {
            decimal profit = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }
                if (deals[i].ProfitPortfolioPercent > 0)
                {
                    profit += deals[i].ProfitPortfolioPercent * (deals[i].MultToJournal / 100);
                }
            }
            if (profit == 0)
            {
                return profit;
            }

            if (GetProfitDeal(deals) == 0)
            {
                return 0;
            }

            return profit / GetProfitDeal(deals);
        }

        private static int GetMaxProfitSeries(Position[] deals)
        {
            int maxSeries = 0;

            int nowSeries = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }
                if (deals[i].ProfitOperationPercent > 0)
                {
                    nowSeries++;

                    if (nowSeries > maxSeries)
                    {
                        maxSeries = nowSeries;
                    }
                }
                else
                {
                    nowSeries = 0;
                }
            }

            return maxSeries;
        }

        #endregion

        #region Loss

        private static int GetLossDial(Position[] deals)
        {
            int lossDeal = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }

                if (deals[i].ProfitOperationPercent <= 0)
                {
                    lossDeal++;
                }
            }

            return lossDeal;
        }

        private static decimal GetLossDialPercent(Position[] deals)
        {
            decimal lossDeal = GetLossDial(deals);

            if (lossDeal == 0)
            {
                return lossDeal;
            }

            int allDeals = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }

                if (deals[i].ProfitOperationPercent <= 0)
                {
                    allDeals++;
                }
            }


            return lossDeal / allDeals * 100;

        }

        private static decimal GetAllMiddleLossInLossInAbsolute(Position[] deals)
        {
            decimal loss = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }

                if (deals[i].ProfitOperationAbs <= 0)
                {
                    loss += deals[i].ProfitOperationAbs * (deals[i].MultToJournal / 100);
                }
            }

            if (loss == 0)
            {
                return loss;
            }

            return Math.Round(loss / GetLossDial(deals), 6);
        }

        private static decimal GetAllMiddleLossInLossInPercent(Position[] deals)
        {
            decimal loss = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }

                if (deals[i].ProfitOperationPercent <= 0)
                {
                    loss += deals[i].ProfitOperationPercent * (deals[i].MultToJournal / 100);
                }
            }
            if (loss == 0)
            {
                return loss;
            }
            return loss / GetLossDial(deals);
        }

        private static decimal GetAllMiddleLossInLossInAbsoluteOnDeposit(Position[] deals)
        {
            decimal loss = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }

                if (deals[i].ProfitPortfolioAbs <= 0)
                {
                    loss += deals[i].ProfitPortfolioAbs * (deals[i].MultToJournal / 100);
                }
            }

            if (loss == 0)
            {
                return loss;
            }

            decimal lossDeals = GetLossDial(deals);
            if (lossDeals == 0)
            {
                return lossDeals;
            }

            return Math.Round(loss / lossDeals, 6);
        }

        private static decimal GetAllMiddleLossInLossInPercentOnDeposit(Position[] deals)
        {
            decimal loss = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }

                if (deals[i].ProfitPortfolioPercent <= 0)
                {
                    loss += deals[i].ProfitPortfolioPercent * (deals[i].MultToJournal / 100);
                }
            }
            if (loss == 0)
            {
                return loss;
            }

            int lossDeals = GetLossDial(deals);

            if (lossDeals == 0)
            {
                return 0;
            }

            return loss / lossDeals;
        }

        private static int GetMaxLossSeries(Position[] deals)
        {
            int maxSeries = 0;

            int nowSeries = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                } 

                if (deals[i].ProfitOperationPercent <= 0)
                {
                    nowSeries++;

                    if (nowSeries > maxSeries)
                    {
                        maxSeries = nowSeries;
                    }
                }
                else
                {
                    nowSeries = 0;
                }
            }

            return maxSeries;
        }

        #endregion

        #region Common

        public static string GetAverageTimeOnPoses(Position[] deals)
        {
            string result = "";

            TimeSpan allTime = new TimeSpan();
            int dealsCount = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                    || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }

                DateTime openTime = deals[i].TimeOpen;
                DateTime closeTime = deals[i].TimeClose;

                if (closeTime == DateTime.MinValue)
                {
                    continue;
                }

                dealsCount++;

                allTime += closeTime - openTime;
            }

            if (dealsCount == 0)
            {
                result = "0";
            }
            else
            {
                long seconds = Convert.ToInt64(allTime.Ticks / dealsCount);
                allTime = new TimeSpan(seconds);

                result =
                    "H: " + Convert.ToInt32(allTime.TotalHours)
                    + " M: " + Convert.ToInt32(allTime.Minutes)
                    + " S: " + Convert.ToInt32(allTime.Seconds);
            }

            return result;
        }

        public static decimal GetMaxDownPercent(Position[] deals)
        {
            decimal maxDownAbs = decimal.MaxValue;
            decimal maxDownPercent = decimal.MaxValue;

            if (GetProfitDeal(deals) == 0)
            {
                return 0;
            }
            decimal firsValue = deals[0].PortfolioValueOnOpenPosition;

            for (int i = 0; i < deals.Length; i++)
            {
                if (firsValue != 0)
                {
                    break;
                }
                firsValue = deals[i].PortfolioValueOnOpenPosition;
            }

            if (firsValue == 0)
            {
                firsValue = 1;
            }

            decimal thisSum = firsValue;
            decimal thisPik = firsValue;

            for (int i = 0; i < deals.Length; i++)
            {
                thisSum += deals[i].ProfitPortfolioAbs * (deals[i].MultToJournal / 100);

                decimal thisDown;

                if (thisSum > thisPik)
                {
                    thisPik = thisSum;
                }
                else
                {
                    if (thisSum < 0)
                    {
                        // уже ушли ниже нулевой отметки по счёту

                        thisDown = -thisPik + thisSum;

                        if (maxDownAbs > thisDown)
                        {
                            maxDownAbs = thisDown;
                            decimal curDownPersent = maxDownAbs / (thisPik / 100);

                            if (maxDownPercent > curDownPersent)
                            {
                                maxDownPercent = curDownPersent;
                            }
                        }
                    }
                    else if (thisSum > 0)
                    {
                        // выше нулевой отметки по счёту
                        thisDown = -(thisPik - thisSum);

                        if (maxDownAbs > thisDown)
                        {
                            maxDownAbs = thisDown;
                            decimal curDownPersent = maxDownAbs / (thisPik / 100);

                            if (maxDownPercent > curDownPersent)
                            {
                                maxDownPercent = curDownPersent;
                            }
                        }

                    }
                }
            }

            if (maxDownPercent == decimal.MaxValue)
            {
                return 0;
            }

            return Round(maxDownPercent);
        }

        public static decimal GetCommissionAmount(Position[] deals)
        {
            if (deals.Length == 0)
            {
                return 0;
            }

            decimal commissionTotal = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }

                commissionTotal += deals[i].CommissionTotal() * (deals[i].MultToJournal / 100);
            }

            return Round(commissionTotal);
        }

        public static decimal GetProfitFactor(Position[] deals)
        {
            decimal commonProfitPunkt = 0m;
            decimal commonLossPunkt = 0m;
            decimal profitFactor = 0m;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                   || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }

                if (deals[i].ProfitOperationAbs < 0)
                {
                    commonLossPunkt = commonLossPunkt + deals[i].ProfitOperationAbs * (deals[i].MultToJournal / 100);
                }
                else
                {
                    commonProfitPunkt = commonProfitPunkt + deals[i].ProfitOperationAbs * (deals[i].MultToJournal / 100);
                }
            }

            if (commonLossPunkt != 0 && commonProfitPunkt != 0) profitFactor = Math.Abs(commonProfitPunkt / commonLossPunkt);

            return Round(profitFactor);
        }

        public static decimal GetPayOffRatio(Position[] deals)
        {
            decimal allProfit = 0;
            decimal allLoss = 0;

            int profitPos = 0;
            int lossPos = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }

                if (deals[i].ProfitOperationAbs > 0)
                {
                    allProfit += deals[i].ProfitOperationAbs * (deals[i].MultToJournal / 100);
                    profitPos++;
                }
                else
                {
                    allLoss += deals[i].ProfitOperationAbs * (deals[i].MultToJournal / 100);
                    lossPos++;
                }
            }

            if (profitPos == 0
                || lossPos == 0)
            {
                return 0;
            }

            // средняя прибыль разделить на средний убыток)
            if (allLoss != 0)
            {
                return Math.Abs(allProfit / profitPos) / Math.Abs(allLoss / lossPos);
            }

            return 0;
        }

        public static decimal GetRecovery(Position[] deals)
        {
            decimal recovery = 0m;
            decimal maxDownAbs = decimal.MaxValue;

            if (GetProfitDeal(deals) == 0)
            {
                return 0;
            }
            decimal firsValue = deals[0].PortfolioValueOnOpenPosition;

            for (int i = 0; i < deals.Length; i++)
            {
                if (firsValue != 0)
                {
                    break;
                }
                firsValue = deals[i].PortfolioValueOnOpenPosition;
            }

            if (firsValue == 0)
            {
                firsValue = 1;
            }

            decimal thisSum = firsValue;
            decimal thisPik = firsValue;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].NameBotClass == "TaxPayer"
                 || deals[i].NameBotClass == "PayOfMarginBot")
                {
                    continue;
                }

                thisSum += deals[i].ProfitPortfolioAbs * (deals[i].MultToJournal / 100);

                decimal thisDown;

                if (thisSum > thisPik)
                {
                    thisPik = thisSum;
                }
                else
                {
                    if (thisSum < 0)
                    {
                        // уже ушли ниже нулевой отметки по счёту

                        thisDown = -thisPik + thisSum;

                        if (maxDownAbs > thisDown)
                        {
                            maxDownAbs = thisDown;
                        }
                    }
                    else if (thisSum > 0)
                    {
                        // выше нулевой отметки по счёту
                        thisDown = -(thisPik - thisSum);

                        if (maxDownAbs > thisDown)
                        {
                            maxDownAbs = thisDown;
                        }
                    }
                }
            }

            decimal profit = GetAllProfitInAbsolute(deals, true);
            if (profit != 0 && maxDownAbs != 0) recovery = profit / Math.Abs(maxDownAbs);

            return Round(recovery);
        }

        public static List<Position> SortByTime(List<Position> positionsAll)
        {
            positionsAll = positionsAll.OrderBy(x => x.TimeOpen).ToList();

            return positionsAll;
        }

        private static decimal Round(decimal number)
        {
            return Decimal.Round(number, 6);
        }

        #endregion
    }
}