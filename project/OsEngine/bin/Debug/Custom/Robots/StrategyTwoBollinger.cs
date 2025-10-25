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
using System.Drawing;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Two Bollinger.

Buy:
1. The price is in the lower zone between the two lower Bollinger lines.
2. The price has become higher than the lower line of the local bollinger (with a smaller deviation).
3. The last two candles are growing.

Sell:
1. The price is in the upper zone between the two upper lines of the bolter.
2. The price has become below the upper line of the local bollinger (with a smaller deviation).
3. The last two candles are falling.

Exit: the other side of the local bollinger.
 */

namespace OsEngine.Robots
{
    [Bot("StrategyTwoBollinger")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyTwoBollinger : BotPanel
    {
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

        // Indicator settings
        private StrategyParameterInt _bollingerLengthGlob;
        private StrategyParameterDecimal _bollingerDeviationGlob;
        private StrategyParameterInt _bollingerLengthLoc;
        private StrategyParameterDecimal _bollingerDeviationLoc;

        // Indicator
        private Aindicator _bollingerGlob;
        private Aindicator _bollingerLoc;

        // The last value of the indicator
        private decimal _lastUpLineLoc;
        private decimal _lastDownLineLoc;

        // The prev value of the indicator
        private decimal _prevUpLineGlob;
        private decimal _prevDownLineGlob;
        private decimal _prevUpLineLoc;
        private decimal _prevDownLineLoc;

        public StrategyTwoBollinger(string name, StartProgram startProgram) : base(name, startProgram)
        {
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

            // Indicator settings
            _bollingerLengthGlob = CreateParameter("Bollinger Length Glob", 21, 7, 48, 7, "Indicator");
            _bollingerDeviationGlob = CreateParameter("Bollinger Deviation Glob", 1.0m, 1, 5, 0.1m, "Indicator");
            _bollingerLengthLoc = CreateParameter("Bollinger Length Loc", 21, 7, 48, 7, "Indicator");
            _bollingerDeviationLoc = CreateParameter("Bollinger Deviation Loc", 1.0m, 1, 5, 0.1m, "Indicator");

            // Create indicator Bollinger Glob
            _bollingerGlob = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "BollingerGlob", false);
            _bollingerGlob = (Aindicator)_tab.CreateCandleIndicator(_bollingerGlob, "Prime");
            ((IndicatorParameterInt)_bollingerGlob.Parameters[0]).ValueInt = _bollingerLengthGlob.ValueInt;
            ((IndicatorParameterDecimal)_bollingerGlob.Parameters[1]).ValueDecimal = _bollingerDeviationGlob.ValueDecimal;
            _bollingerGlob.DataSeries[0].Color = Color.Yellow;
            _bollingerGlob.DataSeries[1].Color = Color.Yellow;
            _bollingerGlob.DataSeries[2].Color = Color.Yellow;
            _bollingerGlob.Save();

            // Create indicator Bollinger Loc
            _bollingerLoc = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "BollingerLoc", false);
            _bollingerLoc = (Aindicator)_tab.CreateCandleIndicator(_bollingerLoc, "Prime");
            ((IndicatorParameterInt)_bollingerLoc.Parameters[0]).ValueInt = _bollingerLengthLoc.ValueInt;
            ((IndicatorParameterDecimal)_bollingerLoc.Parameters[1]).ValueDecimal = _bollingerDeviationLoc.ValueDecimal;
            _bollingerLoc.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyTwoBollinger_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel280;
        }

        private void StrategyTwoBollinger_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_bollingerGlob.Parameters[0]).ValueInt = _bollingerLengthGlob.ValueInt;
            ((IndicatorParameterDecimal)_bollingerGlob.Parameters[1]).ValueDecimal = _bollingerDeviationGlob.ValueDecimal;
            _bollingerGlob.Save();
            _bollingerGlob.Reload();
            ((IndicatorParameterInt)_bollingerLoc.Parameters[0]).ValueInt = _bollingerLengthLoc.ValueInt;
            ((IndicatorParameterDecimal)_bollingerLoc.Parameters[1]).ValueDecimal = _bollingerDeviationLoc.ValueDecimal;
            _bollingerLoc.Save();
            _bollingerLoc.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyTwoBollinger";
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
            if (candles.Count < _bollingerLengthGlob.ValueInt ||
                candles.Count < _bollingerLengthLoc.ValueInt)
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
            // The last value of the indicator
            _lastUpLineLoc = _bollingerLoc.DataSeries[0].Last;
            _lastDownLineLoc = _bollingerLoc.DataSeries[1].Last;

            // The prev value of the indicator
            _prevUpLineGlob = _bollingerGlob.DataSeries[0].Values[_bollingerGlob.DataSeries[0].Values.Count - 2];
            _prevDownLineGlob = _bollingerGlob.DataSeries[1].Values[_bollingerGlob.DataSeries[1].Values.Count - 2];
            _prevUpLineLoc = _bollingerLoc.DataSeries[0].Values[_bollingerLoc.DataSeries[0].Values.Count - 2];
            _prevDownLineLoc = _bollingerLoc.DataSeries[1].Values[_bollingerLoc.DataSeries[1].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;
                decimal prevPriceGlob = candles[candles.Count - 2].High;
                decimal prevPriceLoc = candles[candles.Count - 2].Low;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_prevDownLineLoc > prevPriceLoc &&
                        _prevDownLineGlob < prevPriceLoc &&
                        _lastDownLineLoc < lastPrice && 
                        candles[candles.Count - 1].IsUp && 
                        candles[candles.Count - 2].IsUp)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_prevUpLineLoc < prevPriceGlob &&
                        _prevUpLineGlob > prevPriceGlob &&
                        _lastUpLineLoc > lastPrice &&
                        candles[candles.Count - 1].IsDown &&
                        candles[candles.Count - 2].IsDown)
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
            
            // The prev value of the indicator
            _prevUpLineGlob = _bollingerGlob.DataSeries[0].Values[_bollingerGlob.DataSeries[0].Values.Count - 2];
            _prevDownLineGlob = _bollingerGlob.DataSeries[1].Values[_bollingerGlob.DataSeries[1].Values.Count - 2];
            _prevUpLineLoc = _bollingerLoc.DataSeries[0].Values[_bollingerLoc.DataSeries[0].Values.Count - 2];
            _prevDownLineLoc = _bollingerLoc.DataSeries[1].Values[_bollingerLoc.DataSeries[1].Values.Count - 2];

            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal prevPriceGlob = candles[candles.Count - 2].High;
            decimal prevPriceLoc = candles[candles.Count - 2].Low;

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
                    if (_prevUpLineLoc < prevPriceGlob &&
                        _prevUpLineGlob > prevPriceGlob)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (_prevDownLineLoc > prevPriceLoc &&
                        _prevDownLineGlob < prevPriceLoc)
                    {
                        _tab.CloseAtLimit(pos, lastPrice + _slippage, pos.OpenVolume);
                    }
                }
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