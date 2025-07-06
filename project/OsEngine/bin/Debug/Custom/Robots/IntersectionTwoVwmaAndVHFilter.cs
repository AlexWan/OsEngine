﻿/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market.Servers;
using OsEngine.Market;

/* Description
Trading robot for osengine.

The trend robot on Intersection Two Vwma And VHFilter.

Buy:
1. Price is higher than fast Vwma, fast is higher than slow Vwma.
2. VHFilter value is lower (higher) than minLevel and growing.
Sell:
1. The price is lower than the fast Vwma, the fast is lower than the slow Vwma.
2. VHFilter value is lower (higher) than minLevel and growing.

Exit: reverse intersection of Vwma.
 */

namespace OsEngine.Robots
{
    [Bot("IntersectionTwoVwmaAndVHFilter")] // We create an attribute so that we don't write anything to the BotFactory
    public class IntersectionTwoVwmaAndVHFilter : BotPanel
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
        private StrategyParameterInt _lengthVHF;
        private StrategyParameterDecimal _minLevel;
        private StrategyParameterInt _periodVWMAFast;
        private StrategyParameterInt _periodVWMASlow;

        // Indicator
        private Aindicator _VHF;
        private Aindicator _VWMAFast;
        private Aindicator _VWMASlow;

        // The last value of the indicator
        private decimal _lastVHF;
        private decimal _lastVWMAFast;
        private decimal _lastVWMASlow;

        // The prev value of the indicator
        private decimal _prevVHF;

        public IntersectionTwoVwmaAndVHFilter(string name, StartProgram startProgram) : base(name, startProgram)
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
            _lengthVHF = CreateParameter("VHF Length", 10, 7, 48, 7, "Indicator");
            _minLevel = CreateParameter("Min Level", 1.0m, 1, 5, 0.1m, "Indicator");
            _periodVWMAFast = CreateParameter("Period SMA Fast", 100, 10, 300, 10, "Indicator");
            _periodVWMASlow = CreateParameter("Period SMA Slow", 200, 10, 300, 10, "Indicator");

            // Create indicator SmaFast
            _VWMAFast = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "VWMAFast", false);
            _VWMAFast = (Aindicator)_tab.CreateCandleIndicator(_VWMAFast, "Prime");
            ((IndicatorParameterInt)_VWMAFast.Parameters[0]).ValueInt = _periodVWMAFast.ValueInt;
            _VWMAFast.Save();

            // Create indicator SmaSlow
            _VWMASlow = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "VWMASlow", false);
            _VWMASlow = (Aindicator)_tab.CreateCandleIndicator(_VWMASlow, "Prime");
            _VWMASlow.DataSeries[0].Color = System.Drawing.Color.Yellow;
            ((IndicatorParameterInt)_VWMASlow.Parameters[0]).ValueInt = _periodVWMASlow.ValueInt;
            _VWMASlow.Save();

            // Create indicator VHF
            _VHF = IndicatorsFactory.CreateIndicatorByName("VHFilter", name + "VHFilter", false);
            _VHF = (Aindicator)_tab.CreateCandleIndicator(_VHF, "NewArea");
            ((IndicatorParameterInt)_VHF.Parameters[0]).ValueInt = _lengthVHF.ValueInt;
            _VHF.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionTwoVwmaAndVHFilter_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Intersection Two Vwma And VHFilter. " +
                "Buy: " +
                "1. Price is higher than fast Vwma, fast is higher than slow Vwma. " +
                "2. VHFilter value is lower (higher) than minLevel and growing. " +
                "Sell: " +
                "1. The price is lower than the fast Vwma, the fast is lower than the slow Vwma. " +
                "2. VHFilter value is lower (higher) than minLevel and growing. " +
                "Exit: reverse intersection of Vwma.";
        }

        private void IntersectionTwoVwmaAndVHFilter_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_VHF.Parameters[0]).ValueInt = _lengthVHF.ValueInt;
            _VHF.Save();
            _VHF.Reload();
            ((IndicatorParameterInt)_VWMAFast.Parameters[0]).ValueInt = _periodVWMAFast.ValueInt;
            _VWMAFast.Save();
            _VWMAFast.Reload();
            ((IndicatorParameterInt)_VWMASlow.Parameters[0]).ValueInt = _periodVWMASlow.ValueInt;
            _VWMASlow.Save();
            _VWMASlow.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionTwoVwmaAndVHFilter";
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
            if (candles.Count < _periodVWMAFast.ValueInt ||
                candles.Count < _lengthVHF.ValueInt ||
                candles.Count < _periodVWMASlow.ValueInt)
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
            _lastVHF = _VHF.DataSeries[0].Last;
            _lastVWMAFast = _VWMAFast.DataSeries[0].Last;
            _lastVWMASlow = _VWMASlow.DataSeries[0].Last;

            // The prev value of the indicator
            _prevVHF = _VHF.DataSeries[0].Values[_VHF.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastVWMAFast < lastPrice && _lastVWMAFast > _lastVWMASlow && _lastVHF < _minLevel.ValueDecimal && _prevVHF < _lastVHF)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastVWMAFast > lastPrice && _lastVWMAFast < _lastVWMASlow && _lastVHF < _minLevel.ValueDecimal && _prevVHF < _lastVHF)
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

            // The last value of the indicators
            _lastVWMAFast = _VWMAFast.DataSeries[0].Last;
            _lastVWMASlow = _VWMASlow.DataSeries[0].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is long
                {
                    if (_lastVWMAFast < _lastVWMASlow)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (_lastVWMAFast > _lastVWMASlow)
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