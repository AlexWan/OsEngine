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
        MyBlanks blanks = new MyBlanks("Frank", StartProgram.IsOsTrader); // создание экземпляра класса MyBlanks
        private BotTabSimple _tab; // поле хранения вкладки робота 
        public decimal depo; // количество квотируемой в портфеле
        public decimal tovar; // количество товара  в портфеле


        public Frank(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);  // создание простой вкладки
            _tab = TabsSimple[0]; // записываем первую вкладку в поле
            kvot_name = "USDT";  // тут надо указать - инициализировать название квотируемой валюты
            tovar_name = "BTC"; // тут надо указать - инициализировать название товара 

            _tab.NewTickEvent += _tab_NewTickEvent; // событие новых тиков
            
        }

        private void _tab_NewTickEvent(Trade trade) // тики 
        {
            market_prise = blanks.price;
            decimal b = blanks.Balans_kvot();
            
            Console.WriteLine(" цена портфеля - " + Start_sum());
         }
        public decimal Start_sum() // расчет и запись начального состояния портфеля
        {
            Balans_kvot(kvot_name);
            Balans_tovara(tovar_name);
            decimal start_sum = depo + tovar* market_prise;
            return MyBlanks.Okruglenie (start_sum);
        }
        
        public decimal Balans_kvot(string kvot_name)   // запрос квотируемых средств в портфеле - название присваивается в kvot_name
        {
            List<PositionOnBoard> poses = _tab.Portfolio.GetPositionOnBoard();
            decimal vol_kvot = 0;
            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == kvot_name)
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
        public decimal Balans_tovara(string tovar_name)   // запрос торгуемых средств в портфеле (в BTC ) название присваивается в tovar_name
        {
            List<PositionOnBoard> poses = _tab.Portfolio.GetPositionOnBoard();
            decimal vol_instr = 0;
            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == tovar_name)
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

        public override string GetNameStrategyType()
        {
            return "Frank";
        }
        public override void ShowIndividualSettingsDialog()
        {
            
        }
    }
}
