/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.PanelsGui;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Robots.Trend;
using MessageBox = System.Windows.MessageBox;


namespace OsEngine.OsTrader.Panels
{

    public class PanelCreator
    {
        /// <summary>
        /// list robots name / 
        /// список доступных роботов
        /// </summary>
        public static List<string> GetNamesStrategy()
        {
            List<string> result = new List<string>();

            result.Add("Engine");
            result.Add("ClusterEngine");

            result.AddRange(OsEngine.Robots.BotFactory.GetNamesStrategy());

            result.Add("Levermor");
            result.Add("PairTraderSimple");
            result.Add("RsiTrade");
            result.Add("StochasticTrade");
            result.Add("BollingerTrade");
            result.Add("TRIXTrade");
            result.Add("CCITrade");
            result.Add("MACDTrade");
            result.Add("BBPowerTrade");
            result.Add("RviTrade");
            result.Add("MacdTrail");
            result.Add("MomentumMACD");
            result.Add("PairRsiTrade");
            result.Add("OneLegArbitration");
            result.Add("BollingerOutburst");
            result.Add("PriceChannelBreak");
            result.Add("PriceChannelVolatility");

            return result;
        }

        /// <summary>
        /// create robot
        /// создать робота
        /// </summary>
        public static BotPanel GetStrategyForName(string nameClass, string name, StartProgram startProgram)
        {

            BotPanel bot = null;
            // примеры и бесплатные боты

            if (nameClass == "MomentumMACD")
            {
                bot = new MomentumMacd(name, startProgram);
            }

            if (nameClass == "Engine")
            {
                bot = new StrategyEngineCandle(name, startProgram);
            }
            if (nameClass == "ClusterEngine")
            {
                bot = new ClusterEngine(name, startProgram);
            }
            
            if (nameClass == "Levermor")
            {
                bot = new StrategyLevermor(name, startProgram);
            }
            if (nameClass == "PairTraderSimple")
            {
                bot = new PairTraderSimple(name, startProgram);
            }

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
            if (nameClass == "MacdTrail")
            {
                bot = new MacdTrail(name, startProgram);
            }
            if (nameClass == "PairRsiTrade")
            {
                bot = new PairRsiTrade(name, startProgram);
            }

            if (nameClass == "OneLegArbitration")
            {
                bot = new OneLegArbitration(name, startProgram);
            }
            if (nameClass == "BollingerOutburst")
            {
                bot = new BollingerOutburst(name, startProgram);
            }
            if (nameClass == "PriceChannelVolatility")
            {
                bot = new PriceChannelVolatility(name, startProgram);
            }
            if (nameClass == "PriceChannelBreak")
            {
                bot = new PriceChannelBreak(name, startProgram);
            }

            if(bot == null)
            {
                return OsEngine.Robots.BotFactory.GetStrategyForName(nameClass, name, startProgram);
            }

            return bot;
        }
    }

    # region examples of robots for optimization / примеры роботов для оптимизации

    /// <summary>
    /// Jesse Livermore's trend strategy based on channel breakdown
    /// трендовая стратегия Джесси Ливермора, на основе пробоя канала
    /// </summary>
    public class StrategyLevermor : BotPanel
    {
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

        public override string GetNameStrategyType()
        {
            return "Levermor";
        }

        public override void ShowIndividualSettingsDialog()
        {
            MessageBox.Show(
                OsLocalization.Trader.Label56);
        }

        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        // indicators / индикаторы

        /// <summary>
        /// MA
        /// скользящая средняя
        /// </summary>
        private MovingAverage _smaTrenda;

        /// <summary>
        /// ATR
        /// индикатор: Атр
        /// </summary>
        private PriceChannel _channel;

// settings / настройки стандартные

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public StrategyParameterInt Slipage;

        /// <summary>
        /// regime
        /// режим работы робота
        /// </summary>
        public StrategyParameterString Regime;

        /// <summary>
        /// volume
        /// объём 
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
        /// delete file with save data
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // logic логика

