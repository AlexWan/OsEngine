/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using System;
using System.Collections.Generic;

namespace OsEngine.OsTrader.Grids
{
    public class TrailingUp
    {
        #region Service

        public TrailingUp(TradeGrid grid)
        {
            _grid = grid;
        }

        protected TradeGrid _grid;

        public void Delete()
        {
            _grid = null;
        }

        public bool TrailingUpIsOn;

        public decimal TrailingUpStep;

        public decimal TrailingUpLimit;

        public bool TrailingUpCanMoveExitOrder;

        public bool TrailingDownIsOn;

        public decimal TrailingDownStep;

        public decimal TrailingDownLimit;

        public bool TrailingDownCanMoveExitOrder;

        public virtual string GetSaveString()
        {
            string result = "";

            result += TrailingUpIsOn + "@";
            result += TrailingUpStep + "@";
            result += TrailingUpLimit + "@";

            result += TrailingDownIsOn + "@";
            result += TrailingDownStep + "@";
            result += TrailingDownLimit + "@";
            result += TrailingUpCanMoveExitOrder + "@";
            result += TrailingDownCanMoveExitOrder + "@";
            result += "@";
            result += "@";
            result += "@";
            result += "@";
            result += "@"; // пять пустых полей в резерв

            return result;
        }

        public virtual void LoadFromString(string value)
        {
            try
            {
                string[] values = value.Split('@');

                TrailingUpIsOn = Convert.ToBoolean(values[0]);
                TrailingUpStep = values[1].ToDecimal();
                TrailingUpLimit = values[2].ToDecimal();

                TrailingDownIsOn = Convert.ToBoolean(values[3]);
                TrailingDownStep = values[4].ToDecimal();
                TrailingDownLimit = values[5].ToDecimal();

                if(string.IsNullOrEmpty(values[6]) == false)
                {
                    TrailingUpCanMoveExitOrder = Convert.ToBoolean(values[6]);
                }
                if (string.IsNullOrEmpty(values[7]) == false)
                {
                    TrailingDownCanMoveExitOrder = Convert.ToBoolean(values[7]);
                }
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Logic

        public virtual bool TryTrailingGrid()
        {
            if (TrailingUpIsOn == false
                && TrailingDownIsOn == false)
            {
                return false;
            }

            List<Candle> candles = _grid.Tab.CandlesAll;

            if (candles == null
                || candles.Count == 0)
            {
                return false;
            }

            decimal lastPrice = candles[candles.Count - 1].Close;

            if (lastPrice == 0)
            {
                return false;
            }

            bool trailUpIsDone = false;
            bool trailDownIsDone = false;

            if (TrailingUpIsOn == true
                && TrailingUpStep != 0 
                && TrailingUpLimit != 0)
            {
                trailUpIsDone = TrailingUpMethod(lastPrice);
            }

            if (TrailingDownIsOn == true
                 && TrailingDownStep != 0
                && TrailingDownLimit != 0)
            {
                trailDownIsDone = TrailingDownMethod(lastPrice);
            }

            if(trailUpIsDone == true 
                || trailDownIsDone == true)
            {
                return true;
            }

            return false;
        }

        private bool TrailingUpMethod(decimal lastPrice)
        {
            decimal maxPriceGrid = MaxGridPrice;

            if(maxPriceGrid == 0)
            {
                return false;
            }

            if(lastPrice < maxPriceGrid)
            {
                return false;
            }

            if(maxPriceGrid >= TrailingUpLimit)
            {
                return false;
            }

            decimal different = lastPrice - maxPriceGrid;

            if (different < TrailingUpStep)
            {
                return false;
            }

            if (maxPriceGrid + different >= TrailingUpLimit)
            {
                return false;
            }

            int stepsToUp = Convert.ToInt32(Math.Round(different / TrailingUpStep, 0));

            decimal upValue = stepsToUp * TrailingUpStep;

            ShiftGridUpOnValue(upValue);

            return true;
        }

        private bool TrailingDownMethod(decimal lastPrice)
        {
            decimal minPriceGrid = MinGridPrice;

            if (minPriceGrid == 0)
            {
                return false;
            }

            if (lastPrice > minPriceGrid)
            {
                return false;
            }

            if (minPriceGrid <= TrailingDownLimit)
            {
                return false;
            }

            decimal different = minPriceGrid - lastPrice;

            if (different < TrailingDownStep)
            {
                return false;
            }

            if (minPriceGrid - different <= TrailingDownLimit)
            {
                return false;
            }

            int stepsToDown = Convert.ToInt32(Math.Round(different / TrailingDownStep,0));

            decimal downValue = stepsToDown * TrailingDownStep;

            ShiftGridDownOnValue(downValue);

            return true;
        }

        public decimal MaxGridPrice
        {
            get
            {
                List<TradeGridLine> lines = _grid.GridCreator.Lines;

                if (lines == null || lines.Count == 0)
                {
                    return 0;
                }

                decimal maxPriceGrid = decimal.MinValue;

                for(int i = 0;i < lines.Count;i++)
                {
                    if (lines[i].PriceEnter >  maxPriceGrid)
                    {
                        maxPriceGrid = lines[i].PriceEnter;
                    }
                }

                if(maxPriceGrid ==  decimal.MinValue)
                {
                    return 0;
                }

                return maxPriceGrid;
            }
        }

        public decimal MinGridPrice
        {
            get
            {
                List<TradeGridLine> lines = _grid.GridCreator.Lines;

                if (lines == null || lines.Count == 0)
                {
                    return 0;
                }

                decimal minPriceGrid = decimal.MaxValue;

                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].PriceEnter < minPriceGrid)
                    {
                        minPriceGrid = lines[i].PriceEnter;
                    }
                }

                if (minPriceGrid == decimal.MaxValue)
                {
                    return 0;
                }

                return minPriceGrid;
            }
        }

        public void ShiftGridDownOnValue(decimal value)
        {
            List<TradeGridLine> lines = _grid.GridCreator.Lines;

            if (lines == null || lines.Count == 0)
            {
                return;
            }

            for(int i = 0;i < lines.Count;i++)
            {
                TradeGridLine line = lines[i];
                line.CanReplaceExitOrder = TrailingDownCanMoveExitOrder;
                line.PriceEnter -= value;
                line.PriceExit -= value;
            }
        }

        public void ShiftGridUpOnValue(decimal value)
        {
            List<TradeGridLine> lines = _grid.GridCreator.Lines;

            if (lines == null || lines.Count == 0)
            {
                return;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                TradeGridLine line = lines[i];
                line.CanReplaceExitOrder = TrailingUpCanMoveExitOrder;
                line.PriceEnter += value;
                line.PriceExit += value;
            }
        }

        #endregion

        #region Log

        public void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                ServerMaster.SendNewLogMessage(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}
