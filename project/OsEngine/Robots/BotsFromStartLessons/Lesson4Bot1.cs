/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Language;

/* Description
Robot example from the lecture course "C# for algotreader".
This robot shows the tracking of various source events.

Buy:
SmaFast > SmaSlow. Buy at the market.

Sell:
SmaFast < SmaSlow. Close at the market.
*/

namespace OsEngine.Robots.BotsFromStartLessons
{
    // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    // Вместо того, чтобы добавлять вручную через BotFactory, мы используем атрибут для упрощения процесса.
    [Bot("Lesson4Bot1")]
    public class Lesson4Bot1 : BotPanel
    {
        // Reference to the main trading tab
        // Ссылка на главную вкладку
        private BotTabSimple _tabToTrade;

        // Basic settings
        // Базовые настройки
        private StrategyParameterString _mode;

        // Indicator settings
        // Настройки индикаторов
        private StrategyParameterInt _smaLenFast;
        private StrategyParameterInt _smaLenSlow;

        // GetVolume settings
        // настройки метода GetVolume
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicators
        private Aindicator _smaFast;
        private Aindicator _smaSlow;

        public Lesson4Bot1(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            // Создаём главную вкладку для торговли
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];

            // Basic settings
            // Базовые настройки
            _mode = CreateParameter("Mode", "Off", new[] { "Off", "On" });

            //Indicators settings
            //Настройки индикаторов
            _smaLenFast = CreateParameter("Sma fast len", 15, 1, 10, 1);
            _smaLenSlow = CreateParameter("Sma slow len", 100, 1, 10, 1);

            // GetVolume settings
            // настройки метода GetVolume
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Create indicator SmaFast
            // Создание индикатора SmaFast
            _smaFast = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaFast", false);
            _smaFast = (Aindicator)_tabToTrade.CreateCandleIndicator(_smaFast, "Prime");
            _smaFast.ParametersDigit[0].Value = _smaLenFast.ValueInt;

            // Create indicator SmaSlow
            // Создание индикатора SmaSlow
            _smaSlow = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaSlow", false);
            _smaSlow = (Aindicator)_tabToTrade.CreateCandleIndicator(_smaSlow, "Prime");
            _smaSlow.ParametersDigit[0].Value = _smaLenSlow.ValueInt;

            // Subscribe handler to track robot parameter changes
            // Подписка обработчика для отслеживания изменений параметров робота
            ParametrsChangeByUser += Lesson3Bot3_ParametrsChangeByUser;

            // Subscribe handler to track _tabToTrade events
            // Подписка обработчика для отслеживания _tabToTrade событий
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;
            _tabToTrade.CandleUpdateEvent += _tabToTrade_CandleUpdateEvent;
            _tabToTrade.OrderUpdateEvent += _tabToTrade_OrderUpdateEvent;
            _tabToTrade.MarketDepthUpdateEvent += _tabToTrade_MarketDepthUpdateEvent;
            _tabToTrade.PositionOpeningSuccesEvent += _tabToTrade_PositionOpeningSuccesEvent;
            _tabToTrade.NewTickEvent += _tabToTrade_NewTickEvent;

            Description = OsLocalization.Description.DescriptionLabel11;
        }

        private void _tabToTrade_PositionOpeningSuccesEvent(Position position)
        {
            // successful position opening event
            // событие - успешное открытие позиции

            // For example
            // Для примера
        }

        private void _tabToTrade_MarketDepthUpdateEvent(MarketDepth marketDepth)
        {
            // update market depth
            // событие - обновление стакана заявок

            // For example
            // Для примера
        }

        private void _tabToTrade_OrderUpdateEvent(Order order)
        {
            // order update event 
            // событие - Обновление ордера

            // For example
            // Для примера
        }

        private void _tabToTrade_CandleUpdateEvent(List<Candle> candles)
        {
            // candle update event.
            // событие - обновление свечи

            // For example
            // Для примера
        }

        private void _tabToTrade_NewTickEvent(Trade trade)
        {
            // new trade event
            // событие - новый торговый тик

            // For example
            // Для примера
        }

        private void Lesson3Bot3_ParametrsChangeByUser()
        {
            _smaFast.ParametersDigit[0].Value = _smaLenFast.ValueInt;
            _smaFast.Reload();
            _smaFast.Save();

            _smaSlow.ParametersDigit[0].Value = _smaLenSlow.ValueInt;
            _smaSlow.Reload();
            _smaSlow.Save();
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

            if (positions.Count == 0)
            {   
                // Opening the position 
                // Открытие позиции

                decimal smaFastLast = _smaFast.DataSeries[0].Last;

                if (smaFastLast == 0)
                {
                    return;
                }

                decimal smaSlowLast = _smaSlow.DataSeries[0].Last;

                if (smaSlowLast == 0)
                {
                    return;
                }

                if (smaFastLast > smaSlowLast)
                {
                    decimal volume = GetVolume(_tabToTrade);
                    _tabToTrade.BuyAtMarket(volume);
                }
            }
            else
            {   
                // Сlosing the position
                // Закрытие позиции

                decimal smaFastLast = _smaFast.DataSeries[0].Last;

                if (smaFastLast == 0)
                {
                    return;
                }

                decimal smaSlowLast = _smaSlow.DataSeries[0].Last;

                if (smaSlowLast == 0)
                {
                    return;
                }

                if (smaFastLast < smaSlowLast)
                {
                    // Take position from the array
                    // Берём позицию из массива
                    Position position = positions[0];

                    if (position.State == PositionStateType.Open)
                    {
                        _tabToTrade.CloseAtMarket(position, position.OpenVolume);
                    }
                }
            }
        }

        public override string GetNameStrategyType()
        {
            return "Lesson4Bot1";
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