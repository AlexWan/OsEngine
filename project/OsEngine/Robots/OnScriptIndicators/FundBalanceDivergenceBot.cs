using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using System;

namespace OsEngine.Robots.OnScriptIndicators
{
    public class FundBalanceDivergenceBot : BotPanel
    {
        public FundBalanceDivergenceBot(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });

            IndicatorLookBack = CreateParameter("Indicator Look Back", 10, 5, 25, 1);
            IndicatorLookForward = CreateParameter("Indicator Look Forward", 10, 5, 25, 1);
            IndicatorDivergence = CreateParameter("Divergence to inter", 5m, 1, 25, 1);
            DaysInPosition = CreateParameter("Days in position", 10, 3, 25, 1);
            DaysBeforeEndQuarterToInter = CreateParameter("Days Before End Quarter To Inter", 10, 3, 25, 1);

            FBD = IndicatorsFactory.CreateIndicatorByName("FBD", name + "FBD", false);
            FBD = (Aindicator)_tab.CreateCandleIndicator(FBD, "FBDArea");
            FBD.ParametersDigit[0].Value = IndicatorLookBack.ValueInt;
            FBD.ParametersDigit[1].Value = IndicatorLookForward.ValueInt;
            FBD.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
            ParametrsChangeByUser += Event_ParametrsChangeByUser;
        }

        void Event_ParametrsChangeByUser()
        {
            if (FBD.ParametersDigit[0].Value != IndicatorLookForward.ValueInt ||
                FBD.ParametersDigit[1].Value != IndicatorLookBack.ValueInt)
            {
                FBD.ParametersDigit[0].Value = IndicatorLookForward.ValueInt;
                FBD.ParametersDigit[1].Value = IndicatorLookBack.ValueInt;
                FBD.Reload();
            }
        }

        /// <summary>
        /// uniq strategy name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "FundBalanceDivergenceBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }

        private BotTabSimple _tab;

        private Aindicator FBD;

        public StrategyParameterString Regime;

        public StrategyParameterInt IndicatorLookBack;

        public StrategyParameterInt IndicatorLookForward;

        public StrategyParameterDecimal IndicatorDivergence;

        public StrategyParameterInt DaysInPosition;

        public StrategyParameterInt DaysBeforeEndQuarterToInter;

        //public st

        // logic логика

        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if(FBD.DataSeries[0].Last == 0)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);
                }
            }

            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if(candles[candles.Count-1].TimeStart.Month != 3 &&
                candles[candles.Count - 1].TimeStart.Month != 6 &&
                candles[candles.Count - 1].TimeStart.Month != 9 &&
                candles[candles.Count - 1].TimeStart.Month != 12)
            {
                return;
            }

            if(candles[candles.Count-1].TimeStart.Day < 30 - DaysBeforeEndQuarterToInter.ValueInt)
            {
                return;
            }

            if(FBD.DataSeries[0].Last > IndicatorDivergence.ValueDecimal)
            {
                decimal volume = _tab.Portfolio.ValueCurrent /  candles[candles.Count - 1].Close;
                _tab.SellAtMarket(volume, candles[candles.Count - 1].TimeStart.AddDays(DaysInPosition.ValueInt).ToString());
            }
            if (FBD.DataSeries[0].Last < -IndicatorDivergence.ValueDecimal)
            {
                decimal volume = _tab.Portfolio.ValueCurrent / candles[candles.Count - 1].Close;
                _tab.BuyAtMarket(volume, candles[candles.Count - 1].TimeStart.AddDays(DaysInPosition.ValueInt).ToString());
            }

        }

        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if(position.State != PositionStateType.Open)
            {
                return;
            }

            System.DateTime timeExit = Convert.ToDateTime(position.SignalTypeOpen);

            if(timeExit < candles[candles.Count-1].TimeStart)
            {
                _tab.CloseAtMarket(position, position.OpenVolume);
            }
        }
    }
}