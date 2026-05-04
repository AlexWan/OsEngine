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
using System.Linq;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
Trading robot for osengine.

Trend robot on channel breakdown from Linear Regression Line and ADX.

Buy: 
1. The price is above the upper line of the channel.
2. Adx is growing and crosses level 20 from bottom to top.

Sale:
1. The price is below the bottom line of the channel.
2. Adx is growing and crosses level 20 from bottom to top.

Exit: 
After a certain number of candles.
 */

namespace OsEngine.Robots
{
    [Bot("BreakChannelLinearRegressionLineAndADX")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    public class BreakChannelLinearRegressionLineAndADX : BotPanel
    {
        // Reference to the main trading tab
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterString _orderType;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator Settings
        private StrategyParameterInt _periodADX;
        private StrategyParameterInt _periodLRMAChannel;

        // Indicators
        private Aindicator _ADX;
        private Aindicator _LRMAUp;
        private Aindicator _LRMADown;

        //The last value of the indicators
        private decimal _lastADX;
        private decimal _prevADX;
        private decimal _lastUpLine;
        private decimal _lastDownLine;

        // Exit setting
        private StrategyParameterInt _exitCandles;

        // Non trade periods
        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodsShowDialogButton;

        public BreakChannelLinearRegressionLineAndADX(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // non trade periods
            _tradePeriodsSettings = new NonTradePeriods(name);

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1Start = new TimeOfDay() { Hour = 0, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1End = new TimeOfDay() { Hour = 10, Minute = 05 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1OnOff = true;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2Start = new TimeOfDay() { Hour = 13, Minute = 54 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2End = new TimeOfDay() { Hour = 14, Minute = 6 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2OnOff = false;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3Start = new TimeOfDay() { Hour = 18, Minute = 1 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3End = new TimeOfDay() { Hour = 23, Minute = 58 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3OnOff = true;

            _tradePeriodsSettings.TradeInSunday = false;
            _tradePeriodsSettings.TradeInSaturday = false;

            _tradePeriodsSettings.Load();

            // Create and assign the main trading tab
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _orderType = CreateParameter("Order type", "Market", new[] { "Market", "Limit" }, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Base");
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4, "Base");
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Base");

            // Indicator Settings
            _periodADX = CreateParameter("Period ADX", 15, 50, 300, 10, "Indicator");
            _periodLRMAChannel = CreateParameter("Period LRMA Channel", 21, 7, 48, 7, "Indicator");

            // non trade period button
            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods", "Base");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;

            // Creating an indicator ADX
            _ADX = IndicatorsFactory.CreateIndicatorByName("ADX", name + "ADX", false);
            _ADX = (Aindicator)_tab.CreateCandleIndicator(_ADX, "NewArea");
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = _periodADX.ValueInt;
            _ADX.Save();

            // Creating an indicator LRMA1
            _LRMAUp = IndicatorsFactory.CreateIndicatorByName("LinearRegressionLine", name + "_LRMAUp", false);
            _LRMAUp = (Aindicator)_tab.CreateCandleIndicator(_LRMAUp, "Prime");
            ((IndicatorParameterInt)_LRMAUp.Parameters[0]).ValueInt = _periodLRMAChannel.ValueInt;
            ((IndicatorParameterString)_LRMAUp.Parameters[1]).ValueString = "High";
            _LRMAUp.Save();

            // Creating an indicator LRMA2
            _LRMADown = IndicatorsFactory.CreateIndicatorByName("LinearRegressionLine", name + "_LRMADown", false);
            _LRMADown = (Aindicator)_tab.CreateCandleIndicator(_LRMADown, "Prime");
            ((IndicatorParameterInt)_LRMADown.Parameters[0]).ValueInt = _periodLRMAChannel.ValueInt;
            ((IndicatorParameterString)_LRMADown.Parameters[1]).ValueString = "Low";
            _LRMADown.Save();

            // Exit setting
            _exitCandles = CreateParameter("Exit Candles", 10, 5, 1000, 10, "Exit settings");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakChannelLinearRegressionLineAndADX_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel142;
        }

        // non trade period button click
        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        // Indicators Update event
        private void BreakChannelLinearRegressionLineAndADX_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = _periodADX.ValueInt;
            _ADX.Save();
            _ADX.Reload();

            ((IndicatorParameterInt)_LRMAUp.Parameters[0]).ValueInt = _periodLRMAChannel.ValueInt;
            _LRMAUp.Save();
            _LRMAUp.Reload();

            ((IndicatorParameterInt)_LRMADown.Parameters[0]).ValueInt = _periodLRMAChannel.ValueInt;
            _LRMADown.Save();
            _LRMADown.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakChannelLinearRegressionLineAndADX";
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
            if (candles.Count < _periodADX.ValueInt || candles.Count < _periodLRMAChannel.ValueInt)
            {
                return;
            }

            // If the time does not match, we exit
            if (_tradePeriodsSettings.CanTradeThisTime(candles[^1].TimeStart) == false)
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
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // The prev value of the indicator
                _prevADX = _ADX.DataSeries[0].Values[_ADX.DataSeries[0].Values.Count - 2];

                // The last value of the indicators               
                _lastADX = _ADX.DataSeries[0].Last;
                _lastUpLine = _LRMAUp.DataSeries[0].Last;
                _lastDownLine = _LRMADown.DataSeries[0].Last;

                // Long
                if (_regime.ValueString != "OnlyShort") // if the mode is not only short, then we enter long
                {
                    if (lastPrice > _lastUpLine
                        && _prevADX < _lastADX && _lastADX > 20)
                    {
                        var time = candles.Last().TimeStart;

                        if (_orderType == "Limit")
                        {
                            _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage, time.ToString());
                        }
                        else
                        {
                            _tab.BuyAtMarket(GetVolume(_tab), time.ToString());
                        }
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // if the mode is not only long, we enter the short
                {
                    if (lastPrice < _lastDownLine
                        && _prevADX < _lastADX && _lastADX > 20)
                    {
                        var time = candles.Last().TimeStart;

                        if (_orderType == "Limit")
                        {
                            _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage, time.ToString());
                        }
                        else
                        {
                            _tab.SellAtMarket(GetVolume(_tab), time.ToString());
                        }
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

                if (position.Direction == Side.Buy) // If the direction of the position is long
                {
                    if (_orderType == "Limit")
                    {
                        _tab.CloseAtLimit(position, lastPrice - _slippage, position.OpenVolume);
                    }
                    else
                    {
                        _tab.CloseAtMarket(position, position.OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (_orderType == "Limit")
                    {
                        _tab.CloseAtLimit(position, lastPrice + _slippage, position.OpenVolume);
                    }
                    else
                    {
                        _tab.CloseAtMarket(position, position.OpenVolume);
                    }
                }

            }
        }

        private bool NeedClosePosition(Position position, List<Candle> candles)
        {
            if (position == null || position.OpenVolume == 0)
            {
                return false;
            }

            DateTime openTime = position.TimeOpen;

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