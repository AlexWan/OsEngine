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
using System.Drawing;
using OsEngine.Language;

/* Description
trading robot for osengine

Trend robot on the SuperTrend indicator.

Buy: When the SuperTrend with a smaller period and deviation is higher than the SuperTrend with a larger period and deviation.

Sell: When the SuperTrend with a smaller period and deviation is lower than a SuperTrend with a larger period and deviation.

Exit from buy: When the SuperTrend with a smaller period and deviation is lower than a SuperTrend with a larger period and deviation.

Exit from sell: When the SuperTrend with a smaller period and deviation is higher than the SuperTrend with a larger period and deviation.
 */

namespace OsEngine.Robots.MyBots
{
    [Bot("IntersectionOfSuperTrends")] // We create an attribute so that we don't write anything to the BotFactory
    internal class IntersectionOfSuperTrends : BotPanel
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

        // Indicator settings
        private StrategyParameterInt _lengthFastSP;
        private StrategyParameterString _typeFastPrice;
        private StrategyParameterDecimal _fastSPDeviation;
        private StrategyParameterInt _lengthSlowSP;
        private StrategyParameterString _typeSlowPrice;
        private StrategyParameterDecimal _slowSPDeviation;

        // Indicator
        private Aindicator _fastSP;
        private Aindicator _slowSP;

        public IntersectionOfSuperTrends(string name, StartProgram startProgram) : base(name, startProgram)
        {
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
            _lengthFastSP = CreateParameter("Length Fast SP", 10, 10, 200, 10, "Indicator");
            _fastSPDeviation = CreateParameter("Fast SP Deviation", 1, 1m, 10, 1, "Indicator");
            _typeFastPrice = CreateParameter("Type Fast Price", "Median", new[] { "Median", "Typical" }, "Indicator");
            _lengthSlowSP = CreateParameter("Length Slow SP", 50, 50, 300, 10, "Indicator");
            _slowSPDeviation = CreateParameter("Slow SP Deviation", 1, 1m, 10, 1, "Indicator");
            _typeSlowPrice = CreateParameter("Type Slow Price", "Median", new[] { "Median", "Typical" }, "Indicator");

            // Create indicator SuperTrendFast
            _fastSP = IndicatorsFactory.CreateIndicatorByName("SuperTrend_indicator", name + "SuperTrendFast", false);
            _fastSP = (Aindicator)_tab.CreateCandleIndicator(_fastSP, "Prime");
            ((IndicatorParameterInt)_fastSP.Parameters[0]).ValueInt = _lengthFastSP.ValueInt;
            ((IndicatorParameterDecimal)_fastSP.Parameters[1]).ValueDecimal = _fastSPDeviation.ValueDecimal;
            ((IndicatorParameterString)_fastSP.Parameters[2]).ValueString = _typeFastPrice.ValueString;
            ((IndicatorParameterBool)_fastSP.Parameters[3]).ValueBool = false;
            _fastSP.DataSeries[2].Color = Color.Red;
            _fastSP.Save();

            // Create indicator SuperTrendSlow
            _slowSP = IndicatorsFactory.CreateIndicatorByName("SuperTrend_indicator", name + "SuperTrendSlow", false);
            _slowSP = (Aindicator)_tab.CreateCandleIndicator(_slowSP, "Prime");
            ((IndicatorParameterInt)_slowSP.Parameters[0]).ValueInt = _lengthSlowSP.ValueInt;
            ((IndicatorParameterDecimal)_slowSP.Parameters[1]).ValueDecimal = _slowSPDeviation.ValueDecimal;
            ((IndicatorParameterString)_slowSP.Parameters[2]).ValueString = _typeSlowPrice.ValueString;
            ((IndicatorParameterBool)_slowSP.Parameters[3]).ValueBool = false;
            _slowSP.DataSeries[2].Color = Color.Green;
            _slowSP.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfSuperTrends_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel203;
        }

        private void IntersectionOfSuperTrends_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_fastSP.Parameters[0]).ValueInt = _lengthFastSP.ValueInt;
            ((IndicatorParameterDecimal)_fastSP.Parameters[1]).ValueDecimal = _fastSPDeviation.ValueDecimal;
            ((IndicatorParameterString)_fastSP.Parameters[2]).ValueString = _typeFastPrice.ValueString;
            ((IndicatorParameterBool)_fastSP.Parameters[3]).ValueBool = false;
            _fastSP.Save();
            _fastSP.Reload();

            ((IndicatorParameterInt)_slowSP.Parameters[0]).ValueInt = _lengthSlowSP.ValueInt;
            ((IndicatorParameterDecimal)_slowSP.Parameters[1]).ValueDecimal = _slowSPDeviation.ValueDecimal;
            ((IndicatorParameterString)_slowSP.Parameters[2]).ValueString = _typeSlowPrice.ValueString;
            ((IndicatorParameterBool)_slowSP.Parameters[3]).ValueBool = false;
            _slowSP.Save();
            _slowSP.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionOfSuperTrends";
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
            if (candles.Count < _lengthSlowSP.ValueInt + 10 || candles.Count < _lengthFastSP.ValueInt + 10)
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
            decimal lastFastSp = _fastSP.DataSeries[2].Last;
            decimal lastSlowSp = _slowSP.DataSeries[2].Last;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice > lastFastSp && lastFastSp > lastSlowSp)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastPrice < lastFastSp && lastFastSp < lastSlowSp)
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

            // The last value of the indicator
            decimal lastFastSp = _fastSP.DataSeries[2].Last;
            decimal lastSlowSp = _slowSP.DataSeries[2].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;
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
                    if (lastFastSp < lastSlowSp)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (lastFastSp > lastSlowSp)
                    {
                        _tab.CloseAtLimit(pos, lastPrice + _slippage, pos.OpenVolume);
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