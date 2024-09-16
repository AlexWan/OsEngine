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
using OsEngine.Indicators;
using System.Reflection;
using OsEngine.Market.Servers.Tester;

namespace OsEngine.Robots.VolatilityStageRotationSamples
{
    [Bot("PriceChannelScreenerOnIndexVolatility")]
    public class PriceChannelScreenerOnIndexVolatility : BotPanel
    {
        BotTabScreener _tabScreener;
        BotTabIndex _tabIndex;

        private Aindicator _volatilityStagesOnIndex;

        public StrategyParameterString Regime;
        public StrategyParameterInt MaxPositions;
        public StrategyParameterInt PriceChannelLength;
        private StrategyParameterTimeOfDay TimeStart;
        private StrategyParameterTimeOfDay TimeEnd;

        public StrategyParameterBool AtrFilterIsOn;
        public StrategyParameterInt AtrLength;
        public StrategyParameterDecimal AtrGrowPercent;
        public StrategyParameterInt AtrGrowLookBack;

        public StrategyParameterBool VolatilityFilterIsOn;

        public StrategyParameterBool VolatilityStageOneIsOn;
        public StrategyParameterBool VolatilityStageTwoIsOn;
        public StrategyParameterBool VolatilityStageThreeIsOn;

        public StrategyParameterInt VolatilitySlowSmaLength;
        public StrategyParameterInt VolatilityFastSmaLength;
        public StrategyParameterDecimal VolatilityChannelDeviation;

        public StrategyParameterString VolumeType;
        public StrategyParameterDecimal Volume;
        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterInt TopVolumeSecurities;
        public StrategyParameterInt TopCandlesLookBack;
        public StrategyParameterDecimal MaxVolatilityDifference;
        public StrategyParameterDecimal MinVolatilityDifference;
        public StrategyParameterString SecuritiesToTrade;
        public StrategyParameterBool MessageOnRebuild;

        public PriceChannelScreenerOnIndexVolatility(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Index);
            _tabIndex = TabsIndex[0];
            _tabIndex.SpreadChangeEvent += _tabIndex_SpreadChangeEvent;

            TabCreate(BotTabType.Screener);
            _tabScreener = TabsScreener[0];
            _tabScreener.CandleFinishedEvent += _screenerTab_CandleFinishedEvent;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            MaxPositions = CreateParameter("Max positions", 5, 0, 20, 1);
            TimeStart = CreateParameterTimeOfDay("Start Trade Time", 10, 32, 0, 0);
            TimeEnd = CreateParameterTimeOfDay("End Trade Time", 18, 25, 0, 0);
            VolumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            PriceChannelLength = CreateParameter("Price channel length", 50, 10, 80, 3);
            AtrLength = CreateParameter("Atr length", 25, 10, 80, 3);
            AtrFilterIsOn = CreateParameter("Atr filter is on", false);
            AtrGrowPercent = CreateParameter("Atr grow percent", 3, 1.0m, 50, 4);
            AtrGrowLookBack = CreateParameter("Atr grow look back", 20, 1, 50, 4);

            TopVolumeSecurities = CreateParameter("Top volume securities", 15, 0, 20, 1, "Volatility stage");
            TopCandlesLookBack = CreateParameter("Top days look back", 5, 0, 20, 1, "Volatility stage");
            MaxVolatilityDifference = CreateParameter("Vol Diff Max", 1.4m, 1.0m, 50, 4, "Volatility stage");
            MinVolatilityDifference = CreateParameter("Vol Diff Min", 1.0m, 1.0m, 50, 4, "Volatility stage");
            SecuritiesToTrade = CreateParameter("Securities to trade", "", "Volatility stage");
            MessageOnRebuild = CreateParameter("Message on rebuild", true, "Volatility stage");
            StrategyParameterButton button = CreateParameterButton("Check securities rating", "Volatility stage");
            button.UserClickOnButtonEvent += Button_UserClickOnButtonEvent;

            VolatilityFilterIsOn = CreateParameter("Volatility filter is on", false, "Volatility stage");
            VolatilityStageOneIsOn = CreateParameter("Volatility stage one is on", true, "Volatility stage");
            VolatilityStageTwoIsOn = CreateParameter("Volatility stage two is on", true, "Volatility stage");
            VolatilityStageThreeIsOn = CreateParameter("Volatility stage three is on", true, "Volatility stage");

            VolatilitySlowSmaLength = CreateParameter("Volatility slow sma length", 25, 10, 80, 3, "Volatility stage");
            VolatilityFastSmaLength = CreateParameter("Volatility fast sma length", 7, 10, 80, 3, "Volatility stage");
            VolatilityChannelDeviation = CreateParameter("Volatility channel deviation", 0.5m, 1.0m, 50, 4, "Volatility stage");

