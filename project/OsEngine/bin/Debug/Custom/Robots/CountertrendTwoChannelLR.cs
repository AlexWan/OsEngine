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
using OsEngine.Market.Servers;
using OsEngine.Market;

/* Description
trading robot for osengine

The coutertrend robot on Two Channel Linear Regression.

Buy: the price has become lower than the lower line of the global linear regression channel,
we place a purchase order at the price of the lower line of the local channel.

Sell: the price has become higher than the upper line of the global linear regression channel, 
we place a buy order at the price of the upper line of the local channel.

Exit: channel center.
 */

namespace OsEngine.Robots
{
    [Bot("CountertrendTwoChannelLR")] // We create an attribute so that we don't write anything to the BotFactory
    public class CountertrendTwoChannelLR : BotPanel
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
        private StrategyParameterInt _periodLR;
        private StrategyParameterDecimal _upDeviationLoc;
        private StrategyParameterDecimal _downDeviationLoc;
        private StrategyParameterDecimal _upDeviationGlob;
        private StrategyParameterDecimal _downDeviationGlob;

        // Indicator
        private Aindicator _channelLRLoc;
        private Aindicator _channelLRGlob;

        // The last value of the indicator
        private decimal _lastUpLineLoc;
        private decimal _lastDownLineLoc;
        private decimal _lastUpLineGlob;
        private decimal _lastDownLineGlob;
        private decimal _lastCenterLineLoc;

        // The prev value of the indicator
        private decimal _prevCenterLineLoc;

        public CountertrendTwoChannelLR(string name, StartProgram startProgram) : base(name, startProgram)
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
            _periodLR = CreateParameter("PeriodLRLoc", 100, 10, 500, 10, "Indicator");
            _upDeviationLoc = CreateParameter("UpDeviationLoc", 1.0m, 1, 50, 1, "Indicator");
            _downDeviationLoc = CreateParameter("DownDeviationLoc", 1.0m, 1, 50, 1, "Indicator");
            _upDeviationGlob = CreateParameter("UpDeviationGlob", 3.0m, 1, 50, 1, "Indicator");
            _downDeviationGlob = CreateParameter("DownDeviationGlob", 3.0m, 1, 50, 1, "Indicator");

            // Create indicator LinearRegressionChannel
            _channelLRLoc = IndicatorsFactory.CreateIndicatorByName("LinearRegressionChannel", name + "LinearRegressionLoc", false);
            _channelLRLoc = (Aindicator)_tab.CreateCandleIndicator(_channelLRLoc, "Prime");
            ((IndicatorParameterInt)_channelLRLoc.Parameters[0]).ValueInt = _periodLR.ValueInt;
            ((IndicatorParameterDecimal)_channelLRLoc.Parameters[2]).ValueDecimal = _upDeviationLoc.ValueDecimal;
            ((IndicatorParameterDecimal)_channelLRLoc.Parameters[3]).ValueDecimal = _downDeviationLoc.ValueDecimal;
            _channelLRLoc.Save();

            // Create indicator LinearRegressionChannel
            _channelLRGlob = IndicatorsFactory.CreateIndicatorByName("LinearRegressionChannel", name + "LinearRegressionGlob", false);
            _channelLRGlob = (Aindicator)_tab.CreateCandleIndicator(_channelLRGlob, "Prime");
            ((IndicatorParameterInt)_channelLRGlob.Parameters[0]).ValueInt = _periodLR.ValueInt;
            ((IndicatorParameterDecimal)_channelLRGlob.Parameters[2]).ValueDecimal = _upDeviationGlob.ValueDecimal;
            ((IndicatorParameterDecimal)_channelLRGlob.Parameters[3]).ValueDecimal = _downDeviationGlob.ValueDecimal;
            _channelLRGlob.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += CountertrendTwoChannelLR_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The coutertrend robot on Two Channel Linear Regression. " +
                "Buy: the price has become lower than the lower line of the global linear regression channel, " +
                "we place a purchase order at the price of the lower line of the local channel. " +
                "Sell: the price has become higher than the upper line of the global linear regression channel, " +
                "we place a buy order at the price of the upper line of the local channel. " +
                "Exit: channel center.";
        }

        private void CountertrendTwoChannelLR_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_channelLRLoc.Parameters[0]).ValueInt = _periodLR.ValueInt;
            ((IndicatorParameterDecimal)_channelLRLoc.Parameters[2]).ValueDecimal = _upDeviationLoc.ValueDecimal;
            ((IndicatorParameterDecimal)_channelLRLoc.Parameters[3]).ValueDecimal = _downDeviationLoc.ValueDecimal;
            _channelLRLoc.Save();
            _channelLRLoc.Reload();
            ((IndicatorParameterInt)_channelLRGlob.Parameters[0]).ValueInt = _periodLR.ValueInt;
            ((IndicatorParameterDecimal)_channelLRGlob.Parameters[2]).ValueDecimal = _upDeviationGlob.ValueDecimal;
            ((IndicatorParameterDecimal)_channelLRGlob.Parameters[3]).ValueDecimal = _downDeviationGlob.ValueDecimal;
            _channelLRGlob.Save();
            _channelLRGlob.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CountertrendTwoChannelLR";
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
            if (candles.Count < _periodLR.ValueInt)
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
            _lastUpLineLoc = _channelLRLoc.DataSeries[0].Last;
            _lastDownLineLoc = _channelLRLoc.DataSeries[2].Last;
            _lastUpLineGlob = _channelLRGlob.DataSeries[0].Last;
            _lastDownLineGlob = _channelLRGlob.DataSeries[2].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice < _lastDownLineGlob)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _lastDownLineLoc + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastPrice > _lastUpLineGlob)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _lastUpLineLoc - _slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            // The last value of the indicator
            _lastCenterLineLoc = _channelLRLoc.DataSeries[1].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is long
                {
                    if (_lastCenterLineLoc < lastPrice)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice + _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (_lastCenterLineLoc > lastPrice)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
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