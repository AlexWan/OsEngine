/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Drawing;
using System.Windows.Forms;

namespace OsEngine.Entity
{

    /// <summary>
    /// Окно просмотра дополнительной информации по сделке
    /// </summary>
    public partial class PositionUi
    {

        /// <summary>
        /// позиция
        /// </summary>
        private Position _position;

        /// <summary>
        /// конструктор
        /// </summary>
        public PositionUi(Position position)
        {
            _position = position;
            InitializeComponent();
            CreateMainTable();
            PaintOrderTable();
            PaintTradeTable();
        }

// главная таблица

        /// <summary>
        /// создать таблицу 
        /// </summary>
        /// <returns>таблица для прорисовки на ней позиций</returns>
        private void CreateMainTable()
        {
            DataGridView newGrid = new DataGridView();

            newGrid.AllowUserToOrderColumns = false;
            newGrid.AllowUserToResizeRows = false;
            newGrid.AllowUserToDeleteRows = false;
            newGrid.AllowUserToAddRows = false;
            newGrid.RowHeadersVisible = false;
            newGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            newGrid.MultiSelect = false;


            DataGridViewCellStyle style = new DataGridViewCellStyle();
            style.Alignment = DataGridViewContentAlignment.BottomRight;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = @"Номер";
            colum0.ReadOnly = true;
            colum0.Width = 50;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colum01 = new DataGridViewColumn();
            colum01.CellTemplate = cell0;
            colum01.HeaderText = @"Время отк.";
            colum01.ReadOnly = true;
            colum01.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum01);

            DataGridViewColumn colum02 = new DataGridViewColumn();
            colum02.CellTemplate = cell0;
            colum02.HeaderText = @"Время зак.";
            colum02.ReadOnly = true;
            colum02.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum02);

            DataGridViewColumn colu = new DataGridViewColumn();
            colu.CellTemplate = cell0;
            colu.HeaderText = @"Бот";
            colu.ReadOnly = true;
            colu.Width = 70;

            newGrid.Columns.Add(colu);

            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = @"Инструмент";
            colum1.ReadOnly = true;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            newGrid.Columns.Add(colum1);

            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = @"Напр.";
            colum2.ReadOnly = true;
            colum2.Width = 40;

            newGrid.Columns.Add(colum2);

            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = @"Cостояние";
            colum3.ReadOnly = true;
            colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            newGrid.Columns.Add(colum3);

            DataGridViewColumn colum4 = new DataGridViewColumn();
            colum4.CellTemplate = cell0;
            colum4.HeaderText = @"Объём";
            colum4.ReadOnly = true;
            colum4.Width = 60;

            newGrid.Columns.Add(colum4);

            DataGridViewColumn colum45 = new DataGridViewColumn();
            colum45.CellTemplate = cell0;
            colum45.HeaderText = @"Текущий";
            colum45.ReadOnly = true;
            colum45.Width = 60;

            newGrid.Columns.Add(colum45);

            DataGridViewColumn colum5 = new DataGridViewColumn();
            colum5.CellTemplate = cell0;
            colum5.HeaderText = @"Ожидает";
            colum5.ReadOnly = true;
            colum5.Width = 60;

            newGrid.Columns.Add(colum5);

            DataGridViewColumn colum6 = new DataGridViewColumn();
            colum6.CellTemplate = cell0;
            colum6.HeaderText = @"Цена входа";
            colum6.ReadOnly = true;
            colum6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            newGrid.Columns.Add(colum6);

            DataGridViewColumn colum61 = new DataGridViewColumn();
            colum61.CellTemplate = cell0;
            colum61.HeaderText = @"Цена выхода";
            colum61.ReadOnly = true;
            colum61.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            newGrid.Columns.Add(colum61);

            DataGridViewColumn colum8 = new DataGridViewColumn();
            colum8.CellTemplate = cell0;
            colum8.HeaderText = @"Прибыль";
            colum8.ReadOnly = true;
            colum8.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            newGrid.Columns.Add(colum8);

            DataGridViewColumn colum9 = new DataGridViewColumn();
            colum9.CellTemplate = cell0;
            colum9.HeaderText = @"СтопАктивация";
            colum9.ReadOnly = true;
            colum9.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            newGrid.Columns.Add(colum9);

