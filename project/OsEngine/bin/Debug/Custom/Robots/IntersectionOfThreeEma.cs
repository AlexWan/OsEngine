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
using System.Drawing;
using OsEngine.Market.Servers;
using OsEngine.Market;

/*Discription
Trading robot for osengine.

Trend robot at the intersection of three exponential averages.

Buy: Fast Ema is higher than slow Ema.

Sell: Fast Ema is lower than slow Ema.

Exit from the buy: trailing stop in % of the loy of the candle on which you entered.

Exit from sale: trailing stop in % of the high of the candle on which you entered.
*/

namespace OsEngine.Robots
{
    [Bot("IntersectionOfThreeEma")]//We create an attribute so that we don't write anything in the Boot factory
    class IntersectionOfThreeEma : BotPanel
    {
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

        // Indicator Settings  
        private StrategyParameterInt _periodEmaFast;
        private StrategyParameterInt _periodMiddle;
        private StrategyParameterInt _periodEmaSlow;

        // Indicator
        private Aindicator _ema1;
        private Aindicator _ema2;
        private Aindicator _ema3;

        // He last value of the indicators
        private decimal _lastEmaFast;
        private decimal _lastEmaMiddle;
        private decimal _lastEmaSlow;

        public IntersectionOfThreeEma(string name, StartProgram startProgram) : base(name, startProgram)
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
            _periodEmaFast = CreateParameter("fast EMA1 period", 100, 10, 300, 1, "Indicator");
            _periodMiddle = CreateParameter("middle EMA2 period", 200, 10, 300, 1, "Indicator");
            _periodEmaSlow = CreateParameter("slow EMA3 period", 300, 10, 300, 1, "Indicator");

            // Creating an indicator EmaFast
            _ema1 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema1", false);
            _ema1 = (Aindicator)_tab.CreateCandleIndicator(_ema1, "Prime");
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _periodEmaFast.ValueInt;
            _ema1.ParametersDigit[0].Value = _periodEmaFast.ValueInt;
            _ema1.DataSeries[0].Color = Color.Red;
            _ema1.Save();

            // Creating an indicator Middle
            _ema2 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema2", false);
            _ema2 = (Aindicator)_tab.CreateCandleIndicator(_ema2, "Prime");
            ((IndicatorParameterInt)_ema2.Parameters[0]).ValueInt = _periodMiddle.ValueInt;
            _ema2.ParametersDigit[0].Value = _periodMiddle.ValueInt;
            _ema2.DataSeries[0].Color = Color.Blue;
            _ema2.Save();

            // Creating an indicator EmaSlow
            _ema3 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema3", false);
            _ema3 = (Aindicator)_tab.CreateCandleIndicator(_ema3, "Prime");
            ((IndicatorParameterInt)_ema3.Parameters[0]).ValueInt = _periodEmaSlow.ValueInt;
            _ema3.ParametersDigit[0].Value = _periodEmaSlow.ValueInt;
            _ema3.DataSeries[0].Color = Color.Green;
            _ema3.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfThreeEma_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot at the intersection of three exponential averages. " +
                "Buy: Fast Ema is higher than slow Ema. " +
                "Sell: Fast Ema is lower than slow Ema. " +
                "Exit from the buy: trailing stop in % of the loy of the candle on which you entered. " +
                "Exit from sale: trailing stop in % of the high of the candle on which you entered.";
        }

        // Indicator Update event
        private void IntersectionOfThreeEma_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _periodEmaFast.ValueInt;
            _ema1.Save();
            _ema1.Reload();

            ((IndicatorParameterInt)_ema2.Parameters[0]).ValueInt = _periodMiddle.ValueInt;
            _ema2.Save();
            _ema2.Reload();

            ((IndicatorParameterInt)_ema3.Parameters[0]).ValueInt = _periodEmaSlow.ValueInt;
            _ema3.Save();
            _ema3.Reload();
        }

        // The name of the robot in OsEngin
        public override string GetNameStrategyType()
        {
            return "IntersectionOfThreeEma";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // Candle Completion Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _periodEmaFast.ValueInt || candles.Count < _periodMiddle.ValueInt || candles.Count < _periodEmaSlow.ValueInt)
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

            decimal lastPrice = candles[candles.Count - 1].Close;

            // He last value of the indicators
            _lastEmaFast = _ema1.DataSeries[0].Last;
            _lastEmaMiddle = _ema2.DataSeries[0].Last;
            _lastEmaSlow = _ema3.DataSeries[0].Last;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastEmaFast > _lastEmaMiddle && _lastEmaMiddle > _lastEmaSlow )
                    {
                        // We put a stop on the buy                       
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastEmaFast < _lastEmaMiddle && _lastEmaMiddle < _lastEmaSlow)
                    {
                        // Putting a stop on sale
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestAsk - _slippage);
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
            _lastEmaFast = _ema1.DataSeries[0].Last;
            _lastEmaMiddle = _ema2.DataSeries[0].Last;
            _lastEmaSlow = _ema3.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is long
                {
                    if (lastPrice < _lastEmaSlow)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (lastPrice > _lastEmaSlow)
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