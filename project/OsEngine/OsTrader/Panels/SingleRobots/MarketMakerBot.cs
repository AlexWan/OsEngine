using System;
using System.Collections.Generic;
using System.IO;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.OsTrader.Panels.SingleRobots
{
    /// <summary>
    /// стратегия реализующая набор котртрендовой позиции по линиям
    /// </summary>
    public class MarketMakerBot : BotPanel
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public MarketMakerBot(string name, StartProgram startProgram)
            : base(name, startProgram)
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
            return "MarketMakerBot";
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            MarketMakerBotUi ui = new MarketMakerBotUi(this);
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
        public decimal Volume;

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
                    Volume = Convert.ToDecimal(reader.ReadLine());
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
            decimal totalDeal = 0;

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
}
