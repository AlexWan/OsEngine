/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Candles.Series;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

namespace OsEngine.Robots.FuturesScreeners
{
    [Bot("FuturesScreenerLrSma")]
    public class FuturesScreenerLrSma : BotPanel
    {
        BotTabScreener _futs1;
        BotTabScreener _futs2;
        BotTabScreener _futs3;
        BotTabScreener _futs4;
        BotTabScreener _futs5;
        BotTabScreener _futs6;
        BotTabScreener _futs7;
        BotTabScreener _futs8;
        BotTabScreener _futs9;
        BotTabScreener _futs10;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _icebergCount;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _lrLength;
        private StrategyParameterDecimal _lrDeviation;
        private StrategyParameterBool _smaFilterIsOn;
        private StrategyParameterInt _smaFilterLen;

        private StrategyParameterBool _tradeRegimeSecurity1;
        private StrategyParameterBool _tradeRegimeSecurity2;
        private StrategyParameterBool _tradeRegimeSecurity3;
        private StrategyParameterBool _tradeRegimeSecurity4;
        private StrategyParameterBool _tradeRegimeSecurity5;
        private StrategyParameterBool _tradeRegimeSecurity6;
        private StrategyParameterBool _tradeRegimeSecurity7;
        private StrategyParameterBool _tradeRegimeSecurity8;
        private StrategyParameterBool _tradeRegimeSecurity9;
        private StrategyParameterBool _tradeRegimeSecurity10;

        private bool CanTradeThisSecurity(string securityName)
        {
            if (_futs1.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity1.ValueBool;
            }
            if (_futs2.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity2.ValueBool;
            }
            if (_futs3.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity3.ValueBool;
            }
            if (_futs4.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity4.ValueBool;
            }
            if (_futs5.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity5.ValueBool;
            }
            if (_futs6.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity6.ValueBool;
            }
            if (_futs7.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity7.ValueBool;
            }
            if (_futs8.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity8.ValueBool;
            }
            if (_futs9.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity9.ValueBool;
            }
            if (_futs10.Tabs.Find(t => t.Connector.SecurityName == securityName) != null)
            {
                return _tradeRegimeSecurity10.ValueBool;
            }

            return false;
        }

        // Trade periods
        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodsShowDialogButton;
        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        // Auto connection securities

        private StrategyParameterString _portfolioNum;

        public FuturesScreenerLrSma(string name, StartProgram startProgram) : base(name, startProgram)
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

            _lrLength = CreateParameter("Linear regression Length", 180, 20, 300, 10, "Base");
            _lrDeviation = CreateParameter("Linear regression deviation", 2.4m, 1, 4, 0.1m, "Base");
            _smaFilterIsOn = CreateParameter("Sma filter is on", true, "Base");
            _smaFilterLen = CreateParameter("Sma filter Len", 270, 100, 300, 10, "Base");

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Base");
            _volume = CreateParameter("Volume", 15, 1.0m, 50, 4, "Base");
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Base");

            _tradeRegimeSecurity1 = CreateParameter("Trade security 1", true, "Trade securities");
            _tradeRegimeSecurity2 = CreateParameter("Trade security 2", true, "Trade securities");
            _tradeRegimeSecurity3 = CreateParameter("Trade security 3", true, "Trade securities");
            _tradeRegimeSecurity4 = CreateParameter("Trade security 4", true, "Trade securities");
            _tradeRegimeSecurity5 = CreateParameter("Trade security 5", true, "Trade securities");
            _tradeRegimeSecurity6 = CreateParameter("Trade security 6", true, "Trade securities");
            _tradeRegimeSecurity7 = CreateParameter("Trade security 7", true, "Trade securities");
            _tradeRegimeSecurity8 = CreateParameter("Trade security 8", true, "Trade securities");
            _tradeRegimeSecurity9 = CreateParameter("Trade security 9", true, "Trade securities");
            _tradeRegimeSecurity10 = CreateParameter("Trade security 10", true, "Trade securities");

            // Auto Securities

            if (startProgram == StartProgram.IsOsTrader)
            {
                _portfolioNum = CreateParameter("Portfolio number", "", "Auto deploy");
                StrategyParameterButton buttonAutoDeploy = CreateParameterButton("Deploy standard securities", "Auto deploy");
                buttonAutoDeploy.UserClickOnButtonEvent += ButtonAutoDeploy_UserClickOnButtonEvent;
            }

            // Source creation

