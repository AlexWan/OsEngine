/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

namespace OsEngine.Robots.FuturesTrend
{
    [Bot("FuturesTrendBollinger")]
    public class FuturesTrendBollinger : BotPanel
    {
        BotTabScreener _futuresSource;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _icebergCount;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _bollingerLength;
        private StrategyParameterDecimal _bollingerDeviation;

        // Trade periods
        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodsShowDialogButton;
        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        // Auto connection securities

        public FuturesTrendBollinger(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // non trade periods
            _tradePeriodsSettings = new NonTradePeriods(name);

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1Start = new TimeOfDay() { Hour = 0, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1End = new TimeOfDay() { Hour = 10, Minute = 05 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1OnOff = true;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2Start = new TimeOfDay() { Hour = 13, Minute = 54 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2End = new TimeOfDay() { Hour = 14, Minute = 6 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2OnOff = false;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3Start = new TimeOfDay() { Hour = 18, Minute = 30 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3End = new TimeOfDay() { Hour = 24, Minute = 00 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3OnOff = true;

            _tradePeriodsSettings.TradeInSunday = false;
            _tradePeriodsSettings.TradeInSaturday = false;

            _tradePeriodsSettings.Load();

            // Basic settings
            _regime = CreateParameter("Regime base", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort" }, "Base");

            _icebergCount = CreateParameter("Iceberg orders count", 1, 1, 3, 1, "Base");
            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods", "Base");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;

            _bollingerLength = CreateParameter("Bollinger Length", 50, 20, 300, 10, "Base");
            _bollingerDeviation = CreateParameter("Bollinger deviation", 1.9m, 0.5m, 4, 0.1m, "Base");

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Base");
            _volume = CreateParameter("Volume", 100, 1.0m, 50, 4, "Base");
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Base");

            // Source creation

            _futuresSource = TabCreate<BotTabScreener>();
            _futuresSource.CandleFinishedEvent += _futs1_CandleFinishedEvent;

            _futuresSource.CreateCandleIndicator(1, "Bollinger", new List<string>() {
                _bollingerLength.ValueInt.ToString(), _bollingerDeviation.ValueDecimal.ToString() }, "Prime");

            ParametrsChangeByUser += FuturesStartContangoScreener_ParametrsChangeByUser;

            Description = OsLocalization.ConvertToLocString(
              "Eng:Trend robot on Bollinger breakout, for futures trading. An example of moving futures from one series to another._" +
              "Ru:Трендовый робот на пробое боллинджера, для торговли фьючерсов. Пример перекладывания фьючерсов из одной серии в другую_");
        }

        private void FuturesStartContangoScreener_ParametrsChangeByUser()
        {
            _futuresSource._indicators[0].Parameters
             = new List<string>()
             {
                 _bollingerLength.ValueInt.ToString(),
                 _bollingerDeviation.ValueDecimal.ToString()
             };

            _futuresSource.UpdateIndicatorsParameters();
        }

        #region Logic Entry

        private void _futs1_CandleFinishedEvent(List<Candle> candles, BotTabSimple arg2)
        {
            TryEntryLogic(_futuresSource);
        }

        #endregion

        #region Logic

        private void TryEntryLogic(BotTabScreener futuresScreener)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            BotTabSimple futuresSource = GetFuturesToTrade(futuresScreener, futuresScreener.Tabs[0].Connector.MarketTime);

            if (futuresSource == null)
            {
                return;
            }

            List<Candle> futuresCandles = futuresSource.CandlesFinishedOnly;

            if (futuresCandles == null
                || futuresCandles.Count < 20)
            {
                return;
            }

            if (_tradePeriodsSettings.CanTradeThisTime(futuresCandles[^1].TimeStart) == false)
            {
                return;
            }

            if (futuresSource.IsConnected == false
                || futuresSource.IsReadyToTrade == false)
            {
                return;
            }

            List<Position> futuresPositions = futuresSource.PositionsOpenAll;

            if (futuresPositions.Count > 0)
            { // вход в логику закрытия позиции
                TryClosePositionLogic(futuresSource, futuresCandles, futuresPositions[0]);
            }
            else
            { // вход в логику открытия позиций
                TryOpenPositionLogic(futuresSource, futuresCandles);
            }
        }

        private BotTabSimple GetFuturesToTrade(BotTabScreener futures, DateTime currentTime)
        {
            /*
            Берём фьюч в пару:
            1) Если уже есть позиция
            2) Берём ближайшую пару фьюч / спот. 
            2.2) Если до ближайшего фьючерса меньше 5 дней до экспирации, не учитываем его как точку входа.
            2.3) Но не дальше чем 4 месяца, на случай если пропущена серия в тестере.
            */

            // 1 берём фьючерс, если по нему уже есть открытая позиция

            for (int i = 0; i < futures.Tabs.Count; i++)
            {
                BotTabSimple currentFutures = futures.Tabs[i];

                if (currentFutures.PositionsOpenAll.Count != 0)
                {
                    return currentFutures;
                }
            }

            // 2 теперь пробуем найти ближайший

            BotTabSimple selectedFutures = null;

            for (int i = 0; i < futures.Tabs.Count; i++)
            {
                Security sec = futures.Tabs[i].Security;

                if (sec == null)
                {
                    continue;
                }

                if (sec.Expiration == DateTime.MinValue)
                {
                    continue;
                }

                double daysByExpiration = (sec.Expiration - currentTime).TotalDays;

                if (daysByExpiration < 3
                    || daysByExpiration > 100)
                {
                    continue;
                }

                if (selectedFutures != null
                    && selectedFutures.Security.Expiration < sec.Expiration)
                {
                    continue;
                }

                selectedFutures = futures.Tabs[i];
            }

            return selectedFutures;
        }

        private void TryOpenPositionLogic(
            BotTabSimple futuresSource,
            List<Candle> futuresCandles)
        {
            // 1 берём по обоим вкладкам боллинджеры

            Aindicator futuresBollinger = (Aindicator)futuresSource.Indicators[0];

            if (futuresBollinger.DataSeries[0].Last == 0)
            {
                return;
            }

            decimal candleClose = futuresCandles[futuresCandles.Count - 1].Close;

            // 2 проверяем условия 

            decimal futuresLastPrice = futuresCandles[^1].Close;

            if (_regime.ValueString != "OnlyShort"
                 && futuresLastPrice > futuresBollinger.DataSeries[0].Last)   // фьючерс выше верхнего боллинджера
            {// Лонг
                futuresSource.BuyAtIcebergMarket(GetVolume(futuresSource), _icebergCount.ValueInt, 1000);
            }
            else if (_regime.ValueString != "OnlyLong"
                && futuresLastPrice < futuresBollinger.DataSeries[1].Last) // фьючерс ниже нижнего боллинджера
            {// Шорт
                futuresSource.SellAtIcebergMarket(GetVolume(futuresSource), _icebergCount.ValueInt, 1000);
            }
        }

        private void TryClosePositionLogic(
            BotTabSimple futuresSource,
            List<Candle> futuresCandles,
            Position pos)
        {
            /*
            Выход:
                   1) Фьючерс закрылся с обратной стороны боллинджера. Подключаемый
                   2) Выходим из позиции по фьючу, если до экспирации меньше или равно 2 торговых дня. По любой цене. 
            */

            if (StartProgram != StartProgram.IsOsTrader)
            {
                if (pos.State != PositionStateType.Open)
                {// в тестере и оптимизаторе не допускаем спама ордерами
                    return;
                }
            }

            Aindicator futuresBollinger = (Aindicator)futuresSource.Indicators[0];

            if (futuresBollinger.DataSeries[0].Last == 0)
            {
                return;
            }

            decimal futuresLastPrice = futuresCandles[^1].Close;

            bool needToExit = false;


            if (pos.Direction == Side.Buy
                && futuresLastPrice < futuresBollinger.DataSeries[1].Last)
            {
                needToExit = true;
            }

            if (pos.Direction == Side.Sell
                && futuresLastPrice > futuresBollinger.DataSeries[0].Last)
            {
                needToExit = true;
            }

            double daysByExpiration = (futuresSource.Security.Expiration - futuresCandles[^1].TimeStart).TotalDays;

            if (daysByExpiration < 3)
            {
                needToExit = true;
            }

            if (needToExit == true)
            {
                futuresSource.CloseAtIcebergMarket(pos, pos.OpenVolume, _icebergCount.ValueInt, 1000);
            }
        }

        #endregion

        #region Helpers

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

        #endregion

    }
}
