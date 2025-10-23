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
    [Bot("EnvelopsCountertrend")] // We create an attribute so that we don't write anything to the BotFactory
    public class EnvelopsCountertrend : BotPanel
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

        // Entry settings
        private StrategyParameterDecimal _averagingOnePercent;
        private StrategyParameterDecimal _averagingTwoPercent;

        // Exit settings
        private StrategyParameterDecimal _profitPercent;
        private StrategyParameterDecimal _stopPercent;

        public EnvelopsCountertrend(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic setting
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _envelopsLength = CreateParameter("Envelops length", 50, 10, 80, 3);
            _envelopsDeviation = CreateParameter("Envelops deviation", 0.1m, 0.5m, 5, 0.1m);
            
            // Entry settings
            _averagingOnePercent = CreateParameter("Averaging one percent", 0.1m, 1.0m, 50, 4);
            _averagingTwoPercent = CreateParameter("Averaging two percent", 0.2m, 1.0m, 50, 4);

            // Exit settings
            _profitPercent = CreateParameter("Profit percent", 0.3m, 1.0m, 50, 4);
            _stopPercent = CreateParameter("Stop percent", 0.5m, 1.0m, 50, 4);
            
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

            Description = OsLocalization.Description.DescriptionLabel82;
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
            return "EnvelopsCountertrend";
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
                LogicClosePosition(candles, openPositions);
            }
        }

        // Opening position logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal lastEnvelopsUp = _envelop.DataSeries[0].Values[_envelop.DataSeries[0].Values.Count - 1];
            decimal lastEnvelopsDown = _envelop.DataSeries[2].Values[_envelop.DataSeries[1].Values.Count - 1];

            if (lastEnvelopsUp == 0
                || lastEnvelopsDown == 0)
            {
                return;
            }

            if (lastPrice > lastEnvelopsUp
                && _regime.ValueString != "OnlyLong")
            {
                _tab.SellAtMarket(GetVolume(_tab));
            }
            if (lastPrice < lastEnvelopsDown
                && _regime.ValueString != "OnlyShort")
            {
                _tab.BuyAtMarket(GetVolume(_tab));
            }
        }

        // Close position logic
        private void LogicClosePosition(List<Candle> candles, List<Position> positions)
        {
            if (positions[0].SignalTypeClose == "StopActivate"
                || positions[0].SignalTypeClose == "ProfitActivate"
                || positions[0].State == PositionStateType.Opening)
            {
                return;
            }

            // first averaging

            if (positions.Count == 1)
            {
                decimal nextEntryPrice = 0;

                decimal firstPosEntryPrice = positions[0].EntryPrice;

                if (positions[0].Direction == Side.Buy)
                {
                    nextEntryPrice = firstPosEntryPrice - firstPosEntryPrice * (_averagingOnePercent.ValueDecimal / 100);
                    _tab.BuyAtStopMarket(GetVolume(_tab), nextEntryPrice, nextEntryPrice, 
                        StopActivateType.LowerOrEqual, 1, "", PositionOpenerToStopLifeTimeType.CandlesCount);
                }
                else if (positions[0].Direction == Side.Sell)
                {
                    nextEntryPrice = firstPosEntryPrice + firstPosEntryPrice * (_averagingOnePercent.ValueDecimal / 100);
                    _tab.SellAtStopMarket(GetVolume(_tab), nextEntryPrice, nextEntryPrice, 
                        StopActivateType.HigherOrEqual, 1, "", PositionOpenerToStopLifeTimeType.CandlesCount);
                }
            }

            // second averaging

            if (positions.Count == 2)
            {
                decimal nextEntryPrice = 0;

                decimal firstPosEntryPrice = positions[0].EntryPrice;

                if (positions[0].Direction == Side.Buy)
                {
                    nextEntryPrice = firstPosEntryPrice - firstPosEntryPrice * (_averagingTwoPercent.ValueDecimal / 100);
                    _tab.BuyAtStopMarket(GetVolume(_tab), nextEntryPrice, nextEntryPrice, 
                        StopActivateType.LowerOrEqual, 1, "", PositionOpenerToStopLifeTimeType.CandlesCount);
                }
                else if (positions[0].Direction == Side.Sell)
                {
                    nextEntryPrice = firstPosEntryPrice + firstPosEntryPrice * (_averagingTwoPercent.ValueDecimal / 100);
                    _tab.SellAtStopMarket(GetVolume(_tab), nextEntryPrice, nextEntryPrice, 
                        StopActivateType.HigherOrEqual, 1, "", PositionOpenerToStopLifeTimeType.CandlesCount);
                }
            }

            // We calculate the average entry price

            decimal middleEntryPrice = 0;

            decimal allVolume = 0;

            for (int i = 0; i < positions.Count; i++)
            {
                middleEntryPrice += positions[i].EntryPrice * positions[i].OpenVolume;
                allVolume += positions[i].OpenVolume;
            }

            if(allVolume == 0)
            {
                return;
            }

            middleEntryPrice = middleEntryPrice / allVolume;

            // profit

            decimal profitPrice = 0;

            if (positions[0].Direction == Side.Buy)
            {
                profitPrice = middleEntryPrice + middleEntryPrice * (_profitPercent.ValueDecimal / 100);
            }
            else if (positions[0].Direction == Side.Sell)
            {
                profitPrice = middleEntryPrice - middleEntryPrice * (_profitPercent.ValueDecimal / 100);
            }

            for (int i = 0; i < positions.Count; i++)
            {
                _tab.CloseAtProfitMarket(positions[i], profitPrice, "ProfitActivate");
            }

            // stop-loss

            decimal stopPrice = 0;

            if (positions[0].Direction == Side.Buy)
            {
                stopPrice = middleEntryPrice - middleEntryPrice * (_stopPercent.ValueDecimal / 100);
            }
            else if (positions[0].Direction == Side.Sell)
            {
                stopPrice = middleEntryPrice + middleEntryPrice * (_stopPercent.ValueDecimal / 100);
            }

            for (int i = 0; i < positions.Count; i++)
            {
                _tab.CloseAtStopMarket(positions[i], stopPrice, "StopActivate");
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