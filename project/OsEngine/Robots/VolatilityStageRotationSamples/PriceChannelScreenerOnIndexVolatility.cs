/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

/* Description
trading robot for osengine

The trend robot on PriceChannel Screener On Index Volatility.

Buy:
1. The price breaks above the upper band of the Price Channel.
2. (If ATR filter is enabled) The ATR has grown by at least X% over the last N candles.

Sell:
1. The price breaks below the lower band of the Price Channel.
2. (If ATR filter is enabled) The ATR has grown by at least X% over the last N candles.

Exit for long: Close using a trailing stop set at the lower Price Channel band.
Exit for short: Close using a trailing stop set at the upper Price Channel band.
 */

namespace OsEngine.Robots.VolatilityStageRotationSamples
{
    [Bot("PriceChannelScreenerOnIndexVolatility")] // We create an attribute so that we don't write anything to the BotFactory
    public class PriceChannelScreenerOnIndexVolatility : BotPanel
    {
        private BotTabScreener _tabScreener;
        private BotTabIndex _tabIndex;

        // Indicator
        private Aindicator _volatilityStagesOnIndex;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _maxPositions;
        private StrategyParameterTimeOfDay _timeStart;
        private StrategyParameterTimeOfDay _timeEnd;

        // Indicator settings
        private StrategyParameterInt _priceChannelLength;
        private StrategyParameterBool _atrFilterIsOn;
        private StrategyParameterInt _atrLength;
        private StrategyParameterDecimal _atrGrowPercent;
        private StrategyParameterInt _atrGrowLookBack;

        // Volatility settings
        private StrategyParameterBool _volatilityFilterIsOn;
        private StrategyParameterBool _volatilityStageOneIsOn;
        private StrategyParameterBool _volatilityStageTwoIsOn;
        private StrategyParameterBool _volatilityStageThreeIsOn;
        private StrategyParameterInt _volatilitySlowSmaLength;
        private StrategyParameterInt _volatilityFastSmaLength;
        private StrategyParameterDecimal _volatilityChannelDeviation;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        private StrategyParameterInt _topVolumeSecurities;
        private StrategyParameterInt _topCandlesLookBack;
        private StrategyParameterDecimal _maxVolatilityDifference;
        private StrategyParameterDecimal _minVolatilityDifference;
        private StrategyParameterString _securitiesToTrade;
        private StrategyParameterBool _messageOnRebuild;

        public PriceChannelScreenerOnIndexVolatility(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Index);
            _tabIndex = TabsIndex[0];

            // Subscribe to the spread change event
            _tabIndex.SpreadChangeEvent += _tabIndex_SpreadChangeEvent;

            TabCreate(BotTabType.Screener);
            _tabScreener = TabsScreener[0];

            // Subscribe to the candle finished event
            _tabScreener.CandleFinishedEvent += _screenerTab_CandleFinishedEvent;

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            _maxPositions = CreateParameter("Max positions", 5, 0, 20, 1);
            _timeStart = CreateParameterTimeOfDay("Start Trade Time", 10, 32, 0, 0);
            _timeEnd = CreateParameterTimeOfDay("End Trade Time", 18, 25, 0, 0);

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _priceChannelLength = CreateParameter("Price channel length", 50, 10, 80, 3);
            _atrLength = CreateParameter("Atr length", 25, 10, 80, 3);
            _atrFilterIsOn = CreateParameter("Atr filter is on", false);
            _atrGrowPercent = CreateParameter("Atr grow percent", 3, 1.0m, 50, 4);
            _atrGrowLookBack = CreateParameter("Atr grow look back", 20, 1, 50, 4);

            _topVolumeSecurities = CreateParameter("Top volume securities", 15, 0, 20, 1, "Volatility stage");
            _topCandlesLookBack = CreateParameter("Top days look back", 5, 0, 20, 1, "Volatility stage");
            _maxVolatilityDifference = CreateParameter("Vol Diff Max", 1.4m, 1.0m, 50, 4, "Volatility stage");
            _minVolatilityDifference = CreateParameter("Vol Diff Min", 1.0m, 1.0m, 50, 4, "Volatility stage");
            _securitiesToTrade = CreateParameter("Securities to trade", "", "Volatility stage");
            _messageOnRebuild = CreateParameter("Message on rebuild", true, "Volatility stage");
            StrategyParameterButton button = CreateParameterButton("Check securities rating", "Volatility stage");
            button.UserClickOnButtonEvent += Button_UserClickOnButtonEvent;

