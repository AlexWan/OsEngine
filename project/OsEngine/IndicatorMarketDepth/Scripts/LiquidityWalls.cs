/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using System.Drawing;

namespace OsEngine.IndicatorMarketDepth.Scripts
{
    public class LiquidityWalls : AMarketDepth
    {
        public override string Description
        {
            get
            {
                string eng = "Liquidity Walls are the largest levels in the order book. Series 'BidWall' and 'AskWall' are the largest volumes on the nearest 'Levels' levels, 'BidWallPrice' and 'AskWallPrice' are their prices. A large wall acts as support (bid) or resistance (ask) until it is filled or cancelled. Parameter 'Levels' is how many levels to scan.";
                string ru = "Стены ликвидности (Liquidity Walls) — крупнейшие уровни в стакане. Серии «BidWall» и «AskWall» — максимальные объёмы на ближайших «Levels» уровнях, «BidWallPrice» и «AskWallPrice» — их цены. Крупная стена работает как поддержка (bid) или сопротивление (ask), пока её не исполнят или не снимут. Параметр «Levels» — сколько уровней сканировать.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private ParameterMarketDepthInt _levels;

        private IndicatorDataSeries _bidWall;

        private IndicatorDataSeries _bidWallPrice;

        private IndicatorDataSeries _askWall;

        private IndicatorDataSeries _askWallPrice;

        public override void OnStateChange(MarketDepthIndicatorState state)
        {
            if (state == MarketDepthIndicatorState.Configure)
            {
                _levels = CreateParameterInt("Levels", 20);
                _bidWall = CreateSeries("BidWall", Color.Green, IndicatorChartPaintType.Column, true);
                _bidWallPrice = CreateSeries("BidWallPrice", Color.Green, IndicatorChartPaintType.Line, true);
                _askWall = CreateSeries("AskWall", Color.Red, IndicatorChartPaintType.Column, true);
                _askWallPrice = CreateSeries("AskWallPrice", Color.Red, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(MarketDepth depth)
        {
            int levels = _levels.ValueInt;

            if (levels <= 0)
            {
                levels = 20;
            }

            decimal bidMaxVolume = 0;
            double bidMaxPrice = 0;

            for (int i = 0; i < depth.Bids.Count && i < levels; i++)
            {
                decimal volume = (decimal)depth.Bids[i].Bid;

                if (volume > bidMaxVolume)
                {
                    bidMaxVolume = volume;
                    bidMaxPrice = depth.Bids[i].Price;
                }
            }

            decimal askMaxVolume = 0;
            double askMaxPrice = 0;

            for (int i = 0; i < depth.Asks.Count && i < levels; i++)
            {
                decimal volume = (decimal)depth.Asks[i].Ask;

                if (volume > askMaxVolume)
                {
                    askMaxVolume = volume;
                    askMaxPrice = depth.Asks[i].Price;
                }
            }

            SetValue(_bidWall, bidMaxVolume);
            SetValue(_bidWallPrice, (decimal)bidMaxPrice);
            SetValue(_askWall, askMaxVolume);
            SetValue(_askWallPrice, (decimal)askMaxPrice);
        }
    }
}
