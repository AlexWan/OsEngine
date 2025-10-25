/*
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
using OsEngine.Language;

/* Description
trading robot for osengine

Counter-trend robot based on the MassIndex and Sma indicators.

Buy: When Sma falls and the current value of the MassIndex indicator is below the lower line, 
and the previous one was above the upper line.

Sell: When Sma grows and the current value of the MassIndex indicator is below the lower line, 
and the previous one was above the upper line.

Exit from buy: trailing stop in % of the loy of the candle on which you entered.
Exit from sell: trailing stop in % of the high of the candle on which you entered.
 */

namespace OsEngine.Robots
{
    [Bot("ContrtrendStrategyMiAndSma")] // We create an attribute so that we don't write anything to the BotFactory
    internal class ContrtrendStrategyMiAndSma : BotPanel
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

        // Indicator Settings  
        private StrategyParameterInt _miLength;
        private StrategyParameterInt _miSumLength;
        private StrategyParameterDecimal _upLine;
        private StrategyParameterDecimal _downLine;
        private StrategyParameterInt _smaLength;

        // Exit Setting
        private StrategyParameterDecimal _trailingValue;

        // Indicator
        private Aindicator _mi;
        private Aindicator _sma;

        public ContrtrendStrategyMiAndSma(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator Settings
            _miLength = CreateParameter("Mi Length", 9, 5, 200, 5, "Indicator");
            _miSumLength = CreateParameter("Mi Sum Length", 25, 5, 200, 5, "Indicator");
            _upLine = CreateParameter("Mi Up Line", 27m, 5, 50, 2, "Indicator");
            _downLine = CreateParameter("Mi Down Line", 26.5m, 5, 50, 1, "Indicator");
            _smaLength = CreateParameter("Sma Length", 20, 5, 50, 1, "Indicator");

            // Exit Setting
            _trailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Create indicator MassIndex
            _mi = IndicatorsFactory.CreateIndicatorByName("Mass_Index_MI", name + "MI", false);
            _mi = (Aindicator)_tab.CreateCandleIndicator(_mi, "MiArea");
            ((IndicatorParameterInt)_mi.Parameters[0]).ValueInt = _miLength.ValueInt;
            ((IndicatorParameterInt)_mi.Parameters[1]).ValueInt = _miSumLength.ValueInt;
            ((IndicatorParameterDecimal)_mi.Parameters[2]).ValueDecimal = _upLine.ValueDecimal;
            ((IndicatorParameterDecimal)_mi.Parameters[3]).ValueDecimal = _downLine.ValueDecimal;
            _mi.Save();

            // Create indicator Sma
            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SMA", false);
            _sma = (Aindicator)_tab.CreateCandleIndicator(_sma, "Prime");
            ((IndicatorParameterInt)_sma.Parameters[0]).ValueInt = _smaLength.ValueInt;
            _sma.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyMiAndSma_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel182;
        }

        private void StrategyMiAndSma_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_mi.Parameters[0]).ValueInt = _miLength.ValueInt;
            ((IndicatorParameterInt)_mi.Parameters[1]).ValueInt = _miSumLength.ValueInt;
            ((IndicatorParameterDecimal)_mi.Parameters[2]).ValueDecimal = _upLine.ValueDecimal;
            ((IndicatorParameterDecimal)_mi.Parameters[3]).ValueDecimal = _downLine.ValueDecimal;
            _mi.Save();
            _mi.Reload();
            ((IndicatorParameterInt)_sma.Parameters[0]).ValueInt = _smaLength.ValueInt;
            _sma.Save();
            _sma.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "ContrtrendStrategyMiAndSma";
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
            if (candles.Count < _miLength.ValueInt + 10 || candles.Count < _miSumLength.ValueInt 
                || candles.Count < _smaLength.ValueInt)
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

            // The last value of the indicator
            decimal lastMi = _mi.DataSeries[2].Last;
            decimal lastSma = _sma.DataSeries[0].Last;

            // The prev value of the indicator
            decimal prevMi = _mi.DataSeries[2].Values[_mi.DataSeries[2].Values.Count - 2];
            decimal prevMi2 = _mi.DataSeries[2].Values[_mi.DataSeries[2].Values.Count - 3];
            decimal prevMi3 = _mi.DataSeries[2].Values[_mi.DataSeries[2].Values.Count - 4];
            decimal prevMi4 = _mi.DataSeries[2].Values[_mi.DataSeries[2].Values.Count - 5];
            decimal prevSma= _sma.DataSeries[0].Values[_sma.DataSeries[0].Values.Count - 2];

            if (openPositions == null || openPositions.Count == 0)
            {
                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if ((prevMi > _upLine.ValueDecimal || prevMi2 > _upLine.ValueDecimal || prevMi3 > _upLine.ValueDecimal
                        || prevMi4 > _upLine.ValueDecimal) && lastMi < _downLine.ValueDecimal && lastSma < prevSma)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if ((prevMi > _upLine.ValueDecimal || prevMi2 > _upLine.ValueDecimal || prevMi3 > _upLine.ValueDecimal 
                        || prevMi4 > _upLine.ValueDecimal) && lastMi < _downLine.ValueDecimal && lastSma > prevSma)
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