using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Charts.CandleChart.Indicators;
using System.Threading;
using Microsoft.SqlServer.Server;

namespace OsEngine.Robots.MoiRoboti
{
    public class Frank : BotPanel
    {
        public decimal market_prise; // рыночная стоимость товара
        public string kvot_name ; // название квотируемой валюты - инструмента
        public string tovar_name; // название базовая валюты - товара
        private MyBlanks blanks = new MyBlanks("Frank", StartProgram.IsOsTrader); // создание экземпляра класса MyBlanks
        private BotTabSimple _tab; // поле хранения вкладки робота 
        public decimal percent_tovara; // поле хранения % товара 
        public decimal portfolio_sum; // поле хранения суммы в портфеле 
        public decimal start_sum; // поле хранения суммы на старт работы 
        public decimal start_prise; // поле хранения цены товара на старт работы 
        public decimal depo; // количество квотируемой в портфеле
        public decimal tovar; // количество товара  в портфеле
        public decimal min_lot; // поле хранящее величину минимального лота для биржи

        private StrategyParameterBool vkl_Robota; // поле включения бота 
        private StrategyParameterInt start_per_depo; // какую часть депозита использовать при старте робота в % 
        private StrategyParameterDecimal min_sum;    //  минимальный сумма для входа на бирже

        public Frank(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);  // создание простой вкладки
            _tab = TabsSimple[0]; // записываем первую вкладку в поле
            kvot_name = "USDT";  // тут надо указать - инициализировать название квотируемой валюты (деньги)
            tovar_name = "BTC"; // тут надо указать - инициализировать название товара 

            vkl_Robota = CreateParameter("РОБОТ Включен?", false);
            start_per_depo = CreateParameter("Начинать с ? % депо)", 5, 5, 20, 5);
            min_sum = CreateParameter("МИН сумма орд.на бирже($)", 10.1m, 10.1m, 10.1m, 10.1m);

            _tab.BestBidAskChangeEvent += _tab_BestBidAskChangeEvent; // событие изменения лучших цен
            _tab.OrderUpdateEvent += _tab_OrderUpdateEvent;
            
        }
        private void _tab_OrderUpdateEvent(Order order)
        {
            Switching_mode();
        }
        private void _tab_BestBidAskChangeEvent(decimal bid, decimal ask) // событие изменения лучших цен
        {
            market_prise = blanks.price;
            min_lot = blanks.Lot(min_sum.ValueDecimal);
            Start();

            Console.WriteLine(" минимальный лот  = " + min_lot + " BTC  ");
            Console.WriteLine(" стартовая сумма = " + start_sum + " $ ");
            Console.WriteLine(" в портфеле потрачено = " + Percent_tovara() + " % ");
        }
        void Start()
        {
            Percent_tovara();
            int a = start_per_depo.ValueInt;
            if ( a < percent_tovara )
            {
                return;
            }
            List<Position> positions = _tab.PositionsOpenAll;
            if (positions.Count != 0)
            {
                return;
            }
            if (vkl_Robota.ValueBool == false)
            {
                return;
            }
            start_sum = Portfolio_sum();
            start_prise = market_prise;
            decimal vol = MyBlanks.Okruglenie(blanks.Balans_kvot() / 100 * a, 2);
            if (vol > min_lot)
            {
                _tab.BuyAtLimit(vol, market_prise);
                Console.WriteLine(" Стартуем ордером на = " + vol + " $ по цене " + market_prise);
            }
            //vkl_Robota.ValueBool = false;
            Console.WriteLine(" выключили робот");
        }
        void Switching_mode()
        {
            Percent_tovara();
            if (90 <= percent_tovara) //  режим выхода 
            {

            }
            if ( 70 <= percent_tovara) //  режим фиксации прибыли
            {

            }
            if (50 <= percent_tovara) // режим набора товара и ожидания прибыли
            {

            }
            if (30 <= percent_tovara) // режим набора товара
            {

            }

        }
        public decimal Balans_kvot(string kvot_name)   // запрос квотируемых средств в портфеле - название присваивается в kvot_name
        {
            List<PositionOnBoard> poses = _tab.Portfolio.GetPositionOnBoard();
            decimal vol_kvot = 0;
            decimal vol_kvot_blok = 0;
            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == kvot_name)
                {
                    vol_kvot = poses[i].ValueCurrent;
                    vol_kvot_blok = poses[i].ValueBlocked;
                    break;
                }
            }
            if (vol_kvot != 0)
            {
                depo = vol_kvot + vol_kvot_blok;
            }
            return depo;
        }
        public decimal Balans_tovara(string tovar_name)   // запрос торгуемых средств в портфеле (в BTC ) название присваивается в tovar_name
        {
            List<PositionOnBoard> poses = _tab.Portfolio.GetPositionOnBoard();
            decimal vol_instr = 0;
            decimal vol_instr_blok = 0;
            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == tovar_name)
                {
                    vol_instr = poses[i].ValueCurrent;  // текущий объем 
                    vol_instr_blok = poses[i].ValueBlocked; // блокированный объем 
                }
            }
            if (vol_instr != 0)
            {
                tovar = vol_instr + vol_instr_blok;
            }
            return tovar;
        }
        public decimal Portfolio_sum() // расчет начального состояния портфеля по торгуемой паре
        {
            Balans_kvot(kvot_name);
            Balans_tovara(tovar_name);
            portfolio_sum = MyBlanks.Okruglenie(depo + tovar * market_prise, 2);
            return portfolio_sum;
        }
        public decimal Percent_tovara() // расчет % объема купленного товара в портфеле 
        {
            decimal st = Portfolio_sum();
            decimal kv = Balans_kvot(kvot_name);
            decimal rasxod = st - kv;
            decimal per = rasxod / st * 100;
            percent_tovara = MyBlanks.Okruglenie(per, 2);
            return percent_tovara;
        }
        public override string GetNameStrategyType()
        {
            return "Frank";
        }
        public override void ShowIndividualSettingsDialog()
        {
            
        }
    }
}
