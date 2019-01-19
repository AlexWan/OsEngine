/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.PanelsGui;
using OsEngine.OsTrader.Panels.SingleRobots;
using OsEngine.OsTrader.Panels.Tab;
using MessageBox = System.Windows.MessageBox;

namespace OsEngine.OsTrader.Panels
{

    public class PanelCreator
    {
        public static List<string> GetNamesStrategy()
        {
            List<string> result = new List<string>();

            // публичные примеры
           
            result.Add("Engine");
            result.Add("ClusterEngine");
            result.Add("PatternTrader");
            result.Add("HighFrequencyTrader");
            result.Add("MarketMakerBot");
            result.Add("PivotPointsRobot");
            result.Add("TwoLegArbitrage");
            result.Add("TwoTimeFrameBot");
            result.Add("Bollinger");
            result.Add("Williams Band");
            result.Add("Levermor");
            result.Add("PairTraderSimple");
            result.Add("RsiTrade");
            result.Add("StochasticTrade");
            result.Add("BollingerTrade");
            result.Add("TRIXTrade");
            result.Add("CCITrade");
            result.Add("ParabolicSarTrade");
            result.Add("PriceChannelTrade");
            result.Add("MACDTrade");
            result.Add("BBPowerTrade");
            result.Add("RviTrade");
            result.Add("WilliamsRangeTrade");
            result.Add("MacdTrail");
            result.Add("SmaStochastic");
            result.Add("MomentumMACD");
            result.Add("PinBarTrade");
            result.Add("PairRsiTrade");
            result.Add("OneLegArbitration");
            result.Add("ThreeSoldier");
            result.Add("BollingerOutburst");
            result.Add("PriceChannelBreak");
            result.Add("PriceChannelVolatility");
            result.Add("RsiContrtrend");
            result.Add("PairTraderSpreadSma");
            // роботы с инструкцией по созданию

            result.Add("Robot");
            result.Add("FirstBot");

            
                
            return result;
        }

        public static BotPanel GetStrategyForName(string nameClass, string name, StartProgram startProgram)
        {

            BotPanel bot = null;
            // примеры и бесплатные боты

            if (nameClass == "PatternTrader")
            {
                bot = new PatternTrader(name, startProgram);
            }
            if (nameClass == "HighFrequencyTrader")
            {
                bot = new HighFrequencyTrader(name, startProgram);
            }
            if (nameClass == "TwoTimeFrameBot")
            {
                bot = new BotWhithTwoTimeFrame(name, startProgram);
            }
            if (nameClass == "PivotPointsRobot")
            {
                bot = new PivotPointsRobot(name, startProgram);
            }
            if (nameClass == "Engine")
            {
                bot = new StrategyEngineCandle(name, startProgram);
            }
            if (nameClass == "ClusterEngine")
            {
                bot = new ClusterEngine(name, startProgram);
            }
            
            if (nameClass == "Williams Band")
            {
                bot = new StrategyBillWilliams(name, startProgram);
            }
            if (nameClass == "Levermor")
            {
                bot = new StrategyLevermor(name, startProgram);
            }
            if (nameClass == "MarketMakerBot")
            {
                bot = new MarketMakerBot(name, startProgram);
            }
            if (nameClass == "Bollinger")
            {
                bot = new StrategyBollinger(name, startProgram);
            }
            if (nameClass == "PairTraderSimple")
            {
                bot = new PairTraderSimple(name, startProgram);
            }

// под релиз
            if (nameClass == "RsiTrade")
            {
                bot = new RsiTrade(name, startProgram);
            }
            if (nameClass == "StochasticTrade")
            {
                bot = new StochasticTrade(name, startProgram);
            }
            if (nameClass == "BollingerTrade")
            {
                bot = new BollingerTrade(name, startProgram);
            }
            if (nameClass == "TRIXTrade")
            {
                bot = new TrixTrade(name, startProgram);
            }
            if (nameClass == "CCITrade")
            {
                bot = new CciTrade(name, startProgram);
            }
            if (nameClass == "ParabolicSarTrade")
            {
                bot = new ParabolicSarTrade(name, startProgram);
            }
            if (nameClass == "PriceChannelTrade")
            {
                bot = new PriceChannelTrade(name, startProgram);
            }
            if (nameClass == "MACDTrade")
            {
                bot = new MacdTrade(name, startProgram);
            }
            if (nameClass == "BBPowerTrade")
            {
                bot = new BbPowerTrade(name, startProgram);
            }
            if (nameClass == "RviTrade")
            {
                bot = new RviTrade(name, startProgram);
            }
            if (nameClass == "WilliamsRangeTrade")
            {
                bot = new WilliamsRangeTrade(name, startProgram);
            }
            if (nameClass == "MacdTrail")
            {
                bot = new MacdTrail(name, startProgram);
            }
            if (nameClass == "SmaStochastic")
            {
                bot = new SmaStochastic(name, startProgram);
            }
            if (nameClass == "MomentumMACD")
            {
                bot = new MomentumMacd(name, startProgram);
            }
            if (nameClass == "PinBarTrade")
            {
                bot = new PinBarTrade(name, startProgram);
            }
            if (nameClass == "PairRsiTrade")
            {
                bot = new PairRsiTrade(name, startProgram);
            }
            if (nameClass == "Robot")
            {
                bot = new Robot(name, startProgram);
            }
            if (nameClass == "FirstBot")
            {
                bot = new FirstBot(name, startProgram);
            }

            if (nameClass == "TwoLegArbitrage")
            {
                bot = new TwoLegArbitrage(name, startProgram);
            }
            if (nameClass == "OneLegArbitration")
            {
                bot = new OneLegArbitration(name, startProgram);
            }
            if (nameClass == "ThreeSoldier")
            {
                bot = new ThreeSoldier(name, startProgram);
            }
            if (nameClass == "BollingerOutburst")
            {
                bot = new BollingerOutburst(name, startProgram);
            }
            if (nameClass == "RsiContrtrend")
            {
                bot = new RsiContrtrend(name, startProgram);
            }
            if (nameClass == "PriceChannelVolatility")
            {
                bot = new PriceChannelVolatility(name, startProgram);
            }
            if (nameClass == "PriceChannelBreak")
            {
                bot = new PriceChannelBreak(name, startProgram);
            }
            if (nameClass == "PairTraderSpreadSma")
            {
                bot = new PairTraderSpreadSma(name, startProgram);
            }
            

            return bot;
        }
    }

    # region примеры роботов для оптимизации

    /// <summary>
    /// робот анализирующий плотность стакана для входа
    /// </summary>
    public class HighFrequencyTrader : BotPanel // бывший MarketDepthJuggler
    {
        
        //выставляем заявки над самыми толстыми покупками и продажами. 
        //не далее чем в пяти тиков от центра стакана. По две заявки.
        //Когда одна заявка отрабатывает, снимаем все ордера из системы.
        //Выставляем профит в 10 пунктов и стоп в 5ть.

        //На нашем Ютуб канале есть видео о том как я делаю этого бота:https://www.youtube.com/playlist?list=PL76DtREkiCATe28yPbAT_5em1JqA4xEiB
        //Однако там не всё сделано, т.к. я кое-что доработал для реальной торговли

        public HighFrequencyTrader(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyClosePosition" });
            Volume = CreateParameter("Volume", 1, 1.0m, 100, 2);
            Stop = CreateParameter("Stop", 5, 5, 15, 1);
            Profit = CreateParameter("Profit", 5, 5, 20, 1);

            MaxLevelsInMarketDepth = CreateParameter("MaxLevelsInMarketDepth", 5, 3, 15, 1);

            _tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;

            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
            _tab.PositionClosingFailEvent += _tab_PositionClosingFailEvent;

            // этот поток создан для того чтобы в реальной торговле отзывать заявки
            // т.к. нужно ожидать когда у ордеров вернётся номер ордера на бирже
            // а когда у нас каждую секунду переустанавливаются ордера, этого может не 
            // успевать происходить. Особенно через наш любимый квик.

            Thread closerThread = new Thread(ClosePositionThreadArea);
            closerThread.IsBackground = true;
            closerThread.Start();
        }

        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        /// <summary>
        /// режим работы
        /// </summary>
        public StrategyParameterString Regime;

        /// <summary>
        /// объем
        /// </summary>
        public StrategyParameterDecimal Volume;

        /// <summary>
        /// глубина анализа стакана
        /// </summary>
        public StrategyParameterInt MaxLevelsInMarketDepth;

        /// <summary>
        /// длинна стопа
        /// </summary>
        public StrategyParameterInt Stop;

        /// <summary>
        /// длинна профита
        /// </summary>
        public StrategyParameterInt Profit;

        public override string GetNameStrategyType()
        {
            return "HighFrequencyTrader";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

// начало логики

        /// <summary>
        /// последнее время проверки стакана
        /// </summary>
        private DateTime _lastCheckTime = DateTime.MinValue;

        /// <summary>
        /// новый входящий стакан
        /// </summary>
        void _tab_MarketDepthUpdateEvent(MarketDepth marketDepth)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }
            if (marketDepth.Asks == null || marketDepth.Asks.Count == 0 ||
                marketDepth.Bids == null || marketDepth.Bids.Count == 0)
            {
                return;
            }

            if (_tab.PositionsOpenAll.Find(pos => pos.State == PositionStateType.Open ||
                pos.State == PositionStateType.Closing
                ) != null)
            {
                return;
            }

            if (StartProgram == StartProgram.IsOsTrader &&
                _lastCheckTime.AddSeconds(1) > DateTime.Now)
            { // в реальной торговле, проверяем стакан раз в секунду
                return;
            }

            _lastCheckTime = DateTime.Now;

            Position positionBuy = _tab.PositionsOpenAll.Find(pos => pos.Direction == Side.Buy);
            Position positionSell = _tab.PositionsOpenAll.Find(pos => pos.Direction == Side.Sell);

            decimal buyPrice = 0;
            int lastVolume = 0;

            // проверка на покупку

            for (int i = 0; i < marketDepth.Bids.Count && i < MaxLevelsInMarketDepth.ValueInt; i++)
            {
                if (marketDepth.Bids[i].Bid > lastVolume)
                {
                    buyPrice = marketDepth.Bids[i].Price + _tab.Securiti.PriceStep;
                    lastVolume = Convert.ToInt32(marketDepth.Bids[i].Bid);
                }
            }

            if (positionBuy != null &&
                positionBuy.OpenOrders[0].Price != buyPrice &&
                positionBuy.State != PositionStateType.Open &&
                positionBuy.State != PositionStateType.Closing)
            {
                if (StartProgram == StartProgram.IsOsTrader)
                { // в реальной торговле отправляем позицию на отзыв в массив, 
                    // который обрабатывается отдельным потоком, ожидая когда у ордеров позиции
                    // вернутся номера ордеров, прежде чем мы их будем пытаться отозвать
                    _positionsToClose.Add(positionBuy);

                }
                else
                { // в тестере, сразу отправляем позицию на отзыв
                    _tab.CloseAllOrderToPosition(positionBuy);
                }
                _tab.BuyAtLimit(Volume.ValueDecimal, buyPrice);
            }
            if (positionBuy == null)
            {
                _tab.BuyAtLimit(Volume.ValueDecimal, buyPrice);
            }

            // проверка на продажу

            decimal sellPrice = 0;
            int lastVolumeInAsk = 0;

            for (int i = 0; i < marketDepth.Asks.Count && i < MaxLevelsInMarketDepth.ValueInt; i++)
            {
                if (marketDepth.Asks[i].Ask > lastVolumeInAsk)
                {
                    sellPrice = marketDepth.Asks[i].Price - _tab.Securiti.PriceStep;
                    lastVolumeInAsk = Convert.ToInt32(marketDepth.Asks[i].Ask);
                }
            }

            if (positionSell != null &&
                positionSell.OpenOrders[0].Price != sellPrice &&
                positionSell.State != PositionStateType.Open &&
                positionSell.State != PositionStateType.Closing)
            {
                if (StartProgram == StartProgram.IsOsTrader)
                {
                    _positionsToClose.Add(positionSell);
                    // в реальной торговле отправляем позицию на отзыв в массив, 
                    // который обрабатывается отдельным потоком, ожидая когда у ордеров позиции
                    // вернутся номера ордеров, прежде чем мы их будем пытаться отозвать
                }
                else
                {
                    // в тестере, сразу отправляем позицию на отзыв
                    _tab.CloseAllOrderToPosition(positionSell);
                }

                _tab.SellAtLimit(Volume.ValueDecimal, sellPrice);
            }
            if (positionSell == null)
            {
                _tab.SellAtLimit(Volume.ValueDecimal, sellPrice);
            }
        }

        /// <summary>
        /// успешное открытие позиции
        /// </summary>
        void _tab_PositionOpeningSuccesEvent(Position position)
        {
            if (position.Direction == Side.Buy)
            {
                _tab.CloseAtStop(position, position.EntryPrice - Stop.ValueInt * _tab.Securiti.PriceStep, position.EntryPrice - Stop.ValueInt * _tab.Securiti.PriceStep);
                _tab.CloseAtProfit(position, position.EntryPrice + Profit.ValueInt * _tab.Securiti.PriceStep, position.EntryPrice + Profit.ValueInt * _tab.Securiti.PriceStep);
            }
            if (position.Direction == Side.Sell)
            {
                _tab.CloseAtStop(position, position.EntryPrice + Stop.ValueInt * _tab.Securiti.PriceStep, position.EntryPrice + Stop.ValueInt * _tab.Securiti.PriceStep);
                _tab.CloseAtProfit(position, position.EntryPrice - Profit.ValueInt * _tab.Securiti.PriceStep, position.EntryPrice - Profit.ValueInt * _tab.Securiti.PriceStep);
            }

            List<Position> positions = _tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].Number == position.Number)
                {
                    continue;
                }
                if (StartProgram == StartProgram.IsOsTrader)
                {
                    // в реальной торговле отправляем позицию на отзыв в массив, 
                    // который обрабатывается отдельным потоком, ожидая когда у ордеров позиции
                    // вернутся номера ордеров, прежде чем мы их будем пытаться отозвать
                    _positionsToClose.Add(positions[i]);
                }
                else
                {
                    // в тестере, сразу отправляем позицию на отзыв
                    _tab.CloseAllOrderToPosition(positions[i]);
                }
            }
        }

        /// <summary>
        /// позиция не закрылась и у неё отозваны ордера
        /// </summary>
        void _tab_PositionClosingFailEvent(Position position)
        {
            if (position.CloseActiv)
            {
                return;
            }
            _tab.CloseAtMarket(position, position.OpenVolume);
        }

// отзыв заявок в реальном подключении

        /// <summary>
        /// позиции которые нужно отозвать
        /// </summary>
        List<Position> _positionsToClose = new List<Position>();

        /// <summary>
        /// место работы потока где отзываются заявки в реальном подключении
        /// </summary>
        private void ClosePositionThreadArea()
        {
            while (true)
            {
                Thread.Sleep(1000);

                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                for (int i = 0; i < _positionsToClose.Count; i++)
                {
                    if (_positionsToClose[i].State != PositionStateType.Opening)
                    {
                        continue;
                    }

                    if (_positionsToClose[i].OpenOrders != null &&
                        !string.IsNullOrWhiteSpace(_positionsToClose[i].OpenOrders[0].NumberMarket))
                    {
                        _tab.CloseAllOrderToPosition(_positionsToClose[i]);
                        _positionsToClose.RemoveAt(i);
                        i--;
                    }
                }
            }
        }
    }

    /// <summary>
    /// трендовая стратегия Билла Вильямса на Аллигаторе и фракталах
    /// </summary>
    public class StrategyBillWilliams : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public StrategyBillWilliams(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _tab.CandleFinishedEvent += Bot_CandleFinishedEvent;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            Slipage = CreateParameter("Slipage", 0, 0, 20, 1);
            VolumeFirst = CreateParameter("FirstInterVolume", 3, 1.0m, 50, 1);
            VolumeSecond = CreateParameter("SecondInterVolume", 1, 1.0m, 50, 1);
            MaximumPositions = CreateParameter("MaxPoses", 1, 1, 10, 1);
            AlligatorFastLineLength = CreateParameter("AlligatorFastLineLength", 3, 3, 30, 1);
            AlligatorMiddleLineLength = CreateParameter("AlligatorMiddleLineLength", 10, 10, 70, 5);
            AlligatorSlowLineLength = CreateParameter("AlligatorSlowLineLength", 40, 40, 150, 10);

            _alligator = new Alligator(name + "Alligator", false);
            _alligator = (Alligator)_tab.CreateCandleIndicator(_alligator, "Prime");
            _alligator.Save();

            _alligator.LenghtDown = AlligatorSlowLineLength.ValueInt;
            _alligator.LenghtBase = AlligatorMiddleLineLength.ValueInt;
            _alligator.LenghtUp = AlligatorFastLineLength.ValueInt;

            _fractal = new Fractal(name + "Fractal", false);
            _fractal = (Fractal)_tab.CreateCandleIndicator(_fractal, "Prime");

            _aO = new AwesomeOscillator(name + "AO", false);
            _aO = (AwesomeOscillator)_tab.CreateCandleIndicator(_aO, "AoArea");
            _aO.Save();

            ParametrsChangeByUser += StrategyBillWilliams_ParametrsChangeByUser;
        }

        /// <summary>
        /// параметры изменены юзером
        /// </summary>
        void StrategyBillWilliams_ParametrsChangeByUser()
        {
            if (AlligatorSlowLineLength.ValueInt != _alligator.LenghtDown ||
                AlligatorMiddleLineLength.ValueInt != _alligator.LenghtBase ||
                AlligatorFastLineLength.ValueInt != _alligator.LenghtUp)
            {
                _alligator.LenghtDown = AlligatorSlowLineLength.ValueInt;
                _alligator.LenghtBase = AlligatorMiddleLineLength.ValueInt;
                _alligator.LenghtUp = AlligatorFastLineLength.ValueInt;
                _alligator.Reload();
            }
        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "Williams Band";
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            MessageBox.Show("Это трендовый робот оснванный на стратегии Билла Вильямса");
        }

        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        // индикаторы

        /// <summary>
        /// аллигатор
        /// </summary>
        private Alligator _alligator;

        /// <summary>
        /// фрактал
        /// </summary>
        private Fractal _fractal;

        /// <summary>
        /// удивительный осциллятор
        /// </summary>
        private AwesomeOscillator _aO;

