﻿/*
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

/* Description
trading robot for osengine

The countertrend robot on VHFilter And PriceChannel.

Buy:
1. The price touched the lower PC line and closed higher.
2. The VHFilter value is higher than maxLevel.
Sell:
1. The price touched the upper PC line and closed lower.
2. VHFilter value is higher than maxLevel.

Exit from buy: Stop and profit.
The stop is placed at the minimum for the period specified for the stop (StopCandles). 
Profit is equal to the size of the stop * CoefProfit (CoefProfit – how many times the 
size of the profit is greater than the size of the stop).

Exit from sell: Stop and profit.
The stop is set to the maximum for the period specified for the stop (StopCandles). 
Profit is equal to the size of the stop * CoefProfit (CoefProfit – how many times the 
size of the profit is greater than the size of the stop).
 */

namespace OsEngine.Robots
{
    [Bot("ContertrendVHFilterAndPC")] // We create an attribute so that we don't write anything to the BotFactory
    public class ContertrendVHFilterAndPC : BotPanel
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
        private StrategyParameterInt _lengthVHF;
        private StrategyParameterInt _pcUpLength;
        private StrategyParameterInt _pcDownLength;
        private StrategyParameterDecimal _maxLavel;

        // Indicator
        private Aindicator _VHF;
        private Aindicator _PC;

        // The last value of the indicator
        private decimal _lastVHF;
        private decimal _lastUpPC;
        private decimal _lastDownPC;

        // The prev value of the indicator
        private decimal _prevUpPC;
        private decimal _prevDownPC;

        // Exit settings
        private StrategyParameterDecimal _coefProfit;
        private StrategyParameterInt _stopCandles;

        public ContertrendVHFilterAndPC(string name, StartProgram startProgram) : base(name, startProgram)
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
            _lengthVHF = CreateParameter("Length VHF", 10, 1, 50, 1, "Indicator");
            _pcUpLength = CreateParameter("Up Line Length", 21, 7, 48, 7, "Indicator");
            _pcDownLength = CreateParameter("Down Line Length", 21, 7, 48, 7, "Indicator");
            _maxLavel = CreateParameter("Max Lavel", 0.1m, 0.1m, 2, 0.1m, "Indicator");

            // Create indicator VHF
            _VHF = IndicatorsFactory.CreateIndicatorByName("VHFilter", name + "VHFilter", false);
            _VHF = (Aindicator)_tab.CreateCandleIndicator(_VHF, "NewArea");
            ((IndicatorParameterInt)_VHF.Parameters[0]).ValueInt = _lengthVHF.ValueInt;
            _VHF.Save();

            // Create indicator PC
            _PC = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PC", false);
            _PC = (Aindicator)_tab.CreateCandleIndicator(_PC, "Prime");
            ((IndicatorParameterInt)_PC.Parameters[0]).ValueInt = _pcUpLength.ValueInt;
            ((IndicatorParameterInt)_PC.Parameters[1]).ValueInt = _pcDownLength.ValueInt;
            _PC.Save();

