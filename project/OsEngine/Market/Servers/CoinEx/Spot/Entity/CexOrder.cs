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
		public string status { get; set; }

		public string ToStringValue()
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