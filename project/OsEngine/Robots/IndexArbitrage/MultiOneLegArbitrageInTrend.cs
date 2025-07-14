/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

/* Description
Index Arbitrage robot for OsEngine.

Securities that deviate from the broad market without momentum are traded on a return to the index.
*/

namespace OsEngine.Robots.IndexArbitrage
{
    [Bot("MultiOneLegArbitrageInTrend")] // We create an attribute so that we don't write anything to the BotFactory
    public class MultiOneLegArbitrageInTrend : BotPanel
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

        // GetVolume settings
        private StrategyParameterString _tradeAssetInPortfolio;

        // Cointegration settings
        private StrategyParameterInt _cointegrationCandlesLookBack;
        private StrategyParameterDecimal _cointegrationStandardDeviationMult;

        // Correlation settings
        private StrategyParameterDecimal _correlationMinValue;
        private StrategyParameterInt _correlationCandlesLookBack;

        // Indicator setting
        private StrategyParameterInt _volatilityStageToTrade;

        // Indicator
        private Aindicator _volatilityStagesOnIndex;

        // Exit settings
        private StrategyParameterDecimal _stopMult;

        public MultiOneLegArbitrageInTrend(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Index);
            _index = TabsIndex[0];
            _index.SpreadChangeEvent += _index_SpreadChangeEvent;

            TabCreate(BotTabType.Screener);
            _screener = TabsScreener[0];
            _screener.CreateCandleIndicator(1, "VolatilityAverage", null, "Area2");
            _screener.CandleFinishedEvent += _screener_CandleFinishedEvent;

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort" });
            _maxPositionsCount = CreateParameter("Max poses count", 3, 1, 50, 4);
            _moneyPercentFromDepoOnPosition = CreateParameter("Percent depo on position", 25m, 0.1m, 50, 0.1m);
            _slippagePercent = CreateParameter("Slippage percent", 0.1m, 0.1m, 5, 0.1m);

            // GetVolume settings
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");
            
            // Cointegration settings
            _cointegrationCandlesLookBack = CreateParameter("Cointegration candles look back", 100, 1, 50, 4);
            _cointegrationStandardDeviationMult = CreateParameter("Deviation mult", 1m, 0.1m, 50, 0.1m);

            // Correlation settings
            _correlationMinValue = CreateParameter("Correlation min value", 0.8m, 0.1m, 1, 0.1m);
            _correlationCandlesLookBack = CreateParameter("Correlation candles look back", 100, 1, 50, 4);
            
            // Indicator setting
            _volatilityStageToTrade = CreateParameter("Volatility Stage To Trade", 2, 1, 5, 1);

            // Exit settings
            _stopMult = CreateParameter("Stop mult", 0.1m, 0.1m, 5, 0.1m);

            // Create indicator Volatility
            _volatilityStagesOnIndex = IndicatorsFactory.CreateIndicatorByName("VolatilityStagesAW", name + "VolatilityStagesAW", false);
            _volatilityStagesOnIndex = (Aindicator)_index.CreateCandleIndicator(_volatilityStagesOnIndex, "VolaStagesArea");
            _volatilityStagesOnIndex.Save();

