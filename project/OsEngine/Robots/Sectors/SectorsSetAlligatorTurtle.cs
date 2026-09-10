/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Candles.Series;
using OsEngine.Entity;
using OsEngine.Indicators;
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

/* Description
Торговый робот для OsEngine. Сборник «Секторальный набор», робот №2.

Секторальный трендовик в духе "черепах" по акциям MOEX. 9 скринеров - по одному
на сектор экономики (нефтегаз, финансы, металлы, потребсектор, электроэнергетика,
транспорт, телекомы, химия, ИТ).

Монитор секторов: на каждой бумаге RSI (по умолчанию 100). Показатель сектора -
средний RSI его бумаг. Входы разрешены только в ТОП-N секторах по среднему RSI
(по умолчанию 2). Монитор отображается таблицей на вкладке " Monitor ".

Покупка (только лонг):
1. Сектор бумаги в ТОП-N.
2. Цена выше всех трёх линий Аллигатора и линии выстроены по Вильямсу:
   Lips > Teeth > Jaw ("пасть открыта вверх").
3. Цена ниже верхней границы длинного PriceChannel (проверка по значению
   вторым с конца - последнее значение канала перестраивается по текущей свече).
4. Нет позиции по бумаге, нет позиций в секторе, не достигнут общий лимит позиций.
Вход через BuyAtStopMarketIceberg, цена активации = верхняя граница длинного
PriceChannel (последнее значение), время жизни заявки = 1 свеча.

Выход: ручной трейлинг через CloseAtStopMarketIceberg по нижней границе короткого
PriceChannel. Стоп передвигается на каждой закрытой свече только в сторону прибыли
и только в торговое время.

Точки входа в логику: в реале - события свечей скринеров взводят таймер на 5 секунд;
в тестере и оптимизаторе - серверное событие EndNextMinuteWithCandlesEvent.

Неторговые периоды: торговля 10.00-18.00, в выходные не торгуем. В неторговое время
стоп-заявки на вход отменяются, стопы открытых позиций деактивируются.

Авто-развёртывание: вкладка "Auto deploy" прописывает стандартные списки бумаг
по секторам в источники (в реале - по номеру портфеля, в тестере - из выбранного сета).
*/

namespace OsEngine.Robots.Sectors
{
    [Bot("SectorsSetAlligatorTurtle")]
    public class SectorsSetAlligatorTurtle : BotPanel
    {
        #region Fields

        private List<SectorDataAlligator> _sectors = new List<SectorDataAlligator>();

        private NonTradePeriods _tradePeriodsSettings;

        private System.Threading.Timer _logicTimer;
        private bool _logicTimerStarted = false;
        private readonly object _logicTimerLocker = new object();
        private bool _optimizerEventSubscribed = false;

        private Dictionary<string, DateTime> _lastLogicCandleBySecurity = new Dictionary<string, DateTime>();

        #endregion

        #region Parameters

        // Base
        private StrategyParameterString _regime;
        private StrategyParameterButton _tradePeriodsShowDialogButton;
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;
        private StrategyParameterInt _maxPositions;
        private StrategyParameterInt _icebergOrdersCount;
        private StrategyParameterInt _icebergMillisecondsDistance;
        private StrategyParameterInt _rsiLength;
        private StrategyParameterInt _topSectorsCount;
        private StrategyParameterString _monitorEntryFilter;

        // Indicators
        private StrategyParameterInt _alligatorJawLength;
        private StrategyParameterInt _alligatorTeethLength;
        private StrategyParameterInt _alligatorLipsLength;
        private StrategyParameterInt _longPriceChannelLength;
        private StrategyParameterInt _shortPriceChannelLength;

        // Auto deploy
        private StrategyParameterString _portfolioNum;
        private StrategyParameterString _deployTimeFrame;
        private StrategyParameterString _testerDeployTimeFrame;

        #endregion

        #region Constructor

