﻿/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Strategy Ema And MACD.

Buy:
1. The price is above the Ema.
2. Macd is growing for at least 5 candles.

Sell:
1. The price is below the Ema.
2. Macd is falling for at least 5 candles.

Exit:
Exit from buy: trailing stop in % of the low of the candle on which you entered.
Exit from sell: trailing stop in % of the high of the candle on which you entered.
 */

namespace OsEngine.Robots
{
    [Bot("StrategyEmaAndMACD")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    public class StrategyEmaAndMACD : BotPanel
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
        private StrategyParameterInt _emaPeriod;
        private StrategyParameterInt _fastLineLengthMACD;
        private StrategyParameterInt _slowLineLengthMACD;
        private StrategyParameterInt _signalLineLengthMACD;

        // Indicator
        private Aindicator _ema;
        private Aindicator _MACD;

        // The last value of the indicator
        private decimal _lastEma;
        private decimal _onelastMACD;
        private decimal _twolastMACD;
        private decimal _threelastMACD;
        private decimal _fourlastMACD;
        private decimal _fivelastMACD;

        // Exit setting
        private StrategyParameterDecimal _trailingValue;

        public StrategyEmaAndMACD(string name, StartProgram startProgram) : base(name, startProgram)
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
            _emaPeriod = CreateParameter("Ema Length", 21, 7, 48, 7, "Indicator");
            _fastLineLengthMACD = CreateParameter("MACD Fast Length", 16, 10, 300, 7, "Indicator");
            _slowLineLengthMACD = CreateParameter("MACD Slow Length", 32, 10, 300, 10, "Indicator");
            _signalLineLengthMACD = CreateParameter("MACD Signal Length", 8, 10, 300, 10, "Indicator");

            // Create indicator Ema
            _ema = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMA", false);
            _ema = (Aindicator)_tab.CreateCandleIndicator(_ema, "Prime");
            ((IndicatorParameterInt)_ema.Parameters[0]).ValueInt = _emaPeriod.ValueInt;
            _ema.Save();

            // Create indicator MACD
            _MACD = IndicatorsFactory.CreateIndicatorByName("MACD", name + "MACD", false);
            _MACD = (Aindicator)_tab.CreateCandleIndicator(_MACD, "NewArea");
            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = _fastLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = _slowLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = _signalLineLengthMACD.ValueInt;
            _MACD.Save();

            // Exit setting
            _trailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyEmaAndMACD_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel244;
        }

        private void StrategyEmaAndMACD_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ema.Parameters[0]).ValueInt = _emaPeriod.ValueInt;
            _ema.Save();
            _ema.Reload();

            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = _fastLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = _slowLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = _signalLineLengthMACD.ValueInt;
            _MACD.Save();
            _MACD.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyEmaAndMACD";
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
            if (candles.Count < _emaPeriod.ValueInt ||
                candles.Count < _fastLineLengthMACD.ValueInt ||
                candles.Count < _slowLineLengthMACD.ValueInt + 6 ||
                candles.Count < _signalLineLengthMACD.ValueInt)
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
            _lastEma = _ema.DataSeries[0].Last;
            _onelastMACD = _MACD.DataSeries[0].Last;
            _twolastMACD = _MACD.DataSeries[0].Values[_MACD.DataSeries[0].Values.Count - 2];
            _threelastMACD = _MACD.DataSeries[0].Values[_MACD.DataSeries[0].Values.Count - 3];
            _fourlastMACD = _MACD.DataSeries[0].Values[_MACD.DataSeries[0].Values.Count - 4];
            _fivelastMACD = _MACD.DataSeries[0].Values[_MACD.DataSeries[0].Values.Count - 5];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice > _lastEma &&
                        _onelastMACD > _twolastMACD && 
                        _twolastMACD > _threelastMACD &&
                        _threelastMACD > _fourlastMACD &&
                        _fourlastMACD > _fivelastMACD)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastPrice < _lastEma &&
                        _onelastMACD < _twolastMACD &&
                        _twolastMACD < _threelastMACD &&
                        _threelastMACD < _fourlastMACD &&
                        _fourlastMACD < _fivelastMACD)
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

            decimal stopPrice;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is long
                {
                    decimal lov = candles[candles.Count - 1].Low;
                    stopPrice = lov - lov * _trailingValue.ValueDecimal / 100;
                }
                else // If the direction of the position is short
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * _trailingValue.ValueDecimal / 100;
                }
                _tab.CloseAtTrailingStop(pos, stopPrice, stopPrice);
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
