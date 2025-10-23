/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Language;

namespace OsEngine.Robots.PositionsMicromanagement
{
    [Bot("TwoEntrySample")] // We create an attribute so that we don't write anything to the BotFactory
    public class TwoEntrySample : BotPanel
    { 
        private BotTabSimple _tab;

        // Basic setting
        private StrategyParameterString _regime;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterDecimal _envelopDeviation;
        private StrategyParameterInt _envelopMovingLength;
        private StrategyParameterInt _pcLength;

        // Indicator
        private Aindicator _envelop;
        private Aindicator _pc;

        public TwoEntrySample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic setting
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _envelopDeviation = CreateParameter("Envelop deviation", 0.3m, 0.3m, 4, 0.3m);
            _envelopMovingLength = CreateParameter("Envelop moving length", 10, 10, 200, 5);
            _pcLength = CreateParameter("Price channel length", 20, 5, 50, 1);

            // Create indicator PriceChannel
            _pc = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PriceChannel", false);
            _pc = (Aindicator)_tab.CreateCandleIndicator(_pc, "Prime");
            _pc.ParametersDigit[0].Value = _pcLength.ValueInt;
            _pc.ParametersDigit[1].Value = _pcLength.ValueInt;

            // Create indicator Envelops
            _envelop = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "Envelops", false);
            _envelop = (Aindicator)_tab.CreateCandleIndicator(_envelop, "Prime");
            _envelop.ParametersDigit[0].Value = _envelopMovingLength.ValueInt;
            _envelop.ParametersDigit[1].Value = _envelopDeviation.ValueDecimal;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += EnvelopTrend_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel84;
        }

        private void EnvelopTrend_ParametrsChangeByUser()
        {
            if (_pc.ParametersDigit[0].Value != _pcLength.ValueInt
                || _pc.ParametersDigit[1].Value != _pcLength.ValueInt)
            {
                _pc.ParametersDigit[0].Value = _pcLength.ValueInt;
                _pc.ParametersDigit[1].Value = _pcLength.ValueInt;
                _pc.Reload();
                _pc.Save();
            }

            if (_envelop.ParametersDigit[0].Value != _envelopMovingLength.ValueInt ||
                _envelop.ParametersDigit[1].Value != _envelopDeviation.ValueDecimal)
            {
                _envelop.ParametersDigit[0].Value = _envelopMovingLength.ValueInt;
                _envelop.ParametersDigit[1].Value = _envelopDeviation.ValueDecimal;
            }
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "TwoEntrySample";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Trade logic
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString != "On")
            {
                return;
            }

            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 5)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null 
                || openPositions.Count == 0)
            {
                LogicOpenPriceChannel(candles);
                LogicOpenEnvelops(candles);
            }
            else if(openPositions.Count == 1)
            {
                Position firstPos = openPositions[0];

                if(firstPos.SignalTypeOpen == "PriceChannel")
                {
                    LogicOpenEnvelops(candles);
                }
                else if(firstPos.SignalTypeOpen == "Envelops")
                {
                    LogicOpenPriceChannel(candles);
                }
            }
            
            for(int i = 0;i < openPositions.Count;i++)
            {
                if (openPositions[i].SignalTypeOpen == "PriceChannel")
                {
                    LogicClosePriceChannel(openPositions[i]);
                }
                else if (openPositions[i].SignalTypeOpen == "Envelops")
                {
                    LogicCloseEnvelops(openPositions[i]);
                }
            }
        }

        // Opening PriceChannel logic
        private void LogicOpenPriceChannel(List<Candle> candles)
        {
            decimal upChannelPc = _pc.DataSeries[0].Values[_pc.DataSeries[0].Values.Count - 2];

            if(upChannelPc == 0)
            {
                return;
            }

            decimal lastPrice = candles[candles.Count - 1].Close;

            if(lastPrice > upChannelPc)
            {
                decimal volume = GetVolume(_tab);

                _tab.BuyAtMarket(volume, "PriceChannel");
            }
        }

        // Close PriceChannel logic
        private void LogicClosePriceChannel(Position position)
        {
            if (position.State != PositionStateType.Open)
            {
                return;
            }

            decimal downChannel = _pc.DataSeries[1].Values[_pc.DataSeries[1].Values.Count - 1];

            _tab.CloseAtTrailingStopMarket(position, downChannel);
        }

        // Opening Envelops logic
        private void LogicOpenEnvelops(List<Candle> candles)
        {
            decimal upChannel = _envelop.DataSeries[0].Values[_envelop.DataSeries[0].Values.Count - 1];

            if (upChannel == 0)
            {
                return;
            }

            decimal lastPrice = candles[candles.Count - 1].Close;

            if (lastPrice > upChannel)
            {
                decimal volume = GetVolume(_tab);

                _tab.BuyAtMarket(volume, "Envelops");
            }
        }

        // Close Envelops logic
        private void LogicCloseEnvelops(Position position)
        {
            if (position.State != PositionStateType.Open)
            {
                return;
            }

            decimal downChannel = _envelop.DataSeries[2].Values[_envelop.DataSeries[0].Values.Count - 1];

            _tab.CloseAtTrailingStopMarket(position, downChannel);
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