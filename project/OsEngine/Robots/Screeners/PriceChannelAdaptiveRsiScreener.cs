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
    [Bot("PriceChannelAdaptiveRsiScreener")]
    public class PriceChannelAdaptiveRsiScreener : BotPanel
    {
        public PriceChannelAdaptiveRsiScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _screenerTab = TabsScreener[0];
            _screenerTab.CandleFinishedEvent += _screenerTab_CandleFinishedEvent;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            MaxPoses = CreateParameter("Max poses", 1, 1, 20, 1);
            MinRsiValueToEntry = CreateParameter("Min Rsi value to entry", 80, 1.0m, 95, 4);

            VolumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");
            RsiLength = CreateParameter("Rsi length", 100, 5, 300, 1);
            PcAdxLength = CreateParameter("Pc adx length", 10, 5, 300, 1);
            PcRatio = CreateParameter("Pc ratio", 80, 5, 300, 1);

            SmaFilterIsOn = CreateParameter("Sma filter is on", true);

            SmaFilterLen = CreateParameter("Sma filter Len", 150, 100, 300, 10);

            _screenerTab.CreateCandleIndicator(1,
                "RSI",
                new List<string>() { RsiLength.ValueInt.ToString() },
                "Second");

            _screenerTab.CreateCandleIndicator(2,
                "PriceChannelAdaptive",
                new List<string>() { PcAdxLength.ValueInt.ToString(), PcRatio.ValueInt.ToString() },
                "Prime");

            ParametrsChangeByUser += SmaScreener_ParametrsChangeByUser;
        }

        private void SmaScreener_ParametrsChangeByUser()
        {
            _screenerTab._indicators[0].Parameters = new List<string>() { RsiLength.ValueInt.ToString() };
            _screenerTab._indicators[1].Parameters = new List<string>() { PcAdxLength.ValueInt.ToString(), PcRatio.ValueInt.ToString() };

            _screenerTab.UpdateIndicatorsParameters();
        }

        public override string GetNameStrategyType()
        {
            return "PriceChannelAdaptiveRsiScreener";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private BotTabScreener _screenerTab;

        public StrategyParameterString Regime;

        public StrategyParameterInt MaxPoses;

        public StrategyParameterDecimal MinRsiValueToEntry;

        public StrategyParameterString VolumeType;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterInt RsiLength;

        public StrategyParameterInt PcAdxLength;

        public StrategyParameterInt PcRatio;

        public StrategyParameterBool SmaFilterIsOn;

        public StrategyParameterInt SmaFilterLen;

        private void _screenerTab_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            // 1 Если поза есть, то по трейлинг стопу закрываем

            // 2 Позы нет. Открывать лонг, если последние N свечей мы были над скользящей средней

            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 10)
            {
                return;
            }

            List<Position> positions = tab.PositionsOpenAll;

            if (positions.Count == 0)
            { // open position logic

                int allPosesInAllTabs = this.PositionsCount;

                if (allPosesInAllTabs >= MaxPoses.ValueInt)
                {
                    return;
                }

                Aindicator rsi = (Aindicator)tab.Indicators[0];

                if (rsi.DataSeries[0].Last < MinRsiValueToEntry.ValueDecimal)
                {// Rsi filter
                    return;
                }

                Aindicator priceChannel = (Aindicator)tab.Indicators[1];

                decimal pcUp = priceChannel.DataSeries[0].Values[priceChannel.DataSeries[0].Values.Count - 2];
                //decimal pcDown = priceChannel.DataSeries[1].Values[priceChannel.DataSeries[1].Values.Count-2];

                if (pcUp == 0)
                {
                    return;
                }

                decimal candleClose = candles[candles.Count - 1].Close;

                if (candleClose > pcUp)
                {

                    if (SmaFilterIsOn.ValueBool == true)
                    {
                        decimal smaValue = Sma(candles, SmaFilterLen.ValueInt, candles.Count - 1);
                        decimal smaPrev = Sma(candles, SmaFilterLen.ValueInt, candles.Count - 2);

                        if (smaValue < smaPrev)
                        {
                            return;
                        }
                    }

                    tab.BuyAtMarket(GetVolume(tab));
                }
            }
            else
            {
                Position pos = positions[0];

                if (pos.State != PositionStateType.Open)
                {
                    return;
                }

                Aindicator priceChannel = (Aindicator)tab.Indicators[1];

                //decimal pcUp = priceChannel.DataSeries[0].Last;
                decimal pcDown = priceChannel.DataSeries[1].Last;

                if (pcDown == 0)
                {
                    return;
                }

                tab.CloseAtTrailingStopMarket(pos, pcDown);
            }
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