            _volatilityStagesOnIndex
    = IndicatorsFactory.CreateIndicatorByName("VolatilityStagesAW", name + "VolatilityStagesAW", false);
            _volatilityStagesOnIndex = (Aindicator)_tabIndex.CreateCandleIndicator(_volatilityStagesOnIndex, "VolaStagesArea");
            _volatilityStagesOnIndex.ParametersDigit[0].Value = VolatilitySlowSmaLength.ValueInt;
            _volatilityStagesOnIndex.ParametersDigit[1].Value = VolatilityFastSmaLength.ValueInt;
            _volatilityStagesOnIndex.ParametersDigit[2].Value = VolatilityChannelDeviation.ValueDecimal;

            _volatilityStagesOnIndex.Save();

            _tabScreener.CreateCandleIndicator(1, "PriceChannel", new List<string>() { PriceChannelLength.ValueInt.ToString() }, "Prime");
            _tabScreener.CreateCandleIndicator(2, "ATR", new List<string>() { AtrLength.ValueInt.ToString() }, "Second");

            ParametrsChangeByUser += PriceChannelScreenerOnIndexVolatility_ParametrsChangeByUser;

            if (StartProgram == StartProgram.IsTester && ServerMaster.GetServers() != null)
            {
                TesterServer server = (TesterServer)ServerMaster.GetServers()[0];

                server.TestingStartEvent += Server_TestingStartEvent;
            }
        }

        private void Server_TestingStartEvent()
        {
            SecuritiesToTrade.ValueString = "";
            _lastTimeRating = DateTime.MinValue;
        }

        private void PriceChannelScreenerOnIndexVolatility_ParametrsChangeByUser()
        {
            if (_volatilityStagesOnIndex.ParametersDigit[0].Value != VolatilitySlowSmaLength.ValueInt
                || _volatilityStagesOnIndex.ParametersDigit[1].Value != VolatilityFastSmaLength.ValueInt
                || _volatilityStagesOnIndex.ParametersDigit[2].Value != VolatilityChannelDeviation.ValueDecimal)
            {
                _volatilityStagesOnIndex.ParametersDigit[0].Value = VolatilitySlowSmaLength.ValueInt;
                _volatilityStagesOnIndex.ParametersDigit[1].Value = VolatilityFastSmaLength.ValueInt;
                _volatilityStagesOnIndex.ParametersDigit[2].Value = VolatilityChannelDeviation.ValueDecimal;
                _volatilityStagesOnIndex.Reload();
                _volatilityStagesOnIndex.Save();
            }
        }

        private void _tabIndex_SpreadChangeEvent(List<Candle> candles)
        {
            CheckSecuritiesRating(candles);
        }

