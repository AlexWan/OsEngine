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
    public class Frank_2 : BotPanel
    {
        public string kvot_name ; // название квотируемой валюты - инструмента
        public string tovar_name; // название базовая валюты - товара
        // private MyBlanks blanks = new MyBlanks("Frank", StartProgram.IsOsTrader); // создание экземпляра класса MyBlanks
        private BotTabSimple _tab; // поле хранения вкладки робота 
        decimal percent_tovara; // поле хранения % товара 
        decimal portfolio_sum; // поле хранения суммы в портфеле 
        decimal start_sum; // поле хранения суммы на старт работы 
        decimal temp_profit; // поле хранения профита портфеля
        decimal depo; // количество квотируемой в портфеле
        decimal tovar; // количество товара  в портфеле
        decimal min_lot; // поле хранящее величину минимального лота для биржи
        bool start_metod_vkl; // поле переключения состояния метода старт 
        bool sav_profit_metod_vkl; // поле переключения состояния метода  сохранения профита
        bool block_in; // поле переключения блокировки на усреднение и старт 
        decimal _kom; // поле хранения значения комиссия биржи 
        decimal _temp_greed; // поле для хранения временного значения жадности 
        decimal _do_piram; // поле для хранения временного
        decimal _deltaUsredn; // поле для хранения временного
        decimal _tmpArarm; // временное значение аварийного объема продаж
        decimal _do_profit_temp; // поле хранения расстояния до профит ордера 
        public Volume _vol;
        public MovingAverage _ma_short;     // поле хранения индикатора МА
        public MovingAverage _ma_long;     // поле хранения индикатора МА

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
        private StrategyParameterDecimal greed; // жадность - процент включения для получения прибыли 
        private StrategyParameterDecimal greed_max; // максимальная жадность - процент включения для получения прибыли 
        private StrategyParameterDecimal pir_of; // когда выключать пирамиду
        private StrategyParameterBool vkl_piramid; // поле включения метода пирамида  
        private StrategyParameterDecimal prof_on; // когда включать профит
        private StrategyParameterDecimal ckok_profit_stop; // на каком расстоянии выставить стоп 
        private StrategyParameterBool vkl_stopa; // поле включения стопа 
        private StrategyParameterInt n_min; // количество минут для метода подсчета прошедшего времени 
        private StrategyParameterInt volum_alarm;  // величина объема при достижении которого закроются убытки 
        private StrategyParameterDecimal kpit_sum; // 
        private StrategyParameterBool vkl_met_fix_loss; // поле включения бота 
        private StrategyParameterDecimal Itogo; // итог работы
        private StrategyParameterInt n_min_lesion; // через сколько минут закрывать убыточную сделку 
        private StrategyParameterInt sprint; // объем торгов достигнув который включится  быстрый режим 
        private StrategyParameterDecimal turbo_shift ; // величина сдвига в турбо режиме 
        private StrategyParameterInt alarn_persent; // с каким использованным % депо можно закрывать убыточную сделку 

        public Frank_2(string name, StartProgram startProgram) : base(name, startProgram) // конструктор
        {
            TabCreate(BotTabType.Simple);  // создание простой вкладки
            _tab = TabsSimple[0]; // записываем первую вкладку в поле
            kvot_name = "USDT";  // тут надо указать - инициализировать название квотируемой валюты (деньги)
            tovar_name = "BTC"; // тут надо указать - инициализировать название товара

            vkl_Robota = CreateParameter("РОБОТ Включен?", false);
            slippage = CreateParameter("Велич. проскаль.у ордеров", 1m, 1m, 200m, 5m);
            profit = CreateParameter("ТЭЙКПРОФИТ от рынка На ", 20, 5, 50, 5);
            greed = CreateParameter("Сколько минимум прибыли ожидать в сделке на каждые 100$ ", 0.55m, 0.25m, 5.5m, 0.05m); // жадность
            greed_max = CreateParameter("Сколько Максимум прибыли ожидать в сделке на каждые 100$ ", 1.25m, 0.25m, 5.5m, 0.05m); // жадность
            velich_usrednen = CreateParameter("Усред.уваелич в раз ", 0.01m, 0.01m, 0.5m, 0.01m);
            do_piram = CreateParameter(" РАСТ. до Пирамиды", 20m, 5m, 200m, 5m);
            turbo_shift = CreateParameter(" Turbo добавлять", 50m, 5m, 200m, 5m);
            pir_of = CreateParameter(" ОТКлючить  Пирамиду при % товара", 10m, 5m, 100m, 5m);
            vkl_piramid = CreateParameter("Пирамида включена?", false);
            prof_on = CreateParameter("Забирать профит с % ", 10m, 5m, 100m, 5m);
            ckok_profit_stop = CreateParameter("Сколько ПРИБЫЛИ закрывать стоп ордером? ", 0.5m, 0.01m, 10m, 0.01m);
            vkl_stopa = CreateParameter("Стоп Включен?", true);
            deltaUsredn = CreateParameter("УСРЕДнять через", 20m, 5m, 50m, 5m);
            start_per_depo = CreateParameter("Начинать с ? % депо)", 5, 5, 20, 5);
            min_sum = CreateParameter("МИН сумма орд.на бирже($)", 10.1m, 10.1m, 10.1m, 10.1m);
            komis_birgi = CreateParameter("КОМ биржи в %", 0.2m, 0, 0.1m, 0.1m);
            uchet_blok_sred_vkl = CreateParameter("Учитывать блокир. средства?", false);
            n_min = CreateParameter("зависание снимать через N минут ", 1, 1, 20, 1);
            volum_alarm = CreateParameter("АВАРИЙНЫЙ ОБЪЕМ ПРОДАЖ", 555, 150, 1000, 50);
            kpit_sum = CreateParameter("Закр. Позицию с УБЫТОКОМ от ", -1m, 10.1m, 10.1m, 10.1m);
            vkl_met_fix_loss = CreateParameter("Закрываться в убыток по времени? ", true);
            Itogo = CreateParameter("Итого Заработано ", 0m, 0m, 0m, 0m);
            n_min_lesion = CreateParameter("Закрывать убыток через N минут ",120, 30, 360, 20);
            sprint = CreateParameter("Большие объемы это когда больше = ", 101, 50, 200, 50);
            alarn_persent = CreateParameter("% купленного можно сливать = ", 5, 50, 100, 1);

             block_in = true; // инициализируем значение блокировки 

            _ma_short = new MovingAverage(name + "Ma5", false);
            _ma_short = (MovingAverage)_tab.CreateCandleIndicator(_ma_short, "Prime");
            _ma_short.Lenght = 5; // присвоение значения 
            _ma_short.Save();

            _ma_long = new MovingAverage(name + "Ma20", false);
            _ma_long = (MovingAverage)_tab.CreateCandleIndicator(_ma_long, "Prime");
            _ma_long.Lenght = 20; // присвоение значения 
            _ma_long.Save();

            _vol = new Volume(name + "Volum", false);
            _vol = (Volume)_tab.CreateCandleIndicator(_vol, "nameArea");
            _vol.Save();

            _tab.BestBidAskChangeEvent += _tab_BestBidAskChangeEvent; // событие изменения лучших цен
            _tab.OrderUpdateEvent += _tab_OrderUpdateEvent; // событие обновления ордеров 
            _tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent; // пришел новый стакан
            _tab.PositionNetVolumeChangeEvent += _tab_PositionNetVolumeChangeEvent; // у позиции изменился объем 
            _tab.PositionClosingSuccesEvent += _tab_PositionClosingSuccesEvent; // позиция успешно закрыта 
            _tab.NewTickEvent += _tab_NewTickEvent; // событие новых тиков
            
        } // конструктор

        private void _tab_PositionNetVolumeChangeEvent(Position position) // у позиции изменился объем 
        {
            Percent_tovara(); // смотрим процент купленного товара 
            Switching_mode(); // смотрим условия переключения режимов работы 
            Console.WriteLine(" В позиции изменился объем ");
        }

        public decimal market_price; // рыночная стоимость товара
        public DateTime real_time;
        private DateTime dateTrade; // время трейда
        decimal bid_vol_tr;  // объем покупок
        decimal ask_vol_tr; // объем продаж
        decimal all_volum_trade_min; //все объемы за N минуту
        private void _tab_NewTickEvent(Trade trade)  // событие новых тиков + счет объема торгов
        {
            market_price = trade.Price;
            real_time = trade.Time; // время последнего трейда 

            DateTime time_add_n_min;
            time_add_n_min = dateTrade.AddMinutes(n_min.ValueInt);
            if (trade.Time < time_add_n_min)
            {
                if (trade.Side == Side.Buy)
                {
                    decimal b = trade.Volume;
                    bid_vol_tr = bid_vol_tr + b;
                }
                if (trade.Side == Side.Sell)
                {
                    decimal a = trade.Volume;
                    ask_vol_tr = ask_vol_tr + a;
                }
                all_volum_trade_min = bid_vol_tr + ask_vol_tr;
            }
            else
            {
                dateTrade = trade.Time;
                all_volum_trade_min = 0;
                bid_vol_tr = 0;
                ask_vol_tr = 0;
            }
        }

        private void _tab_PositionClosingSuccesEvent(Position position) // Событие закрытия позиции (блокирование  старта)
        {
            Console.WriteLine("произошло Событие закрытия позиции " );
            block_in = true;
            Console.WriteLine(" Закрыли позицию, старт ожидает машку ");
        }

        public decimal volum_ma_short; // последние значение индикатора MA  
        public decimal volum_ma_long; // последние значение индикатора MA  
        public decimal bol_Up; // последние значение верхнего уровня   болинждера  
        public decimal bol_Down; // последние значение нижнего уровня  болинждера 

        private void _tab_MarketDepthUpdateEvent(MarketDepth marketDepth) // событие стакана
        {
            market_price = _tab.PriceCenterMarketDepth;
            
            volum_ma_short = _ma_short.Values[_ma_short.Values.Count - 1]; // записывается значение индикатора MA 
            volum_ma_long = _ma_long.Values[_ma_long.Values.Count - 1]; // записывается значение индикатора MA 

            if (volum_ma_short < market_price)
            {
                block_in = false;
                Console.WriteLine("Значение МА допустимое, блокировка старта выключена ");
            }
        }

        private void _tab_OrderUpdateEvent(Order order) // событие обновления ордера 
        {
            Price_kon_trade(); // перепроверяем цену последних сделок
            Percent_tovara(); // смотрим процент купленного товара 
            Portfolio_sum(); // сумма в портфеле 
            Console.WriteLine(" Событие обновления ордера!");
        }

        private void _tab_BestBidAskChangeEvent(decimal bid, decimal ask) //  ЛОГИКА тут (в событии изменения лучших цен)
        {
            MiddleMarketVolumeCalculat();
            Console.WriteLine(" СРЕДНИЙ объем торговли сейчас - " + middle_market_vol); 
            List<Position> positions = _tab.PositionsOpenAll;
            if (positions.Count != 0)
            {
                Switching_mode(); // смотрим условия переключения режимов работы 
                decimal q = _tab.PositionsLast.EntryPrice;
                if (q - _kom > market_price )
                {
                    Fix_loss();
                    Usrednenie();
                }
                if (q - _kom < market_price )
                {
                    Piramida();
                    Save_profit();
                    StopLoss();
                }
            }
            if (positions.Count == 0)
            {
                Percent_tovara();
                Balans_tovara(tovar_name);

                if (1 >= percent_tovara) // режим старт, нет товара и позиций, запускаем метод старт
                {
                    Console.WriteLine("нет товара и открытых позиций, разрешаем метод старт - ");
                    start_metod_vkl = true;
                    Start();
                }
            }
        }

        decimal Payment_profit() //  рассчитывает прибыльность (баланса)
        {
            decimal start = start_sum;
            decimal end = Portfolio_sum();
            temp_profit = start - end;
            Console.WriteLine(" прибыльность сделки равна = " + temp_profit);
            return temp_profit;
        }
        void Restart_settings() // перезапись настроек для старта 
        {
            Console.WriteLine(" ЭТО Режим перезапись настроек для старта");
            start_metod_vkl = true;
            Console.WriteLine(" Режим старта разрешен");
            _do_piram = do_piram.ValueDecimal;
            Console.WriteLine(" расстояние до пирамиды " + _do_piram);
            _deltaUsredn = deltaUsredn.ValueDecimal;
            Console.WriteLine("Расстояние до Усреднения " + _deltaUsredn);
            _temp_greed = greed.ValueDecimal;
            Console.WriteLine("Жадность теперь " + _temp_greed);
            sav_profit_metod_vkl = false;
            Console.WriteLine(" ВЫКЛючили метод выставления профита");
            vkl_piramid.ValueBool = true;
            Console.WriteLine(" Режим пирамиды включен");
        }
        void Save_profit() // для выставления профита портфеля 
        {
            if (sav_profit_metod_vkl == false)
            {
                return;
            }
            List<Position> positions = _tab.PositionsOpenAll;
            
            if (positions.Count != 0)
            {
                decimal g = Greed(); // вычисляет жадность
                decimal zn = _tab.PositionsLast.ProfitPortfolioPunkt; // смотрим прибыльность
                Percent_birgi();
                market_price = _tab.PriceCenterMarketDepth;
                decimal pr_poz = _tab.PositionsLast.EntryPrice;

                if (pr_poz < market_price - _kom/2 - _do_profit_temp) // цена открытия позиции ниже рынка -комиссия - расстояние от профита
                {
                    if (zn > g) // прибыль больше жадности 
                    {
                        _tab.CloseAtTrailingStop(positions[0], market_price - _do_profit_temp,
                        market_price - _do_profit_temp - slippage.ValueDecimal * _tab.Securiti.PriceStep);
                        Console.WriteLine(" прибыль больше жадности  - включился трейлинг Прибыли от " + _temp_greed + " $ "
                        + (_tab.PriceCenterMarketDepth - _do_profit_temp - slippage.ValueDecimal * _tab.Securiti.PriceStep));
                    }
                }
                /*else if (zn > g && prof_on.ValueDecimal <= percent_tovara) // если товара уже больше значения prof_on  и есть прибыль в сделке - выставляем трейлик стоп прибыли  
                {
                    _tab.CloseAtTrailingStop(positions[0], market_price - _kom / 2 - _do_profit_temp,
                    market_price - _kom / 2 - _do_profit_temp - slippage.ValueDecimal * _tab.Securiti.PriceStep);
                    Console.WriteLine(" сработал else if - товара больше " + prof_on.ValueDecimal + " %, Включили трейлинг Прибыли от _gde_stop_tmp  ");
                }*/
            }
        }
        void StopLoss() // ограничение   убытков 
        {
            if (prof_on.ValueDecimal <= percent_tovara)
            {
                sav_profit_metod_vkl = true;
            }
            if (vkl_stopa.ValueBool == false) // если выключен стоп 
            {
                return; // выйдет из метода
            }
            List<Position> positions = _tab.PositionsOpenAll;
            if (positions.Count != 0)
            {
                decimal zn = _tab.PositionsLast.ProfitPortfolioPunkt; // смотрим прибыльность
                Percent_birgi();
                market_price = _tab.PriceCenterMarketDepth;
                if (zn > ckok_profit_stop.ValueDecimal ) // если есть прибыль в сделке - выставляем стоп прибыли  
                {
                    sav_profit_metod_vkl = true;
                    _tab.CloseAtTrailingStop(positions[0], market_price - _do_profit_temp + slippage.ValueDecimal * _tab.Securiti.PriceStep, market_price - _do_profit_temp);
                    Console.WriteLine(" Прибыль больше " + ckok_profit_stop.ValueDecimal + " $ , включил выставление профит ордера ");
                }
            }
        }
        void Fix_loss() // метод выхода из позиции с убытком 
        {
            List<Position> positions = _tab.PositionsOpenAll;
            if (positions.Count != 0)
            {
                if (percent_tovara > alarn_persent.ValueInt && // если товара куплено больше % alarn_persent.ValueInt
                    ask_vol_tr > _tmpArarm) // если объемы продаж выше аварийных 
                {
                    _tab.CloseAllAtMarket();
                    Console.WriteLine(" АВАРийные объемы продаж были  " + ask_vol_tr);
                    Console.WriteLine(" Закрыли сделку! товара было на  " + percent_tovara + " %");
                    block_in = true; // включаем блокировку усреднения и старта 
                    Thread.Sleep(1500);
                    Payment_profit();
                    Console.WriteLine(" АВАРИЯ! закрылись с убытком " + temp_profit);
                }
                if (ask_vol_tr > _tmpArarm) // если объемы продаж выше аварийных 
                {
                    block_in = true; // включаем блокировку усреднения и старта 
                    Console.WriteLine("АВАРийный объем продаж!!!! " + ask_vol_tr + " БЛОКИРОВАЛИ усреднение и старт робота ");
                }
                if (percent_tovara > alarn_persent.ValueInt && // если товара куплено больше % alarn_persent.ValueInt
                   ask_vol_tr > _tmpArarm) // если объемы продаж выше аварийных 
                {
                    _tab.CloseAllAtMarket(); // закрываемся с убытком 
                    Payment_profit();
                    Console.WriteLine("Закрылись с убытком!!! " + temp_profit);
                    Console.WriteLine("АВАРийный объем продаж " + ask_vol_tr + " выше " + _tmpArarm);
                    Console.WriteLine(" товара было на  " + percent_tovara + " %");
                    Thread.Sleep(1500);
                }
                int ups = _tab.PositionsLast.MyTrades.Count;
                if (ups <= 1)
                {
                    return;
                }
                if (vkl_met_fix_loss.ValueBool == false)
                {
                    return;
                }
                decimal loss = _tab.PositionsLast.ProfitPortfolioPunkt; // смотрим есть ли убыток
                DateTime last_tred_time = _tab.PositionsLast.MyTrades[ups - 1].Time; // берем время последнего трейда позиции 
                DateTime time_expectation = last_tred_time.AddMinutes(n_min_lesion.ValueInt); // добавляем время ожидания 
                if (time_expectation < real_time &&          // если вышло время ожидания
                    percent_tovara > alarn_persent.ValueInt &&      // если товара куплено > alarn_persent %
                   loss > kpit_sum.ValueDecimal &&      // если убыток меньше критического
                   loss < kpit_sum.ValueDecimal/2)      // но убыток больше  половины критического
                {
                    _tab.CloseAllAtMarket(); // тогда закрываемся 
                    Payment_profit();
                    Console.WriteLine("Закрылись с убытком!!! " + temp_profit);
                    Console.WriteLine("Время позиции в убытке дольше " + n_min_lesion.ValueInt + " минут");
                    Console.WriteLine(" товара было на  " + percent_tovara + " %");
                    Thread.Sleep(1500);
                } 
            }    
        }
        void Start() // метод с которого робот начинает работу  
        {
            if (start_metod_vkl == false) // для выключения метода  
            {
                return;
            }
            List<Position> positions = _tab.PositionsOpenAll;
            if (positions.Count != 0) // если есть открытые позиции старт невозможен !
            {
                Console.WriteLine(" есть открытые позиции старт невозможен ! " );
                return;
            }
            Restart_settings();  // перезапись настроек для старта 
            Thread.Sleep(300);
            Lot(min_sum.ValueDecimal);
            portfolio_sum = 0;
            Portfolio_sum(); // сумма в портфеле 
            start_sum = portfolio_sum; // запись старта портфеля 
            Console.WriteLine(" записали сумму портфеля "+ start_sum);
            decimal vol_start = MyBlanks.Okruglenie(Balans_kvot(kvot_name) / 100 * start_per_depo.ValueInt, 6);
            if (vol_start > min_sum.ValueDecimal)
            {
                if (vkl_Robota.ValueBool == false) // отключение робота 
                {
                    return;
                }
                if (block_in == true)
                {
                    Console.WriteLine("Включилась блокировка старта");
                    return;
                }
                if (_tab.PositionsOpenAll.Count == 0 // нет открытых позиций
                     && volum_ma_short < market_price      // рынок выше короткой машки
                     && volum_ma_long > market_price)   // рынок ниже длинной машки

                {
                    decimal w = MyBlanks.Okruglenie(vol_start / market_price, 6);
                    _tab.BuyAtLimit(w, market_price);
                    Console.WriteLine(" Стартуем ордером на = " + MyBlanks.Okruglenie(w * market_price, 6) + " $ по цене " + market_price);
                    Thread.Sleep(1500);
                    Price_kon_trade(); // перепроверяем цену последних сделок
                    Percent_tovara(); // смотрим процент купленного товара 
                }
            }
            else
            {
                Console.WriteLine(" Сумма для старта " + vol_start + " маловато, измени % входа или пополни баланс");
            }
            if (positions.Count != 0) // первая позиция открылась, выключаем метод 
            {
                start_metod_vkl = false;
                Console.WriteLine(" выключили метод старт");
            }
        }
        void Zavischie()  // Костыль!
        {
            List<Position> positions = _tab.PositionsOpenAll;
            if (positions.Count != 0)
            {
                if (positions[0].State == PositionStateType.Opening && positions[0].State != PositionStateType.Open)
                {
                    int asd = _tab.PositionsLast.MyTrades.Count;
                    if (asd <=1)
                    {
                        return;
                    }
                    DateTime tred_time = _tab.PositionsLast.MyTrades[asd - 1].Time;
                    DateTime time_add_n_min;
                    DateTime dateTrade = tred_time;  // время трейда
                    time_add_n_min = dateTrade.AddMinutes(n_min.ValueInt);
                    if (depo > min_sum.ValueDecimal)
                    {
                        Console.WriteLine(" Через "+ n_min.ValueInt + " минут в " + time_add_n_min + " купится минимальный лот  ");
                        if (time_add_n_min < real_time )
                        {
                            if (positions[0].State == PositionStateType.Opening && positions[0].State != PositionStateType.Open)
                            {
                                //_tab.BuyAtMarketToPosition(positions[0], MyBlanks.Okruglenie(min_lot, 6));
                                Console.WriteLine(" Покупаем минимальный лот, для снятия зависания позиции ");
                                //n_min.ValueInt = n_min.ValueInt + 1;
                                Thread.Sleep(1500);
                            }
                        }
                    }
                }
                if (positions[0].State != PositionStateType.Opening || positions[0].State == PositionStateType.Open)
                {
                    //n_min.ValueInt = 1;
                }
            }
        } // действие над зависшими ордерами
        decimal Greed() // вычисляет жадность :)
        {
            decimal v = _tab.PositionsLast.MaxVolume; // максимальный объем в позиции
            decimal c = v * market_price; // переводим в баксы (деньги)
            decimal g = c / 100 * _temp_greed; // вычисляем сколько денег ждать на эту сумму
            if (g <= 0)
            {
                return 1;
            }
            else return g;
        }
        void Switching_mode() // метод переключения режимов работы 
        {
            decimal greed_average = MyBlanks.Okruglenie(greed_max.ValueDecimal + greed.ValueDecimal / 2, 2); // расчет среднего значения жадности
            Percent_tovara();

             decimal g = Greed(); // вычисляет жадность
            Console.WriteLine(" Жадность g = " + MyBlanks.Okruglenie(g, 3));

            _do_profit_temp = MyBlanks.Okruglenie(profit.ValueInt / g, 2); // динамическое вычисление расстояния до профит ордера 
            Console.WriteLine(" До профит ордера расстояние = " + _do_profit_temp);

            if (pir_of.ValueDecimal <= percent_tovara) // место отключения режима пирамида
            {
                vkl_piramid.ValueBool = false;
                Console.WriteLine(" после набора " + pir_of.ValueDecimal + " %  отключили  режим набора пирамидой");
            }
            if (prof_on.ValueDecimal <= percent_tovara) // место включения режима профит
            {
                sav_profit_metod_vkl = true;
                Console.WriteLine(" после набора " + prof_on.ValueDecimal + " %  включили  режим  сохранения прибыли ");
            }
 
            if (middle_market_vol <= sprint.ValueInt) // режим обычной торговли 
            {
                Console.WriteLine(" РЕЖИМ Обычный ");

                _do_piram = do_piram.ValueDecimal * percent_tovara / 10;
                Console.WriteLine(" расстояние до пирамиды изменено на " + _do_piram);
                _deltaUsredn = deltaUsredn.ValueDecimal * percent_tovara / 10;
                Console.WriteLine("Расстояние до Усреднения теперь " + _deltaUsredn);

                _tmpArarm = volum_alarm.ValueInt;
                Console.WriteLine(" аварийный объем = " + _tmpArarm);
                if (70 <= percent_tovara && percent_tovara < 90) //  режим фиксации прибыли
                {
                    Console.WriteLine(" включился режим фиксации прибыли от 70 %");
                    _temp_greed = greed_max.ValueDecimal;
                    Console.WriteLine("Жадность теперь " + _temp_greed);
                }
                if (50 <= percent_tovara && percent_tovara < 70) // режим разворота и ожидания прибыли
                {
                    Console.WriteLine(" включился режим разворота и ожидания прибыли до 70 %");
                    _temp_greed = greed_average; //  средняя жадность 
                    Console.WriteLine("Жадность теперь " + _temp_greed);
                }
                if (prof_on.ValueDecimal <= percent_tovara && percent_tovara < 50) // режим набора товара и ожидания прибыли
                {
                    sav_profit_metod_vkl = true;
                    Console.WriteLine(" включился режим набора товара от " + prof_on.ValueDecimal + " до 50%");
                    _temp_greed = greed.ValueDecimal;
                    Console.WriteLine("Жадность теперь " + _temp_greed);
                }
                if (start_per_depo.ValueInt/2 <= percent_tovara && percent_tovara < prof_on.ValueDecimal) // режим набора товара
                {
                    Console.WriteLine(" включился режим набора товара до " + prof_on.ValueDecimal + " %");
                    _temp_greed = greed.ValueDecimal;
                    Console.WriteLine("Жадность теперь " + _temp_greed);
                }
            }    // настройки обычного режима 
            else //  настройки turbo режим- рынок с большими объемами 
            {
                Console.WriteLine(" СКОРОСТНОЙ РЕЖИМ Рынка");

                _do_piram = do_piram.ValueDecimal * percent_tovara / 10 + turbo_shift.ValueDecimal;
                Console.WriteLine(" расстояние до пирамиды изменено на " + _do_piram);
                _deltaUsredn = deltaUsredn.ValueDecimal * percent_tovara / 10 + turbo_shift.ValueDecimal;
                Console.WriteLine("Расстояние до Усреднения теперь " + _deltaUsredn);

                _tmpArarm = volum_alarm.ValueInt * 2; // аварийный объем увеличили в два раза 
                Console.WriteLine(" аварийный объем увеличили в два раза = "+ _tmpArarm);
                if (70 <= percent_tovara && percent_tovara < 90) //  режим фиксации прибыли
                {
                    Console.WriteLine(" включился режим фиксации прибыли от 70 %");
                    _temp_greed = greed_max.ValueDecimal + 0.25m;
                    Console.WriteLine("Жадность теперь " + _temp_greed);
                }
                if (50 <= percent_tovara && percent_tovara < 70) // режим разворота и ожидания прибыли
                {
                    Console.WriteLine(" включился режим разворота и ожидания прибыли до 70 %");
                    _temp_greed = greed_average + 0.15m; //  средняя жадность 
                    Console.WriteLine("Жадность теперь " + _temp_greed);
                }
                if (prof_on.ValueDecimal <= percent_tovara && percent_tovara < 50) // режим набора товара и ожидания прибыли
                {
                    sav_profit_metod_vkl = true;
                    Console.WriteLine(" включился режим набора товара от " + prof_on.ValueDecimal + " до 50%");
                    _temp_greed = greed.ValueDecimal + 0.1m;
                    Console.WriteLine("Жадность теперь " + _temp_greed);
                }
                if (start_per_depo.ValueInt/2 <= percent_tovara && percent_tovara < prof_on.ValueDecimal) // режим набора товара
                {
                    Console.WriteLine(" включился режим набора товара до " + prof_on.ValueDecimal + " %");
                    _temp_greed = greed.ValueDecimal;
                    Console.WriteLine("Жадность теперь " + _temp_greed);
                }
            }
        }
        void Usrednenie() // усреднение позиций при снижении рынка 
        {
            List<Position> positions = _tab.PositionsOpenAll;
            decimal per = Percent_birgi();
            Price_kon_trade();
            Lot(min_sum.ValueDecimal);
            decimal z = _tab.PositionsLast.EntryPrice;
            if (z > market_price + _deltaUsredn + per && volum_ma_short < market_price) // если цена ниже последнего трейда и выше машки 
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
            if (vkl_piramid.ValueBool == false)
            {
                return;
            }
            List<Position> positions = _tab.PositionsOpenAll;
            
            if (positions.Count != 0) 
            {
                decimal zen = _tab.PositionsLast.EntryPrice;
                decimal greed = Greed();
                if (greed < _tab.PositionsLast.ProfitPortfolioPunkt)
                {
                    zen = _tab.PositionsLast.EntryPrice;
                    if (market_price > zen + _kom/2 + _do_piram)
                    {
                        decimal vol = VolumForPiramid();
                        if (vol >= min_lot)
                        {
                            if (positions[0].State != PositionStateType.Opening || positions[0].State == PositionStateType.Open)
                            {
                                _tab.BuyAtMarketToPosition(positions[0], MyBlanks.Okruglenie(vol, 6));
                                Console.WriteLine(" Пирамида- докупили НА - " + MyBlanks.Okruglenie(vol * market_price, 6) + " $");
                                Thread.Sleep(1500);
                                zen = _tab.PositionsLast.EntryPrice;
                                Price_kon_trade(); // перепроверяем цену последних сделок
                                Percent_tovara(); // смотрим процент купленного товара 
                            }
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
                    break;
                }
            }
            if (vol_instr >= 0)
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
            portfolio_sum = MyBlanks.Okruglenie(depo + tovar * market_price, 3);
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
                decimal d1 = depo / market_price - 5* min_lot;
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

        void DelPosition ()
        {
            List<Position> positions = _tab.PositionsOpenAll;
            decimal q = _tab.PositionsLast.EntryPrice;
            if (q - _kom < market_price)
            {
                //_tab.CloseAllOrderInSystem();
                //_tab.CloseAllOrderToPosition(positions[positions.Count-1]);
                Console.WriteLine(" ОТОЗВАЛИ  ВСЕ ОТКРЫТЫЕ ордера роботом в системе ");
            }
        } // для удаления позиций 

        decimal middle_market_vol;
        void MiddleMarketVolumeCalculat() //  расчет объемов средних продаж на рынке 
        {
            decimal v_1 = 0;
            decimal v_2 = 0;
            decimal v_3 = 0;
            v_1 = _vol.Values[_vol.Values.Count - 2]; // складываем последние три уже завершенные свечи
            v_2 = _vol.Values[_vol.Values.Count - 3];
            v_3 = _vol.Values[_vol.Values.Count - 4];
            middle_market_vol = MyBlanks.Okruglenie((v_1 + v_2 + v_3) / 3, 1);
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
            return "Frank_2";
        }
        public override void ShowIndividualSettingsDialog()
        {
            
        }
    }
}
