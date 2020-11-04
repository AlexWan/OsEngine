using OsEngine.Entity;
using OsEngine.OsMiner.Patterns;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

namespace OsEngine.Robots.MoiRoboti
{
    public class MyBlanks : BotPanel
    {
        private BotTabSimple _tab; // поле хранения вкладки робота 

        private StrategyParameterString kvot_val; // квотируемая валюта - инструмент
        private StrategyParameterString tovar_val; // Базовая валюта - товар
        private StrategyParameterDecimal komis_birgi; // комиссия биржи в %
        
        
        public decimal _vol_stop; // объем проданного товара по стопу 
        public decimal price; // текущая  цена центра стакана 
        public decimal _kom; // поле для хранения величины комиссии биржи в пунктах
        public decimal depo; // количество квотируемой в портфеле
        public decimal tovar; // количество товара  в портфеле
        public decimal volum_ma; // последние значение индикатора MA  
        public decimal price_position = 1; // хранение цены последней открытой позиции
        public decimal _min_sum; // минимальная стоимость ордера на бирже 
        public decimal _min_lot; // поле хранящее величину минимального лота для биржи

        public MyBlanks (string name, StartProgram startProgram) : base(name, startProgram) // конструктор робота тут  
        {
            // инициализация переменных и параметров 
            price = 1;
            _kom = 0;

            kvot_val = CreateParameter("КвотВалюта-Инструмент", "USDT");
            tovar_val = CreateParameter("Базовая Валюта-Товар", "BTC");
            
            komis_birgi = CreateParameter("КОМ биржи в %", 0.2m, 0, 0.1m, 0.1m);

            TabCreate(BotTabType.Simple);  // создание простой вкладки
            _tab = TabsSimple[0]; // записываем первую вкладку в поле

            _tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;    
        }
        public decimal Percent_birgi() // вычисление % биржи в пунктах для учета в расчетах выставления ордеров 
        {
            decimal price = _tab.PriceCenterMarketDepth;
            return _kom = price / 100 * komis_birgi.ValueDecimal;
        }
        public decimal Lot(decimal _min_sum) // расчет минимального лота 
        {
            _min_lot = Okruglenie(_min_sum / price,6);
            return _min_lot;
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
        public override string GetNameStrategyType()
        {
            return "Frank";
        }
        public override void ShowIndividualSettingsDialog()
        {
            
        }
 // Static методы 
        public static decimal Okruglenie(decimal vol, int n) // округляет децимал до n чисел после запятой 
        {
            decimal value = vol;
            int N = n;
            decimal chah = decimal.Round(value, N, MidpointRounding.ToEven);
            return chah;
        }
    }
}
