/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.Trend
{
    /// <summary>
    /// Trend strategy at the intersection of the ParabolicSar indicator
    /// Трендовая стратегия на пересечение индикатора ParabolicSar
    /// </summary>
    [Bot("ParabolicSarTrade")]
    public class ParabolicSarTrade : BotPanel
    {
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

            Description = "Trend strategy at the intersection of the ParabolicSar indicator. " +
                "if Price < lastSar - close position and open Short. " +
                "if Price > lastSar - close position and open Long.";

            //Подписка на получение событий/команд из телеграма - Subscribe to receive events/commands from Telegram
            ServerTelegram.GetServer().TelegramCommandEvent += TelegramCommandHandler;
            
        }
        //Последний режим работы бота перед выключением по команде
        private BotTradeRegime _lastRegime = BotTradeRegime.Off;
        
        private void TelegramCommandHandler(string botName, Command cmd)
        {
            if (botName != null && !_tab.TabName.Equals(botName)) 
                return;
            
            if (cmd == Command.StopAllBots || cmd == Command.StopBot)
            {
                _lastRegime = Regime;
                Regime = BotTradeRegime.Off;
                
                SendNewLogMessage($"Changed Bot {_tab.TabName} Regime to Off " +
                                  $"by telegram command {cmd}", LogMessageType.User);
            }
            else if (cmd == Command.StartAllBots || cmd == Command.StartBot)
            {

                if (_lastRegime != BotTradeRegime.Off)
                {
                    Regime = _lastRegime;
                }
                else
                {
                    Regime = BotTradeRegime.On;
                }
                
                //changing bot mode to its previous state or On
                SendNewLogMessage($"Changed bot {_tab.TabName} mode to state {Regime} " +
                                  $"by telegram command {cmd}", LogMessageType.User);
            }
            else if (cmd == Command.CancelAllActiveOrders)
            {
                //Some logic for cancel all active orders
            }
            else if (cmd == Command.GetStatus)
            {
                SendNewLogMessage($"Bot {_tab.TabName} is {Regime}. Emulator - {_tab.EmulatorIsOn}, " +
                                  $"Server Status - {_tab.ServerStatus}.", LogMessageType.User);
            }
        }
        
        /// <summary>
        /// strategy name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "ParabolicSarTrade";
        }
        /// <summary>
        /// settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            ParabolicSarTradeUi ui = new ParabolicSarTradeUi(this);
            ui.ShowDialog();
        }
        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        private ParabolicSaR _sar;

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
        private decimal _lastSar;

        // logic логика

        /// <summary>
        /// candle finished event
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            //SendNewLogMessage("Candle finished event", LogMessageType.User);

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
        /// logic close position
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
}
