/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

/* Description
Robot example from the lecture course "C# for algotreader".

Buy:
low-value from Candle < last value Sma and close-value Candle > last value Sma. Buy at market.

Sell:
high-value from Candle > last value Sma and close-value Candle < last value Sma. Sell at market.                 

Exit:
If candle time more than opening position + value parameter robot "_minutesInPosition".Close At Market.
*/

namespace OsEngine.Robots.BotsFromStartLessons
{
    // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    // Вместо того, чтобы добавлять вручную через BotFactory, мы используем атрибут для упрощения процесса.
    [Bot("Lesson5Bot1")]
    public class Lesson5Bot1 : BotPanel
    {
        // Reference to the main trading tab
        // Ссылка на главную вкладку
        private BotTabSimple _tabToTrade;

        // Basic settings
        // Базовые настройки
        private StrategyParameterString _mode;
        private StrategyParameterInt _minutesInPosition;

        // Indicator setting
        // Настройка индикатора
        private StrategyParameterInt _smaLenFast;

        // GetVolume settings
        // настройки метода GetVolume
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        //Indicator
        private Aindicator _sma;

        public Lesson5Bot1(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            // Создаём главную вкладку для торговли
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];

            // Basic settings
            // Базовые настройки
            _mode = CreateParameter("Mode", "Off", new[] { "Off", "On" });
            _minutesInPosition = CreateParameter("Minutes in position", 90, 30, 500, 30);

            // GetVolume settings
            // Настройки метода GetVolume
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            //Indicator setting
            //Настройка индикатора
            _smaLenFast = CreateParameter("Sma len", 15, 1, 10, 1);

            // Create indicator Sma
            // Создание индикатора Sma
            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma", false);
            _sma = (Aindicator)_tabToTrade.CreateCandleIndicator(_sma, "Prime");
            _sma.ParametersDigit[0].Value = _smaLenFast.ValueInt;

            // Subscribe handler to track robot parameter changes
            // Подписка обработчик для отслеживания изменений параметров робота
            ParametrsChangeByUser += Lesson5Bot1_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            // Подписка на завершение свечи
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel12;
        }

        private void Lesson5Bot1_ParametrsChangeByUser()
        {
            _sma.ParametersDigit[0].Value = _smaLenFast.ValueInt;
            _sma.Reload();
            _sma.Save();
        }

        private void _tabToTrade_CandleFinishedEvent(List<Candle> candles)
        {
            // Сalled on each new candle
            // Вызывается перед каждой новой свечой

            if (_mode.ValueString == "Off")
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

                decimal lastSma = _sma.DataSeries[0].Last;

                if (lastSma == 0)
                {
                    return;
                }

                Candle candle = candles[candles.Count - 1];
                decimal closeCandle = candle.Close;
                decimal highCandle = candle.High;
                decimal lowCandle = candle.Low;

                if (
                    lowCandle < lastSma
                    &&
                    closeCandle > lastSma
                    )
                {
                    // Opening long
                    // Открытие лонг

                    decimal volume = GetVolume(_tabToTrade);
                    _tabToTrade.BuyAtMarket(volume);

                }
                else if (
                    highCandle > lastSma
                    &&
                    closeCandle < lastSma)
                {
                    // Opening short
                    // Открытие шорта

                    decimal volume = GetVolume(_tabToTrade);
                    _tabToTrade.SellAtMarket(volume);
                }
                else
                {
                    // Do nothing
                    // Ничего не делаем
                }
            }
            else
            {   
                // Сlosing the position
                // Закрытие позиции

                Position position = positions[0];

                DateTime positionEntryTime = position.TimeOpen;

                DateTime timeExtPosition = positionEntryTime.AddMinutes(_minutesInPosition.ValueInt);

                DateTime candleTime = candles[candles.Count - 1].TimeStart;

                if (candleTime > timeExtPosition)
                {
                    // Exit from position. If candle time more than opening position + value parameter robot "_minutesInPosition"
                    // Выход из позиции. Если время свечи больше, чем открытие позиции + значение параметра робота "_minutesInPosition"

                    if (position.State == PositionStateType.Open)
                    {
                        _tabToTrade.CloseAtMarket(position, position.OpenVolume);
                        SendNewLogMessage($"Закрыта позиция. Время закрытия позиции: {timeExtPosition}, Время открытия свечи: {candleTime}", Logging.LogMessageType.User);
                    }
                }
            }
        }

        public override string GetNameStrategyType()
        {
            return "Lesson5Bot1";
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