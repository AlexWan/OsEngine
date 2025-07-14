/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;
using System.Threading;
using OsEngine.Language;

/* Description
TestBot for OsEngine.

Do not enable - robot for testing the opening and closing of orders.
*/

namespace OsEngine.Robots.AutoTestBots
{
    [Bot("TestBotOpenAndCanselOrders")] //We create an attribute so that we don't write anything in the Boot factory
    public class TestBotOpenAndCanselOrders : BotPanel
    {
        public TestBotOpenAndCanselOrders(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Description = OsLocalization.Description.DescriptionLabel2;

            if (startProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            StrategyParameterButton button = CreateParameterButton("Start send orders");
            button.UserClickOnButtonEvent += Button_UserClickOnButtonEvent;

            CountOrdersInSeries = CreateParameter("Orders count", 5, 5, 500, 1);
            Distance = CreateParameter("Distance from md %", 1m, 1, 50, 1);
            Delay = CreateParameter("Time delay action sec", 1, 1, 500, 1);
            Volume = CreateParameter("Volume", 1m, 1, 50, 1);

            Thread worker = new Thread(WorkerThreadArea);
            worker.Start();
        }

        BotTabSimple _tab;

        public StrategyParameterInt CountOrdersInSeries;

        public StrategyParameterDecimal Volume;

        public StrategyParameterInt Delay;

        public StrategyParameterDecimal Distance;

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "TestBotOpenAndCanselOrders";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // logic

        private void Button_UserClickOnButtonEvent()
        {
            if(_needToWork == false)
            {
                _needToWork = true;
            }
        }

        bool _needToWork;

        // Worker thread Area
        private void WorkerThreadArea()
        {
            while(true)
            {
                Thread.Sleep(1000);

                if(_needToWork)
                {
                    int delay = Delay.ValueInt;

                    int count = CountOrdersInSeries.ValueInt;

                    for(int i = 0; i < count; i++)
                    {
                        Thread.Sleep(delay * 1000);

                        Position pos = OpenBuyPosition();

                        Thread.Sleep(delay * 1000);

                        CanselOrderByPosition(pos);

                        Thread.Sleep(delay * 1000);

                        pos = OpenSellPosition();

                        Thread.Sleep(delay * 1000);

                        CanselOrderByPosition(pos);
                    }
                }

                _needToWork = false;
            }
        }

        // Open buy position
        private Position OpenBuyPosition()
        {
            decimal volume = Volume.ValueDecimal;
            decimal price = _tab.PriceBestBid - _tab.PriceBestBid * (Distance.ValueDecimal /100);

            Position pos = _tab.BuyAtLimit(volume, price);

            return pos;
        }

        // Open sell position
        private Position OpenSellPosition()
        {
            decimal volume = Volume.ValueDecimal;
            decimal price = _tab.PriceBestAsk + _tab.PriceBestAsk * (Distance.ValueDecimal / 100);

            Position pos = _tab.SellAtLimit(volume, price);

            return pos;
        }

        // Cansel all position
        private void CanselOrderByPosition(Position pos)
        {
            _tab.CloseAllOrderToPosition(pos);
        }
    }
}