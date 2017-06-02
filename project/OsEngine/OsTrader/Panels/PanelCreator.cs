﻿/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.OsTrader.Panels.PanelsGui;
using OsEngine.OsTrader.Panels.Tab;
using ru.micexrts.cgate;
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
            result.Add("Bollinger");
            result.Add("Williams Band");
            result.Add("FilippLevel");
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
            result.Add("PivotPointsRobot");
            result.Add("TwoLegArbitration");
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

        public static BotPanel GetStrategyForName(string nameClass, string name)
        {

            BotPanel bot = null;
            // примеры и бесплатные боты

            if (nameClass == "PivotPointsRobot")
            {
                bot = new PivotPointsRobot(name);
            }
            if (nameClass == "Engine")
            {
                bot = new StrategyEngineCandle(name);
            }
            if (nameClass == "Williams Band")
            {
                bot = new StrategyBillWilliams(name);
            }
            if (nameClass == "Levermor")
            {
                bot = new StrategyLevermor(name);
            }
            if (nameClass == "FilippLevel")
            {
                bot = new FilippLevel(name);
            }
            if (nameClass == "Bollinger")
            {
                bot = new StrategyBollinger(name);
            }
            if (nameClass == "PairTraderSimple")
            {
                bot = new PairTraderSimple(name);
            }

// под релиз
            if (nameClass == "RsiTrade")
            {
                bot = new RsiTrade(name);
            }
            if (nameClass == "StochasticTrade")
            {
                bot = new StochasticTrade(name);
            }
            if (nameClass == "BollingerTrade")
            {
                bot = new BollingerTrade(name);
            }
            if (nameClass == "TRIXTrade")
            {
                bot = new TrixTrade(name);
            }
            if (nameClass == "CCITrade")
            {
                bot = new CciTrade(name);
            }
            if (nameClass == "ParabolicSarTrade")
            {
                bot = new ParabolicSarTrade(name);
            }
            if (nameClass == "PriceChannelTrade")
            {
                bot = new PriceChannelTrade(name);
            }
            if (nameClass == "MACDTrade")
            {
                bot = new MacdTrade(name);
            }
            if (nameClass == "BBPowerTrade")
            {
                bot = new BbPowerTrade(name);
            }
            if (nameClass == "RviTrade")
            {
                bot = new RviTrade(name);
            }
            if (nameClass == "WilliamsRangeTrade")
            {
                bot = new WilliamsRangeTrade(name);
            }
            if (nameClass == "MacdTrail")
            {
                bot = new MacdTrail(name);
            }
            if (nameClass == "SmaStochastic")
            {
                bot = new SmaStochastic(name);
            }
            if (nameClass == "MomentumMACD")
            {
                bot = new MomentumMacd(name);
            }
            if (nameClass == "PinBarTrade")
            {
                bot = new PinBarTrade(name);
            }
            if (nameClass == "PairRsiTrade")
            {
                bot = new PairRsiTrade(name);
            }
            if (nameClass == "Robot")
            {
                bot = new Robot(name);
            }
            if (nameClass == "FirstBot")
            {
                bot = new FirstBot(name);
            }

            if (nameClass == "TwoLegArbitration")
            {
                bot = new TwoLegArbitration(name);
            }
            if (nameClass == "OneLegArbitration")
            {
                bot = new OneLegArbitration(name);
            }
            if (nameClass == "ThreeSoldier")
            {
                bot = new ThreeSoldier(name);
            }
            if (nameClass == "BollingerOutburst")
            {
                bot = new BollingerOutburst(name);
            }
            if (nameClass == "RsiContrtrend")
            {
                bot = new RsiContrtrend(name);
            }
            if (nameClass == "PriceChannelVolatility")
            {
                bot = new PriceChannelVolatility(name);
            }
            if (nameClass == "PriceChannelBreak")
            {
                bot = new PriceChannelBreak(name);
            }
            if (nameClass == "PairTraderSpreadSma")
            {
                bot = new PairTraderSpreadSma(name);
            }
            

            return bot;
        }
    }

    # region роботы из инструкций с пошаговым созданием

    public class Robot : BotPanel
    {
        private BotTabSimple _tab;
        private Bollinger _bol;
        public int Volume;

        public override string GetNameStrategyType()
        {
            return "Robot";
        }

        public override void ShowIndividualSettingsDialog()
        {
            RobotUi ui = new RobotUi(this);
            ui.ShowDialog();
        }

        public Robot(string name)
            : base(name)
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

        public FirstBot(string name)
            : base(name)
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
    
    public class TwoLegArbitration : BotPanel

    {
        /// <summary>
        /// конструктор
        /// </summary>
        public TwoLegArbitration(string name)
            : base(name)
        {
            TabCreate(BotTabType.Index);
            _tab1 = TabsIndex[0];

            TabCreate(BotTabType.Simple);
            _tab2 = TabsSimple[0];
            TabCreate(BotTabType.Simple);
            _tab3 = TabsSimple[1];

            _rsi = new Rsi(name + "RSI", false) { Lenght = 20, ColorBase = Color.Gold, };
            _rsi = (Rsi)_tab1.CreateCandleIndicator(_rsi, "RsiArea");

            _rsi.Save();

            _tab2.CandleFinishedEvent += Strateg_CandleFinishedEvent;
            _tab3.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Slipage = 0;
            VolumeFix = 1;
            Upline = 60;
            Downline = 30;

            Load();

            DeleteEvent += Strategy_DeleteEvent;

        }

        /// <summary>
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "TwoLegArbitration";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            TwoLegArbitrationUi ui = new TwoLegArbitrationUi(this);
            ui.ShowDialog();
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
        public decimal Slipage;

        /// <summary>
        /// фиксированный объем для входа
        /// </summary>
        public int VolumeFix;

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// ур. перекупленности по RSI
        /// </summary>
        public int Upline;

        /// <summary>
        /// ур. перепроданности по RSI
        /// </summary>
        public int Downline;

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
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
                    Upline = Convert.ToInt32(reader.ReadLine());
                    Downline = Convert.ToInt32(reader.ReadLine());
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

            if (_rsi.Values == null)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
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
                if (Regime == BotTradeRegime.OnlyClosePosition)
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
        private void LogicOpenPosition(BotTabSimple instrument)
        {
            if (_lastRsi > Upline && Regime != BotTradeRegime.OnlyLong)
            {
                instrument.SellAtLimit(VolumeFix, _lastPrice - Slipage);
            }
            if (_lastRsi < Downline && Regime != BotTradeRegime.OnlyShort)
            {
                instrument.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
            }
        }

        /// <summary>
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(Position position, BotTabSimple instrument)
        {
            if (position.Direction == Side.Buy)
            {
                if (_lastRsi > Upline)
                {
                    instrument.CloseAtLimit(position, _lastPrice - Slipage, position.OpenVolume);
                }
            }
            if (position.Direction == Side.Sell)
            {
                if (_lastRsi < Downline)
                {
                    instrument.CloseAtLimit(position, _lastPrice + Slipage, position.OpenVolume);
                }
            }
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
        public OneLegArbitration(string name)
            : base(name)
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
        public int VolumeFix;

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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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

            if (_ma.Values == null)
            {
                return;
            }
            _lastIndex = _tab1.Candles[_tab1.Candles.Count - 1].Close;
            _lastMa = _ma.Values[_ma.Values.Count - 1];
            _lastPrice = candles[candles.Count - 1].Close;

            if (_ma.Values == null || _ma.Values.Count < _ma.Lenght + 2)
            {
                return;
            }

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
        public ThreeSoldier(string name)
            : base(name)
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
        public int VolumeFix;

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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
        public BollingerOutburst(string name)
            : base(name)
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
        public int VolumeFix;

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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
        public PriceChannelBreak(string name)
            : base(name)
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
        public int VolumeFix;

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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
        public PriceChannelVolatility(string name)
            : base(name)
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
        public int VolumeFix1;

        /// <summary>
        /// фиксированный объем для входа во вторую позицию
        /// </summary>
        public int VolumeFix2;

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
                    VolumeFix1 = Convert.ToInt32(reader.ReadLine());
                    VolumeFix2 = Convert.ToInt32(reader.ReadLine());
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
        public RsiContrtrend(string name)
            : base(name)
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
        public int VolumeFix;

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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
        public StrategyEngineCandle(string name)
            : base(name)
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
    /// трендовая стратегия Билла Вильямса на Аллигаторе и фракталах
    /// </summary>
    public class StrategyBillWilliams : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public StrategyBillWilliams(string name)
            : base(name)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _alligator = new Alligator(name + "Alligator", false);
            _alligator = (Alligator)_tab.CreateCandleIndicator(_alligator, "Prime");
            _alligator.Save();

            _fractal = new Fractal(name + "Fractal", false);
            _fractal = (Fractal)_tab.CreateCandleIndicator(_fractal, "Prime");

            _aO = new AwesomeOscillator(name + "AO", false);
            _aO = (AwesomeOscillator)_tab.CreateCandleIndicator(_aO, "AoArea");

            _tab.CandleFinishedEvent += Bot_CandleFinishedEvent;

            Slipage = 0;
            VolumeFirst = 1;
            VolumeSecond = 1;
            Regime = BotTradeRegime.On;
            MaximumPositions = 3;

            Load();
            DeleteEvent += Strategy_DeleteEvent;
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
            WilliamsUi ui = new WilliamsUi(this);
            ui.ShowDialog();
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
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// объём для первого входа
        /// </summary>
        public int VolumeFirst;

        /// <summary>
        /// объём для последующих входов
        /// </summary>
        public int VolumeSecond;

        /// <summary>
        /// максимальная позиция
        /// </summary>
        public int MaximumPositions;

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
                    writer.WriteLine(VolumeFirst);
                    writer.WriteLine(VolumeSecond);
                    writer.WriteLine(MaximumPositions);
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
                    VolumeFirst = Convert.ToInt32(reader.ReadLine());
                    VolumeSecond = Convert.ToInt32(reader.ReadLine());
                    MaximumPositions = Convert.ToInt32(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

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
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_alligator.ValuesUp == null ||
                _alligator.Values == null ||
                _alligator.ValuesDown == null ||
                _fractal == null||
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

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }

            if (openPosition == null || openPosition.Count == 0
                && candles[candles.Count - 1].TimeStart.Hour >= 11
                && candles[candles.Count - 1].TimeStart.Hour <= 18)
            {
                LogicOpenPosition();
            }
            else if (openPosition.Count != 0 && openPosition.Count < MaximumPositions
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
                && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFirst, _lastPrice + Slipage);
            }
            if (_lastPrice < _lastUpAlligator && _lastPrice < _lastMiddleAlligator && _lastPrice < _lastDownAlligator
                && _lastPrice < _lastFractalDown
                && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(VolumeFirst, _lastPrice - Slipage);
            }
        }

        /// <summary>
        /// логика открытия позиции после первой 
        /// </summary>
        private void LogicOpenPositionSecondary(Side side)
        {
            if (side == Side.Buy && Regime != BotTradeRegime.OnlyShort)
            {
                if (_secondAo < _lastAo &&
                    _secondAo < _thirdAo)
                {
                    _tab.BuyAtLimit(VolumeSecond, _lastPrice + Slipage);
                }
            }

            if (side == Side.Sell && Regime != BotTradeRegime.OnlyLong)
            {
                if (_secondAo > _lastAo &&
                    _secondAo > _thirdAo)
                {
                    _tab.SellAtLimit(VolumeSecond, _lastPrice - Slipage);
                }
            }
        }

        /// <summary>
        /// логика закрытия позиции
        /// </summary>
        private void LogicClosePosition(Position position, List<Candle> candles)
        {
            if (position.Direction == Side.Buy)
            {
                if (_lastPrice < _lastMiddleAlligator)
                {
                    _tab.CloseAtLimit(position, _lastPrice - Slipage, position.OpenVolume);
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastPrice > _lastMiddleAlligator)
                {
                    _tab.CloseAtLimit(position, _lastPrice + Slipage, position.OpenVolume);
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
        public StrategyLevermor(string name)
            : base(name)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _smaTrenda = new MovingAverage(name + "MovingLong", false) { Lenght = 150, ColorBase = Color.DodgerBlue };
            _smaTrenda = (MovingAverage)_tab.CreateCandleIndicator(_smaTrenda, "Prime");
            _smaTrenda.Save();

            _channel = new PriceChannel(name + "Chanel", false) { LenghtUpLine = 12, LenghtDownLine = 12 };
            _channel = (PriceChannel)_tab.CreateCandleIndicator(_channel, "Prime");
            _channel.Save();

            Slipage = 0;
            Regime = BotTradeRegime.On;

            LongStop = 0.8m;
            ShortStop = 0.8m;
            Volume = 1;
            MaximumPosition = 5;
            PersentDopBuy = 0.5m;
            PersentDopSell = 0.5m;

            Load();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += StrategyRutabaga_PositionOpeningSuccesEvent;
            DeleteEvent += Strategy_DeleteEvent;
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
            LevermorUi ui = new LevermorUi(this);
            ui.ShowDialog();
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
        public decimal Slipage;

        /// <summary>
        /// режим работы робота
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// объём исполняемый в одной сделке
        /// </summary>
        public int Volume;


        // настройки стратегии

        public decimal LongStop;
        public decimal ShortStop;
        public int MaximumPosition;
        public decimal PersentDopBuy;
        public decimal PersentDopSell;

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
                    writer.WriteLine(Slipage);
                    writer.WriteLine(Regime);
                    writer.WriteLine(LongStop);
                    writer.WriteLine(ShortStop);
                    writer.WriteLine(Volume);

                    writer.WriteLine(PersentDopBuy);
                    writer.WriteLine(PersentDopSell);
                    writer.WriteLine(MaximumPosition);

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
                    Slipage = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    LongStop = Convert.ToDecimal(reader.ReadLine());
                    ShortStop = Convert.ToDecimal(reader.ReadLine());
                    Volume = Convert.ToInt32(reader.ReadLine());
                    PersentDopBuy = Convert.ToDecimal(reader.ReadLine());
                    PersentDopSell = Convert.ToDecimal(reader.ReadLine());
                    MaximumPosition = Convert.ToInt32(reader.ReadLine());
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

        // логика

        /// <summary>
        /// событие, происходит когда позиция успешно открыта
        /// </summary>
        /// <param name="position">открытая позиция</param>
        private void StrategyRutabaga_PositionOpeningSuccesEvent(Position position)
        {
            try
            {

                if (Regime == BotTradeRegime.Off)
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
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_smaTrenda.Lenght > candles.Count||
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

            if (Regime == BotTradeRegime.OnlyClosePosition)
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

            List<Position> positions = _tab.PositionOpenLong;

            if (lastPrice >= lastMa && Regime != BotTradeRegime.OnlyShort)
            {
                if (positions != null && positions.Count != 0 &&
                    positions[0].Direction == Side.Sell)
                {
                    _tab.CloseAllAtMarket();
                }
                if (positions != null && positions.Count != 0 &&
                    positions[0].Direction == Side.Buy)
                { // если открыты лонги - добавляемся
                    if (positions.Count >= MaximumPosition)
                    {
                        return;
                    }
                    decimal lastIntro = positions[positions.Count - 1].EntryPrice;
                    if (lastIntro + lastIntro * (PersentDopSell / 100) < lastPrice)
                    {
                        //BuyAtLimit(0, Volume, lastPrice + Slipage);
                        _tab.BuyAtLimit(GetVolume(lastPrice), lastPrice + Slipage);
                    }
                }
                else
                { // если ничего не открыто - ставим линии на пробой
                    //BuyAtStop(0, Volume, maxToCandleSeries + Slipage, maxToCandleSeries, candles[candles.Count - 1].Close);
                    _tab.BuyAtStop(GetVolume(lastPrice), maxToCandleSeries + Slipage, maxToCandleSeries, StopActivateType.HigherOrEqual);
                }
            }

            if (lastPrice <= lastMa && Regime != BotTradeRegime.OnlyLong)
            {
                if (positions != null && positions.Count != 0 &&
                    positions[0].Direction == Side.Buy)
                { // если открыты лонги - кроем 
                    _tab.CloseAllAtMarket();
                }
                if (positions != null && positions.Count != 0 &&
                         positions[0].Direction == Side.Sell)
                { // если открыты шорты - добавляемся
                    if (positions.Count >= MaximumPosition)
                    {
                        return;
                    }
                    decimal lastIntro = positions[positions.Count - 1].EntryPrice;

                    if (lastIntro - lastIntro * (PersentDopSell / 100) > lastPrice)
                    {
                        //SellAtLimit(0, Volume, lastPrice - Slipage);
                        _tab.SellAtLimit(GetVolume(lastPrice), lastPrice - Slipage);
                    }
                }
                else
                { // если ничего не открыто - ставим линии на пробой
                    //SellAtStop(0, Volume, minToCandleSeries - Slipage, minToCandleSeries,candles[candles.Count - 1].Close);
                    _tab.SellAtStop(GetVolume(lastPrice), minToCandleSeries - Slipage, minToCandleSeries, StopActivateType.LowerOrEqyal);
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
                decimal lastIntro = positions[positions.Count - 1].EntryPrice;
                if (positions[i].Direction == Side.Buy)
                {
                    _tab.CloseAtStop(positions[i], lastIntro - lastIntro * (LongStop / 100), lastIntro - lastIntro * (LongStop / 100) - Slipage);
                }
                else
                {
                    _tab.CloseAtStop(positions[i], lastIntro + lastIntro * (LongStop / 100), lastIntro + lastIntro * (LongStop / 100) + Slipage);
                }
            }



        }

        /// <summary>
        /// Загружает уровни для пробития. Использовать утром, после загрузки свечек
        /// </summary>
        public void Preload()
        {
            List<Position> openPosition = _tab.PositionsOpenAll;

            if (openPosition == null || openPosition.Count == 0)
            {
                // позиций нет, вызываем логику открытия позиции
                return;
            }

            /*if (LastOpenierPriceStart == 0 ||
                LastOpenierPriceRedLine == 0 ||
                LastOpenierSmaPrice == 0)
            {
                return;
            }

            if (LastOpenierPriceRedLine >= LastOpenierSmaPrice && Regime != BotTradeRegime.OnlyShort)
            {
                // 1 вариант. Алгоритм расчитывает объём. 
                //int contractValue = (int) (StopRisk/(_lastAtr*InitialStop*Securiti.PriceStepCost));
                //BuyAtStop(contractValue, maxToCandleSeries + Slipage, maxToCandleSeries, _lastPrice);

                // 2 вариант. Используется постоянный объём из настроек
                List<Candle> candles = CandlesAll(0);

                BuyAtStop(0, Volume, LastOpenierPriceRedLine + Slipage, LastOpenierPriceRedLine,
                    candles[candles.Count - 1].Close);

            }

            if (LastOpenierPriceRedLine <= LastOpenierSmaPrice && Regime != BotTradeRegime.OnlyLong)
            {
                // 1 вариант. Алгоритм расчитывает объём. 
                //int contractValue = (int) (StopRisk/(_lastAtr*InitialStop*Securiti.PriceStepCost));
                //SellAtStop(contractValue, minToCandleSeries - Slipage, minToCandleSeries, _lastPrice);
                List<Candle> candles = CandlesAll(0);
                // 2 вариант. Используется постоянный объём из настроек
                SellAtStop(0, Volume, LastOpenierPriceRedLine - Slipage, LastOpenierPriceRedLine,
                    candles[candles.Count - 1].Close);
            }*/



        }

        /// <summary>
        /// взять объём для входа в позицию
        /// </summary>
        private int GetVolume(decimal lastPrice)
        {
            return Convert.ToInt32(_tab.Portfolio.ValueCurrent / (lastPrice * _tab.Securiti.Lot) / 3);
        }
    }

    /// <summary>
    /// стратегия основанная на линиях боллинджера
    /// </summary>
    public class StrategyBollinger : BotPanel
    {

        /// <summary>
        /// конструктор
        /// </summary>
        public StrategyBollinger(string name)
            : base(name)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _bollinger = new Bollinger(name + "Bollinger", false);
            _bollinger = (Bollinger)_tab.CreateCandleIndicator(_bollinger, "Prime");
            _bollinger.Save();

            _moving = new MovingAverage(name + "Moving", false) { Lenght = 15 };
            _moving = (MovingAverage)_tab.CreateCandleIndicator(_moving, "Prime");
            _moving.Save();

            _tab.CandleFinishedEvent += Bot_CandleFinishedEvent;

            Slipage = 0;
            Volume = 1;
            Regime = BotTradeRegime.On;

            DeleteEvent += Strategy_DeleteEvent;

            Load();
        }

        /// <summary>
        /// взять уникальное имя стратегии
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "Bollinger";
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            BollingerStrategyUi ui = new BollingerStrategyUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// вкладка через которую ведётся торговля
        /// </summary>
        private BotTabSimple _tab;

        // индикаторы

        /// <summary>
        /// боллиндер
        /// </summary>
        private Bollinger _bollinger;

        /// <summary>
        /// мувинг
        /// </summary>
        private MovingAverage _moving;

        // настройки публичные

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// объём входа
        /// </summary>
        public int Volume;

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
                    writer.WriteLine(Volume);
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
                    Volume = Convert.ToInt32(reader.ReadLine());
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

        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Bot_CandleFinishedEvent(List<Candle> candles)
        {
            // берём значения из инидикаторов.
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_bollinger.ValuesUp == null ||
                _bollinger.ValuesUp.Count == 0 ||
                _bollinger.ValuesUp.Count < candles.Count ||
                _moving.Values.Count < candles.Count)
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

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }

            if (openPosition == null || openPosition.Count == 0
                && candles[candles.Count - 1].TimeStart.Hour >= 11
                && candles[candles.Count - 1].TimeStart.Hour <= 18)
            {
                LogicOpenPosition(candles);
            }
        }

        /// <summary>
        /// логика открытия позиции
        /// </summary>
        /// <param name="candles"></param>
        private void LogicOpenPosition(List<Candle> candles)
        {
            decimal bollingerUpLast = _bollinger.ValuesUp[_bollinger.ValuesUp.Count - 1];

            decimal bollingerDownLast = _bollinger.ValuesDown[_bollinger.ValuesDown.Count - 1];

            if (bollingerUpLast == 0 ||
                bollingerDownLast == 0)
            {
                return;
            }


            decimal close = candles[candles.Count - 1].Close;

            if (close > bollingerUpLast
                && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(Volume, close - Slipage);
            }

            if (close < bollingerDownLast
                && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(Volume, close + Slipage);
            }
        }

        /// <summary>
        /// логика закрытия позиции
        /// </summary>
        private void LogicClosePosition(Position position, List<Candle> candles)
        {
            decimal moving = _moving.Values[_moving.Values.Count - 1];

            decimal lastClose = candles[candles.Count - 1].Close;

            if (position.Direction == Side.Buy)
            {
                if (lastClose > moving)
                {
                    _tab.CloseAtLimit(position, lastClose - Slipage, position.OpenVolume);
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (lastClose < moving)
                {
                    _tab.CloseAtLimit(position, lastClose + Slipage, position.OpenVolume);
                }
            }
        }
    }

    /// <summary>
    /// стратегия реализующая набор котртрендовой позиции по линиям
    /// </summary>
    public class FilippLevel : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public FilippLevel(string name)
            : base(name)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = BotTradeRegime.On;
            PersentToSpreadLines = 0.5m;
            Volume = 1;

            Load();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            DeleteEvent += Strategy_DeleteEvent;
        }

        /// <summary>
        /// переопределённый метод, позволяющий менеджеру ботов определять что за робот перед ним
        /// </summary>
        /// <returns>название стратегии</returns>
        public override string GetNameStrategyType()
        {
            return "FilippLevel";
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            FilipLevelUi ui = new FilipLevelUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// вкладка через которую ведётся торговля
        /// </summary>
        private BotTabSimple _tab;

        // настройки стандартные

        /// <summary>
        /// режим работы робота
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// объём исполняемый в одной сделке
        /// </summary>
        public int Volume;

        /// <summary>
        /// расстояние между линиями в %
        /// </summary>
        public decimal PersentToSpreadLines;

        /// <summary>
        /// нужно ли прорисовывать линии
        /// </summary>
        public bool PaintOn;

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
                    writer.WriteLine(Volume);
                    writer.WriteLine(PersentToSpreadLines);
                    writer.WriteLine(PaintOn);
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
                    Volume = Convert.ToInt32(reader.ReadLine());
                    PersentToSpreadLines = Convert.ToDecimal(reader.ReadLine());
                    PaintOn = Convert.ToBoolean(reader.ReadLine());
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

        private DateTime _lastReloadLineTime = DateTime.MinValue;

        private List<decimal> _lines;

        private List<LineHorisontal> _lineElements;

        // логика

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime == BotTradeRegime.Off)
            {
                ClearLines();
                return;
            }

            if (candles.Count < 2)
            {
                return;
            }

            // распределяем логику в зависимости от текущей позиции

            List<Position> openPosition = _tab.PositionsOpenAll;

            if (candles[candles.Count - 1].TimeStart.DayOfWeek == DayOfWeek.Friday &&
             candles[candles.Count - 1].TimeStart.Hour >= 18)
            {// если у нас пятница вечер
                if (openPosition != null && openPosition.Count != 0)
                {
                    _tab.CloseAllAtMarket();
                }
                return;
            }

            if (_lastReloadLineTime == DateTime.MinValue ||
                candles[candles.Count - 1].TimeStart.DayOfWeek == DayOfWeek.Monday &&
                candles[candles.Count - 1].TimeStart.Hour < 11 &&
                _lastReloadLineTime.Day != candles[candles.Count - 1].TimeStart.Day)
            {// если у нас понедельник утро
                _lastReloadLineTime = candles[candles.Count - 1].TimeStart;
                ReloadLines(candles);
            }

            if (PaintOn)
            {
                RepaintLines();
            }
            else
            {
                ClearLines();
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                // если у бота включен режим "только закрытие"
                return;
            }

            LogicOpenPosition(candles);

        }

        /// <summary>
        /// перезагрузить линии
        /// </summary>
        private void ReloadLines(List<Candle> candles)
        {
            _lines = new List<decimal>();

            // клоз это линия номер ноль и по 30 штук вверх и вниз

            _lines.Add(candles[candles.Count - 1].Close);

            decimal concateValue = candles[candles.Count - 1].Close / 100 * PersentToSpreadLines;

            // считаем 30 вниз

            for (int i = 1; i < 21; i++)
            {
                _lines.Add(candles[candles.Count - 1].Close - concateValue * i);
            }

            // считаем 30 вверх

            for (int i = 1; i < 21; i++)
            {
                _lines.Insert(0, candles[candles.Count - 1].Close + concateValue * i);
            }
        }

        /// <summary>
        /// перерисовать линии
        /// </summary>
        private void RepaintLines()
        {
            if (_lineElements == null ||
                _lines.Count != _lineElements.Count)
            { // нужно полностью перерисовать
                _lineElements = new List<LineHorisontal>();

                for (int i = 0; i < _lines.Count; i++)
                {
                    _lineElements.Add(new LineHorisontal(NameStrategyUniq + "Line" + i, "Prime", false) { Value = _lines[i] });
                    _tab.SetChartElement(_lineElements[i]);
                }
            }
            else
            { // надо проверить уровни линиий, и несовпадающие перерисовать
                for (int i = 0; i < _lineElements.Count; i++)
                {
                    if (_lineElements[i].Value != _lines[i])
                    {
                        _lineElements[i].Value = _lines[i];
                    }
                    _lineElements[i].Refresh();
                }
            }
        }

        /// <summary>
        /// очистить линии с графика
        /// </summary>
        private void ClearLines()
        {
            if (_lineElements == null ||
                _lineElements.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _lineElements.Count; i++)
            {
                _lineElements[i].Delete();
            }
        }

        /// <summary>
        /// логика торговли
        /// </summary>
        /// <param name="candles"></param>
        private void LogicOpenPosition(List<Candle> candles)
        {
            if (_lines == null ||
                _lines.Count == 0)
            {
                return;
            }
            // 1 выясняем каким объёмом и в какую сторону нам надо заходить
            int totalDeal = 0;

            decimal lastPrice = candles[candles.Count - 2].Close;
            decimal nowPrice = candles[candles.Count - 1].Close;

            for (int i = 0; i < _lines.Count; i++)
            {
                if (lastPrice < _lines[i] &&
                    nowPrice > _lines[i])
                { // пробой снизу вверх
                    totalDeal--;
                }

                if (lastPrice > _lines[i] &&
                    nowPrice < _lines[i])
                { // пробой сверху вниз
                    totalDeal++;
                }
            }

            if (totalDeal == 0)
            {
                return;
            }

            // 2 заходим в нужную сторону

            if (totalDeal > 0)
            { // нужно лонговать
                List<Position> positionsShort = _tab.PositionOpenShort;

                if (positionsShort != null && positionsShort.Count != 0)
                {
                    if (positionsShort[0].OpenVolume <= totalDeal)
                    {
                        _tab.CloseAtMarket(positionsShort[0], positionsShort[0].OpenVolume);
                        totalDeal -= positionsShort[0].OpenVolume;
                    }
                    else
                    {
                        _tab.CloseAtMarket(positionsShort[0], totalDeal);
                        totalDeal = 0;
                    }
                }

                if (totalDeal > 0 && totalDeal != 0)
                {
                    List<Position> positionsLong = _tab.PositionOpenLong;

                    if (positionsLong != null && positionsLong.Count != 0)
                    {
                        _tab.BuyAtMarketToPosition(positionsLong[0], totalDeal);
                    }
                    else
                    {
                        _tab.BuyAtMarket(totalDeal);
                    }
                }
            }

            if (totalDeal < 0)
            {
                // нужно шортить
                totalDeal = Math.Abs(totalDeal);

                List<Position> positionsLong = _tab.PositionOpenLong;

                if (positionsLong != null && positionsLong.Count != 0)
                {
                    if (positionsLong[0].OpenVolume <= totalDeal)
                    {
                        _tab.CloseAtMarket(positionsLong[0], positionsLong[0].OpenVolume);
                        totalDeal -= positionsLong[0].OpenVolume;
                    }
                    else
                    {
                        _tab.CloseAtMarket(positionsLong[0], totalDeal);
                        totalDeal = 0;
                    }
                }

                if (totalDeal > 0)
                {
                    List<Position> positionsShort = _tab.PositionOpenShort;

                    if (positionsShort != null && positionsShort.Count != 0)
                    {
                        _tab.SellAtMarketToPosition(positionsShort[0], totalDeal);
                    }
                    else
                    {
                        _tab.SellAtMarket(totalDeal);
                    }
                }
            }
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
        public PairTraderSimple(string name)
            : base(name)
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
                    Volume1 = Convert.ToInt32(reader.ReadLine());
                    Volume2 = Convert.ToInt32(reader.ReadLine());

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

        public int Volume1;

        public int Volume2;

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
        public PairTraderSpreadSma(string name)
            : base(name)
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
                    Volume1 = Convert.ToInt32(reader.ReadLine());
                    Volume2 = Convert.ToInt32(reader.ReadLine());

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
        public int Volume1;

        /// <summary>
        /// объём второго инструмента
        /// </summary>
        public int Volume2;

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
                    _tab1.CloseAtLimit(pos1, _tab1.PriceBestAsk - Slipage1, pos1.OpenVolume);
                }

                if (positions2 != null && positions2.Count != 0)
                {
                    Position pos2 = positions2[0];
                    _tab2.CloseAtLimit(pos2, _tab2.PriceBestBid + Slipage1, pos2.OpenVolume);
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
                    _tab1.CloseAtLimit(pos1, _tab1.PriceBestBid + Slipage1, pos1.OpenVolume);
                }

                if (positions2 != null && positions2.Count != 0)
                {
                    Position pos2 = positions2[0];
                    _tab2.CloseAtLimit(pos2, _tab2.PriceBestAsk - Slipage1, pos2.OpenVolume);
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
        public RsiTrade(string name)
            : base(name)
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
        public int VolumeFix;

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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
        public StochasticTrade(string name)
            : base(name)
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
        public int VolumeFix;

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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
        public BollingerTrade(string name)
            : base(name)
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
        public int VolumeFix;

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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
        public TrixTrade(string name)
            : base(name)
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
        public int VolumeFix;

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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
        public CciTrade(string name)
            : base(name)
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
        public int VolumeFix;

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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
        public ParabolicSarTrade(string name)
            : base(name)
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
        public int VolumeFix;

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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
        public PriceChannelTrade(string name)
            : base(name)
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
        public int VolumeFix;

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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
        public BbPowerTrade(string name)
            : base(name)
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
        public int VolumeFix;

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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
        public MacdTrade(string name)
            : base(name)
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
        public int VolumeFix;

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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
    /// Трендовая стратегия на пересечение индикатора RVI
    /// </summary>
    public class RviTrade : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public RviTrade(string name)
            : base(name)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _rvi = new Rvi(name + "RviArea", false);
            _rvi = (Rvi)_tab.CreateCandleIndicator(_rvi, "MacdArea");


            _rvi.Save();

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
            return "RviTrade";
        }
        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            RviTradeUi ui = new RviTradeUi(this);
            ui.ShowDialog();
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
        public decimal Slipage;

        /// <summary>
        /// фиксированный объем для входа
        /// </summary>
        public int VolumeFix;

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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
        private decimal _lastRviUp;
        private decimal _lastRviDown;
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
            if (_lastRviDown < 0 && _lastRviUp > _lastRviDown && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
            }

            if (_lastRviDown > 0 && _lastRviUp < _lastRviDown && Regime != BotTradeRegime.OnlyLong)
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
                if (_lastRviDown > 0 && _lastRviUp < _lastRviDown)
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
                if (_lastRviDown < 0 && _lastRviUp > _lastRviDown)
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
        public WilliamsRangeTrade(string name)
            : base(name)
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
        public int VolumeFix;

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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
        public MacdTrail(string name)
            : base(name)
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
        public int VolumeFix;


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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
        public SmaStochastic(string name)
            : base(name)
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
        public int VolumeFix;


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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
        public MomentumMacd(string name)
            : base(name)
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
        public int VolumeFix;


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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
        public PinBarTrade(string name)
            : base(name)
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
        public int VolumeFix;


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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
        public PairRsiTrade(string name)
            : base(name)
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
                    Volume1 = Convert.ToInt32(reader.ReadLine());
                    Volume2 = Convert.ToInt32(reader.ReadLine());
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
        public int Volume1;

        /// <summary>
        /// объём второго инструмента
        /// </summary>
        public int Volume2;

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
        public PivotPointsRobot(string name) : base(name)
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
        public int VolumeFix;

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
                    VolumeFix = Convert.ToInt32(reader.ReadLine());
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
