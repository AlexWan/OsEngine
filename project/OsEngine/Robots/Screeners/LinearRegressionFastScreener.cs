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
    [Bot("LinearRegressionFastScreener")]
    public class LinearRegressionFastScreener : BotPanel
    {
        public LinearRegressionFastScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _screenerTab = TabsScreener[0];
            _screenerTab.CandleFinishedEvent += _screenerTab_CandleFinishedEvent;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            MaxPoses = CreateParameter("Max poses", 5, 1, 20, 1);
            IcebergOrdersCount = CreateParameter("Iceberg orders count", 1, 1, 20, 1);
            VolumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            TimeStart = CreateParameterTimeOfDay("Start Trade Time", 10, 32, 0, 0);
            TimeEnd = CreateParameterTimeOfDay("End Trade Time", 18, 25, 0, 0);

            AdxFilterIsOn = CreateParameter("ADX filter is on", true);
            AdxFilterLength = CreateParameter("ADX filter Len", 30, 10, 100, 3);
            MinAdxValue = CreateParameter("ADX min value", 10, 20, 90, 1m);
            MaxAdxValue = CreateParameter("ADX max value", 40, 20, 90, 1m);
            _screenerTab.CreateCandleIndicator(1,
                "ADX", new List<string>() { AdxFilterLength.ValueInt.ToString()}, "Second");

            LrLength = CreateParameter("Linear regression Length", 50, 20, 300, 10);
            LrDeviation = CreateParameter("Linear regression deviation", 2, 1, 4, 0.1m);

            _screenerTab.CreateCandleIndicator(2,
            "LinearRegressionChannelFast_Indicator", 
             new List<string>() 
             { 
                 LrLength.ValueInt.ToString(), 
                 "Close",
                 LrDeviation.ValueDecimal.ToString(),
                 LrDeviation.ValueDecimal.ToString()
             }, 
             "Prime");

            SmaFilterIsOn = CreateParameter("Sma filter is on", true);
            SmaFilterLen = CreateParameter("Sma filter Len", 100, 100, 300, 10);

            _screenerTab.CreateCandleIndicator(3,
                    "Sma", new List<string>() { SmaFilterLen.ValueInt.ToString(), "Close" }, "Prime");

            ParametrsChangeByUser += SmaScreener_ParametrsChangeByUser;
        }

        private void SmaScreener_ParametrsChangeByUser()
        {
            _screenerTab._indicators[0].Parameters 
                = new List<string>() { AdxFilterLength.ValueInt.ToString()};

            _screenerTab._indicators[1].Parameters
              = new List<string>()
             {
                 LrLength.ValueInt.ToString(),
                 "Close",
                 LrDeviation.ValueDecimal.ToString(),
                 LrDeviation.ValueDecimal.ToString()
             };

            _screenerTab._indicators[2].Parameters 
                = new List<string>() { SmaFilterLen.ValueInt.ToString(), "Close" };

            _screenerTab.UpdateIndicatorsParameters();
        }

        public override string GetNameStrategyType()
        {
            return "LinearRegressionFastScreener";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private BotTabScreener _screenerTab;

        public StrategyParameterString Regime;

        public StrategyParameterInt MaxPoses;

        public StrategyParameterInt IcebergOrdersCount;

        public StrategyParameterString VolumeType;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterBool AdxFilterIsOn;

        public StrategyParameterInt AdxFilterLength;

        public StrategyParameterDecimal MinAdxValue;

        public StrategyParameterDecimal MaxAdxValue;

        public StrategyParameterInt LrLength;

        public StrategyParameterDecimal LrDeviation;

        public StrategyParameterBool SmaFilterIsOn;

        public StrategyParameterInt SmaFilterLen;

        private StrategyParameterTimeOfDay TimeStart;

        private StrategyParameterTimeOfDay TimeEnd;

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

            if (TimeStart.Value > tab.TimeServerCurrent ||
                TimeEnd.Value < tab.TimeServerCurrent)
            {
                return;
            }

            List<Position> positions = tab.PositionsOpenAll;

            if (positions.Count == 0)
            { // логика открытия

                int allPosesInAllTabs = _screenerTab.PositionsOpenAll.Count;

                if (allPosesInAllTabs >= MaxPoses.ValueInt)
                {
                    return;
                }

                if(AdxFilterIsOn.ValueBool == true)
                {
                    Aindicator adx = (Aindicator)tab.Indicators[0];

                    decimal adxLast = adx.DataSeries[0].Last;

                    if(adxLast == 0)
                    {
                        return;
                    }

                    if (adxLast < MinAdxValue.ValueDecimal
                        || adxLast > MaxAdxValue.ValueDecimal)
                    {// Adx filter
                        return;
                    }
                }

                decimal candleClose = candles[candles.Count - 1].Close;

                if (SmaFilterIsOn.ValueBool == true)
                {
                    Aindicator sma = (Aindicator)tab.Indicators[2];

                    decimal lastSma = sma.DataSeries[0].Last;

                    if(candleClose < lastSma)
                    {   
                        return;
                    }
                }

                Aindicator lrIndicator = (Aindicator)tab.Indicators[1];

                decimal lrUp = lrIndicator.DataSeries[0].Values[lrIndicator.DataSeries[0].Values.Count - 1];

                if (lrUp == 0)
                {
                    return;
                }

                if (candleClose > lrUp)
                {
                    tab.BuyAtIcebergMarket(GetVolume(tab), IcebergOrdersCount.ValueInt, 2000);
                }
            }
            else
            {// логика закрытия
                Position pos = positions[0];

                if (pos.State != PositionStateType.Open)
                {
                    return;
                }

                Aindicator lrIndicator = (Aindicator)tab.Indicators[1];

                //decimal pcUp = priceChannel.DataSeries[0].Last;
                decimal lrDown = lrIndicator.DataSeries[2].Last;

                if (lrDown == 0)
                {
                    return;
                }

                decimal lastCandleClose = candles[candles.Count - 1].Close;

                if (lastCandleClose < lrDown)
                {
                    tab.CloseAtIcebergMarket(pos, pos.OpenVolume, IcebergOrdersCount.ValueInt, 2000);
                }
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