            // Volatility settings
            _volatilityFilterIsOn = CreateParameter("Volatility filter is on", false, "Volatility stage");
            _volatilityStageOneIsOn = CreateParameter("Volatility stage one is on", true, "Volatility stage");
            _volatilityStageTwoIsOn = CreateParameter("Volatility stage two is on", true, "Volatility stage");
            _volatilityStageThreeIsOn = CreateParameter("Volatility stage three is on", true, "Volatility stage");
            _volatilitySlowSmaLength = CreateParameter("Volatility slow sma length", 25, 10, 80, 3, "Volatility stage");
            _volatilityFastSmaLength = CreateParameter("Volatility fast sma length", 7, 10, 80, 3, "Volatility stage");
            _volatilityChannelDeviation = CreateParameter("Volatility channel deviation", 0.5m, 1.0m, 50, 4, "Volatility stage");

            // Create Indicator VolatilityStagesAW
            _volatilityStagesOnIndex = IndicatorsFactory.CreateIndicatorByName("VolatilityStagesAW", name + "VolatilityStagesAW", false);
            _volatilityStagesOnIndex = (Aindicator)_tabIndex.CreateCandleIndicator(_volatilityStagesOnIndex, "VolaStagesArea");
            _volatilityStagesOnIndex.ParametersDigit[0].Value = _volatilitySlowSmaLength.ValueInt;
            _volatilityStagesOnIndex.ParametersDigit[1].Value = _volatilityFastSmaLength.ValueInt;
            _volatilityStagesOnIndex.ParametersDigit[2].Value = _volatilityChannelDeviation.ValueDecimal;
            _volatilityStagesOnIndex.Save();

            // Create Indicator PriceChannel and ATR
            _tabScreener.CreateCandleIndicator(1, "PriceChannel", new List<string>() { _priceChannelLength.ValueInt.ToString() }, "Prime");
            _tabScreener.CreateCandleIndicator(2, "ATR", new List<string>() { _atrLength.ValueInt.ToString() }, "Second");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += PriceChannelScreenerOnIndexVolatility_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel122;

            if (StartProgram == StartProgram.IsTester 
                && ServerMaster.GetServers() != null)
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

        private void PriceChannelScreenerOnIndexVolatility_ParametrsChangeByUser()
        {
            if (_volatilityStagesOnIndex.ParametersDigit[0].Value != _volatilitySlowSmaLength.ValueInt
                || _volatilityStagesOnIndex.ParametersDigit[1].Value != _volatilityFastSmaLength.ValueInt
                || _volatilityStagesOnIndex.ParametersDigit[2].Value != _volatilityChannelDeviation.ValueDecimal)
            {
                _volatilityStagesOnIndex.ParametersDigit[0].Value = _volatilitySlowSmaLength.ValueInt;
                _volatilityStagesOnIndex.ParametersDigit[1].Value = _volatilityFastSmaLength.ValueInt;
                _volatilityStagesOnIndex.ParametersDigit[2].Value = _volatilityChannelDeviation.ValueDecimal;
                _volatilityStagesOnIndex.Reload();
                _volatilityStagesOnIndex.Save();
            }
        }

        private void _tabIndex_SpreadChangeEvent(List<Candle> candles)
        {
            CheckSecuritiesRating(candles);
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "PriceChannelScreenerOnIndexVolatility";
        }

        // Show settings GUI
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

            if(_volatilityFilterIsOn.ValueBool == true)
            {
                decimal currentStage = _volatilityStagesOnIndex.DataSeries[0].Last;

                if (currentStage == 0
                    || (currentStage == 1 && _volatilityStageOneIsOn.ValueBool == false)
                    || (currentStage == 2 && _volatilityStageTwoIsOn.ValueBool == false)
                    || (currentStage == 3 && _volatilityStageThreeIsOn.ValueBool == false))
                {
                    _securitiesToTrade.ValueString = "";

                    if (_messageOnRebuild.ValueBool == true)
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
                newData.SecurityName = tabs[i].Security.Name;
                newData.Volume = CalculateVolume(_topCandlesLookBack.ValueInt, tabs[i]);
                newData.Volatility = GetVolatilityDiff(tabs[i].CandlesAll, candlesIndex, _topCandlesLookBack.ValueInt);

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

            securityRatingData = securityRatingData.GetRange(0, _topVolumeSecurities.ValueInt);

            // 3 sort by volatility difference to index

            List<SecurityRatingDataPc> securityExitValues = new List<SecurityRatingDataPc>();

            for (int i = 0; i < securityRatingData.Count; i++)
            {
                if (securityRatingData[i].Volatility > _minVolatilityDifference.ValueDecimal
                    && securityRatingData[i].Volatility < _maxVolatilityDifference.ValueDecimal)
                {
                    securityExitValues.Add(securityRatingData[i]);
                }
            }

            string securitiesInTrade = "";

            for (int i = 0; i < securityExitValues.Count; i++)
            {
                securitiesInTrade += securityExitValues[i].SecurityName + " ";
            }

            _securitiesToTrade.ValueString = securitiesInTrade;

            if(_messageOnRebuild.ValueBool == true)
            {
                this.SendNewLogMessage("New securities in trade: \n" + securitiesInTrade, Logging.LogMessageType.Error);
            }
        }

        // Method for calculating volume
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

            if (tab.Security.Lot > 1)
            {
                volume = volume * tab.Security.Lot;
            }

            return volume;
        }

