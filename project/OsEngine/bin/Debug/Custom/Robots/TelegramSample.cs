/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Market.Servers;
using OsEngine.Market;
using System.Net;
using OsEngine.Logging;
using System.Threading;
using OsEngine.Language;

/* Description
trading robot for OsEngine

Robot is an example of sending notifications to Telegram

Telegram receives notifications about opening of a deal, closing of a deal and loss of connection to server

Its trading strategy is based on breakdown of PriceChannel - we buy at breakdown of upper border of channel, close the position at breakdown of lower one

Robot is for demonstration, not for real trading - be careful

Be careful in Tester and Optimazer - it can often send notifications
 */

namespace OsEngine.Robots
{
    [Bot("TelegramSample")]
    public class TelegramSample : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _priceChannelLength;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Telegram Allerts Settings
        private StrategyParameterString _alertsRegime;
        private StrategyParameterString _telegramID;
        private StrategyParameterString _botToken;

        // Indicator
        private Aindicator _priceChannel;

        // Was there a connection to server
        private bool _isConnect;  

        public TelegramSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Trade Settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" }, "Trade settings");
            _priceChannelLength = CreateParameter("Price Channel Length", 21, 7, 70, 7, "Trade settings");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Telegram Allerts Settings
            _alertsRegime = CreateParameter("Alerts Regime", "Off", new[] { "Off", "On" }, "Telegram settings");
            _telegramID = CreateParameter("Telegram ID", "", "Telegram settings");
            _botToken = CreateParameter("Bot Token", "", "Telegram settings");

            // Create indicator PriceChannel
            _priceChannel = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PriceChannel", false);
            _priceChannel = (Aindicator)_tab.CreateCandleIndicator(_priceChannel, "Prime");
            ((IndicatorParameterInt)_priceChannel.Parameters[0]).ValueInt = _priceChannelLength.ValueInt;
            ((IndicatorParameterInt)_priceChannel.Parameters[1]).ValueInt = _priceChannelLength.ValueInt;
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

            Description = OsLocalization.Description.DescriptionLabel295;
        }

        private void _tab_PositionClosingSuccesEvent(Position position)
        {
            if (_alertsRegime.ValueString == "On")
            {
                string message = "Closing long position (" + _tab.NameStrategy + ")" + "\r\n" + "By price: " + position.ClosePrice;
                SendTelegramMessageAsync(message);
            }
        }

        private void _tab_PositionOpeningSuccesEvent(Position position)
        {
            if (_alertsRegime.ValueString == "On")
            {
                string message = "Open long position (" + _tab.NameStrategy + ")" + "\r\n" + "By price: " + position.EntryPrice + "\r\n" + "Volume: " + position.OpenVolume;
                SendTelegramMessageAsync(message);
            }
        }

        private void TelegramSample_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_priceChannel.Parameters[0]).ValueInt = _priceChannelLength.ValueInt;
            ((IndicatorParameterInt)_priceChannel.Parameters[1]).ValueInt = _priceChannelLength.ValueInt;
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
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _priceChannelLength.ValueInt)
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
                    _tab.BuyAtMarket(GetVolume(_tab));
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

        // Method sending message to Telegram
        private async void SendTelegramMessageAsync(string message)
        {
            if (_botToken.ValueString == "" || _telegramID.ValueString == "")
            {
                SendNewLogMessage("Enter Telegram ID and Bot Token", LogMessageType.Error);              
                return;
            }

            // Collecting query string
            string reqStr = "https://api.telegram.org/bot" + _botToken.ValueString + "/sendMessage?chat_id=" + _telegramID.ValueString + "&text=" + message;
          
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

                if (_alertsRegime.ValueString == "On")
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

        // Method for calculating the volume of entry into a position
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
                else // Tester or Optimizer
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
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
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
                    {// расчёт количества контрактов для фьючерсов и опционов на Мосбирже
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
    }
}