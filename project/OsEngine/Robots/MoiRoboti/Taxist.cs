using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.MoiRoboti
{
    /// <summary>
    /// robot my 
    /// пробую сделать робота 
    /// </summary>
       
        public class Taxist : BotPanel
        {

            private BotTabSimple _tab;
            // это конструктор робота 
            public Taxist(string name, StartProgram startProgram)
            : base(name, startProgram)
            {
                TabCreate(BotTabType.Simple);
                _tab = TabsSimple[0];

                _tab.CandleFinishedEvent += TradeLogika; // подписка на сбытие завершения свечи
            }
            public int spred_vhoda = 20;
            
            private void TradeLogika(List<Candle> candles)   // Метод торговой логики 
            {
                // 1 Условие: если есть открытые позиции, закрываеи их и выходим из локики
                if (_tab.PositionsOpenAll != null && _tab.PositionsOpenAll.Count != 0)
                {
                    _tab.CloseAllAtMarket();
                    return;
                }
                // 2 условие:  Если закрытие последней свечи выше закрытия преддыдущей на спред
                //, то покупаем по рынку
                if (candles[candles.Count - 1].Close + spred_vhoda > candles[candles.Count - 2].Close)
                {
                    _tab.BuyAtMarket(0.0012m);

                }
                // 3 условие: если  последня свеча ниже предыдущей на спред продаем все по маркету
                if (candles[candles.Count - 1].Close + spred_vhoda < candles[candles.Count - 2].Close)
                {
                    _tab.SellAtMarket(0.0012m);
                }
            }
           
            public override string GetNameStrategyType()
            {
                return "Taxist";
            }


            // метод преропределения настроек бота 
            public override void ShowIndividualSettingsDialog()
            {
                // Тут будут настройки торговли бота
            }
        }
 }

