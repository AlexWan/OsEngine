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
using System.IO;
using System.Globalization;
using OsEngine.Language;

/* Description
Trading robot-screener for osEngine

The trend robot on three growing candles that must be of a certain size to the current volatility and Volatility group.

Buy:
1. When we see three growing candles of a certain size to the current volatility.
2. Filter by volatility groups. All screener papers are divided into 3 groups. One of them is traded.

Exit for long: Stop and Profit regarding volatility

*/

namespace OsEngine.Robots.AlgoStart
{
    [Bot("AlgoStart2Soldiers")]
    public class AlgoStart2Soldiers : BotPanel
    {
        private BotTabScreener _tabScreener;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _icebergCount;
        private StrategyParameterInt _maxPositions;
        private StrategyParameterInt _clusterToTrade;
        private StrategyParameterInt _clustersLookBack;
        private StrategyParameterDecimal _procHeightTake;
        private StrategyParameterDecimal _procHeightStop;

        // Basic settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Volatility settings
        private StrategyParameterInt _daysVolatilityAdaptive;
        private StrategyParameterDecimal _heightSoldiersVolaPercent;

        // SmaFilter settings
        private StrategyParameterBool _smaFilterIsOn;
        private StrategyParameterInt _smaFilterLen;

        // Trade periods
        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodsShowDialogButton;

