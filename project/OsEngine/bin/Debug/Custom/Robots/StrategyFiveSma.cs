/*
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

The trend robot on of five Sma

Buy:
All Smas are rising (when all five moving averages are larger than they were one bar ago) + 
half of the difference between the high and low of the previous bar.

Sell:
All Smas fall (when all five moving averages are less than they were one bar ago) - 
half the difference between the high and low of the previous bar.

Exit from buy:
Sma1, Sma2 and Sma3 are falling.

Exit from sell:
Sma1, Sma2 and Sma3 are growing.
*/

namespace OsEngine.Robots
{
    [Bot("StrategyFiveSma")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    public class StrategyFiveSma : BotPanel
    {
        // Reference to the main trading tab
        public BotTabSimple _tab;

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
        private StrategyParameterInt _periodSma1;
        private StrategyParameterInt _periodSma2;
        private StrategyParameterInt _periodSma3;
        private StrategyParameterInt _periodSma4;
        private StrategyParameterInt _periodSma5;

        // Indicators
        private Aindicator _sma1;
        private Aindicator _sma2;
        private Aindicator _sma3;
        private Aindicator _sma4;
        private Aindicator _sma5;

        // The last value of the indicators
        private decimal _lastSma1;
        private decimal _lastSma2;
        private decimal _lastSma3;
        private decimal _lastSma4;
        private decimal _lastSma5;

        // The previous value of the indicators
        private decimal _prevSma1;
        private decimal _prevSma2;
        private decimal _prevSma3;
        private decimal _prevSma4;
        private decimal _prevSma5;

        public StrategyFiveSma(string name, StartProgram startProgram) : base(name, startProgram)
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
            _periodSma1 = CreateParameter("Period SMA1", 50, 10, 300, 1, "Indicator");
            _periodSma2 = CreateParameter("Period SMA2", 100, 10, 300, 1, "Indicator");
            _periodSma3 = CreateParameter("Period SMA3", 150, 10, 300, 1, "Indicator");
            _periodSma4 = CreateParameter("Period SMA4", 200, 10, 300, 1, "Indicator");
            _periodSma5 = CreateParameter("Period SMA5", 250, 10, 300, 1, "Indicator");

            // Create indicator Sma1
            _sma1 = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma1", false);
            _sma1 = (Aindicator)_tab.CreateCandleIndicator(_sma1, "Prime");
            _sma1.DataSeries[0].Color = System.Drawing.Color.Blue;
            ((IndicatorParameterInt)_sma1.Parameters[0]).ValueInt = _periodSma1.ValueInt;
            _sma1.Save();

            // Create indicator Sma2
            _sma2 = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma2", false);
            _sma2 = (Aindicator)_tab.CreateCandleIndicator(_sma2, "Prime");
            _sma2.DataSeries[0].Color = System.Drawing.Color.Pink;
            ((IndicatorParameterInt)_sma2.Parameters[0]).ValueInt = _periodSma2.ValueInt;
            _sma2.Save();

            // Create indicator Sma3
            _sma3 = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma3", false);
            _sma3 = (Aindicator)_tab.CreateCandleIndicator(_sma3, "Prime");
            _sma3.DataSeries[0].Color = System.Drawing.Color.Yellow;
            ((IndicatorParameterInt)_sma3.Parameters[0]).ValueInt = _periodSma3.ValueInt;
            _sma3.Save();

            // Create indicator Sma4
            _sma4 = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma4", false);
            _sma4 = (Aindicator)_tab.CreateCandleIndicator(_sma4, "Prime");
            _sma4.DataSeries[0].Color = System.Drawing.Color.Gray;
            ((IndicatorParameterInt)_sma4.Parameters[0]).ValueInt = _periodSma4.ValueInt;
            _sma4.Save();

            // Create indicator Sma5
            _sma5 = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma5", false);
            _sma5 = (Aindicator)_tab.CreateCandleIndicator(_sma5, "Prime");
            _sma5.DataSeries[0].Color = System.Drawing.Color.Green;
            ((IndicatorParameterInt)_sma5.Parameters[0]).ValueInt = _periodSma5.ValueInt;
            _sma5.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyFiveSma_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel252;
        }

        // Indicator Update event
        private void StrategyFiveSma_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_sma1.Parameters[0]).ValueInt = _periodSma1.ValueInt;
            _sma1.Save();
            _sma1.Reload();

