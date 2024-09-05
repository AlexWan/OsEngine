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
using OsEngine.Market.Servers.Tester;

namespace OsEngine.Robots.VolatilityStageRotationSamples
{
    [Bot("ZigZagChannelScreenerRsiFilter")]
    public class ZigZagChannelScreenerRsiFilter : BotPanel
    {
        BotTabScreener _tabScreener;

        public StrategyParameterString Regime;
        public StrategyParameterInt MaxPositions;
        public StrategyParameterInt ZigZagChannelLen;
        public StrategyParameterInt RsiLen;

        public StrategyParameterDecimal Slippage;
        public StrategyParameterString VolumeType;
        public StrategyParameterDecimal Volume;
        public StrategyParameterString TradeAssetInPortfolio;
        public StrategyParameterDecimal TrailStop;

        public StrategyParameterInt MaxSecuritiesToTrade;
        public StrategyParameterInt TopVolumeSecurities;
        public StrategyParameterInt TopVolumeDaysLookBack;
        public StrategyParameterString SecuritiesToTrade;

        public ZigZagChannelScreenerRsiFilter(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);

            _tabScreener = TabsScreener[0];

            _tabScreener.CandleFinishedEvent += _screenerTab_CandleFinishedEvent;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyClosePosition" });

            MaxPositions = CreateParameter("Max positions", 5, 0, 20, 1);

            TopVolumeSecurities = CreateParameter("Top volume securities", 15, 0, 20, 1);
            TopVolumeDaysLookBack = CreateParameter("Top volume days look back", 3, 0, 20, 1);

            MaxSecuritiesToTrade = CreateParameter("Max securities to trade", 5, 0, 20, 1);
            SecuritiesToTrade = CreateParameter("Securities to trade", "");
            StrategyParameterButton button = CreateParameterButton("Check securities rating");
            button.UserClickOnButtonEvent += Button_UserClickOnButtonEvent;

            ZigZagChannelLen = CreateParameter("ZigZag channel length", 50, 0, 20, 1);

            RsiLen = CreateParameter("Rsi length", 25, 0, 20, 1);

            TrailStop = CreateParameter("Trail stop %", 2.9m, 0, 20, 1m);

            VolumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });

            Volume = CreateParameter("Volume", 20, 1.0m, 50, 4);

            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            Slippage = CreateParameter("Slippage %", 0, 0, 20, 1m);

            _tabScreener.CreateCandleIndicator(1, "ZigZagChannel_indicator", new List<string>() { ZigZagChannelLen.ValueInt.ToString() }, "Prime");

            _tabScreener.CreateCandleIndicator(2, "RSI", new List<string>() { RsiLen.ValueInt.ToString() }, "Second");

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

        public override string GetNameStrategyType()
        {
            return "ZigZagChannelScreenerRsiFilter";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // securities rating

        DateTime _lastTimeRating = DateTime.MinValue;

        private void CheckSecuritiesRating()
        {
            if(_tabScreener.Tabs == null 
                || _tabScreener.Tabs.Count == 0
                || _tabScreener.Tabs[0].IsConnected == false)
            {
                return;
            }

            DateTime currentTime = _tabScreener.Tabs[0].TimeServerCurrent;

            if (currentTime.Date == _lastTimeRating.Date)
            {
                return;
            }
            _lastTimeRating = _tabScreener.Tabs[0].TimeServerCurrent;

            List<SecurityRatingData> securityRatingData = new List<SecurityRatingData>();

            List<BotTabSimple> tabs = _tabScreener.Tabs;

            for(int i = 0;i < tabs.Count;i++)
            {
                SecurityRatingData newData = new SecurityRatingData();
                newData.SecurityName = tabs[i].Securiti.Name;
                newData.Volume = CalculateVolume(TopVolumeDaysLookBack.ValueInt,tabs[i]);
                newData.Rsi = GetRsi(tabs[i]);

                if(newData.Volume == 0
                    || newData.Rsi == 0)
                {
                    continue;
                }

                securityRatingData.Add(newData);
            }

            if(securityRatingData.Count == 0)
            {
                return;
            }

            for(int i = 0;i < securityRatingData.Count;i++)
            {
                for(int j = 1;j < securityRatingData.Count;j++)
                {
                    if (securityRatingData[j-1].Volume < securityRatingData[j].Volume)
                    {
                        SecurityRatingData d = securityRatingData[j-1];
                        securityRatingData[j - 1] = securityRatingData[j];
                        securityRatingData[j] = d;
                    }
                }
            }

            securityRatingData = securityRatingData.GetRange(0, TopVolumeSecurities.ValueInt);

            for (int i = 0; i < securityRatingData.Count; i++)
            {
                for (int j = 1; j < securityRatingData.Count; j++)
                {
                    if (securityRatingData[j - 1].Rsi < securityRatingData[j].Rsi)
                    {
                        SecurityRatingData d = securityRatingData[j - 1];
                        securityRatingData[j - 1] = securityRatingData[j];
                        securityRatingData[j] = d;
                    }
                }
            }

            securityRatingData = securityRatingData.GetRange(0, MaxSecuritiesToTrade.ValueInt);

            string securitiesInTrade = "";

            for (int i = 0; i < securityRatingData.Count; i++)
            {
                securitiesInTrade += securityRatingData[i].SecurityName + " ";
            }

            SecuritiesToTrade.ValueString = securitiesInTrade;
        }

        public decimal CalculateVolume(int daysCount,BotTabSimple tab)
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

        private decimal GetRsi(BotTabSimple tab)
        {
            Aindicator rsi = (Aindicator)tab.Indicators[1];

            if (rsi.ParametersDigit[0].Value != RsiLen.ValueInt)
            {
                rsi.ParametersDigit[0].Value = RsiLen.ValueInt;
                rsi.Save();
                rsi.Reload();
            }

            if (rsi.DataSeries[0].Values.Count == 0 ||
                rsi.DataSeries[0].Last == 0)
            {
                return 0 ;
            }

            decimal rsiValue = rsi.DataSeries[0].Last;

            return rsiValue;
        }

        private void Button_UserClickOnButtonEvent()
        {
            _lastTimeRating = DateTime.MinValue;
            CheckSecuritiesRating();
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

            CheckSecuritiesRating();

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

            Aindicator zigZag = (Aindicator)tab.Indicators[0];

            if (zigZag.ParametersDigit[0].Value != ZigZagChannelLen.ValueInt)
            {
                zigZag.ParametersDigit[0].Value = ZigZagChannelLen.ValueInt;
                zigZag.Save();
                zigZag.Reload();
            }

            if (zigZag.DataSeries[4].Values.Count == 0 ||
                zigZag.DataSeries[4].Last == 0)
            {
                return;
            }

            decimal zigZagUpLine = zigZag.DataSeries[4].Last;
            decimal lastCandleClose = candles[candles.Count - 1].Close;

            if (lastCandleClose > zigZagUpLine)
            {
                decimal smaValue = Sma(candles, 150, candles.Count - 1);
                decimal smaPrev = Sma(candles, 150, candles.Count - 2);

                if(smaValue > smaPrev)
                {
                    tab.BuyAtMarket(GetVolume(tab));
                }
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

            decimal lastClose = candles[candles.Count - 1].Close;

            decimal stop = 0;
            decimal stopWithSlippage = 0;

            stop = lastClose - lastClose * (TrailStop.ValueDecimal / 100);
            stopWithSlippage = stop - stop * (Slippage.ValueDecimal / 100);

            if(stop > lastClose)
            {
                tab.CloseAtMarket(position, position.OpenVolume);
                return;
            }

            tab.CloseAtTrailingStop(position, stop, stopWithSlippage);

            Aindicator zigZag = (Aindicator)tab.Indicators[0];

            if (zigZag.DataSeries[4].Values.Count == 0 ||
                zigZag.DataSeries[4].Last == 0)
            {
                return;
            }

            decimal zigZagUpLine = zigZag.DataSeries[4].Last;
            decimal lastCandleClose = candles[candles.Count - 1].Close;

            if(zigZagUpLine != 0 &&
                lastCandleClose > zigZagUpLine)
            {
                position.StopOrderIsActiv = false;
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
    }

    public class SecurityRatingData
    {
        public string SecurityName;

        public decimal Rsi;

        public decimal Volume;
    }
}