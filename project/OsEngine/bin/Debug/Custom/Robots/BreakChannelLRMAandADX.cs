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

/*Discription
Trading robot for osengine

Trend robot at the Break Channel LRMA and ADX.

Buy:
1. The price is above the upper line of the channel.
2. Adx is growing and crosses level 20 from bottom to top.

Sell:
1. The price is below the bottom line of the channel.
2. Adx is growing and crosses level 20 from bottom to top.

Exit: after a certain number of candles.
*/

namespace OsEngine.Robots
{
    [Bot("BreakChannelLRMAandADX")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    public class BreakChannelLRMAandADX : BotPanel
    {
        // Reference to the main trading tab
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

        // Indicator settings
        private StrategyParameterInt _periodLRMA;
        private StrategyParameterInt _periodADX;

        // Indicators
        private Aindicator _oneLRMA;
        private Aindicator _twoLRMA;
        private Aindicator _ADX;

        // The last value of the indicators
        private decimal _lastOneLRMA;
        private decimal _lastTwoLRMA;
        private decimal _lastADX;

        // The prev value of the indicator
        private decimal _prevADX;

        // Exit settings
        private StrategyParameterInt _exitCandles;

        public BreakChannelLRMAandADX(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
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
            _periodLRMA = CreateParameter("period LRMA", 14, 5, 50, 5, "Indicator");
            _periodADX = CreateParameter("Period ADX", 10, 10, 300, 10, "Indicator");

            // Creating indicator One LRMA
            _oneLRMA = IndicatorsFactory.CreateIndicatorByName("LinearRegressionLine", name + "LinearRegressionLine1", false);
            _oneLRMA = (Aindicator)_tab.CreateCandleIndicator(_oneLRMA, "Prime");
            ((IndicatorParameterInt)_oneLRMA.Parameters[0]).ValueInt = _periodLRMA.ValueInt;
            ((IndicatorParameterString)_oneLRMA.Parameters[1]).ValueString = "High";
            _oneLRMA.Save();

            // Creating indicator Two LRMA
            _twoLRMA = IndicatorsFactory.CreateIndicatorByName("LinearRegressionLine", name + "LinearRegressionLine2", false);
            _twoLRMA = (Aindicator)_tab.CreateCandleIndicator(_twoLRMA, "Prime");
            ((IndicatorParameterInt)_twoLRMA.Parameters[0]).ValueInt = _periodLRMA.ValueInt;
            ((IndicatorParameterString)_oneLRMA.Parameters[1]).ValueString = "Low";
            _twoLRMA.Save();

            // Create indicator ADX
            _ADX = IndicatorsFactory.CreateIndicatorByName("ADX", name + "ADX", false);
            _ADX = (Aindicator)_tab.CreateCandleIndicator(_ADX, "NewArea0");
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = _periodADX.ValueInt;
            _ADX.Save();

            // Exit settings
            _exitCandles = CreateParameter("Exit Candles", 10, 5, 1000, 10, "Exit settings");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakChannelLRMAandADX_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel143;
        }

        // Indicator Update event
        private void BreakChannelLRMAandADX_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_oneLRMA.Parameters[0]).ValueInt = _periodLRMA.ValueInt;
            _oneLRMA.Save();
            _oneLRMA.Reload();

            ((IndicatorParameterInt)_twoLRMA.Parameters[0]).ValueInt = _periodLRMA.ValueInt;
            _twoLRMA.Save();
            _twoLRMA.Reload();

            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = _periodADX.ValueInt;
            _ADX.Save();
            _ADX.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakChannelLRMAandADX";
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
            if (candles.Count < _periodLRMA.ValueInt || candles.Count < _periodADX.ValueInt)
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
                    // The last value of the indicators
                    _lastOneLRMA = _oneLRMA.DataSeries[0].Last;
                    _lastTwoLRMA = _twoLRMA.DataSeries[0].Last;
                    _lastADX = _ADX.DataSeries[0].Last;

                    // The prev value of the indicator
                    _prevADX = _ADX.DataSeries[0].Values[_ADX.DataSeries[0].Values.Count - 2];

                    if (lastPrice > _lastOneLRMA && _lastADX > _prevADX && _lastADX > 20 && _prevADX < 20)
                    {
                        var time = candles.Last().TimeStart;
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage, time.ToString());
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastPrice < _lastTwoLRMA && _lastADX > _prevADX && _lastADX > 20 && _prevADX < 20)
                    {
                        var time = candles.Last().TimeStart;
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage, time.ToString());
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
                    _tab.CloseAtLimit(position, lastPrice - _slippage, position.OpenVolume);
                }
                else // If the direction of the position is short
                {
                    _tab.CloseAtLimit(position, lastPrice + _slippage, position.OpenVolume);
                }
            }
        }

        private bool NeedClosePosition(Position position, List<Candle> candles)
        {
            if (position == null || position.OpenVolume == 0)
            {
                return false;
            }

            DateTime openTime = DateTime.Parse(position.SignalTypeOpen);

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