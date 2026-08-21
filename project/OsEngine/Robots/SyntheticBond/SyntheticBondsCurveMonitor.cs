/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Alerts;
using OsEngine.Candles.Series;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

/*

Монитор синтетических облигаций по кривой фьючерсов на акции MOEX

Таблица: три ближайшие серии фьючерсов по каждой облигации с доходностью в % годовых.
Серии - кнопки, открывают чарт соответствующего фьючерса. Мульт редактируется в таблице.
Позиции подсвечиваются: лонг базы - зелёным, шорт фьючерса - красным, объёмы в скобках.

Сигналы: срабатывают по закрытию свечи, если доходность серии превысила порог
(по каждой серии отдельный флаг). Все сигналы за свечу собираются в одно
сводное сообщение с одним звуком (Duck/Wolf).

Ручное управление парами из таблицы. Кнопка Open - окно открытия синтетической
облигации: выбор серии, объёмы в лотах (база = контракты x мульт / лот),
подтверждение перед отправкой ордеров, инструкция по кнопке "?".
Кнопка Close - подтверждение со списком позиций или сообщение, что позиций нет.

Автоматической торговли нет.

Источники
15 пар источников. В каждой паре BotTabSimple - базовая акция, BotTabScreener - фьючерсы на неё.
Первые 10 пар разворачиваются кнопками авто-развёртывания (Т-Банк в реале - с выбором
таймфрейма Min1/5/15/30, выбранный сет в тестере).
Последние 5 пар - запасные слоты, настраиваются вручную

*/

namespace OsEngine.Robots.SyntheticBond
{
    [Bot("SyntheticBondsCurveMonitor")]
    public class SyntheticBondsCurveMonitor : BotPanel
    {
        private StrategyParameterInt _tableUpdateIntervalSec;
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;

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

        private StrategyParameterString _portfolioNum;
        private StrategyParameterString _deployTimeFrame;
        private StrategyParameterString _testerDeployTimeFrame;

        public SyntheticBondsCurveMonitor(string name, StartProgram startProgram) : base(name, startProgram)
        {
            CreateSources();

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
            _signalMusic = CreateParameter("Music", "Duck", new[] { "Duck", "Wolf" }, "Signals");
            _signalErrorLogIsOn = CreateParameter("Error log is on", false, "Signals");

            if (startProgram == StartProgram.IsOsTrader)
            {
                _portfolioNum = CreateParameter("Portfolio number", "", "Auto deploy");
                _deployTimeFrame = CreateParameter("Deploy time frame", "Min5", new[] { "Min1", "Min5", "Min15", "Min30" }, "Auto deploy");
                StrategyParameterButton buttonAutoDeploy = CreateParameterButton("Deploy standard securities", "Auto deploy");
                buttonAutoDeploy.UserClickOnButtonEvent += ButtonAutoDeploy_UserClickOnButtonEvent;

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
                _testerDeployTimeFrame = CreateParameter("Tester deploy time frame", "Min5",
                    new[] { "Min1", "Min2", "Min3", "Min5", "Min10", "Min15", "Min20", "Min30", "Min45", "Hour1" }, "Auto deploy");

                StrategyParameterButton buttonAutoDeployTester = CreateParameterButton("Deploy tester securities", "Auto deploy");
                buttonAutoDeployTester.UserClickOnButtonEvent += ButtonAutoDeployTester_UserClickOnButtonEvent;

                List<IServer> servers = ServerMaster.GetServers();

                if (servers != null
                    && servers.Count > 0
                    && servers[0].ServerType == ServerType.Tester)
                {
                    TesterServer serverT = (TesterServer)servers[0];
                    serverT.EndNextMinuteWithCandlesEvent += ServerT_EndNextMinuteWithCandlesEvent;
                }
            }

            Description = OsLocalization.ConvertToLocString(
              "Eng:Monitor of synthetic bonds on the MOEX stock futures curve. Shows the three nearest futures series with annualized yield for each bond, consolidated signals on candle close, position highlighting and manual pair management from the table (volumes in lots, confirmation dialogs). No automatic trading_" +
              "Ru:Монитор синтетических облигаций на кривой фьючерсов на акции MOEX. Показывает три ближайшие серии фьючерсов с доходностью в годовых по каждой облигации, сводные сигналы по закрытию свечи, подсветку позиций и ручное управление парами из таблицы (объёмы в лотах, окна подтверждения). Автоматической торговли нет_");

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

        private DateTime _lastSignalCandleTime = DateTime.MinValue;

        private void Screener_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            if (candles == null
                || candles.Count == 0)
            {
                return;
            }

            DateTime candleTime = candles[^1].TimeStart;

            if (candleTime <= _lastSignalCandleTime)
            {
                return;
            }

            _lastSignalCandleTime = candleTime;

            RunSignals();
        }