        /// <summary>
        /// an event that occurs when a position is successfully opened
        /// событие, происходит когда позиция успешно открыта
        /// </summary>
        /// <param name="position">opened position / открытая позиция</param>
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
                    LogicClosePosition(openPosition, _tab.CandlesFinishedOnly);
                }
            }
            catch (Exception error)
            {
                _tab.SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// candle finished event
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
            // we distribute logic depending on the current position

            List<Position> openPosition = _tab.PositionsOpenAll;

            if (openPosition != null && openPosition.Count != 0)
            {
                LogicClosePosition(openPosition, candles);
            }

            if (Regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            LogicOpenPosition(candles);

        }

        /// <summary>
        /// position open logic
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
            // take the maximum and minimum for the last n bars

            decimal maxToCandleSeries = _channel.ValuesUp[_channel.ValuesUp.Count - 1];
            decimal minToCandleSeries = _channel.ValuesDown[_channel.ValuesDown.Count - 1];

            List<Position> positions = _tab.PositionsOpenAll;

            if (lastPrice >= lastMa && Regime.ValueString != "OnlyShort")
            {
                if (positions != null && positions.Count != 0 &&
                    positions[0].Direction == Side.Buy)
                { 
                    // если открыты лонги - добавляемся
                    //if longs are open - add more
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
                {
                    // nothing open. Send lines to open
                    // если ничего не открыто - ставим линии на пробой

                    _tab.SellAtStopCanсel();
                    _tab.BuyAtStopCanсel();
                    _tab.BuyAtStop(Volume.ValueDecimal, maxToCandleSeries + (Slipage.ValueInt * _tab.Securiti.PriceStep), maxToCandleSeries, StopActivateType.HigherOrEqual);
                }
            }

            if (lastPrice <= lastMa && Regime.ValueString != "OnlyLong")
            {
                if (positions != null && positions.Count != 0 &&
                         positions[0].Direction == Side.Sell)
                {
                    if (positions.Count >= MaximumPosition.ValueInt)
                    {
                        return;
                    }
                    decimal lastIntro = positions[positions.Count - 1].EntryPrice;

                    if (lastIntro - lastIntro * (PersentDopSell.ValueDecimal / 100) > lastPrice)
                    {
                        _tab.SellAtLimit(Volume.ValueDecimal, lastPrice - (Slipage.ValueInt * _tab.Securiti.PriceStep));
                    }
                }
                else if (positions == null || positions.Count == 0)
                {
                    if (positions != null && positions.Count >= MaximumPosition.ValueInt)
                    {
                        return;
                    }

                    _tab.SellAtStopCanсel();
                    _tab.BuyAtStopCanсel();
                    _tab.SellAtStop(Volume.ValueDecimal, minToCandleSeries - (Slipage.ValueInt * _tab.Securiti.PriceStep), minToCandleSeries, StopActivateType.LowerOrEqyal);
                }
            }
        }

        /// <summary>
        /// exit position logic
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
    /// Trend strategy at the intersection of the indicator RVI
    /// Трендовая стратегия на пересечение индикатора RVI
    /// </summary>
    public class RviTrade : BotPanel
    {
        public RviTrade(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            RviLenght = CreateParameter("RviLength", 10, 10, 80, 3);
            Volume = CreateParameter("Volume", 3, 1.0m, 50, 4);
            Slippage = CreateParameter("Slipage", 0, 0, 20, 1);

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
        /// strategy name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "RviTrade";
        }
        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
        }

        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

//indicators индикаторы

        private Rvi _rvi;

//settings настройки публичные

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public StrategyParameterInt Slippage;

        /// <summary>
        /// volume to inter
        /// фиксированный объем для входа
        /// </summary>
        public StrategyParameterDecimal Volume;

        /// <summary>
        /// regime
        /// режим работы
        /// </summary>
        public StrategyParameterString Regime;

        /// <summary>
        /// indicator length
        /// длинна индикатора
        /// </summary>
        public StrategyParameterInt RviLenght;

        private decimal _lastPrice;
        private decimal _lastRviUp;
        private decimal _lastRviDown;

        // logic / логика

        /// <summary>
        /// candle finished event
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
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
        /// open position logic
        /// логика открытия позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_lastRviDown < 0 && _lastRviUp > _lastRviDown && Regime.ValueString != "OnlyShort")
            {
                _tab.BuyAtLimit(Volume.ValueDecimal, _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
            }

            if (_lastRviDown > 0 && _lastRviUp < _lastRviDown && Regime.ValueString != "OnlyLong")
            {
                _tab.SellAtLimit(Volume.ValueDecimal, _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
            }
        }

        /// <summary>
        /// logic close position
        /// логика зыкрытия позиции
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.Direction == Side.Buy && position.State == PositionStateType.Open)
            {
                if (_lastRviDown > 0 && _lastRviUp < _lastRviDown)
                {
                    _tab.CloseAtLimit(position, _lastPrice - Slippage.ValueInt, position.OpenVolume);

                    if (Regime.ValueString != "OnlyLong" && Regime.ValueString != "OnlyClosePosition")
                    {
                        _tab.SellAtLimit(Volume.ValueDecimal, _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
                    }
                }
            }

            if (position.Direction == Side.Sell && position.State == PositionStateType.Open)
            {
                if (_lastRviDown < 0 && _lastRviUp > _lastRviDown)
                {
                    _tab.CloseAtLimit(position, _lastPrice + Slippage.ValueInt, position.OpenVolume);

                    if (Regime.ValueString != "OnlyShort" && Regime.ValueString != "OnlyClosePosition")
                    {
                        _tab.BuyAtLimit(Volume.ValueDecimal, _lastPrice + Slippage.ValueInt*_tab.Securiti.PriceStep);
                    }
                }
            }
        }

    }

    #endregion

    #region bots sample / готовые роботы

    public class ClusterEngine : BotPanel
    {

        public ClusterEngine(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Cluster);
        }

        /// <summary>
        /// strategy name 
        /// имя стратегии
        /// </summary>
        /// <returns></returns>
        public override string GetNameStrategyType()
        {
            return "ClusterEngine";
        }

        /// <summary>
        /// show settings
        /// показать настройки
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            MessageBox.Show(OsLocalization.Trader.Label112);
        }
    }

    /// <summary>
    /// Trading robot on the index. The intersection of MA on the index from the bottom up long, with the reverse intersection of shorts
    /// Торговый робот на индексе. Пересечение MA на индексе снизу вверх лонг, при обратном пересечении шорт 
    /// </summary>
    public class OneLegArbitration : BotPanel
    {
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
        /// bot name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "OneLegArbitration";
        }
        /// <summary>
        /// settings UI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            OneLegArbitrationUi ui = new OneLegArbitrationUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// index tab
        /// вкладка анализируемого индекса
        /// </summary>
        private BotTabIndex _tab1;

        /// <summary>
        /// trade tab
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab2;

        private MovingAverage _ma;

        //settings / настройки публичные

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// volume
        /// фиксированный объем для входа позицию
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// regime
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// save settings
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
                // ignore
            }
        }

        /// <summary>
        /// load settings
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
                // ignore
            }
        }

        /// <summary>
        /// delete
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        private decimal _lastPrice;
        private decimal _lastIndex;
        private decimal _lastMa;

        // logic логика

        /// <summary>
        /// candle finished event
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
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
        /// open position logic
        /// логика открытия позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            List<Position> openPositions = _tab2.PositionsOpenAll;
            if (openPositions == null || openPositions.Count == 0)
            {
                if (Regime != BotTradeRegime.OnlyShort)
                {
                    if (_lastIndex > _lastMa)
                    {
                        _tab2.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                    }
                }

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

        /// <summary>
        /// close position logic
        /// логика закрытия позиции
        /// </summary>
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
    /// Bollinger Bands trading bargaining robot with pull-up Trailing-Stop through Bollinger Bands
    /// Робот торгующий прорыв Bollinger Bands с подтягивающимся Trailing-Stop по линии Bollinger Bands
    /// </summary>
    public class BollingerOutburst : BotPanel
    {

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

            Slippage = 10;
            VolumeFix = 1;

            Load();

            DeleteEvent += Strategy_DeleteEvent;

        }

        /// <summary>
        /// uniq name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "BollingerOutburst";
        }

        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            BollingerOutburstUi ui = new BollingerOutburstUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// trade tab
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        private Bollinger _bollinger;

        //settings настройки публичные

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public decimal Slippage;

        /// <summary>
        /// volume
        /// фиксированный объем для входа позицию
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// regime
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// save settings
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
                    writer.WriteLine(Slippage);
                    writer.WriteLine(VolumeFix);
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
                    Slippage = Convert.ToDecimal(reader.ReadLine());
                    VolumeFix = Convert.ToDecimal(reader.ReadLine());
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// delete save file
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        private decimal _lastPrice;
        private decimal _lastBbUp;
        private decimal _lastBbDown;

        // logic логика

        /// <summary>
        /// candle finished event
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
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
        /// logic close pos
        /// логика закрытия позиции
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
        /// close one pos
        /// логика закрытия позиции
        /// </summary>
        private void ReloadTrailingPosition(Position position)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].Direction == Side.Buy)
                {
                    _tab.CloseAtTrailingStop(openPositions[i], _bollinger.ValuesDown[_bollinger.ValuesDown.Count - 1], _bollinger.ValuesDown[_bollinger.ValuesDown.Count - 1] - Slippage);
                }
                else
                {
                    _tab.CloseAtTrailingStop(openPositions[i], _bollinger.ValuesUp[_bollinger.ValuesUp.Count - 1], _bollinger.ValuesUp[_bollinger.ValuesUp.Count - 1] + Slippage);
                }
            }
        }


        /// <summary>
        /// open position logic
        /// логика открытия позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            if (openPositions == null || openPositions.Count == 0)
            {
                // long
                if (Regime != BotTradeRegime.OnlyShort)
                {
                    if (_lastPrice > _lastBbUp)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastPrice + Slippage);
                    }
                }

                // Short
                if (Regime != BotTradeRegime.OnlyLong)
                {
                    if (_lastPrice < _lastBbDown)
                    {
                        _tab.SellAtLimit(VolumeFix, _lastPrice - Slippage);
                    }
                }
                return;
            }
        }

    }

    /// <summary>
    ///When the candle is closed outside the PriceChannel channel,
    /// we enter the position, the stop loss is at the extremum of the last candle from the entry candle,
    /// take profit by the channel size from the close of the candle at which the entry occurred
    /// 
    /// При закрытии свечи вне канала PriceChannel входим в позицию , стоп-лосс за экстремум прошлойсвечи от свечи входа,
    /// тейкпрофит на величину канала от закрытия свечи на которой произошел вход
    /// </summary>
    public class PriceChannelBreak : BotPanel
    {
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
        /// uniq name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "PriceChannelBreak";
        }

        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            PriceChannelBreakUi ui = new PriceChannelBreakUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        /// <summary>
        /// PriceChannel
        /// </summary>
        private PriceChannel _pc;

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// volume
        /// фиксированный объем для входа позицию
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// regime
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// save settings
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
                // ignore
            }
        }

        /// <summary>
        /// load settings
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
                // ignore
            }
        }

        /// <summary>
        /// delete save file
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        private decimal _lastPrice;
        private decimal _lastPcUp;
        private decimal _lastPcDown;

        // logic логика

        /// <summary>
        /// candle finished event
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
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
        /// open position logic
        /// логика открытия позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            if (openPositions == null || openPositions.Count == 0)
            {
                // long
                if (Regime != BotTradeRegime.OnlyShort)
                {
                    if (_lastPrice > _lastPcUp)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                    }
                }

                // Short
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
        /// set stop orders and profit orders
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
    ///Breakthrough of the channel built by PriceChannel + -ATR * coefficient,
    /// additional input when the price leaves below the channel line by ATR * coefficient.
    /// Trailing stop on the bottom line of the PriceChannel channel
    /// 
    /// Прорыв канала постоенного по PriceChannel+-ATR*коэффициент ,
    /// дополнительный вход при уходе цены ниже линии канала на ATR*коэффициент.
    /// Трейлинг стоп по нижней линии канала PriceChannel
    /// </summary>
    public class PriceChannelVolatility : BotPanel
    {
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
        /// uniq name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "PriceChannelVolatility";
        }

        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            PriceChannelVolatilityUi ui = new PriceChannelVolatilityUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// trading tab
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        /// <summary>
        /// Atr period
        /// период ATR
        /// </summary>
        public int LengthAtr;

        /// <summary>
        /// PriceChannel up line length
        /// период PriceChannel Up
        /// </summary>
        public int LengthUp;

        /// <summary>
        /// PriceChannel down line length
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

