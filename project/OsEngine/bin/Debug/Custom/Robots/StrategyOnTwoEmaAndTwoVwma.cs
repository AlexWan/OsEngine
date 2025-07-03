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
using System.Linq;
using OsEngine.Market.Servers;
using OsEngine.Market;

/* Description
trading robot for osengine

The trend robot on strategy on two Ema and two Vwma

Buy:
Fast Vwma and slow Vwma higher than both Emas (also fast and slow).

Sell:
Fast Vwma and slow Vwma Lower than both Emas (also fast and slow).

Exit:
The reverse conditions
*/

namespace OsEngine.Robots
{
    [Bot("StrategyOnTwoEmaAndTwoVwma")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    public class StrategyOnTwoEmaAndTwoVwma : BotPanel
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

        // Indicator settings
        private StrategyParameterInt _periodEmaFast;
        private StrategyParameterInt _periodEmaSlow;
        private StrategyParameterInt _periodVwmaFast;
        private StrategyParameterInt _periodVwmaSlow;

        // Indicator
        private Aindicator _emaFast;
        private Aindicator _emaSlow;
        private Aindicator _vwmaFast;
        private Aindicator _vwmaSlow;

        // The last value of the indicators
        private decimal _lastEmaFast;
        private decimal _lastEmaSlow;
        private decimal _lastVwmaFast;
        private decimal _lastVwmaSlow;

        public StrategyOnTwoEmaAndTwoVwma(string name, StartProgram startProgram) : base(name, startProgram)
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
            _periodEmaFast = CreateParameter("Period Ema Fast", 100, 10, 300, 10, "Indicator");
            _periodEmaSlow = CreateParameter("Period Ema Slow", 200, 10, 300, 10, "Indicator");
            _periodVwmaFast = CreateParameter("Period Vwma Fast", 100, 10, 300, 10, "Indicator");
            _periodVwmaSlow = CreateParameter("Period Vwma Slow", 200, 10, 300, 10, "Indicator");

            // Create indicator EmaFast
            _emaFast = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EmaFast", false);
            _emaFast = (Aindicator)_tab.CreateCandleIndicator(_emaFast, "Prime");
            ((IndicatorParameterInt)_emaFast.Parameters[0]).ValueInt = _periodEmaFast.ValueInt;
            _emaFast.Save();

            // Create indicator EmaSlow
            _emaSlow = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EmaSlow", false);
            _emaSlow = (Aindicator)_tab.CreateCandleIndicator(_emaSlow, "Prime");
            ((IndicatorParameterInt)_emaSlow.Parameters[0]).ValueInt = _periodEmaSlow.ValueInt;
            _emaSlow.Save();

            // Create indicator VwmaFast
            _vwmaFast = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "VwmaFast", false);
            _vwmaFast = (Aindicator)_tab.CreateCandleIndicator(_vwmaFast, "Prime");
            ((IndicatorParameterInt)_vwmaFast.Parameters[0]).ValueInt = _periodVwmaFast.ValueInt;
            _vwmaFast.Save();

            // Create indicator VwmaSlow
            _vwmaSlow = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "VwmaSlow", false);
            _vwmaSlow = (Aindicator)_tab.CreateCandleIndicator(_vwmaSlow, "Prime");
            _vwmaSlow.DataSeries[0].Color = System.Drawing.Color.Yellow;
            ((IndicatorParameterInt)_vwmaSlow.Parameters[0]).ValueInt = _periodVwmaSlow.ValueInt;
            _vwmaSlow.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += _vwmaWithAShift_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on strategy on two Ema and two Vwma " +
                "Buy: Fast Vwma and slow Vwma higher than both Emas (also fast and slow). " +
                "Sell: Fast Vwma and slow Vwma lower than both Emas (also fast and slow). " +
                "Exit: The reverse conditions. ";
        }

        // Indicator Update event
        private void _vwmaWithAShift_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_vwmaFast.Parameters[0]).ValueInt = _periodVwmaFast.ValueInt;
            _vwmaFast.Save();
            _vwmaFast.Reload();

            ((IndicatorParameterInt)_vwmaSlow.Parameters[0]).ValueInt = _periodVwmaSlow.ValueInt;
            _vwmaSlow.Save();
            _vwmaSlow.Reload();

            ((IndicatorParameterInt)_emaFast.Parameters[0]).ValueInt = _periodEmaFast.ValueInt;
            _emaFast.Save();
            _emaFast.Reload();

            ((IndicatorParameterInt)_emaSlow.Parameters[0]).ValueInt = _periodEmaSlow.ValueInt;
            _emaSlow.Save();
            _emaSlow.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyOnTwoEmaAndTwoVwma";
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
            if (candles.Count <= _periodVwmaFast.ValueInt ||
                candles.Count <= _periodVwmaSlow.ValueInt ||
                candles.Count <= _periodEmaFast.ValueInt ||
                candles.Count <= _periodEmaSlow.ValueInt)
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

            if (openPositions == null || openPositions.Count == 0)
            {
                // The last value of the indicators
                _lastEmaFast = _emaFast.DataSeries[0].Last;
                _lastEmaSlow = _emaSlow.DataSeries[0].Last;
                _lastVwmaFast = _vwmaFast.DataSeries[0].Last;
                _lastVwmaSlow = _vwmaSlow.DataSeries[0].Last;

                decimal lastPrice = candles[candles.Count - 1].Close;

                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastVwmaFast > _lastEmaFast && _lastVwmaFast > _lastEmaSlow
                        && _lastVwmaSlow > _lastEmaFast && _lastVwmaSlow > _lastEmaSlow)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastVwmaFast < _lastEmaFast && _lastVwmaFast < _lastEmaSlow
                        && _lastVwmaSlow < _lastEmaFast && _lastVwmaSlow < _lastEmaSlow)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestAsk - _slippage);
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
                decimal lastPrice = candles[candles.Count - 1].Close;

                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                // The last value of the indicators
                _lastEmaFast = _emaFast.DataSeries[0].Last;
                _lastEmaSlow = _emaSlow.DataSeries[0].Last;
                _lastVwmaFast = _vwmaFast.DataSeries[0].Last;
                _lastVwmaSlow = _vwmaSlow.DataSeries[0].Last;

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is long
                {
                   if(_lastVwmaFast < _lastEmaFast && _lastVwmaFast < _lastEmaSlow
                      && _lastVwmaSlow < _lastEmaFast && _lastVwmaSlow < _lastEmaSlow)
                   {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                   }
                }
                else // If the direction of the position is short
                {
                    if(_lastVwmaFast > _lastEmaFast && _lastVwmaFast > _lastEmaSlow
                        && _lastVwmaSlow > _lastEmaFast && _lastVwmaSlow > _lastEmaSlow)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice + _slippage, openPositions[i].OpenVolume);
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