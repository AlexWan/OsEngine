/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

/* Description
Index Arbitrage robot for OsEngine.

Classic trading with two index.
*/

namespace OsEngine.Robots.IndexArbitrage
{
    [Bot("IndexArbitrageClassic")] // We create an attribute so that we don't write anything to the BotFactory
    public class IndexArbitrageClassic : BotPanel
    {
        // Index tabs
        private BotTabIndex _indexFirst;
        private BotTabIndex _indexSecond;

        // Screener tabs
        private BotTabScreener _screenerFirst;
        private BotTabScreener _screenerSecond;

        // Basic setting
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _moneyPercentFromDepoOnOneLeg;

        // Exit regime
        private StrategyParameterString _regimeClosePosition;

        // GetVolume setting
        private StrategyParameterString _tradeAssetInPortfolio;

        // Cointegration settings
        private StrategyParameterInt _cointegrationCandlesLookBack;
        private StrategyParameterDecimal _cointegrationStandartDeviationMult;

        // Correlation settings
        private StrategyParameterDecimal _correlatioinMinValue;
        private StrategyParameterInt _correlationCandlesLookBack;

        // logic

        List<Candle> _indexCandlesFirst;

        List<Candle> _indexCandlesSecond;

        public IndexArbitrageClassic(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Index);
            _indexFirst = TabsIndex[0];
            _indexFirst.SpreadChangeEvent += _indexFirst_SpreadChangeEvent;
           
            TabCreate(BotTabType.Index);
            _indexSecond = TabsIndex[1];
            _indexSecond.SpreadChangeEvent += _indexSecond_SpreadChangeEvent;

            TabCreate(BotTabType.Screener);
            _screenerFirst = TabsScreener[0];

            TabCreate(BotTabType.Screener);
            _screenerSecond = TabsScreener[1];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _moneyPercentFromDepoOnOneLeg = CreateParameter("Percent depo on one leg", 25m, 0.1m, 50, 0.1m);

            // GetVolume setting
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Cointegration settings
            _cointegrationCandlesLookBack = CreateParameter("Cointegration candles look back", 100, 1, 50, 4);
            _cointegrationStandartDeviationMult = CreateParameter("Deviation mult", 1m, 0.1m, 50, 0.1m);

            // Correlation settings
            _correlatioinMinValue = CreateParameter("Correlatioin min value", 0.8m, 0.1m, 1, 0.1m);
            _correlationCandlesLookBack = CreateParameter("Correlation candles look back", 100, 1, 50, 4);
            
            // Exit setting
            _regimeClosePosition = CreateParameter("Regime Close Position", "", new[] { "Reverse signal", "No signal" });

            Description = OsLocalization.Description.DescriptionLabel45;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IndexArbitrageClassic";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
            
        }

        private void _indexFirst_SpreadChangeEvent(List<Candle> indexFirst)
        {
            _indexCandlesFirst = indexFirst;

            if (_indexCandlesSecond != null &&
                _indexCandlesFirst[_indexCandlesFirst.Count - 1].TimeStart ==
                _indexCandlesSecond[_indexCandlesSecond.Count - 1].TimeStart)
            {
                Trade();
            }
        }

        private void _indexSecond_SpreadChangeEvent(List<Candle> indexSecond)
        {
            _indexCandlesSecond = indexSecond;

            if (_indexCandlesFirst != null &&
                _indexCandlesFirst[_indexCandlesFirst.Count - 1].TimeStart ==
                _indexCandlesSecond[_indexCandlesSecond.Count - 1].TimeStart)
            {
                Trade();
            }
        }

        private bool _lastCandleOpenPos;

        private void Trade()
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (_screenerFirst.Tabs.Count == 0)
            {
                SendNewLogMessage("ScreenerFirst is note activated", Logging.LogMessageType.Error);
                return;
            }

            if (_screenerSecond.Tabs.Count == 0)
            {
                SendNewLogMessage("ScreenerSecond is note activated", Logging.LogMessageType.Error);
                return;
            }

            bool havePos = HavePositions();