        public SectorsSetAlligatorTurtle(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // неторговые периоды. Торговля с 10.00 до 18.00, в выходные не торгуем
            _tradePeriodsSettings = new NonTradePeriods(name);

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1Start = new TimeOfDay() { Hour = 0, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1End = new TimeOfDay() { Hour = 10, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1OnOff = true;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2OnOff = false;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3Start = new TimeOfDay() { Hour = 18, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3End = new TimeOfDay() { Hour = 23, Minute = 59 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3OnOff = true;

            _tradePeriodsSettings.TradeInSunday = false;
            _tradePeriodsSettings.TradeInSaturday = false;

            _tradePeriodsSettings.Load();

            // Вкладка Base
            _regime = CreateParameter("Regime", "On", new[] { "On", "Off" }, "Base");
            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods", "Base");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Base");
            _volume = CreateParameter("Volume", 12.5m, 1.0m, 50, 4, "Base");
            _tradeAssetInPortfolio = CreateParameter("Trade asset in portfolio", "Prime", "Base");
            _maxPositions = CreateParameter("Max positions", 4, 1, 20, 1, "Base");
            _icebergOrdersCount = CreateParameter("Iceberg orders count", 3, 1, 10, 1, "Base");
            _icebergMillisecondsDistance = CreateParameter("Iceberg milliseconds distance", 1000, 500, 10000, 500, "Base");
            _rsiLength = CreateParameter("Monitor rsi length", 74, 10, 500, 10, "Base");
            _topSectorsCount = CreateParameter("Monitor trade sectors count", 5, 1, 10, 1, "Base");
            _monitorEntryFilter = CreateParameter("Monitor entry filter", "Strongest", new[] { "None", "Strongest", "Weakest" }, "Base");

            // Вкладка Indicators
            _alligatorJawLength = CreateParameter("Alligator jaw length", 60, 5, 100, 1, "Indicators");
            _alligatorTeethLength = CreateParameter("Alligator teeth length", 25, 5, 100, 1, "Indicators");
            _alligatorLipsLength = CreateParameter("Alligator lips length", 5, 5, 100, 1, "Indicators");
            _longPriceChannelLength = CreateParameter("Long price channel length", 14, 10, 500, 10, "Indicators");
            _shortPriceChannelLength = CreateParameter("Short price channel length", 20, 5, 200, 5, "Indicators");

            // Создание источников - 10 скринеров по секторам
            CreateSectors();

            // Индикаторы на каждом скринере: Alligator + два PriceChannel + RSI (монитор)
            for (int i = 0; i < _sectors.Count; i++)
            {
                BotTabScreener screener = _sectors[i].Screener;

                screener.CreateCandleIndicator(1, "Alligator",
                    new List<string>() {
                        _alligatorJawLength.ValueInt.ToString(),
                        _alligatorTeethLength.ValueInt.ToString(),
                        _alligatorLipsLength.ValueInt.ToString(),
                        "8", "5", "3"
                    }, "Prime");
                screener.CreateCandleIndicator(2, "PriceChannel",
                    new List<string>() { _longPriceChannelLength.ValueInt.ToString(), _longPriceChannelLength.ValueInt.ToString() }, "Prime");
                screener.CreateCandleIndicator(3, "PriceChannel",
                    new List<string>() { _shortPriceChannelLength.ValueInt.ToString(), _shortPriceChannelLength.ValueInt.ToString() }, "Prime");
                screener.CreateCandleIndicator(4, "RSI",
                    new List<string>() { _rsiLength.ValueInt.ToString() }, "Second");
            }

            // Точки входа в логику по режимам запуска

            if (startProgram == StartProgram.IsOsTrader)
            {
                _logicTimer = new System.Threading.Timer(LogicTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

                for (int i = 0; i < _sectors.Count; i++)
                {
                    _sectors[i].Screener.CandleFinishedEvent += Screener_CandleFinishedEvent;
                }
            }

            if (startProgram == StartProgram.IsTester)
            {
                List<IServer> servers = ServerMaster.GetServers();

                if (servers != null
                    && servers.Count > 0
                    && servers[0].ServerType == ServerType.Tester)
                {
                    TesterServer server = (TesterServer)servers[0];
                    server.EndNextMinuteWithCandlesEvent += Server_EndNextMinuteWithCandlesEvent;
                }
                else if (servers != null
                    && servers.Count > 0
                    && servers[0].ServerType == ServerType.Optimizer)
                {
                    _sectors[0].Screener.CandleFinishedEvent += Screener_CandleFinishedEventInOptimizer;
                }
            }

            if (startProgram == StartProgram.IsOsOptimizer)
            {
                _sectors[0].Screener.CandleFinishedEvent += Screener_CandleFinishedEventInOptimizer;
            }

            // События позиций - во всех режимах
            for (int i = 0; i < _sectors.Count; i++)
            {
                _sectors[i].Screener.PositionOpeningSuccesEvent += Screener_PositionOpeningSuccesEvent;
            }

            // Вкладка Auto deploy

            if (startProgram == StartProgram.IsOsTrader)
            {
                _portfolioNum = CreateParameter("Portfolio number", "", "Auto deploy");
                _deployTimeFrame = CreateParameter("Deploy time frame", "Min30",
                    new[] { "Min1", "Min5", "Min15", "Min30", "Hour1" }, "Auto deploy");
                StrategyParameterButton buttonAutoDeploy = CreateParameterButton("Deploy standard securities", "Auto deploy");
                buttonAutoDeploy.UserClickOnButtonEvent += ButtonAutoDeploy_UserClickOnButtonEvent;
            }

            // в тестере и оптимизаторе набор параметров обязан совпадать:
            // оптимизатор при одиночном прогоне сверяет количество параметров с эталонным ботом
            if (startProgram == StartProgram.IsTester
                || startProgram == StartProgram.IsOsOptimizer)
            {
                _testerDeployTimeFrame = CreateParameter("Tester deploy time frame", "Min30",
                    new[] { "Min1", "Min2", "Min3", "Min5", "Min10", "Min15", "Min20", "Min30", "Min45", "Hour1" }, "Auto deploy");
                StrategyParameterButton buttonAutoDeployTester = CreateParameterButton("Deploy tester securities", "Auto deploy");
                buttonAutoDeployTester.UserClickOnButtonEvent += ButtonAutoDeployTester_UserClickOnButtonEvent;
            }

            // Таблица монитора - в реале и тестере, в оптимизаторе не создаём

            if (startProgram != StartProgram.IsOsOptimizer)
            {
                this.ParamGuiSettings.Height = 800;
                this.ParamGuiSettings.Width = 780;

                CustomTabToParametersUi customTabMonitor = ParamGuiSettings.CreateCustomTab(" Monitor ");
                CreateColumnsTable();
                customTabMonitor.AddChildren(_hostTable);
            }

            ParametrsChangeByUser += SectorsSetAlligatorTurtle_ParametrsChangeByUser;

            DeleteEvent += SectorsSetAlligatorTurtle_DeleteEvent;

            Description = OsLocalization.ConvertToLocString(
                "Eng:Sectoral trend turtle robot. Nine screeners by economy sectors, sector rating by average RSI, entries only in top sectors. Long breakout of the long PriceChannel above Alligator with lines in Williams order, entry by stop-market iceberg, exit by manual iceberg trailing on the short PriceChannel_" +
                "Ru:Секторальный трендовый робот-черепаха. Девять скринеров по секторам экономики, рейтинг секторов по среднему RSI, входы только в топ-секторах. Лонг на пробое длинного PriceChannel выше Аллигатора с порядком линий по Вильямсу, вход стоп-айсбергом, выход ручным трейлингом айсбергом по короткому PriceChannel_");
        }

        private void CreateSectors()
        {
            CreateSector("Oil&Gas", new string[] { "GAZP", "LKOH", "ROSN", "NVTK", "TATN", "SNGS", "SNGSP", "TATNP", "TRNFP", "BANEP" });
            CreateSector("Finance", new string[] { "SBER", "SBERP", "VTBR", "MOEX", "BSPB" });
            CreateSector("Metals", new string[] { "PLZL", "GMKN", "ALRS", "MAGN", "CHMF", "NLMK", "MTLR", "SELG", "TRMK" });
            CreateSector("Consumer", new string[] { "MGNT", "SVAV" });
            CreateSector("Power", new string[] { "FEES" });
            CreateSector("Transport", new string[] { "AFLT", "FESH" });
            CreateSector("Telecom", new string[] { "MTSS", "RTKM" });
            CreateSector("Chemistry", new string[] { "PHOR" });
            CreateSector("IT", new string[] { "OZON", "YDEX", "VKCO", "ASTR", "POSI", "HEAD", "CNRU" });
        }

        private void CreateSector(string sectorName, string[] tickers)
        {
            TabCreate(BotTabType.Screener);

            SectorDataAlligator sector = new SectorDataAlligator();
            sector.Name = sectorName;
            sector.Screener = TabsScreener[TabsScreener.Count - 1];
            sector.Tickers = tickers;

            _sectors.Add(sector);
        }

        private void SectorsSetAlligatorTurtle_ParametrsChangeByUser()
        {
            for (int i = 0; i < _sectors.Count; i++)
            {
                BotTabScreener screener = _sectors[i].Screener;

                screener._indicators[0].Parameters
                    = new List<string>() {
                        _alligatorJawLength.ValueInt.ToString(),
                        _alligatorTeethLength.ValueInt.ToString(),
                        _alligatorLipsLength.ValueInt.ToString(),
                        "8", "5", "3"
                    };
                screener._indicators[1].Parameters
                    = new List<string>() { _longPriceChannelLength.ValueInt.ToString(), _longPriceChannelLength.ValueInt.ToString() };
                screener._indicators[2].Parameters
                    = new List<string>() { _shortPriceChannelLength.ValueInt.ToString(), _shortPriceChannelLength.ValueInt.ToString() };
                screener._indicators[3].Parameters
                    = new List<string>() { _rsiLength.ValueInt.ToString() };

                screener.UpdateIndicatorsParameters();
            }
        }

        private void SectorsSetAlligatorTurtle_DeleteEvent()
        {
            try
            {
                _tradePeriodsSettings.Delete();
            }
            catch (Exception)
            {
                // игнорируем
            }
        }

        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            try
            {
                _tradePeriodsSettings.ShowDialog();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Auto deploy

        private void ButtonAutoDeploy_UserClickOnButtonEvent()
        {
            try
            {
                SetStandardSecurities();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void SetStandardSecurities()
        {
            // коннектор ищем по номеру портфеля среди всех серверов
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.ConvertToLocString(
                "Eng:Auto deploy will set the standard sector securities to the sources. Current sources settings will be overwritten. Continue_" +
                "Ru:Авто-развёртывание пропишет в источники стандартные бумаги по секторам. Текущие настройки источников будут перезаписаны. Продолжить_"));

            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                return;
            }

            string portfolioName = _portfolioNum.ValueString;

            if (string.IsNullOrEmpty(portfolioName))
            {
                CustomMessageBoxUi uiInfo = new CustomMessageBoxUi(OsLocalization.ConvertToLocString(
                    "Eng:First set the portfolio number in the Auto deploy tab_" +
                    "Ru:Сначала укажите номер портфеля на вкладке Auto deploy_"));
                uiInfo.ShowDialog();
                return;
            }

            List<AServer> servers = ServerMaster.GetAServers();

            if (servers == null || servers.Count == 0)
            {
                SendNewLogMessage("Нет подключённых серверов", LogMessageType.Error);
                return;
            }

            AServer myServer = null;
            Portfolio myPortfolio = null;

            for (int i = 0; i < servers.Count; i++)
            {
                List<Portfolio> portfolios = servers[i].Portfolios;

                if (portfolios == null || portfolios.Count == 0)
                {
                    continue;
                }

                for (int j = 0; j < portfolios.Count; j++)
                {
                    if (portfolios[j].Number == portfolioName)
                    {
                        myServer = servers[i];
                        myPortfolio = portfolios[j];
                        break;
                    }
                }

                if (myServer != null)
                {
                    break;
                }
            }

            if (myServer == null || myPortfolio == null)
            {
                CustomMessageBoxUi uiInfo = new CustomMessageBoxUi(OsLocalization.ConvertToLocString(
                    "Eng:Portfolio not found. Check the portfolio number and connected servers_" +
                    "Ru:Портфель не найден. Проверьте номер портфеля и подключённые коннекторы_"));
                uiInfo.ShowDialog();
                SendNewLogMessage("Не найден портфель для развёртывания источников", LogMessageType.Error);
                return;
            }

            List<Security> securitiesAll = myServer.Securities;

            if (securitiesAll == null || securitiesAll.Count == 0)
            {
                SendNewLogMessage("В коннекторе не найдены бумаги. Возможно он не подключен", LogMessageType.Error);
                return;
            }

            TimeFrame timeFrame = TimeFrame.Min30;

            if (Enum.TryParse(_deployTimeFrame.ValueString, out TimeFrame parsedFrame))
            {
                timeFrame = parsedFrame;
            }

            for (int i = 0; i < _sectors.Count; i++)
            {
                List<Security> found = new List<Security>();

                for (int j = 0; j < _sectors[i].Tickers.Length; j++)
                {
                    Security sec = securitiesAll.Find(s => s.Name == _sectors[i].Tickers[j]
                        && s.SecurityType == SecurityType.Stock);

                    if (sec == null)
                    {
                        SendNewLogMessage("Бумага не найдена в коннекторе: " + _sectors[i].Tickers[j], LogMessageType.Error);
                        continue;
                    }

                    found.Add(sec);
                }

                DeployScreener(_sectors[i].Screener, found, myPortfolio.Number,
                    myServer.ServerType, myServer.ServerNameAndPrefix, timeFrame);
            }

            SendNewLogMessage("Auto deploy done", LogMessageType.System);
        }

        private void ButtonAutoDeployTester_UserClickOnButtonEvent()
        {
            try
            {
                SetTesterSecurities();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void SetTesterSecurities()
        {
            // бумаги берём из выбранного в тестере сета, имена файловые ("SBER.txt")
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

            if (securitiesAll == null || securitiesAll.Count == 0)
            {
                SendNewLogMessage("В тестере не найдены бумаги. Сначала выберите сет и дождитесь загрузки", LogMessageType.Error);
                return;
            }

            if (server.Portfolios == null || server.Portfolios.Count == 0)
            {
                SendNewLogMessage("В тестере не найден портфель", LogMessageType.Error);
                return;
            }

            Portfolio myPortfolio = server.Portfolios[0];

            TimeFrame timeFrame = TimeFrame.Min30;

            if (Enum.TryParse(_testerDeployTimeFrame.ValueString, out TimeFrame parsedFrame))
            {
                timeFrame = parsedFrame;
            }

            for (int i = 0; i < _sectors.Count; i++)
            {
                List<Security> found = new List<Security>();

                for (int j = 0; j < _sectors[i].Tickers.Length; j++)
                {
                    Security sec = securitiesAll.Find(s => s.Name == _sectors[i].Tickers[j] + ".txt");

                    if (sec == null)
                    {
                        continue;
                    }

                    found.Add(sec);
                }

                DeployScreener(_sectors[i].Screener, found, myPortfolio.Number,
                    server.ServerType, server.ServerNameAndPrefix, timeFrame, true);
            }

            SendNewLogMessage("Auto deploy tester done", LogMessageType.System);
        }

        private void DeployScreener(BotTabScreener screener, List<Security> securities,
            string portfolioName, ServerType serverType, string serverName, TimeFrame timeFrame, bool isTester = false)
        {
            if (securities == null || securities.Count == 0)
            {
                return;
            }

            screener.SecuritiesClass = securities[0].NameClass;
            screener.TimeFrame = timeFrame;
            screener.PortfolioName = portfolioName;
            screener.ServerType = serverType;
            screener.ServerName = serverName;

            if (isTester)
            { // в тестере ставим комиссию, как у брокера
                screener.CommissionType = CommissionType.Percent;
                screener.CommissionValue = 0.04m;
            }

            screener.CandleCreateMethodType = CandleCreateMethodType.Simple.ToString();
            ((Simple)screener.CandleSeriesRealization).TimeFrame = timeFrame;
            ((Simple)screener.CandleSeriesRealization).TimeFrameParameter.ValueString = timeFrame.ToString();

            for (int i = 0; i < securities.Count; i++)
            {
                if (screener.SecuritiesNames.Find(s => s.SecurityName == securities[i].Name) == null)
                {
                    ActivatedSecurity sec = new ActivatedSecurity();
                    sec.SecurityClass = securities[i].NameClass;
                    sec.SecurityName = securities[i].Name;
                    sec.IsOn = true;
                    screener.SecuritiesNames.Add(sec);
                }
            }

            screener.SaveSettings();
            screener.NeedToReloadTabs = true;
        }

        #endregion

        #region Logic entry points

        private void Screener_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            try
            {
                // реал: событие свечи любого скринера взводит одноразовый таймер на 5 секунд
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
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void LogicTimerCallback(object state)
        {
            try
            {
                lock (_logicTimerLocker)
                {
                    _logicTimerStarted = false;
                }

                Logic();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void Screener_CandleFinishedEventInOptimizer(List<Candle> candles, BotTabSimple source)
        {
            // оптимизатор: первая свеча любого таба - триггер подписки на серверное событие
            try
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
                    server.EndNextMinuteWithCandlesEvent += Server_EndNextMinuteWithCandlesEvent;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void Server_EndNextMinuteWithCandlesEvent()
        {
            // тестер и оптимизатор: логика от серверного события, все свечи уже обновлены
            try
            {
                Logic();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void Screener_PositionOpeningSuccesEvent(Position pos, BotTabSimple tab)
        {
            // стопы здесь не выставляются. Если достигнут лимит позиций - отменяем заявки на вход везде
            try
            {
                if (CountOpenPositionsTotal() >= _maxPositions.ValueInt)
                {
                    for (int i = 0; i < _sectors.Count; i++)
                    {
                        for (int j = 0; j < _sectors[i].Screener.Tabs.Count; j++)
                        {
                            _sectors[i].Screener.Tabs[j].BuyAtStopCancel();
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void Logic()
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (StartProgram != StartProgram.IsOsOptimizer)
            {
                UpdateSectorRating();
                TryUpdateTable();
            }

            if (StartProgram == StartProgram.IsOsOptimizer)
            {
                UpdateSectorRating();
            }

            for (int i = 0; i < _sectors.Count; i++)
            {
                SectorDataAlligator sector = _sectors[i];

                for (int j = 0; j < sector.Screener.Tabs.Count; j++)
                {
                    LogicOnTab(sector, sector.Screener.Tabs[j]);
                }
            }
        }

        private void LogicOnTab(SectorDataAlligator sector, BotTabSimple tab)
        {
            // коннектор может быть ещё не прикреплён к табу (старт, переподключение источника)
            if (tab.Connector == null)
            {
                return;
            }

            List<Candle> candles = tab.CandlesFinishedOnly;

            if (candles == null || candles.Count < 10)
            {
                return;
            }

            // guard "новая свеча": обрабатываем таб раз на закрытую свечу
            DateTime lastCandleTime = candles[candles.Count - 1].TimeStart;

            if (_lastLogicCandleBySecurity.TryGetValue(tab.Connector.SecurityName, out DateTime lastProcessed)
                && lastProcessed == lastCandleTime)
            {
                return;
            }

            _lastLogicCandleBySecurity[tab.Connector.SecurityName] = lastCandleTime;

            if (_tradePeriodsSettings.CanTradeThisTime(tab.TimeServerCurrent) == false)
            {
                // в неторговое время отменяем заявки на вход и деактивируем стопы позиций
                tab.BuyAtStopCancel();
                SetStopsActive(sector.Screener.PositionsOpenAll, false);
                return;
            }

            // в торговое время включаем стопы позиций обратно
            SetStopsActive(sector.Screener.PositionsOpenAll, true);

            int candlesNeed = Math.Max(Math.Max(Math.Max(_alligatorJawLength.ValueInt, _longPriceChannelLength.ValueInt),
                _shortPriceChannelLength.ValueInt), _rsiLength.ValueInt) + 5;

            if (candles.Count < candlesNeed)
            {
                return;
            }

            Aindicator alligator = (Aindicator)tab.Indicators[0];
            Aindicator pcLong = (Aindicator)tab.Indicators[1];
            Aindicator pcShort = (Aindicator)tab.Indicators[2];
            Aindicator rsi = (Aindicator)tab.Indicators[3];

            if (alligator.DataSeries[0].Values.Count < candles.Count
                || pcLong.DataSeries[0].Values.Count < candles.Count
                || pcShort.DataSeries[0].Values.Count < candles.Count
                || rsi.DataSeries[0].Values.Count < candles.Count)
            {
                return;
            }

            List<Position> positions = tab.PositionsOpenAll;

            if (positions.Count == 0)
            { // Логика открытия
                if (sector.InTop
                    && PassesEntryFilter(sector, tab))
                {
                    LogicOpenLong(candles, tab, sector, alligator, pcLong);
                }
            }
            else
            { // Логика закрытия позиции
                LogicCloseLong(tab, pcShort, positions[0]);
            }
        }

        #endregion

        #region Sector rating

        private void UpdateSectorRating()
        {
            // рейтинг секторов по среднему RSI бумаг. Бумага без прогретого RSI в среднем не участвует
            for (int i = 0; i < _sectors.Count; i++)
            {
                SectorDataAlligator sector = _sectors[i];

                decimal summRsi = 0;
                int countRsi = 0;

                for (int j = 0; j < sector.Screener.Tabs.Count; j++)
                {
                    BotTabSimple tab = sector.Screener.Tabs[j];

                    if (tab.Indicators == null || tab.Indicators.Count < 4)
                    {
                        continue;
                    }

                    Aindicator rsi = (Aindicator)tab.Indicators[3];

                    if (rsi.DataSeries == null
                        || rsi.DataSeries.Count == 0
                        || rsi.DataSeries[0].Values == null
                        || rsi.DataSeries[0].Values.Count == 0)
                    {
                        continue;
                    }

                    decimal lastRsi = rsi.DataSeries[0].Last;

                    if (lastRsi == 0)
                    {
                        continue;
                    }

                    summRsi += lastRsi;
                    countRsi++;
                }

                // сектор без прогретых бумаг уходит вниз рейтинга
                sector.AverageRsi = countRsi > 0 ? summRsi / countRsi : -1;
            }

            List<SectorDataAlligator> ranked = new List<SectorDataAlligator>(_sectors);
            ranked.Sort((a, b) => b.AverageRsi.CompareTo(a.AverageRsi));

            for (int i = 0; i < ranked.Count; i++)
            {
                ranked[i].Rank = i + 1;
                ranked[i].InTop = ranked[i].Rank <= _topSectorsCount.ValueInt
                    && ranked[i].AverageRsi >= 0;
            }
        }

        #endregion

        #region Trade logic

        // Фильтр отбора бумаги внутри сектора: None - без фильтра,
        // Strongest - только бумага с максимальным RSI в секторе, Weakest - с минимальным
        private bool PassesEntryFilter(SectorDataAlligator sector, BotTabSimple tab)
        {
            if (_monitorEntryFilter.ValueString == "None")
            {
                return true;
            }

            decimal tabRsi = GetLastRsi(tab);

            if (tabRsi == 0)
            {
                return false;
            }

            decimal maxRsi = 0;
            decimal minRsi = 0;
            bool hasWarmed = false;

            for (int i = 0; i < sector.Screener.Tabs.Count; i++)
            {
                decimal rsi = GetLastRsi(sector.Screener.Tabs[i]);

                if (rsi == 0)
                {
                    continue;
                }

                if (hasWarmed == false)
                {
                    maxRsi = rsi;
                    minRsi = rsi;
                    hasWarmed = true;
                    continue;
                }

                if (rsi > maxRsi)
                {
                    maxRsi = rsi;
                }

                if (rsi < minRsi)
                {
                    minRsi = rsi;
                }
            }

            if (hasWarmed == false)
            {
                return false;
            }

            if (_monitorEntryFilter.ValueString == "Strongest")
            {
                return tabRsi >= maxRsi;
            }

            return tabRsi <= minRsi;
        }

        private decimal GetLastRsi(BotTabSimple tab)
        {
            if (tab.Indicators == null || tab.Indicators.Count < 4)
            {
                return 0;
            }

            Aindicator rsi = (Aindicator)tab.Indicators[3];

            if (rsi.DataSeries == null
                || rsi.DataSeries.Count == 0
                || rsi.DataSeries[0].Values == null
                || rsi.DataSeries[0].Values.Count == 0)
            {
                return 0;
            }

            return rsi.DataSeries[0].Last;
        }

        private void LogicOpenLong(List<Candle> candles, BotTabSimple tab, SectorDataAlligator sector,
            Aindicator alligator, Aindicator pcLong)
        {
            if (CountOpenPositionsTotal() >= _maxPositions.ValueInt)
            {
                return;
            }

            // жёстко одна позиция на сектор
            if (sector.Screener.PositionsOpenAll.Count > 0)
            {
                return;
            }

            // Серии Alligator: 0 - Jaw, 1 - Teeth, 2 - Lips
            decimal jaw = alligator.DataSeries[0].Last;
            decimal teeth = alligator.DataSeries[1].Last;
            decimal lips = alligator.DataSeries[2].Last;

            // Серии PriceChannel: 0 - верхняя граница, 1 - нижняя граница
            decimal pcLongUp = pcLong.DataSeries[0].Last;

            // нулевые значения = индикатор не прогрет, не торгуем
            if (jaw == 0
                || teeth == 0
                || lips == 0
                || pcLongUp == 0)
            {
                return;
            }

            // условия по каналу проверяем вторым с конца:
            // последнее значение границы перестраивается по экстремуму текущей свечи
            if (pcLong.DataSeries[0].Values.Count < 2)
            {
                return;
            }

            decimal pcLongUpPrev = pcLong.DataSeries[0].Values[pcLong.DataSeries[0].Values.Count - 2];

            if (pcLongUpPrev == 0)
            {
                return;
            }

            decimal lastClose = candles[candles.Count - 1].Close;

            // цена выше всех линий Аллигатора
            if (lastClose <= jaw
                || lastClose <= teeth
                || lastClose <= lips)
            {
                return;
            }

            // порядок линий по Вильямсу: Lips > Teeth > Jaw (пасть открыта вверх)
            if (lips <= teeth
                || teeth <= jaw)
            {
                return;
            }

            // цена ещё под верхней границей длинного канала
            if (lastClose >= pcLongUpPrev)
            {
                return;
            }

            decimal volume = GetVolume(tab);

            if (volume == 0)
            {
                return;
            }

            // перед перевыставлением отменяем предыдущую заявку
            tab.BuyAtStopCancel();

            // заявка стоп-маркет айсберг: цена активации = верхняя граница длинного PriceChannel, жизнь заявки - 1 свеча
            tab.BuyAtStopMarketIceberg(volume, pcLongUp, pcLongUp,
                StopActivateType.HigherOrEqual, 1, "LongEntry",
                PositionOpenerToStopLifeTimeType.CandlesCount,
                _icebergOrdersCount.ValueInt, _icebergMillisecondsDistance.ValueInt);
        }

        private void LogicCloseLong(BotTabSimple tab, Aindicator pcShort, Position position)
        {
            // ручной трейлинг стопом по нижней границе короткого PriceChannel, только в сторону прибыли
            if (position.State != PositionStateType.Open)
            {
                return;
            }

            decimal exitPrice = pcShort.DataSeries[1].Last;

            if (exitPrice == 0)
            {
                return;
            }

            // перестановка стопа только в сторону прибыли (для лонга - вверх)
            if (position.StopOrderPrice == 0
                || exitPrice > position.StopOrderPrice)
            {
                tab.CloseAtStopMarketIceberg(position, exitPrice,
                    _icebergOrdersCount.ValueInt, _icebergMillisecondsDistance.ValueInt);
            }
        }

        private void SetStopsActive(List<Position> positions, bool isActive)
        {
            // трогаем только позиции в состоянии Open
            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (positions[i].StopOrderPrice != 0)
                {
                    positions[i].StopOrderIsActive = isActive;
                }
            }
        }

        #endregion

        #region Positions and volume

        private int CountOpenPositionsTotal()
        {
            int count = 0;

            for (int i = 0; i < _sectors.Count; i++)
            {
                count += _sectors[i].Screener.PositionsOpenAll.Count;
            }

            return count;
        }

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
                else // Тестер или оптимизатор
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
                    if (StartProgram != StartProgram.IsOsOptimizer)
                    {
                        SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, LogMessageType.Error);
                    }
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

        #region Monitor UI

        private WindowsFormsHost _hostTable;
        private DataGridView _tableDataGrid;
        private DateTime _lastTimeUpdateTable = DateTime.MinValue;

        private void CreateColumnsTable()
        {
            // 0 Sector / 1 Ticker / 2 RSI / 3 Sector RSI / 4 Rank / 5 In top / 6 Chart
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

                DataGridViewTextBoxCell cellTemplate = new DataGridViewTextBoxCell();
                cellTemplate.Style = _tableDataGrid.DefaultCellStyle;
                cellTemplate.Style.WrapMode = DataGridViewTriState.True;

                string[] headers = new string[] { "Sector", "Ticker", "RSI", "Sector RSI", "Rank", "In top", "Chart" };

                for (int i = 0; i < headers.Length; i++)
                {
                    DataGridViewColumn column = new DataGridViewColumn();
                    column.CellTemplate = cellTemplate;
                    column.HeaderText = headers[i];
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    _tableDataGrid.Columns.Add(column);
                }

                _tableDataGrid.DataError += _tableDataGrid_DataError;
                _tableDataGrid.CellClick += _tableDataGrid_CellClick;

                _hostTable.Child = _tableDataGrid;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _tableDataGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;
                int column = e.ColumnIndex;

                if (row < 0 || row >= _tableDataGrid.Rows.Count)
                {
                    return;
                }

                if (column != 6)
                { // интересует только кнопка Chart
                    return;
                }

                string sectorName = _tableDataGrid.Rows[row].Cells[0].Value?.ToString();
                string secName = _tableDataGrid.Rows[row].Cells[1].Value?.ToString();

                if (string.IsNullOrEmpty(sectorName) || string.IsNullOrEmpty(secName))
                {
                    return;
                }

                SectorDataAlligator sector = _sectors.Find(s => s.Name == sectorName);

                if (sector == null)
                {
                    return;
                }

                int tabNumber = -1;

                for (int i = 0; i < sector.Screener.Tabs.Count; i++)
                {
                    if (sector.Screener.Tabs[i].Connector != null
                        && sector.Screener.Tabs[i].Connector.SecurityName == secName)
                    {
                        tabNumber = i;
                        break;
                    }
                }

                if (tabNumber == -1)
                {
                    return;
                }

                sector.Screener.ShowChart(tabNumber);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _tableDataGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            SendNewLogMessage(sender.ToString(), LogMessageType.Error);
        }

        private void TryUpdateTable()
        {
            try
            {
                if (_tableDataGrid == null)
                {
                    return;
                }

                if (_tableDataGrid.InvokeRequired)
                {
                    _tableDataGrid.Invoke(new Action(TryUpdateTable));
                    return;
                }

                // не чаще 1 раза в секунду
                if (_lastTimeUpdateTable != DateTime.MinValue
                    && _lastTimeUpdateTable.AddSeconds(1) > DateTime.Now)
                {
                    return;
                }

                _lastTimeUpdateTable = DateTime.Now;

                int totalTabs = 0;

                for (int i = 0; i < _sectors.Count; i++)
                {
                    for (int j = 0; j < _sectors[i].Screener.Tabs.Count; j++)
                    {
                        if (_sectors[i].Screener.Tabs[j].Connector != null)
                        {
                            totalTabs++;
                        }
                    }
                }

                if (totalTabs == 0)
                {
                    return;
                }

                if (_tableDataGrid.Rows.Count != totalTabs)
                { // полное перестроение
                    _tableDataGrid.Rows.Clear();

                    List<SectorDataAlligator> ranked = new List<SectorDataAlligator>(_sectors);
                    ranked.Sort((a, b) => a.Rank.CompareTo(b.Rank));

                    for (int i = 0; i < ranked.Count; i++)
                    {
                        for (int j = 0; j < ranked[i].Screener.Tabs.Count; j++)
                        {
                            if (ranked[i].Screener.Tabs[j].Connector == null)
                            {
                                continue;
                            }

                            _tableDataGrid.Rows.Add(GetRow(ranked[i], ranked[i].Screener.Tabs[j]));
                        }
                    }

                    return;
                }

                // обновление изменившихся ячеек
                for (int i = 0; i < _tableDataGrid.Rows.Count; i++)
                {
                    DataGridViewRow currentRow = _tableDataGrid.Rows[i];

                    string secName = currentRow.Cells[1].Value?.ToString();

                    if (string.IsNullOrEmpty(secName))
                    {
                        continue;
                    }

                    SectorDataAlligator sector = null;
                    BotTabSimple tab = null;

                    for (int s = 0; s < _sectors.Count && tab == null; s++)
                    {
                        tab = _sectors[s].Screener.Tabs.Find(t => t.Connector != null
                            && t.Connector.SecurityName == secName);

                        if (tab != null)
                        {
                            sector = _sectors[s];
                        }
                    }

                    if (tab == null || sector == null)
                    {
                        continue;
                    }

                    DataGridViewRow newRow = GetRow(sector, tab);

                    for (int c = 2; c <= 5; c++)
                    {
                        if (currentRow.Cells[c].Value == null
                            || currentRow.Cells[c].Value.ToString() != newRow.Cells[c].Value.ToString())
                        {
                            currentRow.Cells[c].Value = newRow.Cells[c].Value;
                        }
                    }

                    // цвет сектора мог поменять чётность после переранжирования
                    System.Drawing.Color backColor = GetSectorBackColor(sector);

                    for (int c = 0; c < currentRow.Cells.Count; c++)
                    {
                        currentRow.Cells[c].Style.BackColor = backColor;
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private DataGridViewRow GetRow(SectorDataAlligator sector, BotTabSimple tab)
        {
            // 0 Sector / 1 Ticker / 2 RSI / 3 Sector RSI / 4 Rank / 5 In top / 6 Chart
            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = sector.Name;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = tab.Connector.SecurityName;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].ReadOnly = true;

            decimal rsiValue = 0;

            if (tab.Indicators != null && tab.Indicators.Count >= 4)
            {
                Aindicator rsi = (Aindicator)tab.Indicators[3];

                if (rsi.DataSeries != null
                    && rsi.DataSeries.Count > 0
                    && rsi.DataSeries[0].Values != null
                    && rsi.DataSeries[0].Values.Count > 0)
                {
                    rsiValue = rsi.DataSeries[0].Last;
                }
            }

            row.Cells[^1].Value = Math.Round(rsiValue, 1);

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = sector.AverageRsi >= 0 ? Math.Round(sector.AverageRsi, 1).ToString() : "-";

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = sector.Rank;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = sector.InTop ? "Yes" : "No";

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = "Chart";

            // соседние сектора в таблице чередуются цветами из текущей темы
            System.Drawing.Color backColor = GetSectorBackColor(sector);

            for (int c = 0; c < row.Cells.Count; c++)
            {
                row.Cells[c].Style.BackColor = backColor;
            }

            return row;
        }

        private System.Drawing.Color GetSectorBackColor(SectorDataAlligator sector)
        {
            if (sector.Rank % 2 == 0)
            {
                return Themes.ThemeManager.GetColorWinForms("GridRowAltColor");
            }

            return Themes.ThemeManager.GetColorWinForms("MarketDepthAskBackColor");
        }

        #endregion
    }

    public class SectorDataAlligator
    {
        public string Name;

        public BotTabScreener Screener;

        public string[] Tickers;

        public decimal AverageRsi = -1;

        public int Rank = 100;

        public bool InTop = false;
    }
}
