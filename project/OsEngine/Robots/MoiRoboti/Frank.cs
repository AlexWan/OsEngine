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
        public decimal market_price; // рыночная стоимость товара
        public string kvot_name ; // название квотируемой валюты - инструмента
        public string tovar_name; // название базовая валюты - товара
        // private MyBlanks blanks = new MyBlanks("Frank", StartProgram.IsOsTrader); // создание экземпляра класса MyBlanks
        private BotTabSimple _tab; // поле хранения вкладки робота 
        public decimal percent_tovara; // поле хранения % товара 
        public decimal portfolio_sum; // поле хранения суммы в портфеле 
        public decimal start_sum; // поле хранения суммы на старт работы 
        public decimal start_price; // поле хранения цены товара на старт работы 
        public decimal depo; // количество квотируемой в портфеле
        public decimal tovar; // количество товара  в портфеле
        public decimal min_lot; // поле хранящее величину минимального лота для биржи
        bool start_metod_vkl; // поле переключения состояния метода старт 
        bool sav_profit_metod_vkl; // поле переключения состояния метода  сохранения профита
        bool piramid_metod_vkl; // поле переключения состояния метода  пирамида 
        public decimal _kom; // поле хранения значения комиссия биржи 

        private StrategyParameterBool vkl_Robota; // поле включения бота 
        private StrategyParameterDecimal velich_usrednen; // величина усреднения
        private StrategyParameterDecimal deltaUsredn;   //на сколько ниже осуществлять усреднение
        private StrategyParameterInt start_per_depo; // какую часть депозита использовать при старте робота в % 
        private StrategyParameterDecimal min_sum;    //  минимальная сумма возможного ордера на бирже
        private StrategyParameterDecimal do_piram; // сколько пропустить да пирамиды
        private StrategyParameterDecimal slippage; // величина проскальзывание при установки ордеров 
        private StrategyParameterInt profit;       // расстояние до профита тейкпрофита
        private StrategyParameterDecimal komis_birgi; // комиссия биржи в %
        private StrategyParameterBool uchet_blok_sred_vkl; // учет блокированных средств в портфеле


        public Frank(string name, StartProgram startProgram) : base(name, startProgram) // конструктор
        {
            TabCreate(BotTabType.Simple);  // создание простой вкладки
            _tab = TabsSimple[0]; // записываем первую вкладку в поле
            kvot_name = "USDT";  // тут надо указать - инициализировать название квотируемой валюты (деньги)
            tovar_name = "BTC"; // тут надо указать - инициализировать название товара
            

            vkl_Robota = CreateParameter("РОБОТ Включен?", false);
            slippage = CreateParameter("Велич. проскаль.у ордеров", 5m, 1m, 200m, 5m);
            profit = CreateParameter("ТЭЙКПРОФИТ от рынка На ", 15, 5, 50, 5);
            velich_usrednen = CreateParameter("Усред.уваелич в раз ", 0.1m, 0.1m, 0.5m, 0.1m);
            do_piram = CreateParameter(" РАСТ. до Пирамиды", 20m, 5m, 200m, 5m);
            deltaUsredn = CreateParameter("УСРЕДнять через", 20m, 5m, 50m, 5m);
            start_per_depo = CreateParameter("Начинать с ? % депо)", 5, 5, 20, 5);
            min_sum = CreateParameter("МИН сумма орд.на бирже($)", 10.1m, 10.1m, 10.1m, 10.1m);
            komis_birgi = CreateParameter("КОМ биржи в %", 0.2m, 0, 0.1m, 0.1m);
            uchet_blok_sred_vkl = CreateParameter("Учитывать блокир. средства?", false);

            _tab.BestBidAskChangeEvent += _tab_BestBidAskChangeEvent; // событие изменения лучших цен
            _tab.OrderUpdateEvent += _tab_OrderUpdateEvent; // событие обновления ордеров 
            _tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;
            _tab.PositionClosingSuccesEvent += _tab_PositionClosingSuccesEvent;
 
        } // конструктор
        private void _tab_PositionClosingSuccesEvent(Position position)
        {
            Price_kon_trade(); // перепроверяем цену последних сделок
            Percent_tovara(); // смотрим процент купленного товара 
            Switching_mode(); // смотрим условия переключения режимов работы 
            Portfolio_sum(); // сумма в портфеле 
            Console.WriteLine("СРАБОТАЛО Событие закрытия позиции");
        } // Событие закрытия позиции
        private void _tab_MarketDepthUpdateEvent(MarketDepth marketDepth) // событие стакана
        {
            market_price = _tab.PriceCenterMarketDepth;
        }
        private void _tab_OrderUpdateEvent(Order order) // событие обновления ордера 
        {
            Price_kon_trade(); // перепроверяем цену последних сделок
            Percent_tovara(); // смотрим процент купленного товара 
            Switching_mode(); // смотрим условия переключения режимов работы 
            Portfolio_sum(); // сумма в портфеле 
            Console.WriteLine(" Событие обновления ордера!");
        }
        private void _tab_BestBidAskChangeEvent(decimal bid, decimal ask) //  ЛОГИКА тут (в событие изменения лучших цен)
        {
            Switching_mode();
            List<Position> positions = _tab.PositionsOpenAll;
            if (vkl_Robota.ValueBool == false)
            {
                return;
            }
            if (positions.Count != 0)
            {
                decimal q = _tab.PositionsLast.EntryPrice;
                if (q - _kom > market_price)
                {
                    Usrednenie();
                }
                if (q - _kom < market_price)
                {
                    Save_profit();
                    Piramida();
                }
            }
            else if (positions.Count == 0)
            {
                Portfolio_sum();
                Percent_tovara();
                Balans_tovara(tovar_name);
                Start();
            }
        }
        void Save_profit() // для выставления профита портфеля 
        {
            if (sav_profit_metod_vkl == false)
            {
                return;
            }
            Percent_birgi(); // расчет величины в пунктах  процента биржи 
            List<Position> positions = _tab.PositionsOpenAll;
            if (positions.Count != 0)
            {
                decimal zen = _tab.PositionsLast.EntryPrice;
                if (zen > 0)
                {
                    market_price = _tab.PriceCenterMarketDepth;
                    if (market_price > zen + profit.ValueInt + _kom + slippage.ValueDecimal * _tab.Securiti.PriceStep)
                    {
                        _tab.CloseAtTrailingStop(positions[0], _tab.PriceCenterMarketDepth - profit.ValueInt,
                            _tab.PriceCenterMarketDepth - profit.ValueInt - slippage.ValueDecimal * _tab.Securiti.PriceStep);

                        Console.WriteLine(" Включился трейлинг Прибыли по цене "
                            + (_tab.PriceCenterMarketDepth - profit.ValueInt - slippage.ValueDecimal * _tab.Securiti.PriceStep));
                    }
                }
            }
        }
        void Start() // метод с которого робот начинает работу  
        {
            if (start_metod_vkl == false) // для выключения метода  
            {
                return;
            }
            if (vkl_Robota.ValueBool == false)
            {
                return;
            }
            List<Position> positions = _tab.PositionsOpenAll;
            if (positions.Count != 0) // если есть открытые позиции старт невозможен !
            {
                Console.WriteLine(" есть открытые позиции старт невозможен ! " );
                return;
            }
            Lot(min_sum.ValueDecimal);
            start_sum = Portfolio_sum();
            Console.WriteLine(" записали сумму портфеля "+ start_sum);
            market_price = _tab.PriceCenterMarketDepth;
            start_price = market_price;
            Console.WriteLine(" записали стартовую цену  " + start_price);

            decimal vol_1 = MyBlanks.Okruglenie(Balans_kvot(kvot_name) / 100 * start_per_depo.ValueInt, 6);
            if (vol_1 > min_sum.ValueDecimal)
            {
                if (_tab.PositionsOpenAll.Count == 0)
                {
                    decimal w = MyBlanks.Okruglenie(vol_1 / market_price, 6);
                    _tab.BuyAtLimit(w, market_price);
                    Console.WriteLine(" Стартуем ордером на = " + MyBlanks.Okruglenie(w * market_price, 6) + " $ по цене " + market_price);

                    Price_kon_trade(); // перепроверяем цену последних сделок
                    Thread.Sleep(1500);
                    Percent_tovara(); // смотрим процент купленного товара 
                }

            }
            if (positions.Count != 0) // первая позиция открылась, выключаем метод 
            {
                start_metod_vkl = false;
                Console.WriteLine(" выключили метод старт");
            } 
        } 
        void Switching_mode() // метод переключения режимов работы 
        {
            List<Position> positions = _tab.PositionsOpenAll;
            Percent_tovara();
            if (90 <= percent_tovara) //  режим выхода 
            {
                sav_profit_metod_vkl = true;
            }
            if ( 70 <= percent_tovara && percent_tovara < 90 ) //  режим фиксации прибыли
            {
                Console.WriteLine(" включился режим фиксации прибыли от 70 %");
                sav_profit_metod_vkl = true;
                profit.ValueInt = 10;
                Console.WriteLine(" до профита " + profit.ValueInt);
                do_piram.ValueDecimal = 60m;
                Console.WriteLine(" расстояние до пирамиды изменено на " + do_piram.ValueDecimal);
                deltaUsredn.ValueDecimal = 70m;
                Console.WriteLine("Расстояние до Усреднения теперь " + deltaUsredn.ValueDecimal);
            }
            if (50 <= percent_tovara && percent_tovara < 70) // режим разворота и ожидания прибыли
            {
                Console.WriteLine(" включился режим разворота и ожидания прибыли до 70 %");
                sav_profit_metod_vkl = true;
                Console.WriteLine(" включили метод выставления профита");
                piramid_metod_vkl = false;
                profit.ValueInt = 25;
                Console.WriteLine(" до профита " + profit.ValueInt);
                do_piram.ValueDecimal = 50m;
                Console.WriteLine(" расстояние до пирамиды изменено на " + do_piram.ValueDecimal);
                deltaUsredn.ValueDecimal = 50m;
                Console.WriteLine("Расстояние до Усреднения теперь " + deltaUsredn.ValueDecimal);
            }
            if (30 <= percent_tovara && percent_tovara < 50) // режим набора товара и ожидания прибыли
            {

                sav_profit_metod_vkl = true;
                piramid_metod_vkl = true;
                Console.WriteLine(" включился режим набора товара до 50%");
                profit.ValueInt = 15;
                Console.WriteLine(" до профита " + profit.ValueInt);
                do_piram.ValueDecimal = 30m;
                Console.WriteLine(" расстояние до пирамиды изменено на " + do_piram.ValueDecimal);
                deltaUsredn.ValueDecimal = 30m;
                Console.WriteLine("Расстояние до Усреднения теперь " + deltaUsredn.ValueDecimal);
            }
            if (start_per_depo.ValueInt <= percent_tovara && percent_tovara < 30) // режим набора товара
            {
                sav_profit_metod_vkl = false;
                piramid_metod_vkl = true;
                Console.WriteLine(" включился режим набора товара до 30%");
            }
            if (1 >= percent_tovara) // режим старт, нет товара и позиций, запускаем метод старт
            {
                Console.WriteLine(" включен режим старт");
                sav_profit_metod_vkl = false;
                Console.WriteLine(" ВЫКЛючили метод выставления профита");
                piramid_metod_vkl = true;
                do_piram.ValueDecimal = 5m;
                Console.WriteLine(" расстояние до пирамиды "+ do_piram.ValueDecimal);
                deltaUsredn.ValueDecimal = 5m;
                Console.WriteLine("Расстояние до Усреднения "+ deltaUsredn.ValueDecimal);
                if (positions.Count == 0)
                {
                    Console.WriteLine("нет товара и открытых позиций, разрешаем метод старт - ");
                    start_metod_vkl = true; 
                    Start();
                }
            }
        }
        void Usrednenie() // усреднение позиций при снижении рынка 
        {
            List<Position> positions = _tab.PositionsOpenAll;
            decimal per = Percent_birgi();
            Price_kon_trade();
            Lot(min_sum.ValueDecimal);
            Thread.Sleep(1000);
            decimal z = Price_kon_trade();
            if (z > market_price + deltaUsredn.ValueDecimal + per)
            {
                min_lot = Lot(min_sum.ValueDecimal);
                Balans_kvot(kvot_name);
                Balans_tovara(tovar_name);
                decimal v = VolumForUsred();
                if (v > min_lot)
                {
                    if (_tab.PositionsLast.MyTrades.Count != 0)
                    {
                        Price_kon_trade();
                        if (positions[0].State != PositionStateType.Opening )
                        {
                            _tab.BuyAtMarketToPosition(positions[0], MyBlanks.Okruglenie(v, 6));
                        }
                    }
                    Price_kon_trade(); // перепроверяем цену последних сделок
                    Thread.Sleep(1500);
                    Percent_tovara(); // смотрим процент купленного товара 
                    Console.WriteLine("Усреднились НА - " + v * _tab.PriceBestAsk + " $");
                }
            }
        }
        void Piramida() // докупаем в позицию 
        {
            Percent_birgi();
            Lot(min_sum.ValueDecimal);
            VolumForPiramid();
            if (piramid_metod_vkl == false)
            {
                return;
            }
            List<Position> positions = _tab.PositionsOpenAll;
            
            if (positions.Count != 0) 
            {
                decimal zen = _tab.PositionsLast.EntryPrice;
                if (market_price > zen + _kom + do_piram.ValueDecimal)
                {
                    decimal vol = VolumForPiramid();
                    if (vol >=min_lot)
                    {
                        if (positions[0].State != PositionStateType.Opening || positions[0].State == PositionStateType.Open)
                        {
                            _tab.BuyAtMarketToPosition(positions[0], MyBlanks.Okruglenie(vol, 6));
                            Console.WriteLine(" Пирамида- докупили НА - " + MyBlanks.Okruglenie(vol * market_price, 6) + " $");
                            Price_kon_trade(); // перепроверяем цену последних сделок
                            Percent_tovara(); // смотрим процент купленного товара 
                        }

                    }
                }
            }
        }
        decimal Price_kon_trade()  // получает значение цены последнего трейда, если его нет возвращает цену рынка 
        {
            List<Position> positions = _tab.PositionsOpenAll;
            if (positions.Count ==0)
            {
                return _tab.PriceCenterMarketDepth;
            }
            if (positions.Count != 0)
            {
                if (_tab.PositionsLast.MyTrades.Count != 0)
                {
                    int asd = _tab.PositionsLast.MyTrades.Count;
                    return _tab.PositionsLast.MyTrades[asd - 1].Price;
                }
            }
            return _tab.PriceCenterMarketDepth; 
        }
        decimal Balans_kvot(string kvot_name)   // запрос квотируемых  средств (денег) в портфеле - название присваивается в kvot_name
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
                if (uchet_blok_sred_vkl.ValueBool == true)
                {
                    depo = vol_kvot + vol_kvot_blok;
                }
                else depo = vol_kvot;
            }

            Console.WriteLine(" Депо сейчас  " + MyBlanks.Okruglenie(depo,3) + " $");
            return depo;
        }
        decimal Balans_tovara(string tovar_name)   // запрос торгуемых средств в портфеле (в BTC ) название присваивается в tovar_name
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
                if (uchet_blok_sred_vkl.ValueBool == true) // 
                {
                    tovar = vol_instr + vol_instr_blok;
                }
                if (uchet_blok_sred_vkl.ValueBool == false)
                {
                    tovar = vol_instr;
                }
                
            }
            Console.WriteLine(" Баланс товара =  " + MyBlanks.Okruglenie ( tovar,6) + " BTC На  " + MyBlanks.Okruglenie(tovar * market_price, 2) + "$");
            return tovar;
        }
        decimal Portfolio_sum() // расчет начального состояния портфеля по торгуемой паре
        {
            Balans_kvot(kvot_name);
            Balans_tovara(tovar_name);
            portfolio_sum = MyBlanks.Okruglenie(depo + tovar * market_price, 2);
            return portfolio_sum;
        }
        decimal Percent_tovara() // расчет % объема купленного товара в портфеле 
        {
            decimal st = Portfolio_sum();
            decimal kv = Balans_kvot(kvot_name);
            decimal rasxod = st - kv;
            decimal per = rasxod / st * 100;
            percent_tovara = MyBlanks.Okruglenie(per, 2);
            Console.WriteLine(" процент товара сейчас "+ percent_tovara);
            return percent_tovara;
        }
        decimal VolumForUsred() // рассчитывает объем для усреднения покупок 
        {
            Balans_kvot(kvot_name);
            market_price = _tab.PriceCenterMarketDepth;
            decimal uge = _tab.PositionsLast.MaxVolume; // максимальный объем в позиции
            decimal dob = uge * velich_usrednen.ValueDecimal; // добавляем объема 
            decimal vol = uge + dob;
            if (depo/market_price <= vol)
            {
                decimal d1 = depo / market_price / 2;
                if (d1> min_lot)
                {
                    return d1;
                }
                return min_lot;
            }
            else return vol;
        }
        decimal VolumForPiramid() // рассчитывает объем для пирамиды 
        {
            market_price = _tab.PriceCenterMarketDepth;
            Percent_tovara();
            Lot(min_sum.ValueDecimal);
            if (30 <= percent_tovara && percent_tovara < 70) // режим набора товара и ожидания прибыли
            {
                Balans_kvot(kvot_name);
                market_price = _tab.PriceCenterMarketDepth;
                decimal vol = depo / 100 * start_per_depo.ValueInt;
                if (vol <= min_sum.ValueDecimal)
                {
                    return min_lot;
                }
                return vol / market_price;
            }
            if (start_per_depo.ValueInt <= percent_tovara && percent_tovara < 30) // режим набора товара
            {
                decimal uge = _tab.PositionsLast.MaxVolume; // максимальный объем в позиции
                decimal vol = uge / 2;
                if (vol <= min_lot)
                {
                    return min_lot;
                }
                else return vol;
            }
            return min_lot ;
        }
        public decimal Lot(decimal min_sum) // расчет минимального лота 
        {
 // костыль 
            market_price = 1m; 
            market_price = _tab.PriceCenterMarketDepth;
            min_lot = MyBlanks.Okruglenie(min_sum / market_price, 6);
            return min_lot;
        }
        public decimal Percent_birgi() // вычисление % биржи в пунктах для учета в расчетах выставления ордеров 
        {
            market_price = _tab.PriceCenterMarketDepth;
            return _kom = market_price / 100 * komis_birgi.ValueDecimal;
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
