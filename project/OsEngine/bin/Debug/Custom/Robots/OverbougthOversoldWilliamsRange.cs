/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/*Discription
Trading robot for osengine.

Trend robot on Overbougth Oversold WilliamsRange.

Buy:
1. The price of the instrument is higher than the Ssma and the Ssma is rising.
2. WR leaves the oversold zone, crossing the -80 mark from bottom to top.
Sell:
1. The price of the instrument is lower than the Ssma and the Ssma is falling.
2. WR leaves the overbought zone, crossing the -20 mark from top to bottom.

Exit: stop and profit in % of the entry price.
*/

namespace OsEngine.Robots
{
    [Bot("OverbougthOversoldWilliamsRange")] //We create an attribute so that we don't write anything in the Boot factory
    public class OverbougthOversoldWilliamsRange : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterTimeOfDay _timeStart;
        private StrategyParameterTimeOfDay _timeEnd;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator Settings
        private StrategyParameterInt _ssmaPeriod;
        private StrategyParameterInt _periodWilliams;

        // Indicator
        private Aindicator _ssma;
        private Aindicator _Williams;

        // Exit settings
        private StrategyParameterDecimal _stopValue;
        private StrategyParameterDecimal _profitValue;

        // The last value of the indicator
        private decimal _lastSsma;
        private decimal _lastWilliams;

        // The prev value of the indicator
        private decimal _prevSsma;
        private decimal _prevWilliams;

        public OverbougthOversoldWilliamsRange(string name, StartProgram startProgram) : base(name, startProgram)
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
            _ssmaPeriod = CreateParameter("Moving period", 15, 50, 300, 1, "Indicator");
            _periodWilliams = CreateParameter("Period Williams", 14, 50, 300, 1, "Indicator");

            // Creating an indicator Ssma
            _ssma = IndicatorsFactory.CreateIndicatorByName("Ssma", name + "Ssma", false);
            _ssma = (Aindicator)_tab.CreateCandleIndicator(_ssma, "Prime");
            ((IndicatorParameterInt)_ssma.Parameters[0]).ValueInt = _ssmaPeriod.ValueInt;
            _ssma.Save();

            // Creating an indicator WilliamsRange
            _Williams = IndicatorsFactory.CreateIndicatorByName("WilliamsRange", name + "WilliamsRange", false);
            _Williams = (Aindicator)_tab.CreateCandleIndicator(_Williams, "NewArea");
            ((IndicatorParameterInt)_Williams.Parameters[0]).ValueInt = _periodWilliams.ValueInt;
            _Williams.Save();

            // Exit settings
            _stopValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");
            _profitValue = CreateParameter("Profit Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += OverbougthOversoldWilliamsRange_ParametrsChangeByUser;

            // subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel230;
        }

        // Indicator Update event
        private void OverbougthOversoldWilliamsRange_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ssma.Parameters[0]).ValueInt = _ssmaPeriod.ValueInt;
            _ssma.Save();
            _ssma.Reload();
            ((IndicatorParameterInt)_Williams.Parameters[0]).ValueInt = _periodWilliams.ValueInt;
            _Williams.Save();
            _Williams.Reload();
        }
        
        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "OverbougthOversoldWilliamsRange";
        }
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Candle Completion Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _ssmaPeriod.ValueInt)
            {
                return;
            }

            // If the time does not match, we exit
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

            // The last value of the indicator
            _lastSsma = _ssma.DataSeries[0].Last;
            _lastWilliams = _Williams.DataSeries[0].Last;

            // The prev value of the indicator
            _lastSsma = _ssma.DataSeries[0].Values[_ssma.DataSeries[0].Values.Count - 2];
            _prevWilliams = _Williams.DataSeries[0].Values[_Williams.DataSeries[0].Values.Count - 2];

            decimal lastPrice = candles[candles.Count - 1].Close;

            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice > _lastSsma && _prevWilliams < -80 && _lastWilliams > -80)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, we enter the short
                {
                    if (lastPrice < _lastSsma && _prevWilliams > -20 && _lastWilliams < -20)
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