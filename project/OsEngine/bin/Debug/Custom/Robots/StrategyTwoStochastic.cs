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

Trend robot on the Strategy Two Stochastic.

Buy:
1. A fast stochastic is in the oversold zone or has just left it (below 30) and the stochastic line (blue) is above the signal line (red).
2. Slow stochastic in the oversold zone (below 20) and the stochastic line (blue) above the signal line (red).

Sell:
1. A fast stochastic is in the overbought zone or has just left it (above 70) and the stochastic line (blue) is below the signal line (red).
2. Slow stochastic in the overbought zone (above 80) and the stochastic line (blue) below the signal line (red).

Exit from buy: trailing stop in % of the loy of the candle on which you entered.
Exit from sell: trailing stop in % of the high of the candle on which you entered.
*/

namespace OsEngine.Robots
{
    [Bot("StrategyTwoStochastic")] //We create an attribute so that we don't write anything in the Boot factory
    public class StrategyTwoStochastic : BotPanel
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
        private StrategyParameterInt _fastStochasticPeriod1;
        private StrategyParameterInt _fastStochasticPeriod2;
        private StrategyParameterInt _fastStochasticPeriod3;
        private StrategyParameterInt _slowStochasticPeriod1;
        private StrategyParameterInt _slowStochasticPeriod2;
        private StrategyParameterInt _slowStochasticPeriod3;

        // Indicator
        private Aindicator _fastStochastic;
        private Aindicator _slowStochastic;

        // Exit setting
        private StrategyParameterDecimal _trailingValue;

        //The last value of the indicators
        private decimal _lastBlueStohFast;
        private decimal _lastRedStohFast;
        private decimal _lastBlueStohSlow;
        private decimal _lastRedStohSlow;

        public StrategyTwoStochastic(string name, StartProgram startProgram) : base(name, startProgram)
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
            _fastStochasticPeriod1 = CreateParameter("Fast Stochastic Period One", 10, 10, 300, 10, "Indicator");
            _fastStochasticPeriod2 = CreateParameter("Fast Stochastic Period Two", 20, 10, 300, 10, "Indicator");
            _fastStochasticPeriod3 = CreateParameter("Fast Stochastic Period Three", 30, 10, 300, 10, "Indicator");
            _slowStochasticPeriod1 = CreateParameter("Slow Stochastic Period One", 10, 10, 300, 10, "Indicator");
            _slowStochasticPeriod2 = CreateParameter("Slow Stochastic Period Two", 20, 10, 300, 10, "Indicator");
            _slowStochasticPeriod3 = CreateParameter("Slow Stochastic Period Three", 30, 10, 300, 10, "Indicator");

            // Create indicator Stochastic Fast
            _fastStochastic = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "StochasticFast", false);
            _fastStochastic = (Aindicator)_tab.CreateCandleIndicator(_fastStochastic, "NewArea0");
            ((IndicatorParameterInt)_fastStochastic.Parameters[0]).ValueInt = _fastStochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_fastStochastic.Parameters[1]).ValueInt = _fastStochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_fastStochastic.Parameters[2]).ValueInt = _fastStochasticPeriod3.ValueInt;
            _fastStochastic.Save();

            // Create indicator Stochastic Slow
            _slowStochastic = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "StochasticSlow", false);
            _slowStochastic = (Aindicator)_tab.CreateCandleIndicator(_slowStochastic, "NewArea");
            ((IndicatorParameterInt)_slowStochastic.Parameters[0]).ValueInt = _slowStochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_slowStochastic.Parameters[1]).ValueInt = _slowStochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_slowStochastic.Parameters[2]).ValueInt = _slowStochasticPeriod3.ValueInt;
            _slowStochastic.Save();

            // Exit setting
            _trailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyTwoStochastic_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel285;
        }

        // Indicator Update event
        private void StrategyTwoStochastic_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_fastStochastic.Parameters[0]).ValueInt = _fastStochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_fastStochastic.Parameters[1]).ValueInt = _fastStochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_fastStochastic.Parameters[2]).ValueInt = _fastStochasticPeriod3.ValueInt;
            _fastStochastic.Save();
            _fastStochastic.Reload();
            ((IndicatorParameterInt)_slowStochastic.Parameters[0]).ValueInt = _slowStochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_slowStochastic.Parameters[1]).ValueInt = _slowStochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_slowStochastic.Parameters[2]).ValueInt = _slowStochasticPeriod3.ValueInt;
            _slowStochastic.Save();
            _slowStochastic.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyTwoStochastic";
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
            if (candles.Count < _fastStochasticPeriod1.ValueInt ||
                candles.Count < _fastStochasticPeriod2.ValueInt ||
                candles.Count < _fastStochasticPeriod3.ValueInt ||
                candles.Count < _slowStochasticPeriod1.ValueInt ||
                candles.Count < _slowStochasticPeriod2.ValueInt ||
                candles.Count < _slowStochasticPeriod3.ValueInt)
            {
                return;
            }

            // If the time does not match, we exit
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

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // The last value of the indicators     
                _lastBlueStohFast = _fastStochastic.DataSeries[0].Last;
                _lastRedStohFast = _fastStochastic.DataSeries[1].Last;
                _lastBlueStohSlow = _slowStochastic.DataSeries[0].Last;
                _lastRedStohSlow = _slowStochastic.DataSeries[1].Last;

                decimal lastPrice = candles[candles.Count - 1].Close;

                // Long
                if (_regime.ValueString != "OnlyShort") // if the mode is not only short, then we enter long
                {
                    if (_lastBlueStohFast < 30 && _lastBlueStohFast > _lastRedStohFast &&
                        _lastBlueStohSlow < 20 && _lastBlueStohSlow > _lastRedStohSlow)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // if the mode is not only long, we enter the short
                {
                    if (_lastBlueStohFast > 70 && _lastBlueStohFast < _lastRedStohFast &&
                        _lastBlueStohSlow > 80 && _lastBlueStohSlow < _lastRedStohSlow)
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
                Position position = openPositions[i];

                if (position.State != PositionStateType.Open)
                {
                    continue;
                }

                if (position.Direction == Side.Buy) // If the direction of the position is long
                {
                    decimal lov = candles[candles.Count - 1].Low;
                    stopPrice = lov - lov * _trailingValue.ValueDecimal / 100;
                }
                else // If the direction of the position is short
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * _trailingValue.ValueDecimal / 100;
                }

                _tab.CloseAtTrailingStop(position, stopPrice, stopPrice);
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