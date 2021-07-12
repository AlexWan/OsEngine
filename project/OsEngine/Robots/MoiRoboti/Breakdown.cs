using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OsEngine.Robots.MoiRoboti
{
    public class Breakdown : BotPanel, INotifyPropertyChanged  // типа класс модель 
    {
        private BotTabSimple _tab; // поле хранения вкладки робота 

        public decimal _price; // поле хранения цены
        public decimal Price   // свойство цены
        {
            get => _price;
            set => Set(ref _price, value);
        }

        public Breakdown(string name, StartProgram startProgram) : base(name, startProgram) // конструктор
        {
            TabCreate(BotTabType.Simple);  // создание простой вкладки
            _tab = TabsSimple[0]; // записываем первую вкладку в поле
            _tab.BestBidAskChangeEvent += _tab_BestBidAskChangeEvent;
        }

        public void _tab_BestBidAskChangeEvent(decimal arg1, decimal arg2) // событие обновления лучшей цены
        {
            Price = _tab.MarketDepth.Bids[0].Price;
        }

        public override string GetNameStrategyType()
        {
            return "Breakdown";
        }

        public override void ShowIndividualSettingsDialog()
        {
            Breakdown_Ui ui = new Breakdown_Ui(this);
            ui.Show();
        }

 // реализация интерфейса PropertyChanged-событий изменения свойств

        public event PropertyChangedEventHandler PropertyChanged; // событие изменения свойств
   
        protected void СallUpdate(string name)  // сигнализирует об изменении свойств
        {
               PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
 
        protected void Set<T>(ref T field, T value, [CallerMemberName] string name = "") // сверяет значения любых типов данных
        {
            if (!field.Equals(value))
            {
                field = value;
                СallUpdate(name);
            }
        }

    }
}
        
 