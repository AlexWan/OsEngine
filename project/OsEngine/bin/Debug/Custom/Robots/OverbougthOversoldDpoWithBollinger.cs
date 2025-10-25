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

The contrtrend robot on Overbougth Oversold DPO with Bollinger.

Buy: When the current candle closed above the lower Bollinger line, and the previous candle closed below, 
and the DPO indicator came out of the oversold zone.

Sell: When the current candle closed below the upper Bollinger line, 
and the previous candle closed above, and the DPO indicator left the overbought zone.

Exit from buy: trailing stop in % of the loy of the candle on which you entered.

Exit from sell: trailing stop in % of the high of the candle on which you entered.
 */

namespace OsEngine.Robots
{
    [Bot("OverbougthOversoldDpoWithBollinger")] // We create an attribute so that we don't write anything to the BotFactory
    internal class OverbougthOversoldDpoWithBollinger : BotPanel
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
        private StrategyParameterInt _dpoLength;
        private StrategyParameterDecimal _overboughtLevel;
        private StrategyParameterDecimal _oversoldLevel;
        private StrategyParameterInt _bollingerLength;
        private StrategyParameterDecimal _bollingerDeviation;

        // Exit Setting
        private StrategyParameterDecimal _trailingValue;

        // Indicator
        private Aindicator _dpo;
        private Aindicator _bollinger;

        public OverbougthOversoldDpoWithBollinger(string name, StartProgram startProgram) : base(name, startProgram)
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
            _dpoLength = CreateParameter("Dpo Length", 14, 5, 200, 10, "Indicator");
            _overboughtLevel = CreateParameter("Overbought Level", 0.5m, 100, 1000, 100, "Indicator");
            _oversoldLevel = CreateParameter("Oversold Level", -0.5m, -1000, -100, 100, "Indicator");
            _bollingerLength = CreateParameter("Bollinger Length", 50, 10, 300, 10, "Indicator");
            _bollingerDeviation = CreateParameter("Bollinger Deviation", 1.0m, 1, 5, 0.1m, "Indicator");
            
            // Exit Setting
            _trailingValue = CreateParameter("Trailing Value", 1.0m, 1, 20, 1, "Exit");

            // Create indicator Dpo
            _dpo = IndicatorsFactory.CreateIndicatorByName("DPO_Detrended_Price_Oscillator", name + "Dpo", false);
            _dpo = (Aindicator)_tab.CreateCandleIndicator(_dpo, "DpoArea");
            ((IndicatorParameterInt)_dpo.Parameters[0]).ValueInt = _dpoLength.ValueInt;
            _dpo.Save();

            // Create indicator Bollinger
            _bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
            _bollinger = (Aindicator)_tab.CreateCandleIndicator(_bollinger, "Prime");
            ((IndicatorParameterInt)_bollinger.Parameters[0]).ValueInt = _bollingerLength.ValueInt;
            ((IndicatorParameterDecimal)_bollinger.Parameters[1]).ValueDecimal = _bollingerDeviation.ValueDecimal;
            _bollinger.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += OverbougthOversoldDpoWithBollinger_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel226;
        }

        private void OverbougthOversoldDpoWithBollinger_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_dpo.Parameters[0]).ValueInt = _dpoLength.ValueInt;
            _dpo.Save();
            _dpo.Reload();

            ((IndicatorParameterInt)_bollinger.Parameters[0]).ValueInt = _bollingerLength.ValueInt;
            ((IndicatorParameterDecimal)_bollinger.Parameters[1]).ValueDecimal = _bollingerDeviation.ValueDecimal;
            _bollinger.Save();
            _bollinger.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "OverbougthOversoldDpoWithBollinger";
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
            if (candles.Count < _dpoLength.ValueInt + 10 ||
                candles.Count < _bollingerLength.ValueInt)
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

            // The value of the indicator
            decimal lastDpo = _dpo.DataSeries[0].Last;
            decimal prevDpo = _dpo.DataSeries[0].Values[_dpo.DataSeries[0].Values.Count - 2];
            decimal lastUpLineBol = _bollinger.DataSeries[0].Last;
            decimal lastDownLineBol = _bollinger.DataSeries[1].Last;
            decimal prevUpLineBol = _bollinger.DataSeries[0].Values[_bollinger.DataSeries[0].Values.Count - 2];
            decimal prevDownLineBol = _bollinger.DataSeries[1].Values[_bollinger.DataSeries[1].Values.Count - 2];

            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal prevPrice = candles[candles.Count - 2].Close;

            if (lastDpo == 0)
            {
                return;
            }

            if (openPositions == null || openPositions.Count == 0)
            {
                // Slippage
                decimal _slippage = this._slippage.ValueDecimal / 100 * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastDpo > _oversoldLevel.ValueDecimal && prevDpo < _oversoldLevel.ValueDecimal && lastDownLineBol < lastPrice
                         && prevPrice < prevDownLineBol)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastDpo < _overboughtLevel.ValueDecimal && prevDpo > _overboughtLevel.ValueDecimal && lastUpLineBol > lastPrice
                        && prevPrice > prevUpLineBol)
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