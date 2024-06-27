/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.Patterns
{
    [Bot("ThreeSoldier")]
    public class ThreeSoldier : BotPanel
    {
        public ThreeSoldier(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });

            VolumeFix = CreateParameter("Volume qty", 1, 1.0m, 50, 4);

            Slippage = CreateParameter("Slippage %", 0, 0, 20, 1m);

            HeightSoldiers = CreateParameter("Height soldiers %", 1, 0, 20, 1m);

            MinHeightOneSoldier = CreateParameter("Min height one soldier %", 0.2m, 0, 20, 1m);

            ProcHeightTake = CreateParameter("Profit % from height of pattern", 50m, 0, 20, 1m);

            ProcHeightStop = CreateParameter("Stop % from height of pattern", 20m, 0, 20, 1m);

            Description = "Trading robot Three Soldiers. " +
                "When forming a pattern of three growing / falling candles, " +
                "the entrance to the countertrend with a fixation on a profit or a stop";
        }

        public override string GetNameStrategyType()
        {
            return "ThreeSoldier";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private BotTabSimple _tab;

        // settings

        public StrategyParameterString Regime;

        public StrategyParameterDecimal HeightSoldiers;

        public StrategyParameterDecimal MinHeightOneSoldier;

        public StrategyParameterDecimal ProcHeightTake;

        public StrategyParameterDecimal ProcHeightStop;

        public StrategyParameterDecimal Slippage;

        public StrategyParameterDecimal VolumeFix;

        // logic

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 5)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                if (Regime.ValueString == "OnlyClosePosition")
                {
                    return;
                }
                LogicOpenPosition(candles);
            }
            else
            {
                LogicClosePosition(candles);
            }
        }

        private void LogicOpenPosition(List<Candle> candles)
        {
            decimal _lastPrice = candles[candles.Count - 1].Close;

            if (Math.Abs(candles[candles.Count - 3].Open - candles[candles.Count - 1].Close) 
                / (candles[candles.Count - 1].Close / 100) < HeightSoldiers.ValueDecimal)
            {
                return;
            }
            if (Math.Abs(candles[candles.Count - 3].Open - candles[candles.Count - 3].Close) 
                / (candles[candles.Count - 3].Close / 100) < MinHeightOneSoldier.ValueDecimal)
            {
                return;
            }
            if (Math.Abs(candles[candles.Count - 2].Open - candles[candles.Count - 2].Close) 
                / (candles[candles.Count - 2].Close / 100) < MinHeightOneSoldier.ValueDecimal)
            {
                return;
            }
            if (Math.Abs(candles[candles.Count - 1].Open - candles[candles.Count - 1].Close) 
                / (candles[candles.Count - 1].Close / 100) < MinHeightOneSoldier.ValueDecimal)
            {
                return;
            }

            //  long
            if (Regime.ValueString != "OnlyShort")
            {
                if (candles[candles.Count - 3].Open < candles[candles.Count - 3].Close 
                    && candles[candles.Count - 2].Open < candles[candles.Count - 2].Close 
                    && candles[candles.Count - 1].Open < candles[candles.Count - 1].Close)
                {
                    _tab.BuyAtLimit(VolumeFix.ValueDecimal, _lastPrice + _lastPrice * (Slippage.ValueDecimal / 100));
                }
            }

            // Short
            if (Regime.ValueString != "OnlyLong")
            {
                if (candles[candles.Count - 3].Open > candles[candles.Count - 3].Close 
                    && candles[candles.Count - 2].Open > candles[candles.Count - 2].Close 
                    && candles[candles.Count - 1].Open > candles[candles.Count - 1].Close)
                {
                    _tab.SellAtLimit(VolumeFix.ValueDecimal, _lastPrice - _lastPrice * (Slippage.ValueDecimal / 100));
                }
            }
            return;

        }

        private void LogicClosePosition(List<Candle> candles)
        {
            decimal _lastPrice = candles[candles.Count - 1].Close;

            List<Position> openPositions = _tab.PositionsOpenAll;
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].StopOrderPrice != 0)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy)
                {
                    decimal heightPattern = 
                        Math.Abs(_tab.CandlesAll[_tab.CandlesAll.Count - 4].Open - _tab.CandlesAll[_tab.CandlesAll.Count - 2].Close);

                    decimal priceStop = _lastPrice - (heightPattern * ProcHeightStop.ValueDecimal) / 100;
                    decimal priceTake = _lastPrice + (heightPattern * ProcHeightTake.ValueDecimal) / 100;
                    _tab.CloseAtStop(openPositions[i], priceStop, priceStop - priceStop * (Slippage.ValueDecimal / 100));
                    _tab.CloseAtProfit(openPositions[i], priceTake, priceTake - priceStop * (Slippage.ValueDecimal / 100));
                }
                else
                {
                    decimal heightPattern = Math.Abs(_tab.CandlesAll[_tab.CandlesAll.Count - 2].Close - _tab.CandlesAll[_tab.CandlesAll.Count - 4].Open);
                    decimal priceStop = _lastPrice + (heightPattern * ProcHeightStop.ValueDecimal) / 100;
                    decimal priceTake = _lastPrice - (heightPattern * ProcHeightTake.ValueDecimal) / 100;
                    _tab.CloseAtStop(openPositions[i], priceStop, priceStop + priceStop * (Slippage.ValueDecimal / 100));
                    _tab.CloseAtProfit(openPositions[i], priceTake, priceTake + priceStop * (Slippage.ValueDecimal / 100));
                }
            }
        }
    }
}