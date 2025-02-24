using OsEngine.Entity;
using OsEngine.Market.Servers.CoinEx.Spot.Entity.Enums;
using System;

namespace OsEngine.Market.Servers.CoinEx.Spot.Entity
{
	struct CexOrder
	{
		public long order_id { get; set; }

		public string market { get; set; }

		// Market Type [MARGIN | SPOT | FUTURES]
		public string market_type { get; set; }

		// Currency name
		public string ccy { get; set; }

		public string side { get; set; }

		// limit | market | maker_only | IOC | FOK
		public string type { get; set; }

		// Amount - объём в единицах тикера
		// Value - объём в деньгах
		public string amount { get; set; }

		public string price { get; set; }

		public string unfilled_amount { get; set; }

		public string filled_amount { get; set; }

		public string filled_value { get; set; }

		// User-defined id
		public string client_id { get; set; }

		// BaseCurrencyFee. Trading fee paid in base currency
		public string base_fee { get; set; }

		// QuoteCurrencyFee. Trading fee paid in quote currency
		public string quote_fee { get; set; }

		// DiscountCurrencyFee. Trading fee paid mainly in CET
		public string discount_fee { get; set; }

		public string maker_fee_rate { get; set; }

		public string taker_fee_rate { get; set; }

		public string last_filled_amount { get; set; }

		public string last_filled_price { get; set; }

		public long created_at { get; set; }

		public long updated_at { get; set; }

		// Order status [open, part_filled, filled, part_canceled, canceled]
		public string? status { get; set; }

		public static explicit operator Order(CexOrder cexOrder)
		{
			Order order = new Order();

			order.NumberUser = Convert.ToInt32(cexOrder.client_id);

			order.SecurityNameCode = cexOrder.market;
			order.SecurityClassCode = cexOrder.market.Substring(cexOrder.ccy?.Length ?? cexOrder.market.Length - 3); // Fix for Futures (no Currency info)
			order.Volume = cexOrder.amount.ToString().ToDecimal(); // FIX Разобраться с названием параметра!
			order.VolumeExecute = cexOrder.filled_amount.ToString().ToDecimal(); // FIX Разобраться с названием параметра!

			//order.PortfolioNumber = this.PortfolioName;

			order.Price = cexOrder.price.ToString().ToDecimal();
			if (cexOrder.type == CexOrderType.LIMIT.ToString())
			{
				order.TypeOrder = OrderPriceType.Limit;
			}
			else if (cexOrder.type == CexOrderType.MARKET.ToString())
			{
				order.TypeOrder = OrderPriceType.Market;
				// TODO нужно заполнить цену ?
			}

			order.ServerType = ServerType.CoinExSpot;

			order.NumberMarket = cexOrder.order_id.ToString();

			//order.TimeCallBack = CoinExServerRealization.ConvertToDateTimeFromUnixFromMilliseconds(cexOrder.updated_at);
			order.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(cexOrder.updated_at);
			order.TimeCreate = new DateTime(1970, 1, 1).AddMilliseconds(cexOrder.created_at);

			order.Side = (cexOrder.side == CexOrderSide.BUY.ToString()) ? Side.Buy : Side.Sell;


			// Order placed successfully (unfilled/partially filled)
			order.State = OrderStateType.None;
			if (!string.IsNullOrEmpty(cexOrder.status))
			{
				if (cexOrder.status == CexOrderStatus.OPEN.ToString())
				{
					order.State = OrderStateType.Active;
				}
				else if (cexOrder.status == CexOrderStatus.PART_FILLED.ToString())
				{
					order.State = OrderStateType.Partial;
				}
				else if (cexOrder.status == CexOrderStatus.FILLED.ToString())
				{
					order.State = OrderStateType.Done;
                    order.TimeDone = order.TimeCallBack;
                }
				else if (cexOrder.status == CexOrderStatus.PART_CANCELED.ToString())
				{
					order.State = OrderStateType.Cancel;
                    order.TimeCancel = order.TimeCallBack;
                }
				else if (cexOrder.status == CexOrderStatus.CANCELED.ToString())
				{
					order.State = OrderStateType.Cancel;
                    order.TimeCancel = order.TimeCallBack;
				}
				else
				{
					order.State = OrderStateType.Fail;
				}
			} else
			{
				if(cexOrder.unfilled_amount.ToString().ToDecimal() > 0)
				{
					order.State = cexOrder.amount == cexOrder.unfilled_amount ? OrderStateType.Activ : OrderStateType.Patrial;
				}
			}

			// Cancelled определять в точке вызова по типу запроса [Filled Order, Unfilled Order, ...]

			return order;
		}

		public override string ToString()
		{
			string order = string.Format(
				"CexOrder:{0}Order ID: {1}{0}Client ID: {2}{0}Market: {3}{0}Type: {4}{0}Side: {5}{0}Amount: {6}{0}Price: {7}{0}Unfilled Amount: {8}{0}Filled Amount: {9}{0}Filled Value: {10}{0}Status: {11}", Environment.NewLine
				, order_id
				, client_id
				, market
				, type
				, side
				, amount
				, price
				, unfilled_amount
				, filled_amount
				, filled_value
				, status
				);

			return order;
		}
	}
}