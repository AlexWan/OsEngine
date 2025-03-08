/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Indicators;
using System;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.Attributes;

namespace OsEngine.Robots.Screeners
{
    [Bot("SmaScreener")]
    public class SmaScreener : BotPanel
    {
        public SmaScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _screenerTab = TabsScreener[0];
            _screenerTab.CandleFinishedEvent += _screenerTab_CandleFinishedEvent;
            
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            MaxPoses = CreateParameter("Max poses", 1, 1, 20, 1);
            Slippage = CreateParameter("Slippage", 0, 0, 20, 1);
            VolumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");
            TrailStop = CreateParameter("Trail Stop", 0.7m, 0.5m, 5, 0.1m);
            CandlesLookBack = CreateParameter("Candles Look Back count", 10, 5, 100, 1);
            SmaLength = CreateParameter("Sma length", 100, 5, 300, 1);

            _screenerTab.CreateCandleIndicator(1, 
                "Sma", new List<string>() { SmaLength.ValueInt.ToString(), "Close" }, "Prime");

            Description = "If there is a position, exit by trailing stop. " +
                "If there is no position. Open long if the last N candles " +
                "we were above the moving average";

            ParametrsChangeByUser += SmaScreener_ParametrsChangeByUser;
        }

        private void SmaScreener_ParametrsChangeByUser()
        {
            _screenerTab._indicators[0].Parameters = new List<string>() { SmaLength.ValueInt.ToString(), "Close"};
            _screenerTab.ReloadIndicatorsOnTabs();
        }

        public override string GetNameStrategyType()
        {
            return "SmaScreener";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private BotTabScreener _screenerTab;

        public StrategyParameterString Regime;

        public StrategyParameterInt CandlesLookBack;

        public StrategyParameterInt MaxPoses;

        public StrategyParameterInt Slippage;

        public StrategyParameterString VolumeType;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterDecimal TrailStop;

        public StrategyParameterInt SmaLength;

        private void _screenerTab_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            // 1 Если поза есть, то по трейлинг стопу закрываем

            // 2 Позы нет. Открывать лонг, если последние N свечей мы были над скользящей средней
            
            if(Regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 10)
            {
                return;
            }

            if(candles.Count - 1 - CandlesLookBack.ValueInt - 1 <= 0)
            {
                return;
            }

            List<Position> positions = tab.PositionsOpenAll;

            if(positions.Count == 0)
            { // логика открытия

                int allPosesInAllTabs = this.PositionsCount;

                if (allPosesInAllTabs >= MaxPoses.ValueInt)
                {
                    return;
                }

                Aindicator sma = (Aindicator)tab.Indicators[0];

                for(int i = candles.Count-1; i >= 0 && i > candles.Count -1 - CandlesLookBack.ValueInt;i--)
                {
                    decimal curSma = sma.DataSeries[0].Values[i];

                    if(curSma == 0)
                    {
                        return;
                    }

                    if (candles[i].Close < curSma)
                    {
                        return;
                    }
                }

                if(candles[candles.Count - 1 - CandlesLookBack.ValueInt - 1].Close > sma.DataSeries[0].Values[candles.Count - 1 - CandlesLookBack.ValueInt - 1])
                {
                    return;
                }

                tab.BuyAtLimit(GetVolume(tab), tab.PriceBestAsk + tab.Security.PriceStep * Slippage.ValueInt);
            }
            else
            {
                Position pos = positions[0];

                if(pos.State != PositionStateType.Open)
                {
                    return;
                }

                decimal close = candles[candles.Count - 1].Low;
                decimal priceActivation = close - close * TrailStop.ValueDecimal/100;
                decimal priceOrder = priceActivation - tab.Security.PriceStep * Slippage.ValueInt;

                tab.CloseAtTrailingStop(pos, priceActivation, priceOrder);
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
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = Volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
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

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

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

            return volume;
        }
    }
}