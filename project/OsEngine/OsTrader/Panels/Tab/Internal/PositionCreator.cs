/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using OsEngine.Entity;

namespace OsEngine.OsTrader.Panels.Tab.Internal
{
    /// <summary>
    /// создатель сделок
    /// </summary>
    public class PositionCreator
    {

        /// <summary>
        /// создать сделку
        /// </summary>
        public Position CreatePosition(string botName, Side direction, decimal priceOrder, decimal volume, OrderPriceType priceType, TimeSpan timeLife,
            Security security, Portfolio portfolio)
        {
            Position newDeal = new Position();
            newDeal.Number = NumberGen.GetNumberDeal();
            newDeal.Direction = direction;
            newDeal.State = PositionStateType.Opening;

            newDeal.AddNewOpenOrder(CreateOrder(direction, priceOrder, volume, priceType, timeLife));

            newDeal.NameBot = botName;
            newDeal.Lots = security.Lot;
            newDeal.PriceStepCost = security.PriceStepCost;
            newDeal.PriceStep = security.PriceStep;
            newDeal.PortfolioValueOnOpenPosition = portfolio.ValueCurrent;

            return newDeal;
        }

        /// <summary>
        /// создать ордер
        /// </summary>
        public Order CreateOrder(Side direction, decimal priceOrder, decimal volume, OrderPriceType priceType, TimeSpan timeLife)
        {
            Order newOrder = new Order();
            newOrder.NumberUser = NumberGen.GetNumberOrder();
            newOrder.Side = direction;
            newOrder.Price = priceOrder;
            newOrder.Volume = volume;
            newOrder.TypeOrder = priceType;
            newOrder.LifeTime = timeLife;

            return newOrder;
        }

        /// <summary>
        /// создать закрывающий ордер для сделки
        /// </summary>
        public Order CreateCloseOrderForDeal(Position deal, decimal price, OrderPriceType priceType, TimeSpan timeLife)
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

            newOrder.NumberUser = NumberGen.GetNumberOrder();
            newOrder.Side = direction;
            newOrder.Price = price;
            newOrder.Volume = volume;
            newOrder.TypeOrder = priceType;
            newOrder.LifeTime = timeLife;

            return newOrder;
        }

    }
}
