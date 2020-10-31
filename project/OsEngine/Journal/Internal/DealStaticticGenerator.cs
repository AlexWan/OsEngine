/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using OsEngine.Entity;

namespace OsEngine.Journal.Internal
{
    /// <summary>
    /// class that calculates transaction statistics
    /// класс, рассчитывающий статистику по сделкам
    /// </summary>
    public class PositionStaticticGenerator
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="positions"></param>
        /// <param name="withPunkt">If data from several tabs or several robots, then you need to write false/если данные из нескольких вкладок или нескольких роботов, то нужно писать false</param>
        public static List<string> GetStatisticNew(List<Position> positions, bool withPunkt)
        {
            if (positions == null)
            {
                return null;
            }

            List<Position> positionsNew = positions.FindAll((
                position => position.State != PositionStateType.OpeningFail 
                            && position.EntryPrice != 0 && position.ClosePrice != 0));

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

                 Сред. П\У по сделке
                 Сред. П\У % по сделке
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
            */
            /*Net Profit/Loss
                Net Profit/Loss
                Number of transactions

                Average. Profit/Loss on the deal
                Average. Profit/Loss % of the transaction
                Average. Profit/Loss for capital
                Average. Profit/Loss % for capital

                Profitable deals
                Profitable %
                Average. Profit/Loss on the deal
                Average. Profit/Loss % of the transaction
                Average. Profit/Loss for capital
                Average. Profit/Loss % for capital
                At most in a row.

                Loss transactions
                Losses.
                Average. Profit/Loss on the deal
                Average. Profit/Loss % of the transaction
                Average. Profit/Loss for capital
                Average. Profit/Loss % for capital
                At most in a row.

                Max drawdown %
            */


            report.Add(Convert.ToDouble(GetAllProfitInPunkt(deals)).ToString(new CultureInfo("ru-RU"))); //Net profit
            report.Add(Math.Round(GetAllProfitPersent(deals), 6).ToString(new CultureInfo("ru-RU")));//Net profti %
            report.Add(deals.Length.ToString(new CultureInfo("ru-RU")));// Number of transactions
            report.Add(Math.Round(GetProfitFactor(deals), 6).ToString(new CultureInfo("ru-RU")));   //Profit Factor
            report.Add(Math.Round(GetRecovery(deals), 6).ToString(new CultureInfo("ru-RU")));   // Recovery
            report.Add("");

            report.Add(Convert.ToDouble(GetMidleProfitInPunkt(deals)).ToString(new CultureInfo("ru-RU"))); //average profit
            report.Add(Math.Round(GetMidleProfitInPersent(deals), 6).ToString(new CultureInfo("ru-RU"))); //average profit in %
            report.Add(Convert.ToDouble(GetMidleProfitInPunktToDepozit(deals)).ToString(new CultureInfo("ru-RU"))); //average profit
            report.Add(Math.Round(GetMidleProfitInPersentToDepozit(deals), 6).ToString(new CultureInfo("ru-RU"))); //average profit in %

            report.Add(""); // 11
            report.Add(GetProfitDial(deals).ToString(new CultureInfo("ru-RU"))); //wining trades/выигрышных сделок
            report.Add(Math.Round(GetProfitDialPersent(deals), 6).ToString(new CultureInfo("ru-RU")));//wining trade in %/выигрышных сделок в %
            //report += Convert.ToDouble(GetAllProfitInProfitInPunkt(deals)).ToString(new CultureInfo("ru-RU")) + "\r\n"; //total profit margins/общий профит выигрышных сделок
            report.Add(Convert.ToDouble(GetAllMidleProfitInProfitInPunkt(deals)).ToString(new CultureInfo("ru-RU"))); //Average profit in winning trades/средний профит в выигрышных сделках
            report.Add(Math.Round(GetAllMidleProfitInProfitInPersent(deals), 6).ToString(new CultureInfo("ru-RU"))); //Average profit as a percentage of winning trades/средний профит в процентах в выигрышных сделках
            report.Add(Convert.ToDouble(GetAllMidleProfitInProfitInPunktOnDepozit(deals)).ToString(new CultureInfo("ru-RU"))); //Average profit in winning trades/средний профит в выигрышных сделках
            report.Add(Math.Round(GetAllMidleProfitInProfitInPersentOnDepozit(deals), 6).ToString(new CultureInfo("ru-RU")));//Average profit as a percentage of winning trades/средний профит в процентах в выигрышных сделках
            report.Add(GetMaxProfitSeries(deals).ToString(new CultureInfo("ru-RU"))); //maximum series of winning trades/максимальная серия выигрышных сделок

            report.Add("");
            report.Add(GetLossDial(deals).ToString(new CultureInfo("ru-RU"))); //losing trades/проигрышных сделок
            report.Add(Math.Round(GetLossDialPersent(deals), 6).ToString(new CultureInfo("ru-RU"))); //losing deals in/проигрышных сделок в %
            //report += Convert.ToDouble(GetAllLossInLossInPunkt(deals)).ToString(new CultureInfo("ru-RU")) + "\r\n"; //loss-making total profit/общий профит проигрышных сделок
            report.Add(Convert.ToDouble(GetAllMidleLossInLossInPunkt(deals)).ToString(new CultureInfo("ru-RU")));//average profit in losing trades/средний профит в проигрышных сделках
            report.Add(Math.Round(GetAllMidleLossInLossInPersent(deals), 6).ToString(new CultureInfo("ru-RU")));//Average profit as a percentage in losing trades/средний профит в процентах в проигрышных сделках
            report.Add(Convert.ToDouble(GetAllMidleLossInLossInPunktOnDepozit(deals)).ToString(new CultureInfo("ru-RU"))); //Average profit in winning trades/средний профит в выигрышных сделках
            report.Add(Math.Round(GetAllMidleLossInLossInPersentOnDepozit(deals), 6).ToString(new CultureInfo("ru-RU")));//Average profit as a percentage of winning trades/средний профит в процентах в выигрышных сделках
            report.Add(GetMaxLossSeries(deals).ToString(new CultureInfo("ru-RU")));//maximum series of winning trades/максимальная серия выигрышных сделок
            report.Add("");
            report.Add(Math.Round(GetMaxDownPersent(deals), 6).ToString(new CultureInfo("ru-RU"))); //maximum drawdown in percent/максимальная просадка в процентах

            /*report += Math.Round(GetSharp(), 2).ToString(new CultureInfo("ru-RU"));
            */
            return report;
        }

