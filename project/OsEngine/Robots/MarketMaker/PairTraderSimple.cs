using System;
using System.Collections.Generic;
using System.IO;
using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.MarketMaker
{

    /// <summary>
    /// robot for pair trading. trading two papers based on their acceleration to each other by candle
    ///  робот для парного трейдинга. торговля двумя бумагами на основе их ускорения друг к другу по свечкам
    /// </summary>
    public class PairTraderSimple : BotPanel
    {

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
        /// uniq name
        /// взять уникальное имя стратегии
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "PairTraderSimple";
        }

        /// <summary>
        /// settings GUI
        /// показать индивидуальное окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            PairTraderSimpleUi ui = new PairTraderSimpleUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// save settings
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
                // ignore
            }
        }

        /// <summary>
        /// save settings
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
                // ignore
            }
        }

        /// <summary>
        /// delete save files
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        //settings публичные настройки

        /// <summary>
        /// regime
        /// режим работы робота
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// candles count to backlook
        /// количество свечей смотрим назад 
        /// </summary>
        public int CountCandles;

        /// <summary>
        /// discrepancy after which we start to gain a position
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

        // logic торговля

        /// <summary>
        /// trade tab 1
        /// вкладка с первым инструметом
        /// </summary>
        private BotTabSimple _tab1;

        /// <summary>
        /// trade tab 2
        /// вкладка со вторым инструментом
        /// </summary>
        private BotTabSimple _tab2;

        /// <summary>
        /// ready candles tab1
        /// готовые свечи первого инструмента
        /// </summary>
        private List<Candle> _candles1;

        /// <summary>
        /// ready candles tab2
        /// готовые свечи второго инструмента
        /// </summary>
        private List<Candle> _candles2;

        /// <summary>
        /// new candles in tab1
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
        /// new candles tab2
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
        /// enter position logic
        /// логика входа в позицию
        /// </summary>
        private void Trade()
        {
            if (_candles1.Count - 1 - CountCandles <= 0)
            {
                return;
            }

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
        /// exit position logic
        /// логика выхода из позиции
        /// </summary>
        private void CheckExit()
        {
            if (_candles1.Count - 1 - CountCandles < 0)
            {
                return;
            }

            decimal movePersent1 = 100 / _candles1[_candles1.Count - 1 - CountCandles].Close *
                       _candles1[_candles1.Count - 1].Close;

            decimal movePersent2 = 100 / _candles2[_candles2.Count - 1 - CountCandles].Close *
                                   _candles2[_candles2.Count - 1].Close;

            decimal spredNow = Math.Abs(movePersent1 - movePersent2);

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
        /// close position
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
        /// num position
        /// номера позиции
        /// </summary>
        public List<int> NumberPositions = new List<int>();

        /// <summary>
        /// spread in time inter
        /// спред на момент входа
        /// </summary>
        public decimal Spred;
    }
}
