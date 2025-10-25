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

/*Discription
Trading robot for osengine

Trend robot at the Strategy Rsi And Two LRMA.

Buy:
1. Fast LRMA crosses slow one from bottom to top.
2. The RSI is above 50 and rising.
Sell:
1. The fast LRMA crosses the slow one from top to bottom.
2. The RSI is above 50 and rising.
Exit: stop and profit in % of the entry price.
*/

namespace OsEngine.Robots
{
    [Bot("StrategyRsiAndTwoLRMA")] //We create an attribute so that we don't write anything in the Boot factory
    public class StrategyRsiAndTwoLRMA : BotPanel
    {
        BotTabSimple _tab;

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
        private StrategyParameterInt _periodFastLRMA;
        private StrategyParameterInt _periodSlowLRMA;
        private StrategyParameterInt _periodRSI;

        // Indicator
        private Aindicator _fastLRMA;
        private Aindicator _slowLRMA;
        private Aindicator _rsi;

        // The last value of the indicators
        private decimal _lastFastLRMA;
        private decimal _lastSlowLRMA;
        private decimal _lastRSI;

        // The prev value of the indicator
        private decimal _prevRSI;
        private decimal _prevFastLRMA;
        private decimal _prevSlowLRMA;

        // Exit settings
        private StrategyParameterDecimal _stopValue;
        private StrategyParameterDecimal _profitValue;

        public StrategyRsiAndTwoLRMA(string name, StartProgram startProgram) : base(name, startProgram)
        {
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
            _periodFastLRMA = CreateParameter("period Fast LRMA", 14, 5, 50, 5, "Indicator");
            _periodSlowLRMA = CreateParameter("period Slow LRMA", 24, 10, 100, 10, "Indicator");
            _periodRSI = CreateParameter("Period RSI", 14, 10, 300, 1, "Indicator");

            // Creating indicator Fast LRMA
            _fastLRMA = IndicatorsFactory.CreateIndicatorByName("LinearRegressionLine", name + "LinearRegressionLine1", false);
            _fastLRMA = (Aindicator)_tab.CreateCandleIndicator(_fastLRMA, "Prime");
            ((IndicatorParameterInt)_fastLRMA.Parameters[0]).ValueInt = _periodFastLRMA.ValueInt;
            _fastLRMA.DataSeries[0].Color = Color.Red;
            _fastLRMA.Save();

            // Creating indicator Slow LRMA
            _slowLRMA = IndicatorsFactory.CreateIndicatorByName("LinearRegressionLine", name + "LinearRegressionLine2", false);
            _slowLRMA = (Aindicator)_tab.CreateCandleIndicator(_slowLRMA, "Prime");
            ((IndicatorParameterInt)_slowLRMA.Parameters[0]).ValueInt = _periodSlowLRMA.ValueInt;
            _slowLRMA.DataSeries[0].Color = Color.Green;
            _slowLRMA.Save();

            // Create indicator RSI
            _rsi = IndicatorsFactory.CreateIndicatorByName("RSI", name + "RSI", false);
            _rsi = (Aindicator)_tab.CreateCandleIndicator(_rsi, "NewArea");
            ((IndicatorParameterInt)_rsi.Parameters[0]).ValueInt = _periodRSI.ValueInt;
            _rsi.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyRsiAndTwoLRMA_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Exit settings
            _stopValue = CreateParameter("Stop", 0.5m, 1, 10, 1, "Exit settings");
            _profitValue = CreateParameter("Profit", 0.5m, 1, 10, 1, "Exit settings");

            Description = OsLocalization.Description.DescriptionLabel272;
        }

        // Indicator Update event
        private void StrategyRsiAndTwoLRMA_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_fastLRMA.Parameters[0]).ValueInt = _periodFastLRMA.ValueInt;
            _fastLRMA.Save();
            _fastLRMA.Reload();
            ((IndicatorParameterInt)_slowLRMA.Parameters[0]).ValueInt = _periodSlowLRMA.ValueInt;
            _slowLRMA.Save();
            _slowLRMA.Reload();
            ((IndicatorParameterInt)_rsi.Parameters[0]).ValueInt = _periodRSI.ValueInt;
            _rsi.Save();
            _rsi.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyRsiAndTwoLRMA";
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
            if (candles.Count < _periodFastLRMA.ValueInt || candles.Count < _periodSlowLRMA.ValueInt)
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
                // The last value of the indicators
                _lastFastLRMA = _fastLRMA.DataSeries[0].Last;
                _lastSlowLRMA = _slowLRMA.DataSeries[0].Last;
                _lastRSI = _rsi.DataSeries[0].Last;

                // The prev value of the indicator
                _prevRSI = _rsi.DataSeries[0].Values[_rsi.DataSeries[0].Values.Count - 2];
                _prevFastLRMA = _fastLRMA.DataSeries[0].Values[_fastLRMA.DataSeries[0].Values.Count - 2];
                _prevSlowLRMA = _slowLRMA.DataSeries[0].Values[_slowLRMA.DataSeries[0].Values.Count - 2];

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_prevFastLRMA < _prevSlowLRMA && _lastFastLRMA > _lastSlowLRMA && _lastRSI > 50 && _lastRSI > _prevRSI)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_prevFastLRMA > _prevSlowLRMA && _lastFastLRMA < _lastSlowLRMA && _lastRSI > 50 && _lastRSI > _prevRSI)
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
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is long
                {
                    decimal profitActivation = pos.EntryPrice + pos.EntryPrice * _profitValue.ValueDecimal / 100;
                    decimal stopActivation = pos.EntryPrice - pos.EntryPrice * _stopValue.ValueDecimal / 100;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation + _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation - _slippage);
                }
                else // If the direction of the position is short
                {
                    decimal profitActivation = pos.EntryPrice - pos.EntryPrice * _profitValue.ValueDecimal / 100;
                    decimal stopActivation = pos.EntryPrice + pos.EntryPrice * _stopValue.ValueDecimal / 100;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation - _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation + _slippage);
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