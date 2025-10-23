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

namespace OsEngine.Robots.PositionsMicromanagement
{
    [Bot("UnsafeAveragePosition")] // We create an attribute so that we don't write anything to the BotFactory
    public class UnsafeAveragePosition : BotPanel
    {
        private BotTabSimple _tab;

        // Basic setting
        private StrategyParameterString _regime;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _envelopsLength;
        private StrategyParameterDecimal _envelopsDeviation;

        // Indicator
        private Aindicator _envelop;
        
        // Exit settings
        private StrategyParameterDecimal _stopPercent;
        private StrategyParameterDecimal _profitPercent;
        private StrategyParameterDecimal _averageOnePercent;
        private StrategyParameterDecimal _averageTwoPercent;

        public UnsafeAveragePosition(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _envelopsLength = CreateParameter("Envelops length", 50, 10, 80, 3);
            _envelopsDeviation = CreateParameter("Envelops deviation", 0.1m, 0.5m, 5, 0.1m);

            // Exit settings
            _stopPercent = CreateParameter("Stop percent", 1.5m, 1.0m, 50, 4);
            _profitPercent = CreateParameter("Profit percent", 1.5m, 1.0m, 50, 4);
            _averageOnePercent = CreateParameter("Average one percent", 0.4m, 1.0m, 50, 4);
            _averageTwoPercent = CreateParameter("Average two percent", 0.8m, 1.0m, 50, 4);

            // Create indicator Envelops
            _envelop = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "Envelops", false);
            _envelop = (Aindicator)_tab.CreateCandleIndicator(_envelop, "Prime");
            _envelop.ParametersDigit[0].Value = _envelopsLength.ValueInt;
            _envelop.ParametersDigit[1].Value = _envelopsDeviation.ValueDecimal;
            _envelop.Save();

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += Event_ParametrsChangeByUser;

            _tab.ManualPositionSupport.DisableManualSupport();

            Description = OsLocalization.Description.DescriptionLabel85;
        }

        void Event_ParametrsChangeByUser()
        {
            if (_envelopsLength.ValueInt != _envelop.ParametersDigit[0].Value ||
               _envelop.ParametersDigit[1].Value != _envelopsDeviation.ValueDecimal)
            {
                _envelop.ParametersDigit[0].Value = _envelopsLength.ValueInt;
                _envelop.ParametersDigit[1].Value = _envelopsDeviation.ValueDecimal;
                _envelop.Reload();
                _envelop.Save();
            }
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "UnsafeAveragePosition";
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

            if (_envelop.DataSeries[0].Values == null
                || _envelop.DataSeries[1].Values == null)
            {
                return;
            }

            if (_envelop.DataSeries[0].Values.Count < _envelop.ParametersDigit[0].Value + 2
                || _envelop.DataSeries[1].Values.Count < _envelop.ParametersDigit[1].Value + 2)
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

        // Opening position logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal prevPrice = candles[candles.Count - 2].Close;
            decimal lastEnvelopsUp = _envelop.DataSeries[0].Values[_envelop.DataSeries[0].Values.Count - 1];
            decimal lastEnvelopsDown = _envelop.DataSeries[2].Values[_envelop.DataSeries[1].Values.Count - 1];

            if (lastEnvelopsUp == 0
                || lastEnvelopsDown == 0)
            {
                return;
            }

            if (lastPrice > lastEnvelopsUp
                && prevPrice < lastEnvelopsUp
                && _regime.ValueString != "OnlyLong")
            {
                _tab.SellAtMarket(GetVolume(_tab));
            }
            if (lastPrice < lastEnvelopsDown
                 && prevPrice > lastEnvelopsDown
                && _regime.ValueString != "OnlyShort")
            {
                _tab.BuyAtMarket(GetVolume(_tab));
            }
        }

        // Close position logic
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.SignalTypeClose == "StopActivate"
                || position.SignalTypeClose == "ProfitActivate"
                ||
                (position.State == PositionStateType.Opening && position.OpenOrders.Count == 1))
            {
                return;
            }

            // Average by limit orders

            if (position.OpenActive == false)
            {
                decimal firstOrderPrice = 0;
                decimal secondOrderPrice = 0;

                if (position.Direction == Side.Buy)
                {
                    firstOrderPrice = position.EntryPrice - position.EntryPrice * (_averageOnePercent.ValueDecimal / 100);
                    secondOrderPrice = position.EntryPrice - position.EntryPrice * (_averageTwoPercent.ValueDecimal / 100);
                }
                else if (position.Direction == Side.Sell)
                {
                    firstOrderPrice = position.EntryPrice + position.EntryPrice * (_averageOnePercent.ValueDecimal / 100);
                    secondOrderPrice = position.EntryPrice + position.EntryPrice * (_averageTwoPercent.ValueDecimal / 100);
                }

                int executeOpenOrdersCount = 0;

                for (int i = 0; position.OpenOrders != null && i < position.OpenOrders.Count; i++)
                {
                    if (position.OpenOrders[i].State == OrderStateType.Done)
                    {
                        executeOpenOrdersCount++;
                    }
                }

                if (executeOpenOrdersCount == 1)
                {
                    if (position.Direction == Side.Buy)
                    {
                        _tab.BuyAtLimitToPositionUnsafe(position, firstOrderPrice, GetVolume(_tab));
                        _tab.BuyAtLimitToPositionUnsafe(position, secondOrderPrice, GetVolume(_tab));
                    }
                    else if (position.Direction == Side.Sell)
                    {
                        _tab.SellAtLimitToPositionUnsafe(position, firstOrderPrice, GetVolume(_tab));
                        _tab.SellAtLimitToPositionUnsafe(position, secondOrderPrice, GetVolume(_tab));
                    }
                }
                else if (executeOpenOrdersCount == 2)
                {
                    if (position.Direction == Side.Buy)
                    {
                        _tab.BuyAtLimitToPositionUnsafe(position, secondOrderPrice, GetVolume(_tab));
                    }
                    else if (position.Direction == Side.Sell)
                    {
                        _tab.SellAtLimitToPositionUnsafe(position, secondOrderPrice, GetVolume(_tab));
                    }
                }
            }

            // Stop
            decimal stopPrice = 0;

            if (position.Direction == Side.Buy)
            {
                stopPrice = position.EntryPrice - position.EntryPrice * (_stopPercent.ValueDecimal / 100);
            }
            else if (position.Direction == Side.Sell)
            {
                stopPrice = position.EntryPrice + position.EntryPrice * (_stopPercent.ValueDecimal / 100);
            }

            _tab.CloseAtStopMarket(position, stopPrice, "StopActivate");

            // Profit
            decimal profitPrice = 0;

            if (position.Direction == Side.Buy)
            {
                profitPrice = position.EntryPrice + position.EntryPrice * (_profitPercent.ValueDecimal / 100);
            }
            else if (position.Direction == Side.Sell)
            {
                profitPrice = position.EntryPrice - position.EntryPrice * (_profitPercent.ValueDecimal / 100);
            }

            _tab.CloseAtProfitMarket(position, profitPrice, "ProfitActivate");
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