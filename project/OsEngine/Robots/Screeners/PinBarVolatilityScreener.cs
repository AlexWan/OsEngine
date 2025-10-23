/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using System;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.Attributes;
using System.Globalization;
using System.IO;
using OsEngine.Market.Servers.Tester;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on PinBar Volatility Screener.

Buy:
1. The last candle's close and open prices are near the high of the candle, specifically within the top third of the candle's range.
2. The candle's high-to-low length exceeds a specified threshold (HeightPinBar).
3. If the Simple Moving Average (SMA) filter is enabled, the current SMA must be higher than the previous SMA, indicating an upward trend.

Sell:
1. The last candle's close and open prices are near the low of the candle, specifically within the bottom third of the candle's range.
2. The candle's high-to-low length exceeds HeightPinBar.
3. If SMA filter is enabled, the current SMA must be lower than the previous SMA, indicating a downward trend.

Exit: We close the position by trailing stop.
 */

namespace OsEngine.Robots.Screeners
{
    [Bot("PinBarVolatilityScreener")] // We create an attribute so that we don't write anything to the BotFactory
    public class PinBarVolatilityScreener : BotPanel
    {
        private BotTabScreener _tabScreener;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _maxPositions;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Volatility settings
        private StrategyParameterInt _daysVolatilityAdaptive;
        private StrategyParameterDecimal _heightPinBarVolaPercent;

        // Indicator settings
        private StrategyParameterBool _smaFilterIsOn;
        private StrategyParameterInt _smaFilterLen;

        // Exit setting
        private StrategyParameterDecimal _procHeightStop;

        // Volatility adaptation
        private List<SecuritiesVolatilitySettings> _tradeSettings = new List<SecuritiesVolatilitySettings>();

        public PinBarVolatilityScreener(string name, StartProgram startProgram)
           : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _tabScreener = TabsScreener[0];

            // Subscribe to the candle finished event
            _tabScreener.CandleFinishedEvent += _tab_CandleFinishedEvent1;

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            _maxPositions = CreateParameter("Max positions", 10, 0, 20, 1);

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 10, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Volatility settings
            _daysVolatilityAdaptive = CreateParameter("Days volatility adaptive", 5, 0, 20, 1);
            _heightPinBarVolaPercent = CreateParameter("Height PinBar volatility percent", 30, 0, 20, 1m);

            // Indicator settings
            _smaFilterIsOn = CreateParameter("Sma filter is on", true);
            _smaFilterLen = CreateParameter("Sma filter Len", 100, 100, 300, 10);
            
            // Exit setting
            _procHeightStop = CreateParameter("Stop % from height of pattern", 20m, 0, 20, 1m);

            Description = OsLocalization.Description.DescriptionLabel90;

            if (startProgram == StartProgram.IsOsTrader)
            {
                LoadTradeSettings();
            }
            else if(startProgram == StartProgram.IsTester)
            {
                List<IServer> servers = ServerMaster.GetServers();

                if(servers != null 
                    && servers.Count > 0
                    && servers[0].ServerType == ServerType.Tester)
                {
                    TesterServer server = (TesterServer)servers[0];
                    server.TestingStartEvent += Server_TestingStartEvent;
                }
            }

            this.DeleteEvent += Screener_DeleteEvent;
        }

        private void Server_TestingStartEvent()
        {
            _tradeSettings.Clear();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "PinBarVolatilityScreener";
        }

        // Snow settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Save settings
        private void SaveTradeSettings()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    for (int i = 0; i < _tradeSettings.Count; i++)
                    {
                        writer.WriteLine(_tradeSettings[i].GetSaveString());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        // Load settings
        private void LoadTradeSettings()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        string line = reader.ReadLine();

                        if (string.IsNullOrEmpty(line))
                        {
                            continue;
                        }

                        SecuritiesVolatilitySettings newSettings = new SecuritiesVolatilitySettings();
                        newSettings.LoadFromString(line);
                        _tradeSettings.Add(newSettings);
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void Screener_DeleteEvent()
        {
            try
            {
                if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void AdaptPinBarHeight(List<Candle> candles, SecuritiesVolatilitySettings settings)
        {
            if (_daysVolatilityAdaptive.ValueInt <= 0
                || _heightPinBarVolaPercent.ValueDecimal <= 0)
            {
                return;
            }

            // 1 We are calculating the movement from the high point to the low point within N days

            decimal minValueInDay = decimal.MaxValue;
            decimal maxValueInDay = decimal.MinValue;

            List<decimal> volaInDaysPercent = new List<decimal>();

            DateTime date = candles[candles.Count - 1].TimeStart.Date;

            int days = 0;

            for (int i = candles.Count - 1; i >= 0; i--)
            {
                Candle curCandle = candles[i];

                if (curCandle.TimeStart.Date < date)
                {
                    date = curCandle.TimeStart.Date;
                    days++;

                    decimal volaAbsToday = maxValueInDay - minValueInDay;
                    decimal volaPercentToday = volaAbsToday / (minValueInDay / 100);

                    volaInDaysPercent.Add(volaPercentToday);


                    minValueInDay = decimal.MaxValue;
                    maxValueInDay = decimal.MinValue;
                }

                if (days >= _daysVolatilityAdaptive.ValueInt)
                {
                    break;
                }

                if (curCandle.High > maxValueInDay)
                {
                    maxValueInDay = curCandle.High;
                }
                if (curCandle.Low < minValueInDay)
                {
                    minValueInDay = curCandle.Low;
                }

                if (i == 0)
                {
                    days++;

                    decimal volaAbsToday = maxValueInDay - minValueInDay;
                    decimal volaPercentToday = volaAbsToday / (minValueInDay / 100);
                    volaInDaysPercent.Add(volaPercentToday);
                }
            }

            if (volaInDaysPercent.Count == 0)
            {
                return;
            }

            // 2 We are averaging this movement.An average volatility is needed. percentage

            decimal volaPercentSma = 0;

            for (int i = 0; i < volaInDaysPercent.Count; i++)
            {
                volaPercentSma += volaInDaysPercent[i];
            }

            volaPercentSma = volaPercentSma / volaInDaysPercent.Count;

            // 3 We calculate the size of the candles taking this volatility into account

            decimal allSoldiersHeight = volaPercentSma * (_heightPinBarVolaPercent.ValueDecimal / 100);

            settings.HeightPinBar = allSoldiersHeight;
            settings.LastUpdateTime = candles[candles.Count - 1].TimeStart;
        }

        // Logic
        private void _tab_CandleFinishedEvent1(List<Candle> candles, BotTabSimple tab)
        {
            SecuritiesVolatilitySettings mySettings = null;

            for (int i = 0; i < _tradeSettings.Count; i++)
            {
                if (_tradeSettings[i].SecName == tab.Security.Name &&
                    _tradeSettings[i].SecClass == tab.Security.NameClass)
                {
                    mySettings = _tradeSettings[i];
                    break;
                }
            }

            if (mySettings == null)
            {
                mySettings = new SecuritiesVolatilitySettings();
                mySettings.SecName = tab.Security.Name;
                mySettings.SecClass = tab.Security.NameClass;
                _tradeSettings.Add(mySettings);
            }

            if (mySettings.LastUpdateTime.Date != candles[candles.Count - 1].TimeStart.Date)
            {
                AdaptPinBarHeight(candles, mySettings);

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    SaveTradeSettings();
                }
            }

            if (mySettings.HeightPinBar == 0)
            {
                return;
            }

            Logic(candles, tab, mySettings);
        }

        private void Logic(List<Candle> candles, BotTabSimple tab, SecuritiesVolatilitySettings settings)
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
                LogicOpenPosition(candles, tab, settings);
            }
            else
            {
                LogicClosePosition(candles, tab, settings);
            }
        }

