/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/*Discription
Trading robot for osengine

Trend robot at the strategy on Adaptive Look Back and Bollinger

Buy:
the price is above the upper Bollinger band.

Sell:
the price is below the lower Bollinger band.

Exit the buy:
trailing stop in % of the low of the candle on which the minus exit coefficient entered * Adaptive Look Back.
Exit the sell:
trailing stop in % of the high of the candle on which you entered plus the entry coefficient * Adaptive Look. 
*/

namespace OsEngine.Robots
{
    [Bot("StrategyAdaptiveLookBackandBollinger")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    public class StrategyAdaptiveLookBackandBollinger : BotPanel
    {
        // Reference to the main trading tab
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterTimeOfDay _startTradeTime;
        private StrategyParameterTimeOfDay _endTradeTime;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator Settings
        private StrategyParameterInt _bollingerLength;
        private StrategyParameterDecimal _bollingerDeviation;
        private StrategyParameterInt _periodALB;
        private StrategyParameterDecimal _coefExitALB;
        private StrategyParameterDecimal _coefEntryALB;

        // Indicator
        private Aindicator _ALB;
        private Aindicator _bollinger;

        // The last value of the indicators      
        private decimal _lastUpBollinger;
        private decimal _lastDownBollinger;
        private decimal _lastALB;

        // Exit Setting
        private StrategyParameterDecimal _trailingValue;

        public StrategyAdaptiveLookBackandBollinger(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator Settings
            _periodALB = CreateParameter("Adaptive Look Back", 5, 1, 10, 1, "Indicator");
            _coefExitALB = CreateParameter("CoefExitALB", 0.2m, 0.01m, 2, 0.02m, "Indicator");
            _coefEntryALB = CreateParameter("CoefEntrytALB", 0.2m, 0.01m, 2, 0.02m, "Indicator");
            _bollingerLength = CreateParameter("BollingerLength", 250, 50, 500, 20, "Indicator");
            _bollingerDeviation = CreateParameter("BollingerDeviation", 0.2m, 0.01m, 2, 0.02m, "Indicator");

            // Create indicator Adaptive Look Back
            _ALB = IndicatorsFactory.CreateIndicatorByName("AdaptiveLookBack", name + "ALB", false);
            _ALB = (Aindicator)_tab.CreateCandleIndicator(_ALB, "NewArea");
            ((IndicatorParameterInt)_ALB.Parameters[0]).ValueInt = _periodALB.ValueInt;
            _ALB.Save();

            // Create indicator Bollinger
            _bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
            _bollinger = (Aindicator)_tab.CreateCandleIndicator(_bollinger, "Prime");
            ((IndicatorParameterInt)_bollinger.Parameters[0]).ValueInt = _bollingerLength.ValueInt;
            ((IndicatorParameterDecimal)_bollinger.Parameters[1]).ValueDecimal = _bollingerDeviation.ValueDecimal;
            _bollinger.DataSeries[0].Color = Color.Red;
            _bollinger.DataSeries[1].Color = Color.Red;
            _bollinger.Save();

            // Exit setting
            _trailingValue = CreateParameter("TrailingValue", 1, 1.0m, 10, 1, "Exit settings");

            ParametrsChangeByUser += _strategyAdaptiveLookBackandBollinger_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel236;
        }

        // Indicator Update event
        private void _strategyAdaptiveLookBackandBollinger_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ALB.Parameters[0]).ValueInt = _periodALB.ValueInt;
            _ALB.Save();
            _ALB.Reload();

            ((IndicatorParameterInt)_bollinger.Parameters[0]).ValueInt = _bollingerLength.ValueInt;
            ((IndicatorParameterDecimal)_bollinger.Parameters[1]).ValueDecimal = _bollingerDeviation.ValueDecimal;
            _bollinger.Save();
            _bollinger.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyAdaptiveLookBackandBollinger";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // Candle Finished Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count <= _periodALB.ValueInt ||
                candles.Count <= _bollingerLength.ValueInt + 1)
            {
                return;
            }

            // If the time does not match, we leave
            if (_startTradeTime.Value > _tab.TimeServerCurrent ||
                _endTradeTime.Value < _tab.TimeServerCurrent)
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
            if (_regime.ValueString == "OnlyClosePosition")
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
            List<Position> openPositions = _tab.PositionsOpenAll;

            // The last value of the indicators
            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;
            decimal lastPrice = candles[candles.Count - 1].Close;

            // He last value of the indicator           
            _lastUpBollinger = _bollinger.DataSeries[0].Last;
            _lastDownBollinger = _bollinger.DataSeries[1].Last;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice > _lastUpBollinger)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastPrice < _lastDownBollinger)
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
            
            _lastALB = _ALB.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                decimal stopPriсe;

                if (pos.Direction == Side.Buy) // If the direction of the position is long
                {
                    decimal low = candles[candles.Count - 1].Low;
                    stopPriсe = low - low * _trailingValue.ValueDecimal / 100 - _coefExitALB.ValueDecimal * _lastALB;
                }
                else // If the direction of the position is short
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPriсe = high + high * _trailingValue.ValueDecimal / 100 + _coefEntryALB.ValueDecimal * _lastALB;
                }

                _tab.CloseAtTrailingStop(pos, stopPriсe, stopPriсe);
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