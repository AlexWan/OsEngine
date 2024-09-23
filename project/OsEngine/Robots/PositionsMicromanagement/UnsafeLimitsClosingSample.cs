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

namespace OsEngine.Robots.PositionsMicromanagement
{
    [Bot("UnsafeLimitsClosingSample")]
    public class UnsafeLimitsClosingSample : BotPanel
    {
        private BotTabSimple _tab;

        private Aindicator _envelop;

        public StrategyParameterString Regime;

        public StrategyParameterInt EnvelopsLength;

        public StrategyParameterDecimal EnvelopsDeviation;

        public StrategyParameterString VolumeType;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterDecimal StopPercent;

        public StrategyParameterDecimal ProfitLimitOnePercent;

        public StrategyParameterDecimal ProfitLimitTwoPercent;

        public UnsafeLimitsClosingSample(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });

            VolumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            EnvelopsLength = CreateParameter("Envelops length", 50, 10, 80, 3);
            EnvelopsDeviation = CreateParameter("Envelops deviation", 0.1m, 0.5m, 5, 0.1m);

            StopPercent = CreateParameter("Stop percent", 0.5m, 1.0m, 50, 4);
            ProfitLimitOnePercent = CreateParameter("Profit limit one percent", 0.4m, 1.0m, 50, 4);
            ProfitLimitTwoPercent = CreateParameter("Profit limit two percent", 0.8m, 1.0m, 50, 4);

            _envelop = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "Envelops", false);
            _envelop = (Aindicator)_tab.CreateCandleIndicator(_envelop, "Prime");
            _envelop.ParametersDigit[0].Value = EnvelopsLength.ValueInt;
            _envelop.ParametersDigit[1].Value = EnvelopsDeviation.ValueDecimal;
            _envelop.Save();

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            ParametrsChangeByUser += Event_ParametrsChangeByUser;

            _tab.ManualPositionSupport.DisableManualSupport();

            Description = "Example of using a non-safe method of closing positions with multiple limit orders at the same time";
        }

        void Event_ParametrsChangeByUser()
        {
            if (EnvelopsLength.ValueInt != _envelop.ParametersDigit[0].Value ||
               _envelop.ParametersDigit[1].Value != EnvelopsDeviation.ValueDecimal)
            {
                _envelop.ParametersDigit[0].Value = EnvelopsLength.ValueInt;
                _envelop.ParametersDigit[1].Value = EnvelopsDeviation.ValueDecimal;
                _envelop.Reload();
                _envelop.Save();
            }
        }

        public override string GetNameStrategyType()
        {
            return "UnsafeLimitsClosingSample";
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
            decimal lastEnvelopsUp = _envelop.DataSeries[0].Values[_envelop.DataSeries[0].Values.Count - 1];
            decimal lastEnvelopsDown = _envelop.DataSeries[2].Values[_envelop.DataSeries[1].Values.Count - 1];

            if (lastEnvelopsUp == 0
                || lastEnvelopsDown == 0)
            {
                return;
            }

            if (lastPrice > lastEnvelopsUp
                && Regime.ValueString != "OnlyLong")
            {
                _tab.SellAtMarket(GetVolume(_tab));
            }
            if (lastPrice < lastEnvelopsDown
                && Regime.ValueString != "OnlyShort")
            {
                _tab.BuyAtMarket(GetVolume(_tab));
            }
        }

        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.SignalTypeClose == "StopActivate"
                || position.SignalTypeClose == "ProfitActivate"
                || position.State == PositionStateType.Opening)
            {
                return;
            }

            // Profit by limit orders

            if(position.CloseActiv == false)
            {
                decimal firstOrderPrice = 0;
                decimal secondOrderPrice = 0;

                if (position.Direction == Side.Buy)
                {
                    firstOrderPrice = position.EntryPrice + position.EntryPrice * (ProfitLimitOnePercent.ValueDecimal / 100);
                    secondOrderPrice = position.EntryPrice + position.EntryPrice * (ProfitLimitTwoPercent.ValueDecimal / 100);
                }
                else if (position.Direction == Side.Sell)
                {
                    firstOrderPrice = position.EntryPrice - position.EntryPrice * (ProfitLimitOnePercent.ValueDecimal / 100);
                    secondOrderPrice = position.EntryPrice - position.EntryPrice * (ProfitLimitTwoPercent.ValueDecimal / 100);
                }

                _tab.CloseAtLimitUnsafe(position, firstOrderPrice, position.OpenVolume / 2);
                _tab.CloseAtLimitUnsafe(position, secondOrderPrice, position.OpenVolume / 2);
            }

            // Stop

            if(position.StopOrderPrice == 0)
            {
                decimal stopPrice = 0;

                if (position.Direction == Side.Buy)
                {
                    stopPrice = position.EntryPrice - position.EntryPrice * (StopPercent.ValueDecimal / 100);
                }
                else if (position.Direction == Side.Sell)
                {
                    stopPrice = position.EntryPrice + position.EntryPrice * (StopPercent.ValueDecimal / 100);
                }

                _tab.CloseAtStopMarket(position, stopPrice, "StopActivate");
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