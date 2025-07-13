﻿/*
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
using System.Linq;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Break PriceChannel And SMI.

Buy:
the candle closed above the upper PC line and the stochastic line is above the signal (green).

Sell:
the candle closed below the lower PC line and the stochastic line is below the signal(green).

Exit:
After a certain number of candles.
 */

namespace OsEngine.Robots.AO
{
    [Bot("BreakPriceChannelAndSMI")] // We create an attribute so that we don't write anything to the BotFactory
    public class BreakPriceChannelAndSMI : BotPanel
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

        // Indicator Settings 
        private StrategyParameterInt _stochasticPeriod1;
        private StrategyParameterInt _stochasticPeriod2;
        private StrategyParameterInt _stochasticPeriod3;
        private StrategyParameterInt _stochasticPeriod4;
        private StrategyParameterInt _pcUpLength;
        private StrategyParameterInt _pcDownLength;

        // Indicator
        private Aindicator _SMI;
        private Aindicator _PC;

        // Exit Setting
        private StrategyParameterInt _exitCandles;

        // The last value of the indicator
        private decimal _lastSignalSMI;
        private decimal _lastSMI;

        // The prev value of the indicator
        private decimal _prevUpPC;
        private decimal _prevDownPC;

        public BreakPriceChannelAndSMI(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator Settings
            _stochasticPeriod1 = CreateParameter("Stochastic Period One", 10, 10, 300, 10, "Indicator");
            _stochasticPeriod2 = CreateParameter("Stochastic Period Two", 26, 10, 300, 10, "Indicator");
            _stochasticPeriod3 = CreateParameter("Stochastic Period Three", 3, 10, 300, 10, "Indicator");
            _stochasticPeriod4 = CreateParameter("Stochastic Period 4", 2, 10, 300, 10, "Indicator");
            _pcUpLength = CreateParameter("Up Line Length", 21, 7, 48, 7, "Indicator");
            _pcDownLength = CreateParameter("Down Line Length", 21, 7, 48, 7, "Indicator");

            // Create indicator PC
            _PC = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PC", false);
            _PC = (Aindicator)_tab.CreateCandleIndicator(_PC, "Prime");
            ((IndicatorParameterInt)_PC.Parameters[0]).ValueInt = _pcUpLength.ValueInt;
            ((IndicatorParameterInt)_PC.Parameters[1]).ValueInt = _pcDownLength.ValueInt;
            _PC.Save();

            // Create indicator Stochastic
            _SMI = IndicatorsFactory.CreateIndicatorByName("StochasticMomentumIndex", name + "StochasticMomentumIndex", false);
            _SMI = (Aindicator)_tab.CreateCandleIndicator(_SMI, "NewArea0");
            ((IndicatorParameterInt)_SMI.Parameters[0]).ValueInt = _stochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_SMI.Parameters[1]).ValueInt = _stochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_SMI.Parameters[2]).ValueInt = _stochasticPeriod3.ValueInt;
            ((IndicatorParameterInt)_SMI.Parameters[3]).ValueInt = _stochasticPeriod4.ValueInt;
            _SMI.Save();

            // Exit Setting
            _exitCandles = CreateParameter("Exit Candles", 10, 5, 1000, 10, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakPriceChannelAndSMI_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel165;
        }

        private void BreakPriceChannelAndSMI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_SMI.Parameters[0]).ValueInt = _stochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_SMI.Parameters[1]).ValueInt = _stochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_SMI.Parameters[2]).ValueInt = _stochasticPeriod3.ValueInt;
            ((IndicatorParameterInt)_SMI.Parameters[3]).ValueInt = _stochasticPeriod4.ValueInt;
            _SMI.Save();
            _SMI.Reload();
            ((IndicatorParameterInt)_PC.Parameters[0]).ValueInt = _pcUpLength.ValueInt;
            ((IndicatorParameterInt)_PC.Parameters[1]).ValueInt = _pcDownLength.ValueInt;
            _PC.Save();
            _PC.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakPriceChannelAndSMI";
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
            if (candles.Count < _pcDownLength.ValueInt ||
                candles.Count < _pcUpLength.ValueInt ||
                candles.Count < _stochasticPeriod1.ValueInt ||
                candles.Count < _stochasticPeriod2.ValueInt ||
                candles.Count < _stochasticPeriod3.ValueInt ||
                candles.Count < _stochasticPeriod4.ValueInt)
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
            _lastSMI = _SMI.DataSeries[0].Last;
            _lastSignalSMI = _SMI.DataSeries[1].Last;

            // The prev value of the indicator
            _prevUpPC = _PC.DataSeries[0].Values[_PC.DataSeries[0].Values.Count - 2];
            _prevDownPC = _PC.DataSeries[1].Values[_PC.DataSeries[1].Values.Count - 2];

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice > _prevUpPC && _lastSignalSMI < _lastSMI)
                    {
                        var time = candles.Last().TimeStart;

                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage, time.ToString());
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastPrice < _prevDownPC && _lastSignalSMI > _lastSMI)
                    {
                        var time = candles.Last().TimeStart;

                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage, time.ToString());
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position position = openPositions[i];

                if (position.State != PositionStateType.Open)
                {
                    continue;
                }

                if (!NeedClosePosition(position, candles))
                {
                    continue;
                }

                if (position.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    _tab.CloseAtLimit(position, lastPrice - _slippage, position.OpenVolume);
                }
                else // If the direction of the position is sale
                {
                    _tab.CloseAtLimit(position, lastPrice + _slippage, position.OpenVolume);
                }
            }
        }

        private bool NeedClosePosition(Position position, List<Candle> candles)
        {
            if (position == null || position.OpenVolume == 0)
            {
                return false;
            }

            DateTime openTime = DateTime.Parse(position.SignalTypeOpen);

            int counter = 0;

            for (int i = candles.Count - 1; i >= 0; i--)
            {
                counter++;
                DateTime candelTime = candles[i].TimeStart;
                if (candelTime == openTime)
                {
                    if (counter >= _exitCandles.ValueInt + 1)
                    {
                        return true;
                    }
                }
            }

            return false;
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