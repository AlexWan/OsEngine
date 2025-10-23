/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

/* Description
trading robot for osengine

Counter Trend Strategy Based on FundBalanceDivergence Bot.

Buy: FBD more Indicator Divergence. 

Sell: FBD is less than negative Indicator Divergence.

Exit: after N number of days.
 */

namespace OsEngine.Robots.OnScriptIndicators
{
    [Bot("FundBalanceDivergenceBot")] // We create an attribute so that we don't write anything to the BotFactory
    public class FundBalanceDivergenceBot : BotPanel
    {
        private BotTabSimple _tab;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _indicatorDivergence;
        private StrategyParameterInt _daysInPosition;
        private StrategyParameterInt _daysBeforeEndQuarterToInter;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _indicatorLookBack;
        private StrategyParameterInt _indicatorLookForward;

        // Indicator
        private Aindicator _FBD;

        public FundBalanceDivergenceBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _indicatorDivergence = CreateParameter("Divergence to inter", 5m, 1, 25, 1);
            _daysInPosition = CreateParameter("Days in position", 10, 3, 25, 1);
            _daysBeforeEndQuarterToInter = CreateParameter("Days Before End Quarter To Inter", 10, 3, 25, 1);

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _indicatorLookBack = CreateParameter("Indicator Look Back", 10, 5, 25, 1);
            _indicatorLookForward = CreateParameter("Indicator Look Forward", 10, 5, 25, 1);

            // Create indicator FBD
            _FBD = IndicatorsFactory.CreateIndicatorByName("FBD", name + "FBD", false);
            _FBD = (Aindicator)_tab.CreateCandleIndicator(_FBD, "FBDArea");
            _FBD.ParametersDigit[0].Value = _indicatorLookBack.ValueInt;
            _FBD.ParametersDigit[1].Value = _indicatorLookForward.ValueInt;
            _FBD.Save();

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += Event_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel58;
        }

        void Event_ParametrsChangeByUser()
        {
            if (_FBD.ParametersDigit[0].Value != _indicatorLookForward.ValueInt ||
                _FBD.ParametersDigit[1].Value != _indicatorLookBack.ValueInt)
            {
                _FBD.ParametersDigit[0].Value = _indicatorLookForward.ValueInt;
                _FBD.ParametersDigit[1].Value = _indicatorLookBack.ValueInt;
                _FBD.Reload();
            }
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "FundBalanceDivergenceBot";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Logic
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if(_FBD.DataSeries[0].Last == 0)
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

        // Opening position logic
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if(candles[candles.Count-1].TimeStart.Month != 3 &&
                candles[candles.Count - 1].TimeStart.Month != 6 &&
                candles[candles.Count - 1].TimeStart.Month != 9 &&
                candles[candles.Count - 1].TimeStart.Month != 12)
            {
                return;
            }

            if(candles[candles.Count-1].TimeStart.Day < 30 - _daysBeforeEndQuarterToInter.ValueInt)
            {
                return;
            }

            if(_FBD.DataSeries[0].Last > _indicatorDivergence.ValueDecimal)
            {
                _tab.SellAtMarket(GetVolume(_tab), candles[candles.Count - 1].TimeStart.AddDays(_daysInPosition.ValueInt).ToString());
            }

            if (_FBD.DataSeries[0].Last < -_indicatorDivergence.ValueDecimal)
            {
                _tab.BuyAtMarket(GetVolume(_tab), candles[candles.Count - 1].TimeStart.AddDays(_daysInPosition.ValueInt).ToString());
            }
        }

        // Close position logic
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if(position.State != PositionStateType.Open)
            {
                return;
            }

            System.DateTime timeExit = Convert.ToDateTime(position.SignalTypeOpen);

            if(timeExit < candles[candles.Count-1].TimeStart 
                && position.State == PositionStateType.Open)
            {
                _tab.CloseAtMarket(position, position.OpenVolume);
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (_volumeType.ValueString == "Contracts")
            {
                volume = _volume.ValueDecimal;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = _volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = _volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (_volumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (_tradeAssetInPortfolio.ValueString == "Prime")
                {
                    portfolioPrimeAsset = myPortfolio.ValueCurrent;
                }
                else
                {
                    List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                    if (positionOnBoard == null)
                    {
                        return 0;
                    }

                    for (int i = 0; i < positionOnBoard.Count; i++)
                    {
                        if (positionOnBoard[i].SecurityNameCode == _tradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (_volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    if (tab.Security.UsePriceStepCostToCalculateVolume == true
                     && tab.Security.PriceStep != tab.Security.PriceStepCost
                     && tab.PriceBestAsk != 0
                     && tab.Security.PriceStep != 0
                     && tab.Security.PriceStepCost != 0)
                    {// расчёт количества контрактов для фьючерсов и опционов на Мосбирже
                        qty = moneyOnPosition / (tab.PriceBestAsk / tab.Security.PriceStep * tab.Security.PriceStepCost);
                    }
                    qty = Math.Round(qty, tab.Security.DecimalsVolume);
                }
                else
                {
                    qty = Math.Round(qty, 7);
                }

                return qty;
            }

            return volume;
        }
    }
}