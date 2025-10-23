/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Language;

/* Description
Robot example from the lecture course "C# for algotreader".

Buy:
Buy At Stop high price channel.

Exit:
Close At Trailing Stop Market low price channel
*/

namespace OsEngine.Robots.BotsFromStartLessons
{
    // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    // Вместо того, чтобы добавлять вручную через BotFactory, мы используем атрибут для упрощения процесса.
    [Bot("Lesson8Bot2")]
    public class Lesson8Bot2 : BotPanel
    {
        // Reference to the main trading tab
        // Ссылка на главную вкладку
        private BotTabSimple _tabToTrade;

        // Basic settings
        // Базовые настройки
        private StrategyParameterString _regime;

        // GetVolume settings
        // Настройки метода GetVolume
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Price channel setting
        // Настройка Price channel
        private StrategyParameterInt _priceChannelLen;

        public Lesson8Bot2(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            // Создаём главную вкладку для торговли
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];

            // Basic settings
            // Базовые настройки
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });

            // GetVolume settings
            // Настройки метода GetVolume
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Price channel setting
            // Настройка Price channel
            _priceChannelLen = CreateParameter("Price Channel len", 40, 1, 50, 4);

            // Subscribe to the candle finished event
            // Подписка на завершение свечи
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel17;
        }

        private void _tabToTrade_CandleFinishedEvent(List<Candle> candles)
        {
            // Сalled on each new candle
            // Вызывается перед каждой новой свечой
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 10)
            {
                return;
            }

            List<Position> positions = _tabToTrade.PositionsOpenAll;

            if (positions.Count == 0) // No positions. True! // Нет позиций. Правда!
            {   
                // Opening the position 
                // Открытие позиции

                decimal high = GetHigh(candles, _priceChannelLen.ValueInt);
                decimal volume = GetVolume(_tabToTrade);
                _tabToTrade.BuyAtStop(volume, high, high, StopActivateType.HigherOrEqual);
            }
            else
            {   
                // Сlosing the position
                // Закрытие позиции

                decimal low = GetLow(candles, _priceChannelLen.ValueInt);
                _tabToTrade.CloseAtTrailingStopMarket(positions[0], low);
            }
        }

        private decimal GetHigh(List<Candle> candles, int len)
        {
            decimal high = 0;

            // From the end. Move back.
            // Цикл с конца. Движение назад
            for (int i = candles.Count - 1; i >= 0 && i > candles.Count - 1 - len; i--)
            {
                Candle currentCandle = candles[i];

                if (currentCandle.High > high)
                {
                    high = currentCandle.High;
                }
            }

            return high;
        }

        private decimal GetLow(List<Candle> candles, int len)
        {
            decimal low = decimal.MaxValue;

            // Цикл с конца. Движение вперёд
            for (int i = candles.Count - 1 - len; i >= 0 && i < candles.Count; i++)
            {
                Candle currentCandle = candles[i];

                if (currentCandle.Low < low)
                {
                    low = currentCandle.Low;
                }
            }

            return low;
        }

        public override string GetNameStrategyType()
        {
            return "Lesson8Bot2";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

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