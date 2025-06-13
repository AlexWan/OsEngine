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
    public class TradeGridStopAndProfit
    {
        #region Service

        public OnOffRegime ProfitRegime = OnOffRegime.Off;
        public TradeGridValueType ProfitValueType = TradeGridValueType.Percent;
        public decimal ProfitValue = 1.5m;

        public OnOffRegime StopRegime = OnOffRegime.Off;
        public TradeGridValueType StopValueType = TradeGridValueType.Percent;
        public decimal StopValue = 0.8m;

        public OnOffRegime TrailStopRegime = OnOffRegime.Off;
        public TradeGridValueType TrailStopValueType = TradeGridValueType.Percent;
        public decimal TrailStopValue = 0.8m;

        public string GetSaveString()
        {
            string result = "";

            result += ProfitRegime + "@";
            result += ProfitValueType + "@";
            result += ProfitValue + "@";
            result += StopRegime + "@";
            result += StopValueType + "@";
            result += StopValue + "@";
            result += TrailStopRegime + "@";
            result += TrailStopValueType + "@";
            result += TrailStopValue + "@";
            result += "@";
            result += "@";
            result += "@";
            result += "@";
            result += "@"; // пять пустых полей в резерв

            return result;
        }

        public void LoadFromString(string value)
        {
            try
            {
                string[] values = value.Split('@');

                Enum.TryParse(values[0], out ProfitRegime);
                Enum.TryParse(values[1], out ProfitValueType);
                ProfitValue = values[2].ToDecimal();

                Enum.TryParse(values[3], out StopRegime);
                Enum.TryParse(values[4], out StopValueType);
                StopValue = values[5].ToDecimal();

                Enum.TryParse(values[6], out TrailStopRegime);
                Enum.TryParse(values[7], out TrailStopValueType);
                TrailStopValue = values[8].ToDecimal();
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(),LogMessageType.Error);
            }
        }

        #endregion

        #region Logic

        public void Process(TradeGrid grid)
        {
            List<TradeGridLine> lines = grid.GetLinesWithOpenPosition();

            if (lines == null
                || lines.Count == 0)
            {
                return;
            }

            List<Position> positions = new List<Position>();

            for(int i = 0;i < lines.Count;i++)
            {
                Position pos = lines[i].Position;

                if(pos == null)
                {
                    continue;
                }

                positions.Add(pos);
            }

            if (TrailStopRegime == OnOffRegime.On
                && TrailStopValue > 0)
            {
                SetTrailStop(grid, positions);
            }

            if (ProfitRegime != OnOffRegime.Off
             || StopRegime != OnOffRegime.Off)
            {
                decimal middleEntryPrice = grid.MiddleEntryPrice;

                if (middleEntryPrice == 0)
                {
                    return;
                }

                if (ProfitRegime == OnOffRegime.On
                     && ProfitValue > 0)
                {
                    SetProfit(grid, middleEntryPrice, positions);
                }

                if (StopRegime == OnOffRegime.On
                     && StopValue > 0)
                {
                    SetStop(grid, middleEntryPrice, positions);
                }
            }
        }

        private void SetProfit(TradeGrid grid, decimal middleEntryPrice, List<Position> positions)
        {
            decimal profitPrice = 0;

            if (grid.GridCreator.GridSide == Side.Buy)
            {
                if (ProfitValueType == TradeGridValueType.Absolute)
                {
                    profitPrice = middleEntryPrice + ProfitValue;
                }
                else if(ProfitValueType == TradeGridValueType.Percent)
                {
                    profitPrice = middleEntryPrice + middleEntryPrice * (ProfitValue/100);
                }

                profitPrice = grid.Tab.RoundPrice(profitPrice, grid.Tab.Security, Side.Buy);
            }
            else if (grid.GridCreator.GridSide == Side.Sell)
            {
                if (ProfitValueType == TradeGridValueType.Absolute)
                {
                    profitPrice = middleEntryPrice - ProfitValue;
                }
                else if (ProfitValueType == TradeGridValueType.Percent)
                {
                    profitPrice = middleEntryPrice - middleEntryPrice * (ProfitValue / 100);
                }

                profitPrice = grid.Tab.RoundPrice(profitPrice, grid.Tab.Security, Side.Sell);
            }

            if(profitPrice == 0)
            {
                return;
            }

            for(int i = 0;i < positions.Count;i++)
            {
                Position pos = positions[i];

                if(pos.OpenVolume == 0
                    || pos.State == PositionStateType.Done
                    || pos.CloseActive == true)
                {
                    continue;
                }

                if(pos.ProfitOrderRedLine == profitPrice)
                {
                    continue;
                }

                grid.Tab.CloseAtProfitMarket(pos, profitPrice);
            }
        }

        private void SetStop(TradeGrid grid, decimal middleEntryPrice, List<Position> positions)
        {
            decimal stopPrice = 0;

            if (grid.GridCreator.GridSide == Side.Buy)
            {
                if (StopValueType == TradeGridValueType.Absolute)
                {
                    stopPrice = middleEntryPrice - StopValue;
                }
                else if (StopValueType == TradeGridValueType.Percent)
                {
                    stopPrice = middleEntryPrice - middleEntryPrice * (StopValue / 100);
                }

                stopPrice = grid.Tab.RoundPrice(stopPrice, grid.Tab.Security, Side.Buy);
            }
            else if (grid.GridCreator.GridSide == Side.Sell)
            {
                if (StopValueType == TradeGridValueType.Absolute)
                {
                    stopPrice = middleEntryPrice + StopValue;
                }
                else if (StopValueType == TradeGridValueType.Percent)
                {
                    stopPrice = middleEntryPrice + middleEntryPrice * (StopValue / 100);
                }

                stopPrice = grid.Tab.RoundPrice(stopPrice, grid.Tab.Security, Side.Sell);
            }

            if (stopPrice == 0)
            {
                return;
            }

            for (int i = 0; i < positions.Count; i++)
            {
                Position pos = positions[i];

                if (pos.OpenVolume == 0
                    || pos.State == PositionStateType.Done
                    || pos.CloseActive == true)
                {
                    continue;
                }

                if (pos.StopOrderRedLine == stopPrice)
                {
                    continue;
                }

                grid.Tab.CloseAtStopMarket(pos, stopPrice);
            }
        }

        private void SetTrailStop(TradeGrid grid, List<Position> positions)
        {
            List<Candle> candles = grid.Tab.CandlesAll;

            if (candles == null || candles.Count == 0)
            {
                return;
            }

            decimal lastPrice = candles[candles.Count - 1].Close;

            decimal stopPrice = 0;

            if (grid.GridCreator.GridSide == Side.Buy)
            {
                if (TrailStopValueType == TradeGridValueType.Absolute)
                {
                    stopPrice = lastPrice - TrailStopValue;
                }
                else if (TrailStopValueType == TradeGridValueType.Percent)
                {
                    stopPrice = lastPrice - lastPrice * (TrailStopValue / 100);
                }

                stopPrice = grid.Tab.RoundPrice(stopPrice, grid.Tab.Security, Side.Buy);
            }
            else if (grid.GridCreator.GridSide == Side.Sell)
            {
                if (TrailStopValueType == TradeGridValueType.Absolute)
                {
                    stopPrice = lastPrice + TrailStopValue;
                }
                else if (TrailStopValueType == TradeGridValueType.Percent)
                {
                    stopPrice = lastPrice + lastPrice * (TrailStopValue / 100);
                }

                stopPrice = grid.Tab.RoundPrice(stopPrice, grid.Tab.Security, Side.Sell);
            }

            if (stopPrice == 0)
            {
                return;
            }

            for (int i = 0; i < positions.Count; i++)
            {
                Position pos = positions[i];

                if (pos.OpenVolume == 0
                    || pos.State == PositionStateType.Done
                    || pos.CloseActive == true)
                {
                    continue;
                }

                if (pos.StopOrderRedLine == stopPrice)
                {
                    continue;
                }

                grid.Tab.CloseAtTrailingStopMarket(pos, stopPrice);
            }

            decimal maxPrice = decimal.MinValue;
            decimal minPrice = decimal.MaxValue;

            for (int i = 0; i < positions.Count; i++)
            {
                Position pos = positions[i];

                if (pos.OpenVolume == 0
                    || pos.State == PositionStateType.Done
                    || pos.CloseActive == true)
                {
                    continue;
                }

                if (pos.StopOrderRedLine == 0)
                {
                    continue;
                }

                if(pos.StopOrderRedLine > maxPrice)
                {
                    maxPrice = pos.StopOrderRedLine;
                }
                if(pos.StopOrderRedLine < minPrice)
                {
                    minPrice = pos.StopOrderRedLine;
                }
            }

            if(maxPrice == decimal.MinValue
                || minPrice == decimal.MaxValue)
            {
                return;
            }
            
            if(maxPrice != minPrice)
            {

                for (int i = 0; i < positions.Count; i++)
                {
                    Position pos = positions[i];

                    if (pos.OpenVolume == 0
                        || pos.State == PositionStateType.Done
                        || pos.CloseActive == true)
                    {
                        continue;
                    }

                    if (pos.Direction == Side.Buy)
                    {
                        grid.Tab.CloseAtTrailingStopMarket(pos, maxPrice);
                    }
                    else if (pos.Direction == Side.Sell)
                    {
                        grid.Tab.CloseAtTrailingStopMarket(pos, minPrice);
                    }
                }
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
