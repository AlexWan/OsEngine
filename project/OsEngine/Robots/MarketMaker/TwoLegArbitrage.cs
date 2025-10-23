/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

/* Description 
Pair trading based on index analysis.

Buy: when the RSI indicator built on the index goes below the oversold level.

Sell: when the RSI indicator built on the index goes above the oversold level.

Exit: by reverse system.
*/

namespace OsEngine.Robots.MarketMaker
{
    [Bot("TwoLegArbitrage")] // We create an attribute so that we don't write anything to the BotFactory
    public class TwoLegArbitrage : BotPanel
    {
        private BotTabIndex _tabIndex;
        private BotTabSimple _tab1;
        private BotTabSimple _tab2;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _slippage;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator area line settings
        private StrategyParameterInt _upline;
        private StrategyParameterInt _downline;

        // Indicator setting
        private StrategyParameterInt _rsiLength;
        
        // Indicator
        private Aindicator _rsi;

        // The last value of the indicator
        private decimal _lastRsi;

        public TwoLegArbitrage(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Index);
            _tabIndex = TabsIndex[0];

            TabCreate(BotTabType.Simple);
            _tab1 = TabsSimple[0];

            TabCreate(BotTabType.Simple);
            _tab2 = TabsSimple[1];

            // Subscribe to the candle finished event
            _tab1.CandleFinishedEvent += Tab1_CandleFinishedEvent;
            _tab2.CandleFinishedEvent += Tab2_CandleFinishedEvent;
            _tabIndex.SpreadChangeEvent += TabIndex_CandleFinishedEvent;

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            _slippage = CreateParameter("Slippage", 0, 0, 20, 1);

            // Indicator area line settings
            _upline = CreateParameter("Upline", 10, 50, 80, 3);
            _downline = CreateParameter("Downline", 10, 25, 50, 2);

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator setting
            _rsiLength = CreateParameter("RsiLength", 10, 5, 150, 2);

            // Create indicator Rsi
            _rsi = IndicatorsFactory.CreateIndicatorByName("RSI", name + "RSI", false);
            _rsi = (Aindicator)_tabIndex.CreateCandleIndicator(_rsi, "RsiArea");
            ((IndicatorParameterInt)_rsi.Parameters[0]).ValueInt = _rsiLength.ValueInt;
            _rsi.DataSeries[0].Color = Color.Gold;
            _rsi.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += TwoLegArbitrage_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel51;
        }

