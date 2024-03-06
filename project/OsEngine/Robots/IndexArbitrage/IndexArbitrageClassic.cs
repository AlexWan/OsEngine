using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

namespace OsEngine.Robots.IndexArbitrage
{
    [Bot("IndexArbitrageClassic")]
    public class IndexArbitrageClassic : BotPanel
    {
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

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });

            RegimeClosePosition = CreateParameter("Regime Close Position", "", new[] { "Reverse signal", "No signal" });

            MoneyPercentFromDepoOnOneLeg = CreateParameter("Percent depo on one leg", 25m, 0.1m, 50, 0.1m);

            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            CorrelationCandlesLookBack = CreateParameter("Correlation candles look back", 100, 1, 50, 4);

            CointegrationCandlesLookBack = CreateParameter("Cointegration candles look back", 100, 1, 50, 4);

            CointegrationStandartDeviationMult = CreateParameter("Deviation mult", 1m, 0.1m, 50, 0.1m);

            CorrelatioinMinValue = CreateParameter("Correlatioin min value", 0.8m, 0.1m, 1, 0.1m);

            Description =
                    "Classic trading with two index";

        }

        public override string GetNameStrategyType()
        {
            return "IndexArbitrageClassic";
        }

        public override void ShowIndividualSettingsDialog()
        {
            

        }

        private BotTabIndex _indexFirst;

        private BotTabIndex _indexSecond;

        private BotTabScreener _screenerFirst;

        private BotTabScreener _screenerSecond;

        public StrategyParameterString Regime;

        public StrategyParameterString RegimeClosePosition;

        public StrategyParameterDecimal MoneyPercentFromDepoOnOneLeg;

        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterInt CorrelationCandlesLookBack;

        public StrategyParameterInt CointegrationCandlesLookBack;

        public StrategyParameterDecimal CointegrationStandartDeviationMult;

        public StrategyParameterDecimal CorrelatioinMinValue;

        // logic

        List<Candle> _indexCandlesFirst;

        List<Candle> _indexCandlesSecond;

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
            if (Regime.ValueString == "Off")
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

        // logic open poses

        private void TryOpenPositions()
        {
            CorrelationBuilder correlationIndicator = new CorrelationBuilder();

            PairIndicatorValue correlation =
                correlationIndicator.ReloadCorrelationLast(_indexCandlesFirst, _indexCandlesSecond, CorrelationCandlesLookBack.ValueInt);

            if (correlation == null ||
                correlation.Value < CorrelatioinMinValue.ValueDecimal)
            {
                return;
            }

            CointegrationBuilder cointegrationIndicator = new CointegrationBuilder();
            cointegrationIndicator.CointegrationLookBack = CointegrationCandlesLookBack.ValueInt;
            cointegrationIndicator.CointegrationDeviation = CointegrationStandartDeviationMult.ValueDecimal;
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

        private void BuyFirstSellSecond(string curSideCointegration)
        {
            
            decimal firstLegVolumeOneSec = MoneyPercentFromDepoOnOneLeg.ValueDecimal / _screenerFirst.Tabs.Count;
            decimal firstLegVolumeTwoSec = MoneyPercentFromDepoOnOneLeg.ValueDecimal / _screenerSecond.Tabs.Count;

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

        private void BuySecondSellFirst(string curSideCointegration)
        {
            decimal firstLegVolumeOneSec = MoneyPercentFromDepoOnOneLeg.ValueDecimal / _screenerFirst.Tabs.Count;
            decimal firstLegVolumeTwoSec = MoneyPercentFromDepoOnOneLeg.ValueDecimal / _screenerSecond.Tabs.Count;

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

        private decimal GetVolume(BotTabSimple tab, decimal portfolioPercent)
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

            decimal moneyOnPosition = portfolioPrimeAsset * (portfolioPercent / 100);

            decimal qty = moneyOnPosition / tab.PriceBestAsk;

            if(tab.StartProgram == StartProgram.IsOsTrader)
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

            // проверяем чтобы везде были позиции

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
            cointegrationIndicator.CointegrationLookBack = CointegrationCandlesLookBack.ValueInt;
            cointegrationIndicator.CointegrationDeviation = CointegrationStandartDeviationMult.ValueDecimal;
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
                if (RegimeClosePosition.ValueString == "Reverse signal"
                    && cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Down)
                {
                    CloseAllPositionsByMarket();
                }
                else if (RegimeClosePosition.ValueString == "No signal"
                    && cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.No)
                {
                    CloseAllPositionsByMarket();
                }
            }
            else if (lastSide.ToString() == "Down")
            {
                //"Reverse signal", "No signal"
                if (RegimeClosePosition.ValueString == "Reverse signal"
                    && cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Up)
                {
                    CloseAllPositionsByMarket();
                }
                else if (RegimeClosePosition.ValueString == "No signal"
                    && cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.No)
                {
                    CloseAllPositionsByMarket();
                }
            }

        }

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

    }
}