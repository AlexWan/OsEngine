using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsEngine.Entity;

namespace OsEngine.OsOptimizer
{
    public class OptimizerReport
    {
        public OptimizerReport(int fazeUid,string securityName,List<IIStrategyParameter> paramaters)
        {
            Uid = NumberGen.GetNumberDeal(StartProgram.IsOsOptimizer);
            FazeUid = fazeUid;
            SecurityName = securityName;

            for (int i = 0; i < paramaters.Count; i++)
            {
                StrategyParameters.Add(paramaters[i].GetStringToSave());
            }
        }

        /// <summary>
        /// уникальный номер обхода
        /// </summary>
        public int Uid;

        /// <summary>
        /// номер фазы
        /// </summary>
        public int FazeUid;

        public string SecurityName;

        public List<string> StrategyParameters;

        public decimal TotalProfitInPercent;

        public decimal MaxDrowDawn;

        public int PositionsCount;

        public decimal AverageProfitInPercent;

        public decimal ProfitFactor;

        public decimal ProfitDealsCountInPercent;

    }
}