        // User change params
        void TwoLegArbitrage_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_rsi.Parameters[0]).ValueInt = _rsiLength.ValueInt;
            _rsi.Save();
            _rsi.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "TwoLegArbitrage";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        #region sync tabs

        private void TabIndex_CandleFinishedEvent(List<Candle> candlesIndex)
        {
            if (_tab1.CandlesFinishedOnly == null ||
                _tab2.CandlesFinishedOnly == null ||
                _tabIndex.Candles == null)
            {
                return;
            }

            if (_tab1.CandlesFinishedOnly.Count == 0
                || _tab2.CandlesFinishedOnly.Count == 0
                || _tabIndex.Candles.Count == 0)
            {
                return;
            }

            List<Candle> candlesTab1 = _tab1.CandlesFinishedOnly;
            List<Candle> candlesTab2 = _tab2.CandlesFinishedOnly;

            if (candlesIndex.Last().TimeStart == candlesTab1.Last().TimeStart
                && candlesIndex.Last().TimeStart == candlesTab2.Last().TimeStart)
            {
                TradeLogic(candlesTab1, candlesTab2, candlesIndex);
            }
        }

        private void Tab1_CandleFinishedEvent(List<Candle> candlesTab1)
        {
            if(_tab1.CandlesFinishedOnly == null ||
                _tab2.CandlesFinishedOnly == null ||
                _tabIndex.Candles == null)
            {
                return;
            }

            if (_tab1.CandlesFinishedOnly.Count == 0
                || _tab2.CandlesFinishedOnly.Count == 0
                || _tabIndex.Candles.Count == 0)
            {
                return;
            }

            List<Candle> candlesIndex = _tabIndex.Candles;
            List<Candle> candlesTab2 = _tab2.CandlesFinishedOnly;

            if (candlesIndex.Last().TimeStart == candlesTab1.Last().TimeStart
                && candlesIndex.Last().TimeStart == candlesTab2.Last().TimeStart)
            {
                TradeLogic(candlesTab1, candlesTab2, candlesIndex);
            }
        }

        private void Tab2_CandleFinishedEvent(List<Candle> candlesTab2)
        {
            if (_tab1.CandlesFinishedOnly == null ||
                _tab2.CandlesFinishedOnly == null ||
                _tabIndex.Candles == null)
            {
                return;
            }
            if (_tab1.CandlesFinishedOnly.Count == 0
                || _tab2.CandlesFinishedOnly.Count == 0
                || _tabIndex.Candles.Count == 0)
            {
                return;
            }

            List<Candle> candlesIndex = _tabIndex.Candles;
            List<Candle> candlesTab1 = _tab1.CandlesFinishedOnly;

            if (candlesIndex.Last().TimeStart == candlesTab1.Last().TimeStart
                && candlesIndex.Last().TimeStart == candlesTab2.Last().TimeStart)
            {
                TradeLogic(candlesTab1, candlesTab2, candlesIndex);
            }
        }

        #endregion

        // Trade logic
        private void TradeLogic(List<Candle> candlesTab1, List<Candle> candlesTab2, List<Candle> candlesIndex)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (_rsi.DataSeries[0].Values == null)
            {
                return;
            }

            _lastRsi = _rsi.DataSeries[0].Last;

            for (int j = 0; TabsSimple.Count != 0 && j < TabsSimple.Count; j++)
            {
                List<Position> openPositions = TabsSimple[j].PositionsOpenAll;

                if (openPositions != null && openPositions.Count != 0)
                {
                    for (int i = 0; i < openPositions.Count; i++)
                    {
                        LogicClosePosition(openPositions[i], TabsSimple[j], _lastRsi);
                    }
                }

                if (_regime.ValueString == "OnlyClosePosition")
                {
                    return;
                }

                if (openPositions == null || openPositions.Count == 0)
                {
                    LogicOpenPosition(TabsSimple[j], _lastRsi);
                }
            }
        }

        // Logic opening first position
        private void LogicOpenPosition(BotTabSimple tab, decimal lastRsi)
        {
            if (lastRsi > _upline.ValueInt && _regime.ValueString != "OnlyLong")
            {
                tab.SellAtLimit(GetVolume(tab), tab.PriceBestBid - _slippage.ValueInt * tab.Security.PriceStep);
            }

            if (lastRsi < _downline.ValueInt && _regime.ValueString != "OnlyShort")
            {
                tab.BuyAtLimit(GetVolume(tab), tab.PriceBestAsk + _slippage.ValueInt * tab.Security.PriceStep);
            }
        }

        // Logic close position
        private void LogicClosePosition(Position position, BotTabSimple tab, decimal lastRsi)
        {
            if (position.Direction == Side.Buy)
            {
                if (lastRsi > _upline.ValueInt)
                {
                    tab.CloseAtLimit(position, tab.PriceBestBid - _slippage.ValueInt * tab.Security.PriceStep, position.OpenVolume);
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (lastRsi < _downline.ValueInt)
                {
                    tab.CloseAtLimit(position, tab.PriceBestAsk + _slippage.ValueInt * tab.Security.PriceStep, position.OpenVolume);
                }
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (_volumeType.ValueString == "Contracts")
            {
                volume = _volume.ValueDecimal;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = _volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                        tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = _volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (_volumeType.ValueString == "Deposit percent")
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

                decimal moneyOnPosition = portfolioPrimeAsset * (_volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    if (tab.Security.UsePriceStepCostToCalculateVolume == true
                     && tab.Security.PriceStep != tab.Security.PriceStepCost
                     && tab.PriceBestAsk != 0
                     && tab.Security.PriceStep != 0
                     && tab.Security.PriceStepCost != 0)
                    {// расчёт количества контрактов для фьючерсов и опционов на Мосбирже
                        qty = moneyOnPosition / (tab.PriceBestAsk / tab.Security.PriceStep * tab.Security.PriceStepCost);
                    }
                    qty = Math.Round(qty, tab.Security.DecimalsVolume);
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