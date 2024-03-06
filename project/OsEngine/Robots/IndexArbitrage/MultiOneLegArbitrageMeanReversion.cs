using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using System;
using System.Linq;

namespace OsEngine.Robots.IndexArbitrage
{
    [Bot("MultiOneLegArbitrageMeanReversion")]
    public class MultiOneLegArbitrageMeanReversion : BotPanel
    {
        public MultiOneLegArbitrageMeanReversion(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Index);
            _index = TabsIndex[0];
            _index.SpreadChangeEvent += _index_SpreadChangeEvent;

            TabCreate(BotTabType.Screener);
            _screener = TabsScreener[0];
            _screener.CandleFinishedEvent += _screener_CandleFinishedEvent;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort"});

            RegimeClosePosition = CreateParameter("Regime Close Position", "Reverse signal", new[] { "Reverse signal", "No signal" });

            MaxPositionsCount = CreateParameter("Max poses count", 3, 1, 50, 4);

            MoneyPercentFromDepoOnPosition = CreateParameter("Percent depo on position", 25m, 0.1m, 50, 0.1m);

            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            SlippagePercent = CreateParameter("Slippage percent", 0.1m, 0.1m, 5, 0.1m);

            CorrelationCandlesLookBack = CreateParameter("Correlation candles look back", 100, 1, 50, 4);

            CointegrationCandlesLookBack = CreateParameter("Cointegration candles look back", 100, 1, 50, 4);

            CointegrationStandartDeviationMult = CreateParameter("Deviation mult", 1m, 0.1m, 50, 0.1m);

            CorrelatioinMinValue = CreateParameter("Correlatioin min value", 0.8m, 0.1m, 1, 0.1m);

            Description =
                    "Securities that deviate from the broad market without momentum are traded on a return to the index. ";
        }

