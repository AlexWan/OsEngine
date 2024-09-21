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
    [Bot("PriceChannelCounterTrend")]
    public class PriceChannelCounterTrend : BotPanel
    {
        private BotTabSimple _tab;

        private Aindicator _pc;

        public StrategyParameterString Regime;

        public StrategyParameterInt PriceChannelLength;

        public StrategyParameterString VolumeType;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterDecimal StopPercent;

        public StrategyParameterDecimal ProfitOrderOnePercent;

        public StrategyParameterDecimal ProfitOrderTwoPercent;

        public PriceChannelCounterTrend(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });

            VolumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            PriceChannelLength = CreateParameter("Price channel length", 50, 10, 80, 3);

            StopPercent = CreateParameter("Stop percent", 0.7m, 1.0m, 50, 4);
            ProfitOrderOnePercent = CreateParameter("Profit order one percent", 0.3m, 1.0m, 50, 4);
            ProfitOrderTwoPercent = CreateParameter("Profit order two percent", 0.7m, 1.0m, 50, 4);

            _pc = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PriceChannel", false);
            _pc = (Aindicator)_tab.CreateCandleIndicator(_pc, "Prime");
            _pc.ParametersDigit[0].Value = PriceChannelLength.ValueInt;
            _pc.ParametersDigit[1].Value = PriceChannelLength.ValueInt;
            _pc.Save();

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            ParametrsChangeByUser += Event_ParametrsChangeByUser;

            _tab.ManualPositionSupport.DisableManualSupport();

            Description = "An example of a robot that shows closing a position via two close limit orders.";
        }

        void Event_ParametrsChangeByUser()
        {
            if (PriceChannelLength.ValueInt != _pc.ParametersDigit[0].Value ||
                PriceChannelLength.ValueInt != _pc.ParametersDigit[1].Value)
            {
                _pc.ParametersDigit[0].Value = PriceChannelLength.ValueInt;
                _pc.ParametersDigit[1].Value = PriceChannelLength.ValueInt;
                _pc.Reload();
                _pc.Save();
            }
        }

        public override string GetNameStrategyType()
        {
            return "PriceChannelCounterTrend";
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
                if (Regime.ValueString == "OnlyClosePosition")
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
                && Regime.ValueString != "OnlyLong")
            {
                _tab.SellAtMarket(GetVolume(_tab), "First");
                _tab.SellAtMarket(GetVolume(_tab), "Second");
            }
            if (lastPrice < lastPcDown
                && Regime.ValueString != "OnlyShort")
            {
                _tab.BuyAtMarket(GetVolume(_tab), "First");
                _tab.BuyAtMarket(GetVolume(_tab), "Second");
            }
        }

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
                    price = position.EntryPrice - position.EntryPrice * (StopPercent.ValueDecimal / 100);
                }
                else if (position.Direction == Side.Sell)
                {
                    price = position.EntryPrice + position.EntryPrice * (StopPercent.ValueDecimal / 100);
                }

                _tab.CloseAtStopMarket(position, price, "StopActivate");
            }

            if (position.SignalTypeOpen == "First"
                && position.CloseActiv == false)
            {
                decimal price = 0;

                if (position.Direction == Side.Buy)
                {
                    price = position.EntryPrice + position.EntryPrice * (ProfitOrderOnePercent.ValueDecimal / 100);
                }
                else if (position.Direction == Side.Sell)
                {
                    price = position.EntryPrice - position.EntryPrice * (ProfitOrderOnePercent.ValueDecimal / 100);
                }


                _tab.CloseAtLimit(position, price, position.OpenVolume);
            }

            if (position.SignalTypeOpen == "Second"
                && position.CloseActiv == false)
            {
                decimal price = 0;

                if (position.Direction == Side.Buy)
                {
                    price = position.EntryPrice + position.EntryPrice * (ProfitOrderTwoPercent.ValueDecimal / 100);
                }
                else if (position.Direction == Side.Sell)
                {
                    price = position.EntryPrice - position.EntryPrice * (ProfitOrderTwoPercent.ValueDecimal / 100);
                }


                _tab.CloseAtLimit(position, price, position.OpenVolume);
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