/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Wiki;

/* Description
Trading robot for osEngine

The robot-screener buys shares a few days before the dividend registry close date
and sells on the next day after the registry close.

Entry:
1. The security has future dividends within the next N days.
2. The candle closed above the SMA.
3. No open position on this instrument.
4. The total number of open positions is less than MaxPositions.

Exit:
- At the configured exit time on the next day after the dividend registry close date.

Entry and exit are executed via iceberg orders.
*/

namespace OsEngine.Robots.Dividends
{
    [Bot("DividendCaptureScreener")]
    public class DividendCaptureScreener : BotPanel
    {
        private BotTabScreener _screenerTab;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _daysBeforeRegistry;
        private StrategyParameterInt _smaPeriod;
        private StrategyParameterString _smaFilterIsOn;
        private StrategyParameterInt _maxPositions;
        private StrategyParameterInt _icebergCount;
        private StrategyParameterTimeOfDay _exitTime;
        private StrategyParameterButton _tradePeriodsShowDialogButton;

        // Non-trade periods
        private NonTradePeriods _tradePeriodsSettings;

        // Stores the registry close date for each ticker at the moment of entry.
        // Used for reliable exit after the registry close, because GetDividendsFuture
        // looks forward and returns the next dividend after the registry date.
        private Dictionary<string, DateTime> _entryRegistryDates = new Dictionary<string, DateTime>();

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        public DividendCaptureScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Non-trade periods
            _tradePeriodsSettings = new NonTradePeriods(name);

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1Start = new TimeOfDay() { Hour = 0, Minute = 0 };
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

            TabCreate(BotTabType.Screener);
            _screenerTab = TabsScreener[0];

            // Subscribe to the candle finished event
            _screenerTab.CandleFinishedEvent += _screenerTab_CandleFinishedEvent;

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _daysBeforeRegistry = CreateParameter("Days before registry", 5, 1, 60, 1);
            _smaPeriod = CreateParameter("SMA period", 50, 10, 300, 1);
            _smaFilterIsOn = CreateParameter("Filter sma is on", "On", new[] { "Off", "On" });
            _maxPositions = CreateParameter("Max positions", 5, 1, 50, 1);
            _icebergCount = CreateParameter("Iceberg orders count", 1, 1, 10, 1);
            _exitTime = CreateParameterTimeOfDay("Exit time", 10, 5, 0, 0);
            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Create indicator SMA
            _screenerTab.CreateCandleIndicator(1, "Sma", new List<string>() {
                _smaPeriod.ValueInt.ToString(),
                "Close"
            }, "Prime");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += DividendCaptureScreener_ParametrsChangeByUser;
            DeleteEvent += DividendCaptureScreener_DeleteEvent;

