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

namespace OsEngine.Robots.Screeners
{
    [Bot("PinBarScreener")]
    public class PinBarScreener : BotPanel
    {
        public PinBarScreener(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _tabScreener = TabsScreener[0];

            _tabScreener.CandleFinishedEvent += _tab_CandleFinishedEvent1;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });

            MaxPositions = CreateParameter("Max positions", 5, 0, 20, 1);

            VolumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });

            Volume = CreateParameter("Volume", 1, 1.0m, 50, 4);

            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            Slippage = CreateParameter("Slippage %", 0, 0, 20, 1m);

            MaxHeightCandlesPercent = CreateParameter("Max height candles percent", 1.1m, 0, 20, 1m);
            MinHeightCandlesPercent = CreateParameter("Min height candles percent", 0.5m, 0, 20, 1m);
            
            TrailStop = CreateParameter("Trail stop %", 0.5m, 0, 20, 1m);

            SmaPeriod = CreateParameter("Sma Period", 100, 10, 50, 500);
        }

        public override string GetNameStrategyType()
        {
            return "PinBarScreener";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private BotTabScreener _tabScreener;

        // settings

        public StrategyParameterString Regime;
        public StrategyParameterInt MaxPositions;
        public StrategyParameterDecimal ProcHeightTake;
        public StrategyParameterDecimal ProcHeightStop;
        public StrategyParameterDecimal Slippage;
        public StrategyParameterString VolumeType;
        public StrategyParameterDecimal Volume;
        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterDecimal MinHeightCandlesPercent;
        public StrategyParameterDecimal MaxHeightCandlesPercent;

        public StrategyParameterInt SmaPeriod;
        public StrategyParameterDecimal TrailStop;

        // logic

        private void _tab_CandleFinishedEvent1(List<Candle> candles, BotTabSimple tab)
        {
            Logic(candles, tab);
        }

        private void Logic(List<Candle> candles, BotTabSimple tab)
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

            decimal lastClose = candles[candles.Count - 1].Close;
            decimal lastOpen = candles[candles.Count - 1].Open;
            decimal lastHigh = candles[candles.Count - 1].High;
            decimal lastLow = candles[candles.Count - 1].Low;
            decimal lastSma = Sma(candles, SmaPeriod.ValueInt, candles.Count - 1);

            decimal lenCandlePercent = (lastHigh - lastLow) / (lastLow / 100);

            if(lenCandlePercent > MaxHeightCandlesPercent.ValueDecimal ||
                lenCandlePercent < MinHeightCandlesPercent.ValueDecimal)
            {
                return;
            }

            if (lastClose >= lastHigh - ((lastHigh - lastLow) / 3) && lastOpen >= lastHigh - ((lastHigh - lastLow) / 3)
                && lastSma < lastClose
                && Regime.ValueString != "OnlyShort")
            {
                tab.BuyAtLimit(GetVolume(tab), lastClose + lastClose * (Slippage.ValueDecimal / 100));
            }
            if (lastClose <= lastLow + ((lastHigh - lastLow) / 3) && lastOpen <= lastLow + ((lastHigh - lastLow) / 3)
                && lastSma > lastClose
            && Regime.ValueString != "OnlyLong")
            {
                tab.SellAtLimit(GetVolume(tab), lastClose - lastClose * (Slippage.ValueDecimal / 100));
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

        private decimal Sma(List<Candle> candles, int len, int index)
        {
            if (candles.Count == 0
                || index >= candles.Count
                || index <= 0)
            {
                return 0;
            }

            decimal summ = 0;

            int countPoints = 0;

            for (int i = index; i >= 0 && i > index - len; i--)
            {
                countPoints++;
                summ += candles[i].Close;
            }

            if (countPoints == 0)
            {
                return 0;
            }

            return summ / countPoints;
        }
    }
}