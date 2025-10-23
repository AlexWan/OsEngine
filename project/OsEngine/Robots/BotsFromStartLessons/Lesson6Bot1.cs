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
1) Buy At Stop when the price breaks the upper Bollinger Band.
2) Add a second position: Buy At Stop at EntryPrice + ATR × MultOne.
3) Add a third position: Buy At Stop at EntryPrice + ATR × MultTwo.

Sell:
Close all positions using a Trailing Stop along the lower Bollinger Band.
*/

namespace OsEngine.Robots.BotsFromStartLessons
{
    // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    // Вместо того, чтобы добавлять вручную через BotFactory, мы используем атрибут для упрощения процесса.
    [Bot("Lesson6Bot1")]
    public class Lesson6Bot1 : BotPanel
    {
        // Reference to the main trading tab
        // Ссылка на главную вкладку
        private BotTabSimple _tabToTrade;

        // Basic settings
        // Базовые настройки
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _multOne;
        private StrategyParameterDecimal _multTwo;

        // GetVolume settings
        // Настройки метода GetVolume
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator Bollinger settings
        // Настройка индикатора Bollinger
        private StrategyParameterInt _lengthBollinger;
        private StrategyParameterDecimal _bollingerDeviation;

        // Indicator atr settings
        // Настройка индикатора atr
        private StrategyParameterInt _atrLength;

        // Indicators
        private Aindicator _bollinger;
        private Aindicator _atr;

        public Lesson6Bot1(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            // Создаём главную вкладку для торговли
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];

            // Basic settings
            // Базовые настройки
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _multOne = CreateParameter("Mult 1", 0.5m, 1.0m, 50, 4);
            _multTwo = CreateParameter("Mult 2", 1, 1.0m, 50, 4);

            // GetVolume settings
            // Настройки метода GetVolume
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator Bollinger settings
            // Настройки индикатора Bollinger
            _lengthBollinger = CreateParameter("Bollinger len", 21, 10, 100, 2);
            _bollingerDeviation = CreateParameter("Bollinger deviation", 1.5m, 10, 100, 2);

            // Indicator atr settings
            // Настройка индикатора atr
            _atrLength = CreateParameter("Length ATR", 14, 10, 100, 2);

            // Create indicator Bollinger 
            // Создание индикатора Bollinger 
            _bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
            _bollinger = (Aindicator)_tabToTrade.CreateCandleIndicator(_bollinger, "Prime");
            _bollinger.ParametersDigit[0].Value = _lengthBollinger.ValueInt;
            _bollinger.ParametersDigit[1].Value = _bollingerDeviation.ValueDecimal;

            // Create indicator atr 
            // Создание индикатора atr
            _atr = IndicatorsFactory.CreateIndicatorByName("ATR", name + "ATR", false);
            _atr = (Aindicator)_tabToTrade.CreateCandleIndicator(_atr, "Atr Area");
            _atr.ParametersDigit[0].Value = _atrLength.ValueInt;

            // Subscribe handler to track robot parameter changes
            // Подписка обработчик для отслеживания изменений параметров робота
            ParametrsChangeByUser += Lesson6Bot1_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            // Подписка на завершение свечи
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel14;
        }

        private void Lesson6Bot1_ParametrsChangeByUser()
        {
            _bollinger.ParametersDigit[0].Value = _lengthBollinger.ValueInt;
            _bollinger.ParametersDigit[1].Value = _bollingerDeviation.ValueDecimal;
            _bollinger.Reload();
            _bollinger.Save();

            _atr.ParametersDigit[0].Value = _atrLength.ValueInt;
            _atr.Reload();
            _atr.Save();
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
            { // opening the first position

                decimal bollingerUpLine = _bollinger.DataSeries[0].Last;

                if (bollingerUpLine == 0)
                {
                    return;
                }

                decimal volume = GetVolume(_tabToTrade);

                _tabToTrade.BuyAtStop(volume, bollingerUpLine, bollingerUpLine, StopActivateType.HigherOrEqual);
            }
            else if (positions.Count == 1)
            { // opening the second position
                decimal entryPriceFirstPosition = positions[0].EntryPrice;

                decimal atrValue = _atr.DataSeries[0].Last;

                if (atrValue == 0)
                {
                    return;
                }

                decimal newEntryPrice = entryPriceFirstPosition + atrValue * _multOne.ValueDecimal;

                decimal volume = GetVolume(_tabToTrade);

                _tabToTrade.BuyAtStop(volume, newEntryPrice, newEntryPrice, StopActivateType.HigherOrEqual);
            }
            else if (positions.Count == 2)
            { // opening third position
                decimal entryPriceFirstPosition = positions[0].EntryPrice;

                decimal atrValue = _atr.DataSeries[0].Last;

                if (atrValue == 0)
                {
                    return;
                }

                decimal newEntryPrice = entryPriceFirstPosition + atrValue * _multTwo.ValueDecimal;

                decimal volume = GetVolume(_tabToTrade);

                _tabToTrade.BuyAtStop(volume, newEntryPrice, newEntryPrice, StopActivateType.HigherOrEqual);
            }

            if (positions.Count > 0)
            { // We arrange the trailing stop on all positions

                decimal bollingerDownLine = _bollinger.DataSeries[1].Last;

                if (bollingerDownLine == 0)
                {
                    return;
                }

                for (int i = 0; i < positions.Count; i++)
                {
                    Position currentPos = positions[i];

                    _tabToTrade.CloseAtTrailingStop(currentPos, bollingerDownLine, bollingerDownLine);
                }
            }
        }

        public override string GetNameStrategyType()
        {
            return "Lesson6Bot1";
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