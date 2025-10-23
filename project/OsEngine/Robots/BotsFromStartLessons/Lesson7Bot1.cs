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
Three growing candles in a row.
Sma does not fall.
Volatility of three candles > HeightSoldiers.
Volatility of each candle > MinHeightOneSoldier.

Exit:
Close at trailing stop: stop price = Close last candle - TrailingStopReal.
*/

namespace OsEngine.Robots.BotsFromStartLessons
{
    // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    // Вместо того, чтобы добавлять вручную через BotFactory, мы используем атрибут для упрощения процесса.
    [Bot("Lesson7Bot1")]
    public class Lesson7Bot1 : BotPanel
    {
        // Reference to the main trading tab
        // Ссылка на главную вкладку
        private BotTabSimple _tabToTrade;

        // Basic settings
        // Базовые настройки
        private StrategyParameterString _regime;
        private StrategyParameterTimeOfDay _startTradeTime;
        private StrategyParameterTimeOfDay _endTradeTime;

        // GetVolume settings
        // настройки метода GetVolume
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // variables that are needed to calculate volatility and variables
        // переменные, необходимые для расчета волатильности и переменных
        private StrategyParameterInt _daysVolatilityAdaptive;
        private StrategyParameterDecimal _commonHeightSoldiers;
        private StrategyParameterDecimal _oneHeightSoldier;
        private StrategyParameterDecimal _trailingStopMult;

        // Sma setting
        // Настройка Sma
        private StrategyParameterInt _smaLen;

        // service fields for values.
        // сервисные поля для значений.
        private StrategyParameterDecimal _heightSoldiers;
        private StrategyParameterDecimal _minHeightOneSoldier;
        private StrategyParameterDecimal _trailingStopReal;

        public Lesson7Bot1(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            // Создаём главную вкладку для торговли
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];

            // Basic settings
            // Базовые настройки
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _startTradeTime = CreateParameterTimeOfDay("Start trade time", 11, 0, 0, 0);
            _endTradeTime = CreateParameterTimeOfDay("End trade time", 18, 0, 0, 0);

            // GetVolume settings
            // Настройки метода GetVolume
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Sma setting
            // Настройка Sma 
            _smaLen = CreateParameter("Sma len", 150, 20, 300, 5);

            // Variables that are needed to calculate volatility and variables
            // Переменные, необходимые для расчета волатильности и переменных
            _daysVolatilityAdaptive = CreateParameter("Volatility days average", 4, 2, 8, 1); // number of days we count volatility // количество дней, в течение которых мы считаем волатильность
            _commonHeightSoldiers = CreateParameter("Height all soldiers", 60m, 10, 80, 5);
            _oneHeightSoldier = CreateParameter("Height one soldier", 10m, 10, 80, 5);
            _trailingStopMult = CreateParameter("Trail stop mult", 140m, 10, 180, 5);

            // Service fields for values
            // Поля обслуживания для значений.
            _heightSoldiers = CreateParameter("Height soldiers", 1, 0, 20, 1m);
            _minHeightOneSoldier = CreateParameter("Min height one soldier", 0.2m, 0, 20, 1m);
            _trailingStopReal = CreateParameter("Trail stop real", 0.2m, 0, 20, 1m);

            // Subscribe to the candle finished event
            // Подписка на завершение свечи
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel15;
        }