        private void ServerT_EndNextMinuteWithCandlesEvent()
        {
            DateTime time = this.TimeServer;

            if (time <= _lastSignalCandleTime)
            {
                return;
            }

            _lastSignalCandleTime = time;

            RunSignals();
        }

        private void RunSignals()
        {
            try
            {
                CheckSignals(_monitorRows);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

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

            TimeFrame timeFrame = TimeFrame.Min5;

            if (Enum.TryParse(_deployTimeFrame.ValueString, out TimeFrame parsedFrame))
            {
                timeFrame = parsedFrame;
            }

            tabSpot.Connector.ServerType = server.ServerType;
            tabSpot.Connector.ServerFullName = server.ServerNameAndPrefix;
            tabSpot.Connector.TimeFrame = timeFrame;
            tabSpot.Connector.SecurityName = spotSecurity.Name;
            tabSpot.Connector.SecurityClass = spotSecurity.NameClass;
            tabSpot.Connector.PortfolioName = portfolio.Number;

            tabFutures.SecuritiesClass = futuresSecurity[0].NameClass;
            tabFutures.TimeFrame = timeFrame;
            tabFutures.PortfolioName = portfolio.Number;
            tabFutures.ServerType = server.ServerType;
            tabFutures.ServerName = server.ServerNameAndPrefix;

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

            TimeFrame timeFrame = TimeFrame.Min5;

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
                else if (column == 2)
                {
                    ShowFuturesChart(rowData, 0);
                }
                else if (column == 3)
                {
                    ShowFuturesChart(rowData, 1);
                }
                else if (column == 4)
                {
                    ShowFuturesChart(rowData, 2);
                }
                else if (column == 5)
                {
                    ShowOpenPairWindow(rowData.BaseName);
                }
                else if (column == 6)
                {
                    CloseBondWithConfirm(rowData);
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

        private void ShowFuturesChart(BondMonitorRow rowData, int seriesIndex = 0)
        {
            if (rowData.Series.Count <= seriesIndex)
            {
                return;
            }

            for (int i = 0; i < rowData.Futs.Tabs.Count; i++)
            {
                if (rowData.Futs.Tabs[i] == rowData.Series[seriesIndex].Tab)
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

            SetTabPosInfo(baseSource, newRow);

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

                SetTabPosInfo(seriesTab, info);

                newRow.Series.Add(info);
            }

            rows.Add(newRow);
        }

        private void SetTabPosInfo(BotTabSimple tab, BondMonitorRow row)
        {
            List<Position> positions = tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].State == PositionStateType.Opening
                    || positions[i].State == PositionStateType.Open
                    || positions[i].State == PositionStateType.Closing)
                {
                    row.BaseHasPosition = true;
                    row.BasePosVolume = positions[i].OpenVolume;
                    row.BasePosSide = positions[i].Direction;
                    return;
                }
            }
        }

