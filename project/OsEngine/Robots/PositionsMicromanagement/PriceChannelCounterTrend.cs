/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using OsEngine.Language;

/* Description
trading robot for osengine

The Countertrend robot on PriceChannel.

Buy:
If the price is below the lower level _pc

Sell:
If the current price exceeds the upper level _pc

Exit:
Based on stop-loss and profit targets
 */

namespace OsEngine.Robots.PositionsMicromanagement
{
    [Bot("PriceChannelCounterTrend")] // We create an attribute so that we don't write anything to the BotFactory
    public class PriceChannelCounterTrend : BotPanel
    {
        private BotTabSimple _tab;

        // Basic setting
        private StrategyParameterString _regime;

        // Indicator settings
        private StrategyParameterInt _priceChannelLength;

        // Indicator
        private Aindicator _pc;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Exit settings
        private StrategyParameterDecimal _stopPercent;
        private StrategyParameterDecimal _profitOrderOnePercent;
        private StrategyParameterDecimal _profitOrderTwoPercent;

        public PriceChannelCounterTrend(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic setting
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator setting
            _priceChannelLength = CreateParameter("Price channel length", 50, 10, 80, 3);

            // Exit settings
            _stopPercent = CreateParameter("Stop percent", 0.7m, 1.0m, 50, 4);
            _profitOrderOnePercent = CreateParameter("Profit order one percent", 0.3m, 1.0m, 50, 4);
            _profitOrderTwoPercent = CreateParameter("Profit order two percent", 0.7m, 1.0m, 50, 4);

            // Create indicator PriceChannel
            _pc = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PriceChannel", false);
            _pc = (Aindicator)_tab.CreateCandleIndicator(_pc, "Prime");
            _pc.ParametersDigit[0].Value = _priceChannelLength.ValueInt;
            _pc.ParametersDigit[1].Value = _priceChannelLength.ValueInt;
            _pc.Save();

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += Event_ParametrsChangeByUser;

            _tab.ManualPositionSupport.DisableManualSupport();

            Description = OsLocalization.Description.DescriptionLabel83;
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
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "PriceChannelCounterTrend";
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

            if (_pc.DataSeries[0].Values == null
                || _pc.DataSeries[1].Values == null)
            {
                return;
            }

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
                for (int i = 0; i < openPositions.Count;i++)
                {
                    LogicClosePosition(candles, openPositions[i]);
                }
            }
        }

        // Opening position logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal lastPcUp = _pc.DataSeries[0].Values[_pc.DataSeries[0].Values.Count - 2];
            decimal lastPcDown = _pc.DataSeries[1].Values[_pc.DataSeries[1].Values.Count - 2];

            if (lastPcUp == 0
                || lastPcDown == 0)
            {
                return;
            }

            if (lastPrice > lastPcUp
                && _regime.ValueString != "OnlyLong")
            {
                _tab.SellAtMarket(GetVolume(_tab), "First");
                _tab.SellAtMarket(GetVolume(_tab), "Second");
            }
            if (lastPrice < lastPcDown
                && _regime.ValueString != "OnlyShort")
            {
                _tab.BuyAtMarket(GetVolume(_tab), "First");
                _tab.BuyAtMarket(GetVolume(_tab), "Second");
            }
        }

        // Close position logic
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if(position.State == PositionStateType.Opening)
            {
                return;
            }

            if (position.StopOrderPrice == 0)
            {
                decimal price = 0;

                if (position.Direction == Side.Buy)
                {
                    price = position.EntryPrice - position.EntryPrice * (_stopPercent.ValueDecimal / 100);
                }
                else if (position.Direction == Side.Sell)
                {
                    price = position.EntryPrice + position.EntryPrice * (_stopPercent.ValueDecimal / 100);
                }

                _tab.CloseAtStopMarket(position, price, "StopActivate");
            }

            if (position.SignalTypeOpen == "First"
                && position.CloseActive == false)
            {
                decimal price = 0;

                if (position.Direction == Side.Buy)
                {
                    price = position.EntryPrice + position.EntryPrice * (_profitOrderOnePercent.ValueDecimal / 100);
                }
                else if (position.Direction == Side.Sell)
                {
                    price = position.EntryPrice - position.EntryPrice * (_profitOrderOnePercent.ValueDecimal / 100);
                }

                _tab.CloseAtLimit(position, price, position.OpenVolume);
            }

            if (position.SignalTypeOpen == "Second"
                && position.CloseActive == false)
            {
                decimal price = 0;

                if (position.Direction == Side.Buy)
                {
                    price = position.EntryPrice + position.EntryPrice * (_profitOrderTwoPercent.ValueDecimal / 100);
                }
                else if (position.Direction == Side.Sell)
                {
                    price = position.EntryPrice - position.EntryPrice * (_profitOrderTwoPercent.ValueDecimal / 100);
                }

                _tab.CloseAtLimit(position, price, position.OpenVolume);
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