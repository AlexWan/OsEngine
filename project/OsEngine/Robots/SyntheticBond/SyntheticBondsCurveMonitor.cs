/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Candles.Series;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

using PairInPosition = (OsEngine.OsTrader.Panels.Tab.BotTabSimple Base, OsEngine.OsTrader.Panels.Tab.BotTabSimple Futures);
using Pretender = (OsEngine.OsTrader.Panels.Tab.BotTabSimple Base, OsEngine.OsTrader.Panels.Tab.BotTabSimple Futures, decimal Mult);

/*

Монитор синтетических облигаций по кривой фьючерсов на акции MOEX

Показывает три ближайшие серии фьючерсов по каждой облигации с доходностью в % годовых.
Сигналы при превышении доходностью порога (по каждой серии отдельно).
Ручное открытие/закрытие пар из таблицы монитора.
Торговая логика: первый вход в лучшую пару, перекладывание между сериями,
выход перед экспирацией

Источники
15 пар источников. В каждой паре BotTabSimple - базовая акция, BotTabScreener - фьючерсы на неё.
Первые 10 пар разворачиваются кнопками авто-развёртывания (Т-Банк в реале, выбранный сет в тестере).
Последние 5 пар - запасные слоты, настраиваются вручную

*/

namespace OsEngine.Robots.SyntheticBond
{
    [Bot("SyntheticBondsCurveMonitor")]
    public class SyntheticBondsCurveMonitor : BotPanel
    {
        private StrategyParameterString _regime;
        private StrategyParameterInt _tableUpdateIntervalSec;
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;

        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodButton;

        private StrategyParameterString _multRegime;
        private StrategyParameterDecimal _futuresMult1;
        private StrategyParameterDecimal _futuresMult2;
        private StrategyParameterDecimal _futuresMult3;
        private StrategyParameterDecimal _futuresMult4;
        private StrategyParameterDecimal _futuresMult5;
        private StrategyParameterDecimal _futuresMult6;
        private StrategyParameterDecimal _futuresMult7;
        private StrategyParameterDecimal _futuresMult8;
        private StrategyParameterDecimal _futuresMult9;
        private StrategyParameterDecimal _futuresMult10;
        private StrategyParameterDecimal _futuresMult11;
        private StrategyParameterDecimal _futuresMult12;
        private StrategyParameterDecimal _futuresMult13;
        private StrategyParameterDecimal _futuresMult14;
        private StrategyParameterDecimal _futuresMult15;

        private StrategyParameterBool _series1SignalIsOn;
        private StrategyParameterBool _series2SignalIsOn;
        private StrategyParameterBool _series3SignalIsOn;
        private StrategyParameterDecimal _signalMinYieldPercent;
        private StrategyParameterString _signalMusic;
        private StrategyParameterBool _signalErrorLogIsOn;

        private StrategyParameterBool _tradeSeries1IsOn;
        private StrategyParameterBool _tradeSeries2IsOn;
        private StrategyParameterBool _tradeSeries3IsOn;
        private StrategyParameterDecimal _minYieldToEntry;
        private StrategyParameterDecimal _minYieldDiffToMove;
        private StrategyParameterInt _daysBeforeExpirationToExit;
        private StrategyParameterBool _exitOnErrorEntryIsOn;

        private StrategyParameterString _portfolioNum;
        private StrategyParameterString _testerDeployTimeFrame;