        private void SetTabPosInfo(BotTabSimple tab, SeriesInfo info)
        {
            List<Position> positions = tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].State == PositionStateType.Opening
                    || positions[i].State == PositionStateType.Open
                    || positions[i].State == PositionStateType.Closing)
                {
                    info.HasPosition = true;
                    info.PosVolume = positions[i].OpenVolume;
                    info.PosSide = positions[i].Direction;
                    return;
                }
            }
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
            // 5 Open
            // 6 Close

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
                        object cellValue = _tableDataGrid.Rows[i].Cells[0].Value;

                        if (cellValue == null
                            || (cellValue.ToString() != _monitorRows[i].BaseName
                                && cellValue.ToString().StartsWith(_monitorRows[i].BaseName + " (") == false))
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

                    for (int col = 0; col <= 4; col++)
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

                        if (currentRow.Cells[col].Style.ForeColor != newRow.Cells[col].Style.ForeColor)
                        {
                            currentRow.Cells[col].Style.ForeColor = newRow.Cells[col].Style.ForeColor;
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

            if (data.BaseHasPosition)
            {
                row.Cells[^1].Value = data.BaseName + " (" + data.BasePosVolume + ")";
                row.Cells[^1].Style.ForeColor = data.BasePosSide == Side.Buy
                    ? System.Drawing.Color.LimeGreen
                    : System.Drawing.Color.OrangeRed;
            }
            else
            {
                row.Cells[^1].Value = data.BaseName;
            }

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].ReadOnly = false;
            row.Cells[^1].Value = GetMultByBase(data.Base);

            for (int i = 0; i < 3; i++)
            {
                row.Cells.Add(new DataGridViewButtonCell());
                row.Cells[^1].ReadOnly = true;

                if (data.Series.Count > i)
                {
                    string text = data.Series[i].Name + "  " + Math.Round(data.Series[i].YieldPercent, 1) + "%";

                    if (data.Series[i].HasPosition)
                    {
                        text += " (" + data.Series[i].PosVolume + ")";
                        row.Cells[^1].Style.ForeColor = data.Series[i].PosSide == Side.Buy
                            ? System.Drawing.Color.LimeGreen
                            : System.Drawing.Color.OrangeRed;
                    }

                    row.Cells[^1].Value = text;
                }
                else
                {
                    row.Cells[^1].Value = "";
                }
            }

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

        private void CheckSignals(List<BondMonitorRow> rows)
        {
            try
            {
                List<string> signals = new List<string>();

                for (int i = 0; i < rows.Count; i++)
                {
                    for (int rank = 0; rank < 3; rank++)
                    {
                        if (IsSignalOnForRank(rank) == false)
                        {
                            continue;
                        }

                        if (rows[i].Series.Count <= rank)
                        {
                            continue;
                        }

                        if (rows[i].Series[rank].YieldPercent >= _signalMinYieldPercent.ValueDecimal)
                        {
                            signals.Add(rows[i].BaseName + " / " + rows[i].Series[rank].Name
                                + " yield " + Math.Round(rows[i].Series[rank].YieldPercent, 2) + "% ann");
                        }
                    }
                }

                if (signals.Count > 0)
                {
                    FireSignals(signals);
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

        private void FireSignals(List<string> signals)
        {
            string message = "Synthetic bond signals. Yield >= " + _signalMinYieldPercent.ValueDecimal + "% ann:";

            for (int i = 0; i < signals.Count; i++)
            {
                message += "\n" + signals[i];
            }

            if (_signalErrorLogIsOn.ValueBool)
            {
                SendNewLogMessage(message, LogMessageType.Error);
            }
            else
            {
                SendNewLogMessage(message, LogMessageType.Signal);
            }

            PlaySound(_signalMusic.ValueString);
        }

        private void PlaySound(string soundName)
        {
            try
            {
                UnmanagedMemoryStream stream = Resources.Bird;

                if (soundName == AlertMusic.Duck.ToString())
                {
                    stream = Resources.Duck;
                }
                if (soundName == AlertMusic.Wolf.ToString())
                {
                    stream = Resources.wolf01;
                }

                if (stream != null)
                {
                    SoundPlayer player = new SoundPlayer(stream);
                    player.Play();
                }
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region Manual open and close

        private SyntheticBondsCurveMonitorOpenUi _openPairWindow;

        private void ShowOpenPairWindow(string baseName)
        {
            try
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
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _openPairWindow_Closed(object sender, EventArgs e)
        {
            try
            {
                _openPairWindow.Closed -= _openPairWindow_Closed;
                _openPairWindow = null;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
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

        public (decimal futVolume, decimal baseVolume, decimal baseDisplayLots) GetPairVolumes(string baseName, int seriesIndex, string volumeType, decimal volumeValue)
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

                if (volumeFutures <= 0)
                {
                    return (0, 0, 0);
                }

                decimal mult = GetMultByBase(row.Base);
                decimal baseShares = volumeFutures * mult;
                decimal baseLot = 1;

                if (row.Base.Security != null
                    && row.Base.Security.Lot > 1)
                {
                    baseLot = row.Base.Security.Lot;
                }

                decimal volumeBase = baseShares / baseLot;

                if (StartProgram == StartProgram.IsOsTrader
                    && row.Base.Security != null)
                {
                    volumeBase = Math.Round(volumeBase, row.Base.Security.DecimalsVolume);
                }
                else
                {
                    volumeBase = Math.Round(volumeBase, 6);
                }

                string secName = row.Base.Connector?.SecurityName ?? "";
                decimal baseDisplayLots = Math.Round(baseShares / GetDisplayLotByName(secName), 2);

                return (volumeFutures, volumeBase, baseDisplayLots);
            }

            return (0, 0, 0);
        }

        private decimal GetDisplayLotByName(string securityName)
        {
            if (securityName.Contains("SBER")) return 10;
            if (securityName.Contains("GAZP")) return 10;
            if (securityName.Contains("ALRS")) return 10;
            if (securityName.Contains("AFLT")) return 10;
            if (securityName.Contains("VTB")) return 10000;

            return 1;
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

                    (decimal volumeFutures, decimal volumeBase, _) = GetPairVolumes(baseName, seriesIndex, volumeType, volumeValue);

                    if (volumeFutures <= 0)
                    {
                        SendNewLogMessage("Open pair skipped: futures volume is zero. Not enough money for one contract. " + baseName, LogMessageType.Error);
                        return;
                    }

                    if (volumeBase <= 0)
                    {
                        SendNewLogMessage("Open pair skipped: base volume is zero. " + baseName, LogMessageType.Error);
                        return;
                    }

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

        private void CloseBondWithConfirm(BondMonitorRow rowData)
        {
            try
            {
                List<string> positionsInfo = GetOpenPositionsInfo(rowData);

                if (positionsInfo.Count == 0)
                {
                    CustomMessageBoxUi uiInfo = new CustomMessageBoxUi(OsLocalization.ConvertToLocString(
                        "Eng:No open positions for " + rowData.BaseName + "_" +
                        "Ru:Нет открытых позиций по " + rowData.BaseName + "_"));
                    uiInfo.ShowDialog();
                    return;
                }

                string message = OsLocalization.ConvertToLocString(
                    "Eng:Closing positions for " + rowData.BaseName + "_Ru:Закрываем позиции по " + rowData.BaseName + "_") + "\n\n";

                for (int i = 0; i < positionsInfo.Count; i++)
                {
                    message += positionsInfo[i] + "\n";
                }

                message += "\n" + OsLocalization.ConvertToLocString("Eng:Continue_Ru:Продолжить_") + "?";

                AcceptDialogUi ui = new AcceptDialogUi(message);
                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                CloseAllByBond(rowData);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private List<string> GetOpenPositionsInfo(BondMonitorRow rowData)
        {
            List<string> info = new List<string>();

            AddPositionsInfo(rowData.Base, info);

            for (int i = 0; i < rowData.Futs.Tabs.Count; i++)
            {
                AddPositionsInfo(rowData.Futs.Tabs[i], info);
            }

            return info;
        }

        private void AddPositionsInfo(BotTabSimple tab, List<string> info)
        {
            List<Position> positions = tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].State != PositionStateType.Open
                    || positions[i].OpenVolume <= 0)
                {
                    continue;
                }

                info.Add(tab.Connector?.SecurityName
                    + "  " + positions[i].Direction
                    + "  " + positions[i].OpenVolume);
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

    }

    public class BondMonitorRow
    {
        public BotTabSimple Base;

        public BotTabScreener Futs;

        public string BaseName;

        public bool BaseHasPosition;

        public decimal BasePosVolume;

        public Side BasePosSide;

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

        public bool HasPosition;

        public decimal PosVolume;

        public Side PosSide;
    }
}