            string eng = "Screener for dividend capture strategy. Buys shares within N days before the dividend registry close if the price is above SMA (optional). Exits on the next day after the registry close at the configured time.";
            string ru = "Скринер для стратегии захвата дивидендов. Покупает акции за N дней до закрытия реестра дивидендов, если цена выше SMA (фильтр можно отключить). Выходит на следующий день после закрытия реестра в заданное время.";
            Description = OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
        }

        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        private void DividendCaptureScreener_DeleteEvent()
        {
            try
            {
                _tradePeriodsSettings.Delete();
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void DividendCaptureScreener_ParametrsChangeByUser()
        {
            _screenerTab._indicators[0].Parameters = new List<string>() {
                _smaPeriod.ValueInt.ToString(),
                "Close"
            };

            _screenerTab.UpdateIndicatorsParameters();
        }

        // Logic
        private void _screenerTab_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            try
            {
                if (_regime.ValueString == "Off")
                {
                    return;
                }

                if (candles.Count < _smaPeriod.ValueInt + 5)
                {
                    return;
                }

                Candle lastCandle = candles[candles.Count - 1];

                // Trading time control via non-trade periods
                if (_tradePeriodsSettings.CanTradeThisTime(lastCandle.TimeStart) == false)
                {
                    return;
                }

                Aindicator sma = (Aindicator)tab.Indicators[0];

                if (sma.DataSeries[0].Values.Count < 2)
                {
                    return;
                }

                decimal smaValue = sma.DataSeries[0].Values[sma.DataSeries[0].Values.Count - 1];

                if (smaValue == 0)
                {
                    return;
                }

                List<Position> positions = tab.PositionsOpenAll;

                if (positions.Count == 0)
                { // opening logic
                    int allPositionsCount = this.PositionsCount;

                    if (allPositionsCount >= _maxPositions.ValueInt)
                    {
                        return;
                    }

                    if (_smaFilterIsOn.ValueString == "On"
                        && lastCandle.Close <= smaValue)
                    {
                        return;
                    }

                    if (!HasDividendsBeforeRegistry(tab, lastCandle.TimeStart))
                    {
                        return;
                    }

                    tab.BuyAtIcebergMarket(GetVolume(tab), _icebergCount.ValueInt, 1000);
                }
                else // close logic
                {
                    Position pos = positions[0];

                    if (pos.State != PositionStateType.Open)
                    {
                        return;
                    }

                    if (!ShouldExitAfterDividends(tab, lastCandle.TimeStart, pos))
                    {
                        return;
                    }

                    tab.CloseAtIcebergMarket(pos, pos.OpenVolume, _icebergCount.ValueInt, 1000);
                }
            }
            catch (Exception error)
            {
                string message = $"DividendCaptureScreener logic error for {tab.Security?.Name}: {error}";
                SendNewLogMessage(message, LogMessageType.Error);
            }
        }

        /// <summary>
        /// Checks whether the security has future dividends within the configured window
        /// before the registry close date.
        /// </summary>
        private bool HasDividendsBeforeRegistry(BotTabSimple tab, DateTime referenceDate)
        {
            try
            {
                string ticker = tab.Security?.Name;

                if (string.IsNullOrWhiteSpace(ticker))
                {
                    return false;
                }

                WikiDividendFuture dividendFuture = WikiMaster.GetDividendsFuture(ticker, referenceDate);

                if (dividendFuture?.future == null
                    || string.IsNullOrWhiteSpace(dividendFuture.future.registry_close_date))
                {
                    return false;
                }

                if (!DateTime.TryParseExact(dividendFuture.future.registry_close_date, "dd.MM.yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime registryDate))
                {
                    return false;
                }

                _entryRegistryDates[ticker] = registryDate;

                DateTime maxDate = referenceDate.Date.AddDays(_daysBeforeRegistry.ValueInt);

                return registryDate.Date > referenceDate.Date
                    && registryDate.Date <= maxDate;
            }
            catch (Exception error)
            {
                SendNewLogMessage($"DividendCaptureScreener dividend filter error: {error}", LogMessageType.Error);
                return false;
            }
        }

        /// <summary>
        /// Checks whether it is time to exit the position: the next day after the dividend registry close,
        /// at or after the configured exit time.
        /// </summary>
        private bool ShouldExitAfterDividends(BotTabSimple tab, DateTime currentTime, Position position)
        {
            try
            {
                string ticker = tab.Security?.Name;

                if (string.IsNullOrWhiteSpace(ticker))
                {
                    return false;
                }

                DateTime registryDate;

                if (!_entryRegistryDates.TryGetValue(ticker, out registryDate))
                {
                    // Fallback for positions opened before the robot was loaded (e.g. after restart).
                    // Try the most recent past dividend relative to the position open time.
                    WikiDividendPast dividendPast = WikiMaster.GetDividendsPast(ticker, position.TimeOpen);

                    if (dividendPast?.past == null
                        || string.IsNullOrWhiteSpace(dividendPast.past.registry_close_date)
                        || !DateTime.TryParseExact(dividendPast.past.registry_close_date, "dd.MM.yyyy",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out registryDate))
                    {
                        return false;
                    }

                    _entryRegistryDates[ticker] = registryDate;
                }

                DateTime earliestExitDate = registryDate.Date.AddDays(1);

                if (currentTime.Date < earliestExitDate)
                {
                    return false;
                }

                TimeSpan exitTime = new TimeSpan(_exitTime.Value.Hour, _exitTime.Value.Minute, _exitTime.Value.Second);

                return currentTime.TimeOfDay >= exitTime;
            }
            catch (Exception error)
            {
                SendNewLogMessage($"DividendCaptureScreener exit filter error: {error}", LogMessageType.Error);
                return false;
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
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, LogMessageType.Error);
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