        // Opening logic
        private void LogicOpenPosition(List<Candle> candles, BotTabSimple tab, SecuritiesVolatilitySettings settings)
        {
            if (_tabScreener.PositionsOpenAll.Count >= _maxPositions.ValueInt)
            {
                return;
            }

            // Last candles
            decimal lastClose = candles[candles.Count - 1].Close;
            decimal lastOpen = candles[candles.Count - 1].Open;
            decimal lastHigh = candles[candles.Count - 1].High;
            decimal lastLow = candles[candles.Count - 1].Low;

            decimal lenCandlePercent = (lastHigh - lastLow) / (lastLow / 100);

            if (lenCandlePercent < settings.HeightPinBar)
            {
                return;
            }

            //  long
            if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
            {
                if (lastClose >= lastHigh - ((lastHigh - lastLow) / 3) && lastOpen >= lastHigh - ((lastHigh - lastLow) / 3))
                {
                    if (_smaFilterIsOn.ValueBool == true)
                    {
                        decimal smaValue = Sma(candles, _smaFilterLen.ValueInt, candles.Count - 1);
                        decimal smaPrev = Sma(candles, _smaFilterLen.ValueInt, candles.Count - 2);

                        if (smaValue < smaPrev)
                        {
                            return;
                        }
                    }

                    tab.BuyAtMarket(GetVolume(tab));
                }
            }

            // Short
            if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
            {
                if (lastClose <= lastLow + ((lastHigh - lastLow) / 3) && lastOpen <= lastLow + ((lastHigh - lastLow) / 3))
                {
                    if (_smaFilterIsOn.ValueBool == true)
                    {
                        decimal smaValue = Sma(candles, _smaFilterLen.ValueInt, candles.Count - 1);
                        decimal smaPrev = Sma(candles, _smaFilterLen.ValueInt, candles.Count - 2);

                        if (smaValue > smaPrev)
                        {
                            return;
                        }
                    }

                    tab.SellAtMarket(GetVolume(tab));
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles, BotTabSimple tab, SecuritiesVolatilitySettings settings)
        {
            decimal _lastPrice = candles[candles.Count - 1].Close;

            List<Position> openPositions = tab.PositionsOpenAll;
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is long
                {
                    decimal heightPattern = settings.HeightPinBar;
                    decimal priceStop = _lastPrice - _lastPrice * ((heightPattern * (_procHeightStop.ValueDecimal / 100))/100);

                    tab.CloseAtTrailingStopMarket(openPositions[i], priceStop);
                }
                else // If the direction of the position is short
                {
                    decimal heightPattern = settings.HeightPinBar;
                    decimal priceStop = _lastPrice + _lastPrice * ((heightPattern * (_procHeightStop.ValueDecimal / 100)) / 100);

                    tab.CloseAtTrailingStopMarket(openPositions[i], priceStop);
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

        // Method for calculating Sma
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

    // Storing volatility settings
    public class SecuritiesVolatilitySettings
    {
        public string SecName;

        public string SecClass;

        public decimal HeightPinBar;

        public DateTime LastUpdateTime;

        public string GetSaveString()
        {
            string result = "";

            result += SecName + "%";
            result += SecClass + "%";
            result += HeightPinBar + "%";
            result += LastUpdateTime.ToString(CultureInfo.InvariantCulture) + "%";

            return result;
        }

        public void LoadFromString(string str)
        {
            string[] array = str.Split('%');

            SecName = array[0];
            SecClass = array[1];
            HeightPinBar = array[2].ToDecimal();
            LastUpdateTime = Convert.ToDateTime(array[3], CultureInfo.InvariantCulture);
        }
    }
}