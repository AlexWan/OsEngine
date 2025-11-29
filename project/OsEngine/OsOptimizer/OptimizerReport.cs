/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Text;
using OsEngine.Entity;
using OsEngine.Journal.Internal;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.OsOptimizer
{
    public class OptimizerFazeReport
    {
        public OptimizerFaze Faze;

        public List<OptimizerReport> Reports = new List<OptimizerReport>();

        public void Load(BotPanel bot)
        {
            OptimizerReport report = new OptimizerReport(bot.Parameters);
            report.LoadState(bot);

            Reports.Add(report);
        }

        public StringBuilder GetSaveString()
        {
            StringBuilder result = new StringBuilder();

            result.Append(Faze.GetSaveString() + "^");

            for (int i = 0; i < Reports.Count; i++)
            {
                result.Append(Reports[i].GetSaveString() + "^");
            }

            return result;
        }

        public void LoadFromString(string saveStr)
        {
            string[] str = saveStr.Split('^');

            Faze = new OptimizerFaze();
            Faze.LoadFromString(str[0]);

            for (int i = 1; i < str.Length - 1; i++)
            {
                OptimizerReport newReport = new OptimizerReport();
                newReport.LoadFromString(str[i]);
                Reports.Add(newReport);
            }
        }

        public static void SortResults(List<OptimizerReport> reports, SortBotsType sortType)
        {
            for (int i = 0; i < reports.Count; i++)
            {
                for (int i2 = 0; i2 < reports.Count - 1; i2++)
                {
                    if (FirstLessSecond(reports[i2], reports[i2 + 1], sortType))
                    {
                        OptimizerReport glass = reports[i2];
                        reports[i2] = reports[i2 + 1];
                        reports[i2 + 1] = glass;
                    }
                }
            }
        }

        private static bool FirstLessSecond(OptimizerReport rep1, OptimizerReport rep2, SortBotsType sortType)
        {
            if (sortType == SortBotsType.TotalProfit &&
                rep1.TotalProfit < rep2.TotalProfit)
            {
                return true;
            }
            else if (sortType == SortBotsType.PositionCount &&
                     rep1.PositionsCount < rep2.PositionsCount)
            {
                return true;
            }
            else if (sortType == SortBotsType.MaxDrawDawn &&
                     rep1.MaxDrawDawn < rep2.MaxDrawDawn)
            {
                return true;
            }
            else if (sortType == SortBotsType.AverageProfit &&
                     rep1.AverageProfit < rep2.AverageProfit)
            {
                return true;
            }
            else if (sortType == SortBotsType.AverageProfitPercent &&
                     rep1.AverageProfitPercentOneContract < rep2.AverageProfitPercentOneContract)
            {
                return true;
            }
            else if (sortType == SortBotsType.ProfitFactor &&
                     rep1.ProfitFactor < rep2.ProfitFactor)
            {
                return true;
            }
            else if (sortType == SortBotsType.PayOffRatio &&
                     rep1.PayOffRatio < rep2.PayOffRatio)
            {
                return true;
            }
            else if (sortType == SortBotsType.Recovery &&
                     rep1.Recovery < rep2.Recovery)
            {
                return true;
            }
            else if (sortType == SortBotsType.SharpRatio &&
                    rep1.SharpRatio < rep2.SharpRatio)
            {
                return true;
            }

            return false;
        }
    }

    public class OptimizerReport
    {
        public OptimizerReport(List<IIStrategyParameter> parameters)
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                StrategyParameters.Add(parameters[i].Type + "$" + parameters[i].GetStringToSave() + "$" + parameters[i].Name);
            }
        }

        public OptimizerReport()
        {

        }

        public string BotName;

        public int BotNum
        {
            get
            {
                string num = BotName.Replace(" OpT OutOfSample", "").Replace(" OpT InSample", "");
                return Convert.ToInt32(num);
            }

        }

        public List<string> StrategyParameters = new List<string>();

        public string GetParametersToDataTable()
        {
            string result = "";

            List<IIStrategyParameter> parameters = GetParameters();

            for (int i = 0; i < parameters.Count; i++)
            {
                result += parameters[i].Name + " =; ";

                if (parameters[i].Type == StrategyParameterType.Bool)
                {
                    result += ((StrategyParameterBool)parameters[i]).ValueBool;
                }
                else if (parameters[i].Type == StrategyParameterType.Decimal)
                {
                    result += ((StrategyParameterDecimal)parameters[i]).ValueDecimal;
                }
                else if (parameters[i].Type == StrategyParameterType.Int)
                {
                    result += ((StrategyParameterInt)parameters[i]).ValueInt;
                }
                else if (parameters[i].Type == StrategyParameterType.String)
                {
                    result += ((StrategyParameterString)parameters[i]).ValueString;
                }
                else if (parameters[i].Type == StrategyParameterType.TimeOfDay)
                {
                    result += ((StrategyParameterTimeOfDay)parameters[i]).Value;
                }
                else if (parameters[i].Type == StrategyParameterType.CheckBox)
                {
                    result += ((StrategyParameterCheckBox)parameters[i]).CheckState;
                }
                else if (parameters[i].Type == StrategyParameterType.DecimalCheckBox)
                {
                    result += ((StrategyParameterDecimalCheckBox)parameters[i]).ValueDecimal + " (" + Convert.ToString(((StrategyParameterDecimalCheckBox)parameters[i]).CheckState) + ")";
                }

                result += ";";

                result += "\n";
            }

            return result;
        }

        public List<IIStrategyParameter> GetParameters()
        {
            List<IIStrategyParameter> par = new List<IIStrategyParameter>();

            for (int i = 0; i < StrategyParameters.Count; i++)
            {
                StrategyParameterType type;
                Enum.TryParse(StrategyParameters[i].Split('$')[0], out type);

                string name = StrategyParameters[i].Split('$')[2];

                IIStrategyParameter param = null;
                if (type == StrategyParameterType.Bool)
                {
                    param = new StrategyParameterBool(name, false);
                    param.LoadParamFromString(StrategyParameters[i].Split('$')[1].Split('#'));
                }
                else if (type == StrategyParameterType.Decimal)
                {
                    param = new StrategyParameterDecimal(name, 0, 0, 0, 0);
                    param.LoadParamFromString(StrategyParameters[i].Split('$')[1].Split('#'));
                }
                else if (type == StrategyParameterType.Int)
                {
                    param = new StrategyParameterInt(name, 0, 0, 0, 0);
                    param.LoadParamFromString(StrategyParameters[i].Split('$')[1].Split('#'));
                }
                else if (type == StrategyParameterType.String)
                {
                    param = new StrategyParameterString(name, "", null);
                    param.LoadParamFromString(StrategyParameters[i].Split('$')[1].Split('#'));
                }
                else if (type == StrategyParameterType.TimeOfDay)
                {
                    param = new StrategyParameterTimeOfDay(name, 0, 0, 0, 0);
                    param.LoadParamFromString(StrategyParameters[i].Split('$')[1].Split('#'));
                }
                else if (type == StrategyParameterType.Button)
                {
                    param = new StrategyParameterButton(name);
                    param.LoadParamFromString(StrategyParameters[i].Split('$')[1].Split('#'));
                }
                else if (type == StrategyParameterType.Label)
                {
                    param = new StrategyParameterLabel(name, "", "", 0, 0, System.Drawing.Color.White);
                    param.LoadParamFromString(StrategyParameters[i].Split('$')[1].Split('#'));
                }
                else if (type == StrategyParameterType.CheckBox)
                {
                    param = new StrategyParameterCheckBox(name, false);
                    param.LoadParamFromString(StrategyParameters[i].Split('$')[1].Split('#'));
                }
                else if (type == StrategyParameterType.DecimalCheckBox)
                {
                    param = new StrategyParameterDecimalCheckBox(name, 0, 0, 0, 0, false);
                    param.LoadParamFromString(StrategyParameters[i].Split('$')[1].Split('#'));
                }

                par.Add(param);
            }

            return par;
        }

        public List<OptimizerReportTab> TabsReports = new List<OptimizerReportTab>();

        public void LoadState(BotPanel bot)
        {
            BotName = bot.NameStrategyUniq;
            // фасуем отчёты по вкладкам

            List<Position> allPositionsForAllTabs = new List<Position>();

            List<BotTabSimple> tabsSimple = new List<BotTabSimple>();

            List<IIBotTab> sources = bot.GetTabs();


            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i].TabType == BotTabType.Simple)
                {
                    tabsSimple.Add((BotTabSimple)sources[i]);
                }
                else if (sources[i].TabType == BotTabType.Screener)
                {
                    BotTabScreener screener = (BotTabScreener)sources[i];

                    for (int j = 0; j < screener.Tabs.Count; j++)
                    {
                        tabsSimple.Add(screener.Tabs[j]);
                    }
                }
            }


            for (int i = 0; i < tabsSimple.Count; i++)
            {
                OptimizerReportTab tab = new OptimizerReportTab();
                List<Position> positions =
                    tabsSimple[i].GetJournal().AllPosition.FindAll(
                        pos => pos.State != PositionStateType.OpeningFail && pos.State != PositionStateType.ClosingFail);

                for (int j = 0; j < positions.Count; j++)
                {
                    Position pos = positions[j];
                    if (pos.State == PositionStateType.Open ||
                        pos.State == PositionStateType.Closing ||
                        pos.State == PositionStateType.ClosingFail)
                    {
                        pos.SetBidAsk(tabsSimple[i].PriceBestBid, tabsSimple[i].PriceBestAsk);
                    }
                }

                TabsReports.Add(tab);

                if (positions.Count == 0)
                {
                    continue;
                }

                allPositionsForAllTabs.AddRange(positions);

                Position[] posesArray = positions.ToArray();

                tab.SecurityName = tabsSimple[i].Security.Name;
                tab.PositionsCount = positions.Count;
                tab.TotalProfit = PositionStatisticGenerator.GetAllProfitInAbsolute(posesArray, false);
                tab.TotalProfitPercent = PositionStatisticGenerator.GetAllProfitPercent(posesArray, false);
                tab.MaxDrawDawn = PositionStatisticGenerator.GetMaxDownPercent(posesArray);

                tab.AverageProfit = tab.TotalProfit / (posesArray.Length + 1);

                tab.AverageProfitPercentOneContract = PositionStatisticGenerator.GetMiddleProfitInPercentOneContract(posesArray);

                tab.ProfitFactor = PositionStatisticGenerator.GetProfitFactor(posesArray);
                tab.Recovery = PositionStatisticGenerator.GetRecovery(posesArray);
                tab.PayOffRatio = PositionStatisticGenerator.GetPayOffRatio(posesArray);
                tab.SharpRatio = PositionStatisticGenerator.GetSharpRatio(posesArray, 7);
                tab.TabType = tabsSimple[i].GetType().Name;
            }

            if (TabsReports.Count == 0)
            {
                return;
            }

            // формируем общие данные по отчёту

            if (TabsReports.Count == 1)
            {
                PositionsCount = TabsReports[0].PositionsCount;
                TotalProfit = TabsReports[0].TotalProfit;
                TotalProfitPercent = TabsReports[0].TotalProfitPercent;
                MaxDrawDawn = TabsReports[0].MaxDrawDawn;
                AverageProfit = TabsReports[0].AverageProfit;
                AverageProfitPercentOneContract = TabsReports[0].AverageProfitPercentOneContract;

                ProfitFactor = TabsReports[0].ProfitFactor;
                Recovery = TabsReports[0].Recovery;
                PayOffRatio = TabsReports[0].PayOffRatio;
                SharpRatio = TabsReports[0].SharpRatio;
            }
            else
            {
                allPositionsForAllTabs = PositionStatisticGenerator.SortByTime(allPositionsForAllTabs);

                Position[] posesArray = allPositionsForAllTabs.ToArray();

                PositionsCount = allPositionsForAllTabs.Count;
                TotalProfit = PositionStatisticGenerator.GetAllProfitInAbsolute(posesArray, false);
                TotalProfitPercent = PositionStatisticGenerator.GetAllProfitPercent(posesArray, false);
                MaxDrawDawn = PositionStatisticGenerator.GetMaxDownPercent(posesArray);
                AverageProfit = PositionStatisticGenerator.GetMiddleProfitInAbsolute(posesArray);
                AverageProfitPercentOneContract = PositionStatisticGenerator.GetMiddleProfitInPercentOneContract(posesArray);
                ProfitFactor = PositionStatisticGenerator.GetProfitFactor(posesArray);
                Recovery = PositionStatisticGenerator.GetRecovery(posesArray);
                PayOffRatio = PositionStatisticGenerator.GetPayOffRatio(posesArray);
                SharpRatio = PositionStatisticGenerator.GetSharpRatio(posesArray, 7);
            }
        }

        public int PositionsCount;

        public decimal TotalProfit;

        public decimal TotalProfitPercent;

        public decimal MaxDrawDawn;

        public decimal AverageProfit;

        public decimal AverageProfitPercentOneContract;

        public decimal ProfitFactor;

        public decimal PayOffRatio;

        public decimal Recovery;

        public decimal SharpRatio;

        public StringBuilder GetSaveString()
        {
            StringBuilder result = new StringBuilder();

            // Сохраняем основное
            result.Append(BotName + "@");
            result.Append(PositionsCount + "@");
            result.Append(TotalProfit + "@");
            result.Append(MaxDrawDawn + "@");
            result.Append(AverageProfit + "@");
            result.Append(AverageProfitPercentOneContract + "@");
            result.Append(ProfitFactor + "@");
            result.Append(PayOffRatio + "@");
            result.Append(Recovery + "@");
            result.Append(TotalProfitPercent + "@");
            result.Append(SharpRatio + "@");

            // сохраняем параметры в строковом представлении
            StringBuilder parameters = new StringBuilder();

            for (int i = 0; i < StrategyParameters.Count; i++)
            {
                parameters.Append(StrategyParameters[i] + "&");
            }

            result.Append(parameters + "@");

            // сохраняем отдельные репорты по вкладкам

            StringBuilder reportTabs = new StringBuilder();

            for (int i = 0; i < TabsReports.Count; i++)
            {
                reportTabs.Append(TabsReports[i].GetSaveString() + "&");
            }

            result.Append(reportTabs + "@");

            return result;
        }

        public void LoadFromString(string saveStr)
        {
            string[] str = saveStr.Split('@');

            BotName = str[0];
            PositionsCount = Convert.ToInt32(str[1]);
            TotalProfit = Convert.ToDecimal(str[2]);
            MaxDrawDawn = Convert.ToDecimal(str[3]);
            AverageProfit = Convert.ToDecimal(str[4]);
            AverageProfitPercentOneContract = Convert.ToDecimal(str[5]);
            ProfitFactor = Convert.ToDecimal(str[6]);
            PayOffRatio = Convert.ToDecimal(str[7]);
            Recovery = Convert.ToDecimal(str[8]);
            TotalProfitPercent = Convert.ToDecimal(str[9]);
            SharpRatio = Convert.ToDecimal(str[10]);

            string[] param = str[11].Split('&');

            for (int i = 0; i < param.Length - 1; i++)
            {
                StrategyParameters.Add(param[i]);
            }

            string[] reportTabs = str[12].Split('&');

            for (int i = 0; i < reportTabs.Length - 1; i++)
            {
                OptimizerReportTab faze = new OptimizerReportTab();
                faze.LoadFromSaveString(reportTabs[i]);
                TabsReports.Add(faze);
            }
        }
    }

    public class OptimizerReportTab
    {
        public string TabType;

        public string SecurityName;

        public int PositionsCount;

        public decimal TotalProfit;

        public decimal TotalProfitPercent;

        public decimal MaxDrawDawn;

        public decimal AverageProfit;

        public decimal AverageProfitPercentOneContract;

        public decimal ProfitFactor;

        public decimal PayOffRatio;

        public decimal Recovery;

        public decimal SharpRatio;

        public string GetSaveString()
        {
            string result = "";

            result += TabType + "*";
            result += SecurityName + "*";
            result += PositionsCount + "*";
            result += TotalProfit + "*";
            result += MaxDrawDawn + "*";
            result += AverageProfit + "*";
            result += AverageProfitPercentOneContract + "*";
            result += ProfitFactor + "*";
            result += PayOffRatio + "*";
            result += Recovery + "*";
            result += TotalProfitPercent + "*";
            result += SharpRatio + "*";

            return result;
        }

        public void LoadFromSaveString(string saveStr)
        {
            string[] save = saveStr.Split('*');

            TabType = save[0];
            SecurityName = save[1];
            PositionsCount = Convert.ToInt32(save[2]);
            TotalProfit = save[3].ToDecimal();
            MaxDrawDawn = save[4].ToDecimal();
            AverageProfit = save[5].ToDecimal();
            AverageProfitPercentOneContract = save[6].ToDecimal();
            ProfitFactor = save[7].ToDecimal();
            PayOffRatio = save[8].ToDecimal();
            Recovery = save[9].ToDecimal();
            TotalProfitPercent = save[10].ToDecimal();

            if (save.Length == 11)
            {
                return;
            }

            SharpRatio = save[11].ToDecimal();
        }
    }
}