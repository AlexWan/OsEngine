/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.CandleChart.Indicators;
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
Trading robot for osengine.

The trend robot on BreakEnvelops.

Buy:
The price is above the upper Envelops band.

Sell: 
The price is below the lower Envelops band.

Exit: 
Reverse side of the channel.
*/

namespace OsEngine.Robots.Trend
{
    [Bot("EnvelopTrend")] // We create an attribute so that we don't write anything to the BotFactory
    public class EnvelopTrend : BotPanel
    {
        private BotTabSimple _tab;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _slippage;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterDecimal _envelopDeviation;
        private StrategyParameterInt _envelopMovingLength;

        // Indicator
        private Aindicator _envelop;

        // Exit settings
        private StrategyParameterDecimal _trailStop;

        public EnvelopTrend(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _slippage = CreateParameter("Slippage", 0, 0, 20, 1);

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");
            
            // Indicator settings
            _envelopDeviation = CreateParameter("Envelop Deviation", 0.3m, 0.3m, 4, 0.3m);
            _envelopMovingLength = CreateParameter("Envelop Moving Length", 10, 10, 200, 5);

            // Create indicator Envelops
            _envelop = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "Envelops", false);
            _envelop = (Aindicator)_tab.CreateCandleIndicator(_envelop, "Prime");
            ((IndicatorParameterInt)_envelop.Parameters[0]).ValueInt = _envelopMovingLength.ValueInt;
            ((IndicatorParameterDecimal)_envelop.Parameters[1]).ValueDecimal = _envelopDeviation.ValueDecimal;
            _envelop.Save();
            
            // Exit settings
            _trailStop = CreateParameter("Trail Stop", 0.1m, 0.1m, 5, 0.1m);

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Subscribe to the position opening succes event
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += EnvelopTrend_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel112;
        }

        private void EnvelopTrend_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_envelop.Parameters[0]).ValueInt = _envelopMovingLength.ValueInt;
            ((IndicatorParameterDecimal)_envelop.Parameters[1]).ValueDecimal = _envelopDeviation.ValueDecimal;
            _envelop.Save();
            _envelop.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "EnvelopTrend";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Trade logic
        private void _tab_PositionOpeningSuccesEvent(Position position)
        {
            _tab.BuyAtStopCancel();
            _tab.SellAtStopCancel();

            if (position.Direction == Side.Buy)
            {
                decimal activationPrice = _envelop.DataSeries[0].Last -
                    _envelop.DataSeries[0].Last * (_trailStop.ValueDecimal / 100);

                decimal orderPrice = activationPrice - _tab.Security.PriceStep * _slippage.ValueInt;

                _tab.CloseAtTrailingStop(position,
                    activationPrice, orderPrice);
            }
            if (position.Direction == Side.Sell)
            {
                decimal activationPrice = _envelop.DataSeries[2].Last +
                    _envelop.DataSeries[2].Last * (_trailStop.ValueDecimal / 100);

                decimal orderPrice = activationPrice + _tab.Security.PriceStep * _slippage.ValueInt;

                _tab.CloseAtTrailingStop(position,
                    activationPrice, orderPrice);
            }
        }

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString != "On")
            {
                return;
            }

            if(candles.Count +5 < _envelop.DataSeries[0].Values.Count)
            {
                return;
            }

            List<Position> positions = _tab.PositionsOpenAll;

            if(positions.Count == 0)
            { // open logic
                _tab.BuyAtStop(GetVolume(_tab),
                    _envelop.DataSeries[0].Last + 
                    _slippage.ValueInt * _tab.Security.PriceStep,
                    _envelop.DataSeries[0].Last,
                    StopActivateType.HigherOrEqual,1);

                _tab.SellAtStop(GetVolume(_tab),
                     _envelop.DataSeries[2].Last -
                     _slippage.ValueInt * _tab.Security.PriceStep,
                    _envelop.DataSeries[2].Last,
                    StopActivateType.LowerOrEqual, 1);
            }
            else
            { // trail stop logic

                if(positions[0].State != PositionStateType.Open)
                {
                    return;
                }

                if(positions[0].Direction == Side.Buy)
                {
                    decimal activationPrice = _envelop.DataSeries[0].Last -
                        _envelop.DataSeries[0].Last * (_trailStop.ValueDecimal / 100);

                    decimal orderPrice = activationPrice - _tab.Security.PriceStep * _slippage.ValueInt;

                    _tab.CloseAtTrailingStop(positions[0],
                        activationPrice, orderPrice);
                }
                if (positions[0].Direction == Side.Sell)
                {
                    decimal activationPrice = _envelop.DataSeries[2].Last +
                        _envelop.DataSeries[2].Last * (_trailStop.ValueDecimal / 100);

                    decimal orderPrice = activationPrice + _tab.Security.PriceStep * _slippage.ValueInt;

                    _tab.CloseAtTrailingStop(positions[0],
                        activationPrice, orderPrice);
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