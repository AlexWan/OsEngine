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

namespace OsEngine.Robots.Trend
{
    [Bot("TwoEntrySample")]
    public class TwoEntrySample : BotPanel
    {
        // Parameters

        public StrategyParameterString Regime;

        public StrategyParameterString VolumeType;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterDecimal EnvelopDeviation;

        public StrategyParameterInt EnvelopMovingLength;

        public StrategyParameterInt PcLength;

        public StrategyParameterDecimal TrailStop;

        // indicators

        private Aindicator _envelop;

        private Aindicator _pc;

        // source

        private BotTabSimple _tab;

        public TwoEntrySample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });

            VolumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            EnvelopDeviation = CreateParameter("Envelop deviation", 0.3m, 0.3m, 4, 0.3m);
            EnvelopMovingLength = CreateParameter("Envelop moving length", 10, 10, 200, 5);
            PcLength = CreateParameter("Price channel length", 20, 5, 50, 1);
            TrailStop = CreateParameter("Trail stop envelop", 0.7m, 0.1m, 5, 0.1m);

            _pc = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PriceChannel", false);
            _pc = (Aindicator)_tab.CreateCandleIndicator(_pc, "Prime");
            _pc.ParametersDigit[0].Value = PcLength.ValueInt;
            _pc.ParametersDigit[1].Value = PcLength.ValueInt;

            _envelop = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "Envelops", false);
            _envelop = (Aindicator)_tab.CreateCandleIndicator(_envelop, "Prime");
            _envelop.ParametersDigit[0].Value = EnvelopMovingLength.ValueInt;
            _envelop.ParametersDigit[1].Value = EnvelopDeviation.ValueDecimal;

            ParametrsChangeByUser += EnvelopTrend_ParametrsChangeByUser;
        }

        private void EnvelopTrend_ParametrsChangeByUser()
        {
            if (_pc.ParametersDigit[0].Value != PcLength.ValueInt
                || _pc.ParametersDigit[1].Value != PcLength.ValueInt)
            {
                _pc.ParametersDigit[0].Value = PcLength.ValueInt;
                _pc.ParametersDigit[1].Value = PcLength.ValueInt;
                _pc.Reload();
                _pc.Save();
            }

            if (_envelop.ParametersDigit[0].Value != EnvelopMovingLength.ValueInt ||
                _envelop.ParametersDigit[1].Value != EnvelopDeviation.ValueDecimal)
            {
                _envelop.ParametersDigit[0].Value = EnvelopMovingLength.ValueInt;
                _envelop.ParametersDigit[1].Value = EnvelopDeviation.ValueDecimal;
            }
        }

        public override string GetNameStrategyType()
        {
            return "TwoEntrySample";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // trade logic

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString != "On")
            {
                return;
            }

            if (Regime.ValueString == "Off")
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

        private void LogicClosePriceChannel(Position position)
        {
            if (position.State != PositionStateType.Open)
            {
                return;
            }

            decimal downChannel = _pc.DataSeries[1].Values[_pc.DataSeries[1].Values.Count - 1];

            _tab.CloseAtTrailingStopMarket(position, downChannel);
        }

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

        private void LogicCloseEnvelops(Position position)
        {
            if (position.State != PositionStateType.Open)
            {
                return;
            }

            decimal downChannel = _envelop.DataSeries[2].Values[_envelop.DataSeries[0].Values.Count - 1];

            _tab.CloseAtTrailingStopMarket(position, downChannel);
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