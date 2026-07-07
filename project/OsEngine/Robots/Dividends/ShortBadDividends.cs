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

The robot-screener on Adaptive Price Channel with a "bad dividends" filter.

The robot trades only Short through a BotTabScreener.

Short entry:
1. The candle closed below the lower line of the Adaptive Price Channel.
2. The security paid dividends in the last N days (filter via WikiMaster.GetDividendsPast).
3. The dividend yield of the nearest past payment is less than the configured threshold (M %).
4. The total number of open positions in the screener is less than MaxPositions.

Exit for short: When the Adaptive Price Channel upper line is broken.

Entry and exit are executed via iceberg orders (Iceberg orders count).

Non-trade periods are configurable through the robot parameters (Non trade periods button).
Default non-trade periods: 00:00-10:05 and 18:01-23:58, weekends are disabled.

Indicators:
- Adaptive Price Channel (ADX period, Ratio).
*/

namespace OsEngine.Robots.Dividends
{
    [Bot("ShortBadDividends")]
    public class ShortBadDividends : BotPanel
    {
        private BotTabScreener _screenerTab;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _pcAdxLength;
        private StrategyParameterInt _pcRatio;
        private StrategyParameterInt _lookbackDays;
        private StrategyParameterDecimal _maxDividendYieldPercent;
        private StrategyParameterInt _maxPositions;
        private StrategyParameterInt _icebergCount;
        private StrategyParameterButton _tradePeriodsShowDialogButton;

        // Non-trade periods
        private NonTradePeriods _tradePeriodsSettings;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        public ShortBadDividends(string name, StartProgram startProgram) : base(name, startProgram)
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
            _pcAdxLength = CreateParameter("PC ADX period", 50, 5, 300, 1);
            _pcRatio = CreateParameter("PC ratio", 840, 5, 2000, 1);
            _lookbackDays = CreateParameter("Lookback days", 20, 1, 500, 1);
            _maxDividendYieldPercent = CreateParameter("Max dividend yield %", 2.0m, 0.1m, 50.0m, 0.1m);
            _maxPositions = CreateParameter("Max positions", 5, 1, 50, 1);
            _icebergCount = CreateParameter("Iceberg orders count", 1, 1, 10, 1);
            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Create indicator Adaptive Price Channel
            _screenerTab.CreateCandleIndicator(1, "PriceChannelAdaptive", new List<string>() {
                _pcAdxLength.ValueInt.ToString(),
                _pcRatio.ValueInt.ToString()
            }, "Prime");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += ShortBadDividends_ParametrsChangeByUser;
            DeleteEvent += ShortBadDividends_DeleteEvent;

            string eng = "Adaptive Price Channel screener with a bad-dividend filter. Trades only Short. Entry below the lower channel line, exit above the upper line. The dividend filter checks the nearest past dividend within the last N days and requires its yield to be below the maximum threshold.";
            string ru = "Скринер на адаптивном ценовом канале с фильтром слабых дивидендов. Только Short. Вход ниже нижней линии канала, выход выше верхней. Дивидендный фильтр проверяет ближайшие прошлые выплаты за последние N дней и требует, чтобы их доходность была ниже максимального порога.";
            Description = OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
        }

        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        private void ShortBadDividends_DeleteEvent()
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

        private void ShortBadDividends_ParametrsChangeByUser()
        {
            _screenerTab._indicators[0].Parameters = new List<string>() {
                _pcAdxLength.ValueInt.ToString(),
                _pcRatio.ValueInt.ToString()
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

                if (candles.Count < _pcAdxLength.ValueInt * 2 + 5)
                {
                    return;
                }

                Candle lastCandle = candles[candles.Count - 1];

                // Trading time control via non-trade periods
                if (_tradePeriodsSettings.CanTradeThisTime(lastCandle.TimeStart) == false)
                {
                    return;
                }

                Aindicator priceChannel = (Aindicator)tab.Indicators[0];

                if (priceChannel.DataSeries[0].Values.Count < 2
                    || priceChannel.DataSeries[1].Values.Count < 2)
                {
                    return;
                }

                // For channel indicators (PriceChannel, Donchian, etc.) the last value
                // is redrawn by the current candle, so the breakout must be checked
                // against the previous closed candle value (Count - 2).
                decimal upChannel = priceChannel.DataSeries[0].Values[priceChannel.DataSeries[0].Values.Count - 2];
                decimal downChannel = priceChannel.DataSeries[1].Values[priceChannel.DataSeries[1].Values.Count - 2];

                List<Position> positions = tab.PositionsOpenAll;

                if (positions.Count == 0)
                { // opening logic
                    int allPositionsCount = this.PositionsCount;

                    if (allPositionsCount >= _maxPositions.ValueInt)
                    {
                        return;
                    }

                    if (lastCandle.Close >= downChannel)
                    {
                        return;
                    }

                    if (!IsDividendFilterPassed(tab, lastCandle.TimeStart))
                    {
                        return;
                    }

                    tab.SellAtIcebergMarket(GetVolume(tab), _icebergCount.ValueInt, 1000);
                }
                else // close logic
                {
                    Position pos = positions[0];

                    if (pos.State != PositionStateType.Open)
                    {
                        return;
                    }

                    if (lastCandle.Close > upChannel)
                    {
                        tab.CloseAtIcebergMarket(pos, pos.OpenVolume, _icebergCount.ValueInt, 1000);
                    }
                }
            }
            catch (Exception error)
            {
                string message = $"ShortBadDividends logic error for {tab.Security?.Name}: {error}";
                SendNewLogMessage(message, LogMessageType.Error);
            }
        }

        /// <summary>
        /// Checks whether the security satisfies the dividend filter for short trades.
        /// The filter requires the nearest past dividend record to be within the lookback window
        /// and its yield to be below the configured maximum threshold.
        /// </summary>
        /// <param name="tab">The instrument tab used to resolve the ticker.</param>
        /// <param name="referenceDate">The date relative to which the lookback window is calculated.</param>
        /// <returns>true if the security passed the dividend filter; otherwise, false.</returns>
        private bool IsDividendFilterPassed(BotTabSimple tab, DateTime referenceDate)
        {
            try
            {
                string ticker = tab.Security?.Name;

                if (string.IsNullOrWhiteSpace(ticker))
                {
                    return false;
                }

                WikiDividendPast dividendPast = WikiMaster.GetDividendsPast(ticker, referenceDate);

                if (dividendPast == null
                    || dividendPast.past == null
                    || string.IsNullOrWhiteSpace(dividendPast.past.registry_close_date))
                {
                    return false;
                }

                if (!DateTime.TryParseExact(dividendPast.past.registry_close_date, "dd.MM.yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime recordDate))
                {
                    return false;
                }

                DateTime minDate = referenceDate.AddDays(-_lookbackDays.ValueInt);

                if (recordDate < minDate)
                {
                    return false;
                }

                return dividendPast.past.dividend_yield < _maxDividendYieldPercent.ValueDecimal;
            }
            catch (Exception error)
            {
                SendNewLogMessage($"ShortBadDividends dividend filter error: {error}", LogMessageType.Error);
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