            Description = OsLocalization.Description.DescriptionLabel47;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "MultiOneLegArbitrageInTrend";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
            
        }

        // logic open position

        private void _index_SpreadChangeEvent(List<Candle> index)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (_screener.Tabs.Count == 0)
            {
                SendNewLogMessage("Screener tab is empty", Logging.LogMessageType.Error);
                return;
            }

            if (_volatilityStagesOnIndex.DataSeries[0].Values.Count == 0)
            {
                return;
            }

            decimal lastVolaStage =
                _volatilityStagesOnIndex.DataSeries[0].Values[_volatilityStagesOnIndex.DataSeries[0].Values.Count - 1];

            if (lastVolaStage != _volatilityStageToTrade.ValueInt)
            {
                return;
            }

            List<BotTabSimple> tabsToTrade = _screener.Tabs;

            for (int i = 0; i < tabsToTrade.Count; i++)
            {
                if (_screener.PositionsOpenAll.Count >= _maxPositionsCount.ValueInt)
                {
                    return;
                }

                LogicOpenPositions(index, tabsToTrade[i]);
            }
        }

        // Logic opening position
        private void LogicOpenPositions(List<Candle> candlesIndex, BotTabSimple tab)
        {
            if (tab.IsConnected == false)
            {
                return;
            }

            if (tab.IsReadyToTrade == false)
            {
                return;
            }

            List<Candle> candlesSecurity = tab.CandlesFinishedOnly;

            if (candlesSecurity == null ||
                candlesSecurity.Count == 0)
            {
                return;
            }

            List<Position> posesOpenBySecurity = tab.PositionsOpenAll;

            if (posesOpenBySecurity.Count > 0)
            {
                return;
            }

            if (_correlationCandlesLookBack.ValueInt == 0)
            {
                return;
            }

            CorrelationBuilder correlationIndicator = new CorrelationBuilder();

            PairIndicatorValue correlation =
                correlationIndicator.ReloadCorrelationLast(candlesIndex, candlesSecurity, _correlationCandlesLookBack.ValueInt);

            if (correlation == null ||
                correlation.Value < _correlationMinValue.ValueDecimal)
            {
                return;
            }

            CointegrationBuilder cointegrationIndicator = new CointegrationBuilder();
            cointegrationIndicator.CointegrationLookBack = _cointegrationCandlesLookBack.ValueInt;
            cointegrationIndicator.CointegrationDeviation = _cointegrationStandardDeviationMult.ValueDecimal;
            cointegrationIndicator.ReloadCointegration(candlesIndex, candlesSecurity, false);

            if (cointegrationIndicator.Cointegration == null
                || cointegrationIndicator.Cointegration.Count == 0)
            {
                return;
            }

            if (cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Up
                 && _regime.ValueString != "OnlyLong")
            { // need to short security
                SellSecurity(tab, CointegrationLineSide.Up.ToString());
            }

            if (cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Down
                 && _regime.ValueString != "OnlyShort")
            { // need to long security
                BuySecurity(tab, CointegrationLineSide.Down.ToString());
            }
        }

        // Buy security
        private void BuySecurity(BotTabSimple tab, string signal)
        {
            decimal price = tab.PriceBestAsk;

            if (_slippagePercent.ValueDecimal != 0)
            {
                price = price + price * (_slippagePercent.ValueDecimal / 100);
                price = Math.Round(price, tab.Security.Decimals);
            }

            decimal volume = GetVolume(tab);

            if (price == 0 ||
                volume == 0)
            {
                return;
            }

            tab.BuyAtLimit(volume, price, signal);
        }

        // Sell security
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

            if (poses.Count == 0)
            {
                return;
            }

            Position pos = poses[0];

            if(pos.State != PositionStateType.Open)
            {
                return;
            }

            // close on trailing stop retreating average intraday volatility multiplied by the multiplier

            Aindicator volaIndicatorOnSecurity = (Aindicator)tab.Indicators[0];

            decimal curVolaInPercent = volaIndicatorOnSecurity.DataSeries[1].Last;

            if(curVolaInPercent <= 0)
            {
                return;
            }

            decimal stopPrice = tab.PriceCenterMarketDepth;

            if(pos.Direction == Side.Buy)
            {
                stopPrice = stopPrice - (stopPrice/100) * (curVolaInPercent * _stopMult.ValueDecimal);
            }
            else if(pos.Direction == Side.Sell)
            {
                stopPrice = stopPrice + (stopPrice / 100) * (curVolaInPercent * _stopMult.ValueDecimal);
            }

            tab.CloseAtTrailingStop(pos, stopPrice, stopPrice);
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
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

            decimal moneyOnPosition = portfolioPrimeAsset * (_moneyPercentFromDepoOnPosition.ValueDecimal / 100) / tab.Security.Lot;

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