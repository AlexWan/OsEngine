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

The trend robot on strategy for 4 Ema, Awesome Oscillator and Macd Histogram.

Buy:
 1. EmaFastLoc above EmaSlowLoc;
 2. EmaFastGlob above EmaSlowGlob;
 3. AO growing;
 4. Macd > 0.

Sell:
 1. EmaFastLoc below EmaSlowLoc;
 2. EmaFastGlob below EmaSlowGlob;
 3. AO falling;
 4. Macd < 0.

Exit from buy:
trailing stop in % of the low of the candle on which you entered.

Exit from sell:
trailing stop in % of the high of the candle on which you entered.
*/

namespace OsEngine.Robots
{
    [Bot("StrategyForFourEmaAOAndMacdHistogram")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    public class StrategyForFourEmaAOAndMacdHistogram : BotPanel
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

        // Indicators settings
        private StrategyParameterInt _periodEmaFastLoc;
        private StrategyParameterInt _periodEmaSlowLoc;
        private StrategyParameterInt _periodEmaFastGlob;
        private StrategyParameterInt _periodEmaSlowGlob;
        private StrategyParameterInt _fastLineLengthMacd;
        private StrategyParameterInt _slowLineLengthMacd;
        private StrategyParameterInt _signalLineLengthMacd;
        private StrategyParameterInt _fastLineLengthAO;
        private StrategyParameterInt _slowLineLengthAO;

        // Indicators
        private Aindicator _macd;
        private Aindicator _AO;
        private Aindicator _emaFastLoc;
        private Aindicator _emaSlowLoc;
        private Aindicator _emaFastGlob;
        private Aindicator _emaSlowGlob;

        // The last value of the indicators
        private decimal _lastEmaFastLoc;
        private decimal _lastEmaSlowLoc;
        private decimal _lastEmaFastGlob;
        private decimal _lastEmaSlowGlob;
        private decimal _lastAO;
        private decimal _lastMacd;

        // The prevlast value of the indicator
        private decimal _prevAO;

        // Exit setting
        private StrategyParameterDecimal _trailingValue;

        public StrategyForFourEmaAOAndMacdHistogram(string name, StartProgram startProgram) : base(name, startProgram)
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

            // Indicators settings
            _periodEmaFastLoc = CreateParameter("Period Ema Fast Loc", 36, 10, 300, 10, "Indicator");
            _periodEmaSlowLoc = CreateParameter("Period Ema Slow Loc", 44, 10, 300, 10, "Indicator");
            _periodEmaFastGlob = CreateParameter("Period Ema Fast Glob", 144, 10, 300, 10, "Indicator");
            _periodEmaSlowGlob = CreateParameter("Period Ema Slow Glob", 176, 10, 300, 10, "Indicator");
            _fastLineLengthMacd = CreateParameter("Fast Line Length Macd", 16, 10, 300, 10, "Indicator");
            _slowLineLengthMacd = CreateParameter("Slow Line Length Macd", 32, 10, 300, 10, "Indicator");
            _signalLineLengthMacd = CreateParameter("Signal Line Length Macd", 8, 10, 300, 10, "Indicator");
            _fastLineLengthAO = CreateParameter("Fast Line Length AO", 13, 10, 300, 10, "Indicator");
            _slowLineLengthAO = CreateParameter("Slow Line Length AO", 26, 10, 300, 10, "Indicator");

            // Create indicator EmaFastLoc
            _emaFastLoc = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema One Loc", false);
            _emaFastLoc = (Aindicator)_tab.CreateCandleIndicator(_emaFastLoc, "Prime");
            ((IndicatorParameterInt)_emaFastLoc.Parameters[0]).ValueInt = _periodEmaFastLoc.ValueInt;
            _emaFastLoc.DataSeries[0].Color = Color.Blue;
            _emaFastLoc.Save();

            // Create indicator EmaSlowLoc
            _emaSlowLoc = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema Two Loc", false);
            _emaSlowLoc = (Aindicator)_tab.CreateCandleIndicator(_emaSlowLoc, "Prime");
            ((IndicatorParameterInt)_emaSlowLoc.Parameters[0]).ValueInt = _periodEmaSlowLoc.ValueInt;
            _emaSlowLoc.DataSeries[0].Color = Color.Yellow;
            _emaSlowLoc.Save();

            // Create indicator EmaFastGlob
            _emaFastGlob = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema One Glob", false);
            _emaFastGlob = (Aindicator)_tab.CreateCandleIndicator(_emaFastGlob, "Prime");
            ((IndicatorParameterInt)_emaFastGlob.Parameters[0]).ValueInt = _periodEmaFastGlob.ValueInt;
            _emaFastGlob.DataSeries[0].Color = Color.Green;
            _emaFastGlob.Save();

            // Create indicator EmaSlowGlob
            _emaSlowGlob = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema Two Glob", false);
            _emaSlowGlob = (Aindicator)_tab.CreateCandleIndicator(_emaSlowGlob, "Prime");
            ((IndicatorParameterInt)_emaSlowGlob.Parameters[0]).ValueInt = _periodEmaSlowGlob.ValueInt;
            _emaSlowGlob.DataSeries[0].Color = Color.Red;
            _emaSlowGlob.Save();

            // Create indicator Macd
            _macd = IndicatorsFactory.CreateIndicatorByName("MACD", name + "Macd", false);
            _macd = (Aindicator)_tab.CreateCandleIndicator(_macd, "NewArea");
            ((IndicatorParameterInt)_macd.Parameters[0]).ValueInt = _fastLineLengthMacd.ValueInt;
            ((IndicatorParameterInt)_macd.Parameters[1]).ValueInt = _slowLineLengthMacd.ValueInt;
            ((IndicatorParameterInt)_macd.Parameters[2]).ValueInt = _signalLineLengthMacd.ValueInt;
            _macd.Save();

            // Create indicator AO
            _AO = IndicatorsFactory.CreateIndicatorByName("AO", name + "AO", false);
            _AO = (Aindicator)_tab.CreateCandleIndicator(_AO, "NewArea1");
            ((IndicatorParameterInt)_AO.Parameters[0]).ValueInt = _fastLineLengthAO.ValueInt;
            ((IndicatorParameterInt)_AO.Parameters[1]).ValueInt = _slowLineLengthAO.ValueInt;
            _AO.Save();

            // Exit setting
            _trailingValue = CreateParameter("TrailingValue", 1.0m, 1, 10, 1, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyForFourEmaAOAndMacdHistogram_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel253;
        }

        // Indicator Update event
        private void StrategyForFourEmaAOAndMacdHistogram_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_emaFastLoc.Parameters[0]).ValueInt = _periodEmaFastLoc.ValueInt;
            _emaFastLoc.Save();
            _emaFastLoc.Reload();

