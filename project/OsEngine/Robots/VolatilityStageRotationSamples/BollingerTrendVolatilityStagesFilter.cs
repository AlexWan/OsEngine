/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.Market.Servers;
using OsEngine.Market;
using System;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Bollinger Trend VolatilityStages Filter.

Buy:
1. The price has broken above the upper line of the main Bollinger band.
2. If the volatility filter is enabled, the current volatility stage must match the one selected for trading.

Sell:
1. The price has broken below the lower line of the main Bollinger band.
2. If the volatility filter is enabled, the current volatility stage must match the one selected for trading.

Exit for long: Close by a trailing stop set at the lower Bollinger line.
Exit for short: Close by a trailing stop set at the upper Bollinger line.
*/

namespace OsEngine.Robots.VolatilityStageRotationSamples
{
    [Bot("BollingerTrendVolatilityStagesFilter")] // We create an attribute so that we don't write anything to the BotFactory
    public class BollingerTrendVolatilityStagesFilter : BotPanel
    {
        private BotTabSimple _tab;

        // Basic setting
        private StrategyParameterString _regime;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _bollingerLength;
        private StrategyParameterDecimal _bollingerDeviation;

        // Volatility settings
        private StrategyParameterBool _volatilityFilterIsOn;
        private StrategyParameterString _volatilityStageToTrade;
        private StrategyParameterInt _volatilitySlowSmaLength;
        private StrategyParameterInt _volatilityFastSmaLength;
        private StrategyParameterDecimal _volatilityChannelDeviation;

        // Indicator
        private Aindicator _bollinger;
        private Aindicator _volatilityStages;

        public BollingerTrendVolatilityStagesFilter(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic setting
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _bollingerLength = CreateParameter("Bollinger length", 30, 10, 80, 3);
            _bollingerDeviation = CreateParameter("Bollinger deviation", 2, 1.0m, 50, 4);

            // Volatility settings
            _volatilityFilterIsOn = CreateParameter("Volatility filter is on", false);
            _volatilityStageToTrade = CreateParameter("Volatility stage to trade", "2", new[] { "1", "2", "3", "4" });
            _volatilitySlowSmaLength = CreateParameter("Volatility slow sma length", 25, 10, 80, 3);
            _volatilityFastSmaLength = CreateParameter("Volatility fast sma length", 7, 10, 80, 3);
            _volatilityChannelDeviation = CreateParameter("Volatility channel deviation", 0.5m, 1.0m, 50, 4);

            // Create indicator Bollinger
            _bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
            _bollinger = (Aindicator)_tab.CreateCandleIndicator(_bollinger, "Prime");
            _bollinger.ParametersDigit[0].Value = _bollingerLength.ValueInt;
            _bollinger.ParametersDigit[1].Value = _bollingerDeviation.ValueDecimal;
            _bollinger.Save();

            // Create indicator VolatilityStages
            _volatilityStages = IndicatorsFactory.CreateIndicatorByName("VolatilityStagesAW", name + "VolatilityStages", false);
            _volatilityStages = (Aindicator)_tab.CreateCandleIndicator(_volatilityStages, "VolatilityStagesArea");
            _volatilityStages.ParametersDigit[0].Value = _volatilitySlowSmaLength.ValueInt;
            _volatilityStages.ParametersDigit[1].Value = _volatilityFastSmaLength.ValueInt;
            _volatilityStages.ParametersDigit[2].Value = _volatilityChannelDeviation.ValueDecimal;
            _volatilityStages.Save();

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += Event_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel121;
        }

        void Event_ParametrsChangeByUser()
        {
            if (_bollingerLength.ValueInt != _bollinger.ParametersDigit[0].Value ||
                _bollingerLength.ValueInt != _bollinger.ParametersDigit[1].Value)
            {
                _bollinger.ParametersDigit[0].Value = _bollingerLength.ValueInt;
                _bollinger.ParametersDigit[1].Value = _bollingerDeviation.ValueDecimal;
                _bollinger.Reload();
            }

            if (_volatilityStages.ParametersDigit[0].Value != _volatilitySlowSmaLength.ValueInt
                || _volatilityStages.ParametersDigit[1].Value != _volatilityFastSmaLength.ValueInt
                || _volatilityStages.ParametersDigit[2].Value != _volatilityChannelDeviation.ValueDecimal)
            {
                _volatilityStages.ParametersDigit[0].Value = _volatilitySlowSmaLength.ValueInt;
                _volatilityStages.ParametersDigit[1].Value = _volatilityFastSmaLength.ValueInt;
                _volatilityStages.ParametersDigit[2].Value = _volatilityChannelDeviation.ValueDecimal;
                _volatilityStages.Reload();
            }
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BollingerTrendVolatilityStagesFilter";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Logic
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (_bollinger.DataSeries[0].Values == null
                || _bollinger.DataSeries[1].Values == null)
            {
                return;
            }

            if (_bollinger.DataSeries[0].Values.Count < _bollinger.ParametersDigit[0].Value + 2
                || _bollinger.DataSeries[1].Values.Count < _bollinger.ParametersDigit[1].Value + 2)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                if (_regime.ValueString == "OnlyClosePosition")
                {
                    return;
                }
                LogicOpenPosition(candles);
            }
            else
            {
                LogicClosePosition(candles, openPositions[0]);
            }
        }

        // Opening logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal lastPcUp = _bollinger.DataSeries[0].Values[_bollinger.DataSeries[0].Values.Count - 1];
            decimal lastPcDown = _bollinger.DataSeries[1].Values[_bollinger.DataSeries[1].Values.Count - 1];

            if (lastPcUp == 0
                || lastPcDown == 0)
            {
                return;
            }

            if (lastPrice > lastPcUp
                && _regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
            {
                if (_volatilityFilterIsOn.ValueBool == true)
                {
                    decimal stage = _volatilityStages.DataSeries[0].Values[_volatilityStages.DataSeries[0].Values.Count - 2];

                    if (stage != _volatilityStageToTrade.ValueString.ToDecimal())
                    {
                        return;
                    }
                }

                _tab.BuyAtMarket(GetVolume(_tab));
            }
            if (lastPrice < lastPcDown
                && _regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
            {
                if (_volatilityFilterIsOn.ValueBool == true)
                {
                    decimal stage = _volatilityStages.DataSeries[0].Values[_volatilityStages.DataSeries[0].Values.Count - 2];

                    if (stage != _volatilityStageToTrade.ValueString.ToDecimal())
                    {
                        return;
                    }
                }

                _tab.SellAtMarket(GetVolume(_tab));
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            decimal lastPcUp = _bollinger.DataSeries[0].Values[_bollinger.DataSeries[0].Values.Count - 1];
            decimal lastPcDown = _bollinger.DataSeries[1].Values[_bollinger.DataSeries[1].Values.Count - 1];

            if (position.Direction == Side.Buy)
            {
                _tab.CloseAtTrailingStopMarket(position, lastPcDown);
            }
            if (position.Direction == Side.Sell)
            {
                _tab.CloseAtTrailingStopMarket(position, lastPcUp);
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