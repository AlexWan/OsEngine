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
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on ZigZagChannel Screener RsiFilter.

Buy:
1. The total number of open positions is below the maximum allowed.
2. The last candle’s close is above the upper ZigZag line.
3. The SMA is rising.

Exit:
1. The position is open and not already being closed.
2. If stop is greater than the current price.
3. Otherwise, place a trailing stop order using the calculated levels.
 */

namespace OsEngine.Robots.VolatilityStageRotationSamples
{
    [Bot("ZigZagChannelScreenerRsiFilter")] // We create an attribute so that we don't write anything to the BotFactory
    public class ZigZagChannelScreenerRsiFilter : BotPanel
    {
        private BotTabScreener _tabScreener;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _maxPositions;
        private StrategyParameterDecimal _slippage;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Securities settings
        private StrategyParameterInt _maxSecuritiesToTrade;
        private StrategyParameterInt _topVolumeSecurities;
        private StrategyParameterInt _topVolumeDaysLookBack;
        private StrategyParameterString _securitiesToTrade;

        // Indicator settings
        private StrategyParameterInt _zigZagChannelLen;
        private StrategyParameterInt _rsiLen;

        // Exit setting
        private StrategyParameterDecimal _trailStop;

        public ZigZagChannelScreenerRsiFilter(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _tabScreener = TabsScreener[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyClosePosition" });
            _maxPositions = CreateParameter("Max positions", 5, 0, 20, 1);
            _slippage = CreateParameter("Slippage %", 0, 0, 20, 1m);

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Securities settings
            _topVolumeSecurities = CreateParameter("Top volume securities", 15, 0, 20, 1);
            _topVolumeDaysLookBack = CreateParameter("Top volume days look back", 3, 0, 20, 1);
            _maxSecuritiesToTrade = CreateParameter("Max securities to trade", 5, 0, 20, 1);
            _securitiesToTrade = CreateParameter("Securities to trade", "");

            StrategyParameterButton button = CreateParameterButton("Check securities rating");
            button.UserClickOnButtonEvent += Button_UserClickOnButtonEvent;

            // Indicator settings
            _zigZagChannelLen = CreateParameter("ZigZag channel length", 50, 0, 20, 1);
            _rsiLen = CreateParameter("Rsi length", 25, 0, 20, 1);

            // Exit setting
            _trailStop = CreateParameter("Trail stop %", 2.9m, 0, 20, 1m);

            // Subscribe to the candle finished event
            _tabScreener.CandleFinishedEvent += _screenerTab_CandleFinishedEvent;

            // Create indicator ZizZagChannel
            _tabScreener.CreateCandleIndicator(1, "ZigZagChannel_indicator", new List<string>() { _zigZagChannelLen.ValueInt.ToString() }, "Prime");

            // Create indicator Rsi
            _tabScreener.CreateCandleIndicator(2, "RSI", new List<string>() { _rsiLen.ValueInt.ToString() }, "Second");

            Description = OsLocalization.Description.DescriptionLabel124;

            if (StartProgram == StartProgram.IsTester && ServerMaster.GetServers() != null)
            {
                List<IServer> servers = ServerMaster.GetServers();

                if (servers != null
                    && servers.Count > 0
                    && servers[0].ServerType == ServerType.Tester)
                {
                    TesterServer server = (TesterServer)servers[0];
                    server.TestingStartEvent += Server_TestingStartEvent;
                }
            }
        }

        private void Server_TestingStartEvent()
        {
            _securitiesToTrade.ValueString = "";
            _lastTimeRating = DateTime.MinValue;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "ZigZagChannelScreenerRsiFilter";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Securities rating

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
                newData.SecurityName = tabs[i].Security.Name;
                newData.Volume = CalculateVolume(_topVolumeDaysLookBack.ValueInt,tabs[i]);
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

            int count = _topVolumeSecurities.ValueInt;

            if (count > securityRatingData.Count)
            {
                count = securityRatingData.Count;
            }

            securityRatingData = securityRatingData.GetRange(0, count);

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

            securityRatingData = securityRatingData.GetRange(0, count);

            string securitiesInTrade = "";

            for (int i = 0; i < securityRatingData.Count; i++)
            {
                securitiesInTrade += securityRatingData[i].SecurityName + " ";
            }

            _securitiesToTrade.ValueString = securitiesInTrade;
        }

        // Method for calculating volume
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

            if (tab.Security.Lot > 1)
            {
                volume = volume * tab.Security.Lot;
            }

            return volume;
        }

        // Method get value Rsi
        private decimal GetRsi(BotTabSimple tab)
        {
            Aindicator rsi = (Aindicator)tab.Indicators[1];

            if (rsi.ParametersDigit[0].Value != _rsiLen.ValueInt)
            {
                rsi.ParametersDigit[0].Value = _rsiLen.ValueInt;
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
            if (_regime.ValueString == "Off")
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
                if (_regime.ValueString == "OnlyClosePosition")
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

        // Opening logic
        private void LogicOpenPosition(List<Candle> candles, BotTabSimple tab)
        {
            if (_tabScreener.PositionsOpenAll.Count >= _maxPositions.ValueInt)
            {
                return;
            }

            if (_securitiesToTrade.ValueString.Contains(tab.Security.Name) == false)
            {
                return;
            }

            Aindicator zigZag = (Aindicator)tab.Indicators[0];

            if (zigZag.ParametersDigit[0].Value != _zigZagChannelLen.ValueInt)
            {
                zigZag.ParametersDigit[0].Value = _zigZagChannelLen.ValueInt;
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

        // Logic close position
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

            stop = lastClose - lastClose * (_trailStop.ValueDecimal / 100);
            stopWithSlippage = stop - stop * (_slippage.ValueDecimal / 100);

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
                position.StopOrderIsActive = false;
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

        // Method for calculating sma
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