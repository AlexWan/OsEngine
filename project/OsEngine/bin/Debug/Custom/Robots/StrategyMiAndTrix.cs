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

Trend robot based on MassIndex and Trix indicators.

Buy:
When the MassIndex indicator value is above the lower line, and the Trix indicator is greater than zero.

Sell:
When the MassIndex indicator value is above the lower line, and the Trix indicator is less than zero.

Exit from buy:
When the MassIndex indicator value is above the lower line, and the Trix indicator is less than zero.

Exit from sell:
When the MassIndex indicator value is above the lower line, and the Trix indicator is greater than zero.
*/

namespace OsEngine.Robots
{
    [Bot("StrategyMiAndTrix")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    internal class StrategyMiAndTrix : BotPanel
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

        // Indicators settings 
        private StrategyParameterInt _miLength;
        private StrategyParameterInt _miSumLength;
        private StrategyParameterDecimal _miLine;
        private StrategyParameterInt _lengthTrix;

        // Indicators
        private Aindicator _mi;
        private Aindicator _trix;

        public StrategyMiAndTrix(string name, StartProgram startProgram) : base(name, startProgram)
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

            // Indicator settings
            _miLength = CreateParameter("Mi Length", 9, 5, 200, 5, "Indicator");
            _miSumLength = CreateParameter("Mi Sum Length", 25, 5, 200, 5, "Indicator");
            _miLine = CreateParameter("Mi Line", 26.5m, 5, 50, 1, "Indicator");
            _lengthTrix = CreateParameter("Length Trix", 9, 7, 48, 7, "Indicator");

            // Create indicator MassIndex
            _mi = IndicatorsFactory.CreateIndicatorByName("Mass_Index_MI", name + "MI", false);
            _mi = (Aindicator)_tab.CreateCandleIndicator(_mi, "MiArea");
            ((IndicatorParameterInt)_mi.Parameters[0]).ValueInt = _miLength.ValueInt;
            ((IndicatorParameterInt)_mi.Parameters[1]).ValueInt = _miSumLength.ValueInt;
            ((IndicatorParameterDecimal)_mi.Parameters[2]).ValueDecimal = _miLine.ValueDecimal;
            ((IndicatorParameterDecimal)_mi.Parameters[3]).ValueDecimal = _miLine.ValueDecimal;
            _mi.Save();

            // Create indicator Trix
            _trix = IndicatorsFactory.CreateIndicatorByName("Trix", name + "Trix", false);
            _trix = (Aindicator)_tab.CreateCandleIndicator(_trix, "NewArea");
            ((IndicatorParameterInt)_trix.Parameters[0]).ValueInt = _lengthTrix.ValueInt;
            _trix.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyMi_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel257;
        }

        private void StrategyMi_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_mi.Parameters[0]).ValueInt = _miLength.ValueInt;
            ((IndicatorParameterInt)_mi.Parameters[1]).ValueInt = _miSumLength.ValueInt;
            ((IndicatorParameterDecimal)_mi.Parameters[2]).ValueDecimal = _miLine.ValueDecimal;
            ((IndicatorParameterDecimal)_mi.Parameters[3]).ValueDecimal = _miLine.ValueDecimal;
            _mi.Save();
            _mi.Reload();

            ((IndicatorParameterInt)_trix.Parameters[0]).ValueInt = _lengthTrix.ValueInt;
            _trix.Save();
            _trix.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "StrategyMiAndTrix";
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
            if (candles.Count < _miLength.ValueInt + 10 || candles.Count < _miSumLength.ValueInt
                || candles.Count < _lengthTrix.ValueInt)
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

            // The last value of the indicator
            decimal lastMi = _mi.DataSeries[2].Last;
            decimal lastTrix = _trix.DataSeries[0].Last;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastMi > _miLine.ValueDecimal && lastTrix > 0)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastMi > _miLine.ValueDecimal && lastTrix < 0)
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

            // The last value of the indicator
            decimal lastMi = _mi.DataSeries[2].Last;
            decimal lastTrix = _trix.DataSeries[0].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal _slippage = _tab.Securiti.PriceStep * this._slippage.ValueDecimal / 100;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is long
                {
                    if (lastMi > _miLine.ValueDecimal && lastTrix < 0)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (lastMi > _miLine.ValueDecimal && lastTrix > 0)
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