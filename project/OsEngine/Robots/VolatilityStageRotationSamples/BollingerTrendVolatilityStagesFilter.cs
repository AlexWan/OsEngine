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

namespace OsEngine.Robots.VolatilityStageRotationSamples
{
    [Bot("BollingerTrendVolatilityStagesFilter")]
    public class BollingerTrendVolatilityStagesFilter : BotPanel
    {
        private BotTabSimple _tab;

        private Aindicator _bollinger;

        private Aindicator _volatilityStages;

        public StrategyParameterString Regime;

        public StrategyParameterInt BollingerLength;

        public StrategyParameterDecimal BollingerDeviation;

        public StrategyParameterBool VolatilityFilterIsOn;

        public StrategyParameterString VolatilityStageToTrade;

        public StrategyParameterInt VolatilitySlowSmaLength;

        public StrategyParameterInt VolatilityFastSmaLength;

        public StrategyParameterDecimal VolatilityChannelDeviation;

        public StrategyParameterString VolumeType;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString TradeAssetInPortfolio;

        public BollingerTrendVolatilityStagesFilter(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });

            VolumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            BollingerLength = CreateParameter("Bollinger length", 30, 10, 80, 3);
            BollingerDeviation = CreateParameter("Bollinger deviation", 2, 1.0m, 50, 4);

            VolatilityFilterIsOn = CreateParameter("Volatility filter is on", false);
            VolatilityStageToTrade = CreateParameter("Volatility stage to trade", "2", new[] { "1", "2", "3", "4" });
            VolatilitySlowSmaLength = CreateParameter("Volatility slow sma length", 25, 10, 80, 3);
            VolatilityFastSmaLength = CreateParameter("Volatility fast sma length", 7, 10, 80, 3);
            VolatilityChannelDeviation = CreateParameter("Volatility channel deviation", 0.5m, 1.0m, 50, 4);

            _bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
            _bollinger = (Aindicator)_tab.CreateCandleIndicator(_bollinger, "Prime");
            _bollinger.ParametersDigit[0].Value = BollingerLength.ValueInt;
            _bollinger.ParametersDigit[1].Value = BollingerDeviation.ValueDecimal;
            _bollinger.Save();

            _volatilityStages = IndicatorsFactory.CreateIndicatorByName("VolatilityStagesAW", name + "VolatilityStages", false);
            _volatilityStages = (Aindicator)_tab.CreateCandleIndicator(_volatilityStages, "VolatilityStagesArea");
            _volatilityStages.ParametersDigit[0].Value = VolatilitySlowSmaLength.ValueInt;
            _volatilityStages.ParametersDigit[1].Value = VolatilityFastSmaLength.ValueInt;
            _volatilityStages.ParametersDigit[2].Value = VolatilityChannelDeviation.ValueDecimal;

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            ParametrsChangeByUser += Event_ParametrsChangeByUser;
        }

        void Event_ParametrsChangeByUser()
        {
            if (BollingerLength.ValueInt != _bollinger.ParametersDigit[0].Value ||
                BollingerLength.ValueInt != _bollinger.ParametersDigit[1].Value)
            {
                _bollinger.ParametersDigit[0].Value = BollingerLength.ValueInt;
                _bollinger.ParametersDigit[1].Value = BollingerDeviation.ValueDecimal;

                _bollinger.Reload();
            }

            if (_volatilityStages.ParametersDigit[0].Value != VolatilitySlowSmaLength.ValueInt
                || _volatilityStages.ParametersDigit[1].Value != VolatilityFastSmaLength.ValueInt
                || _volatilityStages.ParametersDigit[2].Value != VolatilityChannelDeviation.ValueDecimal)
            {
                _volatilityStages.ParametersDigit[0].Value = VolatilitySlowSmaLength.ValueInt;
                _volatilityStages.ParametersDigit[1].Value = VolatilityFastSmaLength.ValueInt;
                _volatilityStages.ParametersDigit[2].Value = VolatilityChannelDeviation.ValueDecimal;

                _volatilityStages.Reload();
            }
        }

        public override string GetNameStrategyType()
        {
            return "BollingerTrendVolatilityStagesFilter";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // logic

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
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
                if (Regime.ValueString == "OnlyClosePosition")
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
                && Regime.ValueString != "OnlyShort")
            {
                if (VolatilityFilterIsOn.ValueBool == true)
                {
                    decimal stage = _volatilityStages.DataSeries[0].Values[_volatilityStages.DataSeries[0].Values.Count - 2];

                    if (stage != VolatilityStageToTrade.ValueString.ToDecimal())
                    {
                        return;
                    }
                }

                _tab.BuyAtMarket(GetVolume(_tab));
            }
            if (lastPrice < lastPcDown
                && Regime.ValueString != "OnlyLong")
            {
                if (VolatilityFilterIsOn.ValueBool == true)
                {
                    decimal stage = _volatilityStages.DataSeries[0].Values[_volatilityStages.DataSeries[0].Values.Count - 2];

                    if (stage != VolatilityStageToTrade.ValueString.ToDecimal())
                    {
                        return;
                    }
                }

                _tab.SellAtMarket(GetVolume(_tab));
            }
        }

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

        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (VolumeType.ValueString == "Contracts")
            {
                volume = Volume.ValueDecimal;
            }
            else if (VolumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = Volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Securiti.Lot != 0 &&
                        tab.Securiti.Lot > 1)
                    {
                        volume = Volume.ValueDecimal / (contractPrice * tab.Securiti.Lot);
                    }

                    volume = Math.Round(volume, tab.Securiti.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (VolumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (TradeAssetInPortfolio.ValueString == "Prime")
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
                        if (positionOnBoard[i].SecurityNameCode == TradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + TradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (Volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Securiti.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    qty = Math.Round(qty, tab.Securiti.DecimalsVolume);
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