        private void _tabToTrade_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 30)
            {
                return;
            }

            if (_startTradeTime.Value > candles[candles.Count - 1].TimeStart
                ||
                 _endTradeTime.Value < candles[candles.Count - 1].TimeStart)
            {
                // limits the algorithm’s uptime
                // ограничивает время работы алгоритма

                return;
            }

            if (candles.Count > 20
                &&
                candles[candles.Count - 1].TimeStart.Date != candles[candles.Count - 2].TimeStart.Date)
            {
                AdaptSoldiersHeight(candles);
            }

            List<Position> positions = _tabToTrade.PositionsOpenAll;

            if (positions.Count == 0)
            {
                // check the opening conditions
                // проверка условий открытия

                Candle candleFirst = candles[candles.Count - 1];
                Candle candleSecond = candles[candles.Count - 2];
                Candle candleThird = candles[candles.Count - 3];

                if (candleFirst.IsDown
                    || candleSecond.IsDown
                    || candleThird.IsDown)
                {
                    return;
                }

                decimal lastSma = Sma(candles, _smaLen.ValueIntStart, candles.Count - 1);
                decimal prevSma = Sma(candles, _smaLen.ValueIntStart, candles.Count - 2);

                if (lastSma < prevSma)
                {
                    return;
                }

                decimal _lastPrice = candleFirst.Close;

                if (Math.Abs(candleThird.Open - candleFirst.Close)
                    / (candleFirst.Close / 100) < _heightSoldiers.ValueDecimal)
                {
                    return;
                }

                if (Math.Abs(candleThird.Open - candleThird.Close)
                    / (candleThird.Close / 100) < _minHeightOneSoldier.ValueDecimal)
                {
                    return;
                }

                if (Math.Abs(candleSecond.Open - candleSecond.Close)
                    / (candleSecond.Close / 100) < _minHeightOneSoldier.ValueDecimal)
                {
                    return;
                }

                if (Math.Abs(candleFirst.Open - candleFirst.Close)
                    / (candleFirst.Close / 100) < _minHeightOneSoldier.ValueDecimal)
                {
                    return;
                }

                // you need to buy here. All filters passed
                // Вы должны купить здесь. Все фильтры прошли

                decimal volume = GetVolume(_tabToTrade);
                _tabToTrade.BuyAtMarket(volume);
            }
            else
            {
                // Close At Trailing Stop
                // Закрытие по trailing Stop

                decimal stopPrice = candles[candles.Count - 1].Close - _trailingStopReal.ValueDecimal;
                _tabToTrade.CloseAtTrailingStopMarket(positions[0], stopPrice);
            }
        }

        private void AdaptSoldiersHeight(List<Candle> candles)
        {
            if (_daysVolatilityAdaptive.ValueInt <= 0)
            {
                return;
            }

            // 1) Calculate the movement from high to low within N days
            // 1) Вычислить движение от high к Low в течение N дней

            decimal minValueInDay = decimal.MaxValue;
            decimal maxValueInDay = decimal.MinValue;

            List<decimal> volaInDaysPercent = new List<decimal>();

            DateTime date = candles[candles.Count - 1].TimeStart.Date;

            int days = 0;

            for (int i = candles.Count - 1; i >= 0; i--)
            {
                Candle curCandle = candles[i];

                if (curCandle.TimeStart.Date < date)
                {
                    date = curCandle.TimeStart.Date;
                    days++;

                    decimal volaAbsToday = maxValueInDay - minValueInDay;
                    decimal volaPercentToday = volaAbsToday / (minValueInDay / 100);

                    volaInDaysPercent.Add(volaPercentToday);

                    minValueInDay = decimal.MaxValue;
                    maxValueInDay = decimal.MinValue;
                }

                if (days >= _daysVolatilityAdaptive.ValueInt)
                {
                    break;
                }

                if (curCandle.High > maxValueInDay)
                {
                    maxValueInDay = curCandle.High;
                }
                if (curCandle.Low < minValueInDay)
                {
                    minValueInDay = curCandle.Low;
                }

                if (i == 0)
                {
                    days++;

                    decimal volaAbsToday = maxValueInDay - minValueInDay;
                    decimal volaPercentToday = volaAbsToday / (minValueInDay / 100);
                    volaInDaysPercent.Add(volaPercentToday);
                }
            }

            if (volaInDaysPercent.Count == 0)
            {
                return;
            }

            // 2) Averaging this movement. Average volatility is needed. Percent
            // 2) Усреднение этого движения. Требуется средняя волатильность. Процент

            decimal volaPercentSma = 0;

            for (int i = 0; i < volaInDaysPercent.Count; i++)
            {
                volaPercentSma += volaInDaysPercent[i];
            }

            volaPercentSma = volaPercentSma / volaInDaysPercent.Count;

            // 3) we calculate the size of the candles taking into account this volatility
            // 3) мы считаем размер свечей с учётом этой волатильности

            decimal allSoldiersHeight = volaPercentSma * (_commonHeightSoldiers.ValueDecimal / 100);
            decimal oneSoldiersHeight = volaPercentSma * (_oneHeightSoldier.ValueDecimal / 100);
            decimal trail = volaPercentSma * (_trailingStopMult.ValueDecimal / 100);

            _heightSoldiers.ValueDecimal = allSoldiersHeight;
            _minHeightOneSoldier.ValueDecimal = oneSoldiersHeight;
            _trailingStopReal.ValueDecimal = trail;
        }

        private decimal Sma(List<Candle> candles, int len, int index)
        {
            if (candles.Count == 0
                || index >= candles.Count
                || index <= 0)
            {
                return 0;
            }

            decimal summ = 0;

            int countPoints = 0;

            for (int i = index; i >= 0 && i > index - len; i--)
            {
                countPoints++;
                summ += candles[i].Close;
            }

            if (countPoints == 0)
            {
                return 0;
            }

            return summ / countPoints;
        }

        public override string GetNameStrategyType()
        {
            return "Lesson7Bot1";
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