            ((IndicatorParameterInt)_sma2.Parameters[0]).ValueInt = _periodSma2.ValueInt;
            _sma2.Save();
            _sma2.Reload();

            ((IndicatorParameterInt)_sma3.Parameters[0]).ValueInt = _periodSma3.ValueInt;
            _sma3.Save();
            _sma3.Reload();

            ((IndicatorParameterInt)_sma4.Parameters[0]).ValueInt = _periodSma4.ValueInt;
            _sma4.Save();
            _sma4.Reload();

            ((IndicatorParameterInt)_sma5.Parameters[0]).ValueInt = _periodSma5.ValueInt;
            _sma5.Save();
            _sma5.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyFiveSma";
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
            if (candles.Count < _periodSma1.ValueInt
                || candles.Count < _periodSma2.ValueInt
                || candles.Count < _periodSma3.ValueInt
                || candles.Count < _periodSma4.ValueInt
                || candles.Count < _periodSma5.ValueInt)
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
                // We find the last value and the penultimate value of the indicator
                _prevSma1 = _sma1.DataSeries[0].Values[_sma1.DataSeries[0].Values.Count - 2];
                _prevSma2 = _sma2.DataSeries[0].Values[_sma2.DataSeries[0].Values.Count - 2];
                _prevSma3 = _sma3.DataSeries[0].Values[_sma3.DataSeries[0].Values.Count - 2];
                _prevSma4 = _sma4.DataSeries[0].Values[_sma4.DataSeries[0].Values.Count - 2];
                _prevSma5 = _sma5.DataSeries[0].Values[_sma5.DataSeries[0].Values.Count - 2];
                _lastSma1 = _sma1.DataSeries[0].Last;
                _lastSma2 = _sma2.DataSeries[0].Last;
                _lastSma3 = _sma3.DataSeries[0].Last;
                _lastSma4 = _sma4.DataSeries[0].Last;
                _lastSma5 = _sma5.DataSeries[0].Last;

                decimal high = candles[candles.Count - 1].High;
                decimal low = candles[candles.Count - 1].Low;
                decimal highminuslow = (high - low) / 2;

                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastSma1 > _prevSma1 + highminuslow
                        && _lastSma2 > _prevSma2 + highminuslow
                        && _lastSma3 > _prevSma3 + highminuslow
                        && _lastSma4 > _prevSma4 + highminuslow
                        && _lastSma5 > _prevSma5 + highminuslow)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastSma1 < _prevSma1 - highminuslow
                        && _lastSma2 < _prevSma2 - highminuslow
                        && _lastSma3 < _prevSma3 - highminuslow
                        && _lastSma4 < _prevSma4 - highminuslow
                        && _lastSma5 < _prevSma5 - highminuslow)
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

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                // We find the last value and the penultimate value of the indicator
                _prevSma1 = _sma1.DataSeries[0].Values[_sma1.DataSeries[0].Values.Count - 2];
                _prevSma2 = _sma2.DataSeries[0].Values[_sma2.DataSeries[0].Values.Count - 2];
                _prevSma3 = _sma3.DataSeries[0].Values[_sma3.DataSeries[0].Values.Count - 2];
                _lastSma1 = _sma1.DataSeries[0].Last;
                _lastSma2 = _sma2.DataSeries[0].Last;
                _lastSma3 = _sma3.DataSeries[0].Last;

                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is long
                {
                    if (_lastSma1 < _prevSma1
                        && _lastSma2 < _prevSma2
                        && _lastSma3 < _prevSma3)
                    {
                        decimal lastPrice = candles[candles.Count - 1].Close;
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (_lastSma1 > _prevSma1
                        && _lastSma2 > _prevSma2
                        && _lastSma3 > _prevSma3)
                    {
                        decimal lastPrice = candles[candles.Count - 1].Close;
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