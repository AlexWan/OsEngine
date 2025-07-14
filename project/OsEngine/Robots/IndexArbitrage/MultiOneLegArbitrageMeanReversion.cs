/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using System;
using OsEngine.Language;

/* Description
Index Arbitrage robot for OsEngine.

Securities that deviate from the broad market without momentum are traded on a return to the index.
*/

namespace OsEngine.Robots.IndexArbitrage
{
    [Bot("MultiOneLegArbitrageMeanReversion")] // We create an attribute so that we don't write anything to the BotFactory
    public class MultiOneLegArbitrageMeanReversion : BotPanel
    {
        // Index tabs
        private BotTabIndex _index;

        // Screener tabs
        private BotTabScreener _screener;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _maxPositionsCount;
        private StrategyParameterDecimal _moneyPercentFromDepoOnPosition;
        private StrategyParameterDecimal _slippagePercent;

        // GetVolume setting
        private StrategyParameterString _tradeAssetInPortfolio;

        // Cointegration settings
        private StrategyParameterInt _cointegrationCandlesLookBack;
        private StrategyParameterDecimal _cointegrationStandartDeviationMult;

        // Correlation settings
        private StrategyParameterDecimal _correlatioinMinValue;
        private StrategyParameterInt _correlationCandlesLookBack;

        // Exit setting
        private StrategyParameterString _regimeClosePosition;

        public MultiOneLegArbitrageMeanReversion(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Index);
            _index = TabsIndex[0];
            _index.SpreadChangeEvent += _index_SpreadChangeEvent;

            TabCreate(BotTabType.Screener);
            _screener = TabsScreener[0];
            _screener.CandleFinishedEvent += _screener_CandleFinishedEvent;

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort"});
            _maxPositionsCount = CreateParameter("Max poses count", 3, 1, 50, 4);
            _slippagePercent = CreateParameter("Slippage percent", 0.1m, 0.1m, 5, 0.1m);
            _moneyPercentFromDepoOnPosition = CreateParameter("Percent depo on position", 25m, 0.1m, 50, 0.1m);
            
            // GetVolume setting
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");
            
            // Exit setting
            _regimeClosePosition = CreateParameter("Regime Close Position", "Reverse signal", new[] { "Reverse signal", "No signal" });

            // Cointegration settings
            _cointegrationCandlesLookBack = CreateParameter("Cointegration candles look back", 100, 1, 50, 4);
            _cointegrationStandartDeviationMult = CreateParameter("Deviation mult", 1m, 0.1m, 50, 0.1m);

            // Correlation settings
            _correlatioinMinValue = CreateParameter("Correlatioin min value", 0.8m, 0.1m, 1, 0.1m);
            _correlationCandlesLookBack = CreateParameter("Correlation candles look back", 100, 1, 50, 4);

            Description = OsLocalization.Description.DescriptionLabel47;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "MultiOneLegArbitrageMeanReversion";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // logic open position
        private void _index_SpreadChangeEvent(List<Candle> index)
        {
            if(_regime.ValueString == "Off")
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
                if(_screener.PositionsOpenAll.Count >= _maxPositionsCount.ValueInt)
                {
                    return;
                }

                LogicOpenPositions(index, tabsToTrade[i]);
            }
        }
        
        // open position
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

            if(_correlationCandlesLookBack.ValueInt == 0)
            {
                return;
            }

            CorrelationBuilder correlationIndicator = new CorrelationBuilder();

            PairIndicatorValue correlation = 
                correlationIndicator.ReloadCorrelationLast(candlesIndex, candlesSecurity, _correlationCandlesLookBack.ValueInt);

            if(correlation == null ||
                correlation.Value < _correlatioinMinValue.ValueDecimal)
            {
                return;
            }

            CointegrationBuilder cointegrationIndicator = new CointegrationBuilder();
            cointegrationIndicator.CointegrationLookBack = _cointegrationCandlesLookBack.ValueInt;
            cointegrationIndicator.CointegrationDeviation = _cointegrationStandartDeviationMult.ValueDecimal;
            cointegrationIndicator.ReloadCointegration(candlesIndex, candlesSecurity, false);

            if(cointegrationIndicator.Cointegration == null 
                || cointegrationIndicator.Cointegration.Count == 0)
            {
                return;
            }

