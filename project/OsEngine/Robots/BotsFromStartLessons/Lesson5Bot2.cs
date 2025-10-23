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

Buy:
if alligator lips > teeth > jaw (lips - fast, teeth - medium, jaw - slow),
additional open if last value AO > previous value AO and previous value AO > previous previous value AO.

Exit: Close At Trailing Stop Market.
*/

namespace OsEngine.Robots.BotsFromStartLessons
{
    // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    // Вместо того, чтобы добавлять вручную через BotFactory, мы используем атрибут для упрощения процесса.
    [Bot("Lesson5Bot2")]
    public class Lesson5Bot2 : BotPanel
    {
        // Reference to the main trading tab
        // Ссылка на главную вкладку
        private BotTabSimple _tabToTrade;

        // Basic settings
        // Базовые настройки
        private StrategyParameterString _mode;
        
        // GetVolume settings
        // настройки метода GetVolume
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator alligator settings
        // Настройки индикатора alligator
        private StrategyParameterInt _lengthJaw;
        private StrategyParameterInt _lengthTeeth;
        private StrategyParameterInt _lengthLips;

        // Indicator PriceChannel setting
        // Настройка индикатора PriceChannel
        private StrategyParameterInt _lengthPriceChannel;

        // Indicator AO settings
        // Настройки индикатора AO
        private StrategyParameterInt _lengthFastLineAO;
        private StrategyParameterInt _lengthSlowLineAO;

        // Indicators
        private Aindicator _alligator;
        private Aindicator _priceChannel;
        private Aindicator _aO;

        public Lesson5Bot2(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            // Создаём главную вкладку для торговли
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];

            // Basic settings
            // Базовые настройки
            _mode = CreateParameter("Mode", "Off", new[] { "Off", "On" });

            // GetVolume settings
            // Настройки метода GetVolume
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Alligator settings
            // Настройки индикатора alligator
            _lengthJaw = CreateParameter("Alligator Jaw", 13, 10, 100, 2);
            _lengthTeeth = CreateParameter("Alligator Teeth", 8, 8, 100, 2);
            _lengthLips = CreateParameter("Alligator Lips", 5, 10, 100, 2);

            // PriceChannel setting
            // Настройка индикатора PriceChannel
            _lengthPriceChannel = CreateParameter("Length price channel", 21, 10, 100, 2);

            // AO settings
            // Настройки индикатора AO
            _lengthFastLineAO = CreateParameter("AO fast line", 5, 10, 100, 2);
            _lengthSlowLineAO = CreateParameter("AO slow line", 32, 10, 100, 2);

            // Create indicator Alligator
            // Настройки индикатора alligator
            _alligator = IndicatorsFactory.CreateIndicatorByName("Alligator", name + "Alligator", false);
            _alligator = (Aindicator)_tabToTrade.CreateCandleIndicator(_alligator, "Prime");
            _alligator.ParametersDigit[0].Value = _lengthJaw.ValueInt;
            _alligator.ParametersDigit[1].Value = _lengthTeeth.ValueInt;
            _alligator.ParametersDigit[2].Value = _lengthLips.ValueInt;

            // Create indicator PriceChannel
            // Создание индикатора PriceChannel
            _priceChannel = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PriceChannel", false);
            _priceChannel = (Aindicator)_tabToTrade.CreateCandleIndicator(_priceChannel, "Prime");
            _priceChannel.ParametersDigit[0].Value = _lengthPriceChannel.ValueInt;
            _priceChannel.ParametersDigit[1].Value = _lengthPriceChannel.ValueInt;

            // Create indicator AO
            // Создание индикатора AO
            _aO = IndicatorsFactory.CreateIndicatorByName("AO", name + "AO", false);
            _aO = (Aindicator)_tabToTrade.CreateCandleIndicator(_aO, "AreaAO");
            _aO.ParametersDigit[0].Value = _lengthFastLineAO.ValueInt;
            _aO.ParametersDigit[1].Value = _lengthSlowLineAO.ValueInt;

            // Subscribe handler to track robot parameter changes
            // Подписка обработчик для отслеживания изменений параметров робота
            ParametrsChangeByUser += Lesson5Bot2_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            // Подписка на завершение свечи
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel13;
        }

        private void Lesson5Bot2_ParametrsChangeByUser()
        {
            _alligator.ParametersDigit[0].Value = _lengthJaw.ValueInt;
            _alligator.ParametersDigit[1].Value = _lengthTeeth.ValueInt;
            _alligator.ParametersDigit[2].Value = _lengthLips.ValueInt;
            _alligator.Reload();
            _alligator.Save();

            _priceChannel.ParametersDigit[0].Value = _lengthPriceChannel.ValueInt;
            _priceChannel.ParametersDigit[1].Value = _lengthPriceChannel.ValueInt;
            _priceChannel.Reload();
            _priceChannel.Save();

            _aO.ParametersDigit[0].Value = _lengthFastLineAO.ValueInt;
            _aO.ParametersDigit[1].Value = _lengthSlowLineAO.ValueInt;
            _aO.Reload();
            _aO.Save();
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

            if (positions.Count == 0) // no positions. True! // Нет позиций. Правда!
            {   
                // Opening the position 
                // Открытие позиции

                decimal jaw = _alligator.DataSeries[0].Last;   // Long // Долгий
                decimal teeth = _alligator.DataSeries[1].Last; // Medium // Средний
                decimal lips = _alligator.DataSeries[2].Last;  // Short // Быстрый

                if (jaw == 0
                    || // Operator OR // Оператор ИЛИ
                    teeth == 0
                    || lips == 0)
                {
                    // If something is true - exit the method. Indicator not ready
                    // Если что-то верно - выход из метода. Индикатор не готов
                    return;
                }

                if (lips > teeth
                    && // Operator AND // Оператор И
                    teeth > jaw)
                {
                    // If both expressions are true, enter the position
                    // Если оба выражения верны, входим в позицию

                    decimal volume = GetVolume(_tabToTrade);
                    _tabToTrade.BuyAtMarket(volume);
                }
            }
            else if (positions[0].OpenOrders.Count == 1) // There is already one open position. True! // Уже есть одна открытая позиция. Правда!
            {
                // Additional open
                // Дополнительное открытые

                decimal lastAO = _aO.DataSeries[0].Values[_aO.DataSeries[0].Values.Count - 1];
                decimal prevAO = _aO.DataSeries[0].Values[_aO.DataSeries[0].Values.Count - 2];
                decimal prevPrevAO = _aO.DataSeries[0].Values[_aO.DataSeries[0].Values.Count - 3];

                if (lastAO == 0
                    || prevAO == 0
                    || prevPrevAO == 0)
                {
                    return;
                }

                if (prevAO < lastAO
                    && prevAO < prevPrevAO)
                {
                    decimal volume = GetVolume(_tabToTrade);
                    _tabToTrade.BuyAtMarketToPosition(_tabToTrade.PositionsOpenAll[0], volume);
                }
            }

            if (positions.Count == 1) // position is open // позиция открыта
            {
                // use trailling stop
                // используем trailling stop

                decimal pcLow = _priceChannel.DataSeries[1].Last;

                if (pcLow == 0)
                {
                    return;
                }

                Position position = positions[0];

                _tabToTrade.CloseAtTrailingStopMarket(position, pcLow);
            }
        }

        public override string GetNameStrategyType()
        {
            return "Lesson5Bot2";
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