        private decimal GetVolatilityDiff(List<Candle> sec, List<Candle> index, int len)
        {
            if(sec == null || sec.Count == 0)
            {
                return 0;
            }

            if (index == null || index.Count == 0)
            {
                return 0;
            }

            // Volatility. We take the intraday volatility over N days for the stock (V1) and for the index (V2) in percentage terms.
            // Then, we divide V1 by V2 to get the ratio of the stock's volatility to the index's volatility.

            decimal volSec = GetVolatility(sec, len);
            decimal volIndex = GetVolatility(index, len);

            if (volIndex == 0)
            {
                return 0;
            }

            decimal result = volSec / volIndex;

            return result;
        }

        // Method get value Volatility
        private decimal GetVolatility(List<Candle> candles, int len)
        {
            if (candles == null
                || candles.Count == 0)
            {
                return 0;
            }

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
            if (_regime.ValueString == "Off")
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

            if (_timeStart.Value > tab.TimeServerCurrent ||
                _timeEnd.Value < tab.TimeServerCurrent)
            {
                return;
            }

            Aindicator priceChannel = (Aindicator)tab.Indicators[0];

            if (priceChannel.ParametersDigit[0].Value != _priceChannelLength.ValueInt)
            {
                priceChannel.ParametersDigit[0].Value = _priceChannelLength.ValueInt;
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

            if (atr.ParametersDigit[0].Value != _atrLength.ValueInt)
            {
                atr.ParametersDigit[0].Value = _atrLength.ValueInt;
                atr.Save();
                atr.Reload();
            }

            if (atr.DataSeries[0].Values.Count == 0 ||
                atr.DataSeries[0].Last == 0)
            {
                return;
            }

            if (lastPrice > lastPcUp && _regime.ValueString != "OnlyShort")
            {
                if (_atrFilterIsOn.ValueBool == true)
                {
                    if (atr.DataSeries[0].Values.Count - 1 - _atrGrowLookBack.ValueInt <= 0)
                    {
                        return;
                    }

                    decimal atrLast = atr.DataSeries[0].Values[atr.DataSeries[0].Values.Count - 1];
                    decimal atrLookBack = atr.DataSeries[0].Values[atr.DataSeries[0].Values.Count - 1 - _atrGrowLookBack.ValueInt];

                    if (atrLast == 0
                        || atrLookBack == 0)
                    {
                        return;
                    }

                    decimal atrGrowPercent = atrLast / (atrLookBack / 100) - 100;

                    if (atrGrowPercent < _atrGrowPercent.ValueDecimal)
                    {
                        return;
                    }
                }

                tab.BuyAtMarket(GetVolume(tab));
            }

            if (lastPrice < lastPcDown && _regime.ValueString != "OnlyLong")
            {
                if (_atrFilterIsOn.ValueBool == true)
                {
                    if (atr.DataSeries[0].Values.Count - 1 - _atrGrowLookBack.ValueInt <= 0)
                    {
                        return;
                    }

                    decimal atrLast = atr.DataSeries[0].Values[atr.DataSeries[0].Values.Count - 1];
                    decimal atrLookBack = atr.DataSeries[0].Values[atr.DataSeries[0].Values.Count - 1 - _atrGrowLookBack.ValueInt];

                    if (atrLast == 0
                        || atrLookBack == 0)
                    {
                        return;
                    }

                    decimal atrGrowPercent = atrLast / (atrLookBack / 100) - 100;

                    if (atrGrowPercent < _atrGrowPercent.ValueDecimal)
                    {
                        return;
                    }
                }

                tab.SellAtMarket(GetVolume(tab));
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

            Aindicator _pc = (Aindicator)tab.Indicators[0];

            decimal lastPcUp = _pc.DataSeries[0].Values[_pc.DataSeries[0].Values.Count - 1];
            decimal lastPcDown = _pc.DataSeries[1].Values[_pc.DataSeries[1].Values.Count - 1];

            if (position.Direction == Side.Buy) // If the direction of the position is long
            {
                tab.CloseAtTrailingStopMarket(position, lastPcDown);
            }
            if (position.Direction == Side.Sell) // If the direction of the position is short
            {
                tab.CloseAtTrailingStopMarket(position, lastPcUp);
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

    public class SecurityRatingDataPc
    {
        public string SecurityName;

        public decimal Volatility;

        public decimal Volume;
    }
}