            if(cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Up
                && _regime.ValueString != "OnlyShort")
            { // need to short security
                BuySecurity(tab, CointegrationLineSide.Up.ToString());
            }

            if (cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Down
                && _regime.ValueString != "OnlyLong")
            { // need to long security
                SellSecurity(tab, CointegrationLineSide.Down.ToString());
            }
        }

        // Logic buy position
        private void BuySecurity(BotTabSimple tab, string signal)
        {
            decimal price = tab.PriceBestAsk;

            if(_slippagePercent.ValueDecimal != 0)
            {
                price = price + price * (_slippagePercent.ValueDecimal / 100);
                price = Math.Round(price, tab.Security.Decimals);
            }

            decimal volume = GetVolume(tab);

            if(price == 0 ||
                volume == 0)
            {
                return;
            }

            tab.BuyAtLimit(volume, price, signal);
        }

        // Logic sell position
        private void SellSecurity(BotTabSimple tab, string signal)
        {
            decimal price = tab.PriceBestBid;

            if (_slippagePercent.ValueDecimal != 0)
            {
                price = price - price * (_slippagePercent.ValueDecimal / 100);
                price = Math.Round(price, tab.Security.Decimals);
            }

            decimal volume = GetVolume(tab);

            if (price == 0 ||
                volume == 0)
            {
                return;
            }

            tab.SellAtLimit(volume, price, signal);
        }

        // logic close position

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

            if (_regime.ValueString == "Off")
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
            cointegrationIndicator.CointegrationLookBack = _cointegrationCandlesLookBack.ValueInt;
            cointegrationIndicator.CointegrationDeviation = _cointegrationStandartDeviationMult.ValueDecimal;
            cointegrationIndicator.ReloadCointegration(candlesIndex, candlesSecurity, false);

            if (cointegrationIndicator.Cointegration == null
                || cointegrationIndicator.Cointegration.Count == 0)
            {
                return;
            }

            if (pos.SignalTypeOpen == "Up")
            {
                //"Reverse signal", "No signal"
                if (_regimeClosePosition.ValueString == "Reverse signal"
                    && cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Down)
                {
                    ClosePosition(pos, tab);
                }
                else if (_regimeClosePosition.ValueString == "No signal"
                    && cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.No)
                {
                    ClosePosition(pos, tab);
                }
            }
            else if(pos.SignalTypeOpen == "Down")
            {
                //"Reverse signal", "No signal"
                if (_regimeClosePosition.ValueString == "Reverse signal"
                    && cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Up)
                {
                    ClosePosition(pos, tab);
                }
                else if (_regimeClosePosition.ValueString == "No signal"
                    && cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.No)
                {
                    ClosePosition(pos, tab);
                }
            }
        }

        // Close position logic
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

                if (_slippagePercent.ValueDecimal != 0)
                {
                    price = price - price * (_slippagePercent.ValueDecimal / 100);
                    price = Math.Round(price, tab.Security.Decimals);
                }
            }
            else if(pos.Direction == Side.Sell)
            {
                price = tab.PriceBestAsk;

                if (_slippagePercent.ValueDecimal != 0)
                {
                    price = price + price * (_slippagePercent.ValueDecimal / 100);
                    price = Math.Round(price, tab.Security.Decimals);
                }
            }

            tab.CloseAtLimit(pos, price, volume);
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
        {
            Portfolio myPortfolio = tab.Portfolio;

            if(myPortfolio == null)
            {
                return 0;
            }

            decimal portfolioPrimeAsset = 0;

            if(_tradeAssetInPortfolio.ValueString == "Prime")
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
                    if (positionOnBoard[i].SecurityNameCode == _tradeAssetInPortfolio.ValueString)
                    {
                        portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                        break;
                    }
                }
            }

            if(portfolioPrimeAsset == 0)
            {
                SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString,Logging.LogMessageType.Error);
                return 0;
            }

            decimal moneyOnPosition = portfolioPrimeAsset * (_moneyPercentFromDepoOnPosition.ValueDecimal / 100);

            decimal qty = moneyOnPosition / tab.PriceBestAsk;

            if (tab.StartProgram == StartProgram.IsOsTrader)
            {
                qty = Math.Round(qty, tab.Security.DecimalsVolume);
            }
            else
            {
                qty = Math.Round(qty, 7);
            }

            return qty;
        }
    }
}