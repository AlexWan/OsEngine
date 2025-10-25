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

/*Discription
Trading robot for osengine

Robot at the trend SmaChannel And SMI.

Buy:
The candle has closed below the lower SmaChannel line and the stochastic (violet) line is below a certain level.

Sell:
the candle closed above the upper SmaChannel line and the stochastic (violet) line is above a certain level.

Exit: 
We set the stop and profit as a percentage of the entry price.
*/

namespace OsEngine.Robots
{
    [Bot("StrategySmaChannelAndSMI")] //We create an attribute so that we don't write anything in the Boot factory
    public class StrategySmaChannelAndSMI : BotPanel
    {
        BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterTimeOfDay _timeStart;
        private StrategyParameterTimeOfDay _timeEnd;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _stochasticPeriod1;
        private StrategyParameterInt _stochasticPeriod2;
        private StrategyParameterInt _stochasticPeriod3;
        private StrategyParameterInt _stochasticPeriod4;
        private StrategyParameterInt _smaLength;
        private StrategyParameterDecimal _smaDeviation;
        private StrategyParameterDecimal _overboughtLine;
        private StrategyParameterDecimal _oversoldLine;

        // Indicator
        private Aindicator _SMI;
        private Aindicator _smaChannel;

        // The last value of the indicators
        private decimal _lastSMI;
        private decimal _lastUpSma;
        private decimal _lastDownSma;

        // Exit settings
        private StrategyParameterDecimal _stopValue;
        private StrategyParameterDecimal _profitValue;

        public StrategySmaChannelAndSMI(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _timeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _timeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator Settings
            _stochasticPeriod1 = CreateParameter("Stochastic Period One", 10, 10, 300, 10, "Indicator");
            _stochasticPeriod2 = CreateParameter("Stochastic Period Two", 26, 10, 300, 10, "Indicator");
            _stochasticPeriod3 = CreateParameter("Stochastic Period Three", 3, 10, 300, 10, "Indicator");
            _stochasticPeriod4 = CreateParameter("Stochastic Period 4", 2, 10, 300, 10, "Indicator");
            _smaLength = CreateParameter("SmaLength", 21, 10, 300, 10, "Indicator");
            _smaDeviation = CreateParameter("SmaDeviation", 2.0m, 10, 300, 10, "Indicator");
            _overboughtLine = CreateParameter("OverboughtLine", 2.0m, 10, 300, 10, "Indicator");
            _oversoldLine = CreateParameter("OversoldLine", 2.0m, 10, 300, 10, "Indicator");

            // Create indicator SmaChannel
            _smaChannel = IndicatorsFactory.CreateIndicatorByName("SmaChannel", name + "SmaChannel", false);
            _smaChannel = (Aindicator)_tab.CreateCandleIndicator(_smaChannel, "Prime");
            ((IndicatorParameterInt)_smaChannel.Parameters[0]).ValueInt = _smaLength.ValueInt;
            ((IndicatorParameterDecimal)_smaChannel.Parameters[1]).ValueDecimal = _smaDeviation.ValueDecimal;
            _smaChannel.Save();

            // Create indicator Stochastic
            _SMI = IndicatorsFactory.CreateIndicatorByName("StochasticMomentumIndex", name + "StochasticMomentumIndex", false);
            _SMI = (Aindicator)_tab.CreateCandleIndicator(_SMI, "NewArea0");
            ((IndicatorParameterInt)_SMI.Parameters[0]).ValueInt = _stochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_SMI.Parameters[1]).ValueInt = _stochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_SMI.Parameters[2]).ValueInt = _stochasticPeriod3.ValueInt;
            ((IndicatorParameterInt)_SMI.Parameters[3]).ValueInt = _stochasticPeriod4.ValueInt;
            _SMI.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategySmaChannelAndSMI_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Exit settings
            _stopValue = CreateParameter("Stop", 0.5m, 1, 10, 1, "Exit settings");
            _profitValue = CreateParameter("Profit", 0.5m, 1, 10, 1, "Exit settings");

            Description = OsLocalization.Description.DescriptionLabel274;
        }

        // Indicator Update event
        private void StrategySmaChannelAndSMI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_smaChannel.Parameters[0]).ValueInt = _smaLength.ValueInt;
            ((IndicatorParameterDecimal)_smaChannel.Parameters[1]).ValueDecimal = _smaDeviation.ValueDecimal;
            _smaChannel.Save();
            _smaChannel.Reload();
            ((IndicatorParameterInt)_SMI.Parameters[0]).ValueInt = _stochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_SMI.Parameters[1]).ValueInt = _stochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_SMI.Parameters[2]).ValueInt = _stochasticPeriod3.ValueInt;
            ((IndicatorParameterInt)_SMI.Parameters[3]).ValueInt = _stochasticPeriod4.ValueInt;
            _SMI.Save();
            _SMI.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategySmaChannelAndSMI";
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
            if (candles.Count < _smaLength.ValueInt || 
                candles.Count < _stochasticPeriod1.ValueInt || 
                candles.Count < _stochasticPeriod2.ValueInt || 
                candles.Count < _stochasticPeriod3.ValueInt || 
                candles.Count < _stochasticPeriod4.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
            if (_timeStart.Value > _tab.TimeServerCurrent ||
                _timeEnd.Value < _tab.TimeServerCurrent)
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

            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    // The last value of the indicator
                    _lastSMI = _SMI.DataSeries[0].Last;
                    _lastUpSma = _smaChannel.DataSeries[0].Last;
                    _lastDownSma = _smaChannel.DataSeries[2].Last;

                    if (_lastDownSma < lastPrice && _lastSMI < _oversoldLine.ValueDecimal)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastUpSma > lastPrice && _lastSMI > _overboughtLine.ValueDecimal)
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
           
            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is long
                {
                    decimal profitActivation = pos.EntryPrice + pos.EntryPrice * _profitValue.ValueDecimal / 100;
                    decimal stopActivation = pos.EntryPrice - pos.EntryPrice * _stopValue.ValueDecimal / 100;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation + _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation - _slippage);
                }
                else // If the direction of the position is short
                {
                    decimal profitActivation = pos.EntryPrice - pos.EntryPrice * _profitValue.ValueDecimal / 100;
                    decimal stopActivation = pos.EntryPrice + pos.EntryPrice * _stopValue.ValueDecimal / 100;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation - _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation + _slippage);
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