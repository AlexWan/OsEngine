/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Drawing;
using System.Windows.Forms;
using OsEngine.Language;

namespace OsEngine.Entity
{

    /// <summary>
    /// View additional information about the transaction in the window
    /// Окно просмотра дополнительной информации по сделке
    /// </summary>
    public partial class PositionUi
    {

        /// <summary>
        /// position
        /// позиция
        /// </summary>
        private Position _position;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        public PositionUi(Position position)
        {
            _position = position;
            InitializeComponent();
            CreateMainTable();
            PaintOrderTable();
            PaintTradeTable();

            Title = OsLocalization.Entity.TitlePositionUi;
            PositionLabel1.Content = OsLocalization.Entity.PositionLabel1;
            PositionLabel2.Content = OsLocalization.Entity.PositionLabel2;
            PositionLabel3.Content = OsLocalization.Entity.PositionLabel3;

        }
        // main table
        // главная таблица

        /// <summary>
        /// create a table
        /// создать таблицу 
        /// </summary>
        /// <returns>table for drawing positions on it/таблица для прорисовки на ней позиций</returns>
        private void CreateMainTable()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridPosition();

            newGrid.Rows.Add(GetRow(_position));

            FormsHostMainGrid.Child = newGrid;
        }

        /// <summary>
        /// take a row for the table representing the position

        /// взять строку для таблицы представляющую позицию
        /// </summary>
        /// <param name="position">position/позиция</param>
        /// <returns>table row/строка для таблицы</returns>
        private DataGridViewRow GetRow(Position position)
        {
            if (position == null)
            {
                return null;
            }


            DataGridViewCellStyle styleSide = new DataGridViewCellStyle();

            if (position.Direction == Side.Buy)
            {
                styleSide.BackColor = Color.DodgerBlue;
                styleSide.SelectionBackColor = Color.DodgerBlue;
            }
            else
            {
                styleSide.BackColor = Color.DarkOrange;
                styleSide.SelectionBackColor = Color.DarkOrange;
            }

            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = position.Number;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = position.TimeCreate;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[2].Value = position.TimeClose;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[3].Value = position.NameBot;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[4].Value = position.SecurityName;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[5].Value = position.Direction;
            nRow.Cells[5].Style = styleSide;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[6].Value = position.State;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[7].Value = position.MaxVolume;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[8].Value = position.OpenVolume;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[9].Value = position.WaitVolume;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[10].Value = position.EntryPrice.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[11].Value = position.ClosePrice.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[12].Value = position.ProfitPortfolioPunkt.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[13].Value = position.StopOrderRedLine.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[14].Value = position.StopOrderPrice.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[15].Value = position.ProfitOrderRedLine.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[16].Value = position.ProfitOrderPrice.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[17].Value = position.SignalTypeOpen;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[18].Value = position.SignalTypeClose;

            return nRow;
        }
        // Orders
        // ордера

        /// <summary>
        /// draw order tables
        /// прорисовать таблицы ордеров
        /// </summary>
        private void PaintOrderTable()
        {
            DataGridView openOrdersGrid = CreateOrderTable();

            for (int i = 0; _position.OpenOrders != null && i < _position.OpenOrders.Count; i++)
            {
                openOrdersGrid.Rows.Add(CreateOrderRow(_position.OpenOrders[i]));
            }
            FormsHostOpenDealGrid.Child = openOrdersGrid;

            DataGridView closeOrdersGrid = CreateOrderTable();

            for (int i = 0; _position.CloseOrders != null && i < _position.CloseOrders.Count; i++)
            {
                closeOrdersGrid.Rows.Add(CreateOrderRow(_position.CloseOrders[i]));
            }
            FormsHostCloseDealGrid.Child = closeOrdersGrid;
        }

        /// <summary>
        /// create a table of orders
           /// создать таблицу ордеров
        /// </summary>
        private DataGridView CreateOrderTable()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridOrder();

            newGrid.AutoResizeColumnHeadersHeight();
            return newGrid;
        }

        /// <summary>
        /// create a row for the table from the order
        /// создать строку для таблицы из ордера
        /// </summary>
        private DataGridViewRow CreateOrderRow(Order order)
        {
            if (order == null)
            {
                return null;
            }

            DataGridViewRow nRow = new DataGridViewRow();


            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = order.NumberUser;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = order.NumberMarket;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[2].Value = order.TimeCallBack;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[3].Value = order.SecurityNameCode;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[4].Value = order.PortfolioNumber;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[5].Value = order.Side;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[6].Value = order.State;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[7].Value = order.Price.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[8].Value = order.PriceReal.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[9].Value = order.Volume;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[10].Value = order.TypeOrder;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[11].Value = order.TimeRoundTrip.TotalMilliseconds;

            return nRow;
        }
        //trades
        // трейды

        /// <summary>
        /// draw the table of trades
        /// прорисовать таблицу трейдов
        /// </summary>
        private void PaintTradeTable()
        {
            DataGridView tradesGrid = CreateTradeTable();

            for (int i = 0; _position.OpenOrders != null && i < _position.OpenOrders.Count; i++)
            {
                for (int i2 = 0; _position.OpenOrders[i].MyTrades != null && i2 < _position.OpenOrders[i].MyTrades.Count; i2++)
                {
                    tradesGrid.Rows.Add(CreateTradeRow(_position.OpenOrders[i].MyTrades[i2]));
                }
            }

            for (int i = 0; _position.CloseOrders != null && i < _position.CloseOrders.Count; i++)
            {
                for (int i2 = 0; _position.CloseOrders[i].MyTrades != null && i2 < _position.CloseOrders[i].MyTrades.Count; i2++)
                {
                    tradesGrid.Rows.Add(CreateTradeRow(_position.CloseOrders[i].MyTrades[i2]));
                }
            }


            FormsHostTreid.Child = tradesGrid;

        }

        /// <summary>
        /// create a table for trades
        /// создать таблицу для трейдов
        /// </summary>
        private DataGridView CreateTradeTable()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridMyTrade();

            return newGrid;
        }

        /// <summary>
        /// create row for table from trade
        /// создать строку для таблицы из трейда
        /// </summary>
        private DataGridViewRow CreateTradeRow(MyTrade trade)
        {
            if (trade == null)
            {
                return null;
            }

            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = trade.NumberTrade;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = trade.NumberOrderParent;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[2].Value = trade.SecurityNameCode;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[3].Value = trade.Time;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[4].Value = trade.Price.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[5].Value = trade.Volume;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[6].Value = trade.Side;

            return nRow;
        }

    }
}