// настройки публичные

        /// <summary>
        /// длинна быстрой линии аллигатора
        /// </summary>
        public StrategyParameterInt AlligatorFastLineLength;

        /// <summary>
        /// длинна средней линии аллигатора
        /// </summary>
        public StrategyParameterInt AlligatorMiddleLineLength;

        /// <summary>
        /// длинна медленной линии аллигатора
        /// </summary>
        public StrategyParameterInt AlligatorSlowLineLength;

        /// <summary>
        /// проскальзывание
        /// </summary>
        public StrategyParameterInt Slipage;

        /// <summary>
        /// объём для первого входа
        /// </summary>
        public StrategyParameterDecimal VolumeFirst;

        /// <summary>
        /// объём для последующих входов
        /// </summary>
        public StrategyParameterDecimal VolumeSecond;

        /// <summary>
        /// максимальная позиция
        /// </summary>
        public StrategyParameterInt MaximumPositions;

        /// <summary>
        /// режим работы
        /// </summary>
        public StrategyParameterString Regime;

// переменные, нужные для торговли

        private decimal _lastPrice;

        private decimal _lastUpAlligator;

        private decimal _lastMiddleAlligator;

        private decimal _lastDownAlligator;

        private decimal _lastFractalUp;

        private decimal _lastFractalDown;

        private decimal _lastAo;

        private decimal _secondAo;

        private decimal _thirdAo;

        // логика

        /// <summary>
        /// собитие завершения свечи
        /// </summary>
        private void Bot_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (StartProgram == StartProgram.IsOsTrader
                && DateTime.Now.Hour < 10)
            {
                return;
            }

            if (_alligator.ValuesUp == null ||
                _alligator.Values == null ||
                _alligator.ValuesDown == null ||
                _fractal == null ||
                _alligator.LenghtBase > candles.Count ||
                _alligator.LenghtDown > candles.Count ||
                _alligator.LenghtUp > candles.Count)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _lastUpAlligator = _alligator.ValuesUp[_alligator.ValuesUp.Count - 1];
            _lastMiddleAlligator = _alligator.Values[_alligator.Values.Count - 1];
            _lastDownAlligator = _alligator.ValuesDown[_alligator.ValuesDown.Count - 1];

            for (int i = _fractal.ValuesUp.Count - 1; i > -1; i--)
            {
                if (_fractal.ValuesUp[i] != 0)
                {
                    _lastFractalUp = _fractal.ValuesUp[i];
                    break;
                }
            }

            for (int i = _fractal.ValuesDown.Count - 1; i > -1; i--)
            {
                if (_fractal.ValuesDown[i] != 0)
                {
                    _lastFractalDown = _fractal.ValuesDown[i];
                    break;
                }
            }

            _lastAo = _aO.Values[_aO.Values.Count - 1];

            if (_aO.Values.Count > 3)
            {
                _secondAo = _aO.Values[_aO.Values.Count - 2];
                _thirdAo = _aO.Values[_aO.Values.Count - 3];
            }

            if (_lastUpAlligator == 0 ||
                _lastMiddleAlligator == 0 ||
                _lastDownAlligator == 0)
            {
                return;
            }


            // распределяем логику в зависимости от текущей позиции

            List<Position> openPosition = _tab.PositionsOpenAll;

            if (openPosition != null && openPosition.Count != 0
                && candles[candles.Count - 1].TimeStart.Hour <= 18)
            {
                for (int i = 0; i < openPosition.Count; i++)
                {
                    LogicClosePosition(openPosition[i], candles);
                }
            }

            if (Regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            if (openPosition == null || openPosition.Count == 0
                && candles[candles.Count - 1].TimeStart.Hour >= 11
                && candles[candles.Count - 1].TimeStart.Hour <= 18)
            {
                LogicOpenPosition();
            }
            else if (openPosition.Count != 0 && openPosition.Count < MaximumPositions.ValueInt
                     && candles[candles.Count - 1].TimeStart.Hour >= 11
                     && candles[candles.Count - 1].TimeStart.Hour <= 18)
            {
                LogicOpenPositionSecondary(openPosition[0].Direction);
            }


        }

        /// <summary>
        /// логика открытия позиции
        /// </summary>
        private void LogicOpenPosition()
        {
            if (_lastPrice > _lastUpAlligator && _lastPrice > _lastMiddleAlligator && _lastPrice > _lastDownAlligator
                && _lastPrice > _lastFractalUp
                && Regime.ValueString != "OnlyShort")
            {
                _tab.BuyAtLimit(VolumeFirst.ValueDecimal, _lastPrice + Slipage.ValueInt * _tab.Securiti.PriceStep);
            }
            if (_lastPrice < _lastUpAlligator && _lastPrice < _lastMiddleAlligator && _lastPrice < _lastDownAlligator
                && _lastPrice < _lastFractalDown
                && Regime.ValueString != "OnlyLong")
            {
                _tab.SellAtLimit(VolumeFirst.ValueDecimal, _lastPrice - Slipage.ValueInt * _tab.Securiti.PriceStep);
            }
        }

        /// <summary>
        /// логика открытия позиции после первой 
        /// </summary>
        private void LogicOpenPositionSecondary(Side side)
        {
            if (side == Side.Buy && Regime.ValueString != "OnlyShort")
            {
                if (_secondAo < _lastAo &&
                    _secondAo < _thirdAo)
                {
                    _tab.BuyAtLimit(VolumeSecond.ValueDecimal, _lastPrice + Slipage.ValueInt * _tab.Securiti.PriceStep);
                }
            }

            if (side == Side.Sell && Regime.ValueString != "OnlyLong")
            {
                if (_secondAo > _lastAo &&
                    _secondAo > _thirdAo)
                {
                    _tab.SellAtLimit(VolumeSecond.ValueDecimal, _lastPrice - Slipage.ValueInt * _tab.Securiti.PriceStep);
                }
            }
        }

        /// <summary>
        /// логика закрытия позиции
        /// </summary>
        private void LogicClosePosition(Position position, List<Candle> candles)
        {
            if (position.State != PositionStateType.Open)
            {
                return;
            }

            if (position.Direction == Side.Buy)
            {
                if (_lastPrice < _lastMiddleAlligator)
                {
                    _tab.CloseAtLimit(position, _lastPrice - Slipage.ValueInt * _tab.Securiti.PriceStep, position.OpenVolume);
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastPrice > _lastMiddleAlligator)
                {
                    _tab.CloseAtLimit(position, _lastPrice + Slipage.ValueInt * _tab.Securiti.PriceStep, position.OpenVolume);
                }
            }
        }
    }

    /// <summary>
    /// трендовая стратегия Джесси Ливермора, на основе пробоя канала.
    /// только большой ТФ
    /// </summary>
    public class StrategyLevermor : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public StrategyLevermor(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            ChannelLength = CreateParameter("ChannelLength", 10, 10, 400, 10);
            SmaLength = CreateParameter("SmaLength", 10, 5, 150, 2);
            MaximumPosition = CreateParameter("MaxPosition", 5, 1, 20, 3);
            Volume = CreateParameter("Volume", 3, 1.0m, 50, 4);
            Slipage = CreateParameter("Slipage", 0, 0, 20, 1);
            PersentDopBuy = CreateParameter("PersentDopBuy", 0.5m, 0.1m, 2, 0.1m);
            PersentDopSell = CreateParameter("PersentDopSell", 0.5m, 0.1m, 2, 0.1m);

            TralingStopLength = CreateParameter("TralingStopLength", 3, 3, 8, 0.5m);
            ExitType = CreateParameter("ExitType", "Traling", new[] { "Traling", "Sma" });

            _smaTrenda = new MovingAverage(name + "MovingLong", false) { Lenght = 150, ColorBase = Color.DodgerBlue };
            _smaTrenda = (MovingAverage)_tab.CreateCandleIndicator(_smaTrenda, "Prime");
            _smaTrenda.Lenght = SmaLength.ValueInt;

            _smaTrenda.Save();

            _channel = new PriceChannel(name + "Chanel", false) { LenghtUpLine = 12, LenghtDownLine = 12 };
            _channel = (PriceChannel)_tab.CreateCandleIndicator(_channel, "Prime");
            _channel.LenghtDownLine = ChannelLength.ValueInt;
            _channel.LenghtUpLine = ChannelLength.ValueInt;
            _channel.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += StrategyRutabaga_PositionOpeningSuccesEvent;
            DeleteEvent += Strategy_DeleteEvent;

            ParametrsChangeByUser += StrategyLevermor_ParametrsChangeByUser;
        }

        void StrategyLevermor_ParametrsChangeByUser()
        {
            _channel.LenghtDownLine = ChannelLength.ValueInt;
            _channel.LenghtUpLine = ChannelLength.ValueInt;
            _channel.Reload();

            _smaTrenda.Lenght = SmaLength.ValueInt;
            _smaTrenda.Reload();
        }

        /// <summary>
        /// переопределённый метод, позволяющий менеджеру ботов определять что за робот перед ним
        /// </summary>
        /// <returns>название стратегии</returns>
        public override string GetNameStrategyType()
        {
            return "Levermor";
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            MessageBox.Show(
                "Трендовая стратегия описанная в книге Эдвина Лафевра: Воспоминания биржевого спекулянта. Подробнее: " +
                "http://o-s-a.net/posts/34-bad-quant-edvin-lefevr-vospominanija-birzhevogo-spekuljanta.html");
        }

        private BotTabSimple _tab;

        // индикаторы

        /// <summary>
        /// индикатор: скользящая средняя
        /// </summary>
        private MovingAverage _smaTrenda;

        /// <summary>
        /// индикатор: Атр
        /// </summary>
        private PriceChannel _channel;

        // настройки стандартные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public StrategyParameterInt Slipage;
        /// <summary>
        /// режим работы робота
        /// </summary>
        public StrategyParameterString Regime;
        /// <summary>
        /// объём исполняемый в одной сделке
        /// </summary>
        public StrategyParameterDecimal Volume;
        public StrategyParameterInt MaximumPosition;
        public StrategyParameterDecimal PersentDopBuy;
        public StrategyParameterDecimal PersentDopSell;

        public StrategyParameterInt ChannelLength;
        public StrategyParameterInt SmaLength;

        public StrategyParameterDecimal TralingStopLength;
        public StrategyParameterString ExitType;

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // логика

        /// <summary>
        /// событие, происходит когда позиция успешно открыта
        /// </summary>
        /// <param name="position">открытая позиция</param>
        private void StrategyRutabaga_PositionOpeningSuccesEvent(Position position)
        {
            try
            {
                if (Regime.ValueString == "Off")
                {
                    return;
                }

                List<Position> openPosition = _tab.PositionsOpenAll;

                if (openPosition != null && openPosition.Count != 0)
                {
                    // есть открытая позиция, вызываем установку стопов
                    LogicClosePosition(openPosition, _tab.CandlesFinishedOnly);
                }
            }
            catch (Exception error)
            {
                _tab.SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (StartProgram == StartProgram.IsOsTrader
                && DateTime.Now.Hour < 10)
            {
                return;
            }

            if (_smaTrenda.Lenght > candles.Count ||
                _channel.LenghtUpLine > candles.Count ||
                _channel.LenghtDownLine > candles.Count)
            {
                return;
            }

            // распределяем логику в зависимости от текущей позиции

            List<Position> openPosition = _tab.PositionsOpenAll;

            if (openPosition != null && openPosition.Count != 0)
            {
                // есть открытая позиция, вызываем установку стопов
                LogicClosePosition(openPosition, candles);
            }

            if (Regime.ValueString == "OnlyClosePosition")
            {
                // если у бота включен режим "только закрытие"
                return;
            }

            LogicOpenPosition(candles);

        }

        /// <summary>
        /// логика открытия позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles)
        {
            if (_smaTrenda.Values == null)
            {
                return;
            }
            decimal lastMa = _smaTrenda.Values[_smaTrenda.Values.Count - 1];

            decimal lastPrice = candles[candles.Count - 1].Close;

            if (lastMa == 0)
            {
                return;
            }

            // берём максимум и минимум за последние n баров

            decimal maxToCandleSeries = _channel.ValuesUp[_channel.ValuesUp.Count - 1];
            decimal minToCandleSeries = _channel.ValuesDown[_channel.ValuesDown.Count - 1];

            List<Position> positions = _tab.PositionsOpenAll;

            if (lastPrice >= lastMa && Regime.ValueString != "OnlyShort")
            {
                if (positions != null && positions.Count != 0 &&
                    positions[0].Direction == Side.Buy)
                { // если открыты лонги - добавляемся
                    if (positions.Count >= MaximumPosition.ValueInt)
                    {
                        return;
                    }
                    decimal lastIntro = positions[positions.Count - 1].EntryPrice;
                    if (lastIntro + lastIntro * (PersentDopSell.ValueDecimal / 100) < lastPrice)
                    {
                        if (positions.Count >= MaximumPosition.ValueInt)
                        {
                            return;
                        }
                        _tab.BuyAtLimit(Volume.ValueDecimal, lastPrice + (Slipage.ValueInt * _tab.Securiti.PriceStep));
                    }
                }
                else if (positions == null || positions.Count == 0)
                { // если ничего не открыто - ставим линии на пробой
                    //BuyAtStop(0, Volume, maxToCandleSeries + Slipage, maxToCandleSeries, candles[candles.Count - 1].Close);
                    _tab.SellAtStopCanсel();
                    _tab.BuyAtStopCanсel();
                    _tab.BuyAtStop(Volume.ValueDecimal, maxToCandleSeries + (Slipage.ValueInt * _tab.Securiti.PriceStep), maxToCandleSeries, StopActivateType.HigherOrEqual);
                }
            }

            if (lastPrice <= lastMa && Regime.ValueString != "OnlyLong")
            {
                if (positions != null && positions.Count != 0 &&
                         positions[0].Direction == Side.Sell)
                { // если открыты шорты - добавляемся
                    if (positions.Count >= MaximumPosition.ValueInt)
                    {
                        return;
                    }
                    decimal lastIntro = positions[positions.Count - 1].EntryPrice;

                    if (lastIntro - lastIntro * (PersentDopSell.ValueDecimal / 100) > lastPrice)
                    {
                        //SellAtLimit(0, Volume, lastPrice - Slipage);
                        _tab.SellAtLimit(Volume.ValueDecimal, lastPrice - (Slipage.ValueInt * _tab.Securiti.PriceStep));
                    }
                }
                else if (positions == null || positions.Count == 0)
                { // если ничего не открыто - ставим линии на пробой
                    if (positions != null && positions.Count >= MaximumPosition.ValueInt)
                    {
                        return;
                    }
                    //SellAtStop(0, Volume, minToCandleSeries - Slipage, minToCandleSeries,candles[candles.Count - 1].Close);
                    _tab.SellAtStopCanсel();
                    _tab.BuyAtStopCanсel();
                    _tab.SellAtStop(Volume.ValueDecimal, minToCandleSeries - (Slipage.ValueInt * _tab.Securiti.PriceStep), minToCandleSeries, StopActivateType.LowerOrEqyal);
                }
            }
        }

        /// <summary>
        /// логика выхода из позиции
        /// </summary>
        private void LogicClosePosition(List<Position> positions, List<Candle> candles)
        {
            if (positions == null || positions.Count == 0)
            {
                return;
            }

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (positions[i].State == PositionStateType.Closing)
                {
                    continue;
                }

                if (ExitType.ValueString == "Sma")
                {
                    if (positions[i].Direction == Side.Buy)
                    {
                        if (candles[candles.Count - 1].Close < _smaTrenda.Values[_smaTrenda.Values.Count - 1])
                        {
                            _tab.CloseAtMarket(positions[i], positions[i].OpenVolume);
                        }
                    }
                    else
                    {
                        if (candles[candles.Count - 1].Close > _smaTrenda.Values[_smaTrenda.Values.Count - 1])
                        {
                            _tab.CloseAtMarket(positions[i], positions[i].OpenVolume);
                        }
                    }
                }
                else if (ExitType.ValueString == "Traling")
                {
                    if (positions[i].Direction == Side.Buy)
                    {
                        _tab.CloseAtTrailingStop(positions[i],
                            candles[candles.Count - 1].Close -
                            candles[candles.Count - 1].Close * TralingStopLength.ValueDecimal / 100,
                            candles[candles.Count - 1].Close -
                            candles[candles.Count - 1].Close * TralingStopLength.ValueDecimal / 100);
                    }
                    else
                    {
                        _tab.CloseAtTrailingStop(positions[i],
                            candles[candles.Count - 1].Close +
                            candles[candles.Count - 1].Close*TralingStopLength.ValueDecimal/100,
                            candles[candles.Count - 1].Close +
                            candles[candles.Count - 1].Close*TralingStopLength.ValueDecimal/100);
                    }
                }
            }
        }

    }

    /// <summary>
    /// Трендовая стратегия на пересечение индикатора RVI
    /// </summary>
    public class RviTrade : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public RviTrade(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            RviLenght = CreateParameter("RviLength", 10, 10, 80, 3);
            Volume = CreateParameter("Volume", 3, 1.0m, 50, 4);
            Slipage = CreateParameter("Slipage", 0, 0, 20, 1);

            _rvi = new Rvi(name + "RviArea", false);
            _rvi = (Rvi)_tab.CreateCandleIndicator(_rvi, "MacdArea");
            _rvi.Period = RviLenght.ValueInt;
            _rvi.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
            ParametrsChangeByUser += RviTrade_ParametrsChangeByUser;
        }

        void RviTrade_ParametrsChangeByUser()
        {
            if (RviLenght.ValueInt != _rvi.Period)
            {
                _rvi.Period = RviLenght.ValueInt;
                _rvi.Reload();
            }
        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "RviTrade";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
        }

        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //индикаторы

        /// <summary>
        /// RVI индикатор
        /// </summary>
        private Rvi _rvi;

        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public StrategyParameterInt Slipage;

        /// <summary>
        /// фиксированный объем для входа
        /// </summary>
        public StrategyParameterDecimal Volume;

        /// <summary>
        /// режим работы
        /// </summary>
        public StrategyParameterString Regime;

        /// <summary>
        /// длинна индикатора
        /// </summary>
        public StrategyParameterInt RviLenght;


        // переменные, нужные для торговли
        private decimal _lastPrice;
        private decimal _lastRviUp;
        private decimal _lastRviDown;
        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (_rvi.ValuesUp == null)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _lastRviUp = _rvi.ValuesUp[_rvi.ValuesUp.Count - 1];
            _lastRviDown = _rvi.ValuesDown[_rvi.ValuesDown.Count - 1];

            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);

                }
            }

            if (Regime.ValueString == "OnlyClosePosition")
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        /// <summary>
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_lastRviDown < 0 && _lastRviUp > _lastRviDown && Regime.ValueString != "OnlyShort")
            {
                _tab.BuyAtLimit(Volume.ValueDecimal, _lastPrice + Slipage.ValueInt * _tab.Securiti.PriceStep);
            }

            if (_lastRviDown > 0 && _lastRviUp < _lastRviDown && Regime.ValueString != "OnlyLong")
            {
                _tab.SellAtLimit(Volume.ValueDecimal, _lastPrice - Slipage.ValueInt * _tab.Securiti.PriceStep);
            }
        }

        /// <summary>
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.Direction == Side.Buy && position.State == PositionStateType.Open)
            {
                if (_lastRviDown > 0 && _lastRviUp < _lastRviDown)
                {
                    _tab.CloseAtLimit(position, _lastPrice - Slipage.ValueInt, position.OpenVolume);

                    if (Regime.ValueString != "OnlyLong" && Regime.ValueString != "OnlyClosePosition")
                    {
                        _tab.SellAtLimit(Volume.ValueDecimal, _lastPrice - Slipage.ValueInt * _tab.Securiti.PriceStep);
                    }
                }
            }

            if (position.Direction == Side.Sell && position.State == PositionStateType.Open)
            {
                if (_lastRviDown < 0 && _lastRviUp > _lastRviDown)
                {
                    _tab.CloseAtLimit(position, _lastPrice + Slipage.ValueInt, position.OpenVolume);

                    if (Regime.ValueString != "OnlyShort" && Regime.ValueString != "OnlyClosePosition")
                    {
                        _tab.BuyAtLimit(Volume.ValueDecimal, _lastPrice + Slipage.ValueInt*_tab.Securiti.PriceStep);
                    }
                }
            }
        }

    }

    public class TwoLegArbitrage : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public TwoLegArbitrage(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Index);
            _tab1 = TabsIndex[0];

            TabCreate(BotTabType.Simple);
            _tab2 = TabsSimple[0];
            TabCreate(BotTabType.Simple);
            _tab3 = TabsSimple[1];

            _tab2.CandleFinishedEvent += Strateg_CandleFinishedEvent;
            _tab3.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            Upline = CreateParameter("Upline", 10, 50, 80, 3);
            Downline = CreateParameter("Downline", 10, 25, 50, 2);
            Volume = CreateParameter("Volume", 3, 1, 50, 4);
            Slipage = CreateParameter("Slipage", 0, 0, 20, 1);
            RsiLength = CreateParameter("RsiLength", 10, 5, 150, 2);

            _rsi = new Rsi(name + "RSI", false) { Lenght = 20, ColorBase = Color.Gold, };
            _rsi = (Rsi)_tab1.CreateCandleIndicator(_rsi, "RsiArea");
            _rsi.Lenght = RsiLength.ValueInt;
            _rsi.Save();

            ParametrsChangeByUser += TwoLegArbitrage_ParametrsChangeByUser;
        }

        /// <summary>
        /// пользователь изменил параметр
        /// </summary>
        void TwoLegArbitrage_ParametrsChangeByUser()
        {
            if (_rsi.Lenght != RsiLength.ValueInt)
            {
                _rsi.Lenght = RsiLength.ValueInt;
                _rsi.Reload();
            }
        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "TwoLegArbitrage";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {

        }

        /// <summary>
        /// вкладка для формирования индекса
        /// </summary>
        private BotTabIndex _tab1;

        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab2;

        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab3;

        //индикаторы

        /// <summary>
        /// RSI
        /// </summary>
        private Rsi _rsi;

        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public StrategyParameterInt Slipage;
        /// <summary>
        /// режим работы робота
        /// </summary>
        public StrategyParameterString Regime;
        /// <summary>
        /// объём исполняемый в одной сделке
        /// </summary>
        public StrategyParameterInt Volume;

        /// <summary>
        /// верхняя граница для RSI для принятия решений
        /// </summary>
        public StrategyParameterInt Upline;

        /// <summary>
        /// верхняя граница для RSI для принятия решений
        /// </summary>
        public StrategyParameterInt Downline;

        /// <summary>
        /// длинна RSI
        /// </summary>
        public StrategyParameterInt RsiLength;

// переменные, нужные для торговли

        private decimal _lastRsi;

// логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (_tab1.Candles.Count == 0 ||
                _tab2.CandlesFinishedOnly.Count != _tab3.CandlesFinishedOnly.Count)
            {
                return;
            }

            if (_rsi.Values == null)
            {
                return;
            }

            _lastRsi = _rsi.Values[_rsi.Values.Count - 1];

            if (_rsi.Values == null || _rsi.Values.Count < _rsi.Lenght + 5)
            {
                return;

            }

            // распределяем логику в зависимости от текущей позиции инструментов

            for (int j = 0; TabsSimple.Count != 0 && j < TabsSimple.Count; j++)
            {
                List<Position> openPositions = TabsSimple[j].PositionsOpenAll;
                if (openPositions != null && openPositions.Count != 0)
                {
                    for (int i = 0; i < openPositions.Count; i++)
                    {
                        LogicClosePosition(openPositions[i], TabsSimple[j]);
                    }
                }
                if (Regime.ValueString == "OnlyClosePosition")
                {
                    return;
                }
                if (openPositions == null || openPositions.Count == 0)
                {
                    LogicOpenPosition(TabsSimple[j]);
                }
            }
        }

        /// <summary>
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(BotTabSimple tab)
        {
            if (_lastRsi > Upline.ValueInt && Regime.ValueString != "OnlyLong")
            {
                tab.SellAtLimit(Volume.ValueInt, tab.PriceBestBid - Slipage.ValueInt * tab.Securiti.PriceStep);
            }
            if (_lastRsi < Downline.ValueInt && Regime.ValueString != "OnlyShort")
            {
                tab.BuyAtLimit(Volume.ValueInt, tab.PriceBestAsk + Slipage.ValueInt * tab.Securiti.PriceStep);
            }
        }

        /// <summary>
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(Position position, BotTabSimple tab)
        {
            if (position.Direction == Side.Buy)
            {
                if (_lastRsi > Upline.ValueInt)
                {
                    tab.CloseAtLimit(position, tab.PriceBestBid - Slipage.ValueInt * tab.Securiti.PriceStep, position.OpenVolume);
                }
            }
            if (position.Direction == Side.Sell)
            {
                if (_lastRsi < Downline.ValueInt)
                {
                    tab.CloseAtLimit(position, tab.PriceBestAsk + Slipage.ValueInt * tab.Securiti.PriceStep, position.OpenVolume);
                }
            }
        }

    }

    # endregion

    # region роботы из инструкций с пошаговым созданием на нашем канале ютуб https://www.youtube.com/channel/UCLmOUsdFs48mo37hgXmIJTQ

    public class Robot : BotPanel
    {
        private BotTabSimple _tab;
        private Bollinger _bol;
        public decimal Volume;

        public override string GetNameStrategyType()
        {
            return "Robot";
        }

        public override void ShowIndividualSettingsDialog()
        {
            RobotUi ui = new RobotUi(this);
            ui.ShowDialog();
        }

        public Robot(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _bol = new Bollinger(false);
            _bol = (Bollinger)_tab.CreateCandleIndicator(_bol, "Prime");

            _bol.Save();

            _tab.CandleFinishedEvent += TradeLogic;

            Volume = 1;
        }

        private decimal _lastPrice;
        private decimal _lastBolUp;
        private decimal _lastBolDown;
        private void TradeLogic(List<Candle> candles)
        {
            _lastPrice = candles[candles.Count - 1].Close;
            _lastBolUp = _bol.ValuesUp[_bol.ValuesUp.Count - 1];
            _lastBolDown = _bol.ValuesDown[_bol.ValuesDown.Count - 1];

           

            if (_bol.ValuesUp == null)
            {
                return;
            }
            if (_bol.ValuesUp.Count < _bol.Lenght + 5)
            {
                return;
            }

            if (_tab.PositionsOpenAll != null && _tab.PositionsOpenAll.Count != 0)
            {
                if (_lastPrice < _lastBolDown)
                {
                    _tab.CloseAllAtMarket();
                }
                return;
            }

            if (_lastPrice > _lastBolUp)
            {
                _tab.SellAtMarket(Volume);

            }
        }

    }

    public class FirstBot : BotPanel
    {
        public override string GetNameStrategyType()
        {
            return "FirstBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
            MessageBox.Show("У данной стратегии пока нет настроек");
        }

        public FirstBot(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _tab.CandleFinishedEvent += TradeLogic;

        }

        private BotTabSimple _tab;

        private void TradeLogic(List<Candle> candles)
        {
            // 1. если свечей меньше чем 5 - выходим из метода.
            if (candles.Count < 5)
            {
                return;
            }

            // 2. если уже есть открытые позиции – закрываем и выходим.
            if (_tab.PositionsOpenAll != null && _tab.PositionsOpenAll.Count != 0)
            {
                _tab.CloseAllAtMarket();
                return;
            }

            // 3. если закрытие последней свечи выше закрытия предыдущей – покупаем. 
            if (candles[candles.Count - 1].Close > candles[candles.Count - 2].Close)
            {
                _tab.BuyAtMarket(1);
            }

            // 4. если закрытие последней свечи ниже закрытия предыдущей, продаем. 
            if (candles[candles.Count - 1].Close < candles[candles.Count - 2].Close)
            {
                _tab.SellAtMarket(1);
            }
        }
    }

    # endregion 

    # region готовые роботы

    /// <summary>
    /// Двуногий арбитраж. Торговля двумя инструменетами конттренд при уходе индекса в зону перекупленности/перепроданности по RSI
    /// </summary>

    public class BotWhithTwoTimeFrame : BotPanel
    {
        public BotWhithTwoTimeFrame(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            TabsSimple[0].CandleFinishedEvent += BotWhithTwoTimeFrame_CandleFinishedEvent;
            TabsSimple[0].PositionOpeningSuccesEvent += BotWhithTwoTimeFrame_PositionOpeningSuccesEvent;

            Moving = new MovingAverage("moving",false);
            Moving.Lenght = 25;
            Moving.TypeCalculationAverage = MovingAverageTypeCalculation.Exponential;
        }

        void BotWhithTwoTimeFrame_PositionOpeningSuccesEvent(Position position)
        {

            TabsSimple[0].CloseAtStop(position, position.EntryPrice - 20*TabsSimple[0].Securiti.PriceStep,
                position.EntryPrice - 20*TabsSimple[0].Securiti.PriceStep);

            TabsSimple[0].CloseAtProfit(position, position.EntryPrice + 20 * TabsSimple[0].Securiti.PriceStep,
                position.EntryPrice + 20 * TabsSimple[0].Securiti.PriceStep);

        }

        /// <summary>
        /// машка, которая рассчитывается по дополнительному ТаймФрейму
        /// </summary>
        public MovingAverage Moving;

        public List<Candle> MergeCandles;

        void BotWhithTwoTimeFrame_CandleFinishedEvent(List<Candle> candles)
        {
            // логика такая.
            // на базовом ТФ последняя свеча растущая
            // на сжатом ТФ закрытие свечи выше чем машка
            // выход по стопу и профиту

            if (candles.Count < 5)
            {
                CandleConverter.Clear();
                return;
            }

            List<Position> positions = TabsSimple[0].PositionsOpenAll;

            MergeCandles = CandleConverter.Merge(candles, 5);
            Moving.Process(MergeCandles); // прогружаем индикатор вручную, схлопнутыми свечками

            if (positions == null ||
                positions.Count == 0)
            {

                if (MergeCandles.Count < Moving.Lenght)
                {
                    return;
                }

                if (candles[candles.Count - 1].IsUp &&
                    MergeCandles[MergeCandles.Count - 1].Close >
                    Moving.Values[Moving.Values.Count - 1])
                {
                    TabsSimple[0].BuyAtLimit(1, candles[candles.Count - 1].Close);
                }
            }
        }

        public override string GetNameStrategyType()
        {

            return "TwoTimeFrameBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
            BotWhithTwoTimeFrameUi ui = new BotWhithTwoTimeFrameUi(this);
            ui.ShowDialog();
        }
    }

    /// <summary>
    /// Торговый робот на индексе. Пересечение MA на индексе снизу вверх лонг торгового инст., при обратном пересечении шорт торг. инст.
    /// </summary>
    public class OneLegArbitration : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="_ma">MovingAverage</param>
        /// <param name="Slipage">Проскальзывание</param>
        /// <param name="VolumeFix">Объем для первого входа</param>
        public OneLegArbitration(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Index);
            _tab1 = TabsIndex[0];

            _ma = new MovingAverage(name + "MovingAverage", false) { Lenght = 12, ColorBase = Color.DodgerBlue };
            _ma = (MovingAverage)_tab1.CreateCandleIndicator(_ma, "Prime");
            _ma.Save();

            TabCreate(BotTabType.Simple);
            _tab2 = TabsSimple[0];

            _tab2.CandleFinishedEvent += Strateg_CandleFinishedEvent;
            //_tab.PositionOpeningSuccesEvent += Strateg_PositionOpen;

            Slipage = 10;
            VolumeFix = 1;

            Load();

            DeleteEvent += Strategy_DeleteEvent;

        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "OneLegArbitration";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            OneLegArbitrationUi ui = new OneLegArbitrationUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// вкладка анализируемого индекса
        /// </summary>
        private BotTabIndex _tab1;

        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab2;

        //индикаторы

        /// <summary>
        /// MovingAverage
        /// </summary>
        private MovingAverage _ma;

        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// фиксированный объем для входа позицию
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Regime);
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли

        private decimal _lastPrice;
        private decimal _lastIndex;
        private decimal _lastMa;

        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_ma.Values == null || _ma.Values.Count < _ma.Lenght + 2)
            {
                return;
            }

            _lastIndex = _tab1.Candles[_tab1.Candles.Count - 1].Close;
            _lastMa = _ma.Values[_ma.Values.Count - 1];
            _lastPrice = candles[candles.Count - 1].Close;

            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab2.PositionsOpenAll;
            if (openPositions != null && openPositions.Count != 0)
            {
                LogicClosePosition(candles, openPositions);
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        /// <summary>
        /// логика открытия позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            List<Position> openPositions = _tab2.PositionsOpenAll;
            if (openPositions == null || openPositions.Count == 0)
            {
                // открытие long
                if (Regime != BotTradeRegime.OnlyShort)
                {
                    if (_lastIndex > _lastMa)
                    {
                        _tab2.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                    }
                }

                // открытие Short
                if (Regime != BotTradeRegime.OnlyLong)
                {
                    if (_lastIndex < _lastMa)
                    {
                        _tab2.SellAtLimit(VolumeFix, _lastPrice - Slipage);
                    }
                }
                return;
            }
        }

        // логика закрытия позиции
        private void LogicClosePosition(List<Candle> candles, List<Position> position)
        {
            List<Position> openPositions = _tab2.PositionsOpenAll;
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].Direction == Side.Buy)
                {
                    if (_lastIndex < _lastMa)
                    {
                        if (Regime == BotTradeRegime.OnlyClosePosition)
                        {
                            _tab2.CloseAtMarket(openPositions[i], openPositions[i].OpenVolume);
                        }
                        else
                        {
                            _tab2.CloseAtMarket(openPositions[i], openPositions[i].OpenVolume);
                            _tab2.SellAtLimit(VolumeFix, _lastPrice - Slipage);
                        }
                    }
                }
                else
                {
                    if (_lastIndex > _lastMa)
                    {
                        if (Regime == BotTradeRegime.OnlyClosePosition)
                        {
                            _tab2.CloseAtMarket(openPositions[i], openPositions[i].OpenVolume);
                        }
                        else
                        {
                            _tab2.CloseAtMarket(openPositions[i], openPositions[i].OpenVolume);
                            _tab2.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                        }
                    }
                }

            }
        }


    }

    /// <summary>
    /// Торговый робот ТриСрлдата. При формироваваниии паттерна из трех растущих/падующих свечей вход по в контртренд с фиксацией по тейку или по стопу
    /// </summary>
    public class ThreeSoldier : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="Slipage">Проскальзывание</param>
        /// <param name="VolumeFix">Объем для первого входа</param>
        /// <param name="heightSoldiers">общая высота паттерна из трех свечей по телам</param>
        /// <param name="minHeightSoldier">минимальный размер тела свечи в паттрене</param>
        /// <param name="procHeightSto">процент от общей высоты паттрена на стоплос от точки входа</param>
        /// <param name="procHeightTake">процент от общей высоты паттрена на тейкпрофит от точки входа</param>
        public ThreeSoldier(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += Strateg_ClosePosition;

            Slipage = 10;
            VolumeFix = 1;
            HeightSoldiers = 1;
            MinHeightSoldier = 1;
            ProcHeightTake = 30;
            ProcHeightStop = 10;

            Load();

            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "ThreeSoldier";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            ThreeSoldierUi ui = new ThreeSoldierUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //настройки публичные

        /// <summary>
        /// общая высота паттрена
        /// </summary>
        public decimal HeightSoldiers;

        /// <summary>
        /// минимальная высота свечи в паттрене
        /// </summary>
        public decimal MinHeightSoldier;

        /// <summary>
        /// процент от высоты паттрена на закрытие по тейку
        /// </summary>
        public decimal ProcHeightTake;

        /// <summary>
        /// процент от высоты паттрена на закрытие по стопу
        /// </summary>
        public decimal ProcHeightStop;

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// фиксированный объем для входа позицию
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Regime);
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.WriteLine(HeightSoldiers);
                    writer.WriteLine(MinHeightSoldier);
                    writer.WriteLine(ProcHeightTake);
                    writer.WriteLine(ProcHeightStop);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    HeightSoldiers = Convert.ToInt32(reader.ReadLine());
                    MinHeightSoldier = Convert.ToInt32(reader.ReadLine());
                    ProcHeightTake = Convert.ToInt32(reader.ReadLine());
                    ProcHeightStop = Convert.ToInt32(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли

        private decimal _lastPrice;

        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (candles.Count < 3)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;

            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }


        /// <summary>
        /// логика закрытия позиции trailing-stop
        /// </summary>
        private void Strateg_ClosePosition(Position position)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].Direction == Side.Buy)
                {
                    decimal heightPattern = Math.Abs(_tab.CandlesAll[_tab.CandlesAll.Count - 3].Open - _tab.CandlesAll[_tab.CandlesAll.Count - 1].Close);
                    decimal priceStop = _lastPrice - (heightPattern * ProcHeightStop) / 100;
                    decimal priceTake = _lastPrice + (heightPattern * ProcHeightTake) / 100;
                    _tab.CloseAtStop(openPositions[i], priceStop, priceStop - Slipage);
                    _tab.CloseAtProfit(openPositions[i], priceTake, priceTake - Slipage);
                }
                else
                {
                    decimal heightPattern = Math.Abs(_tab.CandlesAll[_tab.CandlesAll.Count - 1].Close - _tab.CandlesAll[_tab.CandlesAll.Count - 3].Open);
                    decimal priceStop = _lastPrice + (heightPattern * ProcHeightStop) / 100;
                    decimal priceTake = _lastPrice - (heightPattern * ProcHeightTake) / 100;
                    _tab.CloseAtStop(openPositions[i], priceStop, priceStop + Slipage);
                    _tab.CloseAtProfit(openPositions[i], priceTake, priceTake + Slipage);
                }
            }
        }


        /// <summary>
        /// логика открытия позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            if (openPositions == null || openPositions.Count == 0)
            {
                if (Math.Abs(candles[candles.Count - 3].Open - candles[candles.Count - 1].Close) < HeightSoldiers) return;
                if (Math.Abs(candles[candles.Count - 3].Open - candles[candles.Count - 3].Close) < MinHeightSoldier) return;
                if (Math.Abs(candles[candles.Count - 2].Open - candles[candles.Count - 2].Close) < MinHeightSoldier) return;
                if (Math.Abs(candles[candles.Count - 1].Open - candles[candles.Count - 1].Close) < MinHeightSoldier) return;

                // открытие long
                if (Regime != BotTradeRegime.OnlyShort)
                {
                    if (candles[candles.Count - 3].Open > candles[candles.Count - 3].Close && candles[candles.Count - 2].Open > candles[candles.Count - 2].Close && candles[candles.Count - 1].Open > candles[candles.Count - 1].Close)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                    }
                }

                // открытие Short
                if (Regime != BotTradeRegime.OnlyLong)
                {
                    if (candles[candles.Count - 3].Open < candles[candles.Count - 3].Close && candles[candles.Count - 2].Open < candles[candles.Count - 2].Close && candles[candles.Count - 1].Open < candles[candles.Count - 1].Close)
                    {
                        _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
                    }
                }
                return;
            }
        }

    }

    /// <summary>
    /// Робот торгующий прорыв Bollinger Bands с подтягивающимся Trailing-Stop по линии Bollinger Bands
    /// </summary>
    public class BollingerOutburst : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="_bollinger">BollingerBands</param>
        /// <param name="Slipage">Проскальзывание</param>
        /// <param name="VolumeFix">Объем для первого входа</param>
        public BollingerOutburst(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _bollinger = new Bollinger(name + "Bollinger", false) { Lenght = 12, ColorUp = Color.DodgerBlue, ColorDown = Color.DarkRed, };
            _bollinger = (Bollinger)_tab.CreateCandleIndicator(_bollinger, "Prime");
            _bollinger.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += ReloadTrailingPosition;

            Slipage = 10;
            VolumeFix = 1;

            Load();

            DeleteEvent += Strategy_DeleteEvent;

        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "BollingerOutburst";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            BollingerOutburstUi ui = new BollingerOutburstUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //индикаторы

        /// <summary>
        /// PriceChannel
        /// </summary>
        private Bollinger _bollinger;

        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// фиксированный объем для входа позицию
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Regime);
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли

        private decimal _lastPrice;
        private decimal _lastBbUp;
        private decimal _lastBbDown;

        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_bollinger.ValuesUp == null || _bollinger.ValuesDown == null)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _lastBbUp = _bollinger.ValuesUp[_bollinger.ValuesUp.Count - 2];
            _lastBbDown = _bollinger.ValuesDown[_bollinger.ValuesDown.Count - 2];

            if (_bollinger.ValuesUp == null || _bollinger.ValuesDown == null || _bollinger.ValuesUp.Count < _bollinger.Lenght + 2)
            {
                return;
            }

            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                LogicClosePosition(candles, openPositions);
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }


        /// <summary>
        /// логика закрытия позиции trailing-stop
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, List<Position> position)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                ReloadTrailingPosition(openPositions[i]);
            }
        }

        /// <summary>
        /// логика закрытия позиции trailing-stop
        /// </summary>
        private void ReloadTrailingPosition(Position position)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].Direction == Side.Buy)
                {
                    _tab.CloseAtTrailingStop(openPositions[i], _bollinger.ValuesDown[_bollinger.ValuesDown.Count - 1], _bollinger.ValuesDown[_bollinger.ValuesDown.Count - 1] - Slipage);
                }
                else
                {
                    _tab.CloseAtTrailingStop(openPositions[i], _bollinger.ValuesUp[_bollinger.ValuesUp.Count - 1], _bollinger.ValuesUp[_bollinger.ValuesUp.Count - 1] + Slipage);
                }
            }
        }


        /// <summary>
        /// логика открытия позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            if (openPositions == null || openPositions.Count == 0)
            {
                // открытие long
                if (Regime != BotTradeRegime.OnlyShort)
                {
                    if (_lastPrice > _lastBbUp)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                    }
                }

                // открытие Short
                if (Regime != BotTradeRegime.OnlyLong)
                {
                    if (_lastPrice < _lastBbDown)
                    {
                        _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
                    }
                }
                return;
            }
        }

    }

    /// <summary>
    /// При закрытии свечи вне канала PriceChannel входим в позицию , стоп-лосс за экстремум прошлойсвечи от свечи входа, тейкпрофит на величину канала от закрытия свечи на которой произошел вход
    /// </summary>
    public class PriceChannelBreak : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public PriceChannelBreak(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _pc = new PriceChannel(name + "PriceChannel", false) { LenghtUpLine = 12, LenghtDownLine = 12, ColorUp = Color.DodgerBlue, ColorDown = Color.DarkRed };
            _pc = (PriceChannel)_tab.CreateCandleIndicator(_pc, "Prime");
            _pc.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += Strateg_PositionOpen;

            Slipage = 10;
            VolumeFix = 1;

            Load();

            DeleteEvent += Strategy_DeleteEvent;

        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "PriceChannelBreak";
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            PriceChannelBreakUi ui = new PriceChannelBreakUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //индикаторы

        /// <summary>
        /// PriceChannel
        /// </summary>
        private PriceChannel _pc;

        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// фиксированный объем для входа позицию
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Regime);
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли

        private decimal _lastPrice;
        private decimal _lastPcUp;
        private decimal _lastPcDown;

        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_pc.ValuesUp == null || _pc.ValuesDown == null)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _lastPcUp = _pc.ValuesUp[_pc.ValuesUp.Count - 2];
            _lastPcDown = _pc.ValuesDown[_pc.ValuesDown.Count - 2];

            if (_pc.ValuesUp == null || _pc.ValuesDown == null || _pc.ValuesUp.Count < _pc.LenghtUpLine + 2 || _pc.ValuesDown.Count < _pc.LenghtDownLine + 2)
            {
                return;
            }

            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        /// <summary>
        /// логика открытия позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            if (openPositions == null || openPositions.Count == 0)
            {
                // открытие long
                if (Regime != BotTradeRegime.OnlyShort)
                {
                    if (_lastPrice > _lastPcUp)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                    }
                }

                // открытие Short
                if (Regime != BotTradeRegime.OnlyLong)
                {
                    if (_lastPrice < _lastPcDown)
                    {
                        _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
                    }
                }
            }
        }

        /// <summary>
        /// выставление стоп-лосс и таке-профит
        /// </summary>
        private void Strateg_PositionOpen(Position position)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].Direction == Side.Buy)
                {
                    decimal lowCandle = _tab.CandlesAll[_tab.CandlesAll.Count - 2].Low;
                    _tab.CloseAtStop(openPositions[i], lowCandle, lowCandle - Slipage);
                    _tab.CloseAtProfit(openPositions[i], _lastPrice + (_lastPcUp - _lastPcDown), (_lastPrice + (_lastPcUp - _lastPcDown)) - Slipage);
                }
                else
                {
                    decimal highCandle = _tab.CandlesAll[_tab.CandlesAll.Count - 2].High;
                    _tab.CloseAtStop(openPositions[i], highCandle, highCandle + Slipage);
                    _tab.CloseAtProfit(openPositions[i], _lastPrice - (_lastPcUp - _lastPcDown), (_lastPrice - (_lastPcUp - _lastPcDown)) + Slipage);
                }

            }
        }


    }

    /// <summary>
    /// Прорыв канала постоенного по PriceChannel+-ATR*коэффициент , дополнительный вход при уходе цены ниже линии канала на ATR*коэффициент. Трейлинг стоп по нижней линии канала PriceChannel
    /// </summary>
    public class PriceChannelVolatility : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public PriceChannelVolatility(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _pc = new PriceChannel(name + "PriceChannel", false) { LenghtUpLine = 12, LenghtDownLine = 12, ColorUp = Color.DodgerBlue, ColorDown = Color.DarkRed };
            _atr = new Atr(name + "ATR", false) { Lenght = 14, ColorBase = Color.DodgerBlue, };

            _pc.Save();
            _atr.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += Strateg_PositionOpen;

            Slipage = 10;
            VolumeFix1 = 1;
            VolumeFix2 = 1;
            LengthAtr = 14;
            KofAtr = 0.5m;
            LengthUp = 12;
            LengthDown = 12;

            Load();

            DeleteEvent += Strategy_DeleteEvent;

        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "PriceChannelVolatility";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            PriceChannelVolatilityUi ui = new PriceChannelVolatilityUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //индикаторы

        /// <summary>
        /// период ATR
        /// </summary>
        public int LengthAtr;

        /// <summary>
        /// период PriceChannel Up
        /// </summary>
        public int LengthUp;

        /// <summary>
        /// период PriceChannel Down
        /// </summary>
        public int LengthDown;

        /// <summary>
        /// PriceChannel
        /// </summary>
        private PriceChannel _pc;

        /// <summary>
        /// ATR
        /// </summary>
        private Atr _atr;

        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// фиксированный объем для входа в первую позицию
        /// </summary>
        public decimal VolumeFix1;

        /// <summary>
        /// фиксированный объем для входа во вторую позицию
        /// </summary>
        public decimal VolumeFix2;

        /// <summary>
        /// коэффициент ATR
        /// </summary>
        public decimal KofAtr;

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Regime);
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix1);
                    writer.WriteLine(VolumeFix2);
                    writer.WriteLine(LengthAtr);
                    writer.WriteLine(KofAtr);
                    writer.WriteLine(LengthUp);
                    writer.WriteLine(LengthDown);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix1 = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix2 = Convert.ToDecimal(reader.ReadLine());
                    LengthAtr = Convert.ToInt32(reader.ReadLine());
                    KofAtr= Convert.ToDecimal(reader.ReadLine());
                    LengthUp = Convert.ToInt32(reader.ReadLine());
                    LengthDown = Convert.ToInt32(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли

        private decimal _lastPcUp;
        private decimal _lastPcDown;
        private decimal _lastAtr;

        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }
            _pc.LenghtUpLine = LengthUp;
            _pc.LenghtDownLine = LengthDown;
            _pc.Process(candles);
            _atr.Lenght = LengthAtr;
            _atr.Process(candles);

            if (_pc.ValuesUp == null || _pc.ValuesDown == null || _atr.Values == null)
            {
                return;
            }

            _lastPcUp = _pc.ValuesUp[_pc.ValuesUp.Count - 1];
            _lastPcDown = _pc.ValuesDown[_pc.ValuesDown.Count - 1];
            _lastAtr = _atr.Values[_atr.Values.Count - 1];

            if (_pc.ValuesUp == null || _pc.ValuesDown == null || _pc.ValuesUp.Count < _pc.LenghtUpLine + 1 ||
                _pc.ValuesDown.Count < _pc.LenghtDownLine + 1 || _atr.Values == null || _atr.Values.Count < _atr.Lenght + 1)
            {
                return;
            }


            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                LogicClosePosition();
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles);
            }
        }

        /// <summary>
        /// логика открытия первой позиции и дополнительного входа
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles )
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            if (openPositions == null || openPositions.Count == 0)
            {
                // открытие long
                if (Regime != BotTradeRegime.OnlyShort)
                {
                    decimal priceEnter = _lastPcUp + (_lastAtr * KofAtr);
                    _tab.BuyAtStop(VolumeFix1, priceEnter + Slipage, priceEnter, StopActivateType.HigherOrEqual);
                }

                // открытие Short
                if (Regime != BotTradeRegime.OnlyLong)
                {
                    decimal priceEnter = _lastPcDown - (_lastAtr * KofAtr);
                    _tab.SellAtStop(VolumeFix1, priceEnter - Slipage, priceEnter, StopActivateType.LowerOrEqyal);
                }
                return;
            }

            // дополнительный вход в позицию
            openPositions = _tab.PositionsOpenAll;
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State == PositionStateType.Open)
                {
                    if (openPositions[i].Direction == Side.Buy)
                    {
                        if (openPositions[i].OpenVolume < (VolumeFix1 + VolumeFix2) &&
                            candles[candles.Count - 1].Close < _lastPcUp - (_lastAtr * KofAtr))
                        {
                            decimal priceEnter = _lastPcUp - (_lastAtr * KofAtr);
                            _tab.BuyAtLimitToPosition(openPositions[i], priceEnter, VolumeFix2);
                        }
                    }
                    else
                    {
                        if (openPositions[i].OpenVolume < (VolumeFix1 + VolumeFix2) &&
                            candles[candles.Count - 1].Close > _lastPcUp - (_lastAtr * KofAtr))
                        {
                            decimal priceEnter = _lastPcDown + (_lastAtr * KofAtr);
                            _tab.SellAtLimitToPosition(openPositions[i], priceEnter, VolumeFix2);
                        }
                    }
                }
            }

        }

        /// <summary>
        /// логика зыкрытия позиции
        /// </summary>
        private void LogicClosePosition()
        {
            // закрытие по стопу
            List<Position> openPositions = _tab.PositionsOpenAll;
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy)
                {
                    decimal priceClose = _lastPcDown;
                    _tab.CloseAtStop(openPositions[i], priceClose, priceClose - Slipage);
                }
                else
                {
                    decimal priceClose = _lastPcUp;
                    _tab.CloseAtStop(openPositions[i], priceClose, priceClose + Slipage);
                }
            }

        }

        // удаление стоп-ордера при входе в позицию
        private void Strateg_PositionOpen(Position position)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {

                if (openPositions[i].Direction == Side.Buy)
                {
                    _tab.SellAtStopCanсel();
                }
                else
                {
                    _tab.BuyAtStopCanсel();
                }

            }
        }

    }

    /// <summary>
    /// конттрендовая стратегия по перекупленности/перепроданности RSI с фильтром по тренду через MovingAverage
    /// </summary>
    public class RsiContrtrend : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="upline">ур. линиии перекупленности</param>
        /// <param name="downline">ур. линиии перепроданности</param>
        /// <param name="_ma">MovingAverage</param>
        /// <param name="_rsi">RSI</param>
        /// <param name="Slipage">Проскальзывание</param>
        /// <param name="VolumeFix">Объем для первого входа</param>
        public RsiContrtrend(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _ma = new MovingAverage(name + "MA", false) { Lenght = 50, ColorBase = Color.CornflowerBlue, };
            _ma = (MovingAverage)_tab.CreateCandleIndicator(_ma, "Prime");

            _rsi = new Rsi(name + "RSI", false) { Lenght = 20, ColorBase = Color.Gold, };
            _rsi = (Rsi)_tab.CreateCandleIndicator(_rsi, "RsiArea");

            Upline = new LineHorisontal("upline", "RsiArea", false)
            {
                Color = Color.Green,
                Value = 0,


            };
            _tab.SetChartElement(Upline);

            Downline = new LineHorisontal("downline", "RsiArea", false)
            {
                Color = Color.Yellow,
                Value = 0

            };
            _tab.SetChartElement(Downline);

            _rsi.Save();
            _ma.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;
            Upline.Value = 65;
            Downline.Value = 35;


            Load();

            Upline.TimeEnd = DateTime.Now;
            Downline.TimeEnd = DateTime.Now;

            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "RsiContrtrend";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            RsiContrtrendUi ui = new RsiContrtrendUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //индикаторы

        /// <summary>
        /// MovingAverage
        /// </summary>
        private MovingAverage _ma;

        /// <summary>
        /// RSI
        /// </summary>
        private Rsi _rsi;

        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// верхняя линия для отрисовки
        /// </summary>
        public LineHorisontal Upline;

        /// <summary>
        /// нижняя линия для отрисовки
        /// </summary>
        public LineHorisontal Downline;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Regime);
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.WriteLine(Upline.Value);
                    writer.WriteLine(Downline.Value);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    Upline.Value = Convert.ToDecimal(reader.ReadLine());
                    Downline.Value = Convert.ToDecimal(reader.ReadLine());
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли

        private decimal _lastPrice;
        private decimal _lastMa;
        private decimal _lastRsi;


        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_ma.Values == null || _rsi.Values == null)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _lastMa = _ma.Values[_ma.Values.Count - 1];
            _lastRsi = _rsi.Values[_rsi.Values.Count - 1];


            if (_ma.Values.Count < _ma.Lenght + 1 || _rsi.Values == null || _rsi.Values.Count < _rsi.Lenght + 5)
            {
                return;

            }


            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);

                    Upline.Refresh();
                    Downline.Refresh();
                }
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        /// <summary>
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            decimal lastClose = candles[candles.Count - 1].Close;
            if (_lastMa > lastClose && _lastRsi > Upline.Value && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
            }
            if (_lastMa < lastClose && _lastRsi < Downline.Value && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
            }
        }

        /// <summary>
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            decimal lastClose = candles[candles.Count - 1].Close;
            if (position.Direction == Side.Buy)
            {
                if (lastClose < _lastMa || _lastRsi > Upline.Value)
                {
                    _tab.CloseAtLimit(position, _lastPrice - Slipage, position.OpenVolume);

                }
            }
            if (position.Direction == Side.Sell)
            {
                if (lastClose > _lastMa || _lastRsi < Downline.Value)
                {
                    _tab.CloseAtLimit(position, _lastPrice + Slipage, position.OpenVolume);

                }
            }
        }

    }

    /// <summary>
    /// пустая стратегия для ручной торговли
    /// </summary>
    public class StrategyEngineCandle : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public StrategyEngineCandle(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            //создание вкладки
            TabCreate(BotTabType.Simple);
        }

        /// <summary>
        /// униальное имя стратегии
        /// </summary>
        /// <returns></returns>
        public override string GetNameStrategyType()
        {
            return "Engine";
        }

        /// <summary>
        /// показать настройки
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            MessageBox.Show("У данной стратегии нет настроек. Это ж привод и сам он ничего не делает.");
        }
    }

    /// <summary>
    ///  робот для парного трейдинга. торговля двумя бумагами на основе их ускорения друг к другу по свечкам
    /// </summary>
    public class PairTraderSimple : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public PairTraderSimple(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab1 = TabsSimple[0];
            _tab1.CandleFinishedEvent += _tab1_CandleFinishedEvent;

            TabCreate(BotTabType.Simple);
            _tab2 = TabsSimple[1];
            _tab2.CandleFinishedEvent += _tab2_CandleFinishedEvent;

            Volume1 = 1;
            Volume2 = 1;

            Slipage1 = 0;
            Slipage2 = 0;

            CountCandles = 5;
            SpreadDeviation = 1m;

            Loss = 0.5m;
            Profit = 0.5m;
            _positionNumbers = new List<PairDealStausSaver>();
            Load();

            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// взять уникальное имя стратегии
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "PairTraderSimple";
        }

        /// <summary>
        /// показать индивидуальное окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            PairTraderSimpleUi ui = new PairTraderSimpleUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// сохранить публичные настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Regime);
                    writer.WriteLine(Volume1);
                    writer.WriteLine(Volume2);

                    writer.WriteLine(Slipage1);
                    writer.WriteLine(Slipage2);

                    writer.WriteLine(CountCandles);
                    writer.WriteLine(SpreadDeviation);

                    writer.WriteLine(Loss);
                    writer.WriteLine(Profit);

                    string positions = "";

                    for (int i = 0; _positionNumbers != null && i < _positionNumbers.Count; i++)
                    {
                        positions += _positionNumbers[i].NumberPositions + "$" + _positionNumbers[i].Spred + "%";
                    }

                    writer.WriteLine(positions);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить публичные настройки из файла
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Volume1 = Convert.ToDecimal(reader.ReadLine());
                    Volume2 = Convert.ToDecimal(reader.ReadLine());

                    Slipage1 = Convert.ToDecimal(reader.ReadLine());
                    Slipage2 = Convert.ToDecimal(reader.ReadLine());

                    CountCandles = Convert.ToInt32(reader.ReadLine());

                    SpreadDeviation = Convert.ToDecimal(reader.ReadLine());

                    Loss = Convert.ToDecimal(reader.ReadLine());
                    Profit = Convert.ToDecimal(reader.ReadLine());

                    string[] positions = reader.ReadLine().Split('%');
                    if (positions.Length != 0)
                    {
                        for (int i = 0; i < positions.Length; i++)
                        {
                            string[] pos = positions[i].Split('$');

                            if (pos.Length == 2)
                            {
                                PairDealStausSaver save = new PairDealStausSaver();
                                save.NumberPositions.Add(Convert.ToInt32(pos[0]));
                                save.Spred = Convert.ToDecimal(pos[1]);
                                _positionNumbers.Add(save);
                            }
                        }
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // публичные настройки

        /// <summary>
        /// режим работы робота
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// количество свечей смотрим назад 
        /// </summary>
        public int CountCandles;

        /// <summary>
        /// расхождение после которого начинаем набирать позицию
        /// </summary>
        public decimal SpreadDeviation;

        public decimal Volume1;

        public decimal Volume2;

        public decimal Slipage1;

        public decimal Slipage2;

        public decimal Loss;

        public decimal Profit;

        private List<PairDealStausSaver> _positionNumbers;

        // торговля

        /// <summary>
        /// вкладка с первым инструметом
        /// </summary>
        private BotTabSimple _tab1;

        /// <summary>
        /// вкладка со вторым инструментом
        /// </summary>
        private BotTabSimple _tab2;

        /// <summary>
        /// готовые свечи первого инструмента
        /// </summary>
        private List<Candle> _candles1;

        /// <summary>
        /// готовые свечи второго инструмента
        /// </summary>
        private List<Candle> _candles2;

        /// <summary>
        /// в первой вкладке новая свеча
        /// </summary>
        void _tab1_CandleFinishedEvent(List<Candle> candles)
        {
            _candles1 = candles;

            if (_candles2 == null ||
                _candles2[_candles2.Count - 1].TimeStart != _candles1[_candles1.Count - 1].TimeStart)
            {
                return;
            }

            CheckExit();
            Trade();
        }

        /// <summary>
        /// во второй вкладки новая свеча
        /// </summary>
        void _tab2_CandleFinishedEvent(List<Candle> candles)
        {
            _candles2 = candles;

            if (_candles1 == null ||
                _candles2[_candles2.Count - 1].TimeStart != _candles1[_candles1.Count - 1].TimeStart)
            {
                return;
            }

            Trade();
            CheckExit();
        }

        /// <summary>
        /// логика входа в позицию
        /// </summary>
        private void Trade()
        {
            if (_candles1.Count - 1 - CountCandles <= 0)
            {
                return;
            }
            // сюда исполнение заходит только когда все свечи 
            // готовы, синхронизированы и только что завершились

            // логика
            // 1 находим на какой процент двигались инструменты последние n свечей
            // 2 если есть расхождение больше чем на установленный процент. покупаем спред

            if (_candles1.Count < 10)
            {
                _positionNumbers = new List<PairDealStausSaver>();
                return;
            }

            if (_positionNumbers == null)
            {
                _positionNumbers = new List<PairDealStausSaver>();
            }

            decimal movePersent1 = 100 / _candles1[_candles1.Count - 1 - CountCandles].Close *
                                   _candles1[_candles1.Count - 1].Close;

            decimal movePersent2 = 100 / _candles2[_candles2.Count - 1 - CountCandles].Close *
                                   _candles2[_candles2.Count - 1].Close;

            if (movePersent1 > movePersent2 &&
                movePersent1 - movePersent2 > SpreadDeviation)
            {
                // Первый инструмент улетел вверх
                List<Position> positons1 = _tab1.PositionOpenShort;

                if (positons1 == null || positons1.Count == 0)
                {
                    Position pos1 = _tab1.SellAtLimit(Volume1, _candles1[_candles1.Count - 1].Close - Slipage1);
                    Position pos2 = _tab2.BuyAtLimit(Volume2, _candles2[_candles2.Count - 1].Close + Slipage2);

                    PairDealStausSaver saver = new PairDealStausSaver();
                    saver.Spred = movePersent1 - movePersent2;
                    saver.NumberPositions.Add(pos1.Number);
                    saver.NumberPositions.Add(pos2.Number);
                    _positionNumbers.Add(saver);
                }
            }

            if (movePersent2 > movePersent1 &&
                movePersent2 - movePersent1 > SpreadDeviation)
            {
                // Второй инструмент улетел вверх
                List<Position> positons2 = _tab2.PositionOpenShort;

                if (positons2 == null || positons2.Count == 0)
                {
                    Position pos1 = _tab2.SellAtLimit(Volume2, _candles2[_candles2.Count - 1].Close - Slipage2);
                    Position pos2 = _tab1.BuyAtLimit(Volume1, _candles1[_candles1.Count - 1].Close + Slipage1);

                    PairDealStausSaver saver = new PairDealStausSaver();
                    saver.Spred = movePersent2 - movePersent1;
                    saver.NumberPositions.Add(pos1.Number);
                    saver.NumberPositions.Add(pos2.Number);
                    _positionNumbers.Add(saver);
                }
            }
        }

        /// <summary>
        /// логика выхода из позиции
        /// </summary>
        private void CheckExit()
        {

            // cчитаем текущий спред
            if (_candles1.Count - 1 - CountCandles < 0)
            {
                return;
            }

            decimal movePersent1 = 100 / _candles1[_candles1.Count - 1 - CountCandles].Close *
                       _candles1[_candles1.Count - 1].Close;

            decimal movePersent2 = 100 / _candles2[_candles2.Count - 1 - CountCandles].Close *
                                   _candles2[_candles2.Count - 1].Close;

            decimal spredNow = Math.Abs(movePersent1 - movePersent2);

            // смотрим есть ли у нас активные позиции

            for (int i = 0; _positionNumbers != null && i < _positionNumbers.Count; i++)
            {
                PairDealStausSaver pairDeal = _positionNumbers[i];

                if (spredNow > pairDeal.Spred &&
                    spredNow - pairDeal.Spred > Loss)
                {
                    NeadToClose(pairDeal.NumberPositions[0]);
                    NeadToClose(pairDeal.NumberPositions[1]);
                    _positionNumbers.Remove(pairDeal);
                    i--;
                    continue;
                }

                if (pairDeal.Spred > spredNow &&
                    pairDeal.Spred - spredNow > Profit)
                {
                    NeadToClose(pairDeal.NumberPositions[0]);
                    NeadToClose(pairDeal.NumberPositions[1]);
                    _positionNumbers.Remove(pairDeal);
                    i--;
                }
            }
        }

        /// <summary>
        /// закрываем позицию по номеру
        /// </summary>
        private void NeadToClose(int positionNum)
        {
            Position pos;

            pos = _tab1.PositionsOpenAll.Find(position => position.Number == positionNum);

            if (pos != null)
            {

                decimal price;

                if (pos.Direction == Side.Buy)
                {
                    price = _tab1.CandlesAll[_tab1.CandlesAll.Count - 1].Close - _tab1.Securiti.PriceStep * 10;
                }
                else
                {
                    price = _tab1.CandlesAll[_tab1.CandlesAll.Count - 1].Close + _tab1.Securiti.PriceStep * 10;
                }

                _tab1.CloseAtLimit(pos, price, pos.OpenVolume);
                return;
            }

            pos = _tab2.PositionsOpenAll.Find(position => position.Number == positionNum);

            if (pos != null)
            {
                decimal price;

                if (pos.Direction == Side.Buy)
                {
                    price = _tab2.CandlesAll[_tab2.CandlesAll.Count - 1].Close - _tab2.Securiti.PriceStep * 10;
                }
                else
                {
                    price = _tab2.CandlesAll[_tab2.CandlesAll.Count - 1].Close + _tab2.Securiti.PriceStep * 10;
                }

                _tab2.CloseAtLimit(pos, price, pos.OpenVolume);
            }
        }
    }

    public class PairDealStausSaver
    {
        /// <summary>
        /// номера позиции
        /// </summary>
        public List<int> NumberPositions = new List<int>();

        /// <summary>
        /// спред на момент входа
        /// </summary>
        public decimal Spred;
    }

    /// <summary>
    /// робот для парного трейдинга строящий спред и торгующий на основе данных о пересечении машек на графике спреда
    /// </summary>
    public class PairTraderSpreadSma : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public PairTraderSpreadSma(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab1 = TabsSimple[0];
            _tab1.CandleFinishedEvent += _tab1_CandleFinishedEvent;

            TabCreate(BotTabType.Simple);
            _tab2 = TabsSimple[1];
            _tab2.CandleFinishedEvent += _tab2_CandleFinishedEvent;

            TabCreate(BotTabType.Index);
            _tabSpread = TabsIndex[0];
            _tabSpread.SpreadChangeEvent += _tabSpread_SpreadChangeEvent;

            _smaLong = new MovingAverage(name + "MovingLong", false) { Lenght = 22, ColorBase = Color.DodgerBlue };
            _smaLong = (MovingAverage)_tabSpread.CreateCandleIndicator(_smaLong, "Prime");
            _smaLong.Save();

            _smaShort = new MovingAverage(name + "MovingShort", false) { Lenght = 3, ColorBase = Color.DarkRed };
            _smaShort = (MovingAverage)_tabSpread.CreateCandleIndicator(_smaShort, "Prime");
            _smaShort.Save();

            Volume1 = 1;
            Volume2 = 1;

            Slipage1 = 0;
            Slipage2 = 0;

            Load();

            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// взять уникальное имя стратегии
        /// </summary>
        /// <returns></returns>
        public override string GetNameStrategyType()
        {
            return "PairTraderSpreadSma";
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            PairTraderSpreadSmaUi ui = new PairTraderSpreadSmaUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// сохранить публичные настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Regime);
                    writer.WriteLine(Volume1);
                    writer.WriteLine(Volume2);

                    writer.WriteLine(Slipage1);
                    writer.WriteLine(Slipage2);


                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить публичные настройки из файла
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Volume1 = Convert.ToDecimal(reader.ReadLine());
                    Volume2 = Convert.ToDecimal(reader.ReadLine());

                    Slipage1 = Convert.ToDecimal(reader.ReadLine());
                    Slipage2 = Convert.ToDecimal(reader.ReadLine());


                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // публичные настройки

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// объём первого инструмента
        /// </summary>
        public decimal Volume1;

        /// <summary>
        /// объём второго инструмента
        /// </summary>
        public decimal Volume2;

        /// <summary>
        /// проскальзоывание для первого инструмента
        /// </summary>
        public decimal Slipage1;

        /// <summary>
        /// проскальзывание для второго инструмента
        /// </summary>
        public decimal Slipage2;

        // торговля

        /// <summary>
        /// вкладка с первым инструметом
        /// </summary>
        private BotTabSimple _tab1;

        /// <summary>
        /// вкладка со вторым инструментом
        /// </summary>
        private BotTabSimple _tab2;

        /// <summary>
        /// вкладка спреда
        /// </summary>
        private BotTabIndex _tabSpread;

        /// <summary>
        /// готовые свечи первого инструмента
        /// </summary>
        private List<Candle> _candles1;

        /// <summary>
        /// готовые свечи второго инструмента
        /// </summary>
        private List<Candle> _candles2;

        /// <summary>
        /// свечи спреда
        /// </summary>
        private List<Candle> _candlesSpread;

        /// <summary>
        /// индикатор: скользящая средняя длинная
        /// </summary>
        private MovingAverage _smaLong;

        /// <summary>
        /// индикатор: скользящая средняя короткая
        /// </summary>
        private MovingAverage _smaShort;

        /// <summary>
        /// в первой вкладке новая свеча
        /// </summary>
        void _tab1_CandleFinishedEvent(List<Candle> candles)
        {
            _candles1 = candles;

            if (_candles2 == null || _candlesSpread == null ||
                _candles2[_candles2.Count - 1].TimeStart != _candles1[_candles1.Count - 1].TimeStart ||
                _candles1[_candles1.Count - 1].TimeStart != _candlesSpread[_candlesSpread.Count - 1].TimeStart)
            {
                return;
            }

            CheckExit();
            Trade();
        }

        /// <summary>
        /// во второй вкладки новая свеча
        /// </summary>
        void _tab2_CandleFinishedEvent(List<Candle> candles)
        {
            _candles2 = candles;

            if (_candles1 == null || _candlesSpread == null ||
                _candles2[_candles2.Count - 1].TimeStart != _candles1[_candles1.Count - 1].TimeStart ||
                _candles2[_candles2.Count - 1].TimeStart != _candlesSpread[_candlesSpread.Count - 1].TimeStart)
            {
                return;
            }

            Trade();
            CheckExit();
        }

        void _tabSpread_SpreadChangeEvent(List<Candle> candles)
        {
            _candlesSpread = candles;

            if (_candles2 == null || _candles1 == null ||
                _candlesSpread[_candlesSpread.Count - 1].TimeStart != _candles1[_candles1.Count - 1].TimeStart ||
                _candlesSpread[_candlesSpread.Count - 1].TimeStart != _candles2[_candles2.Count - 1].TimeStart)
            {
                return;
            }

            CheckExit();
            Trade();
        }

        /// <summary>
        /// логика входа в позицию
        /// </summary>
        private void Trade()
        {
            // сюда исполнение заходит только когда все свечи 
            // готовы, синхронизированы и только что завершились

            // логика
            // 1 если короткая машка на спреде пересекла длинную машку

            if (_candles1.Count < 10)
            {
                return;
            }

            if (StartProgram == StartProgram.IsOsTrader && DateTime.Now.Hour < 10)
            {
                return;
            }

            List<Position> positions = _tab1.PositionsOpenAll;

            if (positions != null && positions.Count != 0)
            { // у нас может быть только одна позиция
                return;
            }

            if (_smaShort.Values == null)
            {
                return;
            }

            decimal smaShortNow = _smaShort.Values[_smaShort.Values.Count - 1];
            decimal smaShortLast = _smaShort.Values[_smaShort.Values.Count - 2];
            decimal smaLong = _smaLong.Values[_smaLong.Values.Count - 1];
            decimal smaLongLast = _smaLong.Values[_smaLong.Values.Count - 1];

            if (smaShortNow == 0 || smaLong == 0
                || smaShortLast == 0 || smaLongLast == 0)
            {
                return;
            }

            if (smaShortLast < smaLongLast &&
                smaShortNow > smaLong)
            {
                // пересекли вверх
                _tab1.SellAtLimit(Volume1, _candles1[_candles1.Count - 1].Close - Slipage1);
                _tab2.BuyAtLimit(Volume2, _candles2[_candles2.Count - 1].Close + Slipage2);
            }

            if (smaShortLast > smaLongLast &&
                smaShortNow < smaLong)
            {
                // пересекли вниз
                _tab2.SellAtLimit(Volume2, _candles2[_candles2.Count - 1].Close - Slipage2);
                _tab1.BuyAtLimit(Volume1, _candles1[_candles1.Count - 1].Close + Slipage1);
            }
        }

        /// <summary>
        /// проверить выходы из позиций
        /// </summary>
        private void CheckExit()
        {
            List<Position> positions = _tab1.PositionsOpenAll;

            if (positions == null || positions.Count == 0)
            { // у нас может быть только одна позиция
                return;
            }

            decimal smaShortNow = _smaShort.Values[_smaShort.Values.Count - 1];
            decimal smaShortLast = _smaShort.Values[_smaShort.Values.Count - 2];
            decimal smaLong = _smaLong.Values[_smaLong.Values.Count - 1];
            decimal smaLongLast = _smaLong.Values[_smaLong.Values.Count - 1];

            if (smaShortNow == 0 || smaLong == 0
                || smaShortLast == 0 || smaLongLast == 0)
            {
                return;
            }

            if (smaShortLast < smaLongLast &&
                smaShortNow > smaLong)
            {
                // пересекли вверх
                List<Position> positions1 = _tab1.PositionOpenLong;
                List<Position> positions2 = _tab2.PositionOpenShort;

                if (positions1 != null && positions1.Count != 0)
                {
                    Position pos1 = positions1[0];
                    _tab1.CloseAtLimit(pos1, _tab1.PriceBestBid - Slipage1, pos1.OpenVolume);
                }

                if (positions2 != null && positions2.Count != 0)
                {
                    Position pos2 = positions2[0];
                    _tab2.CloseAtLimit(pos2, _tab2.PriceBestAsk + Slipage1, pos2.OpenVolume);
                }
            }

            if (smaShortLast > smaLongLast &&
                smaShortNow < smaLong)
            {
                // пересекли вниз
                List<Position> positions1 = _tab1.PositionOpenShort;
                List<Position> positions2 = _tab2.PositionOpenLong;

                if (positions1 != null && positions1.Count != 0)
                {
                    Position pos1 = positions1[0];
                    _tab1.CloseAtLimit(pos1, _tab1.PriceBestAsk + Slipage1, pos1.OpenVolume);
                }

                if (positions2 != null && positions2.Count != 0)
                {
                    Position pos2 = positions2[0];
                    _tab2.CloseAtLimit(pos2, _tab2.PriceBestBid - Slipage1, pos2.OpenVolume);
                }
            }
        }
    }

    /// <summary>
    /// конттрендовая стратегия RSI на перекупленность и перепроданность
    /// </summary>
    public class RsiTrade : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public RsiTrade(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _rsi = new Rsi(name + "RSI", false) { Lenght = 20, ColorBase = Color.Gold, };
            _rsi = (Rsi)_tab.CreateCandleIndicator(_rsi, "RsiArea");

            Upline = new LineHorisontal("upline", "RsiArea", false)
            {
                Color = Color.Green,
                Value = 0,


            };
            _tab.SetChartElement(Upline);

            Downline = new LineHorisontal("downline", "RsiArea", false)
            {
                Color = Color.Yellow,
                Value = 0

            };
            _tab.SetChartElement(Downline);

            _rsi.Save();


            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;
            Upline.Value = 65;
            Downline.Value = 35;


            Load();

            Upline.TimeEnd = DateTime.Now;
            Downline.TimeEnd = DateTime.Now;

            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "RsiTrade";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            RsiTradeUi ui = new RsiTradeUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //индикаторы

        /// <summary>
        /// RSI
        /// </summary>
        private Rsi _rsi;

        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// верхняя линия для отрисовки
        /// </summary>
        public LineHorisontal Upline;

        /// <summary>
        /// нижняя линия для отрисовки
        /// </summary>
        public LineHorisontal Downline;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.WriteLine(Regime);
                    writer.WriteLine(Upline.Value);
                    writer.WriteLine(Downline.Value);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Upline.Value = Convert.ToDecimal(reader.ReadLine());
                    Downline.Value = Convert.ToDecimal(reader.ReadLine());
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли

        private decimal _lastPrice;
        private decimal _firstLastRsi;
        private decimal _secondLastRsi;


        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_rsi.Values == null)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _firstLastRsi = _rsi.Values[_rsi.Values.Count - 1];
            _secondLastRsi = _rsi.Values[_rsi.Values.Count - 2];


            if (_rsi.Values == null || _rsi.Values.Count < _rsi.Lenght + 5)
            {
                return;

            }


            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);

                    Upline.Refresh();
                    Downline.Refresh();
                }
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        /// <summary>
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_secondLastRsi < Downline.Value && _firstLastRsi > Downline.Value && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
            }

            if (_secondLastRsi > Upline.Value && _firstLastRsi < Upline.Value && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
            }
        }

        /// <summary>
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {

            if (position.State == PositionStateType.Closing)
            {
                return;
            }
            if (position.Direction == Side.Buy)
            {
                if (_secondLastRsi >= Upline.Value && _firstLastRsi <= Upline.Value)
                {
                    _tab.CloseAtLimit(position, _lastPrice - Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyLong && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
                    }

                }
            }
            if (position.Direction == Side.Sell)
            {
                if (_secondLastRsi <= Downline.Value && _firstLastRsi >= Downline.Value)
                {
                    _tab.CloseAtLimit(position, _lastPrice + Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyShort && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                    }

                }
            }
        }

    }

    /// <summary>
    /// конттрендовая стратегия Stochastic
    /// </summary>
    public class StochasticTrade : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public StochasticTrade(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _stoch = new StochasticOscillator(name + "Stochastic", false);
            _stoch = (StochasticOscillator)_tab.CreateCandleIndicator(_stoch, "StochasticArea");
            
            Upline = new LineHorisontal("upline", "StochasticArea", false)
            {
                Color = Color.Green,
                Value = 0,


            };
            _tab.SetChartElement(Upline);

            Downline = new LineHorisontal("downline", "StochasticArea", false)
            {
                Color = Color.Yellow,
                Value = 0

            };
            _tab.SetChartElement(Downline);

            _stoch.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;
            Upline.Value = 80;
            Downline.Value = 20;

            Upline.TimeEnd = DateTime.Now;
            Downline.TimeEnd = DateTime.Now;

            Load();

            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "StochasticTrade";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            StochasticTradeUi ui = new StochasticTradeUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //индикаторы

        /// <summary>
        /// Стохастик
        /// </summary>
        private StochasticOscillator _stoch;

        /// <summary>
        /// верхняя линия для отрисовки
        /// </summary>
        public LineHorisontal Upline;

        /// <summary>
        /// нижняя линия для отрисовки
        /// </summary>
        public LineHorisontal Downline;

        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.WriteLine(Regime);
                    writer.WriteLine(Upline.Value);
                    writer.WriteLine(Downline.Value);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Upline.Value = Convert.ToDecimal(reader.ReadLine());
                    Downline.Value = Convert.ToDecimal(reader.ReadLine());
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли

        private decimal _lastPrice;
        private decimal _stocLastUp;
        private decimal _stocLastDown;


        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {


            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_stoch.ValuesUp == null)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _stocLastUp = _stoch.ValuesUp[_stoch.ValuesUp.Count - 1];
            _stocLastDown = _stoch.ValuesDown[_stoch.ValuesDown.Count - 1];



            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);

                    Upline.Refresh();
                    Downline.Refresh();
                }
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        /// <summary>
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_stocLastDown < Downline.Value && _stocLastDown > _stocLastUp && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
            }

            if (_stocLastDown > Upline.Value && _stocLastDown < _stocLastUp && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
            }

        }

        /// <summary>
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.Direction == Side.Buy)
            {
                if (_stocLastDown > Upline.Value && _stocLastDown < _stocLastUp)
                {
                    _tab.CloseAtLimit(position, _lastPrice - Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyLong && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
                    }
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_stocLastDown < Downline.Value && _stocLastDown > _stocLastUp)
                {
                    _tab.CloseAtLimit(position, _lastPrice + Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyShort && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                    }

                }
            }
        }

    }

    /// <summary>
    /// Трендовая стратегия на основе пробития линий болинджера
    /// </summary>
    public class BollingerTrade : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public BollingerTrade(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _bol = new Bollinger(name + "Bollinger", false);
            _bol = (Bollinger)_tab.CreateCandleIndicator(_bol, "Prime");

            _bol.Save();


            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;

            Load();

            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "BollingerTrade";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            BollingerTradeUi ui = new BollingerTradeUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //индикаторы

        /// <summary>
        /// Болинджер индикатор
        /// </summary>
        private Bollinger _bol;

        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.WriteLine(Regime);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли

        private decimal _lastPrice;
        private decimal _bolLastUp;
        private decimal _bolLastDown;

        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_bol.ValuesDown == null || candles.Count < _bol.Lenght + 2)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _bolLastUp = _bol.ValuesUp[_bol.ValuesUp.Count - 1];
            _bolLastDown = _bol.ValuesDown[_bol.ValuesDown.Count - 1];



            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);
                }
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        /// <summary>
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_lastPrice > _bolLastUp && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
            }

            if (_lastPrice < _bolLastDown && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
            }

        }

        /// <summary>
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.State == PositionStateType.Closing)
            {
                return;
            }

            if (position.Direction == Side.Buy)
            {
                if (_lastPrice < _bolLastDown)
                {
                    _tab.CloseAtLimit(position, _lastPrice - Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyLong && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
                    }
                }
            }
            if (position.Direction == Side.Sell)
            {
                if (_lastPrice > _bolLastUp)
                {
                    _tab.CloseAtLimit(position, _lastPrice + Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyShort && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                    }

                }
            }

        }

    }

    /// <summary>
    /// Трендовая стратегия на основе индикатора TRIX
    /// </summary>
    public class TrixTrade : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public TrixTrade(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _trix = new Trix(name + "Trix", false);
            _trix = (Trix)_tab.CreateCandleIndicator(_trix, "TrixArea");

            _trix.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;
            Step = 0.02m;

            Load();

            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "TRIXTrade";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            TrixTradeUi ui = new TrixTradeUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //индикаторы

        /// <summary>
        /// Trix индикатор
        /// </summary>
        private Trix _trix;

        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// Шаг от 0 - го уровня
        /// </summary>
        public decimal Step;

        /// <summary>
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.WriteLine(Regime);
                    writer.WriteLine(Step);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Step = Convert.ToDecimal(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли

        private decimal _lastPrice;
        private decimal _lastTrix;

        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_trix.Values == null)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _lastTrix = _trix.Values[_trix.Values.Count - 1];


            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);
                }
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        /// <summary>
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_lastTrix > Step && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
            }

            if (_lastTrix < -Step && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
            }
        }

        /// <summary>
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.State == PositionStateType.Closing)
            {
                return;
            }
            if (position.Direction == Side.Buy)
            {
                if (_lastTrix < -Step)
                {
                    _tab.CloseAtLimit(position, _lastPrice - Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyLong && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
                    }

                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastTrix > Step)
                {
                    _tab.CloseAtLimit(position, _lastPrice + Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyShort && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                    }

                }
            }
        }

    }

    /// <summary>
    /// Контртрендовая стратегия на основе индикатора CCI
    /// </summary>
    public class CciTrade : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public CciTrade(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _cci = new Cci(name + "Cci", false);
            _cci = (Cci)_tab.CreateCandleIndicator(_cci, "CciArea");

            Upline = new LineHorisontal("upline", "CciArea", false)
            {
                Color = Color.Green,
                Value = 0,

            };
            _tab.SetChartElement(Upline);

            Downline = new LineHorisontal("downline", "CciArea", false)
            {
                Color = Color.Yellow,
                Value = 0

            };
            _tab.SetChartElement(Downline);

            _cci.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;
            Upline.Value = 150;
            Downline.Value = -150;

            Load();

            DeleteEvent += Strategy_DeleteEvent;
        }



        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "CCITrade";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            CciTradeUi ui = new CciTradeUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //индикаторы

        /// <summary>
        /// CCI индикатор
        /// </summary>
        private Cci _cci;

        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// верхняя линия для отрисовки
        /// </summary>
        public LineHorisontal Upline;

        /// <summary>
        /// нижняя линия для отрисовки
        /// </summary>
        public LineHorisontal Downline;

        /// <summary>
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.WriteLine(Regime);
                    writer.WriteLine(Upline.Value);
                    writer.WriteLine(Downline.Value);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Upline.Value = Convert.ToDecimal(reader.ReadLine());
                    Downline.Value = Convert.ToDecimal(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли

        private decimal _lastPrice;
        private decimal _lastCci;

        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_cci.Values == null)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _lastCci = _cci.Values[_cci.Values.Count - 1];


            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);

                    Upline.Refresh();
                    Downline.Refresh();
                }
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        /// <summary>
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_lastCci < Downline.Value && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
            }

            if (_lastCci > Upline.Value && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
            }

        }

        /// <summary>
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.Direction == Side.Buy)
            {
                if (_lastCci > Upline.Value)
                {
                    _tab.CloseAtLimit(position, _lastPrice - Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyLong && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
                    }
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastCci < Downline.Value)
                {
                    _tab.CloseAtLimit(position, _lastPrice + Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyShort && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                    }

                }
            }
        }

    }

    /// <summary>
    /// Трендовая стратегия на пересечение индикатора ParabolicSar
    /// </summary>
    public class ParabolicSarTrade : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public ParabolicSarTrade(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _sar = new ParabolicSaR(name + "Prime", false);
            _sar = (ParabolicSaR)_tab.CreateCandleIndicator(_sar, "Prime");



            _sar.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;


            Load();

            DeleteEvent += Strategy_DeleteEvent;
        }



        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "ParabolicSarTrade";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            ParabolicSarTradeUi ui = new ParabolicSarTradeUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //индикаторы

        /// <summary>
        /// ParabolicSar индикатор
        /// </summary>
        private ParabolicSaR _sar;

        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.WriteLine(Regime);


                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);


                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли

        private decimal _lastPrice;
        private decimal _lastSar;

        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_sar.Values == null)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _lastSar = _sar.Values[_sar.Values.Count - 1];


            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);

                }
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        /// <summary>
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_lastPrice > _lastSar && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
            }

            if (_lastPrice < _lastSar && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
            }

        }

        /// <summary>
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.Direction == Side.Buy)
            {
                if (_lastPrice < _lastSar)
                {
                    _tab.CloseAtLimit(position, _lastPrice - Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyLong && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
                    }
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastPrice > _lastSar)
                {
                    _tab.CloseAtLimit(position, _lastPrice + Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyShort && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                    }

                }
            }
        }

    }

    /// <summary>
    /// Трендовая стратегия на пересечение индикатора PriceChannel
    /// </summary>
    public class PriceChannelTrade : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public PriceChannelTrade(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _priceCh = new PriceChannel(name + "Prime", false);
            _priceCh = (PriceChannel)_tab.CreateCandleIndicator(_priceCh, "Prime");



            _priceCh.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;


            Load();

            DeleteEvent += Strategy_DeleteEvent;
        }



        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "PriceChannelTrade";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            PriceChannelTradeUi ui = new PriceChannelTradeUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //индикаторы

        /// <summary>
        /// PriceChannel индикатор
        /// </summary>
        private PriceChannel _priceCh;

        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.WriteLine(Regime);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);


                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли
        private decimal _lastPriceC;
        private decimal _lastPriceH;
        private decimal _lastPriceL;
        private decimal _lastPriceChUp;
        private decimal _lastPriceChDown;
        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_priceCh.ValuesUp == null || _priceCh.ValuesUp.Count < _priceCh.LenghtUpLine + 3
                || _priceCh.ValuesDown.Count < _priceCh.LenghtDownLine + 3)
            {
                return;
            }

            _lastPriceC = candles[candles.Count - 1].Close;
            _lastPriceH = candles[candles.Count - 1].High;
            _lastPriceL = candles[candles.Count - 1].Low;
            _lastPriceChUp = _priceCh.ValuesUp[_priceCh.ValuesUp.Count - 2];
            _lastPriceChDown = _priceCh.ValuesDown[_priceCh.ValuesDown.Count - 2];

            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);

                }
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        /// <summary>
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_lastPriceH > _lastPriceChUp && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastPriceC + Slipage);
            }

            if (_lastPriceL < _lastPriceChDown && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastPriceC - Slipage);
            }
        }

        /// <summary>
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.Direction == Side.Buy)
            {
                if (_lastPriceL < _lastPriceChDown)
                {
                    _tab.CloseAtLimit(position, _lastPriceC - Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyLong && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.SellAtLimit(VolumeFix, _lastPriceC - Slipage);
                    }
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastPriceH > _lastPriceChUp)
                {
                    _tab.CloseAtLimit(position, _lastPriceC + Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyShort && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastPriceC + Slipage);
                    }

                }
            }

        }

    }

    /// <summary>
    /// Трендовая стратегия на основе двух индикаторов BullsPower и BearsPower
    /// </summary>
    public class BbPowerTrade : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public BbPowerTrade(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _bearsP = new BearsPower(name + "BearsPower", false);
            _bearsP = (BearsPower)_tab.CreateCandleIndicator(_bearsP, "BearsArea");

            _bullsP = new BullsPower(name + "BullsPower", false);
            _bullsP = (BullsPower)_tab.CreateCandleIndicator(_bullsP, "BullsArea");


            _bearsP.Save();
            _bullsP.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;
            Step = 100;

            Load();

            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "BBPowerTrade";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            BbPowerTradeUi ui = new BbPowerTradeUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //индикаторы

        /// <summary>
        /// BullsPower индикатор
        /// </summary>
        private BullsPower _bullsP;

        /// <summary>
        /// BearsPower индикатор
        /// </summary>
        private BearsPower _bearsP;

        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// шаг от 0-го уровня
        /// </summary>
        public decimal Step;

        /// <summary>
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.WriteLine(Regime);
                    writer.WriteLine(Step);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Step = Convert.ToDecimal(reader.ReadLine());


                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли
        private decimal _lastPrice;
        private decimal _lastBearsPrice;
        private decimal _lastBullsPrice;

        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_bearsP.Values == null || _bullsP.Values == null || _bullsP.Values.Count < _bullsP.Period + 2)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _lastBearsPrice = _bearsP.Values[_bearsP.Values.Count - 1];
            _lastBullsPrice = _bullsP.Values[_bullsP.Values.Count - 1];

            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);
                }
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        /// <summary>
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_lastBullsPrice + _lastBearsPrice > Step && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
            }

            if (_lastBullsPrice + _lastBearsPrice < -Step && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
            }

        }

        /// <summary>
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.Direction == Side.Buy)
            {
                if (_lastBullsPrice + _lastBearsPrice < -Step)
                {
                    _tab.CloseAtLimit(position, _lastPrice - Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyLong && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
                    }
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastBullsPrice + _lastBearsPrice > Step)
                {
                    _tab.CloseAtLimit(position, _lastPrice + Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyShort && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                    }

                }
            }
        }
    }

    /// <summary>
    /// Трендовая стратегия на пересечение индикатора MACD
    /// </summary>
    public class MacdTrade : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public MacdTrade(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _macd = new MacdLine(name + "MacdArea", false);
            _macd = (MacdLine)_tab.CreateCandleIndicator(_macd, "MacdArea");


            _macd.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;


            Load();

            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "MACDTrade";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            MacdTradeUi ui = new MacdTradeUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //индикаторы

        /// <summary>
        /// MACD индикатор
        /// </summary>
        private MacdLine _macd;

        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.WriteLine(Regime);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);


                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли
        private decimal _lastPrice;
        private decimal _lastMacdUp;
        private decimal _lastMacdDown;
        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_macd.ValuesUp == null)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _lastMacdUp = _macd.ValuesUp[_macd.ValuesUp.Count - 1];
            _lastMacdDown = _macd.ValuesDown[_macd.ValuesDown.Count - 1];

            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);

                }
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        /// <summary>
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_lastMacdDown < 0 && _lastMacdUp > _lastMacdDown && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
            }

            if (_lastMacdDown > 0 && _lastMacdUp < _lastMacdDown && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
            }
        }

        /// <summary>
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.Direction == Side.Buy)
            {
                if (_lastMacdDown > 0 && _lastMacdUp < _lastMacdDown)
                {
                    _tab.CloseAtLimit(position, _lastPrice - Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyLong && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
                    }
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastMacdDown < 0 && _lastMacdUp > _lastMacdDown)
                {
                    _tab.CloseAtLimit(position, _lastPrice + Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyShort && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                    }

                }
            }
        }

    }

    /// <summary>
    /// Контртрендовая стратегия на основе индикатора Willams %R
    /// </summary>
    public class WilliamsRangeTrade : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public WilliamsRangeTrade(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _wr = new WilliamsRange(name + "WillamsRange", false);
            _wr = (WilliamsRange)_tab.CreateCandleIndicator(_wr, "WilliamsArea");

            Upline = new LineHorisontal("upline", "WilliamsArea", false)
            {
                Color = Color.Green,
                Value = 0,


            };
            _tab.SetChartElement(Upline);

            Downline = new LineHorisontal("downline", "WilliamsArea", false)
            {
                Color = Color.Yellow,
                Value = 0

            };
            _tab.SetChartElement(Downline);

            _wr.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;
            Upline.Value = -20;
            Downline.Value = -80;

            Load();

            Upline.TimeEnd = DateTime.Now;
            Downline.TimeEnd = DateTime.Now;

            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "WilliamsRangeTrade";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            WilliamsRangeTradeUi ui = new WilliamsRangeTradeUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //индикаторы

        /// <summary>
        /// WilliamsRange индикатор
        /// </summary>
        private WilliamsRange _wr;

        /// <summary>
        /// верхняя линия для отрисовки
        /// </summary>
        public LineHorisontal Upline;

        /// <summary>
        /// нижняя линия для отрисовки
        /// </summary>
        public LineHorisontal Downline;

        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.WriteLine(Regime);
                    writer.WriteLine(Upline.Value);
                    writer.WriteLine(Downline.Value);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Upline.Value = Convert.ToDecimal(reader.ReadLine());
                    Downline.Value = Convert.ToDecimal(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли
        private decimal _lastPrice;
        private decimal _lastWr;

        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_wr.Values == null || _wr.Values.Count < _wr.Nperiod + 2)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _lastWr = _wr.Values[_wr.Values.Count - 1];

            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);

                    Upline.Refresh();
                    Downline.Refresh();
                }
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        /// <summary>
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_lastWr < Downline.Value && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
            }

            if (_lastWr > Upline.Value && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
            }
        }

        /// <summary>
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.Direction == Side.Buy)
            {
                if (_lastWr > Upline.Value)
                {
                    _tab.CloseAtLimit(position, _lastPrice - Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyLong && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.SellAtLimit(VolumeFix, _lastPrice - Slipage);
                    }

                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastWr < Downline.Value)
                {
                    _tab.CloseAtLimit(position, _lastPrice + Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyShort && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                    }

                }
            }
        }

    }

    /// <summary>
    /// Трендовая стратегия на основе индикатора Macd и трейлстопа
    /// </summary>
    public class MacdTrail : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public MacdTrail(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _macd = new MacdLine(name + "MACD", false);
            _macd = (MacdLine)_tab.CreateCandleIndicator(_macd, "MacdArea");


            _macd.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;
            TrailStop = 2000;
            Step = 50;


            Load();


            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "MacdTrail";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            MacdTrailUi ui = new MacdTrailUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //индикаторы

        /// <summary>
        /// Macd индикатор
        /// </summary>
        private MacdLine _macd;


        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// Значение ТрейлСтоп
        /// </summary>
        public decimal TrailStop;

        /// <summary>
        /// Шаг ТрейлСтоп
        /// </summary>
        public decimal Step;

        /// <summary>
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;


        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.WriteLine(Regime);
                    writer.WriteLine(TrailStop);
                    writer.WriteLine(Step);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    TrailStop = Convert.ToDecimal(reader.ReadLine());
                    Step = Convert.ToDecimal(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли
        private decimal _lastClose;
        private decimal _lastLastClose;
        private decimal _lastMacdDown;
        private decimal _lastMacdUp;
        private decimal _awG;

        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_macd.ValuesUp == null)
            {
                return;
            }

            _lastClose = candles[candles.Count - 1].Close;
            _lastLastClose = candles[candles.Count - 2].Close;
            _lastMacdUp = _macd.ValuesUp[_macd.ValuesUp.Count - 1];
            _lastMacdDown = _macd.ValuesDown[_macd.ValuesDown.Count - 1];

            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);

                }
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        /// <summary>
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_lastMacdDown < 0 && _lastMacdUp > _lastMacdDown && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastClose + Slipage);
                _awG = _lastClose - TrailStop;
            }
            if (_lastMacdDown > 0 && _lastMacdUp < _lastMacdDown && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastClose - Slipage);
                _awG = _lastClose + TrailStop;
            }

        }

        /// <summary>
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.Direction == Side.Buy)
            {
                if (_lastClose > _lastLastClose)
                {
                    _awG = _awG + Step;
                }

                _tab.CloseAtStop(position, _awG, _lastClose);
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastClose < _lastLastClose)
                {
                    _awG = _awG - Step;
                }

                _tab.CloseAtStop(position, _awG, _lastClose);
            }
        }

    }

    /// <summary>
    /// Трендовая стратегия на основе 2х индикаторов Sma и RSI
    /// </summary>
    public class SmaStochastic : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public SmaStochastic(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _sma = new MovingAverage(name + "Sma", false);
            _sma = (MovingAverage)_tab.CreateCandleIndicator(_sma, "Prime");
            _sma.Save();

            _stoc = new StochasticOscillator(name + "ST", false);
            _stoc = (StochasticOscillator)_tab.CreateCandleIndicator(_stoc, "StocArea");
            _stoc.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;
            Step = 500;
            Upline = 70;
            Downline = 30;


            Load();


            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "SmaStochastic";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            SmaStochasticUi ui = new SmaStochasticUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //индикаторы

        /// <summary>
        /// MovingAverage индикатор
        /// </summary>
        private MovingAverage _sma;

        /// <summary>
        /// Stochastic индикатор
        /// </summary>
        private StochasticOscillator _stoc;

        //настройки публичные

        /// <summary>
        /// верхняя граница стохастика
        /// </summary>
        public decimal Upline;

        /// <summary>
        /// нижняя граница стохастика
        /// </summary>
        public decimal Downline;

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// Шаг 
        /// </summary>
        public decimal Step;

        /// <summary>
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;


        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.WriteLine(Regime);
                    writer.WriteLine(Step);
                    writer.WriteLine(Upline);
                    writer.WriteLine(Downline);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Upline = Convert.ToDecimal(reader.ReadLine());
                    Downline = Convert.ToDecimal(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли
        private decimal _lastClose;
        private decimal _firstLastRsi;
        private decimal _secondLastRsi;
        private decimal _lastSma;

        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_sma.Values == null || _stoc.ValuesUp == null ||
                _sma.Lenght + 3 > _sma.Values.Count || _stoc.P1 + 3 > _stoc.ValuesUp.Count)
            {
                return;
            }

            _lastClose = candles[candles.Count - 1].Close;
            _firstLastRsi = _stoc.ValuesUp[_stoc.ValuesUp.Count - 1];
            _secondLastRsi = _stoc.ValuesUp[_stoc.ValuesUp.Count - 2];
            _lastSma = _sma.Values[_sma.Values.Count - 1];


            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);

                }
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        /// <summary>
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_lastClose > _lastSma + Step && _secondLastRsi <= Downline && _firstLastRsi >= Downline &&
                Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastClose + Slipage);
            }

            if (_lastClose < _lastSma - Step && _secondLastRsi >= Upline && _firstLastRsi <= Upline &&
                Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastClose - Slipage);
            }
        }

        /// <summary>
        /// логика зыкрытия позиции и открытие 
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.Direction == Side.Buy)
            {
                if (_lastClose < _lastSma - Step)
                {
                    _tab.CloseAtLimit(position, _lastClose - Slipage, position.OpenVolume);
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastClose > _lastSma + Step)
                {
                    _tab.CloseAtLimit(position, _lastClose - Slipage, position.OpenVolume);
                }
            }
        }

    }

    /// <summary>
    /// Трендовая стратегия на основе 2х индикаторов Momentum и Macd
    /// </summary>
    public class MomentumMacd : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public MomentumMacd(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _macd = new MacdLine(name + "Macd", false);
            _macd = (MacdLine)_tab.CreateCandleIndicator(_macd, "MacdArea");
            _macd.Save();

            _mom = new Momentum(name + "Momentum", false);
            _mom = (Momentum)_tab.CreateCandleIndicator(_mom, "Momentum");
            _mom.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;

            Load();


            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "MomentumMACD";
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            MomentumMacdUi ui = new MomentumMacdUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //индикаторы

        /// <summary>
        /// Macd индикатор
        /// </summary>
        private MacdLine _macd;

        /// <summary>
        /// Momentum индикатор
        /// </summary>
        private Momentum _mom;

        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;


        /// <summary>
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;


        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.WriteLine(Regime);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);


                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        private void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли
        private decimal _lastClose;

        private decimal _lastMacdUp;
        private decimal _lastMacdDown;

        private decimal _lastMom;

        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_macd.ValuesUp == null || _macd.ValuesDown == null ||
                _mom.Nperiod + 3 > _mom.Values.Count)
            {
                return;
            }

            _lastClose = candles[candles.Count - 1].Close;
            _lastMacdUp = _macd.ValuesUp[_macd.ValuesUp.Count - 1];
            _lastMacdDown = _macd.ValuesDown[_macd.ValuesDown.Count - 1];
            _lastMom = _mom.Values[_mom.Values.Count - 1];


            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);

                }
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        /// <summary>
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_lastMacdUp > _lastMacdDown && _lastMom > 100 && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastClose + Slipage);
            }
            if (_lastMacdUp < _lastMacdDown && _lastMom < 100 && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastClose - Slipage);
            }
        }

        /// <summary>
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.Direction == Side.Buy)
            {
                if (_lastMacdUp < _lastMacdDown && _lastMom < 100)
                {
                    _tab.CloseAtLimit(position, _lastClose - Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyLong && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.SellAtLimit(VolumeFix, _lastClose - Slipage);
                    }
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastMacdUp > _lastMacdDown && _lastMom > 100)
                {
                    _tab.CloseAtLimit(position, _lastClose + Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyShort && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastClose + Slipage);
                    }
                }
            }

        }
    }

    /// <summary>
    /// Трендовая стратегия на основе свечной формации пинбара и пробития Sma
    /// </summary>
    public class PinBarTrade : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public PinBarTrade(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Sma = new MovingAverage(name + "MA", false);
            Sma = (MovingAverage)_tab.CreateCandleIndicator(Sma, "Prime");
            Sma.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;

            Load();


            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "PinBarTrade";
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            PinBarTradeUi ui = new PinBarTradeUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //индикаторы

        public MovingAverage Sma;

        //настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;


        /// <summary>
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;


        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.WriteLine(Regime);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);


                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        private void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли
        private decimal _lastClose;
        private decimal _lastOpen;
        private decimal _lastHigh;
        private decimal _lastLow;

        private decimal _lastSma;

        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (Sma.Values.Count < Sma.Lenght + 2)
            {
                return;
            }


            _lastClose = candles[candles.Count - 1].Close;
            _lastOpen = candles[candles.Count - 1].Open;
            _lastHigh = candles[candles.Count - 1].High;
            _lastLow = candles[candles.Count - 1].Low;
            _lastSma = Sma.Values[Sma.Values.Count - 1];


            // распределяем логику в зависимости от текущей позиции

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);

                }
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }



        /// <summary>
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_lastClose >= _lastHigh - ((_lastHigh - _lastLow) / 3) && _lastOpen >= _lastHigh - ((_lastHigh - _lastLow) / 3)
                && _lastSma < _lastClose && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastClose + Slipage);
            }
            if (_lastClose <= _lastLow + ((_lastHigh - _lastLow) / 3) && _lastOpen <= _lastLow + ((_lastHigh - _lastLow) / 3)
                && _lastSma > _lastClose && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastClose + Slipage);

            }

        }

        /// <summary>
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.Direction == Side.Buy)
            {
                if (_lastClose <= _lastLow + ((_lastHigh - _lastLow) / 3) && _lastOpen <= _lastLow + ((_lastHigh - _lastLow) / 3)
                     && _lastSma > _lastClose)
                {
                    _tab.CloseAtLimit(position, _lastClose - Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyLong && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.SellAtLimit(VolumeFix, _lastClose - Slipage);
                    }
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastClose >= _lastHigh - ((_lastHigh - _lastLow) / 3) && _lastOpen >= _lastHigh - ((_lastHigh - _lastLow) / 3)
                     && _lastSma < _lastClose)
                {
                    _tab.CloseAtLimit(position, _lastClose + Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyShort && Regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastClose + Slipage);
                    }

                }
            }

        }
    }

    /// <summary>
    /// Парная торговля на основе индикатора RSI
    /// </summary>
    public class PairRsiTrade : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public PairRsiTrade(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab1 = TabsSimple[0];
            _tab1.CandleFinishedEvent += _tab1_CandleFinishedEvent;

            TabCreate(BotTabType.Simple);
            _tab2 = TabsSimple[1];
            _tab2.CandleFinishedEvent += _tab2_CandleFinishedEvent;

            _rsi1 = new Rsi( name + "RSI1", false) {Lenght = 25, ColorBase = Color.Gold };
            _rsi1 = (Rsi) _tab1.CreateCandleIndicator(_rsi1, "Rsi1_Area");
            _rsi1.Save();

            _rsi2 = new Rsi(name + "RSI2", false) {Lenght = 25, ColorBase = Color.GreenYellow};
            _rsi2 = (Rsi) _tab2.CreateCandleIndicator(_rsi2, "Rsi2_Area");
            _rsi2.Save();

            RsiSpread = 20;

            Volume1 = 1;
            Volume2 = 1;
            
            Load();

            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// взять уникальное имя стратегии
        /// </summary>
        /// <returns></returns>
        public override string GetNameStrategyType()
        {
            return "PairRsiTrade";
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            PairRsiTradeUi ui = new PairRsiTradeUi(this);
            ui.ShowDialog(); 
        }

        /// <summary>
        /// сохранить публичные настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Volume1);
                    writer.WriteLine(Volume2);
                    writer.WriteLine(RsiSpread);


                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить публичные настройки из файла
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Volume1 = Convert.ToDecimal(reader.ReadLine());
                    Volume2 = Convert.ToDecimal(reader.ReadLine());
                    RsiSpread = Convert.ToInt32(reader.ReadLine());


                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // публичные настройки

       /// <summary>
        /// объём первого инструмента
        /// </summary>
        public int RsiSpread;

        /// <summary>
        /// объём первого инструмента
        /// </summary>
        public decimal Volume1;

        /// <summary>
        /// объём второго инструмента
        /// </summary>
        public decimal Volume2;

        /// <summary>
        /// вкладка с первым инструметом
        /// </summary>
        private BotTabSimple _tab1;

        /// <summary>
        /// вкладка со вторым инструментом
        /// </summary>
        private BotTabSimple _tab2;

        /// <summary>
        /// готовые свечи первого инструмента
        /// </summary>
        private List<Candle> _candles1;

        /// <summary>
        /// готовые свечи второго инструмента
        /// </summary>
        private List<Candle> _candles2;

        /// <summary>
        /// индикатор: скользящая средняя длинная
        /// </summary>
        private Rsi _rsi1;

        /// <summary>
        /// индикатор: скользящая средняя короткая
        /// </summary>
        private Rsi _rsi2;

        void _tab1_CandleFinishedEvent(List<Candle> candles)
        {
            _candles1 = candles;

            if (_candles2 == null ||
                _candles2[_candles2.Count - 1].TimeStart != _candles1[_candles1.Count - 1].TimeStart )
            {
                return;
            }

            CheckExit();
            Trade();
        }

        /// <summary>
        /// во второй вкладки новая свеча
        /// </summary>
        void _tab2_CandleFinishedEvent(List<Candle> candles)
        {
            _candles2 = candles;

            if (_candles1 == null ||
                _candles2[_candles2.Count -1].TimeStart != _candles1[_candles1.Count - 1].TimeStart)
            {
                return;
            }

            CheckExit();
            Trade();
        }

        private void Trade()
        {
            // сюда исполнение заходит только когда все свечи 
            // готовы, синхронизированы и только что завершились

            // логика
            // 

            if (_candles1.Count < 10 && _candles2.Count < 10)
            {
                return;;
            }

            List<Position> pos1 = _tab1.PositionsOpenAll;
            List<Position> pos2 = _tab2.PositionsOpenAll;

            if (pos1 != null && pos1.Count != 0 || pos2 != null && pos2.Count != 0)
            { // у нас может быть только одна позиция
                return;
            }

            if (_rsi1.Values == null && _rsi2.Values == null )
            {
                return;
            }

            if ( _rsi1.Values.Count < _rsi1.Lenght+3 || _rsi2.Values.Count < _rsi2.Lenght + 3)
            {
                return;
            }

            decimal lastRsi1 = _rsi1.Values[_rsi1.Values.Count - 1];
            decimal lastRsi2 = _rsi2.Values[_rsi2.Values.Count - 1];
           
            if (lastRsi1 > lastRsi2 + RsiSpread)
            {
                _tab1.SellAtMarket(Volume1);
                _tab2.BuyAtMarket(Volume2);
            } 

            if (lastRsi2 > lastRsi1 + RsiSpread)
            {
                _tab1.BuyAtMarket(Volume1);
                _tab2.SellAtMarket(Volume2);
            } 
        }

        private void CheckExit()
        {
            List<Position> positions1 = _tab1.PositionsOpenAll;
            List<Position> positions2 = _tab2.PositionsOpenAll;

            decimal lastRsi1 = _rsi1.Values[_rsi1.Values.Count - 1];
            decimal lastRsi2 = _rsi2.Values[_rsi2.Values.Count - 1];

            if (positions1 == null || positions1.Count == 0)
            {
                return;
            }

            if (lastRsi1 <= lastRsi2 && positions1[0].Direction == Side.Sell)
            {
                _tab1.CloseAtMarket(positions1[0], positions1[0].OpenVolume);
                _tab2.CloseAtMarket(positions2[0], positions1[0].OpenVolume);
            }

            if (lastRsi2 <= lastRsi1 && positions1[0].Direction == Side.Buy)
            {
                _tab1.CloseAtMarket(positions1[0], positions1[0].OpenVolume);
                _tab2.CloseAtMarket(positions2[0], positions1[0].OpenVolume);
            }
        }
    }

    /// <summary>
    /// торговля на основе индикатора Pivot Points
    /// </summary>
    public class PivotPointsRobot : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="name"></param>
        public PivotPointsRobot(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _pivot = new PivotPoints(name + "Prime", false);
            _pivot = (PivotPoints)_tab.CreateCandleIndicator(_pivot, "Prime");

            _pivot.Save();

            //_tab.CandleFinishedEvent += TradeLogic;
            _tab.CandleFinishedEvent += TradeLogic;
            Slipage = 0;
            VolumeFix = 1;
            Stop = 0.5m;
            Load();

            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "PivotPointsRobot";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            //MessageBox.Show("Пусто");
            PivotPointsRobotUi ui = new PivotPointsRobotUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// вкладка с первым инструметом
        /// </summary>
        private BotTabSimple _tab;

        /// <summary>
        /// индикатор PivotPoints
        /// </summary>
        private PivotPoints _pivot;

        //публичные настройки

        /// <summary>
        /// размер стопа в %
        /// </summary>
        public decimal Stop;

        /// <summary>
        ///  проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Slipage);
                    writer.WriteLine(VolumeFix);
                    writer.WriteLine(Regime);
                    writer.WriteLine(Stop);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Stop = Convert.ToDecimal(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }



        /// <summary>
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // переменные, нужные для торговли
        private decimal _lastPriceO;
        private decimal _lastPriceC;
        private decimal _pivotR1;
        private decimal _pivotR3;
        private decimal _pivotS1;
        private decimal _pivotS3;

        // логика
        
        /// <summary>
        /// Метод-обработчик события завершения свечи
        /// </summary>
        /// <param name="candles"></param>
        private void TradeLogic(List<Candle> candles)
        {
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            _lastPriceO = candles[candles.Count - 1].Open;
            _lastPriceC = candles[candles.Count - 1].Close;
            _pivotR1 = _pivot.ValuesR1[_pivot.ValuesR1.Count - 1];
            
            _pivotS1 = _pivot.ValuesS1[_pivot.ValuesS1.Count - 1];
            

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);
                }
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }

            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }

        }

        /// <summary>
        /// логика открытия позиции
        /// </summary>
        /// <param name="candles"></param>
        /// <param name="openPositions"></param>
        private void LogicOpenPosition(List<Candle> candles, List<Position> openPositions)
        {
            if (_lastPriceC > _pivotR1 && _lastPriceO < _pivotR1 && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastPriceC + Slipage);
                _pivotR3 = _pivot.ValuesR3[_pivot.ValuesR3.Count - 1];
            }

            if (_lastPriceC < _pivotS1 && _lastPriceO > _pivotS1 && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFix, _lastPriceC - Slipage);
                _pivotS3 = _pivot.ValuesS3[_pivot.ValuesS3.Count - 1];
            }
        }

        /// <summary>
        /// логика закрытия позиции
        /// </summary>
        /// <param name="candles"></param>
        /// <param name="openPosition"></param>
        private void LogicClosePosition(List<Candle> candles, Position openPosition)
        {
            if (openPosition.Direction == Side.Buy)
            {
                if (_lastPriceC > _pivotR3)
                {
                    _tab.CloseAtLimit(openPosition, _lastPriceC - Slipage, openPosition.OpenVolume);
                }
                if (_lastPriceC < openPosition.EntryPrice - openPosition.EntryPrice / 100m * Stop)
                {
                    _tab.CloseAtMarket(openPosition, openPosition.OpenVolume);
                }
            }

            if (openPosition.Direction == Side.Sell)
            {
                if (_lastPriceC < _pivotS3)
                {
                    _tab.CloseAtLimit(openPosition, _lastPriceC + Slipage, openPosition.OpenVolume);
                }
                if (_lastPriceC > openPosition.EntryPrice + openPosition.EntryPrice / 100m * Stop)
                {
                    _tab.CloseAtMarket(openPosition, openPosition.OpenVolume);
                }
            }
        }
    }
    #endregion

    /// <summary>
    /// режим работы робота
    /// </summary>
    public enum BotTradeRegime
    {
        /// <summary>
        /// включен
        /// </summary>
        On,

        /// <summary>
        /// включен только лонг
        /// </summary>
        OnlyLong,

        /// <summary>
        /// включен только шорт
        /// </summary>
        OnlyShort,

        /// <summary>
        /// только закрытие позиции
        /// </summary>
        OnlyClosePosition,

        /// <summary>
        /// выключен
        /// </summary>
        Off
    }
}
