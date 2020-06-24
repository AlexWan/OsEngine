/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using OsEngine.Entity;
using OsEngine.Market;

namespace OsEngine.OsTrader.Panels.Tab.Internal
{
    /// <summary>
    /// create position / 
    /// создатель сделок
    /// </summary>
    public class PositionCreator
    {

        /// <summary>
        /// create position / 
        /// создать сделку
        /// </summary>
        public Position CreatePosition(string botName, Side direction, decimal priceOrder, decimal volume, OrderPriceType priceType, TimeSpan timeLife,
            Security security, Portfolio portfolio, StartProgram startProgram)
        {
            Position newDeal = new Position();
            newDeal.Number = NumberGen.GetNumberDeal(startProgram);
            newDeal.Direction = direction;
            newDeal.State = PositionStateType.Opening;

            newDeal.AddNewOpenOrder(CreateOrder(direction, priceOrder, volume, priceType, timeLife, startProgram,OrderPositionConditionType.Open));

            newDeal.NameBot = botName;
            newDeal.Lots = security.Lot;
            newDeal.PriceStepCost = security.PriceStepCost;
            newDeal.PriceStep = security.PriceStep;
            newDeal.PortfolioValueOnOpenPosition = portfolio.ValueCurrent;

            return newDeal;
        }

        /// <summary>
        /// create order / 
        /// создать ордер
        /// </summary>
        public Order CreateOrder(
            Side direction, decimal priceOrder, decimal volume, 
            OrderPriceType priceType, TimeSpan timeLife, StartProgram startProgram,
                OrderPositionConditionType positionConditionType)
        {
            Order newOrder = new Order();
            newOrder.NumberUser = NumberGen.GetNumberOrder(startProgram);
            newOrder.Side = direction;
            newOrder.Price = priceOrder;
            newOrder.Volume = volume;
            newOrder.TypeOrder = priceType;
            newOrder.LifeTime = timeLife;
            newOrder.PositionConditionType = positionConditionType;

            return newOrder;
        }

        /// <summary>
        /// create closing order / 
        /// создать закрывающий ордер
        /// </summary>
        public Order CreateCloseOrderForDeal(Position deal, decimal price, OrderPriceType priceType, TimeSpan timeLife, StartProgram startProgram)
        {
            Side direction;

            if (deal.Direction == Side.Buy)
            {
                direction = Side.Sell;
            }
            else
            {
                direction = Side.Buy;
            }

            decimal volume = deal.OpenVolume;

            if (volume == 0)
            {
                return null;
            }

            Order newOrder = new Order();

            newOrder.NumberUser = NumberGen.GetNumberOrder(startProgram);
            newOrder.Side = direction;
            newOrder.Price = price;
            newOrder.Volume = volume;
            newOrder.TypeOrder = priceType;
            newOrder.LifeTime = timeLife;
            newOrder.PositionConditionType = OrderPositionConditionType.Close;

            return newOrder;
        }

    }
}