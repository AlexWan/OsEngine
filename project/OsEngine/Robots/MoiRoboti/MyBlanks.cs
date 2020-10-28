using OsEngine.Entity;
using OsEngine.OsMiner.Patterns;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

namespace OsEngine.Robots.MoiRoboti
{
    public class MyBlanks 
    {
        private BotTabSimple _tab; // поле хранения вкладки робота 
        private StrategyParameterDecimal slippage; // величина проскальзывание при установки ордеров  
        private StrategyParameterDecimal komis_birgi; // комиссия биржи в %
        //private StrategyParameterInt  part_depo;  // часть депозита для входа
        private StrategyParameterInt part_tovara; // часть товара для продажи
        private StrategyParameterDecimal min_lot;    //  минимальный объем для входа на бирже

        public decimal _vol_stop; // объем проданного товара по стопу 
        public decimal price; // текущая  цена центра стакана 
        public decimal _kom; // поле для хранения величины комиссии биржи в пунктах
        public decimal depo; // количество квотируемой в портфеле
        public decimal tovar; // количество товара  в портфеле
        public decimal volum_ma; // последние значение индикатора MA  
        public decimal price_position = 1; // хранение цены последней открытой позиции

        public MyBlanks() 
        {
 
            // инициализация переменных и параметров 
            price = 0;
            _kom = 0;
 
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;    
        }

 
        public decimal Percent_birgi() // вычисление % биржи в пунктах для учета в расчетах выставления ордеров 
        {
            decimal price = _tab.PriceCenterMarketDepth;
            return _kom = price / 100 * komis_birgi.ValueDecimal;
        }
        public static decimal Okruglenie(decimal vol) // округляет децимал до 6 чисел после запятой 
        {
            decimal value = vol;
            int N = 6;
            decimal chah = decimal.Round(value, N, MidpointRounding.ToEven);
            return chah;
        }
        public decimal Lot() // расчет минимального лота 
        {
            price = _tab.PriceCenterMarketDepth;
            min_lot.ValueDecimal = Okruglenie(10.1m / price);
            return Okruglenie(10.1m / price);
        }
        public decimal Balans_kvot()   // запрос квотируемых средств в портфеле (в USDT) 
        {
            List<PositionOnBoard> poses = _tab.Portfolio.GetPositionOnBoard();
            decimal vol_kvot = 0;
            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == "USDT")
                {
                    vol_kvot = poses[i].ValueCurrent;
                    break;
                }
            }
            if (vol_kvot != 0)
            {
                depo = vol_kvot;
            }
            return depo;
        }
        public decimal Balans_tovara()   // запрос торгуемых средств в портфеле (в BTC ) 
        {
            List<PositionOnBoard> poses = _tab.Portfolio.GetPositionOnBoard();
            decimal vol_instr = 0;
            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == "BTC")
                {
                    vol_instr = poses[i].ValueCurrent;
                }
            }
            if (vol_instr != 0)
            {
                tovar = vol_instr;
            }
            return tovar;
        }
        private void _tab_MarketDepthUpdateEvent(MarketDepth marketDepth)
        {
            price = _tab.PriceCenterMarketDepth; // записываем текущую цену рынка
        }
        private void _tab_CandleFinishedEvent(List<Candle> candles) { }
 
    }
}
