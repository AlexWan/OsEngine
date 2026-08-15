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
using System.Threading;

using PairInPosition = (OsEngine.OsTrader.Panels.Tab.BotTabSimple Base, OsEngine.OsTrader.Panels.Tab.BotTabSimple Futures);
using Pretender = (OsEngine.OsTrader.Panels.Tab.BotTabSimple Base, OsEngine.OsTrader.Panels.Tab.BotTabSimple Futures, decimal Mult);

/*

Арбитраж синтетических облигаций. Контанго-арбитраж на рынке фьючерсов на акции MOEX

Конструкция позиции (синтетическая облигация в контанго)
Лонг акция (база) + Шорт фьючерс

Источники
15 пар источников. В каждой паре BotTabSimple - базовая акция, BotTabScreener - фьючерсы на неё.
Первые 10 пар разворачиваются кнопками авто-развёртывания (Т-Банк в реале, выбранный сет в тестере).
Последние 5 пар - запасные слоты, настраиваются вручную

ВХОД в позицию
Доходность синтетической облигации (контанго, пересчитанное в % годовых) больше Min Yield To Entry.
Из всех претендентов выбирается пара с максимальной доходностью

ПЕРЕНОС позиции
Если доходность у претендента больше текущего на Min Yield To Entry,
позиция закрывается и открывается на более доходной паре

ВЫХОД из позиции
1) Накануне экспирации фьючерса
2) Аварийный выход, если открылась только одна нога
3) Если позиций стало больше одной, закрывается худшая

*/

namespace OsEngine.Robots.SyntheticBond
{
    [Bot("SyntheticBondsArbitrage")]
    public class SyntheticBondsArbitrage : BotPanel
    {
        private StrategyParameterString _regime;
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodButton;

        private StrategyParameterDecimal _minYieldToEntry;

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

        private StrategyParameterString _portfolioNum;