        public override string GetNameStrategyType()
        {
            return "MultiOneLegArbitrageMeanReversion";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private BotTabIndex _index;

        private BotTabScreener _screener;

        public StrategyParameterString Regime;

        public StrategyParameterString RegimeClosePosition;

        public StrategyParameterInt MaxPositionsCount;

        public StrategyParameterDecimal MoneyPercentFromDepoOnPosition;

        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterDecimal SlippagePercent;

        public StrategyParameterInt CorrelationCandlesLookBack;

        public StrategyParameterInt CointegrationCandlesLookBack;

        public StrategyParameterDecimal CointegrationStandartDeviationMult;

        public StrategyParameterDecimal CorrelatioinMinValue;

        // logic open poses

        private void _index_SpreadChangeEvent(List<Candle> index)
        {
            if(Regime.ValueString == "Off")
            {
                return;
            }

            if(_screener.Tabs.Count == 0)
            {
                SendNewLogMessage("Screener tab is empty",Logging.LogMessageType.Error);
                return;
            }

            List<BotTabSimple> tabsToTrade = _screener.Tabs;

            for(int i = 0;i < tabsToTrade.Count;i++)
            {
                if(_screener.PositionsOpenAll.Count >= MaxPositionsCount.ValueInt)
                {
                    return;
                }

                LogicOpenPositions(index, tabsToTrade[i]);
            }
        }

        private void LogicOpenPositions(List<Candle> candlesIndex, BotTabSimple tab)
        {
            if(tab.IsConnected == false)
            {
                return;
            }

            if(tab.IsReadyToTrade == false)
            {
                return;
            }

            List<Candle> candlesSecurity = tab.CandlesFinishedOnly;

            if(candlesSecurity == null ||
                candlesSecurity.Count == 0)
            {
                return;
            }

            List<Position> posesOpenBySecurity = tab.PositionsOpenAll;

            if(posesOpenBySecurity.Count> 0)
            {
                return;
            }

            if(CorrelationCandlesLookBack.ValueInt == 0)
            {
                return;
            }

            CorrelationBuilder correlationIndicator = new CorrelationBuilder();

            PairIndicatorValue correlation = 
                correlationIndicator.ReloadCorrelationLast(candlesIndex, candlesSecurity, CorrelationCandlesLookBack.ValueInt);

            if(correlation == null ||
                correlation.Value < CorrelatioinMinValue.ValueDecimal)
            {
                return;
            }

            CointegrationBuilder cointegrationIndicator = new CointegrationBuilder();
            cointegrationIndicator.CointegrationLookBack = CointegrationCandlesLookBack.ValueInt;
            cointegrationIndicator.CointegrationDeviation = CointegrationStandartDeviationMult.ValueDecimal;
            cointegrationIndicator.ReloadCointegration(candlesIndex, candlesSecurity, false);

            if(cointegrationIndicator.Cointegration == null 
                || cointegrationIndicator.Cointegration.Count == 0)
            {
                return;
            }

            if(cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Up
                && Regime.ValueString != "OnlyShort")
            { // nead to short security
                BuySecurity(tab, CointegrationLineSide.Up.ToString());
            }

            if (cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Down
                && Regime.ValueString != "OnlyLong")
            { // nead to long security
                SellSecurity(tab, CointegrationLineSide.Down.ToString());
            }
        }

        private void BuySecurity(BotTabSimple tab, string signal)
        {
            decimal price = tab.PriceBestAsk;

            if(SlippagePercent.ValueDecimal != 0)
            {
                price = price + price * (SlippagePercent.ValueDecimal / 100);
                price = Math.Round(price, tab.Securiti.Decimals);
            }

            decimal volume = GetVolume(tab);

            if(price == 0 ||
                volume == 0)
            {
                return;
            }

            tab.BuyAtLimit(volume, price, signal);
        }

        private void SellSecurity(BotTabSimple tab, string signal)
        {
            decimal price = tab.PriceBestBid;

            if (SlippagePercent.ValueDecimal != 0)
            {
                price = price - price * (SlippagePercent.ValueDecimal / 100);
                price = Math.Round(price, tab.Securiti.Decimals);
            }

            decimal volume = GetVolume(tab);

            if (price == 0 ||
                volume == 0)
            {
                return;
            }

            tab.SellAtLimit(volume, price, signal);
        }

        private decimal GetVolume(BotTabSimple tab)
        {
            Portfolio myPortfolio = tab.Portfolio;

            if(myPortfolio == null)
            {
                return 0;
            }

            decimal portfolioPrimeAsset = 0;

            if(TradeAssetInPortfolio.ValueString == "Prime")
            {
                portfolioPrimeAsset = myPortfolio.ValueCurrent;
            }
            else
            {
                List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                if(positionOnBoard == null)
                {
                    return 0;
                }

                for(int i = 0;i < positionOnBoard.Count;i++)
                {
                    if (positionOnBoard[i].SecurityNameCode == TradeAssetInPortfolio.ValueString)
                    {
                        portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                        break;
                    }
                }
            }

            if(portfolioPrimeAsset == 0)
            {
                SendNewLogMessage("Can`t found portfolio " + TradeAssetInPortfolio.ValueString,Logging.LogMessageType.Error);
                return 0;
            }

            decimal moneyOnPosition = portfolioPrimeAsset * (MoneyPercentFromDepoOnPosition.ValueDecimal / 100);

            decimal qty = moneyOnPosition / tab.PriceBestAsk;

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

        // logic close poses

        private void _screener_CandleFinishedEvent(List<Candle> candlesSecurity, BotTabSimple tab)
        {
            if (tab.IsConnected == false)
            {
                return;
            }

            if (tab.IsReadyToTrade == false)
            {
                return;
            }

            if (Regime.ValueString == "Off")
            {
                return;
            }

            List<Position> poses = tab.PositionsOpenAll;

            if(poses.Count == 0)
            {
                return;
            }
            Position pos = poses[0];

            List<Candle> candlesIndex = _index.Candles;

            if(candlesIndex == null ||
                candlesIndex.Count == 0)
            {
                return;
            }

            CointegrationBuilder cointegrationIndicator = new CointegrationBuilder();
            cointegrationIndicator.CointegrationLookBack = CointegrationCandlesLookBack.ValueInt;
            cointegrationIndicator.CointegrationDeviation = CointegrationStandartDeviationMult.ValueDecimal;
            cointegrationIndicator.ReloadCointegration(candlesIndex, candlesSecurity, false);

            if (cointegrationIndicator.Cointegration == null
                || cointegrationIndicator.Cointegration.Count == 0)
            {
                return;
            }

            if (pos.SignalTypeOpen == "Up")
            {
                //"Reverse signal", "No signal"
                if (RegimeClosePosition.ValueString == "Reverse signal"
                    && cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Down)
                {
                    ClosePosition(pos, tab);
                }
                else if (RegimeClosePosition.ValueString == "No signal"
                    && cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.No)
                {
                    ClosePosition(pos, tab);
                }
            }
            else if(pos.SignalTypeOpen == "Down")
            {
                //"Reverse signal", "No signal"
                if (RegimeClosePosition.ValueString == "Reverse signal"
                    && cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Up)
                {
                    ClosePosition(pos, tab);
                }
                else if (RegimeClosePosition.ValueString == "No signal"
                    && cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.No)
                {
                    ClosePosition(pos, tab);
                }
            }
        }

        private void ClosePosition(Position pos, BotTabSimple tab)
        {
            if(pos.State != PositionStateType.Open)
            {
                return;
            }

            decimal price = 0;
            decimal volume = pos.OpenVolume;

            if(pos.Direction == Side.Buy)
            {
                price = tab.PriceBestBid;

                if (SlippagePercent.ValueDecimal != 0)
                {
                    price = price - price * (SlippagePercent.ValueDecimal / 100);
                    price = Math.Round(price, tab.Securiti.Decimals);
                }
            }
            else if(pos.Direction == Side.Sell)
            {
                price = tab.PriceBestAsk;

                if (SlippagePercent.ValueDecimal != 0)
                {
                    price = price + price * (SlippagePercent.ValueDecimal / 100);
                    price = Math.Round(price, tab.Securiti.Decimals);
                }
            }

            tab.CloseAtLimit(pos, price, volume);
        }
    }
}