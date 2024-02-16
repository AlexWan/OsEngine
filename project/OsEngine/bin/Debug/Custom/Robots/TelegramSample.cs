using System;
using System.Net;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using System.Collections.Generic;
using OsEngine.OsTrader.Panels.Attributes;         
using OsEngine.Indicators;
using OsEngine.Logging;
using System.Threading;

/* Description
trading robot for OsEngine

Robot is an example of sending notifications to Telegram

Telegram receives notifications about opening of a deal, closing of a deal and loss of connection to server

Its trading strategy is based on breakdown of PriceChannel - we buy at breakdown of upper border of channel, close the position at breakdown of lower one

Robot is for demonstration, not for real trading - be careful

Be careful in Tester and Optimazer - it can often send notifications
 */


namespace OsEngine.Robots.MyRobots
{
    [Bot("TelegramSample")]
    public class TelegramSample : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterDecimal Volume;
        private StrategyParameterInt PriceChannelLength;

        // Telegram Allerts Settings
        private StrategyParameterString AlertsRegime;
        private StrategyParameterString TelegramID;
        private StrategyParameterString BotToken;

        // Indicator
        Aindicator _priceChannel;

        // Was there a connection to server
        private bool _isConnect;  

        public TelegramSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Trade Settings
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" }, "Trade settings");
            Volume = CreateParameter("Volume (lots)", 1m, 1m, 50m, 1m, "Trade settings");
            PriceChannelLength = CreateParameter("Price Channel Length", 21, 7, 70, 7, "Trade settings");

            // Telegram Allerts Settings
            AlertsRegime = CreateParameter("Alerts Regime", "Off", new[] { "Off", "On" }, "Telegram settings");
            TelegramID = CreateParameter("Telegram ID", "", "Telegram settings");
            BotToken = CreateParameter("Bot Token", "", "Telegram settings");

            // Create indicator PriceChannel
            _priceChannel = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PriceChannel", false);
            _priceChannel = (Aindicator)_tab.CreateCandleIndicator(_priceChannel, "Prime");
            ((IndicatorParameterInt)_priceChannel.Parameters[0]).ValueInt = PriceChannelLength.ValueInt;
            ((IndicatorParameterInt)_priceChannel.Parameters[1]).ValueInt = PriceChannelLength.ValueInt;
            _priceChannel.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += TelegramSample_ParametrsChangeByUser;

            // Subscribe to candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Subscribe to event of successful opening of a position
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;

            // Subscribe to event of successful closing of a position
            _tab.PositionClosingSuccesEvent += _tab_PositionClosingSuccesEvent;

            // If this is a real connection (OsTrader) - run server connection check in a separate thread
            if (startProgram == StartProgram.IsOsTrader)
            {
                Thread worker = new Thread(CheckConnect);
                worker.IsBackground = true;
                worker.Start();
            }

            Description = "Robot is an example of sending notifications to Telegram. " +
                "Telegram receives notifications about opening of a deal, closing of a deal and loss of connection to server. " +
                "Its trading strategy is based on breakdown of PriceChannel - we buy at breakdown of upper border of channel, close position at breakdown of lower one. " +
                "Robot is for demonstration, not for real trading - be careful. " +
                "Be careful in Tester and Optimazer - it can often send notifications.";
        }

        private void _tab_PositionClosingSuccesEvent(Position position)
        {
            if (AlertsRegime.ValueString == "On")
            {
                string message = "Closing long position (" + _tab.NameStrategy + ")" + "\r\n" + "By price: " + position.ClosePrice;
                SendTelegramMessageAsync(message);
            }
        }

        private void _tab_PositionOpeningSuccesEvent(Position position)
        {
            if (AlertsRegime.ValueString == "On")
            {
                string message = "Open long position (" + _tab.NameStrategy + ")" + "\r\n" + "By price: " + position.EntryPrice + "\r\n" + "Volume: " + position.OpenVolume;
                SendTelegramMessageAsync(message);
            }
        }

        private void TelegramSample_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_priceChannel.Parameters[0]).ValueInt = PriceChannelLength.ValueInt;
            ((IndicatorParameterInt)_priceChannel.Parameters[1]).ValueInt = PriceChannelLength.ValueInt;
            _priceChannel.Save();
            _priceChannel.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "TelegramSample";
        }
        public override void ShowIndividualSettingsDialog()
        {
            
        }

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (Regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < PriceChannelLength.ValueInt)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            // If there are positions, then go to the position closing method
            if (openPositions != null && openPositions.Count != 0)
            {
                LogicClosePosition(candles);
            }

            // If there are no positions, then go to the position opening method
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles);
            }
        }

        // Opening logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            decimal prevUpChannel = _priceChannel.DataSeries[0].Values[_priceChannel.DataSeries[0].Values.Count - 2]; 

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                if (candles[candles.Count - 1].Close > prevUpChannel)
                {
                    _tab.BuyAtMarket(GetVolume());
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            Position pos = _tab.PositionsOpenAll[0];
            decimal prevDownChannel = _priceChannel.DataSeries[1].Values[_priceChannel.DataSeries[1].Values.Count - 2];

            if (pos.State == PositionStateType.Open)
            {
                if (candles[candles.Count - 1].Close < prevDownChannel)
                {
                    _tab.CloseAtMarket(pos, pos.OpenVolume);
                }
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume()
        {
            decimal volume = Volume.ValueDecimal;

            // If the robot is running in the tester
            if (StartProgram == StartProgram.IsTester)
            {
                volume = Math.Round(volume, 6);
            }
            else
            {
                volume = Math.Round(volume, _tab.Securiti.DecimalsVolume);
            }
            return volume;
        }

        // Method sending message to Telegram
        private async void SendTelegramMessageAsync(string message)
        {
            if (BotToken.ValueString == "" || TelegramID.ValueString == "")
            {
                SendNewLogMessage("Enter Telegram ID and Bot Token", LogMessageType.Error);              
                return;
            }

            // Collecting query string
            string reqStr = "https://api.telegram.org/bot" + BotToken.ValueString + "/sendMessage?chat_id=" + TelegramID.ValueString + "&text=" + message;
          
            try
            {
                WebRequest request = WebRequest.Create(reqStr);
                using (await request.GetResponseAsync()) { }
            }
            catch (Exception ex)
            {
                SendNewLogMessage("Check that Telegram ID and Bot Token are entered correctly", LogMessageType.Error);
            }
        }

        // Method of checking connection to server
        private void CheckConnect()
        {
            while (true)
            {
                // Check server status every 10 seconds
                Thread.Sleep(10000);

                // Check connection to server starts working after first connection to it
                if (_tab.ServerStatus == Market.Servers.ServerConnectStatus.Connect && _isConnect == false)
                {
                    _isConnect = true;
                }

                if (AlertsRegime.ValueString == "On")
                {
                    if (_tab.ServerStatus == Market.Servers.ServerConnectStatus.Disconnect && _isConnect == true)
                    {
                        string message = "Connection to server is lost (" + _tab.NameStrategy + ")!";
                        SendTelegramMessageAsync(message);
                        // Attention - it will spam every 10 seconds until you connect (process it additionally or turn it off)
                    }
                }
            }
        }
    }
}