            _futs1 = TabCreate<BotTabScreener>();
            _futs1.CandleFinishedEvent += _futs1_CandleFinishedEvent;
            CreateIndicators(_futs1);

            _futs2 = TabCreate<BotTabScreener>();
            _futs2.CandleFinishedEvent += _futs2_CandleFinishedEvent;
            CreateIndicators(_futs2);

            _futs3 = TabCreate<BotTabScreener>();
            _futs3.CandleFinishedEvent += _futs3_CandleFinishedEvent;
            CreateIndicators(_futs3);

            _futs4 = TabCreate<BotTabScreener>();
            _futs4.CandleFinishedEvent += _futs4_CandleFinishedEvent;
            CreateIndicators(_futs4);

            _futs5 = TabCreate<BotTabScreener>();
            _futs5.CandleFinishedEvent += _futs5_CandleFinishedEvent;
            CreateIndicators(_futs5);

            _futs6 = TabCreate<BotTabScreener>();
            _futs6.CandleFinishedEvent += _futs6_CandleFinishedEvent;
            CreateIndicators(_futs6);

            _futs7 = TabCreate<BotTabScreener>();
            _futs7.CandleFinishedEvent += _futs7_CandleFinishedEvent;
            CreateIndicators(_futs7);

            _futs8 = TabCreate<BotTabScreener>();
            _futs8.CandleFinishedEvent += _futs8_CandleFinishedEvent;
            CreateIndicators(_futs8);

            _futs9 = TabCreate<BotTabScreener>();
            _futs9.CandleFinishedEvent += _futs9_CandleFinishedEvent;
            CreateIndicators(_futs9);

            _futs10 = TabCreate<BotTabScreener>();
            _futs10.CandleFinishedEvent += _futs10_CandleFinishedEvent;
            CreateIndicators(_futs10);


            ParametrsChangeByUser += FuturesStartContangoScreener_ParametrsChangeByUser;

            Description = OsLocalization.ConvertToLocString(
              "Eng:Trend screener for futures based on the breakout of the linear regression channel with a filter by the long moving average. An example of moving futures from one series to another._" +
              "Ru:Трендовый скринер фьючерсов на пробое канала линейной регрессии с фильтром по длинной скользящей средней. Пример перекладывания фьючерсов из одной серии в другую_");
        }

        private void FuturesStartContangoScreener_ParametrsChangeByUser()
        {
            UpdateSettingsInIndicators(_futs1);
            UpdateSettingsInIndicators(_futs2);
            UpdateSettingsInIndicators(_futs3);
            UpdateSettingsInIndicators(_futs4);
            UpdateSettingsInIndicators(_futs5);
            UpdateSettingsInIndicators(_futs6);
            UpdateSettingsInIndicators(_futs7);
            UpdateSettingsInIndicators(_futs8);
            UpdateSettingsInIndicators(_futs9);
            UpdateSettingsInIndicators(_futs10);
        }

        private void CreateIndicators(BotTabScreener futuresSource)
        {
            futuresSource.CreateCandleIndicator(1,
                "LinearRegressionChannelFast_Indicator",
                new List<string>() {
                    _lrLength.ValueInt.ToString(), "Close",
                    _lrDeviation.ValueDecimal.ToString(),
                    _lrDeviation.ValueDecimal.ToString()
                }, "Prime");

            futuresSource.CreateCandleIndicator(2, 
                "Sma", new List<string>() 
                { _smaFilterLen.ValueInt.ToString(), 
                    "Close" }, 
                "Prime");
        }

        private void UpdateSettingsInIndicators(BotTabScreener futuresSource)
        {
            futuresSource._indicators[0].Parameters
              = new List<string>()
             {
                 _lrLength.ValueInt.ToString(),
                 "Close",
                 _lrDeviation.ValueDecimal.ToString(),
                 _lrDeviation.ValueDecimal.ToString()
             };

            futuresSource._indicators[1].Parameters
                = new List<string>() { _smaFilterLen.ValueInt.ToString(), "Close" };

            futuresSource.UpdateIndicatorsParameters();
        }

        #region Logic Entry

        private void _futs1_CandleFinishedEvent(List<Candle> candles, BotTabSimple arg2)
        {
            TryEntryLogic(_futs1);
        }

