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

The trend robot on Strategy MACD and ADX.

Buy:
1. MACD histogram > 0 or rising;
2. Adx > 20 and growing.

Sell:
1. MACD histogram < 0 or falling;
2. Adx > 20 and growing.

Exit:
From buy: MACD histogram <0 or falling.
From sell: MACD histogram> 0 or rising.
*/

namespace OsEngine.Robots
{
    [Bot("StrategyMACDandADX")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    public class StrategyMACDandADX : BotPanel
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
        private StrategyParameterInt _periodADX;
        private StrategyParameterInt _fastLineLengthMACD;
        private StrategyParameterInt _slowLineLengthMACD;
        private StrategyParameterInt _signalLineLengthMACD;

        // Indicators
        private Aindicator _ADX;
        private Aindicator _MACD;

        // The last value of the indicator
        private decimal _lastADX;
        private decimal _lastMACD;

        // The prev value of the indicator
        private decimal _prevADX;
        private decimal _prevMACD;

        public StrategyMACDandADX(string name, StartProgram startProgram) : base(name, startProgram)
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
            _periodADX = CreateParameter("ADX Length", 21, 7, 48, 7, "Indicator");
            _fastLineLengthMACD = CreateParameter("MACD Fast Length", 16, 10, 300, 7, "Indicator");
            _slowLineLengthMACD = CreateParameter("MACD Slow Length", 32, 10, 300, 10, "Indicator");
            _signalLineLengthMACD = CreateParameter("MACD Signal Length", 8, 10, 300, 10, "Indicator");

            // Create indicator ADX
            _ADX = IndicatorsFactory.CreateIndicatorByName("ADX", name + "ADX", false);
            _ADX = (Aindicator)_tab.CreateCandleIndicator(_ADX, "NewArea");
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = _periodADX.ValueInt;
            _ADX.Save();

            // Create indicator MACD
            _MACD = IndicatorsFactory.CreateIndicatorByName("MACD", name + "MACD", false);
            _MACD = (Aindicator)_tab.CreateCandleIndicator(_MACD, "NewArea0");
            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = _fastLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = _slowLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = _signalLineLengthMACD.ValueInt;
            _MACD.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyMACDandADX_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel256;
        }

        private void StrategyMACDandADX_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = _periodADX.ValueInt;
            _ADX.Save();
            _ADX.Reload();

            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = _fastLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = _slowLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = _signalLineLengthMACD.ValueInt;
            _MACD.Save();
            _MACD.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyMACDandADX";
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
            if (candles.Count < _periodADX.ValueInt ||
                candles.Count < _fastLineLengthMACD.ValueInt ||
                candles.Count < _slowLineLengthMACD.ValueInt ||
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
            _lastADX = _ADX.DataSeries[0].Last;
            _lastMACD = _MACD.DataSeries[0].Last;

            // The prev value of the indicator
            _prevADX = _ADX.DataSeries[0].Values[_ADX.DataSeries[0].Values.Count - 2];
            _prevMACD = _MACD.DataSeries[0].Values[_MACD.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    bool condition = _lastMACD > 0 && _lastADX > 20 && _prevADX < _lastADX;
                    bool condition1 = _lastMACD > _prevMACD && _prevADX < _lastADX && _lastADX > 20;

                    if (condition || condition1)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                //If the position is already open - exit
                if (openPositions.Count != 0)
                {
                    return;
                }

                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    bool condition = _lastMACD < 0 && _lastADX > 20 && _prevADX < _lastADX;
                    bool condition1 = _lastMACD < _prevMACD && _prevADX < _lastADX && _lastADX > 20;

                    if (condition || condition1)
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

            // The last value of the indicator
            _lastMACD = _MACD.DataSeries[0].Last;

            // The prev value of the indicator
            _prevMACD = _MACD.DataSeries[0].Values[_MACD.DataSeries[0].Values.Count - 2];

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is long
                {
                    if (_lastMACD < 0 || _lastMACD < _prevMACD)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (_lastMACD > 0 || _lastMACD > _prevMACD)
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