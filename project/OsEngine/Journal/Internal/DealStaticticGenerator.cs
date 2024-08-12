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


            report.Add(Convert.ToDouble(GetAllProfitInPunkt(deals)).ToString(new CultureInfo("ru-RU"))); //Net profit
            report.Add(Math.Round(GetAllProfitPersent(deals), 6).ToString(new CultureInfo("ru-RU")));//Net profti %
            report.Add(deals.Length.ToString(new CultureInfo("ru-RU")));// Number of transactions
            report.Add(GetAverageTimeOnPoses(deals));
            report.Add(GetSharpRatio(deals,7).ToString());
            
            report.Add(Math.Round(GetProfitFactor(deals), 6).ToString(new CultureInfo("ru-RU")));   //Profit Factor
            report.Add(Math.Round(GetRecovery(deals), 6).ToString(new CultureInfo("ru-RU")));   // Recovery
            report.Add("");

            report.Add(Convert.ToDouble(GetMidleProfitInPunkt(deals)).ToString(new CultureInfo("ru-RU"))); //average profit in 1 contract
            report.Add(Math.Round(GetMidleProfitInPersentOneContract(deals), 6).ToString(new CultureInfo("ru-RU"))); //average profit in % 1 contract
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
            report.Add(Math.Round(GetCommissionAmount(deals), 6).ToString(new CultureInfo("ru-RU"))); //maximum drawdown in percent/максимальная просадка в процентах

            /*report += Math.Round(GetSharp(), 2).ToString(new CultureInfo("ru-RU"));
            */
            return report;
        }

        // время

        public static string GetAverageTimeOnPoses(Position[] deals)
        {
            string result = "";

            TimeSpan allTime = new TimeSpan();
            int dealsCount = 0;

            for(int i = 0;i < deals.Length;i++)
            {
                DateTime openTime = deals[i].TimeOpen;
                DateTime closeTime = deals[i].TimeClose;
                
                if(closeTime == DateTime.MinValue)
                {
                    continue;
                }
                
                dealsCount++;

                allTime += closeTime - openTime;
            }

            if(dealsCount == 0)
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

        // профиты

        /// <summary>
        /// to take profits in points to deposit
        /// взять профит в пунктах к депозиту
        /// </summary>
        public static decimal GetAllProfitInPunkt(Position[] deals)
        {
            decimal profit = 0;

            for (int i = 0; i < deals.Length; i++)
            {
                profit += deals[i].ProfitPortfolioPunkt * (deals[i].MultToJournal / 100);
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
        public static decimal GetMidleProfitInPersentOneContract(Position[] deals) 
        {
            if (deals.Length == 0)
            {
                return 0;
            }

            decimal profit = 0;

            int divider = deals.Length;

            for (int i = 0; i < deals.Length; i++)
            {
                decimal enter = deals[i].EntryPrice;
                decimal exit = deals[i].ClosePrice;

                if(exit == 0)
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

            if(divider <= 0)
            {
                return 0;
            }

            return Round(profit / divider);
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
                decimal curProfit = deals[i].ProfitOperationPunkt * (deals[i].MultToJournal / 100);

                profit += curProfit;

                if (curProfit != 0)
                {
                    someProfit = curProfit;
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
                profit += deals[i].ProfitPortfolioPunkt * (deals[i].MultToJournal / 100);
            }

            return Math.Round(profit / deals.Length, 6);
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
                profit += deals[i].ProfitPortfolioPersent * (deals[i].MultToJournal / 100);
            }

            return Math.Round(profit / deals.Length, 6);
        }

        public static decimal GetSharpRatio(Position[] deals, decimal riskFreeProfitInYear)
        {
            /*

            Sharpe Ratio = (AHPR - (1+RFR)) / SD

            AHPR - усреднённая прибыль в % к портфелю со всех сделок за всё время 
            RFR - безрисковая ставка, рассчитанная за всё время которое мог получить инвестор от открытия первой сделки до открытия последней
            SD - стандартное отклонение массива прибылей всех сделок в отдельности

            */

            if(deals == null ||
                deals.Length == 0)
            {
                return 0;
            }

            // 1 берём AHRP - прибыль в % к портфелю со всех сделок за всё время 

            decimal ahpr = GetAllProfitPersent(deals);

            if(ahpr == 0)
            {
                return 0;
            }

            // берём RFR - безрисковая ставка, рассчитанная за всё время которое мог получить инвестор от открытия первой сделки до открытия последней

            DateTime timeFirstDeal = DateTime.MaxValue;
            DateTime timeEndDeal = DateTime.MinValue;

            for(int i = 0;i < deals.Length;i++)
            {
                if(deals[i].TimeOpen < timeFirstDeal)
                {
                    timeFirstDeal = deals[i].TimeOpen;
                }

                if(deals[i].TimeOpen > timeEndDeal)
                {
                    timeEndDeal = deals[i].TimeOpen;
                }

                if (deals[i].TimeClose > timeEndDeal)
                {
                    timeEndDeal = deals[i].TimeClose;
                }
            }

            decimal rfr = 0;

            if (timeFirstDeal != DateTime.MaxValue &&
                timeEndDeal != DateTime.MinValue &&
                riskFreeProfitInYear != 0)
            {
                int daysCountInPoses = Convert.ToInt32((timeEndDeal - timeFirstDeal).TotalDays);
                decimal riskFreeProfitInDay = riskFreeProfitInYear / 365;
                rfr = daysCountInPoses * riskFreeProfitInDay;
            }

            // берём SD - стандартное отклонение массива прибылей всех сделок в отдельности

            List<decimal> profitArray = new List<decimal>();

            for(int i = 0;i < deals.Length;i++)
            {
                profitArray.Add(deals[i].ProfitPortfolioPersent);
            }

            decimal sd = GetValueStandardDeviation(profitArray);

            // Sharpe Ratio = (AHPR - (1+RFR)) / SD

            if(sd == 0)
            {
                return 0;
            }

            decimal sharp = (ahpr - (1 + rfr)) / sd;

            return Math.Round(sharp,4);
        }

        private static decimal GetValueStandardDeviation(List<decimal> candles)
        {
            int length = candles.Count-1;

            if(length < 2)
            {
                return 0;
            }

            decimal sd = 0;

            decimal sum = 0;

            for (int j = length; j > -1; j--)
            {
                sum += candles[j];
            }

            var m = sum / length;

            for (int i = length; i > -1; i--)
            {
                decimal x = candles[i] - m;  //Difference between values for period and average/разница между значениями за период и средней
                double g = Math.Pow((double)x, 2.0);   // difference square
                sd += (decimal)g;   //square footage/ сумма квадратов
            }

            sd = (decimal)Math.Sqrt((double)sd / length);  //find the root of sum/period // находим корень из суммы/период 

            return Math.Round(sd, 5);

        }

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
                    profit += deals[i].ProfitOperationPunkt * (deals[i].MultToJournal / 100);
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
                    profit += deals[i].ProfitOperationPersent * (deals[i].MultToJournal / 100);
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
                    profit += deals[i].ProfitPortfolioPunkt * (deals[i].MultToJournal / 100);
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
                    profit += deals[i].ProfitPortfolioPersent * (deals[i].MultToJournal / 100);
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
                    loss += deals[i].ProfitOperationPunkt * (deals[i].MultToJournal / 100);
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
                    loss += deals[i].ProfitOperationPersent * (deals[i].MultToJournal / 100);
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
                    loss += deals[i].ProfitPortfolioPunkt * (deals[i].MultToJournal / 100);
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
                    loss += deals[i].ProfitPortfolioPersent * (deals[i].MultToJournal / 100);
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
            decimal maxDownAbs = decimal.MaxValue;
            decimal maxDownPersent = decimal.MaxValue;

            if (GetProfitDial(deals) == 0)
            {
                return 0;
            }
            decimal firsValue = deals[0].PortfolioValueOnOpenPosition;

            for(int i = 0;i < deals.Length;i++)
            {
                if(firsValue != 0)
                {
                    break;
                }
                firsValue = deals[i].PortfolioValueOnOpenPosition;
            }

            if(firsValue == 0)
            {
                firsValue = 1;
            }

            decimal thisSumm = firsValue;
            decimal thisPik = firsValue;

            for (int i = 0; i < deals.Length; i++)
            {
                thisSumm += deals[i].ProfitPortfolioPunkt * (deals[i].MultToJournal / 100);

                decimal thisDown;

                if (thisSumm > thisPik)
                {
                    thisPik = thisSumm;
                }
                else
                {
                    if (thisSumm < 0)
                    {
                        // уже ушли ниже нулевой отметки по счёту

                        thisDown = -thisPik + thisSumm;

                        if (maxDownAbs > thisDown)
                        {
                            maxDownAbs = thisDown;
                            decimal curDownPersent = maxDownAbs / (thisPik / 100);

                            if (maxDownPersent > curDownPersent)
                            {
                                maxDownPersent = curDownPersent;
                            }
                        }
                    }
                    else if (thisSumm > 0)
                    {
                        // выше нулевой отметки по счёту
                        thisDown = -(thisPik - thisSumm);

                        if (maxDownAbs > thisDown)
                        {
                            maxDownAbs = thisDown;
                            decimal curDownPersent = maxDownAbs / (thisPik / 100);

                            if(maxDownPersent > curDownPersent)
                            {
                                maxDownPersent = curDownPersent;
                            }
                        }

                    }
                }
            }

            if (maxDownPersent == decimal.MaxValue)
            {
                return 0;
            }

            return Round(maxDownPersent);
        }

        /// <summary>
        /// take Commission
        /// взять комиссию
        /// </summary>
        public static decimal GetCommissionAmount(Position[] deals)
        {
            if (deals.Length == 0)
            {
                return 0;
            }

            decimal commissionTotal = 0;

            for(int i = 0;i < deals.Length;i++)
            {
                commissionTotal += deals[i].CommissionTotal() * (deals[i].MultToJournal / 100);
            }

            return Round(commissionTotal);
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
                    commonLossPunkt = commonLossPunkt + deals[i].ProfitOperationPunkt * (deals[i].MultToJournal / 100);
                }
                else
                {
                    commonProfitPunkt = commonProfitPunkt + deals[i].ProfitOperationPunkt * (deals[i].MultToJournal / 100);
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
                if (deals[i].ProfitOperationPunkt > 0)
                {
                    allProfit += deals[i].ProfitOperationPunkt * (deals[i].MultToJournal / 100);
                    profitPos++;
                }
                else
                {
                    allLoss += deals[i].ProfitOperationPunkt * (deals[i].MultToJournal / 100);
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
                return Math.Abs(allProfit/profitPos) / Math.Abs(allLoss/lossPos);
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
                    maxLossPunkt = deals[i].ProfitOperationPunkt * (deals[i].MultToJournal / 100);
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