            // Exit settings
            _coefProfit = CreateParameter("Coef Profit", 1, 1m, 10, 1, "Exit settings");
            _stopCandles = CreateParameter("Stop Candles", 1, 2, 10, 1, "Exit settings");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += ContertrendVHFilterAndPC_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The countertrend robot on VHFilter And PriceChannel. " +
                "Buy: " +
                "1. The price touched the lower PC line and closed higher. " +
                "2. The VHFilter value is higher than maxLevel. " +
                "Sell: " +
                "1. The price touched the upper PC line and closed lower. " +
                "2. VHFilter value is higher than maxLevel. " +
                "Exit from buy: Stop and profit. " +
                "The stop is placed at the minimum for the period specified for the stop (StopCandles).  " +
                "Profit is equal to the size of the stop * CoefProfit (CoefProfit – how many times the  " +
                "size of the profit is greater than the size of the stop). " +
                "Exit from sell: Stop and profit. " +
                "The stop is set to the maximum for the period specified for the stop (StopCandles).  " +
                "Profit is equal to the size of the stop * CoefProfit (CoefProfit – how many times the  " +
                "size of the profit is greater than the size of the stop).";
        }

        private void ContertrendVHFilterAndPC_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_VHF.Parameters[0]).ValueInt = _lengthVHF.ValueInt;
            _VHF.Save();
            _VHF.Reload();
            ((IndicatorParameterInt)_PC.Parameters[0]).ValueInt = _pcUpLength.ValueInt;
            ((IndicatorParameterInt)_PC.Parameters[1]).ValueInt = _pcDownLength.ValueInt;
            _PC.Save();
            _PC.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "ContertrendVHFilterAndPC";
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
            if (candles.Count < _pcUpLength.ValueInt ||
                candles.Count < _pcDownLength.ValueInt ||
                candles.Count < _lengthVHF.ValueInt ||
                candles.Count < _maxLavel.ValueDecimal)
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
            _lastUpPC = _PC.DataSeries[0].Last;
            _lastDownPC = _PC.DataSeries[1].Last;

            // The prev value of the indicator
            _prevUpPC = _PC.DataSeries[0].Values[_PC.DataSeries[0].Values.Count - 2];
            _prevDownPC = _PC.DataSeries[1].Values[_PC.DataSeries[1].Values.Count - 2];

            List<decimal> value = _VHF.DataSeries[0].Values;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal prevPriceHigh = candles[candles.Count - 2].High;
                decimal prevPriceLow = candles[candles.Count - 2].Low;
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (prevPriceLow <= _prevDownPC && lastPrice > _lastDownPC && _lastVHF > _maxLavel.ValueDecimal)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (prevPriceHigh >= _prevUpPC && lastPrice < _lastUpPC && _lastVHF > _maxLavel.ValueDecimal)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage);
                    }
                }
            }
        }

        //  Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
           
            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal profitActivation;
            decimal price;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is long
                {
                    decimal stopActivation = GetPriceStop(pos.TimeCreate, Side.Buy, candles, candles.Count - 1);

                    if (stopActivation == 0)
                    {
                        return;
                    }

                    price = stopActivation;
                    profitActivation = pos.EntryPrice + (pos.EntryPrice - price) * _coefProfit.ValueDecimal;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation + _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation - _slippage);
                }
                else // If the direction of the position is short
                {
                    decimal stopActivation = GetPriceStop(pos.TimeCreate, Side.Sell, candles, candles.Count - 1);

                    if (stopActivation == 0)
                    {
                        return;
                    }

                    price = stopActivation;
                    profitActivation = pos.EntryPrice - (price - pos.EntryPrice) * _coefProfit.ValueDecimal;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation - _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation + _slippage);
                }
            }
        }

        private decimal GetPriceStop(DateTime positionCreateTime, Side side, List<Candle> candles, int index)
        {
            if (candles == null || index < _stopCandles.ValueInt)
            {
                return 0;
            }

            if (side == Side.Buy)
            {
                // We calculate the stop price at Long
                // We find the minimum for the time from the opening of the transaction to the current one
                decimal price = decimal.MaxValue; ;
                int indexIntro = 0;
                DateTime openPositionTime = positionCreateTime;

                if (openPositionTime == DateTime.MinValue)
                {
                    openPositionTime = candles[index - 2].TimeStart;
                }

                for (int i = index; i > 0; i--)
                {
                    // Look at the index of the candle, after which the opening of the pose occurred
                    if (candles[i].TimeStart <= openPositionTime)
                    {
                        indexIntro = i;
                        break;
                    }
                }

                for (int i = indexIntro; i > 0 && i > indexIntro - _stopCandles.ValueInt; i--)
                {
                    // Looking at the minimum after opening
                    if (candles[i].Low < price)
                    {
                        price = candles[i].Low;
                    }
                }

                return price;
            }

            if (side == Side.Sell)
            {
                //  We find the maximum for the time from the opening of the transaction to the current one
                decimal price = 0;
                int indexIntro = 0;
                DateTime openPositionTime = positionCreateTime;

                if (openPositionTime == DateTime.MinValue)
                {
                    openPositionTime = candles[index - 1].TimeStart;
                }

                for (int i = index; i > 0; i--)
                {
                    // Look at the index of the candle, after which the opening of the pose occurred
                    if (candles[i].TimeStart <= openPositionTime)
                    {
                        indexIntro = i;
                        break;
                    }
                }

                for (int i = indexIntro; i > 0 && i > indexIntro - _stopCandles.ValueInt; i--)
                {
                    // Looking at the maximum high
                    if (candles[i].High > price)
                    {
                        price = candles[i].High;
                    }
                }

                return price;
            }

            return 0;
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