using System;
using System.Collections.Generic;
using System.IO;
using OsEngine.Entity;
using OsEngine.OsMiner;
using OsEngine.OsMiner.Patterns;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.OsTrader.Panels.SingleRobots
{
    /// <summary>
    /// робот для торговли паттернами
    /// </summary>
    public class PatternTrader : BotPanel
    {

// сервис
        public PatternTrader(string name) : base(name)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
            _minerMaster = new OsMinerMaster();
            _minerMaster.LogMessageEvent += _minerMaster_LogMessageEvent;

            DeleteEvent += Strategy_DeleteEvent;
            Regime= BotTradeRegime.Off;

            WeigthToInter = 1;
            WeigthToExit = 1;
            StopOrderIsOn = false;
            StopOrderValue = 20;
            StopOrderSleepage = 0;
            ProfitOrderIsOn = false;
            ProfitOrderValue = 20;
            ProfitOrderSleepage = 0;
            ExitFromSomeCandlesIsOn = false;
            ExitFromSomeCandlesValue = 10;
            ExitFromSomeCandlesSleepage = 0;
            TrailingStopIsOn = false;
            TreilingStopValue = 20;
            TreilingStopSleepage =0;
            MaxPosition = 3;
            OpenVolume = 1;

            Load();

            if (NameGroupPatternsToTrade != null)
            {
                GetPatterns();
            }
        }

        /// <summary>
        /// входящее сообщение из хранилища паттернов
        /// </summary>
        void _minerMaster_LogMessageEvent(string message, Logging.LogMessageType messageType)
        {
            _tab.SetNewLogMessage(message, messageType);
        }

        /// <summary>
        /// хранилище паттернов
        /// </summary>
        private OsMinerMaster _minerMaster;

        /// <summary>
        /// взять название робота
        /// </summary>
        /// <returns></returns>
        public override string GetNameStrategyType()
        {
            return "PatternTrader";
        }

        /// <summary>
        /// открыть окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            PatternTraderUi ui = new PatternTraderUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// обновить паттерны
        /// </summary>
        public void GetPatterns()
        {
            PatternsToOpen = _minerMaster.GetPatternsToInter(NameGroupPatternsToTrade);
            PatternsToClose = _minerMaster.GetPatternsToExit(NameGroupPatternsToTrade);
        }

        /// <summary>
        /// взять список групп паттернов из хранилища
        /// </summary>
        /// <param name="nameSet"></param>
        /// <returns></returns>
        public List<string> GetListPatternsNames(string nameSet)
        {
            return _minerMaster.GetListPatternsNames(nameSet);
        }

        /// <summary>
        /// взять список названий сетов
        /// </summary>
        /// <returns></returns>
        public List<string> GetListSetsName()
        {
            List<string> names = new List<string>();

            for (int i = 0; i < _minerMaster.Sets.Count; i++)
            {
                names.Add(_minerMaster.Sets[i].Name);
            }
            return names;
        }

// настройки

        /// <summary>
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// сторона входа для паттерна
        /// </summary>
        public Side SideInter;

        /// <summary>
        /// общий вес одиночных паттернов для входа
        /// </summary>
        public decimal WeigthToInter;

        /// <summary>
        /// общий вес одиночных паттернов для выхода из позиции
        /// </summary>
        public decimal WeigthToExit;

        /// <summary>
        /// включен ли стоп ордер для выхода
        /// </summary>
        public bool StopOrderIsOn;

        /// <summary>
        /// величина стопОрдера
        /// </summary>
        public decimal StopOrderValue;

        /// <summary>
        /// проскальзывание для стопОрдера
        /// </summary>
        public int StopOrderSleepage;

        /// <summary>
        /// включен ли профит ордер для выхода
        /// </summary>
        public bool ProfitOrderIsOn;

        /// <summary>
        /// величина профит ордера
        /// </summary>
        public decimal ProfitOrderValue;

        /// <summary>
        /// просальзывание для профитОрдера
        /// </summary>
        public int ProfitOrderSleepage;

        /// <summary>
        /// включен ли выход через N свечек
        /// </summary>
        public bool ExitFromSomeCandlesIsOn;

        /// <summary>
        /// количество свечек после которого сработает выход через N свечек
        /// </summary>
        public int ExitFromSomeCandlesValue;

        /// <summary>
        /// проскальзывание для выхода через N свечек
        /// </summary>
        public int ExitFromSomeCandlesSleepage;

        /// <summary>
        /// включен ли трейлинг стоп для выхода
        /// </summary>
        public bool TrailingStopIsOn;

        /// <summary>
        /// величина трейлинг стопа
        /// </summary>
        public decimal TreilingStopValue;

        /// <summary>
        /// проскальзывание для трейлинг стопа
        /// </summary>
        public int TreilingStopSleepage;

        /// <summary>
        /// название группы  паттернов которые торгует данный робот
        /// </summary>
        public string NameGroupPatternsToTrade;

        /// <summary>
        /// имя сета 
        /// </summary>
        public string NameSetToTrade;

        /// <summary>
        /// проскальзывание для входа по паттернам
        /// </summary>
        public int InterToPatternSleepage;

        /// <summary>
        /// проскальзывание для выхода по паттернам
        /// </summary>
        public int ExitToPatternsSleepage;

        /// <summary>
        /// Максимум позиций
        /// </summary>
        public int MaxPosition;

        /// <summary>
        /// объём для открытия
        /// </summary>
        public decimal OpenVolume;


// работа с файловой системой

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
                    writer.WriteLine(SideInter);
                    writer.WriteLine(WeigthToInter);
                    writer.WriteLine(WeigthToExit);
                    writer.WriteLine(StopOrderIsOn);
                    writer.WriteLine(StopOrderValue);
                    writer.WriteLine(StopOrderSleepage);
                    writer.WriteLine(ProfitOrderIsOn);
                    writer.WriteLine(ProfitOrderValue);
                    writer.WriteLine(ProfitOrderSleepage);
                    writer.WriteLine(ExitFromSomeCandlesIsOn);
                    writer.WriteLine(ExitFromSomeCandlesValue);
                    writer.WriteLine(ExitFromSomeCandlesSleepage);
                    writer.WriteLine(TrailingStopIsOn);
                    writer.WriteLine(TreilingStopValue);
                    writer.WriteLine(TreilingStopSleepage);
                    writer.WriteLine(NameGroupPatternsToTrade);
                    writer.WriteLine(InterToPatternSleepage);
                    writer.WriteLine(ExitToPatternsSleepage);
                    writer.WriteLine(MaxPosition );
                    writer.WriteLine(OpenVolume);
                    writer.WriteLine(NameSetToTrade);

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
                    Enum.TryParse(reader.ReadLine(), true, out SideInter);

                    WeigthToInter = Convert.ToDecimal(reader.ReadLine());
                    WeigthToExit = Convert.ToDecimal(reader.ReadLine());
                    StopOrderIsOn = Convert.ToBoolean(reader.ReadLine());
                    StopOrderValue = Convert.ToDecimal(reader.ReadLine());
                    StopOrderSleepage = Convert.ToInt32(reader.ReadLine());
                    ProfitOrderIsOn = Convert.ToBoolean(reader.ReadLine());
                    ProfitOrderValue = Convert.ToDecimal(reader.ReadLine());
                    ProfitOrderSleepage = Convert.ToInt32(reader.ReadLine());
                    ExitFromSomeCandlesIsOn = Convert.ToBoolean(reader.ReadLine());
                    ExitFromSomeCandlesValue = Convert.ToInt32(reader.ReadLine());
                    ExitFromSomeCandlesSleepage = Convert.ToInt32(reader.ReadLine());
                    TrailingStopIsOn = Convert.ToBoolean(reader.ReadLine());
                    TreilingStopValue = Convert.ToDecimal(reader.ReadLine());
                    TreilingStopSleepage = Convert.ToInt32(reader.ReadLine());
                    NameGroupPatternsToTrade = reader.ReadLine();
                    InterToPatternSleepage = Convert.ToInt32(reader.ReadLine());
                    ExitToPatternsSleepage = Convert.ToInt32(reader.ReadLine());
                    MaxPosition = Convert.ToInt32(reader.ReadLine());
                    OpenVolume = Convert.ToDecimal(reader.ReadLine());
                    NameSetToTrade = reader.ReadLine();

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

// торговая логика

        /// <summary>
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        /// <summary>
        /// одиночные паттерны для входа в позицию
        /// </summary>
        public List<IPattern> PatternsToOpen = new List<IPattern>();

        /// <summary>
        /// одиночные паттерны для входа в позицию
        /// </summary>
        public List<IPattern> PatternsToClose = new List<IPattern>();

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            List<Position> positions = _tab.PositionsOpenAll;


            if (positions.Count < MaxPosition)
            {
                if (CheckInter(PatternsToOpen, candles, candles.Count - 1, WeigthToInter))
                {
                    InterInNewPosition(candles[candles.Count-1].Close);
                }
            }

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].State != PositionStateType.Open)
                {
                    continue;
                }
                decimal priceExit;

                priceExit = CheckExit(positions[i], PatternsToClose, candles, candles.Count - 1, candles[candles.Count-1].Close);

                if (priceExit == 0)
                {
                    continue;
                }

                _tab.CloseAtLimit(positions[i],priceExit,positions[i].OpenVolume);
            }
        }

        /// <summary>
        /// войти в позицию
        /// </summary>
        /// <param name="price"></param>
        private void InterInNewPosition(decimal price)
        {
            if (SideInter == Side.Buy)
            {
                _tab.BuyAtLimit(OpenVolume, price + _tab.Securiti.PriceStep * InterToPatternSleepage);
            }
            else
            {
                _tab.SellAtLimit(OpenVolume, price - _tab.Securiti.PriceStep * InterToPatternSleepage);
            }
        }

        /// <summary>
        /// событие открытия новой позиции
        /// </summary>
        void _tab_PositionOpeningSuccesEvent(Position position)
        {
            if (ProfitOrderIsOn)
            {
                if (position.Direction == Side.Buy)
                {
                    decimal stopPrice = position.EntryPrice + position.EntryPrice * (ProfitOrderValue / 100);
                    decimal stopOrderPrice = stopPrice - _tab.Securiti.PriceStep * ProfitOrderSleepage;

                    _tab.CloseAtProfit(position, stopPrice, stopOrderPrice);
                }
                else if (position.Direction == Side.Sell)
                {
                    decimal stopPrice = position.EntryPrice - position.EntryPrice * (ProfitOrderValue / 100);
                    decimal stopOrderPrice = stopPrice + _tab.Securiti.PriceStep * ProfitOrderSleepage;
                    _tab.CloseAtProfit(position, stopPrice, stopOrderPrice);
                }
            }

            if (StopOrderIsOn)
            {
                if (position.Direction == Side.Buy)
                {
                    decimal stopPrice = position.EntryPrice - position.EntryPrice * (StopOrderValue/100);
                    decimal stopOrderPrice = stopPrice - _tab.Securiti.PriceStep * StopOrderSleepage;
                    _tab.CloseAtStop(position, stopPrice, stopOrderPrice);
                }
                else if (position.Direction == Side.Sell)
                {
                    decimal stopPrice = position.EntryPrice + position.EntryPrice * (StopOrderValue / 100);
                    decimal stopOrderPrice = stopPrice + _tab.Securiti.PriceStep * StopOrderSleepage;
                    _tab.CloseAtStop(position, stopPrice, stopOrderPrice);
                }
            }
        }

        /// <summary>
        /// проверить паттерны на вход в позицию
        /// </summary>
        private bool CheckInter(List<IPattern> patterns, List<Candle> series, int index, decimal weigthToInterOrExit)
        {
            if (patterns == null ||
                patterns.Count == 0)
            {
                return false;
            }
            decimal weigth = 0;

            for (int i = 0; i < patterns.Count; i++)
            {
                if (patterns[i].ThisIsIt(series, _tab.Indicators, index))
                {
                    weigth += patterns[i].Weigth;
                }
            }

            if (weigth >= weigthToInterOrExit)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// проверить выход из позиции
        /// </summary>
        private decimal CheckExit(Position position, List<IPattern> patterns, List<Candle> candles, int index, decimal price)
        {
            if (CheckInter(patterns, candles, index, WeigthToExit))
            { // если выходим по паттернам
                return GetPriceExit(position,price,ExitToPatternsSleepage);
            }

            if (TrailingStopIsOn)
            {
                if (position.Direction == Side.Buy)
                {
                    decimal newTrail = candles[candles.Count - 1].Close - candles[candles.Count - 1].Close * (TreilingStopValue / 100);
                    _tab.CloseAtTrailingStop(position,newTrail,newTrail - _tab.Securiti.PriceStep * StopOrderSleepage);
                }
                else
                {
                    decimal newTrail = candles[candles.Count - 1].Close + candles[candles.Count - 1].Close * (TreilingStopValue / 100);
                    _tab.CloseAtTrailingStop(position, newTrail, newTrail + _tab.Securiti.PriceStep * StopOrderSleepage);
                }
            }

            // проверить выход по времени

            if (ExitFromSomeCandlesIsOn)
            {
                if (GetIndexInter(position.TimeOpen, candles) + ExitFromSomeCandlesValue <= index)
                {
                    return GetPriceExit(position, price,ExitFromSomeCandlesSleepage);
                }
            }

            return 0;
        }

        /// <summary>
        /// взять индекс свечи по времени
        /// </summary>
        private int GetIndexInter(DateTime time, List<Candle> candles)
        {
            for (int i = candles.Count - 1; i > 0; i--)
            {
                if (candles[i].TimeStart <= time)
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>
        /// взять цену выхода из позиции
        /// </summary>
        private decimal GetPriceExit(Position position, decimal price, int sleepage)
        {
            if (position.Direction == Side.Buy)
            {
                return price - _tab.Securiti.PriceStep*sleepage;
            }
            else // if (position.Direction == Side.Sell)
            {
                return price + _tab.Securiti.PriceStep * sleepage;
            }
        }
    }

}