        public override string GetNameStrategyType()
        {
            return "PriceChannelScreenerOnIndexVolatility";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // securities selection

        DateTime _lastTimeRating = DateTime.MinValue;

        private void CheckSecuritiesRating(List<Candle> candlesIndex)
        {
            if(candlesIndex == null 
                || candlesIndex.Count == 0)
            {
                return;
            }

            if (_tabScreener.Tabs == null
                || _tabScreener.Tabs.Count == 0
                || _tabScreener.Tabs[0].IsConnected == false)
            {
                return;
            }

            DateTime currentTime = candlesIndex[candlesIndex.Count-1].TimeStart;

            if (currentTime.Date == _lastTimeRating.Date)
            {
                return;
            }

            _lastTimeRating = currentTime;

            // 0 check volatility stage on index

            if(VolatilityFilterIsOn.ValueBool == true)
            {
                decimal currentStage = _volatilityStagesOnIndex.DataSeries[0].Last;

                if (currentStage == 0
                    || (currentStage == 1 && VolatilityStageOneIsOn.ValueBool == false)
                    || (currentStage == 2 && VolatilityStageTwoIsOn.ValueBool == false)
                    || (currentStage == 3 && VolatilityStageThreeIsOn.ValueBool == false))
                {
                    SecuritiesToTrade.ValueString = "";

                    if (MessageOnRebuild.ValueBool == true)
                    {
                        this.SendNewLogMessage("No trading. Current volatility stage on index: " + currentStage, Logging.LogMessageType.Error);
                    }

                    return;
                }
            }

            // 1 calculate variables

            List<SecurityRatingDataPc> securityRatingData = new List<SecurityRatingDataPc>();

            List<BotTabSimple> tabs = _tabScreener.Tabs;

            for (int i = 0; i < tabs.Count; i++)
            {
                SecurityRatingDataPc newData = new SecurityRatingDataPc();
                newData.SecurityName = tabs[i].Securiti.Name;
                newData.Volume = CalculateVolume(TopCandlesLookBack.ValueInt, tabs[i]);
                newData.Volatility = GetVolatilityDiff(tabs[i].CandlesAll, candlesIndex, TopCandlesLookBack.ValueInt);

                if (newData.Volume == 0
                    || newData.Volatility == 0)
                {
                    continue;
                }

                securityRatingData.Add(newData);
            }

            if (securityRatingData.Count == 0)
            {
                return;
            }

            // 2 sort by volume

            for (int i = 0; i < securityRatingData.Count; i++)
            {
                for (int j = 1; j < securityRatingData.Count; j++)
                {
                    if (securityRatingData[j - 1].Volume < securityRatingData[j].Volume)
                    {
                        SecurityRatingDataPc d = securityRatingData[j - 1];
                        securityRatingData[j - 1] = securityRatingData[j];
                        securityRatingData[j] = d;
                    }
                }
            }

            securityRatingData = securityRatingData.GetRange(0, TopVolumeSecurities.ValueInt);

            // 3 sort by volatility difference to index

            List<SecurityRatingDataPc> securityExitValues = new List<SecurityRatingDataPc>();

            for (int i = 0; i < securityRatingData.Count; i++)
            {
                if (securityRatingData[i].Volatility > MinVolatilityDifference.ValueDecimal
                    && securityRatingData[i].Volatility < MaxVolatilityDifference.ValueDecimal)
                {
                    securityExitValues.Add(securityRatingData[i]);
                }
            }

            string securitiesInTrade = "";

            for (int i = 0; i < securityExitValues.Count; i++)
            {
                securitiesInTrade += securityExitValues[i].SecurityName + " ";
            }

            SecuritiesToTrade.ValueString = securitiesInTrade;

            if(MessageOnRebuild.ValueBool == true)
            {
                this.SendNewLogMessage("New securities in trade: \n" + securitiesInTrade, Logging.LogMessageType.Error);
            }
        }

        public decimal CalculateVolume(int daysCount, BotTabSimple tab)
        {
            List<Candle> candles = tab.CandlesAll;

            if (candles == null
                || candles.Count == 0)
            {
                return 0;
            }

            int curDay = candles[candles.Count - 1].TimeStart.Day;
            int curDaysCount = 1;
            decimal volume = 0;

            for (int i = candles.Count - 1; i > 0; i--)
            {
                Candle curCandle = candles[i];
                volume += curCandle.Volume * curCandle.Open;

                if (curDay != curCandle.TimeStart.Day)
                {
                    curDay = candles[i].TimeStart.Day;
                    curDaysCount++;

                    if (daysCount == curDaysCount)
                    {
                        break;
                    }
                }
            }

            if (tab.Securiti.Lot > 1)
            {
                volume = volume * tab.Securiti.Lot;
            }

            return volume;
        }

        private decimal GetVolatilityDiff(List<Candle> sec, List<Candle> index, int len)
        {
            // волатильность. Берём внутридневную волу за N дней в % по бумаге(V1) и по индексу(V2)
            // делим V1 / V2 - получаем отношение волатильности бумаги к индексу. 

            decimal volSec = GetVolatility(sec, len);
            decimal volIndex = GetVolatility(index, len);

            if (volIndex == 0)
            {
                return 0;
            }

            decimal result = volSec / volIndex;

            return result;
        }

        private decimal GetVolatility(List<Candle> candles, int len)
        {
            List<decimal> curDaysVola = new List<decimal>();

            decimal curMinInDay = decimal.MaxValue;
            decimal curMaxInDay = 0;
            int curDay = candles[candles.Count - 1].TimeStart.Day;
            int daysCount = 1;


            for (int i = candles.Count - 1; i > 0; i--)
            {
                Candle curCandle = candles[i];

                if (curDay != curCandle.TimeStart.Day)
                {
                    if (curMaxInDay != 0 &&
                        curMinInDay != decimal.MaxValue)
                    {
                        decimal moveInDay = curMaxInDay - curMinInDay;
                        decimal percentMove = moveInDay / (curMinInDay / 100);
                        curDaysVola.Add(percentMove);
                    }

                    curMinInDay = decimal.MaxValue;
                    curMaxInDay = 0;
                    curDay = candles[i].TimeStart.Day;

                    daysCount++;

                    if (len == daysCount)
                    {
                        break;
                    }
                }

                if (curCandle.High > curMaxInDay)
                {
                    curMaxInDay = curCandle.High;
                }
                if (curCandle.Low < curMinInDay)
                {
                    curMinInDay = curCandle.Low;
                }
            }

            if (curDaysVola.Count == 0)
            {
                return 0;
            }

            decimal result = 0;

            for (int i = 0; i < curDaysVola.Count; i++)
            {
                result += curDaysVola[i];
            }

            return result / curDaysVola.Count;
        }

        private void Button_UserClickOnButtonEvent()
        {
            _lastTimeRating = DateTime.MinValue;
            CheckSecuritiesRating(_tabIndex.Candles);
        }

        // logic

        private void _screenerTab_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
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

            if (SecuritiesToTrade.ValueString.Contains(tab.Securiti.Name) == false)
            {
                return;
            }

            if (TimeStart.Value > tab.TimeServerCurrent ||
                TimeEnd.Value < tab.TimeServerCurrent)
            {
                return;
            }

            Aindicator priceChannel = (Aindicator)tab.Indicators[0];

            if (priceChannel.ParametersDigit[0].Value != PriceChannelLength.ValueInt)
            {
                priceChannel.ParametersDigit[0].Value = PriceChannelLength.ValueInt;
                priceChannel.Save();
                priceChannel.Reload();
            }

            if (priceChannel.DataSeries[0].Values.Count == 0 ||
                priceChannel.DataSeries[0].Last == 0)
            {
                return;
            }

            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal lastPcUp = priceChannel.DataSeries[0].Values[priceChannel.DataSeries[0].Values.Count - 2];
            decimal lastPcDown = priceChannel.DataSeries[1].Values[priceChannel.DataSeries[1].Values.Count - 2];

            if (lastPcUp == 0
                || lastPcDown == 0)
            {
                return;
            }

            Aindicator atr = (Aindicator)tab.Indicators[1];

            if (atr.ParametersDigit[0].Value != AtrLength.ValueInt)
            {
                atr.ParametersDigit[0].Value = AtrLength.ValueInt;
                atr.Save();
                atr.Reload();
            }

            if (atr.DataSeries[0].Values.Count == 0 ||
                atr.DataSeries[0].Last == 0)
            {
                return;
            }

            if (lastPrice > lastPcUp
                && Regime.ValueString != "OnlyShort")
            {
                if (AtrFilterIsOn.ValueBool == true)
                {
                    if (atr.DataSeries[0].Values.Count - 1 - AtrGrowLookBack.ValueInt <= 0)
                    {
                        return;
                    }
                    decimal atrLast = atr.DataSeries[0].Values[atr.DataSeries[0].Values.Count - 1];
                    decimal atrLookBack =
                    atr.DataSeries[0].Values[atr.DataSeries[0].Values.Count - 1 - AtrGrowLookBack.ValueInt];
                    if (atrLast == 0
                        || atrLookBack == 0)
                    {
                        return;
                    }

                    decimal atrGrowPercent = atrLast / (atrLookBack / 100) - 100;

                    if (atrGrowPercent < AtrGrowPercent.ValueDecimal)
                    {
                        return;
                    }
                }

                tab.BuyAtMarket(GetVolume(tab));
            }

            if (lastPrice < lastPcDown
                && Regime.ValueString != "OnlyLong")
            {
                if (AtrFilterIsOn.ValueBool == true)
                {
                    if (atr.DataSeries[0].Values.Count - 1 - AtrGrowLookBack.ValueInt <= 0)
                    {
                        return;
                    }
                    decimal atrLast = atr.DataSeries[0].Values[atr.DataSeries[0].Values.Count - 1];
                    decimal atrLookBack =
                    atr.DataSeries[0].Values[atr.DataSeries[0].Values.Count - 1 - AtrGrowLookBack.ValueInt];
                    if (atrLast == 0
                        || atrLookBack == 0)
                    {
                        return;
                    }

                    decimal atrGrowPercent = atrLast / (atrLookBack / 100) - 100;

                    if (atrGrowPercent < AtrGrowPercent.ValueDecimal)
                    {
                        return;
                    }
                }

                tab.SellAtMarket(GetVolume(tab));
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

            Aindicator _pc = (Aindicator)tab.Indicators[0];

            decimal lastPcUp = _pc.DataSeries[0].Values[_pc.DataSeries[0].Values.Count - 1];
            decimal lastPcDown = _pc.DataSeries[1].Values[_pc.DataSeries[1].Values.Count - 1];

            if (position.Direction == Side.Buy)
            {
                tab.CloseAtTrailingStopMarket(position, lastPcDown);
            }
            if (position.Direction == Side.Sell)
            {
                tab.CloseAtTrailingStopMarket(position, lastPcUp);
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

    }

    public class SecurityRatingDataPc
    {
        public string SecurityName;

        public decimal Volatility;

        public decimal Volume;
    }
}