using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Robots.MoiRoboti;

namespace OsEngine.Robots.MoiRoboti
{
    public class Setka : BotPanel
    {
        // парметры стартегии
        private StrategyParameterInt slippage; // величина проскальзывание при установки ордеров  
        private StrategyParameterBool vkl_Robota; // поле включения робота 
        private StrategyParameterInt volum_lot; // количество лотов для сделки
        private StrategyParameterInt profit; // профит в пунктах 
        private StrategyParameterDecimal min_lot; // профит в пунктах 

        // поле хранения вкладки
        private BotTabSimple _vkl; // поле хранения вкладки робота 

        public Setka(string name, StartProgram startProgram) : base(name, startProgram)  // это конструктор робота
        {
            TabCreate(BotTabType.Simple);
            _vkl = TabsSimple[0];
            slippage = CreateParameter("Величина проскальзывания", 5, 3, 50, 1);
            vkl_Robota = CreateParameter(" Включен/ Выключен", false);
            volum_lot = CreateParameter("Cколько лотов входить", 1, 1, 10, 1);
            profit = CreateParameter("Через сколько пунктов ставить профит", 5, 1, 100, 1);
            min_lot = CreateParameter("минимал допустимый лот для биржи", 10.1m, 10.1m, 10.1m, 0m);

            _vkl.CandleFinishedEvent += _vkl_CandleFinishedEvent;
            _vkl.PositionOpeningSuccesEvent += _vkl_PositionOpeningSuccesEvent; // событие открытия позиции

        }

        private void _vkl_PositionOpeningSuccesEvent(Position position)
        {

        }

        private void _vkl_CandleFinishedEvent(List<Candle> candles)
        {

        }

        public override string GetNameStrategyType()
        {
            return "Setka";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}
