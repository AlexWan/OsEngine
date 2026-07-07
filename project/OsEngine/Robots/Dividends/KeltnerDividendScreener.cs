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

The trend robot-screener on Keltner Channel with dividend filter.

The robot trades only Long through a BotTabScreener.

Buy:
1. The candle closed above the upper line of the Keltner Channel.
2. The security paid dividends in the last N days (filter via WikiMaster.GetDividendsPast).
3. The total number of open positions in the screener is less than MaxPositions.

Exit for long: When the Keltner Channel bottom line is broken.

Entry and exit are executed via iceberg orders (Iceberg orders count).

Non-trade periods are configurable through the robot parameters (Non trade periods button).
Default non-trade periods: 00:00-10:05 and 18:01-23:58, weekends are disabled.

Indicators:
- Keltner Channel (EMA period, ATR period, ATR EMA period, multiplier, Close price).
*/

namespace OsEngine.Robots.Dividends
{
    [Bot("KeltnerDividendScreener")]
    public class KeltnerDividendScreener : BotPanel
    {
        private BotTabScreener _screenerTab;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _keltnerEmaPeriod;
        private StrategyParameterInt _keltnerAtrPeriod;
        private StrategyParameterDecimal _keltnerMultiplier;
        private StrategyParameterInt _lookbackDays;
        private StrategyParameterDecimal _minDividendYieldPercent;
        private StrategyParameterInt _maxPositions;
        private StrategyParameterInt _icebergCount;
        private StrategyParameterButton _tradePeriodsShowDialogButton;

        // Non-trade periods
        private NonTradePeriods _tradePeriodsSettings;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        public KeltnerDividendScreener(string name, StartProgram startProgram) : base(name, startProgram)
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
            _keltnerEmaPeriod = CreateParameter("Keltner EMA period", 300, 5, 300, 1);
            _keltnerAtrPeriod = CreateParameter("Keltner ATR period", 20, 5, 100, 1);
            _keltnerMultiplier = CreateParameter("Keltner multiplier", 4.0m, 0.5m, 5.0m, 0.1m);
            _lookbackDays = CreateParameter("Lookback days", 30, 1, 500, 1);
            _minDividendYieldPercent = CreateParameter("Min dividend yield %", 2.0m, 0.1m, 50.0m, 0.1m);
            _maxPositions = CreateParameter("Max positions", 5, 1, 50, 1);
            _icebergCount = CreateParameter("Iceberg orders count", 1, 1, 10, 1);
            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Create indicator Keltner Channel
            _screenerTab.CreateCandleIndicator(1, "KeltnerChannel", new List<string>() {
                _keltnerEmaPeriod.ValueInt.ToString(),
                _keltnerAtrPeriod.ValueInt.ToString(),
                _keltnerAtrPeriod.ValueInt.ToString(),
                _keltnerMultiplier.ValueDecimal.ToString(),
                "Close"
            }, "Prime");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += KeltnerDividendScreener_ParametrsChangeByUser;
            DeleteEvent += KeltnerDividendScreener_DeleteEvent;

            string eng = "Keltner Channel screener with dividend filter. Trades only Long. Entry above the upper Keltner line, exit below the lower line. The dividend filter checks the nearest past dividend within the last N days and requires its yield to exceed the minimum threshold.";
            string ru = "Скринер на канале Кельтнера с дивидендным фильтром. Только Long. Вход выше верхней линии Кельтнера, выход ниже нижней. Дивидендный фильтр проверяет ближайшие прошлые выплаты за последние N дней и требует, чтобы их доходность превышала минимальный порог.";
            Description = OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
        }

        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        private void KeltnerDividendScreener_DeleteEvent()
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

        private void KeltnerDividendScreener_ParametrsChangeByUser()
        {
            _screenerTab._indicators[0].Parameters = new List<string>() {
                _keltnerEmaPeriod.ValueInt.ToString(),
                _keltnerAtrPeriod.ValueInt.ToString(),
                _keltnerAtrPeriod.ValueInt.ToString(),
                _keltnerMultiplier.ValueDecimal.ToString(),
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

                if (candles.Count < _keltnerEmaPeriod.ValueInt + _keltnerAtrPeriod.ValueInt + 5)
                {
                    return;
                }

                Candle lastCandle = candles[candles.Count - 1];

                // Trading time control via non-trade periods
                if (_tradePeriodsSettings.CanTradeThisTime(lastCandle.TimeStart) == false)
                {
                    return;
                }

                Aindicator keltner = (Aindicator)tab.Indicators[0];

                if (keltner.DataSeries[1].Last == 0
                    || keltner.DataSeries[2].Last == 0)
                {
                    return;
                }

                decimal upChannel = keltner.DataSeries[1].Values[keltner.DataSeries[1].Values.Count - 1];
                decimal downChannel = keltner.DataSeries[2].Values[keltner.DataSeries[2].Values.Count - 1];

                List<Position> positions = tab.PositionsOpenAll;

                if (positions.Count == 0)
                { // opening logic
                    int allPositionsCount = this.PositionsCount;

                    if (allPositionsCount >= _maxPositions.ValueInt)
                    {
                        return;
                    }

                    if (lastCandle.Close <= upChannel)
                    {
                        return;
                    }

                    if (!IsDividendFilterPassed(tab, lastCandle.TimeStart))
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

                    if (lastCandle.Close < downChannel)
                    {
                        tab.CloseAtIcebergMarket(pos, pos.OpenVolume, _icebergCount.ValueInt, 1000);
                    }
                }
            }
            catch (Exception error)
            {
                string message = $"KeltnerDividendScreener logic error for {tab.Security?.Name}: {error}";
                SendNewLogMessage(message, LogMessageType.Error);
            }
        }

        /// <summary>
        /// Checks whether the security satisfies the dividend filter.
        /// The filter requires the nearest past dividend record to be within the lookback window
        /// and its yield to exceed the configured minimum threshold.
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

                return dividendPast.past.dividend_yield > _minDividendYieldPercent.ValueDecimal;
            }
            catch (Exception error)
            {
                SendNewLogMessage($"KeltnerDividendScreener dividend filter error: {error}", LogMessageType.Error);
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
