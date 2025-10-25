/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on strategy for two Ssma and Accumulation Distribution.

Buy: fast Ssma above slow Ssma and AD rising.

Sell: fast Ssma below slow Ssma and AD falling.

Exit:
From buy: fast Ssma below slow Ssma;

From sell: fast Ssma is higher than slow Ssma.
 */

namespace OsEngine.Robots
{
    [Bot("IntersectionOfTwoSsmaAndAD")] // We create an attribute so that we don't write anything to the BotFactory
    public class IntersectionOfTwoSsmaAndAD : BotPanel
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
        private StrategyParameterInt _periodSsmaFast;
        private StrategyParameterInt _periodSsmaSlow;

        // Indicator
        private Aindicator _AD;
        private Aindicator _FastSsma;
        private Aindicator _SlowSsma;

        // The last value of the indicators
        private decimal _lastFastSsma;
        private decimal _lastSlowSsma;
        private decimal _lastAD;

        // The prevlast value of the indicator
        private decimal _prevAD;

        public IntersectionOfTwoSsmaAndAD(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic setting
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Base");
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4, "Base");
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Base");

            // Setting indicator
            _periodSsmaFast = CreateParameter("Period Ssma Fast", 13, 10, 300, 10, "Indicator");
            _periodSsmaSlow = CreateParameter("Period Ssma Slow", 26, 10, 300, 10, "Indicator");

            // Create indicator AD
            _AD = IndicatorsFactory.CreateIndicatorByName("AccumulationDistribution", name + "AD", false);
            _AD = (Aindicator)_tab.CreateCandleIndicator(_AD, "NewArea");
            _AD.Save();

            // Create indicator FastSsma
            _FastSsma = IndicatorsFactory.CreateIndicatorByName("Ssma", name + "Ssma Fast", false);
            _FastSsma = (Aindicator)_tab.CreateCandleIndicator(_FastSsma, "Prime");
            ((IndicatorParameterInt)_FastSsma.Parameters[0]).ValueInt = _periodSsmaFast.ValueInt;
            _FastSsma.DataSeries[0].Color = Color.Yellow;
            _FastSsma.Save();

            // Create indicator SlowSsma
            _SlowSsma = IndicatorsFactory.CreateIndicatorByName("Ssma", name + "Ssma Slow", false);
            _SlowSsma = (Aindicator)_tab.CreateCandleIndicator(_SlowSsma, "Prime");
            ((IndicatorParameterInt)_SlowSsma.Parameters[0]).ValueInt = _periodSsmaSlow.ValueInt;
            _SlowSsma.DataSeries[0].Color = Color.Green;
            _SlowSsma.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfTwoSsmaAndAD_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel213;
        }

        // Indicator Update event
        private void IntersectionOfTwoSsmaAndAD_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_FastSsma.Parameters[0]).ValueInt = _periodSsmaFast.ValueInt;
            _FastSsma.Save();
            _FastSsma.Reload();

            ((IndicatorParameterInt)_SlowSsma.Parameters[0]).ValueInt = _periodSsmaSlow.ValueInt;
            _SlowSsma.Save();
            _SlowSsma.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionOfTwoSsmaAndAD";
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
            if (candles.Count < _periodSsmaSlow.ValueInt)
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
                _lastFastSsma = _FastSsma.DataSeries[0].Last;
                _lastSlowSsma = _SlowSsma.DataSeries[0].Last;
                _lastAD = _AD.DataSeries[0].Last;

                // The prevlast value of the indicator
                _prevAD = _AD.DataSeries[0].Values[_AD.DataSeries[0].Values.Count - 2];

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastFastSsma > _lastSlowSsma && _lastAD > _prevAD)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastFastSsma < _lastSlowSsma && _lastAD < _prevAD)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage);
                    }
                }

                return;
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            // The last value of the indicators
            _lastFastSsma = _FastSsma.DataSeries[0].Last;
            _lastSlowSsma = _SlowSsma.DataSeries[0].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position position = openPositions[i];

                if (position.State != PositionStateType.Open)
                {
                    continue;
                }

                if (position.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_lastFastSsma < _lastSlowSsma)
                    {
                        _tab.CloseAtLimit(position, lastPrice - _slippage, position.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_lastFastSsma > _lastSlowSsma)
                    {
                        _tab.CloseAtLimit(position, lastPrice + _slippage, position.OpenVolume);
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