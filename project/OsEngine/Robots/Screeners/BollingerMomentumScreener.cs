/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Indicators;

namespace OsEngine.Robots.Screeners
{
    [Bot("BollingerMomentumScreener")]
    public class BollingerMomentumScreener : BotPanel
    {
        BotTabScreener _tabScreener;

        public StrategyParameterString Regime;
        public StrategyParameterInt MaxPositions;
        public StrategyParameterInt BollingerLen;
        public StrategyParameterDecimal BollingerDev;
        public StrategyParameterInt MomentumLen;
        public StrategyParameterDecimal Slippage;
        public StrategyParameterString VolumeType;
        public StrategyParameterDecimal Volume;
        public StrategyParameterString TradeAssetInPortfolio;
        public StrategyParameterDecimal TrailStop;
        public StrategyParameterDecimal MinMomentumValue;

        public BollingerMomentumScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
           
            _tabScreener = TabsScreener[0];

            _tabScreener.CandleFinishedEvent += _screenerTab_CandleFinishedEvent;

            _tabScreener.CreateCandleIndicator(1, "Bollinger", new List<string>() { "100" }, "Prime");
            _tabScreener.CreateCandleIndicator(2, "Momentum", new List<string>() { "15" }, "Second");

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyClosePosition" });
            
            MaxPositions = CreateParameter("Max positions", 5, 0, 20, 1);

            MinMomentumValue = CreateParameter("Min momentum value", 105m, 0, 20, 1m);

            BollingerLen = CreateParameter("Bollinger length", 50, 0, 20, 1);

            BollingerDev = CreateParameter("Bollinger deviation", 2m, 0, 20, 1m);

            MomentumLen = CreateParameter("Momentum length", 50, 0, 20, 1);

            TrailStop = CreateParameter("Trail stop %", 2.9m, 0, 20, 1m);

            VolumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            
            Volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");
            
            Slippage = CreateParameter("Slippage %", 0, 0, 20, 1m);
        }

        public override string GetNameStrategyType()
        {
            return "BollingerMomentumScreener";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // logic

        private void _screenerTab_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 5)
            {
                return;
            }

            List<Position> openPositions = tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                if (Regime.ValueString == "OnlyClosePosition")
                {
                    return;
                }
                LogicOpenPosition(candles, tab);
            }
            else
            {
                LogicClosePosition(candles, tab, openPositions[0]);
            }
        }

        private void LogicOpenPosition(List<Candle> candles, BotTabSimple tab)
        {
            if (_tabScreener.PositionsOpenAll.Count >= MaxPositions.ValueInt)
            {
                return;
            }

            decimal lastCandleClose = candles[candles.Count - 1].Close;

            Aindicator bollinger = (Aindicator)tab.Indicators[0];

            if (bollinger.ParametersDigit[0].Value != BollingerLen.ValueInt
                || bollinger.ParametersDigit[1].Value != BollingerDev.ValueDecimal)
            {
                bollinger.ParametersDigit[0].Value = BollingerLen.ValueInt;
                bollinger.ParametersDigit[1].Value = BollingerDev.ValueDecimal;
                bollinger.Save();
                bollinger.Reload();
            }

            if (bollinger.DataSeries[0].Values.Count == 0 ||
                bollinger.DataSeries[0].Last == 0)
            {
                return;
            }

            decimal lastUpBollingerLine = bollinger.DataSeries[0].Last;

            Aindicator momentum = (Aindicator)tab.Indicators[1];

            if (momentum.ParametersDigit[0].Value != MomentumLen.ValueInt)
            {
                momentum.ParametersDigit[0].Value = MomentumLen.ValueInt;
                momentum.Save();
                momentum.Reload();
            }

            if (momentum.DataSeries[0].Values.Count == 0 ||
                momentum.DataSeries[0].Last == 0)
            {
                return;
            }

            decimal lastMomentum = momentum.DataSeries[0].Last;

             if (lastCandleClose > lastUpBollingerLine
                && lastMomentum > MinMomentumValue.ValueDecimal)
             {
                 tab.BuyAtLimit(GetVolume(tab), lastCandleClose + lastCandleClose * (Slippage.ValueDecimal / 100));
             }
        }

        private void LogicClosePosition(List<Candle> candles, BotTabSimple tab, Position position)
        {
            if (position.State != PositionStateType.Open
                          ||
                          (position.CloseOrders != null
                          && position.CloseOrders.Count > 0)
                          )
            {
                return;
            }

            decimal lastClose = candles[candles.Count - 1].Close;

            decimal stop = 0;
            decimal stopWithSlippage = 0;

            if (position.Direction == Side.Buy)
            {
                stop = lastClose - lastClose * (TrailStop.ValueDecimal / 100);
                stopWithSlippage = stop - stop * (Slippage.ValueDecimal / 100);
            }
            else //if (position.Direction == Side.Sell)
            {
                stop = lastClose + lastClose * (TrailStop.ValueDecimal / 100);
                stopWithSlippage = stop + stop * (Slippage.ValueDecimal / 100);
            }

            tab.CloseAtTrailingStop(position, stop, stopWithSlippage);
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