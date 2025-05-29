/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using System;


namespace OsEngine.OsTrader.Grids
{
    public class TradeGridStopBy
    {
        public bool StopGridByMoveUpIsOn;

        public decimal StopGridByMoveUpValuePercent;

        public TradeGridRegime StopGridByMoveUpReaction = TradeGridRegime.CloseForced;

        public bool StopGridByMoveDownIsOn;

        public decimal StopGridByMoveDownValuePercent;

        public TradeGridRegime StopGridByMoveDownReaction = TradeGridRegime.CloseForced;

        public bool StopGridByPositionsCountIsOn;

        public int StopGridByPositionsCountValue;

        public TradeGridRegime StopGridByPositionsCountReaction = TradeGridRegime.CloseForced;

        public void TryStopGridByEvent()
        {
            /*if (Regime != TradeGridRegime.On)
            {
                return;
            }

            if (StopGridByPositionsCountIsOn.ValueBool == true)
            {
                if (_lastGridOpenPositions > StopGridByPositionsCountValue.ValueInt)
                { // Останавливаем сетку по кол-ву уже открытых позиций с последнего создания сетки
                    Regime.ValueString = "Only Close";

                    SendNewLogMessage(
                        "Grid stopped by open positions count. Open positions: " + _lastGridOpenPositions,
                        OsEngine.Logging.LogMessageType.System);

                    return;
                }
            }

            if (StopGridByProfitIsOn.ValueBool == true
                || StopGridByStopIsOn.ValueBool == true)
            {
                decimal lastPrice = _tab.PriceBestAsk;

                if (lastPrice == 0)
                {
                    return;
                }

                if (StopGridByProfitIsOn.ValueBool == true)
                {
                    decimal profitMove = 0;

                    if (GridSide == Side.Buy)
                    {
                        profitMove = (lastPrice - FirstPrice) / (FirstPrice / 100);
                    }
                    else if (GridSide == Side.Sell)
                    {
                        profitMove = (FirstPrice - lastPrice) / (FirstPrice / 100);
                    }

                    if (profitMove > StopGridByProfitValuePercent.ValueDecimal)
                    {
                        // Останавливаем сетку по движению вверх от первой цены сетки
                        Regime.ValueString = "Only Close";

                        SendNewLogMessage(
                            "Grid stopped by move in Profit. Open positions: " + _lastGridOpenPositions,
                            OsEngine.Logging.LogMessageType.System);

                        return;
                    }
                }

                if (StopGridByStopIsOn.ValueBool == true)
                {
                    decimal lossMove = 0;

                    if (GridSide == Side.Buy)
                    {
                        lossMove = (FirstPrice - lastPrice) / (FirstPrice / 100);
                    }
                    else if (GridSide == Side.Sell)
                    {
                        lossMove = (lastPrice - FirstPrice) / (FirstPrice / 100);
                    }

                    if (lossMove > StopGridByProfitValuePercent.ValueDecimal)
                    {
                        // Останавливаем сетку по движению вверх от первой цены сетки
                        Regime.ValueString = "Only Close";

                        SendNewLogMessage(
                            "Grid stopped by move in Loss. Open positions: " + _lastGridOpenPositions,
                            OsEngine.Logging.LogMessageType.System);

                        return;
                    }
                }
            }*/
        }


        public string GetSaveString()
        {
            string result = "";

            result += StopGridByMoveUpIsOn + "@";
            result += StopGridByMoveUpValuePercent + "@";
            result += StopGridByMoveUpReaction + "@";

            result += StopGridByMoveDownIsOn + "@";
            result += StopGridByMoveDownValuePercent + "@";
            result += StopGridByMoveDownReaction + "@";

            result += StopGridByPositionsCountIsOn + "@";
            result += StopGridByPositionsCountValue + "@";
            result += StopGridByPositionsCountReaction + "@";
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

                // stop grid by event

                StopGridByMoveUpIsOn = Convert.ToBoolean(values[0]);
                StopGridByMoveUpValuePercent = values[1].ToDecimal();
                Enum.TryParse(values[2], out StopGridByMoveUpReaction);

                StopGridByMoveDownIsOn = Convert.ToBoolean(values[3]);
                StopGridByMoveDownValuePercent = values[4].ToDecimal();
                Enum.TryParse(values[5], out StopGridByMoveDownReaction);

                StopGridByPositionsCountIsOn = Convert.ToBoolean(values[6]);
                StopGridByPositionsCountValue = Convert.ToInt32(values[7]);
                Enum.TryParse(values[8], out StopGridByPositionsCountReaction);

            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(),LogMessageType.Error);
            }
        }


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