        public SyntheticBondsCurveMonitor(string name, StartProgram startProgram) : base(name, startProgram)
        {
            CreateSources();

            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" }, "Base");

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

            _tradePeriodButton = CreateParameterButton("Non trade periods", "Base");
            _tradePeriodButton.UserClickOnButtonEvent += _tradePeriodButton_UserClickOnButtonEvent;

            _tableUpdateIntervalSec = CreateParameter("Table update interval, sec", 5, 1, 60, 1, "Base");
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contract currency", "Deposit percent" }, "Base");
            _volume = CreateParameter("Volume", 80m, 1.0m, 100, 4, "Base");

            _multRegime = CreateParameter("Mult regime", "Auto", new[] { "Auto", "Manual" }, "Fut mults");
            _futuresMult1 = CreateParameter("Fut mult 1", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult2 = CreateParameter("Fut mult 2", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult3 = CreateParameter("Fut mult 3", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult4 = CreateParameter("Fut mult 4", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult5 = CreateParameter("Fut mult 5", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult6 = CreateParameter("Fut mult 6", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult7 = CreateParameter("Fut mult 7", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult8 = CreateParameter("Fut mult 8", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult9 = CreateParameter("Fut mult 9", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult10 = CreateParameter("Fut mult 10", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult11 = CreateParameter("Fut mult 11", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult12 = CreateParameter("Fut mult 12", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult13 = CreateParameter("Fut mult 13", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult14 = CreateParameter("Fut mult 14", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult15 = CreateParameter("Fut mult 15", 1m, 1.0m, 50, 4, "Fut mults");

            _series1SignalIsOn = CreateParameter("Series 1 signal is on", false, "Signals");
            _series2SignalIsOn = CreateParameter("Series 2 signal is on", false, "Signals");
            _series3SignalIsOn = CreateParameter("Series 3 signal is on", false, "Signals");
            _signalMinYieldPercent = CreateParameter("Min yield % ann", 20m, 1.0m, 100, 1, "Signals");
            _signalMusic = CreateParameter("Music", "", "Signals");
            _signalErrorLogIsOn = CreateParameter("Error log is on", true, "Signals");

            _tradeSeries1IsOn = CreateParameter("Trade series 1 is on", true, "Trading");
            _tradeSeries2IsOn = CreateParameter("Trade series 2 is on", true, "Trading");
            _tradeSeries3IsOn = CreateParameter("Trade series 3 is on", false, "Trading");
            _minYieldToEntry = CreateParameter("Min yield to entry % ann", 20m, 1.0m, 100, 1, "Trading");
            _minYieldDiffToMove = CreateParameter("Min yield diff to move % ann", 3m, 1.0m, 100, 1, "Trading");
            _daysBeforeExpirationToExit = CreateParameter("Days before expiration to exit", 2, 1, 10, 1, "Trading");
            _exitOnErrorEntryIsOn = CreateParameter("Exit on error entry is on", true, "Trading");

            if (startProgram == StartProgram.IsOsTrader)
            {
                _portfolioNum = CreateParameter("Portfolio number", "", "Auto deploy");
                StrategyParameterButton buttonAutoDeploy = CreateParameterButton("Deploy standard securities", "Auto deploy");
                buttonAutoDeploy.UserClickOnButtonEvent += ButtonAutoDeploy_UserClickOnButtonEvent;

                _logicTimer = new System.Threading.Timer(LogicTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

                _futs1.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs2.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs3.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs4.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs5.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs6.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs7.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs8.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs9.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs10.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs11.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs12.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs13.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs14.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs15.CandleFinishedEvent += Screener_CandleFinishedEvent;
            }

            if (startProgram == StartProgram.IsTester)
            {
                _testerDeployTimeFrame = CreateParameter("Tester deploy time frame", "Min15",
                    new[] { "Min1", "Min2", "Min3", "Min5", "Min10", "Min15", "Min20", "Min30", "Min45", "Hour1" }, "Auto deploy");

                StrategyParameterButton buttonAutoDeployTester = CreateParameterButton("Deploy tester securities", "Auto deploy");
                buttonAutoDeployTester.UserClickOnButtonEvent += ButtonAutoDeployTester_UserClickOnButtonEvent;

                List<IServer> server = ServerMaster.GetServers();

                if (server != null &&
                    server.Count > 0
                    && server[0].ServerType == ServerType.Tester)
                {
                    TesterServer serverT = (TesterServer)server[0];
                    serverT.EndNextMinuteWithCandlesEvent += ServerT_EndNextMinuteWithCandlesEvent;
                }
            }

            if (startProgram == StartProgram.IsOsOptimizer)
            {
                _futs1.CandleFinishedEvent += Screener_CandleFinishedEventInOptimizer;
            }

            Description = OsLocalization.ConvertToLocString(
              "Eng:Monitor of synthetic bonds on the MOEX stock futures curve. Shows the three nearest futures series with annualized yield for each bond, signals by yield thresholds, manual pair management from the table and automatic trading with position moving between series_" +
              "Ru:Монитор синтетических облигаций на кривой фьючерсов на акции MOEX. Показывает три ближайшие серии фьючерсов с доходностью в годовых по каждой облигации, сигналы по порогам доходности, ручное управление парами из таблицы и автоматическую торговлю с перекладыванием между сериями_");

            if (startProgram != StartProgram.IsOsOptimizer)
            {
                this.ParamGuiSettings.Height = 900;
                this.ParamGuiSettings.Width = 1000;

                CustomTabToParametersUi customTabMonitor = ParamGuiSettings.CreateCustomTab(" Monitor ");
                CreateColumnsTable();
                customTabMonitor.AddChildren(_hostTable);

                _monitorTimer = new System.Threading.Timer(MonitorTimerCallback, null, 2000, Timeout.Infinite);
            }
        }

        private void _tradePeriodButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        #region Logic entry synchronization

        private System.Threading.Timer _logicTimer;
        private readonly object _logicTimerLocker = new object();
        private bool _logicTimerStarted = false;

        private void Screener_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            lock (_logicTimerLocker)
            {
                if (_logicTimerStarted)
                {
                    return;
                }

                _logicTimerStarted = true;
                _logicTimer.Change(5000, Timeout.Infinite);
            }
        }

        private void LogicTimerCallback(object state)
        {
            lock (_logicTimerLocker)
            {
                _logicTimerStarted = false;
            }

            Logic();
        }

        private void ServerT_EndNextMinuteWithCandlesEvent()
        {
            Logic();
        }

        private bool _optimizerEventSubscribed = false;

        private void Screener_CandleFinishedEventInOptimizer(List<Candle> candles, BotTabSimple source)
        {
            if (_optimizerEventSubscribed)
            {
                return;
            }

            if (source.Connector.ServerType != ServerType.Optimizer)
            {
                return;
            }

            _optimizerEventSubscribed = true;

            OptimizerServer server = source.Connector.MyServer as OptimizerServer;

            if (server != null)
            {
                server.EndNextMinuteWithCandlesEvent += ServerT_EndNextMinuteWithCandlesEvent;
            }
        }

        #endregion

        #region Logic

        private void Logic()
        {
            try
            {
                LogicInternal();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void LogicInternal()
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            TradingLogic();
        }

        #endregion

        #region Sources

        private BotTabSimple _base1;
        private BotTabScreener _futs1;

        private BotTabSimple _base2;
        private BotTabScreener _futs2;

        private BotTabSimple _base3;
        private BotTabScreener _futs3;

        private BotTabSimple _base4;
        private BotTabScreener _futs4;

        private BotTabSimple _base5;
        private BotTabScreener _futs5;

        private BotTabSimple _base6;
        private BotTabScreener _futs6;

        private BotTabSimple _base7;
        private BotTabScreener _futs7;

        private BotTabSimple _base8;
        private BotTabScreener _futs8;

        private BotTabSimple _base9;
        private BotTabScreener _futs9;

        private BotTabSimple _base10;
        private BotTabScreener _futs10;

        private BotTabSimple _base11;
        private BotTabScreener _futs11;

        private BotTabSimple _base12;
        private BotTabScreener _futs12;

        private BotTabSimple _base13;
        private BotTabScreener _futs13;

        private BotTabSimple _base14;
        private BotTabScreener _futs14;

        private BotTabSimple _base15;
        private BotTabScreener _futs15;

        private void CreateSources()
        {
            _base1 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs1 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base2 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs2 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base3 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs3 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base4 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs4 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base5 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs5 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base6 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs6 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base7 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs7 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base8 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs8 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base9 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs9 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base10 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs10 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base11 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs11 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base12 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs12 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base13 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs13 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base14 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs14 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base15 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs15 = (BotTabScreener)TabCreate(BotTabType.Screener);
        }

        private decimal GetMultByBase(BotTabSimple baseSource)
        {
            if (_multRegime.ValueString == "Auto"
                && baseSource.Security != null)
            {
                DateTime time = baseSource.TimeServerCurrent;

                if (time == DateTime.MinValue)
                {
                    time = DateTime.Now;
                }

                return GetAutoMult(baseSource.Security, time);
            }

            if (baseSource == _base1) return _futuresMult1.ValueDecimal;
            if (baseSource == _base2) return _futuresMult2.ValueDecimal;
            if (baseSource == _base3) return _futuresMult3.ValueDecimal;
            if (baseSource == _base4) return _futuresMult4.ValueDecimal;
            if (baseSource == _base5) return _futuresMult5.ValueDecimal;
            if (baseSource == _base6) return _futuresMult6.ValueDecimal;
            if (baseSource == _base7) return _futuresMult7.ValueDecimal;
            if (baseSource == _base8) return _futuresMult8.ValueDecimal;
            if (baseSource == _base9) return _futuresMult9.ValueDecimal;
            if (baseSource == _base10) return _futuresMult10.ValueDecimal;
            if (baseSource == _base11) return _futuresMult11.ValueDecimal;
            if (baseSource == _base12) return _futuresMult12.ValueDecimal;
            if (baseSource == _base13) return _futuresMult13.ValueDecimal;
            if (baseSource == _base14) return _futuresMult14.ValueDecimal;
            if (baseSource == _base15) return _futuresMult15.ValueDecimal;

            return 1;
        }

        private void SetMultByBase(BotTabSimple baseSource, decimal mult)
        {
            if (baseSource == _base1) _futuresMult1.ValueDecimal = mult;
            if (baseSource == _base2) _futuresMult2.ValueDecimal = mult;
            if (baseSource == _base3) _futuresMult3.ValueDecimal = mult;
            if (baseSource == _base4) _futuresMult4.ValueDecimal = mult;
            if (baseSource == _base5) _futuresMult5.ValueDecimal = mult;
            if (baseSource == _base6) _futuresMult6.ValueDecimal = mult;
            if (baseSource == _base7) _futuresMult7.ValueDecimal = mult;
            if (baseSource == _base8) _futuresMult8.ValueDecimal = mult;
            if (baseSource == _base9) _futuresMult9.ValueDecimal = mult;
            if (baseSource == _base10) _futuresMult10.ValueDecimal = mult;
            if (baseSource == _base11) _futuresMult11.ValueDecimal = mult;
            if (baseSource == _base12) _futuresMult12.ValueDecimal = mult;
            if (baseSource == _base13) _futuresMult13.ValueDecimal = mult;
            if (baseSource == _base14) _futuresMult14.ValueDecimal = mult;
            if (baseSource == _base15) _futuresMult15.ValueDecimal = mult;
        }

        private DateTime GetCurrentServerTime()
        {
            BotTabSimple[] bases = new BotTabSimple[]
            {
                _base1, _base2, _base3, _base4, _base5,
                _base6, _base7, _base8, _base9, _base10,
                _base11, _base12, _base13, _base14, _base15
            };

            for (int i = 0; i < bases.Length; i++)
            {
                if (string.IsNullOrEmpty(bases[i].Connector?.SecurityName))
                {
                    continue;
                }

                DateTime time = bases[i].TimeServerCurrent;

                if (time != DateTime.MinValue)
                {
                    return time;
                }
            }

            return DateTime.MinValue;
        }

        #endregion

        private decimal GetVolume(BotTabSimple tab)
        {
            return GetVolume(tab, _volumeType.ValueString, _volume.ValueDecimal);
        }

        private decimal GetVolume(BotTabSimple tab, string volumeType, decimal volumeValue)
        {
            decimal volume = 0;

            if (volumeType == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = volumeValue / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = volumeValue / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (volumeType == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = myPortfolio.ValueCurrent;

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio", LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (volumeValue / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    if (tab.Security.UsePriceStepCostToCalculateVolume == true
                        && tab.Security.PriceStep != tab.Security.PriceStepCost
                        && tab.PriceBestAsk != 0
                        && tab.Security.PriceStep != 0
                        && tab.Security.PriceStepCost != 0)
                    {
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

        #region Auto-set securities to T-Investment

        private void ButtonAutoDeploy_UserClickOnButtonEvent()
        {
            SetTSecurities();
        }

        public void SetTSecurities()
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.ConvertToLocString(
                "Eng:Auto deploy will set the standard securities. 10 pairs of MOEX stock and futures via the T-Invest connector will be assigned to the sources. Current sources settings will be overwritten. Continue_" +
                "Ru:Авто-развёртывание установит стандартные бумаги. В источники будут прописаны 10 пар акция плюс фьючерсы MOEX через коннектор Т-Инвестиции. Текущие настройки источников будут перезаписаны. Продолжить_"));

            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                return;
            }

            List<AServer> servers = ServerMaster.GetAServers();

            if (servers == null
                || servers.Count == 0)
            {
                SendNewLogMessage("Сначала подключите коннектор к Т-Инвестиции", LogMessageType.Error);
                return;
            }

            if (servers.Find(s => s.ServerType == ServerType.TInvest) == null)
            {
                SendNewLogMessage("Сначала подключите коннектор к Т-Инвестиции", LogMessageType.Error);
                return;
            }

            string portfolioName = _portfolioNum.ValueString;

            if (string.IsNullOrEmpty(portfolioName) == true)
            {
                CustomMessageBoxUi uiInfo = new CustomMessageBoxUi(OsLocalization.ConvertToLocString(
                    "Eng:First set the portfolio number in the Auto deploy tab_" +
                    "Ru:Сначала укажите номер портфеля на вкладке Auto deploy_"));
                uiInfo.ShowDialog();
                SendNewLogMessage("Не указан портфель для развёртывания источников", LogMessageType.Error);
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
                CustomMessageBoxUi uiInfo = new CustomMessageBoxUi(OsLocalization.ConvertToLocString(
                    "Eng:Portfolio not found. Check the portfolio number and the T-Invest connector_" +
                    "Ru:Портфель не найден. Проверьте номер портфеля и коннектор Т-Инвестиции_"));
                uiInfo.ShowDialog();
                SendNewLogMessage("Не найден портфель и сервер. Возможно указан не верный портфель", LogMessageType.Error);
                return;
            }

            List<Security> securitiesAll = myServer.Securities;

            if (securitiesAll == null
                || securitiesAll.Count == 0)
            {
                SendNewLogMessage("В коннекторе не найдены бумаги. Возможно он не подключен", LogMessageType.Error);
                return;
            }

            if (securitiesAll.Find(s => s.SecurityType == SecurityType.Futures) == null)
            {
                SendNewLogMessage("В коннекторе не найдены фьючерсы. Возможно в коннекторе выключено разрешение на их скачивание. Это настраивается в коннекторе", LogMessageType.Error);
                return;
            }

            if (securitiesAll.Find(s => s.SecurityType == SecurityType.Stock) == null)
            {
                SendNewLogMessage("В коннекторе не найдены акции. Возможно в коннекторе выключено разрешение на их скачивание. Это настраивается в коннекторе", LogMessageType.Error);
                return;
            }

            Security spotSber = securitiesAll.Find(s => s.Name == "SBER" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresSber =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("SRH") || s.Name.StartsWith("SRM")
                || s.Name.StartsWith("SRZ") || s.Name.StartsWith("SRU")));

            SetSecurities(_base1, _futs1, spotSber, futuresSber, myPortfolio, myServer);

            Security spotSberPref = securitiesAll.Find(s => s.Name == "SBERP" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresSberPref =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("SPH") || s.Name.StartsWith("SPM")
                || s.Name.StartsWith("SPZ") || s.Name.StartsWith("SPU")));

            SetSecurities(_base2, _futs2, spotSberPref, futuresSberPref, myPortfolio, myServer);

            Security spotGazp = securitiesAll.Find(s => s.Name == "GAZP" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresGazp =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("GZH") || s.Name.StartsWith("GZM")
                || s.Name.StartsWith("GZZ") || s.Name.StartsWith("GZU")));

            SetSecurities(_base3, _futs3, spotGazp, futuresGazp, myPortfolio, myServer);

            Security spotRosn = securitiesAll.Find(s => s.Name == "ROSN" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresRosn =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("RNH") || s.Name.StartsWith("RNM")
                || s.Name.StartsWith("RNZ") || s.Name.StartsWith("RNU")));

            SetSecurities(_base4, _futs4, spotRosn, futuresRosn, myPortfolio, myServer);

            Security spotLkoh = securitiesAll.Find(s => s.Name == "LKOH" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresLkoh =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("LKH") || s.Name.StartsWith("LKM")
                || s.Name.StartsWith("LKZ") || s.Name.StartsWith("LKU")));

            SetSecurities(_base5, _futs5, spotLkoh, futuresLkoh, myPortfolio, myServer);

            Security spotVtb = securitiesAll.Find(s => s.Name == "VTBR" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresVtb =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("VBH") || s.Name.StartsWith("VBM")
                || s.Name.StartsWith("VBZ") || s.Name.StartsWith("VBU")));

            SetSecurities(_base6, _futs6, spotVtb, futuresVtb, myPortfolio, myServer);

            Security spotGmk = securitiesAll.Find(s => s.Name == "GMKN" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresGmk =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("GKH") || s.Name.StartsWith("GKM")
                || s.Name.StartsWith("GKZ") || s.Name.StartsWith("GKU")));

            SetSecurities(_base7, _futs7, spotGmk, futuresGmk, myPortfolio, myServer);

            Security spotAlrs = securitiesAll.Find(s => s.Name == "ALRS" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresAlrs =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("ALH") || s.Name.StartsWith("ALM")
                || s.Name.StartsWith("ALZ") || s.Name.StartsWith("ALU")));

            SetSecurities(_base8, _futs8, spotAlrs, futuresAlrs, myPortfolio, myServer);

            Security spotAflt = securitiesAll.Find(s => s.Name == "AFLT" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresAflt =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
               (s.Name.StartsWith("AFH") || s.Name.StartsWith("AFM")
                || s.Name.StartsWith("AFZ") || s.Name.StartsWith("AFU")));

            SetSecurities(_base9, _futs9, spotAflt, futuresAflt, myPortfolio, myServer);

            Security spotMgnt = securitiesAll.Find(s => s.Name == "MGNT" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresMgnt =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("MNH") || s.Name.StartsWith("MNM")
                || s.Name.StartsWith("MNZ") || s.Name.StartsWith("MNU")));

            SetSecurities(_base10, _futs10, spotMgnt, futuresMgnt, myPortfolio, myServer);
        }

        private void SetSecurities(BotTabSimple tabSpot, BotTabScreener tabFutures,
            Security spotSecurity, List<Security> futuresSecurity, Portfolio portfolio, AServer server)
        {
            if (spotSecurity == null
                || futuresSecurity == null
                || futuresSecurity.Count == 0)
            {
                return;
            }

            tabSpot.Connector.ServerType = server.ServerType;
            tabSpot.Connector.ServerFullName = server.ServerNameAndPrefix;
            tabSpot.Connector.TimeFrame = TimeFrame.Min15;
            tabSpot.Connector.SecurityName = spotSecurity.Name;
            tabSpot.Connector.SecurityClass = spotSecurity.NameClass;
            tabSpot.Connector.PortfolioName = portfolio.Number;

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

            SetMultByBase(tabSpot, GetAutoMult(spotSecurity, DateTime.Now));
        }

        private decimal GetAutoMult(Security spotSecurity, DateTime time)
        {
            decimal coeff = 1;

            if (spotSecurity.Name.Contains("MGNT") == false
                && spotSecurity.Name.Contains("VTB") == false
                && spotSecurity.Name.Contains("GMKN") == false)
            {
                for (int i = 0; i < spotSecurity.Decimals; i++)
                {
                    coeff = coeff * 10;
                }
            }
            else if (spotSecurity.Name.Contains("VTB") == true)
            {
                if (time.Year < 2024
                    || (time.Year == 2024 && time.Month < 7)
                    || (time.Year == 2024 && time.Month == 7 && time.Day < 15))
                {
                    coeff = 20;
                }
                else
                {
                    coeff = 100;
                }
            }
            else if (spotSecurity.Name.Contains("GMKN") == true)
            {
                if (time.Year < 2024
                    || (time.Year == 2024 && time.Month < 4)
                    || (time.Year == 2024 && time.Month == 4 && time.Day < 4))
                {
                    coeff = 100;
                }
                else
                {
                    coeff = 10;
                }
            }

            return coeff;
        }

        #endregion

        #region SetSecurities in tester

        private void ButtonAutoDeployTester_UserClickOnButtonEvent()
        {
            SetTesterSecurities();
        }

        public void SetTesterSecurities()
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.ConvertToLocString(
                "Eng:Auto deploy will set the securities from the set selected in the tester to the sources. Current sources settings will be overwritten. Continue_" +
                "Ru:Авто-развёртывание пропишет в источники бумаги из выбранного в тестере сета. Текущие настройки источников будут перезаписаны. Продолжить_"));

            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                return;
            }

            List<IServer> servers = ServerMaster.GetServers();

            if (servers == null
                || servers.Count == 0
                || servers[0].ServerType != ServerType.Tester)
            {
                SendNewLogMessage("Сначала подключите тестер", LogMessageType.Error);
                return;
            }

            IServer server = servers[0];

            List<Security> securitiesAll = server.Securities;

            if (securitiesAll == null
                || securitiesAll.Count == 0)
            {
                SendNewLogMessage("В тестере не найдены бумаги. Сначала выберите сет и дождитесь загрузки", LogMessageType.Error);
                return;
            }

            if (server.Portfolios == null
                || server.Portfolios.Count == 0)
            {
                SendNewLogMessage("В тестере не найден портфель", LogMessageType.Error);
                return;
            }

            Portfolio myPortfolio = server.Portfolios[0];

            Security spotSber = securitiesAll.Find(s => s.Name == "SBER.txt");
            List<Security> futuresSber =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("SRH") || s.Name.StartsWith("SRM")
                || s.Name.StartsWith("SRZ") || s.Name.StartsWith("SRU"));

            SetSecuritiesInTester(_base1, _futs1, spotSber, futuresSber, myPortfolio, server);

            Security spotSberPref = securitiesAll.Find(s => s.Name == "SBERP.txt");
            List<Security> futuresSberPref =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("SPH") || s.Name.StartsWith("SPM")
                || s.Name.StartsWith("SPZ") || s.Name.StartsWith("SPU"));

            SetSecuritiesInTester(_base2, _futs2, spotSberPref, futuresSberPref, myPortfolio, server);

            Security spotGazp = securitiesAll.Find(s => s.Name == "GAZP.txt");
            List<Security> futuresGazp =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("GZH") || s.Name.StartsWith("GZM")
                || s.Name.StartsWith("GZZ") || s.Name.StartsWith("GZU"));

            SetSecuritiesInTester(_base3, _futs3, spotGazp, futuresGazp, myPortfolio, server);

            Security spotRosn = securitiesAll.Find(s => s.Name == "ROSN.txt");
            List<Security> futuresRosn =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("RNH") || s.Name.StartsWith("RNM")
                || s.Name.StartsWith("RNZ") || s.Name.StartsWith("RNU"));