        private void _futs2_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_futs2);
        }

        private void _futs3_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_futs3);
        }

        private void _futs4_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_futs4);
        }

        private void _futs5_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_futs5);
        }

        private void _futs6_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_futs6);
        }

        private void _futs7_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_futs7);
        }

        private void _futs8_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_futs8);
        }

        private void _futs9_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_futs9);
        }

        private void _futs10_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_futs10);
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

            if (CanTradeThisSecurity(futuresSource.Security.Name) == false)
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

            // 2 проверяем условия 

            decimal futuresLastPrice = futuresCandles[^1].Close;

            if (_regime.ValueString != "OnlyShort"
                && futuresLastPrice > futuresBollinger.DataSeries[0].Last)   // фьючерс выше верхнего боллинджера
            {// Лонг

                if (_smaFilterIsOn.ValueBool == true)
                {// Sma filter

                    Aindicator sma = (Aindicator)futuresSource.Indicators[1];

                    decimal lastSma = sma.DataSeries[0].Last;

                    if (futuresCandles[^1].Close < lastSma)
                    {
                        return;
                    }
                }

                futuresSource.BuyAtIcebergMarket(GetVolume(futuresSource), _icebergCount.ValueInt, 1000);


            }
            else if (_regime.ValueString != "OnlyLong"
                && futuresLastPrice < futuresBollinger.DataSeries[2].Last) // фьючерс ниже нижнего боллинджера
            {// Шорт

                if (_smaFilterIsOn.ValueBool == true)
                {// Sma filter

                    Aindicator sma = (Aindicator)futuresSource.Indicators[1];

                    decimal lastSma = sma.DataSeries[0].Last;

                    if (futuresCandles[^1].Close > lastSma)
                    {
                        return;
                    }
                }

                futuresSource.SellAtIcebergMarket(GetVolume(futuresSource), _icebergCount.ValueInt, 1000);
            }

        }

        private void TryClosePositionLogic(
            BotTabSimple futuresSource,
            List<Candle> futuresCandles,
            Position pos)
        {
            if (StartProgram != StartProgram.IsOsTrader)
            {
                if (pos.State != PositionStateType.Open)
                {// в тестере и оптимизаторе не допускаем спама ордерами
                    return;
                }
            }

            Aindicator lrIndicator = (Aindicator)futuresSource.Indicators[0];

            if (lrIndicator.DataSeries[0].Last == 0)
            {
                return;
            }

            decimal futuresLastPrice = futuresCandles[^1].Close;

            bool needToExit = false;


            if (pos.Direction == Side.Buy
                && futuresLastPrice < lrIndicator.DataSeries[2].Last)
            {
                needToExit = true;
            }

            if (pos.Direction == Side.Sell
                && futuresLastPrice > lrIndicator.DataSeries[0].Last)
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

        #region Auto-set securities to T-Investment

        private void ButtonAutoDeploy_UserClickOnButtonEvent()
        {
            SetTSecurities();
        }

        public void SetTSecurities()
        {
            // 1 сервер Т-Банк должен быть включен

            List<AServer> servers = ServerMaster.GetAServers();

            if (servers == null
                || servers.Count == 0)
            {
                SendNewLogMessage("Сначала подключите коннектор к Т-Инвестиции", Logging.LogMessageType.Error);
                return;
            }

            if (servers.Find(s => s.ServerType == ServerType.TInvest) == null)
            {
                SendNewLogMessage("Сначала подключите коннектор к Т-Инвестиции", Logging.LogMessageType.Error);
                return;
            }

            // 2 номер портфеля должен быть указан

            string portfolioName = _portfolioNum.ValueString;

            if (string.IsNullOrEmpty(portfolioName) == true)
            {
                SendNewLogMessage("Не указан портфель для развёртывания источников", Logging.LogMessageType.Error);
                return;
            }

            Portfolio myPortfolio = null;
            AServer myServer = null;

            for (int i = 0; i < servers.Count; i++)
            {
                if (servers[i].ServerType != ServerType.TInvest)
                {
                    continue;
                }

                List<Portfolio> portfoliosInServer = servers[i].Portfolios;

                if (portfoliosInServer == null
                    || portfoliosInServer.Count == 0)
                {
                    continue;
                }

                for (int j = 0; j < portfoliosInServer.Count; j++)
                {
                    if (portfoliosInServer[j].Number == portfolioName)
                    {
                        myServer = servers[i];
                        myPortfolio = portfoliosInServer[j];
                        break;
                    }
                }

                if (myServer != null)
                {
                    break;
                }
            }

            if (myServer == null)
            {
                SendNewLogMessage("Не найден портфель и сервер. Возможно указан не верный портфель", Logging.LogMessageType.Error);
                return;
            }

            // 3 фьючерсная площадка и спот, должны быть подключены к коннектору

            List<Security> securitiesAll = myServer.Securities;

            if (securitiesAll == null
                || securitiesAll.Count == 0)
            {
                SendNewLogMessage("В коннекторе не найдены бумаги. Возможно он не подключен", Logging.LogMessageType.Error);
                return;
            }

            if (securitiesAll.Find(s => s.SecurityType == SecurityType.Futures) == null)
            {
                SendNewLogMessage("В коннекторе не найдены фьючерсы. Возможно в коннекторе выключено разрешение на их скачивание. Это настраивается в коннекторе", Logging.LogMessageType.Error);
                return;
            }

            if (securitiesAll.Find(s => s.SecurityType == SecurityType.Stock) == null)
            {
                SendNewLogMessage("В коннекторе не найдены акции. Возможно в коннекторе выключено разрешение на их скачивание. Это настраивается в коннекторе", Logging.LogMessageType.Error);
                return;
            }

            // 4 устанавливаем инструменты

            Security spotSber = securitiesAll.Find(s => s.Name == "SBER" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresSber =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("SRH") || s.Name.StartsWith("SRM")
                || s.Name.StartsWith("SRZ") || s.Name.StartsWith("SRU")));

            SetSecurities(_futs1, futuresSber, myPortfolio, myServer);

            Security spotSberPref = securitiesAll.Find(s => s.Name == "SBERP" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresSberPref =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("SPH") || s.Name.StartsWith("SPM")
                || s.Name.StartsWith("SPZ") || s.Name.StartsWith("SPU")));

            SetSecurities(_futs2, futuresSberPref, myPortfolio, myServer);

            Security spotGazp = securitiesAll.Find(s => s.Name == "GAZP" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresGazp =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("GZH") || s.Name.StartsWith("GZM")
                || s.Name.StartsWith("GZZ") || s.Name.StartsWith("GZU")));

            SetSecurities( _futs3, futuresGazp, myPortfolio, myServer);

            Security spotRosn = securitiesAll.Find(s => s.Name == "ROSN" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresRosn =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("RNH") || s.Name.StartsWith("RNM")
                || s.Name.StartsWith("RNZ") || s.Name.StartsWith("RNU")));

            SetSecurities( _futs4, futuresRosn, myPortfolio, myServer);

            Security spotLkoh = securitiesAll.Find(s => s.Name == "LKOH" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresLkoh =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("LKH") || s.Name.StartsWith("LKM")
                || s.Name.StartsWith("LKZ") || s.Name.StartsWith("LKU")));

            SetSecurities(_futs5, futuresLkoh, myPortfolio, myServer);

            Security spotVtb = securitiesAll.Find(s => s.Name == "VTBR" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresVtb =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("VBH") || s.Name.StartsWith("VBM")
                || s.Name.StartsWith("VBZ") || s.Name.StartsWith("VBU")));
        }

        private void SetSecurities(BotTabScreener tabFutures,
            List<Security> futuresSecurity, Portfolio portfolio, AServer server)
        {
            if (futuresSecurity == null)
            {
                return;
            }

            tabFutures.SecuritiesClass = futuresSecurity[0].NameClass;
            tabFutures.TimeFrame = TimeFrame.Min15;
            tabFutures.PortfolioName = portfolio.Number;
            tabFutures.ServerType = server.ServerType;
            tabFutures.ServerName = server.ServerNameAndPrefix;

            tabFutures.CandleCreateMethodType = CandleCreateMethodType.Simple.ToString();
            ((Simple)tabFutures.CandleSeriesRealization).TimeFrame = TimeFrame.Min15;
            ((Simple)tabFutures.CandleSeriesRealization).TimeFrameParameter.ValueString = TimeFrame.Min15.ToString();

            List<ActivatedSecurity> securitiesToScreener = new List<ActivatedSecurity>();

            for (int i = 0; i < futuresSecurity.Count; i++)
            {
                ActivatedSecurity sec = new ActivatedSecurity();
                sec.SecurityClass = futuresSecurity[i].NameClass;
                sec.SecurityName = futuresSecurity[i].Name;
                sec.IsOn = true;
                securitiesToScreener.Add(sec);
            }

            for (int i = 0; i < securitiesToScreener.Count; i++)
            {
                if (tabFutures.SecuritiesNames.Find(s => s.SecurityName == securitiesToScreener[i].SecurityName) == null)
                {
                    tabFutures.SecuritiesNames.Add(securitiesToScreener[i]);
                }
            }

            tabFutures.SaveSettings();
            tabFutures.NeedToReloadTabs = true;
        }

        #endregion

    }
}
