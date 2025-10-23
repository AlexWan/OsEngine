/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

/* Description
trading robot for osengine

The trend robot on PriceChannel and AtrFilter.

Buy:
1.The price has broken above the upper line of the Price Channel
2.ATR has grown compared to AtrGrowLookBack candles ago.
Sell:
1.The price has broken below the lower line of the Price Channel
2.ATR has grown compared to AtrGrowLookBack candles ago.

Exit for long:
Exit using a trailing stop set to the lower line of the Price Channel.
Exit for short:
Exit using a trailing stop set to the upper line of the Price Channel.
*/

namespace OsEngine.Robots.VolatilityStageRotationSamples
{
    [Bot("PriceChannelTrendAtrFilter")] // We create an attribute so that we don't write anything to the BotFactory
    public class PriceChannelTrendAtrFilter : BotPanel
    {
        private BotTabSimple _tab;

        // Indicator
        private Aindicator _pc;
        private Aindicator _atr;

        // Basic setting
        private StrategyParameterString _regime;

        // Indicator settings
        private StrategyParameterInt _priceChannelLength;
        private StrategyParameterInt _atrLength;
        private StrategyParameterBool _atrFilterIsOn;
        private StrategyParameterDecimal _atrGrowPercent;
        private StrategyParameterInt _atrGrowLookBack;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        public PriceChannelTrendAtrFilter(string name, StartProgram startProgram)
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
            _priceChannelLength = CreateParameter("Price channel length", 50, 10, 80, 3);
            _atrLength = CreateParameter("Atr length", 25, 10, 80, 3);
            _atrFilterIsOn = CreateParameter("Atr filter is on", false);
            _atrGrowPercent = CreateParameter("Atr grow percent", 3, 1.0m, 50, 4);
            _atrGrowLookBack = CreateParameter("Atr grow look back", 20, 1, 50, 4);

            // Create indicator PriceChannel
            _pc = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PriceChannel", false);
            _pc = (Aindicator)_tab.CreateCandleIndicator(_pc, "Prime");
            _pc.ParametersDigit[0].Value = _priceChannelLength.ValueInt;
            _pc.ParametersDigit[1].Value = _priceChannelLength.ValueInt;
            _pc.Save();

            // Create indicator ATR
            _atr = IndicatorsFactory.CreateIndicatorByName("ATR", name + "Atr", false);
            _atr = (Aindicator)_tab.CreateCandleIndicator(_atr, "AtrArea");
            _atr.ParametersDigit[0].Value = _atrLength.ValueInt;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += Event_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel123;
        }

        void Event_ParametrsChangeByUser()
        {
            if (_priceChannelLength.ValueInt != _pc.ParametersDigit[0].Value ||
                _priceChannelLength.ValueInt != _pc.ParametersDigit[1].Value)
            {
                _pc.ParametersDigit[0].Value = _priceChannelLength.ValueInt;
                _pc.ParametersDigit[1].Value = _priceChannelLength.ValueInt;
                _pc.Reload();
                _pc.Save();
            }

            if(_atr.ParametersDigit[0].Value != _atrLength.ValueInt)
            {
                _atr.ParametersDigit[0].Value = _atrLength.ValueInt;
                _atr.Reload();
                _atr.Save();
            }
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "PriceChannelTrendAtrFilter";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // logic
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // If value indicator equals null, we exit
            if (_pc.DataSeries[0].Values == null 
                || _pc.DataSeries[1].Values == null)
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (_pc.DataSeries[0].Values.Count < _pc.ParametersDigit[0].Value + 2 
                || _pc.DataSeries[1].Values.Count < _pc.ParametersDigit[1].Value + 2)
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
            // The last value of the indicator
            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal lastPcUp = _pc.DataSeries[0].Values[_pc.DataSeries[0].Values.Count - 2];
            decimal lastPcDown = _pc.DataSeries[1].Values[_pc.DataSeries[1].Values.Count - 2];

            if(lastPcUp == 0 
                || lastPcDown == 0)
            {
                return;
            }

            if (lastPrice > lastPcUp
                && _regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
            {
                if (_atrFilterIsOn.ValueBool == true)
                {
                    if (_atr.DataSeries[0].Values.Count - 1 - _atrGrowLookBack.ValueInt <= 0)
                    {
                        return;
                    }

                    decimal atrLast = _atr.DataSeries[0].Values[_atr.DataSeries[0].Values.Count - 1];
                    decimal atrLookBack = _atr.DataSeries[0].Values[_atr.DataSeries[0].Values.Count - 1 - _atrGrowLookBack.ValueInt];

                    if (atrLast == 0
                        || atrLookBack == 0)
                    {
                        return;
                    }

                    decimal atrGrowPercent = atrLast / (atrLookBack / 100) - 100;

                    if (atrGrowPercent < _atrGrowPercent.ValueDecimal)
                    {
                        return;
                    }
                }

                _tab.BuyAtMarket(GetVolume(_tab));
            }

            if (lastPrice < lastPcDown
                && _regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
            {
                if (_atrFilterIsOn.ValueBool == true)
                {
                    if (_atr.DataSeries[0].Values.Count - 1 - _atrGrowLookBack.ValueInt <= 0)
                    {
                        return;
                    }

                    decimal atrLast = _atr.DataSeries[0].Values[_atr.DataSeries[0].Values.Count - 1];
                    decimal atrLookBack =
                        _atr.DataSeries[0].Values[_atr.DataSeries[0].Values.Count - 1 - _atrGrowLookBack.ValueInt];

                    if (atrLast == 0
                        || atrLookBack == 0)
                    {
                        return;
                    }

                    decimal atrGrowPercent = atrLast / (atrLookBack / 100) - 100;

                    if (atrGrowPercent < _atrGrowPercent.ValueDecimal)
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
            // The last value of the indicator
            decimal lastPcUp = _pc.DataSeries[0].Values[_pc.DataSeries[0].Values.Count - 1];
            decimal lastPcDown = _pc.DataSeries[1].Values[_pc.DataSeries[1].Values.Count - 1];

            if(position.Direction == Side.Buy) // If the direction of the position is long
            {
                _tab.CloseAtTrailingStopMarket(position, lastPcDown);
            }
            if (position.Direction == Side.Sell) // If the direction of the position is short
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