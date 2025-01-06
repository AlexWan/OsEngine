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

        public string GetSaveString()
        {
            string result = "";

            result += Faze.GetSaveString() + "^";

            for(int i = 0;i < Reports.Count;i++)
            {
                result += Reports[i].GetSaveString() + "^";
            }

            return result;
        }

        public void LoadFromString(string saveStr)
        {
            string[] str = saveStr.Split('^');

            Faze = new OptimizerFaze();
            Faze.LoadFromString(str[0]);
            
            for(int i = 1;i < str.Length -1;i++)
            {
                OptimizerReport newReport = new OptimizerReport();
                newReport.LoadFromString(str[i]);
                Reports.Add(newReport);
            }


        }

        public static void SortResults(List<OptimizerReport> reports, SortBotsType sortType)
        {
            if (reports ==  null || reports.Count == 0) 
            { 
                return; 
            }

            if (sortType == SortBotsType.BotName)
            {
                reports.Sort(delegate (OptimizerReport rep1, OptimizerReport rep2)
                {
                    return Convert.ToInt32(rep1.BotName.Split(' ')[0]).CompareTo(Convert.ToInt32(rep2.BotName.Split(' ')[0]));
                });
            }
            else if (sortType == SortBotsType.TotalProfit)
            {
                reports.Sort(delegate (OptimizerReport rep1, OptimizerReport rep2)
                {
                    return rep2.TotalProfit.CompareTo(rep1.TotalProfit);
                });
            }
            else if (sortType == SortBotsType.PositionCount)
            {
                reports.Sort(delegate (OptimizerReport rep1, OptimizerReport rep2)
                {
                    return rep2.PositionsCount.CompareTo(rep1.PositionsCount);
                });
            }
            else if (sortType == SortBotsType.MaxDrowDawn)
            {
                reports.Sort(delegate (OptimizerReport rep1, OptimizerReport rep2)
                {
                    return rep2.MaxDrowDawn.CompareTo(rep1.MaxDrowDawn);
                });
            }
            else if (sortType == SortBotsType.AverageProfit)
            {
                reports.Sort(delegate (OptimizerReport rep1, OptimizerReport rep2)
                {
                    return rep2.AverageProfit.CompareTo(rep1.AverageProfit);
                });
            }
            else if (sortType == SortBotsType.AverageProfitPercent)
            {
                reports.Sort(delegate (OptimizerReport rep1, OptimizerReport rep2)
                {
                    return rep2.AverageProfitPercentOneContract.CompareTo(rep1.AverageProfitPercentOneContract);
                });
            }
            else if (sortType == SortBotsType.ProfitFactor)
            {
                reports.Sort(delegate (OptimizerReport rep1, OptimizerReport rep2)
                {
                    return rep2.ProfitFactor.CompareTo(rep1.ProfitFactor);
                });
            }
            else if (sortType == SortBotsType.PayOffRatio)
            {
                reports.Sort(delegate (OptimizerReport rep1, OptimizerReport rep2)
                {
                    return rep2.PayOffRatio.CompareTo(rep1.PayOffRatio);
                });
            }
            else if (sortType == SortBotsType.Recovery)
            {
                reports.Sort(delegate (OptimizerReport rep1, OptimizerReport rep2)
                {
                    return rep2.Recovery.CompareTo(rep1.Recovery);
                });
            }
            else if (sortType == SortBotsType.SharpRatio)
            {
                reports.Sort(delegate (OptimizerReport rep1, OptimizerReport rep2)
                {
                    return rep2.SharpRatio.CompareTo(rep1.SharpRatio);
                });
            }
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

        public OptimizerReport()
        {

        }

        public string BotName;

        public List<string> StrategyParameters = new List<string>();

        public string GetParamsToDataTable()
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
                    param = new StrategyParameterLabel(name,"","",0,0,System.Drawing.Color.White);
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

            for (int i = 0; i < bot.TabsSimple.Count; i++)
            {
                OptimizerReportTab tab = new OptimizerReportTab();
                List<Position> positions =
                    bot.TabsSimple[i].GetJournal().AllPosition.FindAll(
                        pos => pos.State != PositionStateType.OpeningFail && pos.State != PositionStateType.ClosingFail);

                for(int j= 0;j< positions.Count;j++)
                {
                    Position pos = positions[j];
                    if(pos.State == PositionStateType.Open ||
                        pos.State == PositionStateType.Closing ||
                        pos.State == PositionStateType.ClosingFail)
                    {
                        pos.SetBidAsk(bot.TabsSimple[i].PriceBestBid, bot.TabsSimple[i].PriceBestAsk);
                    }
                }

                TabsReports.Add(tab);

                if (positions.Count == 0)
                {
                    continue;
                }

                allPositionsForAllTabs.AddRange(positions);

                Position[] posesArray = positions.ToArray();

                tab.SecurityName = bot.TabsSimple[i].Security.Name;
                tab.PositionsCount = positions.Count;
                tab.TotalProfit = PositionStatisticGenerator.GetAllProfitInPunkt(posesArray);
                tab.TotalProfitPersent = PositionStatisticGenerator.GetAllProfitPersent(posesArray);
                tab.MaxDrowDawn = PositionStatisticGenerator.GetMaxDownPercent(posesArray);

                tab.AverageProfit = tab.TotalProfit / (posesArray.Length+1);
                
                tab.AverageProfitPercentOneContract = PositionStatisticGenerator.GetMiddleProfitInPercentOneContract(posesArray);

                tab.ProfitFactor = PositionStatisticGenerator.GetProfitFactor(posesArray);
                tab.Recovery = PositionStatisticGenerator.GetRecovery(posesArray);
                tab.PayOffRatio = PositionStatisticGenerator.GetPayOffRatio(posesArray);
                tab.SharpRatio = PositionStatisticGenerator.GetSharpRatio(posesArray,7);
                tab.TabType = bot.TabsSimple[i].GetType().Name;
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
                TotalProfitPersent = TabsReports[0].TotalProfitPersent;
                MaxDrowDawn = TabsReports[0].MaxDrowDawn;
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
                TotalProfit = PositionStatisticGenerator.GetAllProfitInPunkt(posesArray);
                TotalProfitPersent = PositionStatisticGenerator.GetAllProfitPersent(posesArray);
                MaxDrowDawn = PositionStatisticGenerator.GetMaxDownPercent(posesArray);
                AverageProfit = PositionStatisticGenerator.GetMiddleProfitInPunkt(posesArray);
                AverageProfitPercentOneContract = PositionStatisticGenerator.GetMiddleProfitInPercentOneContract(posesArray);
                ProfitFactor = PositionStatisticGenerator.GetProfitFactor(posesArray);
                Recovery = PositionStatisticGenerator.GetRecovery(posesArray);
                PayOffRatio = PositionStatisticGenerator.GetPayOffRatio(posesArray);
                SharpRatio = PositionStatisticGenerator.GetSharpRatio(posesArray, 7);
            }
        }

        public int PositionsCount;

        public decimal TotalProfit;

        public decimal TotalProfitPersent;

        public decimal MaxDrowDawn;

        public decimal AverageProfit;

        public decimal AverageProfitPercentOneContract;

        public decimal ProfitFactor;

        public decimal PayOffRatio;

        public decimal Recovery;

        public decimal SharpRatio;

        public string GetSaveString()
        {
            string result = "";

            // Сохраняем основное
            result += BotName + "@";
            result +=  PositionsCount + "@";
            result += TotalProfit + "@";
            result += MaxDrowDawn + "@";
            result += AverageProfit + "@";
            result += AverageProfitPercentOneContract + "@";
            result += ProfitFactor + "@";
            result += PayOffRatio + "@";
            result += Recovery + "@";
            result += TotalProfitPersent + "@";
            result += SharpRatio + "@";

            // сохраняем параметры в строковом представлении
            string param = "";

            for(int i = 0;i < StrategyParameters.Count;i++)
            {
                param += StrategyParameters[i] + "&";
            }

            result += param + "@";

            // сохраняем отдельные репорты по вкладкам

            string reportTabs = "";

            for (int i = 0; i < TabsReports.Count; i++)
            {
                reportTabs += TabsReports[i].GetSaveString() + "&";
            }
            result += reportTabs + "@";

            return result;
        }

        public void LoadFromString(string saveStr)
        {
            string[] str = saveStr.Split('@');

            BotName = str[0];
            PositionsCount = Convert.ToInt32(str[1]);
            TotalProfit = Convert.ToDecimal(str[2]);
            MaxDrowDawn = Convert.ToDecimal(str[3]);
            AverageProfit = Convert.ToDecimal(str[4]);
            AverageProfitPercentOneContract = Convert.ToDecimal(str[5]);
            ProfitFactor = Convert.ToDecimal(str[6]);
            PayOffRatio = Convert.ToDecimal(str[7]);
            Recovery = Convert.ToDecimal(str[8]);
            TotalProfitPersent = Convert.ToDecimal(str[9]);
            SharpRatio = Convert.ToDecimal(str[10]);

            string [] param = str[11].Split('&');

            for(int i = 0;i < param.Length-1;i++)
            {
                StrategyParameters.Add(param[i]);
            }

            string [] reportTabs = str[12].Split('&');

            for(int i = 0;i < reportTabs.Length-1;i++)
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

        public decimal TotalProfitPersent;

        public decimal MaxDrowDawn;

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
            result += MaxDrowDawn + "*";
            result += AverageProfit + "*";
            result += AverageProfitPercentOneContract + "*";
            result += ProfitFactor + "*";
            result += PayOffRatio + "*";
            result += Recovery + "*";
            result += TotalProfitPersent + "*";
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
            MaxDrowDawn = save[4].ToDecimal();
            AverageProfit = save[5].ToDecimal();
            AverageProfitPercentOneContract = save[6].ToDecimal();
            ProfitFactor = save[7].ToDecimal();
            PayOffRatio = save[8].ToDecimal();
            Recovery = save[9].ToDecimal();
            TotalProfitPersent = save[10].ToDecimal();

            if(save.Length == 11)
            {
                return;
            }

            SharpRatio = save[11].ToDecimal();
        }
    }
}