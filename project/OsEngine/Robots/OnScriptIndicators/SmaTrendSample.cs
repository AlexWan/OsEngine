/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

﻿using OsEngine.Charts.CandleChart.Indicators;
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

Trend robot SmaTrendSample.

Buy: lastCandlePrice > smaValue and lastCandlePrice > upChannel.

Sell: lastCandlePrice < smaValue and lastCandlePrice < downChannel.

Exit: By TralingStop.
*/

namespace OsEngine.Robots.OnScriptIndicators
{
    [Bot("SmaTrendSample")] // We create an attribute so that we don't write anything to the BotFactory
    public class SmaTrendSample : BotPanel
    {
        private BotTabSimple _tab;

        // Indicators
        private Aindicator _sma;
        private Aindicator _envelop;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _slippage;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _smaLength;
        private StrategyParameterInt _envelopLength;
        private StrategyParameterDecimal _envelopDeviation;

        // Exit setting
        private StrategyParameterDecimal _baseStopPercent;

        public SmaTrendSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On"});
            _slippage = CreateParameter("Slippage", 0, 0, 20, 1);

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _smaLength = CreateParameter("Sma length", 30, 0, 20, 1);
            _envelopLength = CreateParameter("Envelop length", 30, 0, 20, 1);
            _envelopDeviation = CreateParameter("Envelop Deviation", 1, 1.0m, 50, 4);

            // Exit setting
            _baseStopPercent = CreateParameter("Base Stop Percent", 0.3m, 1.0m, 50, 4);

            // Create indicator Sma
            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "sma", false);
            _sma = (Aindicator)_tab.CreateCandleIndicator(_sma, "Prime");
            _sma.ParametersDigit[0].Value = _smaLength.ValueInt;
            _sma.Save();

            // Create indicator Envelops
            _envelop = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "env", false);
            _envelop = (Aindicator)_tab.CreateCandleIndicator(_envelop, "Prime");
            _envelop.ParametersDigit[0].Value = _envelopLength.ValueInt;
            _envelop.ParametersDigit[1].Value = _envelopDeviation.ValueDecimal;
            _envelop.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += SmaTrendSample_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel67;
        }

        private void SmaTrendSample_ParametrsChangeByUser()
        {
            if(_smaLength.ValueInt != _sma.ParametersDigit[0].Value)
            {
                _sma.ParametersDigit[0].Value = _smaLength.ValueInt;
                _sma.Save();
                _sma.Reload();
            }

            if (_envelopLength.ValueInt != _envelop.ParametersDigit[0].Value ||
                _envelopDeviation.ValueDecimal != _envelop.ParametersDigit[1].Value)
            {
                _envelop.ParametersDigit[0].Value = _envelopLength.ValueInt;
                _envelop.ParametersDigit[1].Value = _envelopDeviation.ValueDecimal;
                _envelop.Save();
                _envelop.Reload();
            }
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "SmaTrendSample";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
            
        }

        // Logic
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if(candles.Count < _smaLength.ValueInt +1)
            {
                return;
            }

            if(_regime.ValueString == "Off")
            {
                return;
            }

            List<Position> poses = _tab.PositionsOpenAll;

            if(poses.Count == 0)
            {
                OpenPositionLogic(candles);
            }
            else
            {
                ClosePositionLogic(poses[0],candles);
            }
        }

        // Opening position logic
        private void OpenPositionLogic(List<Candle> candles)
        {
            decimal smaValue = _sma.DataSeries[0].Last;

            if(smaValue == 0)
            {
                return;
            }

            decimal lastCandlePrice = candles[candles.Count-1].Close;

            decimal upChannel = _envelop.DataSeries[0].Last;
            decimal downChannel = _envelop.DataSeries[2].Last;

            if(upChannel == 0 ||
                downChannel == 0)
            {
                return;
            }

            if (lastCandlePrice > smaValue &&
                lastCandlePrice > upChannel)
            {
                _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _tab.Security.PriceStep * _slippage.ValueInt);
            }

            if (lastCandlePrice < smaValue &&
                lastCandlePrice < downChannel)
            {
                _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid + _tab.Security.PriceStep * _slippage.ValueInt);
            }
        }

        // Close position logic
        private void ClosePositionLogic(Position position, List<Candle> candles)
        {
            if(position.State == PositionStateType.Closing)
            {
                return;
            }

            decimal stopPrice = position.EntryPrice - position.EntryPrice * _baseStopPercent.ValueDecimal/100;

            if (position.Direction == Side.Sell)
            {
                stopPrice = position.EntryPrice + position.EntryPrice * _baseStopPercent.ValueDecimal / 100;
            }

            decimal smaValue = _sma.DataSeries[0].Last;

            decimal lastCandlePrice = candles[candles.Count - 1].Close;

            if (position.Direction == Side.Buy &&
                smaValue > stopPrice 
                && lastCandlePrice > smaValue)
            {
                stopPrice = smaValue;
            }

            if (position.Direction == Side.Sell &&
                smaValue < stopPrice 
                && lastCandlePrice < smaValue)
            {
                stopPrice = smaValue;
            }

            decimal priceOrder = stopPrice;

            if(StartProgram == StartProgram.IsOsTrader)
            {
                if (position.Direction == Side.Buy)
                {
                    priceOrder = priceOrder - _tab.Security.PriceStep * _slippage.ValueInt;
                }

                if (position.Direction == Side.Sell)
                {
                    priceOrder = priceOrder + _tab.Security.PriceStep * _slippage.ValueInt;
                }
            }

            _tab.CloseAtTrailingStop(position, stopPrice, priceOrder);
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