            SetSecuritiesInTester(_base4, _futs4, spotRosn, futuresRosn, myPortfolio, server);

            Security spotLkoh = securitiesAll.Find(s => s.Name == "LKOH.txt");
            List<Security> futuresLkoh =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("LKH") || s.Name.StartsWith("LKM")
                || s.Name.StartsWith("LKZ") || s.Name.StartsWith("LKU"));

            SetSecuritiesInTester(_base5, _futs5, spotLkoh, futuresLkoh, myPortfolio, server);

            Security spotVtb = securitiesAll.Find(s => s.Name == "VTBR.txt");
            List<Security> futuresVtb =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("VBH") || s.Name.StartsWith("VBM")
                || s.Name.StartsWith("VBZ") || s.Name.StartsWith("VBU"));

            SetSecuritiesInTester(_base6, _futs6, spotVtb, futuresVtb, myPortfolio, server);

            Security spotGmk = securitiesAll.Find(s => s.Name == "GMKN.txt");
            List<Security> futuresGmk =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("GKH") || s.Name.StartsWith("GKM")
                || s.Name.StartsWith("GKZ") || s.Name.StartsWith("GKU"));

            SetSecuritiesInTester(_base7, _futs7, spotGmk, futuresGmk, myPortfolio, server);

            Security spotAlrs = securitiesAll.Find(s => s.Name == "ALRS.txt");
            List<Security> futuresAlrs =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("ALH") || s.Name.StartsWith("ALM")
                || s.Name.StartsWith("ALZ") || s.Name.StartsWith("ALU"));

            SetSecuritiesInTester(_base8, _futs8, spotAlrs, futuresAlrs, myPortfolio, server);

            Security spotAflt = securitiesAll.Find(s => s.Name == "AFLT.txt");
            List<Security> futuresAflt =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("AFH") || s.Name.StartsWith("AFM")
                || s.Name.StartsWith("AFZ") || s.Name.StartsWith("AFU"));

            SetSecuritiesInTester(_base9, _futs9, spotAflt, futuresAflt, myPortfolio, server);

            Security spotMgnt = securitiesAll.Find(s => s.Name == "MGNT.txt");
            List<Security> futuresMgnt =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("MNH") || s.Name.StartsWith("MNM")
                || s.Name.StartsWith("MNZ") || s.Name.StartsWith("MNU"));

            SetSecuritiesInTester(_base10, _futs10, spotMgnt, futuresMgnt, myPortfolio, server);
        }

        private void SetSecuritiesInTester(BotTabSimple tabSpot, BotTabScreener tabFutures,
            Security spotSecurity, List<Security> futuresSecurity, Portfolio portfolio, IServer server)
        {
            if (spotSecurity == null
                || futuresSecurity == null
                || futuresSecurity.Count == 0)
            {
                return;
            }

            TimeFrame timeFrame = TimeFrame.Min15;

            if (Enum.TryParse(_testerDeployTimeFrame.ValueString, out TimeFrame parsedFrame))
            {
                timeFrame = parsedFrame;
            }

            tabSpot.Connector.ServerType = server.ServerType;
            tabSpot.Connector.ServerFullName = server.ServerNameAndPrefix;
            tabSpot.Connector.TimeFrame = timeFrame;
            tabSpot.Connector.SecurityName = spotSecurity.Name;
            tabSpot.Connector.SecurityClass = spotSecurity.NameClass;
            tabSpot.Connector.PortfolioName = portfolio.Number;
            tabSpot.Connector.CommissionType = CommissionType.Percent;
            tabSpot.Connector.CommissionValue = 0.04m;

            tabFutures.SecuritiesClass = futuresSecurity[0].NameClass;
            tabFutures.TimeFrame = timeFrame;
            tabFutures.PortfolioName = portfolio.Number;
            tabFutures.ServerType = server.ServerType;
            tabFutures.ServerName = server.ServerNameAndPrefix;
            tabFutures.CommissionType = CommissionType.Percent;
            tabFutures.CommissionValue = 0.04m;

            tabFutures.CandleCreateMethodType = CandleCreateMethodType.Simple.ToString();
            ((Simple)tabFutures.CandleSeriesRealization).TimeFrame = timeFrame;
            ((Simple)tabFutures.CandleSeriesRealization).TimeFrameParameter.ValueString = timeFrame.ToString();

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

            DateTime multTime = DateTime.Now;

            TesterServer testerServer = server as TesterServer;

            if (testerServer != null
                && testerServer.TimeStart != DateTime.MinValue)
            {
                multTime = testerServer.TimeStart;
            }

            SetMultByBase(tabSpot, GetAutoMult(spotSecurity, multTime));
        }

        #endregion

        #region Monitor table

        private WindowsFormsHost _hostTable;
        private DataGridView _tableDataGrid;
        private System.Threading.Timer _monitorTimer;
        private bool _monitorUpdateInProgress = false;
        private List<BondMonitorRow> _monitorRows = new List<BondMonitorRow>();

        private void CreateColumnsTable()
        {
            try
            {
                if (MainWindow.GetDispatcher.CheckAccess() == false)
                {
                    MainWindow.GetDispatcher.Invoke(new Action(CreateColumnsTable));
                    return;
                }

                _hostTable = new WindowsFormsHost();

                _tableDataGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                       DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders);
                _tableDataGrid.ScrollBars = ScrollBars.Vertical;
                _tableDataGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                _tableDataGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                _tableDataGrid.RowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                DataGridViewTextBoxCell cellParam0 = new DataGridViewTextBoxCell();
                cellParam0.Style = _tableDataGrid.DefaultCellStyle;
                cellParam0.Style.WrapMode = DataGridViewTriState.True;

                DataGridViewColumn newColumn0 = new DataGridViewColumn();
                newColumn0.CellTemplate = cellParam0;
                newColumn0.HeaderText = "Stock";
                _tableDataGrid.Columns.Add(newColumn0);
                newColumn0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn1 = new DataGridViewColumn();
                newColumn1.CellTemplate = cellParam0;
                newColumn1.HeaderText = "Mult";
                _tableDataGrid.Columns.Add(newColumn1);
                newColumn1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn2 = new DataGridViewColumn();
                newColumn2.CellTemplate = cellParam0;
                newColumn2.HeaderText = "Series 1";
                _tableDataGrid.Columns.Add(newColumn2);
                newColumn2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn3 = new DataGridViewColumn();
                newColumn3.CellTemplate = cellParam0;
                newColumn3.HeaderText = "Series 2";
                _tableDataGrid.Columns.Add(newColumn3);
                newColumn3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn4 = new DataGridViewColumn();
                newColumn4.CellTemplate = cellParam0;
                newColumn4.HeaderText = "Series 3";
                _tableDataGrid.Columns.Add(newColumn4);
                newColumn4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn5 = new DataGridViewColumn();
                newColumn5.CellTemplate = cellParam0;
                newColumn5.HeaderText = "Fut Chart";
                _tableDataGrid.Columns.Add(newColumn5);
                newColumn5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn6 = new DataGridViewColumn();
                newColumn6.CellTemplate = cellParam0;
                newColumn6.HeaderText = "Open";
                _tableDataGrid.Columns.Add(newColumn6);
                newColumn6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn7 = new DataGridViewColumn();
                newColumn7.CellTemplate = cellParam0;
                newColumn7.HeaderText = "Close";
                _tableDataGrid.Columns.Add(newColumn7);
                newColumn7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _tableDataGrid.DataError += _tableDataGrid_DataError;
                _tableDataGrid.CellClick += _tableDataGrid_CellClick;
                _tableDataGrid.CellEndEdit += _tableDataGrid_CellEndEdit;

                _hostTable.Child = _tableDataGrid;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _tableDataGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            SendNewLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void _tableDataGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;
                int column = e.ColumnIndex;

                if (row < 0
                    || row >= _monitorRows.Count)
                {
                    return;
                }

                BondMonitorRow rowData = _monitorRows[row];

                if (column == 0)
                {
                    ShowChartForTab(rowData.Base);
                }
                else if (column == 5)
                {
                    ShowFuturesChart(rowData);
                }
                else if (column == 6)
                {
                    ShowOpenPairWindow(rowData.BaseName);
                }
                else if (column == 7)
                {
                    CloseAllByBond(rowData);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _tableDataGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;
                int column = e.ColumnIndex;

                if (row < 0
                    || row >= _monitorRows.Count
                    || column != 1)
                {
                    return;
                }

                object value = _tableDataGrid.Rows[row].Cells[column].Value;

                if (value == null)
                {
                    return;
                }

                decimal newMult = value.ToString().ToDecimal();

                if (newMult <= 0)
                {
                    return;
                }

                SetMultByBase(_monitorRows[row].Base, newMult);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ShowChartForTab(BotTabSimple tab)
        {
            try
            {
                if (tab == null)
                {
                    return;
                }

                ActiveTab = tab;
                ShowChartDialog();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ShowFuturesChart(BondMonitorRow rowData)
        {
            if (rowData.Series.Count == 0)
            {
                return;
            }

            for (int i = 0; i < rowData.Futs.Tabs.Count; i++)
            {
                if (rowData.Futs.Tabs[i] == rowData.Series[0].Tab)
                {
                    rowData.Futs.ShowChart(i);
                    return;
                }
            }
        }

        private void MonitorTimerCallback(object state)
        {
            try
            {
                if (_monitorUpdateInProgress)
                {
                    return;
                }

                _monitorUpdateInProgress = true;

                RefreshMonitorData();
                UpdateTable();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
            finally
            {
                _monitorUpdateInProgress = false;

                try
                {
                    int interval = _tableUpdateIntervalSec.ValueInt;

                    if (interval < 1)
                    {
                        interval = 1;
                    }

                    _monitorTimer?.Change(interval * 1000, Timeout.Infinite);
                }
                catch
                {
                    // ignore
                }
            }
        }

        private void RefreshMonitorData()
        {
            List<BondMonitorRow> rows = new List<BondMonitorRow>();

            AddBondMonitorRow(_base1, _futs1, rows);
            AddBondMonitorRow(_base2, _futs2, rows);
            AddBondMonitorRow(_base3, _futs3, rows);
            AddBondMonitorRow(_base4, _futs4, rows);
            AddBondMonitorRow(_base5, _futs5, rows);
            AddBondMonitorRow(_base6, _futs6, rows);
            AddBondMonitorRow(_base7, _futs7, rows);
            AddBondMonitorRow(_base8, _futs8, rows);
            AddBondMonitorRow(_base9, _futs9, rows);
            AddBondMonitorRow(_base10, _futs10, rows);
            AddBondMonitorRow(_base11, _futs11, rows);
            AddBondMonitorRow(_base12, _futs12, rows);
            AddBondMonitorRow(_base13, _futs13, rows);
            AddBondMonitorRow(_base14, _futs14, rows);
            AddBondMonitorRow(_base15, _futs15, rows);

            _monitorRows = rows;

            CheckSignals(rows);
        }

        private void AddBondMonitorRow(BotTabSimple baseSource, BotTabScreener screener, List<BondMonitorRow> rows)
        {
            if (string.IsNullOrEmpty(baseSource.Connector?.SecurityName))
            {
                return;
            }

            BondMonitorRow newRow = new BondMonitorRow();
            newRow.Base = baseSource;
            newRow.Futs = screener;
            newRow.BaseName = baseSource.Connector.SecurityName;

            DateTime time = baseSource.TimeServerCurrent;

            if (time == DateTime.MinValue)
            {
                rows.Add(newRow);
                return;
            }

            decimal mult = GetMultByBase(baseSource);

            List<BotTabSimple> nearestSeries = GetNearestSeries(screener, time, 3);

            for (int i = 0; i < nearestSeries.Count; i++)
            {
                BotTabSimple seriesTab = nearestSeries[i];

                SeriesInfo info = new SeriesInfo();
                info.Tab = seriesTab;
                info.Name = seriesTab.Connector?.SecurityName;
                info.Expiration = seriesTab.Security.Expiration;
                info.DaysToExpiration = (info.Expiration - time).Days;
                info.YieldPercent = CalculateYieldForMonitor(baseSource, seriesTab, mult, info.DaysToExpiration);

                newRow.Series.Add(info);
            }

            rows.Add(newRow);
        }

        private List<BotTabSimple> GetNearestSeries(BotTabScreener screener, DateTime time, int count)
        {
            List<BotTabSimple> result = new List<BotTabSimple>();

            for (int i = 0; i < screener.Tabs.Count; i++)
            {
                BotTabSimple curTab = screener.Tabs[i];

                if (curTab.Security == null
                    || curTab.Security.Expiration == DateTime.MinValue)
                {
                    continue;
                }

                int daysToExpiration = (curTab.Security.Expiration - time).Days;

                if (daysToExpiration <= 0)
                {
                    continue;
                }

                result.Add(curTab);
            }

            if (result.Count > 1)
            {
                result = result.OrderBy(tab => tab.Security.Expiration).ToList();
            }

            if (result.Count > count)
            {
                result = result.GetRange(0, count);
            }

            return result;
        }

        private decimal CalculateYieldForMonitor(BotTabSimple baseSource, BotTabSimple futuresSource, decimal mult, int daysToExpiration)
        {
            if (baseSource.PriceBestAsk == 0
                || futuresSource.PriceBestBid == 0
                || daysToExpiration <= 0)
            {
                return 0;
            }

            decimal deviation = futuresSource.PriceBestBid / mult - baseSource.PriceBestAsk;
            deviation = deviation / (baseSource.PriceBestAsk / 100);

            return deviation * 365 / daysToExpiration;
        }

        private void UpdateTable()
        {
            // 0 Stock
            // 1 Mult
            // 2 Series 1
            // 3 Series 2
            // 4 Series 3
            // 5 Fut Chart
            // 6 Open
            // 7 Close

            try
            {
                if (_tableDataGrid.InvokeRequired)
                {
                    _tableDataGrid.Invoke(new Action(UpdateTable));
                    return;
                }

                bool needRebuild = _tableDataGrid.Rows.Count != _monitorRows.Count;

                if (needRebuild == false)
                {
                    for (int i = 0; i < _monitorRows.Count; i++)
                    {
                        if (_tableDataGrid.Rows[i].Cells[0].Value == null
                            || _tableDataGrid.Rows[i].Cells[0].Value.ToString() != _monitorRows[i].BaseName)
                        {
                            needRebuild = true;
                            break;
                        }
                    }
                }

                if (needRebuild)
                {
                    _tableDataGrid.Rows.Clear();

                    for (int i = 0; i < _monitorRows.Count; i++)
                    {
                        _tableDataGrid.Rows.Add(GetRow(_monitorRows[i]));
                    }

                    return;
                }

                for (int i = 0; i < _monitorRows.Count; i++)
                {
                    DataGridViewRow currentRow = _tableDataGrid.Rows[i];
                    DataGridViewRow newRow = GetRow(_monitorRows[i]);

                    for (int col = 1; col <= 4; col++)
                    {
                        if (currentRow.Cells[col].Value == null
                            || currentRow.Cells[col].Value.ToString() != newRow.Cells[col].Value.ToString())
                        {
                            if (col == 1 && currentRow.Cells[col].IsInEditMode)
                            {
                                continue;
                            }

                            currentRow.Cells[col].Value = newRow.Cells[col].Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private DataGridViewRow GetRow(BondMonitorRow data)
        {
            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = data.BaseName;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].ReadOnly = false;
            row.Cells[^1].Value = GetMultByBase(data.Base);

            for (int i = 0; i < 3; i++)
            {
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[^1].ReadOnly = true;

                if (data.Series.Count > i)
                {
                    row.Cells[^1].Value = data.Series[i].Name + "  " + Math.Round(data.Series[i].YieldPercent, 1) + "%";
                }
                else
                {
                    row.Cells[^1].Value = "";
                }
            }

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = "Fut Chart";

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = "Open";

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = "Close";

            return row;
        }

        #endregion

        #region Signals

        private Dictionary<string, bool> _firedSignals = new Dictionary<string, bool>();

        private void CheckSignals(List<BondMonitorRow> rows)
        {
            try
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    for (int rank = 0; rank < 3; rank++)
                    {
                        if (IsSignalOnForRank(rank) == false)
                        {
                            continue;
                        }

                        string signalKey = rows[i].BaseName + "#" + rank;

                        if (rows[i].Series.Count <= rank)
                        {
                            _firedSignals.Remove(signalKey);
                            continue;
                        }

                        decimal yield = rows[i].Series[rank].YieldPercent;

                        if (yield >= _signalMinYieldPercent.ValueDecimal)
                        {
                            if (_firedSignals.ContainsKey(signalKey) == false)
                            {
                                _firedSignals.Add(signalKey, true);
                                FireSignal(rows[i].BaseName, rows[i].Series[rank]);
                            }
                        }
                        else
                        {
                            _firedSignals.Remove(signalKey);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private bool IsSignalOnForRank(int rank)
        {
            if (rank == 0) return _series1SignalIsOn.ValueBool;
            if (rank == 1) return _series2SignalIsOn.ValueBool;
            if (rank == 2) return _series3SignalIsOn.ValueBool;

            return false;
        }

        private void FireSignal(string baseName, SeriesInfo series)
        {
            string message = "Synthetic bond signal. " + baseName + " / " + series.Name
                + " yield " + Math.Round(series.YieldPercent, 2) + "% ann"
                + " >= " + _signalMinYieldPercent.ValueDecimal + "% ann";

            if (_signalErrorLogIsOn.ValueBool)
            {
                SendNewLogMessage(message, LogMessageType.Error);
            }
            else
            {
                SendNewLogMessage(message, LogMessageType.Signal);
            }

            try
            {
                string path = _signalMusic.ValueString;

                if (string.IsNullOrEmpty(path) == false
                    && File.Exists(path))
                {
                    SoundPlayer player = new SoundPlayer(path);
                    player.Play();
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Manual open and close

        private SyntheticBondsCurveMonitorOpenUi _openPairWindow;

        private void ShowOpenPairWindow(string baseName)
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke(new Action<string>(ShowOpenPairWindow), baseName);
                return;
            }

            if (_openPairWindow != null)
            {
                _openPairWindow.Activate();
                return;
            }

            _openPairWindow = new SyntheticBondsCurveMonitorOpenUi(this, baseName);
            _openPairWindow.Closed += _openPairWindow_Closed;
            _openPairWindow.Show();
        }

        private void _openPairWindow_Closed(object sender, EventArgs e)
        {
            _openPairWindow.Closed -= _openPairWindow_Closed;
            _openPairWindow = null;
        }

        public List<string> GetConfiguredBondNames()
        {
            List<string> names = new List<string>();

            for (int i = 0; i < _monitorRows.Count; i++)
            {
                names.Add(_monitorRows[i].BaseName);
            }

            return names;
        }

        public List<SeriesInfo> GetSeriesQuotes(string baseName)
        {
            for (int i = 0; i < _monitorRows.Count; i++)
            {
                if (_monitorRows[i].BaseName == baseName)
                {
                    return _monitorRows[i].Series;
                }
            }

            return new List<SeriesInfo>();
        }

        public decimal GetMultByBondName(string baseName)
        {
            for (int i = 0; i < _monitorRows.Count; i++)
            {
                if (_monitorRows[i].BaseName == baseName)
                {
                    return GetMultByBase(_monitorRows[i].Base);
                }
            }

            return 1;
        }

        public string GetVolumeTypeDefault()
        {
            return _volumeType.ValueString;
        }

        public decimal GetVolumeValueDefault()
        {
            return _volume.ValueDecimal;
        }

        public (decimal futBid, decimal baseAsk) GetPairPrices(string baseName, int seriesIndex)
        {
            for (int i = 0; i < _monitorRows.Count; i++)
            {
                if (_monitorRows[i].BaseName == baseName
                    && _monitorRows[i].Series.Count > seriesIndex)
                {
                    BondMonitorRow row = _monitorRows[i];
                    return (row.Series[seriesIndex].Tab.PriceBestBid, row.Base.PriceBestAsk);
                }
            }

            return (0, 0);
        }

        public void OpenPairManually(string baseName, int seriesIndex, string volumeType, decimal volumeValue)
        {
            try
            {
                for (int i = 0; i < _monitorRows.Count; i++)
                {
                    if (_monitorRows[i].BaseName != baseName
                        || _monitorRows[i].Series.Count <= seriesIndex)
                    {
                        continue;
                    }

                    BondMonitorRow row = _monitorRows[i];

                    decimal volumeFutures = GetVolume(row.Series[seriesIndex].Tab, volumeType, volumeValue);
                    decimal volumeBase = GetVolume(row.Base, volumeType, volumeValue);

                    row.Series[seriesIndex].Tab.SellAtMarket(volumeFutures);
                    row.Base.BuyAtMarket(volumeBase);

                    return;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void CloseAllByBond(BondMonitorRow rowData)
        {
            try
            {
                ClosePositionsOnTab(rowData.Base);

                for (int i = 0; i < rowData.Futs.Tabs.Count; i++)
                {
                    ClosePositionsOnTab(rowData.Futs.Tabs[i]);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ClosePositionsOnTab(BotTabSimple tab)
        {
            List<Position> positions = tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (tab.IsReadyToTrade == false)
                {
                    continue;
                }

                tab.CloseAtMarket(positions[i], positions[i].OpenVolume);
            }
        }

        #endregion

        #region Trading logic

        private void TradingLogic()
        {
            DateTime currentTime = GetCurrentServerTime();

            if (currentTime == DateTime.MinValue)
            {
                return;
            }

            if (_tradePeriodsSettings.CanTradeThisTime(currentTime) == false)
            {
                return;
            }

            List<PairInPosition> pairsInPosition = GetPairsInPositions();

            if (pairsInPosition.Count > 1)
            {
                for (int i = 0; i < pairsInPosition.Count; i++)
                {
                    if (HaveClosingPosition(pairsInPosition[i].Base)
                        || HaveClosingPosition(pairsInPosition[i].Futures))
                    {
                        return;
                    }
                }

                decimal dev0 = CalculateAnnualizedYieldContango(
                    pairsInPosition[0].Base, pairsInPosition[0].Futures, GetMultByBase(pairsInPosition[0].Base));
                decimal dev1 = CalculateAnnualizedYieldContango(
                    pairsInPosition[1].Base, pairsInPosition[1].Futures, GetMultByBase(pairsInPosition[1].Base));

                if (dev0 > dev1)
                {
                    ExitFromPosition(pairsInPosition[1].Base, pairsInPosition[1].Futures);
                }
                else
                {
                    ExitFromPosition(pairsInPosition[0].Base, pairsInPosition[0].Futures);
                }

                return;
            }

            List<Pretender> pretenders = GetPretenders(currentTime);

            if (pairsInPosition.Count > 0)
            {
                PairInPosition pair = pairsInPosition[0];

                if (_exitOnErrorEntryIsOn.ValueBool
                    && TryExitByErrorEntry(pair.Base, pair.Futures))
                {
                    return;
                }

                if (TryExitByExpiration(pair.Base, pair.Futures))
                {
                    return;
                }

                TryMovePosition(pair.Base, pair.Futures, pretenders);
            }
            else
            {
                TryFirstEntry(pretenders);
            }
        }

        private void TryFirstEntry(List<Pretender> pretenders)
        {
            if (pretenders == null
                || pretenders.Count == 0)
            {
                return;
            }

            BotTabSimple bestBase = null;
            BotTabSimple bestFutures = null;
            decimal bestYield = 0;

            for (int i = 0; i < pretenders.Count; i++)
            {
                decimal curYield = CalculateAnnualizedYieldContango(pretenders[i].Base, pretenders[i].Futures, pretenders[i].Mult);

                if (curYield > bestYield)
                {
                    bestYield = curYield;
                    bestBase = pretenders[i].Base;
                    bestFutures = pretenders[i].Futures;
                }
            }

            if (bestBase == null
                || bestYield < _minYieldToEntry.ValueDecimal)
            {
                return;
            }

            EntryInPositionContango(bestBase, bestFutures);
        }

        private bool TryExitByExpiration(BotTabSimple baseSource, BotTabSimple futuresSource)
        {
            int daysToExpiration = (futuresSource.Security.Expiration - futuresSource.TimeServerCurrent).Days;

            if (daysToExpiration <= _daysBeforeExpirationToExit.ValueInt)
            {
                ExitFromPosition(baseSource, futuresSource);
                return true;
            }

            return false;
        }

        private bool TryExitByErrorEntry(BotTabSimple baseSource, BotTabSimple futuresSource)
        {
            List<Position> basePos = baseSource.PositionsOpenAll;
            List<Position> futPos = futuresSource.PositionsOpenAll;

            if (basePos.Count + futPos.Count != 2)
            {
                ExitFromPosition(baseSource, futuresSource);
                return true;
            }

            return false;
        }

        private void TryMovePosition(BotTabSimple baseInPosition, BotTabSimple futuresInPosition, List<Pretender> pretenders)
        {
            if (pretenders == null
                || pretenders.Count == 0)
            {
                return;
            }

            BotTabSimple bestBase = null;
            BotTabSimple bestFutures = null;
            decimal bestYield = 0;

            for (int i = 0; i < pretenders.Count; i++)
            {
                decimal curYield = CalculateAnnualizedYieldContango(pretenders[i].Base, pretenders[i].Futures, pretenders[i].Mult);

                if (curYield > bestYield)
                {
                    bestYield = curYield;
                    bestBase = pretenders[i].Base;
                    bestFutures = pretenders[i].Futures;
                }
            }

            if (bestBase == null)
            {
                return;
            }

            decimal currentYield = CalculateAnnualizedYieldContango(baseInPosition, futuresInPosition, GetMultByBase(baseInPosition));

            if (currentYield >= bestYield
                || bestYield <= 0
                || currentYield == 0)
            {
                return;
            }

            decimal diff = bestYield - currentYield;

            if (diff > _minYieldDiffToMove.ValueDecimal)
            {
                ExitFromPosition(baseInPosition, futuresInPosition);
                EntryInPositionContango(bestBase, bestFutures);
            }
        }

        private decimal CalculateAnnualizedYieldContango(BotTabSimple baseSource, BotTabSimple futuresSource, decimal mult)
        {
            List<Candle> baseCandles = baseSource.CandlesAll;
            List<Candle> futCandles = futuresSource.CandlesAll;

            if (baseCandles == null
                || baseCandles.Count == 0
                || futCandles == null
                || futCandles.Count == 0)
            {
                return 0;
            }

            Candle lastBaseC = baseCandles[^1];
            Candle lastFutC = futCandles[^1];

            if (lastBaseC.TimeStart != lastFutC.TimeStart)
            {
                return 0;
            }

            if (lastFutC.Close / mult <= lastBaseC.Close)
            {
                return 0;
            }

            if (baseSource.PriceBestAsk == 0)
            {
                return 0;
            }

            if (futuresSource.Security == null
                || futuresSource.Security.Expiration == DateTime.MinValue)
            {
                return 0;
            }

            int daysToExpiration = (futuresSource.Security.Expiration - futuresSource.TimeServerCurrent).Days;

            if (daysToExpiration <= 0)
            {
                return 0;
            }

            decimal deviation = futuresSource.PriceBestBid / mult - baseSource.PriceBestAsk;
            deviation = deviation / (baseSource.PriceBestAsk / 100);

            return deviation * 365 / daysToExpiration;
        }

        #endregion

        #region Pairs creation

        private List<PairInPosition> GetPairsInPositions()
        {
            List<PairInPosition> result = new List<PairInPosition>();

            AddPairsInPositionsBySecurity(_base1, _futs1, result);
            AddPairsInPositionsBySecurity(_base2, _futs2, result);
            AddPairsInPositionsBySecurity(_base3, _futs3, result);
            AddPairsInPositionsBySecurity(_base4, _futs4, result);
            AddPairsInPositionsBySecurity(_base5, _futs5, result);
            AddPairsInPositionsBySecurity(_base6, _futs6, result);
            AddPairsInPositionsBySecurity(_base7, _futs7, result);
            AddPairsInPositionsBySecurity(_base8, _futs8, result);
            AddPairsInPositionsBySecurity(_base9, _futs9, result);
            AddPairsInPositionsBySecurity(_base10, _futs10, result);
            AddPairsInPositionsBySecurity(_base11, _futs11, result);
            AddPairsInPositionsBySecurity(_base12, _futs12, result);
            AddPairsInPositionsBySecurity(_base13, _futs13, result);
            AddPairsInPositionsBySecurity(_base14, _futs14, result);
            AddPairsInPositionsBySecurity(_base15, _futs15, result);

            return result;
        }

        private void AddPairsInPositionsBySecurity(
            BotTabSimple baseSource, BotTabScreener screener, List<PairInPosition> result)
        {
            if (string.IsNullOrEmpty(baseSource.Connector?.SecurityName))
            {
                return;
            }

            if (baseSource.PositionsOpenAll.Count == 0)
            {
                return;
            }

            for (int i = 0; i < screener.Tabs.Count; i++)
            {
                BotTabSimple curTab = screener.Tabs[i];

                if (curTab.PositionsOpenAll.Count > 0)
                {
                    result.Add((baseSource, curTab));
                }
            }
        }

        private List<Pretender> GetPretenders(DateTime time)
        {
            List<Pretender> result = new List<Pretender>();

            AddPretendersBySecurity(_base1, _futs1, GetMultByBase(_base1), time, result);
            AddPretendersBySecurity(_base2, _futs2, GetMultByBase(_base2), time, result);
            AddPretendersBySecurity(_base3, _futs3, GetMultByBase(_base3), time, result);
            AddPretendersBySecurity(_base4, _futs4, GetMultByBase(_base4), time, result);
            AddPretendersBySecurity(_base5, _futs5, GetMultByBase(_base5), time, result);
            AddPretendersBySecurity(_base6, _futs6, GetMultByBase(_base6), time, result);
            AddPretendersBySecurity(_base7, _futs7, GetMultByBase(_base7), time, result);
            AddPretendersBySecurity(_base8, _futs8, GetMultByBase(_base8), time, result);
            AddPretendersBySecurity(_base9, _futs9, GetMultByBase(_base9), time, result);
            AddPretendersBySecurity(_base10, _futs10, GetMultByBase(_base10), time, result);
            AddPretendersBySecurity(_base11, _futs11, GetMultByBase(_base11), time, result);
            AddPretendersBySecurity(_base12, _futs12, GetMultByBase(_base12), time, result);
            AddPretendersBySecurity(_base13, _futs13, GetMultByBase(_base13), time, result);
            AddPretendersBySecurity(_base14, _futs14, GetMultByBase(_base14), time, result);
            AddPretendersBySecurity(_base15, _futs15, GetMultByBase(_base15), time, result);

            return result;
        }

        private void AddPretendersBySecurity(
            BotTabSimple baseSource, BotTabScreener screener, decimal mult, DateTime time, List<Pretender> result)
        {
            if (string.IsNullOrEmpty(baseSource.Connector?.SecurityName))
            {
                return;
            }

            if (baseSource.PositionsOpenAll.Count > 0)
            {
                return;
            }

            List<BotTabSimple> nearestSeries = GetNearestSeries(screener, time, 3);

            for (int i = 0; i < nearestSeries.Count; i++)
            {
                if (IsTradeOnForRank(i) == false)
                {
                    continue;
                }

                int daysToExpiration = (nearestSeries[i].Security.Expiration - time).Days;

                if (daysToExpiration <= _daysBeforeExpirationToExit.ValueInt)
                {
                    continue;
                }

                result.Add((baseSource, nearestSeries[i], mult));
            }
        }

        private bool IsTradeOnForRank(int rank)
        {
            if (rank == 0) return _tradeSeries1IsOn.ValueBool;
            if (rank == 1) return _tradeSeries2IsOn.ValueBool;
            if (rank == 2) return _tradeSeries3IsOn.ValueBool;

            return false;
        }

        #endregion

        #region Position execution logic

        private void EntryInPositionContango(BotTabSimple baseSource, BotTabSimple futuresSource)
        {
            decimal volumeFutures = GetVolume(futuresSource);
            decimal volumeBase = GetVolume(baseSource);

            futuresSource.SellAtMarket(volumeFutures);
            baseSource.BuyAtMarket(volumeBase);
        }

        private void ExitFromPosition(BotTabSimple baseSource, BotTabSimple futuresSource)
        {
            List<Position> positionsFut = futuresSource.PositionsOpenAll;
            List<Position> positionsBase = baseSource.PositionsOpenAll;

            if (positionsFut.Count > 0)
            {
                ClosePosAtMarket(futuresSource, positionsFut[0]);
            }
            if (positionsBase.Count > 0)
            {
                ClosePosAtMarket(baseSource, positionsBase[0]);
            }
        }

        private void ClosePosAtMarket(BotTabSimple tab, Position pos)
        {
            if (tab.IsReadyToTrade == false)
            {
                return;
            }

            if (pos.State != PositionStateType.Open)
            {
                return;
            }

            tab.CloseAtMarket(pos, pos.OpenVolume);
        }

        private bool HaveClosingPosition(BotTabSimple tab)
        {
            List<Position> positions = tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].State == PositionStateType.Closing)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }

    public class BondMonitorRow
    {
        public BotTabSimple Base;

        public BotTabScreener Futs;

        public string BaseName;

        public List<SeriesInfo> Series = new List<SeriesInfo>();
    }

    public class SeriesInfo
    {
        public BotTabSimple Tab;

        public string Name;

        public decimal YieldPercent;

        public decimal ContangoAbsPercent;

        public int DaysToExpiration;

        public DateTime Expiration;
    }
}