//settings / настройки публичные

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// volume first
        /// фиксированный объем для входа в первую позицию
        /// </summary>
        public decimal VolumeFix1;

        /// <summary>
        /// volume next
        /// фиксированный объем для входа во вторую позицию
        /// </summary>
        public decimal VolumeFix2;

        /// <summary>
        /// atr coef
        /// коэффициент ATR
        /// </summary>
        public decimal KofAtr;

        /// <summary>
        /// regime
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// save settings
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
                // ignore
            }
        }

        /// <summary>
        /// save settings
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

        private decimal _lastPcUp;
        private decimal _lastPcDown;
        private decimal _lastAtr;

        // logic логика

        /// <summary>
        /// candle finished event
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
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
        /// logic open position
        /// логика открытия первой позиции и дополнительного входа
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles )
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            if (openPositions == null || openPositions.Count == 0)
            {
                // long
                if (Regime != BotTradeRegime.OnlyShort)
                {
                    decimal priceEnter = _lastPcUp + (_lastAtr * KofAtr);
                    _tab.BuyAtStop(VolumeFix1, priceEnter + Slipage, priceEnter, StopActivateType.HigherOrEqual);
                }

                // Short
                if (Regime != BotTradeRegime.OnlyLong)
                {
                    decimal priceEnter = _lastPcDown - (_lastAtr * KofAtr);
                    _tab.SellAtStop(VolumeFix1, priceEnter - Slipage, priceEnter, StopActivateType.LowerOrEqyal);
                }
                return;
            }

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
        /// logic close position
        /// логика зыкрытия позиции
        /// </summary>
        private void LogicClosePosition()
        {
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
    /// blank strategy for manual trading
    /// пустая стратегия для ручной торговли
    /// </summary>
    public class StrategyEngineCandle : BotPanel
    {

        public StrategyEngineCandle(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            //создание вкладки
            TabCreate(BotTabType.Simple);
        }

        /// <summary>
        /// uniq name
        /// униальное имя стратегии
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "Engine";
        }

        /// <summary>
        /// show settings GUI
        /// показать настройки
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            MessageBox.Show(OsLocalization.Trader.Label57);
        }
    }

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

    /// <summary>
    /// RSI's concurrent overbought and oversold strategy
    /// конттрендовая стратегия RSI на перекупленность и перепроданность
    /// </summary>
    public class RsiTrade : BotPanel
    {
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
        /// uniq name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "RsiTrade";
        }

        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            RsiTradeUi ui = new RsiTradeUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        private Rsi _rsi;

        //settings настройки публичные

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// volume
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// regime
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
        /// save settings
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
                // ignore
            }
        }

        /// <summary>
        /// load settings
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
                // ignore
            }
        }

        /// <summary>
        /// delete save file
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        private decimal _lastPrice;
        private decimal _firstLastRsi;
        private decimal _secondLastRsi;


        //logic логика

        /// <summary>
        /// candles finished event
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
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
        /// logic open first position
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
        /// logic close position
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
    /// counter trend strategy stochastic
    /// конттрендовая стратегия Stochastic
    /// </summary>
    public class StochasticTrade : BotPanel
    {
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
        /// uniq name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "StochasticTrade";
        }
        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            StochasticTradeUi ui = new StochasticTradeUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        private StochasticOscillator _stoch;

        public LineHorisontal Upline;

        public LineHorisontal Downline;

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
        /// save settings
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
                // ignore
            }
        }

        /// <summary>
        /// load settings
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
                // ignore
            }
        }

        /// <summary>
        /// delete settins file
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        private decimal _lastPrice;
        private decimal _stocLastUp;
        private decimal _stocLastDown;


        // logic логика

        /// <summary>
        /// candle finished event
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
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
        /// logic open position
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
        /// logic close position
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
                        List<Position> positions = _tab.PositionsOpenAll;
                        if (positions.Count >= 2)
                        {
                            return;
                        }

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
                        List<Position> positions = _tab.PositionsOpenAll;
                        if (positions.Count >= 2)
                        {
                            return;
                        }
                        _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                    }

                }
            }
        }

    }

    /// <summary>
    /// Trend Strategy Based on Breaking Bollinger Lines
    /// Трендовая стратегия на основе пробития линий болинджера
    /// </summary>
    public class BollingerTrade : BotPanel
    {
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
        /// bot name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "BollingerTrade";
        }
        /// <summary>
        /// strategy name
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            BollingerTradeUi ui = new BollingerTradeUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// trade tab
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //indicators индикаторы

        private Bollinger _bol;

        //settings настройки публичные

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// volume
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// regime
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// save settings
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
                // ignore
            }
        }

        /// <summary>
        /// load settings
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
                // ignore
            }
        }

        /// <summary>
        /// delete save file
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        private decimal _lastPrice;
        private decimal _bolLastUp;
        private decimal _bolLastDown;

        // logic логика

        /// <summary>
        /// candle finished event
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
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
        /// logic open position
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
        /// logic close position
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
    /// Trend strategy based on the TRIX indicator
    /// Трендовая стратегия на основе индикатора TRIX
    /// </summary>
    public class TrixTrade : BotPanel
    {
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
        /// strategy name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "TRIXTrade";
        }
        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            TrixTradeUi ui = new TrixTradeUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// trading tab
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        private Trix _trix;

        //settings настройки публичные

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// step from level zero
        /// Шаг от 0 - го уровня
        /// </summary>
        public decimal Step;

        /// <summary>
        /// volume
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// regime
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// save settings
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
                // ignore
            }
        }

        /// <summary>
        /// load settings
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
                // ignore
            }
        }

        /// <summary>
        /// delete save file
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        private decimal _lastPrice;
        private decimal _lastTrix;

        // logic логика

        /// <summary>
        /// candle finished event
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
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
        /// logic open position
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
        /// logic close position
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
    /// Counter Trend Strategy Based on CCI Indicator
    /// Контртрендовая стратегия на основе индикатора CCI
    /// </summary>
    public class CciTrade : BotPanel
    {
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
        /// strategy name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "CCITrade";
        }
        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            CciTradeUi ui = new CciTradeUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        private Cci _cci;

        //settings настройки публичные

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// up line to trade
        /// верхняя линия для отрисовки
        /// </summary>
        public LineHorisontal Upline;

        /// <summary>
        /// down line to trade
        /// нижняя линия для отрисовки
        /// </summary>
        public LineHorisontal Downline;

        /// <summary>
        /// volume
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// regime
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// save settings
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
                // ignore
            }
        }

        /// <summary>
        /// load settings
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
                // ignore
            }
        }

        /// <summary>
        /// delete save file
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        private decimal _lastPrice;
        private decimal _lastCci;

        // logic логика

        /// <summary>
        /// candle finished event
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
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

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i],openPositions);

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
        /// logic open position
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
        /// logic close position
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position, List<Position> allPos)
        {
            if (position.State != PositionStateType.Open)
            {
                return;
            }

            if (position.Direction == Side.Buy)
            {
                if (_lastCci > Upline.Value)
                {
                    _tab.CloseAtLimit(position, _lastPrice - Slipage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyLong && 
                        Regime != BotTradeRegime.OnlyClosePosition &&
                        allPos.Count < 3)
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

                    if (Regime != BotTradeRegime.OnlyShort && 
                        Regime != BotTradeRegime.OnlyClosePosition &&
                        allPos.Count < 3)
                    {
                        _tab.BuyAtLimit(VolumeFix, _lastPrice + Slipage);
                    }
                }
            }
        }

    }

    /// <summary>
    /// Trend strategy based on two indicators BullsPower and BearsPower
    /// Трендовая стратегия на основе двух индикаторов BullsPower и BearsPower
    /// </summary>
    public class BbPowerTrade : BotPanel
    {
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
        /// uniq strategy name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "BBPowerTrade";
        }
        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            BbPowerTradeUi ui = new BbPowerTradeUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        // indicators индикаторы

        private BullsPower _bullsP;

        private BearsPower _bearsP;

        //settings настройки публичные

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// value to trade formula
        /// шаг от 0-го уровня
        /// </summary>
        public decimal Step;

        /// <summary>
        /// volume
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// regime
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// save settings
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
                // ignore
            }
        }

        /// <summary>
        /// load settings
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
                // ignore
            }
        }

        /// <summary>
        /// delete save file
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        private decimal _lastPrice;
        private decimal _lastBearsPrice;
        private decimal _lastBullsPrice;

        // logic логика

        /// <summary>
        /// candle finished event
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
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
        /// logic open position
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
        /// logic close position
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
    /// Trend strategy at the intersection of the MACD indicator
    /// Трендовая стратегия на пересечение индикатора MACD
    /// </summary>
    public class MacdTrade : BotPanel
    {
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
        /// uniq strategy name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "MACDTrade";
        }
        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            MacdTradeUi ui = new MacdTradeUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        /// <summary>
        /// MACD 
        /// </summary>
        private MacdLine _macd;

        //settings настройки публичные

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// volume
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// regime
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// save settings
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
                // ignore
            }
        }

        /// <summary>
        /// load settings
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
                // ignore
            }
        }

        /// <summary>
        /// delete save file
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        private decimal _lastPrice;
        private decimal _lastMacdUp;
        private decimal _lastMacdDown;

        // logic логика

        /// <summary>
        /// candle finished event
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
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
        /// logic open position
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
        /// logic close position
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
    /// Trend strategy based on the Macd indicator and trail stop
    /// Трендовая стратегия на основе индикатора Macd и трейлстопа
    /// </summary>
    public class MacdTrail : BotPanel
    {
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
        /// uniq strategy name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "MacdTrail";
        }
        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            MacdTrailUi ui = new MacdTrailUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        /// <summary>
        /// Macd 
        /// </summary>
        private MacdLine _macd;

        //settings настройки публичные

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// Stop Order value
        /// Значение ТрейлСтоп
        /// </summary>
        public decimal TrailStop;

        /// <summary>
        /// Stop Order step
        /// Шаг ТрейлСтоп
        /// </summary>
        public decimal Step;

        /// <summary>
        /// volume
        /// фиксированный объем для входа
        /// </summary>
        public decimal VolumeFix;

        /// <summary>
        /// regime
        /// режим работы
        /// </summary>
        public BotTradeRegime Regime;

        /// <summary>
        /// save settings
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
                // ignore
            }
        }

        /// <summary>
        /// load settings
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
                // ignore
            }
        }

        /// <summary>
        /// deltete save file
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        private decimal _lastClose;
        private decimal _lastLastClose;
        private decimal _lastMacdDown;
        private decimal _lastMacdUp;
        private decimal _awG;

        // logic логика

        /// <summary>
        /// candle finished event
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
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
        /// logic open position
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
        /// logic close position
        /// логика зыкрытия позиции
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
    /// Pair trading based on the RSI indicator
    /// Парная торговля на основе индикатора RSI
    /// </summary>
    public class PairRsiTrade : BotPanel
    {
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
        /// uniq strategy name
        /// взять уникальное имя стратегии
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "PairRsiTrade";
        }

        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            PairRsiTradeUi ui = new PairRsiTradeUi(this);
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
                    writer.WriteLine(Volume1);
                    writer.WriteLine(Volume2);
                    writer.WriteLine(RsiSpread);


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
                    Volume1 = Convert.ToDecimal(reader.ReadLine());
                    Volume2 = Convert.ToDecimal(reader.ReadLine());
                    RsiSpread = Convert.ToInt32(reader.ReadLine());


                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// delete save file
        /// удаление файла с сохранением
        /// </summary>
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // security публичные настройки

       /// <summary>
       /// spread to trade
        /// спред для торговли
        /// </summary>
        public int RsiSpread;

        /// <summary>
        /// volume to first security
        /// объём первого инструмента
        /// </summary>
        public decimal Volume1;

        /// <summary>
        /// volume to second security
        /// объём второго инструмента
        /// </summary>
        public decimal Volume2;

        /// <summary>
        /// tab to trade tab1
        /// вкладка с первым инструметом
        /// </summary>
        private BotTabSimple _tab1;

        /// <summary>
        /// tab to trade tab2
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

        private Rsi _rsi1;

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

        /// <summary>
        /// trade logic
        /// </summary>
        private void Trade()
        {

            if (_candles1.Count < 10 && _candles2.Count < 10)
            {
                return;;
            }

            List<Position> pos1 = _tab1.PositionsOpenAll;
            List<Position> pos2 = _tab2.PositionsOpenAll;

            if (pos1 != null && pos1.Count != 0 || pos2 != null && pos2.Count != 0)
            { 
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

    #endregion

    /// <summary>
    /// robot trade regime
    /// режим работы робота
    /// </summary>
    public enum BotTradeRegime
    {
        /// <summary>
        /// is on
        /// включен
        /// </summary>
        On,

        /// <summary>
        /// on only long position
        /// включен только лонг
        /// </summary>
        OnlyLong,

        /// <summary>
        /// on only short position
        /// включен только шорт
        /// </summary>
        OnlyShort,

        /// <summary>
        /// on only close position
        /// только закрытие позиции
        /// </summary>
        OnlyClosePosition,

        /// <summary>
        /// robot is off
        /// выключен
        /// </summary>
        Off
    }
}