            if (havePos == false)
            {
                TryOpenPositions();
                _lastCandleOpenPos = true;
            }
            else if (havePos == true)
            {
                if(_lastCandleOpenPos == true)
                {
                    _lastCandleOpenPos = false;
                    return;
                }

                TryClosePositions();
            }
        }

        // Checking for position availability
        private bool HavePositions()
        {
            for (int i = 0; i < _screenerFirst.Tabs.Count; i++)
            {
                if (_screenerFirst.Tabs[i].PositionsOpenAll.Count != 0)
                {
                    return true;
                }
            }

            for (int i = 0; i < _screenerSecond.Tabs.Count; i++)
            {
                if (_screenerSecond.Tabs[i].PositionsOpenAll.Count != 0)
                {
                    return true;
                }
            }

            return false;
        }

        // logic open position
        private void TryOpenPositions()
        {
            CorrelationBuilder correlationIndicator = new CorrelationBuilder();

            PairIndicatorValue correlation = 
                correlationIndicator.ReloadCorrelationLast(_indexCandlesFirst, _indexCandlesSecond, _correlationCandlesLookBack.ValueInt);

            if (correlation == null ||
                correlation.Value < _correlatioinMinValue.ValueDecimal)
            {
                return;
            }

            CointegrationBuilder cointegrationIndicator = new CointegrationBuilder();
            cointegrationIndicator.CointegrationLookBack = _cointegrationCandlesLookBack.ValueInt;
            cointegrationIndicator.CointegrationDeviation = _cointegrationStandartDeviationMult.ValueDecimal;
            cointegrationIndicator.ReloadCointegration(_indexCandlesFirst, _indexCandlesSecond, false);

            if (cointegrationIndicator.Cointegration == null
                || cointegrationIndicator.Cointegration.Count == 0)
            {
                return;
            }

            if (cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Up)
            { // long first short second 
                BuyFirstSellSecond(CointegrationLineSide.Up.ToString());
            }

            if (cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Down)
            { // long second short first
                BuySecondSellFirst(CointegrationLineSide.Down.ToString());
            }
        }

        // Buy first and sell second logic
        private void BuyFirstSellSecond(string curSideCointegration)
        {
            decimal firstLegVolumeOneSec = _moneyPercentFromDepoOnOneLeg.ValueDecimal / _screenerFirst.Tabs.Count;
            decimal firstLegVolumeTwoSec = _moneyPercentFromDepoOnOneLeg.ValueDecimal / _screenerSecond.Tabs.Count;

            for (int i = 0;i < _screenerFirst.Tabs.Count;i++)
            {
                decimal volume = GetVolume(_screenerFirst.Tabs[i], firstLegVolumeOneSec);
                _screenerFirst.Tabs[i].BuyAtMarket(volume, curSideCointegration);
            }

            for (int i = 0; i < _screenerSecond.Tabs.Count; i++)
            {
                decimal volume = GetVolume(_screenerSecond.Tabs[i], firstLegVolumeTwoSec);
                _screenerSecond.Tabs[i].SellAtMarket(volume, curSideCointegration);
            }
        }

        // Buy second and sell first logic
        private void BuySecondSellFirst(string curSideCointegration)
        {
            decimal firstLegVolumeOneSec = _moneyPercentFromDepoOnOneLeg.ValueDecimal / _screenerFirst.Tabs.Count;
            decimal firstLegVolumeTwoSec = _moneyPercentFromDepoOnOneLeg.ValueDecimal / _screenerSecond.Tabs.Count;

            for (int i = 0; i < _screenerFirst.Tabs.Count; i++)
            {
                decimal volume = GetVolume(_screenerFirst.Tabs[i], firstLegVolumeOneSec);
                _screenerFirst.Tabs[i].SellAtMarket(volume, curSideCointegration);
            }

            for (int i = 0; i < _screenerSecond.Tabs.Count; i++)
            {
                decimal volume = GetVolume(_screenerSecond.Tabs[i], firstLegVolumeTwoSec);
                _screenerSecond.Tabs[i].BuyAtMarket(volume, curSideCointegration);
            }
        }

        // logic close position
        private void TryClosePositions()
        {
            if(_screenerFirst.Tabs.Count == 0 ||
                _screenerSecond.Tabs.Count == 0)
            {
                return;
            }

            for(int i = 0;i < _screenerFirst.Tabs.Count;i++)
            {
                if (_screenerFirst.Tabs[i].IsConnected == false ||
                    _screenerFirst.Tabs[i].IsReadyToTrade == false)
                {
                    return;
                }
            }

            for (int i = 0; i < _screenerSecond.Tabs.Count; i++)
            {
                if (_screenerSecond.Tabs[i].IsConnected == false ||
                    _screenerSecond.Tabs[i].IsReadyToTrade == false)
                {
                    return;
                }
            }

            // we check that there are positions everywhere

            for (int i = 0; i < _screenerFirst.Tabs.Count; i++)
            {
                if (_screenerFirst.Tabs[i].PositionsOpenAll.Count == 0)
                {
                    SendNewLogMessage("In one of securities no positions!", Logging.LogMessageType.Error);
                    CloseAllPositionsByMarket();
                    return;
                }
            }

            for (int i = 0; i < _screenerSecond.Tabs.Count; i++)
            {
                if (_screenerSecond.Tabs[i].PositionsOpenAll.Count == 0)
                {
                    SendNewLogMessage("In one of securities no positions!", Logging.LogMessageType.Error);
                    CloseAllPositionsByMarket();
                    return;
                }
            }

            CointegrationBuilder cointegrationIndicator = new CointegrationBuilder();
            cointegrationIndicator.CointegrationLookBack = _cointegrationCandlesLookBack.ValueInt;
            cointegrationIndicator.CointegrationDeviation = _cointegrationStandartDeviationMult.ValueDecimal;
            cointegrationIndicator.ReloadCointegration(_indexCandlesFirst, _indexCandlesSecond, false);

            if (cointegrationIndicator.Cointegration == null
                || cointegrationIndicator.Cointegration.Count == 0)
            {
                return;
            }

            // RegimeClosePosition

            CointegrationLineSide lastSide;
            
            if(Enum.TryParse(_screenerFirst.Tabs[0].PositionsOpenAll[0].SignalTypeOpen,out lastSide) == false)
            {
                SendNewLogMessage("Last side is unknown! Error", Logging.LogMessageType.Error);
                return;
            }

            if (lastSide.ToString() == "Up")
            {
                if (_regimeClosePosition.ValueString == "Reverse signal"
                    && cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Down)
                {
                    CloseAllPositionsByMarket();
                }
                else if (_regimeClosePosition.ValueString == "No signal"
                    && cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.No)
                {
                    CloseAllPositionsByMarket();
                }
            }
            else if (lastSide.ToString() == "Down")
            {
                //"Reverse signal", "No signal"
                if (_regimeClosePosition.ValueString == "Reverse signal"
                    && cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Up)
                {
                    CloseAllPositionsByMarket();
                }
                else if (_regimeClosePosition.ValueString == "No signal"
                    && cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.No)
                {
                    CloseAllPositionsByMarket();
                }
            }
        }

        // Close all position
        private void CloseAllPositionsByMarket()
        {
            for (int i = 0; i < _screenerFirst.Tabs.Count; i++)
            {
                if (_screenerFirst.Tabs[i].PositionsOpenAll.Count != 0)
                {
                    Position pos = _screenerFirst.Tabs[i].PositionsOpenAll[0];

                    if(pos.State == PositionStateType.Open)
                    {
                        _screenerFirst.Tabs[i].CloseAtMarket(pos, pos.OpenVolume);
                    }
                }
            }

            for (int i = 0; i < _screenerSecond.Tabs.Count; i++)
            {
                if (_screenerSecond.Tabs[i].PositionsOpenAll.Count != 0)
                {
                    Position pos = _screenerSecond.Tabs[i].PositionsOpenAll[0];

                    if (pos.State == PositionStateType.Open)
                    {
                        _screenerSecond.Tabs[i].CloseAtMarket(pos, pos.OpenVolume);
                    }
                }
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab, decimal portfolioPercent)
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

            decimal moneyOnPosition = portfolioPrimeAsset * (portfolioPercent / 100);

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