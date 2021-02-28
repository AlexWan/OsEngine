using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Journal.Internal;
using OsEngine.OsTrader.Panels;

namespace OsEngine.OsOptimizer
{
    public class OptimazerFazeReport
    {
        public OptimizerFaze Faze;

        public List<OptimizerReport> Reports = new List<OptimizerReport>();

        public void Load(BotPanel bot)
        {
            OptimizerReport report = new OptimizerReport(bot.Parameters);
            report.LoadState(bot);

            Reports.Add(report);
        }
    }

    public class OptimizerReport
    {
        public OptimizerReport(List<IIStrategyParameter> paramaters)
        {
            for (int i = 0; i < paramaters.Count; i++)
            {
                StrategyParameters.Add(paramaters[i].Type + "$" + paramaters[i].GetStringToSave() + "$"+  paramaters[i].Name);
            }
        }

        public string BotName;

        public List<string> StrategyParameters = new List<string>();

        public string GetParamsToDataTable()
        {
            string result = "";

            List<IIStrategyParameter> parameters = GetParameters();

            for (int i = 0; i < parameters.Count; i++)
            {
                result += parameters[i].Name + " = ";

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

            for (int i = 0; i < bot.TabsSimple.Count; i++)
            {
                OptimizerReportTab tab = new OptimizerReportTab();
                List<Position> positions =
                    bot.TabsSimple[i].GetJournal().AllPosition.FindAll(
                        pos => pos.State != PositionStateType.OpeningFail);

                if (positions.Count == 0)
                {
                    continue;
                }

                allPositionsForAllTabs.AddRange(positions);

                Position[] posesArray = positions.ToArray();

                tab.SecurityName = bot.TabsSimple[i].Securiti.Name;
                tab.PositionsCount = positions.Count;
                tab.TotalProfit = PositionStaticticGenerator.GetAllProfitInPunkt(posesArray);
                tab.MaxDrowDawn = PositionStaticticGenerator.GetMaxDownPersent(posesArray);

                tab.AverageProfit = tab.TotalProfit / (posesArray.Length+1);
                
                tab.AverageProfitPercent = PositionStaticticGenerator.GetMidleProfitInPersent(posesArray);

                tab.ProfitFactor = PositionStaticticGenerator.GetProfitFactor(posesArray);
                tab.Recovery = PositionStaticticGenerator.GetRecovery(posesArray);
                tab.PayOffRatio = PositionStaticticGenerator.GetPayOffRatio(posesArray);
                tab.TabType = bot.TabsSimple[i].GetType().Name;

                TabsReports.Add(tab);
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
                MaxDrowDawn = TabsReports[0].MaxDrowDawn;
                AverageProfit = TabsReports[0].AverageProfit;
                AverageProfitPercent = TabsReports[0].AverageProfitPercent;

                ProfitFactor = TabsReports[0].ProfitFactor;
                Recovery = TabsReports[0].Recovery;
                PayOffRatio = TabsReports[0].PayOffRatio;
            }
            else
            {
                allPositionsForAllTabs = PositionStaticticGenerator.SortByTime(allPositionsForAllTabs);

                Position[] posesArray = allPositionsForAllTabs.ToArray();

                PositionsCount = allPositionsForAllTabs.Count;
                TotalProfit = PositionStaticticGenerator.GetAllProfitInPunkt(posesArray);
                MaxDrowDawn = PositionStaticticGenerator.GetMaxDownPersent(posesArray);
                AverageProfit = PositionStaticticGenerator.GetMidleProfitInPunkt(posesArray);
                AverageProfitPercent = PositionStaticticGenerator.GetMidleProfitInPersent(posesArray);
                ProfitFactor = PositionStaticticGenerator.GetProfitFactor(posesArray);
                Recovery = PositionStaticticGenerator.GetRecovery(posesArray);
                PayOffRatio = PositionStaticticGenerator.GetPayOffRatio(posesArray);
            }
        }

        public int PositionsCount;

        public decimal TotalProfit;

        public decimal MaxDrowDawn;

        public decimal AverageProfit;

        public decimal AverageProfitPercent;

        public decimal ProfitFactor;

        public decimal PayOffRatio;

        public decimal Recovery;

    }

    public class OptimizerReportTab
    {
        public string TabType;

        public string SecurityName;

        public int PositionsCount;

        public decimal TotalProfit;

        public decimal MaxDrowDawn;

        public decimal AverageProfit;

        public decimal AverageProfitPercent;

        public decimal ProfitFactor;

        public decimal PayOffRatio;

        public decimal Recovery;
    }
}