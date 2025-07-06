﻿/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market.Servers;
using OsEngine.Market;
using System.Drawing;

/*Discription
Trading robot for osengine.

Trend robot at the Intersection of Ssma and  Ssma offset.

Buy: Fast Ssma is higher than slow Ssma.

Sell: Fast Ssma is lower than slow Ssma.

Exit: on the opposite signal.
*/

namespace OsEngine.Robots
{
    [Bot("IntersectionOfSsmaAndSsmaOffset")]//We create an attribute so that we don't write anything in the Boot factory
    public class IntersectionOfSsmaAndSsmaOffset : BotPanel
    {
        private BotTabSimple _tab;
        
        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterTimeOfDay _timeStart;
        private StrategyParameterTimeOfDay _timeEnd;

        // Indicator
        private Aindicator _ssma1;
        private Aindicator _ssma2;

        // Indicator settings
        private StrategyParameterInt _periodSsmaFast;
        private StrategyParameterInt _periodSsmaSlow;
        private StrategyParameterInt _periodOffset;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // The last value of the indicators
        private decimal _lastSsmaSlow;
        private decimal _lastSsmaFast;

        public IntersectionOfSsmaAndSsmaOffset(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _timeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _timeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // Indicator Settings
            _periodSsmaFast = CreateParameter("fast Ssma1 period", 25, 10, 200, 10, "Indicator");
            _periodSsmaSlow = CreateParameter("slow Ssma2 period", 150, 50, 500, 10, "Indicator");
            _periodOffset = CreateParameter("offset SSma2", 5, 3, 100, 1, "Indicator");

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Creating an indicator Ssma1
            _ssma1 = IndicatorsFactory.CreateIndicatorByName("Ssma", name + "Ssma1", false);
            _ssma1 = (Aindicator)_tab.CreateCandleIndicator(_ssma1, "Prime");
            ((IndicatorParameterInt)_ssma1.Parameters[0]).ValueInt = _periodSsmaFast.ValueInt;
            _ssma1.DataSeries[0].Color = Color.Red;
            _ssma1.Save();

            // Creating indicator Ssma2
            _ssma2 = IndicatorsFactory.CreateIndicatorByName("OffsetSsma", name + "Ssma2", false);
            _ssma2 = (Aindicator)_tab.CreateCandleIndicator(_ssma2, "Prime");
            ((IndicatorParameterInt)_ssma2.Parameters[0]).ValueInt = _periodSsmaSlow.ValueInt;
            ((IndicatorParameterInt)_ssma2.Parameters[1]).ValueInt = _periodOffset.ValueInt;
            _ssma2.DataSeries[0].Color = Color.Green;
            _ssma2.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfSsmaAndSsmaOffset_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot at the Intersection of Ssma and  Ssma offset. " +
                "Buy: Fast Ssma is higher than slow Ssma. " +
                "Sell: Fast Ssma is lower than slow Ssma. " +
                "Exit: on the opposite signal.";
        }

        // Indicator Update event
        private void IntersectionOfSsmaAndSsmaOffset_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ssma1.Parameters[0]).ValueInt = _periodSsmaFast.ValueInt;
            _ssma1.Save();
            _ssma1.Reload();

            ((IndicatorParameterInt)_ssma2.Parameters[0]).ValueInt = _periodSsmaSlow.ValueInt;
            ((IndicatorParameterInt)_ssma2.Parameters[1]).ValueInt = _periodOffset.ValueInt;
            _ssma2.Save();
            _ssma2.Reload();
        }

        // Candle Completion Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _periodSsmaFast.ValueInt || candles.Count < _periodSsmaSlow.ValueInt)
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

            // if there are positions, then go to the position closing method
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

            // He last value of the indicators
            _lastSsmaFast = _ssma1.DataSeries[0].Last;
            _lastSsmaSlow = _ssma2.DataSeries[0].Last;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastSsmaFast > _lastSsmaSlow && lastPrice > _lastSsmaFast)
                    {
                        // We put a stop on the buy                       
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastSsmaFast < _lastSsmaSlow && lastPrice < _lastSsmaFast)
                    {
                        // Putting a stop on sale
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

            // He last value of the indicators
            _lastSsmaFast = _ssma1.DataSeries[0].Last;
            _lastSsmaSlow = _ssma2.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is buy
                {
                    if (_lastSsmaFast < _lastSsmaSlow && lastPrice < _lastSsmaFast)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_lastSsmaFast > _lastSsmaSlow && lastPrice > _lastSsmaFast)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice + _slippage, openPositions[i].OpenVolume);
                    }
                }
            }
        }

        // The name of the robot in OsEngin
        public override string GetNameStrategyType()
        {
            return "IntersectionOfSsmaAndSsmaOffset";
        }

        public override void ShowIndividualSettingsDialog()
        {

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