        public SyntheticBondsArbitrage(string name, StartProgram startProgram) : base(name, startProgram)
        {
            CreateSources();

            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" }, "Base");
            _minYieldToEntry = CreateParameter("Min Yield To Entry % ann", 20m, 1.0m, 100, 1, "Base");

            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Execution");
            _volume = CreateParameter("Volume", 0.5m, 1.0m, 50, 4, "Execution");
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Execution");

            _tradePeriodsSettings = new NonTradePeriods(name);
            _tradePeriodButton = CreateParameterButton("Clearing", "No trade periods");
            _tradePeriodButton.UserClickOnButtonEvent += _tradePeriodButton_UserClickOnButtonEvent;

            _futuresMult1 = CreateParameter("Fut mult 2", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult2 = CreateParameter("Fut mult 4", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult3 = CreateParameter("Fut mult 6", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult4 = CreateParameter("Fut mult 8", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult5 = CreateParameter("Fut mult 10", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult6 = CreateParameter("Fut mult 12", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult7 = CreateParameter("Fut mult 14", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult8 = CreateParameter("Fut mult 16", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult9 = CreateParameter("Fut mult 18", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult10 = CreateParameter("Fut mult 20", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult11 = CreateParameter("Fut mult 22", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult12 = CreateParameter("Fut mult 24", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult13 = CreateParameter("Fut mult 26", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult14 = CreateParameter("Fut mult 28", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult15 = CreateParameter("Fut mult 30", 1m, 1.0m, 50, 4, "Fut mults");

            if (startProgram == StartProgram.IsOsTrader)
            {
                _portfolioNum = CreateParameter("Portfolio number", "", "Auto deploy");
                StrategyParameterButton buttonAutoDeploy = CreateParameterButton("Deploy standard securities", "Auto deploy");
                buttonAutoDeploy.UserClickOnButtonEvent += ButtonAutoDeploy_UserClickOnButtonEvent;

                _logicTimer = new Timer(LogicTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

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
                StrategyParameterButton buttonAutoDeployTester = CreateParameterButton("Deploy tester securities", "Tester deploy");
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
              "Eng:Arbitrage of synthetic bonds on the MOEX stock futures market. Long stock plus short futures when the annualized contango yield exceeds the threshold. The position is moved to a more profitable contract and closed before expiration_" +
              "Ru:Арбитраж синтетических облигаций на рынке фьючерсов на акции MOEX. Лонг акция плюс шорт фьючерс при превышении доходностью контанго в годовых заданного порога. Позиция переносится на более доходный контракт и закрывается перед экспирацией_");
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

        private void ServerT_EndNextMinuteWithCandlesEvent()
        {
            Logic();
        }

        private void _tradePeriodButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        #region Logic entry synchronization in real

        private Timer _logicTimer;
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

            List<Pretender> pretenders = GetPretenders();

            if (pairsInPosition.Count > 0)
            {
                PairInPosition pair = pairsInPosition[0];

                if (TryExitByErrorEntry(pair.Base, pair.Futures))
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
            decimal bestDeviation = 0;

            for (int i = 0; i < pretenders.Count; i++)
            {
                decimal curDeviation = CalculateAnnualizedYieldContango(pretenders[i].Base, pretenders[i].Futures, pretenders[i].Mult);

                if (curDeviation > bestDeviation)
                {
                    bestDeviation = curDeviation;
                    bestBase = pretenders[i].Base;
                    bestFutures = pretenders[i].Futures;
                }
            }

            if (bestBase == null
                || bestDeviation < _minYieldToEntry.ValueDecimal)
            {
                return;
            }

            EntryInPositionContango(bestBase, bestFutures);
        }

        private bool TryExitByExpiration(BotTabSimple baseSource, BotTabSimple futuresSource)
        {
            int daysToExpiration = (futuresSource.Security.Expiration - futuresSource.TimeServerCurrent).Days;

            if (daysToExpiration <= 1
                && futuresSource.TimeServerCurrent.Hour == 10)
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
            decimal bestDeviation = 0;

            for (int i = 0; i < pretenders.Count; i++)
            {
                decimal curDeviation = CalculateAnnualizedYieldContango(pretenders[i].Base, pretenders[i].Futures, pretenders[i].Mult);

                if (curDeviation > bestDeviation)
                {
                    bestDeviation = curDeviation;
                    bestBase = pretenders[i].Base;
                    bestFutures = pretenders[i].Futures;
                }
            }

            if (bestBase == null)
            {
                return;
            }

            decimal curDev = CalculateAnnualizedYieldContango(baseInPosition, futuresInPosition, GetMultByBase(baseInPosition));

            if (curDev >= bestDeviation
                || bestDeviation <= 0
                || curDev == 0)
            {
                return;
            }

            decimal diff = bestDeviation - curDev;

            if (diff > _minYieldToEntry.ValueDecimal)
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

        private decimal GetMultByBase(BotTabSimple baseSource)
        {            if (baseSource == _base1) return _futuresMult1.ValueDecimal;
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

        private List<Pretender> GetPretenders()
        {
            List<Pretender> result = new List<Pretender>();

            AddPretenderBySecurity(_base1, _futs1, _futuresMult1.ValueDecimal, result);
            AddPretenderBySecurity(_base2, _futs2, _futuresMult2.ValueDecimal, result);
            AddPretenderBySecurity(_base3, _futs3, _futuresMult3.ValueDecimal, result);
            AddPretenderBySecurity(_base4, _futs4, _futuresMult4.ValueDecimal, result);
            AddPretenderBySecurity(_base5, _futs5, _futuresMult5.ValueDecimal, result);
            AddPretenderBySecurity(_base6, _futs6, _futuresMult6.ValueDecimal, result);
            AddPretenderBySecurity(_base7, _futs7, _futuresMult7.ValueDecimal, result);
            AddPretenderBySecurity(_base8, _futs8, _futuresMult8.ValueDecimal, result);
            AddPretenderBySecurity(_base9, _futs9, _futuresMult9.ValueDecimal, result);
            AddPretenderBySecurity(_base10, _futs10, _futuresMult10.ValueDecimal, result);
            AddPretenderBySecurity(_base11, _futs11, _futuresMult11.ValueDecimal, result);
            AddPretenderBySecurity(_base12, _futs12, _futuresMult12.ValueDecimal, result);
            AddPretenderBySecurity(_base13, _futs13, _futuresMult13.ValueDecimal, result);
            AddPretenderBySecurity(_base14, _futs14, _futuresMult14.ValueDecimal, result);
            AddPretenderBySecurity(_base15, _futs15, _futuresMult15.ValueDecimal, result);

            return result;
        }

        private void AddPretenderBySecurity(
             BotTabSimple baseSource, BotTabScreener screener, decimal mult, List<Pretender> result)
        {
            if (string.IsNullOrEmpty(baseSource.Connector?.SecurityName))
            {
                return;
            }

            if (baseSource.PositionsOpenAll.Count > 0)
            {
                return;
            }

            DateTime time = baseSource.TimeServerCurrent;

            BotTabSimple nearestFutures = null;

            for (int i = 0; i < screener.Tabs.Count; i++)
            {
                BotTabSimple curTab = screener.Tabs[i];

                if (curTab.Security == null
                    || curTab.Security.Expiration == DateTime.MinValue)
                {
                    continue;
                }

                int daysToExpiration = (curTab.Security.Expiration - time).Days;

                if (daysToExpiration <= 5)
                {
                    continue;
                }

                if (daysToExpiration > 100)
                {
                    continue;
                }

                if (nearestFutures != null
                    && nearestFutures.Security.Expiration < curTab.Security.Expiration)
                {
                    continue;
                }

                nearestFutures = curTab;
            }

            if (nearestFutures != null)
            {
                result.Add((baseSource, nearestFutures, mult));
            }
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
                else
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
    }
}