            DataGridViewColumn colum10 = new DataGridViewColumn();
            colum10.CellTemplate = cell0;
            colum10.HeaderText = @"СтопЦена";
            colum10.ReadOnly = true;
            colum10.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            newGrid.Columns.Add(colum10);

            DataGridViewColumn colum11 = new DataGridViewColumn();
            colum11.CellTemplate = cell0;
            colum11.HeaderText = @"ПрофитАктивация";
            colum11.ReadOnly = true;
            colum11.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            newGrid.Columns.Add(colum11);

            DataGridViewColumn colum12 = new DataGridViewColumn();
            colum12.CellTemplate = cell0;
            colum12.HeaderText = @"ПрофитЦена";
            colum12.ReadOnly = true;
            colum12.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            newGrid.Columns.Add(colum12);

            newGrid.Rows.Add(GetRow(_position));

            FormsHostMainGrid.Child = newGrid;

        }

        /// <summary>
        /// взять строку для таблицы представляющую позицию
        /// </summary>
        /// <param name="position">позиция</param>
        /// <returns>строка для таблицы</returns>
        private DataGridViewRow GetRow(Position position)
        {
            if (position == null)
            {
                return null;
            }

            DataGridViewCellStyle styleDefolt = new DataGridViewCellStyle();


            if (position.ProfitPortfolioPunkt > 0)
            {
                styleDefolt.BackColor = Color.SeaGreen;
                styleDefolt.SelectionBackColor = Color.SeaGreen;
            }
            else if (position.ProfitPortfolioPunkt < 0)
            {
                styleDefolt.BackColor = Color.Salmon;
                styleDefolt.SelectionBackColor = Color.Salmon;
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

            nRow.DefaultCellStyle = styleDefolt;

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
            nRow.Cells[10].Value = position.EntryPrice;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[11].Value = position.ClosePrice;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[12].Value = position.ProfitPortfolioPunkt;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[13].Value = position.StopOrderRedLine;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[14].Value = position.StopOrderPrice;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[15].Value = position.ProfitOrderRedLine;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[16].Value = position.ProfitOrderPrice;

            return nRow;
        }

// ордера

        /// <summary>
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
        /// создать таблицу ордеров
        /// </summary>
        private  DataGridView CreateOrderTable()
        {
            DataGridView newGrid = new DataGridView();

            newGrid.AllowUserToOrderColumns = false;
            newGrid.AllowUserToResizeRows = false;
            newGrid.AllowUserToDeleteRows = false;
            newGrid.AllowUserToAddRows = false;
            newGrid.RowHeadersVisible = false;
            newGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            newGrid.MultiSelect = false;

            DataGridViewCellStyle style = new DataGridViewCellStyle();
            style.Alignment = DataGridViewContentAlignment.BottomRight;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = @"ID";
            colum0.ReadOnly = true;
            colum0.Width = 50;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colum01 = new DataGridViewColumn();
            colum01.CellTemplate = cell0;
            colum01.HeaderText = @"ID на бирже";
            colum01.ReadOnly = true;
            colum01.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum01);

            DataGridViewColumn colum02 = new DataGridViewColumn();
            colum02.CellTemplate = cell0;
            colum02.HeaderText = @"Время выст.";
            colum02.ReadOnly = true;
            colum02.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum02);

            DataGridViewColumn colu = new DataGridViewColumn();
            colu.CellTemplate = cell0;
            colu.HeaderText = @"Бумага";
            colu.ReadOnly = true;
            colu.Width = 60;
            newGrid.Columns.Add(colu);

            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = @"Портфель";
            colum1.ReadOnly = true;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum1);

            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = @"Напр.";
            colum2.ReadOnly = true;
            colum2.Width = 40;
            newGrid.Columns.Add(colum2);

            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = @"Cтатус";
            colum3.ReadOnly = true;
            colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum3);

            DataGridViewColumn colum4 = new DataGridViewColumn();
            colum4.CellTemplate = cell0;
            colum4.HeaderText = @"Цена";
            colum4.ReadOnly = true;
            colum4.Width = 60;
            newGrid.Columns.Add(colum4);

            DataGridViewColumn colum45 = new DataGridViewColumn();
            colum45.CellTemplate = cell0;
            colum45.HeaderText = @"Исполнение";
            colum45.ReadOnly = true;
            colum45.Width = 60;
            newGrid.Columns.Add(colum45);

            DataGridViewColumn colum5 = new DataGridViewColumn();
            colum5.CellTemplate = cell0;
            colum5.HeaderText = @"Объём";
            colum5.ReadOnly = true;
            colum5.Width = 60;
            newGrid.Columns.Add(colum5);

            DataGridViewColumn colum6 = new DataGridViewColumn();
            colum6.CellTemplate = cell0;
            colum6.HeaderText = @"Тип";
            colum6.ReadOnly = true;
            colum6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum6);

            DataGridViewColumn colum7 = new DataGridViewColumn();
            colum7.CellTemplate = cell0;
            colum7.HeaderText = @"RoundTrip мск";
            colum7.ReadOnly = true;
            colum7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum7);

            return newGrid;
        }

        /// <summary>
        /// создать строку для таблицы из ордера
        /// </summary>
        private DataGridViewRow CreateOrderRow(Order order)
        {
            if (order == null)
            {
                return null;
            }

            DataGridViewCellStyle styleDefolt = new DataGridViewCellStyle();

            styleDefolt.BackColor = Color.WhiteSmoke;
            styleDefolt.SelectionBackColor = Color.WhiteSmoke;
            styleDefolt.SelectionForeColor = Color.Black;

            DataGridViewRow nRow = new DataGridViewRow();
            nRow.DefaultCellStyle = styleDefolt;

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
            nRow.Cells[7].Value = order.Price;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[8].Value = order.PriceReal;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[9].Value = order.Volume;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[10].Value = order.TypeOrder;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[11].Value = order.TimeRoundTrip.TotalMilliseconds;

            return nRow;
        }