        public AlgoStart2Soldiers(string name, StartProgram startProgram) : base(name, startProgram)
        {

            // non trade periods
            _tradePeriodsSettings = new NonTradePeriods(name);

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1Start = new TimeOfDay() { Hour = 5, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1End = new TimeOfDay() { Hour = 10, Minute = 05 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1OnOff = true;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2Start = new TimeOfDay() { Hour = 13, Minute = 54 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2End = new TimeOfDay() { Hour = 14, Minute = 6 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2OnOff = false;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3Start = new TimeOfDay() { Hour = 18, Minute = 1 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3End = new TimeOfDay() { Hour = 23, Minute = 58 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3OnOff = true;

            _tradePeriodsSettings.TradeInSunday = false;
            _tradePeriodsSettings.TradeInSaturday = false;

            _tradePeriodsSettings.Load();

            // Source creation

            TabCreate(BotTabType.Screener);
            _tabScreener = TabsScreener[0];

            // Subscribe to the candle finished event
            _tabScreener.CandleFinishedEvent += _tab_CandleFinishedEvent1;

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On"});
            _icebergCount = CreateParameter("Iceberg orders count", 1, 1, 3, 1);
            _maxPositions = CreateParameter("Max positions", 10, 0, 20, 1);
            _clusterToTrade = CreateParameter("Volatility cluster to trade", 3, 1, 3, 1);
            _clustersLookBack = CreateParameter("Volatility cluster lookBack", 80, 10, 300, 1);

            _procHeightTake = CreateParameter("Profit % from height of pattern", 185m, 0, 20, 1m);
            _procHeightStop = CreateParameter("Stop % from height of pattern", 106m, 0, 20, 1m);
            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 10, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Volatility settings
            _daysVolatilityAdaptive = CreateParameter("Days volatility adaptive", 7, 0, 20, 1);
            _heightSoldiersVolaPercent = CreateParameter("Height soldiers volatility percent", 80, 0, 20, 1m);

            // SmaFilter settings
            _smaFilterIsOn = CreateParameter("Sma filter is on", true);
            _smaFilterLen = CreateParameter("Sma filter Len", 150, 10, 300, 10);

            if (startProgram == StartProgram.IsOsTrader)
            {
                LoadTradeSettings();
            }

            Description = OsLocalization.Description.DescriptionLabel325;
            DeleteEvent += AlgoStart2ScreenerSoldiers_DeleteEvent;
        }

        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        // Volatility adaptation

        private VolatilityStageClusters _volatilityStageClusters = new VolatilityStageClusters();
        private DateTime _lastTimeSetClusters;
        private List<SecuritiesTradeSettings> _tradeSettings = new List<SecuritiesTradeSettings>();

        // save settings in .txt file
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

        // Load settins from .txt file
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

                        SecuritiesTradeSettings newSettings = new SecuritiesTradeSettings();
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

        // Delete save file
        private void AlgoStart2ScreenerSoldiers_DeleteEvent()
        {
            try
            {
                _tradePeriodsSettings.Delete();

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

        private void AdaptSoldiersHeight(List<Candle> candles, SecuritiesTradeSettings settings)
        {
            if (_daysVolatilityAdaptive.ValueInt <= 0
                || _heightSoldiersVolaPercent.ValueDecimal <= 0)
            {
                return;
            }

            // 1 we calculate the movement from high to low within N days

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

            // 2 we average this movement. We need average volatility percentage

            decimal volaPercentSma = 0;

            for (int i = 0; i < volaInDaysPercent.Count; i++)
            {
                volaPercentSma += volaInDaysPercent[i];
            }

            volaPercentSma = volaPercentSma / volaInDaysPercent.Count;

            // 3 we calculate the size of the candles taking this volatility into account

            decimal allSoldiersHeight = volaPercentSma * (_heightSoldiersVolaPercent.ValueDecimal / 100);

            settings.HeightSoldiers = allSoldiersHeight;
            settings.LastUpdateTime = candles[candles.Count - 1].TimeStart;
        }

        // logic

        private void _tab_CandleFinishedEvent1(List<Candle> candles, BotTabSimple tab)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 50)
            {
                return;
            }

            if (_tradePeriodsSettings.CanTradeThisTime(candles[^1].TimeStart) == false)
            {
                return;
            }

            List<Position> openPositions = tab.PositionsOpenAll;

            if (openPositions.Count == 0
                && _clusterToTrade.ValueInt != 0)
            {
                if (_lastTimeSetClusters == DateTime.MinValue
                 || _lastTimeSetClusters != candles[^1].TimeStart)
                {
                    _volatilityStageClusters.Calculate(_tabScreener.Tabs, _clustersLookBack.ValueInt);
                    _lastTimeSetClusters = candles[^1].TimeStart;
                }

                if (_clusterToTrade.ValueInt == 1)
                {
                    if (_volatilityStageClusters.ClusterOne.Find(source => source.Connector.SecurityName == tab.Connector.SecurityName) == null)
                    {
                        return;
                    }
                }
                else if (_clusterToTrade.ValueInt == 2)
                {
                    if (_volatilityStageClusters.ClusterTwo.Find(source => source.Connector.SecurityName == tab.Connector.SecurityName) == null)
                    {
                        return;
                    }
                }
                else if (_clusterToTrade.ValueInt == 3)
                {
                    if (_volatilityStageClusters.ClusterThree.Find(source => source.Connector.SecurityName == tab.Connector.SecurityName) == null)
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            SecuritiesTradeSettings mySettings = null;

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
                mySettings = new SecuritiesTradeSettings();
                mySettings.SecName = tab.Security.Name;
                mySettings.SecClass = tab.Security.NameClass;
                _tradeSettings.Add(mySettings);
            }

            if (mySettings.LastUpdateTime.Date != candles[candles.Count - 1].TimeStart.Date)
            {
                AdaptSoldiersHeight(candles, mySettings);

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    SaveTradeSettings();
                }
            }

            if (mySettings.HeightSoldiers == 0)
            {
                return;
            }

            Logic(candles, tab, mySettings);
        }

        // Logic
        private void Logic(List<Candle> candles, BotTabSimple tab, SecuritiesTradeSettings settings)
        {
            if (candles.Count < 5)
            {
                return;
            }

            List<Position> openPositions = tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, tab, settings);
            }
            else
            {
                LogicClosePosition(candles, tab);
            }
        }

        // Opening position logic
        private void LogicOpenPosition(List<Candle> candles, BotTabSimple tab, SecuritiesTradeSettings settings)
        {
            if (_tabScreener.PositionsOpenAll.Count >= _maxPositions.ValueInt)
            {
                return;
            }

            decimal _lastPrice = candles[candles.Count - 1].Close;

            if (Math.Abs(candles[candles.Count - 3].Open - candles[candles.Count - 1].Close)
                / (candles[candles.Count - 1].Close / 100) < settings.HeightSoldiers)
            {
                return;
            }

            //  long

            if (candles[candles.Count - 3].Open < candles[candles.Count - 3].Close
                && candles[candles.Count - 2].Open < candles[candles.Count - 2].Close
                && candles[candles.Count - 1].Open < candles[candles.Count - 1].Close)
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

                tab.BuyAtIcebergMarket(GetVolume(tab), _icebergCount.ValueInt, 1000);
            }

            return;
        }

        // Close position logic
        private void LogicClosePosition(List<Candle> candles, BotTabSimple tab)
        {
            decimal _lastPrice = candles[candles.Count - 1].Close;

            List<Position> openPositions = tab.PositionsOpenAll;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                decimal heightPattern =
                    Math.Abs(tab.CandlesAll[tab.CandlesAll.Count - 4].Open - tab.CandlesAll[tab.CandlesAll.Count - 2].Close);

                decimal priceStop = _lastPrice - (heightPattern * _procHeightStop.ValueDecimal) / 100;
                decimal priceTake = _lastPrice + (heightPattern * _procHeightTake.ValueDecimal) / 100;

                if(pos.StopOrderPrice == 0)
                {
                    pos.StopOrderPrice = priceStop;
                }
                if(pos.ProfitOrderPrice == 0)
                {
                    pos.ProfitOrderPrice = priceTake;
                    
                    if(StartProgram == StartProgram.IsOsTrader)
                    {
                        tab._journal.Save();
                    }
                }

                decimal lastClose = candles[^1].Close;

                if (lastClose <= pos.StopOrderPrice)
                {
                    tab.CloseAtIcebergMarket(pos, pos.OpenVolume, _icebergCount.ValueInt, 1000);
                }
                if (lastClose >= pos.ProfitOrderPrice)
                {
                    tab.CloseAtIcebergMarket(pos, pos.OpenVolume, _icebergCount.ValueInt, 1000);
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

        // Method for calculating MovingAverage
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

    public class SecuritiesTradeSettings
    {
        public string SecName;

        public string SecClass;

        public decimal HeightSoldiers;

        public DateTime LastUpdateTime;

        public string GetSaveString()
        {
            string result = "";

            result += SecName + "%";
            result += SecClass + "%";
            result += HeightSoldiers + "%";
            result += LastUpdateTime.ToString(CultureInfo.InvariantCulture) + "%";

            return result;
        }

        public void LoadFromString(string str)
        {
            string[] array = str.Split('%');

            SecName = array[0];
            SecClass = array[1];
            HeightSoldiers = array[2].ToDecimal();
            LastUpdateTime = Convert.ToDateTime(array[3], CultureInfo.InvariantCulture);
        }
    }
}