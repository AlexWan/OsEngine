/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Linq;
using OsEngine.Market.Servers;
using OsEngine.Market;
using System.Drawing;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Break Alligator with CMO.

Buy:
1. The value of the CMO indicator crosses the level 0 from the bottom up.
2. fast line (lips) above the middle line (teeth), middle line above the slow line (jaw).

Sell:
1. The value of the CMO indicator crosses level 0 from top to bottom.
2. fast line (lips) below the midline (teeth), middle line below the slow one (jaw).

Buy exit:
trailing stop in % of the line of the candle on which you entered.
Sell ​​exit:
trailing stop in % of the high of the candle where you entered.
 */

namespace OsEngine.Robots
{
    [Bot("BreakAlligatorCMO")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    public class BreakAlligatorCMO : BotPanel
    {
        // Reference to the main trading tab
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay StartTradeTime;
        private StrategyParameterTimeOfDay EndTradeTime;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator Settings 
        private StrategyParameterInt _lengthCMO;
        private StrategyParameterInt _alligatorFastLineLength;
        private StrategyParameterInt _alligatorMiddleLineLength;
        private StrategyParameterInt _alligatorSlowLineLength;

        // Indicator
        private Aindicator _CMO;
        private Aindicator _Alligator;

        // Exit Settings
        private StrategyParameterDecimal TrailingValue;

        // The last value of the indicator
        private decimal _lastCMO;
        private decimal _lastFastAlg;
        private decimal _lastMiddleAlg;
        private decimal _lastSlowAlg;

        // The prev value of the indicator
        private decimal _prevCMO;

        public BreakAlligatorCMO(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            StartTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            EndTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator Settings
            _lengthCMO = CreateParameter("CMO Length", 14, 7, 48, 7, "Indicator");
            _alligatorFastLineLength = CreateParameter("Alligator Fast Line Length", 10, 10, 300, 10, "Indicator");
            _alligatorMiddleLineLength = CreateParameter("Alligator Middle Line Length", 20, 10, 300, 10, "Indicator");
            _alligatorSlowLineLength = CreateParameter("Alligator Slow Line Length", 30, 10, 300, 10, "Indicator");

            // Create indicator Alligator
            _Alligator = IndicatorsFactory.CreateIndicatorByName("Alligator", name + "Alligator", false);
            _Alligator = (Aindicator)_tab.CreateCandleIndicator(_Alligator, "Prime");
            ((IndicatorParameterInt)_Alligator.Parameters[0]).ValueInt = _alligatorSlowLineLength.ValueInt;
            ((IndicatorParameterInt)_Alligator.Parameters[1]).ValueInt = _alligatorMiddleLineLength.ValueInt;
            ((IndicatorParameterInt)_Alligator.Parameters[2]).ValueInt = _alligatorFastLineLength.ValueInt;
            _Alligator.Save();

            // Create indicator CMO
            _CMO = IndicatorsFactory.CreateIndicatorByName("CMO", name + "CMO", false);
            _CMO = (Aindicator)_tab.CreateCandleIndicator(_CMO, "NewArea");
            ((IndicatorParameterInt)_CMO.Parameters[0]).ValueInt = _lengthCMO.ValueInt;
            _CMO.Save();

            // Exit Settings
            TrailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += _breakAlligatorCMO_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel136;
        }

        private void _breakAlligatorCMO_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_CMO.Parameters[0]).ValueInt = _lengthCMO.ValueInt;
            _CMO.Save();
            _CMO.Reload();

            ((IndicatorParameterInt)_Alligator.Parameters[0]).ValueInt = _alligatorSlowLineLength.ValueInt;
            ((IndicatorParameterInt)_Alligator.Parameters[1]).ValueInt = _alligatorMiddleLineLength.ValueInt;
            ((IndicatorParameterInt)_Alligator.Parameters[2]).ValueInt = _alligatorFastLineLength.ValueInt;
            _Alligator.Save();
            _Alligator.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakAlligatorCMO";
        }
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Candle Finished Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (Regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count <= _lengthCMO.ValueInt ||
                candles.Count <= _alligatorSlowLineLength.ValueInt + 8 ||
                candles.Count <= _alligatorMiddleLineLength.ValueInt ||
                candles.Count <= _alligatorFastLineLength.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
            if (StartTradeTime.Value > _tab.TimeServerCurrent ||
                EndTradeTime.Value < _tab.TimeServerCurrent)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            // If there are positions, then go to the position closing method
            if (openPositions != null && openPositions.Count != 0)
            {
                LogicClosePosition(candles);
            }

            // If the position closing mode, then exit the method
            if (Regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            // If there are no positions, then go to the position opening method
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles);
            }
        }

        // Opening logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            // The last value of the indicator
            _lastCMO = _CMO.DataSeries[0].Last;
            _lastFastAlg = _Alligator.DataSeries[2].Last;
            _lastMiddleAlg = _Alligator.DataSeries[1].Last;
            _lastSlowAlg = _Alligator.DataSeries[0].Last;

            // The prev value of the indicator
            _prevCMO = _CMO.DataSeries[0].Values[_CMO.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastCMO > 0 && _prevCMO < 0 && _lastFastAlg > _lastMiddleAlg && _lastMiddleAlg > _lastSlowAlg)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastCMO < 0 && _prevCMO > 0 && _lastFastAlg < _lastMiddleAlg && _lastMiddleAlg < _lastSlowAlg)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            
            decimal stopPrice;

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is long
                {
                    decimal lov = candles[candles.Count - 1].Low;
                    stopPrice = lov - lov * TrailingValue.ValueDecimal / 100;
                }
                else // If the direction of the position is short
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * TrailingValue.ValueDecimal / 100;
                }
                _tab.CloseAtTrailingStop(pos, stopPrice, stopPrice);
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