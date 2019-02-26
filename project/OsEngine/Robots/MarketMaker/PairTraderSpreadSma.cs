/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.MarketMaker
{
    /// <summary>
    /// pair trading robot building spread and trading based on the intersection of MA on the spread chart
    /// робот для парного трейдинга строящий спред и торгующий на основе данных о пересечении машек на графике спреда
    /// </summary>
    public class PairTraderSpreadSma : BotPanel
    {
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
        /// uniq name
        /// взять уникальное имя стратегии
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "PairTraderSpreadSma";
        }

        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            PairTraderSpreadSmaUi ui = new PairTraderSpreadSmaUi(this);
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


                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// load settings
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
                // ignore
            }
        }

        /// <summary>
        /// delete file
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // settings публичные настройки

        /// <summary>
        /// regime
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// volume to tab1
        /// объём первого инструмента
        /// </summary>
        public decimal Volume1;

        /// <summary>
        /// volume to tab2
        /// объём второго инструмента
        /// </summary>
        public decimal Volume2;

        /// <summary>
        /// slippage tab1
        /// проскальзоывание для первого инструмента
        /// </summary>
        public decimal Slipage1;

        /// <summary>
        /// slippage tab2
        /// проскальзывание для второго инструмента
        /// </summary>
        public decimal Slipage2;

        //trade logic торговля

        /// <summary>
        /// tab to trade 1
        /// вкладка с первым инструметом
        /// </summary>
        private BotTabSimple _tab1;

        /// <summary>
        /// tab to trade 2
        /// вкладка со вторым инструментом
        /// </summary>
        private BotTabSimple _tab2;

        /// <summary>
        /// index tab
        /// вкладка спреда
        /// </summary>
        private BotTabIndex _tabSpread;

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
        /// index candles
        /// свечи спреда
        /// </summary>
        private List<Candle> _candlesSpread;

        private MovingAverage _smaLong;

        private MovingAverage _smaShort;

        /// <summary>
        /// new candles in tab 1
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
        /// new candles in tab2
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

        /// <summary>
        /// tab index new candles
        /// новые свечи из вкладки со спредом
        /// </summary>
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
        /// open position logic
        /// логика входа в позицию
        /// </summary>
        private void Trade()
        {
            // 1 если короткая машка на спреде пересекла длинную машку
            //1 if the short MA on the spread crossed the long MA
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
            {
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
                // crossed up
                _tab1.SellAtLimit(Volume1, _candles1[_candles1.Count - 1].Close - Slipage1);
                _tab2.BuyAtLimit(Volume2, _candles2[_candles2.Count - 1].Close + Slipage2);
            }

            if (smaShortLast > smaLongLast &&
                smaShortNow < smaLong)
            {
                // пересекли вниз
                //crossed down
                _tab2.SellAtLimit(Volume2, _candles2[_candles2.Count - 1].Close - Slipage2);
                _tab1.BuyAtLimit(Volume1, _candles1[_candles1.Count - 1].Close + Slipage1);
            }
        }

        /// <summary>
        /// check exit from position
        /// проверить выходы из позиций
        /// </summary>
        private void CheckExit()
        {
            List<Position> positions = _tab1.PositionsOpenAll;

            if (positions == null || positions.Count == 0)
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
}