        /// <summary>
        /// to take profits in points to deposit
        /// взять профит в пунктах к депозиту
        /// </summary>
        public static decimal GetAllProfitInPunkt(Position[] deals)
        {
            decimal profit = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                profit += deals[i].ProfitPortfolioPunkt;
            }

            return Round(profit);
        }

        /// <summary>
        /// take a profit as a percentage of the deposit
        /// взять профит в процентах к депозиту
        /// </summary>
        public static decimal GetAllProfitPersent(Position[] deals) 
        {
            if (deals == null || deals.Length == 0)
            {
                return 0;
            }

            decimal start = 0;
            int i = 0;
            while (start  == 0)
            {
                start = deals[i ].PortfolioValueOnOpenPosition;
                i ++;
                if(i >= deals.Length)
                    break;
            }

            if (start == 0)
            {
                return 0;
            }

            decimal end = start + GetAllProfitInPunkt(deals);

            decimal profit = end / start * 100 - 100;
            return profit;
        }

        /// <summary>
        /// to take the average profit from the deal as a percentage
        /// взять средний профит со сделки в процентах
        /// </summary>
        public static decimal GetMidleProfitInPersent(Position[] deals) 
        {
            if (deals.Length == 0)
            {
                return 0;
            }

            decimal profit = 0;
            
            for (int i = 0; i < deals.Length; i++)
            {
                decimal enter = deals[i].EntryPrice;
                decimal exit = deals[i].ClosePrice;

                if (enter == 0) continue;

                if (deals[i].Direction == Side.Buy)
                {
                    profit += exit / enter * 100 - 100;
                }
                else if (deals[i].Direction == Side.Sell)
                {
                    profit += -(exit / enter * 100 - 100);
                }
            }

            return Round(profit / deals.Length);
        }

        /// <summary>
        /// take the average profit from the transaction in points
        /// взять средний профит со сделки в пунктах
        /// </summary>
        public static decimal GetMidleProfitInPunkt(Position[] deals)
        {
            if(deals.Length == 0)
            {
                return 0;
            }
            decimal profit = 0;
            decimal someProfit = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                profit += deals[i].ProfitOperationPunkt;

                if (deals[i].ProfitOperationPunkt != 0)
                {
                    someProfit = deals[i].ProfitOperationPunkt;
                }
            }

            if (Convert.ToInt32(someProfit) == someProfit)
            {
                return Math.Round(profit / deals.Length, 2);
            }

            try
            {
                    return Math.Round(profit / deals.Length, 6);
            }
            catch (Exception)
            {
                return Math.Round(profit, 6);
            }
        }

        /// <summary>
        /// Take the average profit from the transaction as a percentage of the deposit
        /// взять средний профит со сделки в процентах к депозиту
        /// </summary>
        private static decimal GetMidleProfitInPersentToDepozit(Position[] deals)
        {
            if (deals.Length == 0)
            {
                return 0;
            }
            decimal profit = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                profit += deals[i].ProfitPortfolioPersent;
            }

            return Math.Round(profit / deals.Length, 6);
        }

        /// <summary>
        /// to take the average profit from the deal to the deposit
        /// взять средний профит со сделки к депозиту
        /// </summary>
        private static decimal GetMidleProfitInPunktToDepozit(Position[] deals)
        {
            if (deals.Length == 0)
            {
                return 0;
            }
            decimal profit = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                profit += deals[i].ProfitPortfolioPunkt;
            }

            return Math.Round(profit / deals.Length, 6);
        }

        // Profits
        // профиты

        /// <summary>
        /// take the number of profitable transactions
        /// взять кол-во прибыльных сделок
        /// </summary>
        private static int GetProfitDial(Position[] deals)
        {
            int profitDeal = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].ProfitOperationPersent > 0)
                {
                    profitDeal++;
                }
            }

            return profitDeal;
        }

        /// <summary>
        /// take% of profitable trades
        /// взять % прибыльных сделок
        /// </summary>
        private static decimal GetProfitDialPersent(Position[] deals)
        {
            decimal profitDeal = GetProfitDial(deals);

            if (profitDeal == 0)
            {
                return profitDeal;
            }

            return profitDeal / deals.Length * 100;

        }

        /// <summary>
        /// take the average profit in points from profitable transactions
        /// взять средний профит в пунктах у прибыльных сделок
        /// </summary>
        private static decimal GetAllMidleProfitInProfitInPunkt(Position[] deals)
        {
            decimal profit = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].ProfitOperationPunkt > 0)
                {
                    profit += deals[i].ProfitOperationPunkt;
                }
            }

            if(profit == 0)
            {
                return profit;
            }

            if (GetProfitDial(deals) == 0)
            {
                return 0;
            }

            return Math.Round(profit / GetProfitDial(deals), 6);
        }

        /// <summary>
        /// take the average profit in% from profitable transactions
        /// взять средний профит в % у прибыльных сделок
        /// </summary>
        private static decimal GetAllMidleProfitInProfitInPersent(Position[] deals)
        {
            decimal profit = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].ProfitOperationPersent > 0)
                {
                    profit += deals[i].ProfitOperationPersent;
                }
            }
            if (profit == 0)
            {
                return profit;
            }

            if (GetProfitDial(deals) == 0)
            {
                return 0;
            }

            return profit / GetProfitDial(deals);
        }

        /// <summary>
        /// take the average profit in points from profitable transactions
        /// взять средний профит в пунктах у прибыльных сделок
        /// </summary>
        private static decimal GetAllMidleProfitInProfitInPunktOnDepozit(Position[] deals)
        {
            decimal profit = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].ProfitPortfolioPunkt > 0)
                {
                    profit += deals[i].ProfitPortfolioPunkt;
                }
            }

            if (profit == 0)
            {
                return profit;
            }

            if (GetProfitDial(deals) == 0)
            {
                return 0;
            }

            return Math.Round(profit / GetProfitDial(deals), 6);
        }

        /// <summary>
        /// take the average profit in% from profitable transactions
        /// взять средний профит в % у прибыльных сделок
        /// </summary>
        private static decimal GetAllMidleProfitInProfitInPersentOnDepozit(Position[] deals)
        {
            decimal profit = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].ProfitPortfolioPersent > 0)
                {
                    profit += deals[i].ProfitPortfolioPersent;
                }
            }
            if (profit == 0)
            {
                return profit;
            }

            if (GetProfitDial(deals) == 0)
            {
                return 0;
            }

            return profit / GetProfitDial(deals);
        }

        /// <summary>
        /// take maximum profit
        /// взять максимальный профит
        /// </summary>
        private static int GetMaxProfitSeries(Position[] deals)
        {
            int maxSeries = 0;

            int nowSeries = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].ProfitOperationPersent > 0)
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
        //Losses
        // лоси

        /// <summary>
        /// take the number of losing trades
        /// взять кол-во убыточных сделок
        /// </summary>
        private static int GetLossDial(Position[] deals)
        {
            int lossDeal = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].ProfitOperationPersent <= 0)
                {
                    lossDeal++;
                }
            }

            return lossDeal;
        }

        /// <summary>
        /// take the percentage of losing trades
        /// взять процент убыточных сделок
        /// </summary>
        private static decimal GetLossDialPersent(Position[] deals)
        {
            decimal lossDeal = GetLossDial(deals);

            if (lossDeal == 0)
            {
                return lossDeal;
            }

            return lossDeal / deals.Length * 100;

        }


        /// <summary>
        /// take the average loss in points
        /// взять средний убыток в пунктах
        /// </summary>
        private static decimal GetAllMidleLossInLossInPunkt(Position[] deals)
        {
            decimal loss = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].ProfitOperationPunkt <= 0)
                {
                    loss += deals[i].ProfitOperationPunkt;
                }
            }
            if (loss == 0)
            {
                return loss;
            }
            return Math.Round(loss / GetLossDial(deals), 6);
        }

        /// <summary>
        /// take an average loss in%
        /// взять средний убыток в %
        /// </summary>
        private static decimal GetAllMidleLossInLossInPersent(Position[] deals)
        {
            decimal loss = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].ProfitOperationPersent <= 0)
                {
                    loss += deals[i].ProfitOperationPersent;
                }
            }
            if (loss == 0)
            {
                return loss;
            }
            return loss / GetLossDial(deals);
        }

        /// <summary>
        /// take the average loss in points
        /// взять средний убыток в пунктах
        /// </summary>
        private static decimal GetAllMidleLossInLossInPunktOnDepozit(Position[] deals)
        {
            decimal loss = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].ProfitPortfolioPunkt <= 0)
                {
                    loss += deals[i].ProfitPortfolioPunkt;
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

        /// <summary>
        /// take an average loss in%
        /// взять средний убыток в %
        /// </summary>
        private static decimal GetAllMidleLossInLossInPersentOnDepozit(Position[] deals)
        {
            decimal loss = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].ProfitPortfolioPersent <= 0)
                {
                    loss += deals[i].ProfitPortfolioPersent;
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

        /// <summary>
        /// take the maximum losses
        /// взять максимальный лось
        /// </summary>
        private static int GetMaxLossSeries(Position[] deals)
        {
            int maxSeries = 0;

            int nowSeries = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].ProfitOperationPersent <= 0)
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

        /// <summary>
        /// take maximum drawdown
        /// взять максимальную просадку
        /// </summary>
        public static decimal GetMaxDownPersent(Position[] deals) 
        {
            decimal maxDown = decimal.MaxValue;

            if (GetProfitDial(deals) == 0)
            {
                return 0;
            }
            decimal thisSumm = 0;
            decimal thisPik = decimal.MinValue;

            for (int i = 0; i < deals.Length; i++)
            {
                thisSumm += deals[i].ProfitPortfolioPersent;

                decimal thisDown;
                if (thisSumm > thisPik)
                {
                    thisPik = thisSumm;
                }
                else
                {
                    if (thisPik > 0 && thisSumm < 0)
                    {
                        // if the last peak is above zero and the current sum is less than zero
                        // если последний пик выше нуля и текущая сумма меньше нуля

                        thisDown = -thisPik + thisSumm;

                        if (maxDown > thisDown)
                        {
                            maxDown = thisDown;
                        }

                    }
                    else if (thisPik < 0 && thisSumm < 0)
                    {
                        // if the last peak is below zero and the current sum is less than zero
                        // если последний пик ниже нуля и текущая сумма меньше нуля
                        thisDown = thisPik + thisSumm;

                        if (maxDown > thisDown)
                        {
                            maxDown = thisDown;
                        }

                    }
                    else if (thisPik > 0 && thisSumm > 0)
                    {
                        // if the last peak is above zero and the current sum is above zero
                        // если последний пик выше нуля и текущая сумма выше нуля
                        thisDown = -(thisPik - thisSumm);


                        if (maxDown > thisDown)
                        {
                            maxDown = thisDown;
                        }

                    }
                }
            }

            if (maxDown == decimal.MaxValue)
            {
                return 0;
            }

            return Round(maxDown);
        }

        /// <summary>
        /// take Profit factor
        /// взять Profit Factor
        /// </summary>
        public static decimal GetProfitFactor(Position[] deals)
        {
            decimal commonProfitPunkt = 0m;
            decimal commonLossPunkt = 0m;
            decimal profitFactor = 0m;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].ProfitOperationPunkt < 0)
                {
                    commonLossPunkt = commonLossPunkt + deals[i].ProfitOperationPunkt;
                }
                else
                {
                    commonProfitPunkt = commonProfitPunkt + deals[i].ProfitOperationPunkt;
                }
            }

            if (commonLossPunkt != 0 && commonProfitPunkt != 0) profitFactor = Math.Abs(commonProfitPunkt / commonLossPunkt);

            return Round(profitFactor);
        }

        public static decimal GetPayOffRatio(Position[] deals)
        {
            decimal avProfit = 0;
            decimal avLoss = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].ProfitOperationPunkt > 0)
                {
                    avProfit += deals[i].ProfitOperationPunkt;
                }
                else
                {
                    avLoss += deals[i].ProfitOperationPunkt;
                }
            }

            if (avLoss != 0)
            {
                return avProfit / avLoss;
            }

            return 0;
        }

        /// <summary>
        /// take recovery
        /// взять Recovery
        /// </summary>
        public static decimal GetRecovery(Position[] deals)
        {
            decimal recovery = 0m;
            decimal maxLossPunkt = 0m;

            for (int i = 0; i < deals.Length; i++)
            {
                if (deals[i].ProfitOperationPunkt <= maxLossPunkt)   // ProfitOperationPersent
                {
                    maxLossPunkt = deals[i].ProfitOperationPunkt;
                }
            }
            decimal profit = GetAllProfitInPunkt(deals);
            if (profit != 0 && maxLossPunkt != 0) recovery = Math.Abs(profit / maxLossPunkt);

            return Round(recovery);
        }

        public static List<Position> SortByTime(List<Position> positionsAll)
        {
            List<Position> newPositionsAll = new List<Position>();

            for (int i = 0; i < positionsAll.Count; i++)
            {
                if (newPositionsAll.Count == 0 ||
                    newPositionsAll[newPositionsAll.Count - 1].TimeCreate < positionsAll[i].TimeCreate)
                {
                    newPositionsAll.Add(positionsAll[i]);
                }
                else if (newPositionsAll[0].TimeCreate >= positionsAll[i].TimeCreate)
                {
                    newPositionsAll.Insert(0, positionsAll[i]);
                }
                else
                {
                    for (int i2 = 0; i2 < newPositionsAll.Count - 1; i2++)
                    {
                        if (newPositionsAll[i2].TimeCreate <= positionsAll[i].TimeCreate &&
                            newPositionsAll[i2 + 1].TimeCreate >= positionsAll[i].TimeCreate)
                        {
                            newPositionsAll.Insert(i2 + 1, positionsAll[i]);
                            break;
                        }
                    }
                }
            }

            return newPositionsAll;
        }

        private static decimal Round(decimal number)
        {
            return Decimal.Round(number, 6);
        }
    }
}
