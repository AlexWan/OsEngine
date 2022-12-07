/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Indicators;

namespace OsEngine.Robots.Screeners
{
    public class SmaScreener : BotPanel
    {
        public SmaScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _screenerTab = TabsScreener[0];

            _screenerTab.NewTabCreateEvent += _screenerTab_NewTabCreateEvent;
            _screenerTab.CreateCandleIndicator(1, "Sma", new List<string>() { "100" }, "Prime");

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            MaxPoses = CreateParameter("Max poses", 1, 1, 20, 1);
            Slippage = CreateParameter("Slippage", 0, 0, 20, 1);
            Volume = CreateParameter("Volume", 0.1m, 0.1m, 50, 0.1m);
            TrailStop = CreateParameter("Trail Stop", 0.7m, 0.5m, 5, 0.1m);
            CandlesLookBack = CreateParameter("Candles Look Back count", 10, 5, 100, 1);

        }

        public override string GetNameStrategyType()
        {
            return "SmaScreener";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        /// <summary>
        /// вкладка скринера
        /// </summary>
        BotTabScreener _screenerTab;

        /// <summary>
        /// максимальное кол-во позиций
        /// </summary>
        public StrategyParameterInt MaxPoses;

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public StrategyParameterInt Slippage;

        /// <summary>
        /// volume for entry
        /// объём для входа
        /// </summary>
        public StrategyParameterDecimal Volume;

        /// <summary>
        /// Trail stop length in percent
        /// длинна трейлинг стопа в процентах
        /// </summary>
        public StrategyParameterDecimal TrailStop;

        /// <summary>
        /// regime
        /// режим работы
        /// </summary>
        public StrategyParameterString Regime;

        /// <summary>
        /// Кол-во свечек которые мы смотрим с конца
        /// </summary>
        public StrategyParameterInt CandlesLookBack;

        /// <summary>
        /// Событие создания новой вкладки
        /// </summary>
        private void _screenerTab_NewTabCreateEvent(BotTabSimple newTab)
        {
            newTab.CandleFinishedEvent += (List<Candle> candles) =>
            {
                NewCandleEvent(candles, newTab);
            };
        }

        /// <summary>
        /// событие завершения свечи
        /// </summary>
        /// <param name="candles">массив свечек</param>
        /// <param name="tab">источник по которому произошло событие</param>
        private void NewCandleEvent(List<Candle> candles, BotTabSimple tab)
        {
            // 1 Если поза есть, то по трейлинг стопу закрываем

            // 2 Позы нет. Открывать лонг, если последние N свечей мы были над скользящей средней
            
            if(Regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 10)
            {
                return;
            }

            if(candles.Count - 1 - CandlesLookBack.ValueInt - 1 <= 0)
            {
                return;
            }

            int allPosesInAllTabs = this.PositionsCount;

            if(allPosesInAllTabs >= MaxPoses.ValueInt)
            {
                return;
            }

            List<Position> positions = tab.PositionsOpenAll;

            if(positions.Count == 0)
            { // логика открытия

                Aindicator sma = (Aindicator)tab.Indicators[0];

                for(int i = candles.Count-1; i >= 0 && i > candles.Count -1 - CandlesLookBack.ValueInt;i--)
                {
                    decimal curSma = sma.DataSeries[0].Values[i];

                    if(curSma == 0)
                    {
                        return;
                    }

                    if (candles[i].Close < curSma)
                    {
                        return;
                    }
                }

                if(candles[candles.Count - 1 - CandlesLookBack.ValueInt - 1].Close > sma.DataSeries[0].Values[candles.Count - 1 - CandlesLookBack.ValueInt - 1])
                {
                    return;
                }

                tab.BuyAtLimit(Volume.ValueDecimal, tab.PriceBestAsk + tab.Securiti.PriceStep * Slippage.ValueInt);
            }
            else
            {
                Position pos = positions[0];

                if(pos.State != PositionStateType.Open)
                {
                    return;
                }

                decimal close = candles[candles.Count - 1].Low;
                decimal priceActivation = close - close * TrailStop.ValueDecimal/100;
                decimal priceOrder = priceActivation - tab.Securiti.PriceStep * Slippage.ValueInt;

                tab.CloseAtTrailingStop(pos, priceActivation, priceOrder);
            }
        }
    }
}