            ((IndicatorParameterInt)_emaSlowLoc.Parameters[0]).ValueInt = _periodEmaSlowLoc.ValueInt;
            _emaSlowLoc.Save();
            _emaSlowLoc.Reload();

            ((IndicatorParameterInt)_emaFastGlob.Parameters[0]).ValueInt = _periodEmaFastGlob.ValueInt;
            _emaFastGlob.Save();
            _emaFastGlob.Reload();

            ((IndicatorParameterInt)_emaSlowGlob.Parameters[0]).ValueInt = _periodEmaSlowGlob.ValueInt;
            _emaSlowGlob.Save();
            _emaSlowGlob.Reload();

            ((IndicatorParameterInt)_macd.Parameters[0]).ValueInt = _fastLineLengthMacd.ValueInt;
            ((IndicatorParameterInt)_macd.Parameters[1]).ValueInt = _slowLineLengthMacd.ValueInt;
            ((IndicatorParameterInt)_macd.Parameters[2]).ValueInt = _signalLineLengthMacd.ValueInt;
            _macd.Save();
            _macd.Reload();

            ((IndicatorParameterInt)_AO.Parameters[0]).ValueInt = _fastLineLengthAO.ValueInt;
            ((IndicatorParameterInt)_AO.Parameters[1]).ValueInt = _slowLineLengthAO.ValueInt;
            _AO.Save();
            _AO.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyForFourEmaAOAndMacdHistogram";
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
            if (candles.Count < _periodEmaSlowGlob.ValueInt)
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
                _lastEmaFastLoc = _emaFastLoc.DataSeries[0].Last;
                _lastEmaSlowLoc = _emaSlowLoc.DataSeries[0].Last;
                _lastEmaFastGlob = _emaFastGlob.DataSeries[0].Last;
                _lastEmaSlowGlob = _emaSlowGlob.DataSeries[0].Last;
                _lastAO = _AO.DataSeries[0].Last;
                _lastMacd = _macd.DataSeries[0].Last;

                // The prevlast value of the indicator
                _prevAO = _AO.DataSeries[0].Values[_AO.DataSeries[0].Values.Count - 2];

                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastEmaFastLoc > _lastEmaSlowLoc &&
                        _lastEmaFastGlob > _lastEmaSlowGlob &&
                        _lastAO > _prevAO &&
                        _lastMacd > 0)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastEmaFastLoc < _lastEmaSlowLoc &&
                    _lastEmaFastGlob < _lastEmaSlowGlob &&
                    _lastAO < _prevAO &&
                    _lastMacd < 0)
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

                // Stop Price
                decimal stopPrice;

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