// трейды

        /// <summary>
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
        /// создать таблицу для трейдов
        /// </summary>
        private DataGridView CreateTradeTable()
        {
            DataGridView newGrid = new DataGridView();

            newGrid.AllowUserToOrderColumns = false;
            newGrid.AllowUserToResizeRows = false;
            newGrid.AllowUserToDeleteRows = false;
            newGrid.AllowUserToAddRows = false;
            newGrid.RowHeadersVisible = false;
            newGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            newGrid.MultiSelect = false;

            DataGridViewCellStyle style = new DataGridViewCellStyle();
            style.Alignment = DataGridViewContentAlignment.BottomRight;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = @"ID";
            colum0.ReadOnly = true;
            colum0.Width = 50;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colum03 = new DataGridViewColumn();
            colum03.CellTemplate = cell0;
            colum03.HeaderText = @"ID ордера";
            colum03.ReadOnly = true;
            colum03.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum03);

            DataGridViewColumn colum01 = new DataGridViewColumn();
            colum01.CellTemplate = cell0;
            colum01.HeaderText = @"Бумага";
            colum01.ReadOnly = true;
            colum01.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum01);

            DataGridViewColumn colum02 = new DataGridViewColumn();
            colum02.CellTemplate = cell0;
            colum02.HeaderText = @"Время выст.";
            colum02.ReadOnly = true;
            colum02.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum02);

            DataGridViewColumn colu = new DataGridViewColumn();
            colu.CellTemplate = cell0;
            colu.HeaderText = @"Цена";
            colu.ReadOnly = true;
            colu.Width = 60;
            newGrid.Columns.Add(colu);

            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = @"Объём";
            colum1.ReadOnly = true;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum1);

            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = @"Напр.";
            colum2.ReadOnly = true;
            colum2.Width = 40;
            newGrid.Columns.Add(colum2);

            return newGrid;
        }

        /// <summary>
        /// создать строку для таблицы из трейда
        /// </summary>
        private DataGridViewRow CreateTradeRow(MyTrade trade)
        {
            if (trade == null)
            {
                return null;
            }

            DataGridViewCellStyle styleDefolt = new DataGridViewCellStyle();

            styleDefolt.BackColor = Color.WhiteSmoke;
            styleDefolt.SelectionBackColor = Color.WhiteSmoke;
            styleDefolt.SelectionForeColor = Color.Black;

            DataGridViewRow nRow = new DataGridViewRow();
            nRow.DefaultCellStyle = styleDefolt;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = trade.NumberTrade;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = trade.NumberOrderParent;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[2].Value = trade.SecurityNameCode;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[3].Value = trade.Time;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[4].Value = trade.Price;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[5].Value = trade.Volume;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[6].Value = trade.Side;

            return nRow;
        }

    }
}
