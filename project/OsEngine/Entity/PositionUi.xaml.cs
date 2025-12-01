/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows.Forms;
using OsEngine.Language;
using System;
using System.Collections.Generic;
using System.Globalization;
using OsEngine.Market;

namespace OsEngine.Entity
{
    public partial class PositionUi
    {
        private Position _position;

        public PositionUi(Position position, StartProgram startProgram)
        {
            _startProgram = startProgram;
            _position = position;
            InitializeComponent();
            _currentCulture = OsLocalization.CurCulture;
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            CreateMainTable();
            CreateOrdersTable();
            CreateTradeTable();

            RePaint();

            Title = OsLocalization.Entity.TitlePositionUi + "  " + _position.Number;
            PositionLabel1.Content = OsLocalization.Entity.PositionLabel1;
            PositionLabel2.Content = OsLocalization.Entity.PositionLabel2;
            PositionLabel3.Content = OsLocalization.Entity.PositionLabel3;
            SaveChangesButton.Content = OsLocalization.Entity.PositionLabel4;

            LabelStartDepo.Content = OsLocalization.Entity.PositionStartDepoLabel;
            TextBoxStartDepo.Text = _position.PortfolioValueOnOpenPosition.ToString();

            this.Activate();
            this.Focus();

            Closed += PositionUi_Closed;
        }

        private void PositionUi_Closed(object sender, EventArgs e)
        {
            _position = null;

            //main grid
            if (_mainPosGrid != null)
            {
                FormsHostMainGrid.Child = null;
                DataGridFactory.ClearLinks(_mainPosGrid);
                _mainPosGrid.DataError -= _mainPosGrid_DataError;
                _mainPosGrid.Rows.Clear();
                _mainPosGrid.DataSource = null;
                _mainPosGrid.Dispose();
                _mainPosGrid = null;
            }

            // orders grid
            if (_openOrdersGrid != null)
            {
                FormsHostOpenDealGrid.Child = null;
                DataGridFactory.ClearLinks(_openOrdersGrid);
                _openOrdersGrid.Click -= OpenOrdersGrid_Click;
                _openOrdersGrid.DataError -= _mainPosGrid_DataError;
                _openOrdersGrid.Rows.Clear();
                _openOrdersGrid.DataSource = null;
                _openOrdersGrid.Dispose();
                _openOrdersGrid = null;
            }

            if (_closeOrdersGrid != null)
            {
                FormsHostCloseDealGrid.Child = null;
                DataGridFactory.ClearLinks(_closeOrdersGrid);
                _closeOrdersGrid.Click -= CloseOrdersGrid_Click;
                _closeOrdersGrid.DataError -= _mainPosGrid_DataError;
                _closeOrdersGrid.Rows.Clear();
                _closeOrdersGrid.DataSource = null;
                _closeOrdersGrid.Dispose();
                _closeOrdersGrid = null;
            }

            // trade grid

            if (_tradesGrid != null)
            {
                FormsHostTreid.Child = null;
                DataGridFactory.ClearLinks(_tradesGrid);
                _tradesGrid.Click -= _tradesGrid_Click;
                _tradesGrid.DataError -= _mainPosGrid_DataError;
                _tradesGrid.Rows.Clear();
                _tradesGrid.DataSource = null;
                _tradesGrid.Dispose();
                _tradesGrid = null;
            }
        }

        private CultureInfo _currentCulture;

        private StartProgram _startProgram;

        private void RePaint()
        {
            PaintPosTable();
            PaintOrderTable();
            PaintTradeTable();
        }

        #region Main table

        private DataGridView _mainPosGrid;

        private void CreateMainTable()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridPosition(false);
            newGrid.ScrollBars = ScrollBars.Vertical;
            FormsHostMainGrid.Child = newGrid;
            _mainPosGrid = newGrid;
            _mainPosGrid.DataError += _mainPosGrid_DataError;
        }

        private void _mainPosGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), Logging.LogMessageType.Error);
        }

        private void PaintPosTable()
        {
            _mainPosGrid.Rows.Clear();
            _mainPosGrid.Rows.Add(GetPositionRow(_position));
        }

        private DataGridViewRow GetPositionRow(Position position)
        {
            if (position == null)
            {
                return null;
            }

            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = position.Number;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = position.TimeCreate.ToString(_currentCulture);

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            if (position.TimeClose != position.TimeOpen)
            {
                nRow.Cells[2].Value = position.TimeClose.ToString(_currentCulture);
            }
            else
            {
                nRow.Cells[2].Value = "";
            }

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[3].Value = position.NameBot;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[4].Value = position.SecurityName;

            DataGridViewComboBoxCell dirCell = new DataGridViewComboBoxCell();

            dirCell.Items.Add(Side.Buy.ToString());
            dirCell.Items.Add(Side.Sell.ToString());
            dirCell.Items.Add(Side.None.ToString());

            nRow.Cells.Add(dirCell);
            nRow.Cells[5].Value = position.Direction.ToString();

            DataGridViewComboBoxCell stateCell = new DataGridViewComboBoxCell();

            stateCell.Items.Add(PositionStateType.None.ToString());
            stateCell.Items.Add(PositionStateType.Open.ToString());
            stateCell.Items.Add(PositionStateType.Done.ToString());
            stateCell.Items.Add(PositionStateType.Closing.ToString());
            stateCell.Items.Add(PositionStateType.ClosingFail.ToString());
            stateCell.Items.Add(PositionStateType.ClosingSurplus.ToString());
            stateCell.Items.Add(PositionStateType.Opening.ToString());
            stateCell.Items.Add(PositionStateType.OpeningFail.ToString());
            stateCell.Items.Add(PositionStateType.Deleted.ToString());

            int decimalsPrice = position.PriceStep.ToStringWithNoEndZero().DecimalsCount();

            decimalsPrice++;

            nRow.Cells.Add(stateCell);
            nRow.Cells[6].Value = position.State.ToString();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[7].Value = position.MaxVolume;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[8].Value = position.OpenVolume;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[9].Value = position.WaitVolume;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[10].Value = Math.Round(position.EntryPrice, decimalsPrice).ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[11].Value = Math.Round(position.ClosePrice, decimalsPrice).ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[12].Value = Math.Round(position.ProfitPortfolioAbs, decimalsPrice).ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[13].Value = Math.Round(position.StopOrderRedLine, decimalsPrice).ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[14].Value = Math.Round(position.StopOrderPrice, decimalsPrice).ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[15].Value = Math.Round(position.ProfitOrderRedLine, decimalsPrice).ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[16].Value = Math.Round(position.ProfitOrderPrice, decimalsPrice).ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[17].Value = position.SignalTypeOpen;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[18].Value = position.SignalTypeClose;

            return nRow;
        }

        #endregion

        #region Orders

        private DataGridView _openOrdersGrid;

        private DataGridView _closeOrdersGrid;

        private void CreateOrdersTable()
        {
            DataGridView openOrdersGrid = CreateOrderTable();
            openOrdersGrid.Click += OpenOrdersGrid_Click;
            _openOrdersGrid = openOrdersGrid;
            FormsHostOpenDealGrid.Child = openOrdersGrid;
            _openOrdersGrid.DataError += _mainPosGrid_DataError;
            DataGridView closeOrdersGrid = CreateOrderTable();
            closeOrdersGrid.Click += CloseOrdersGrid_Click;
            _closeOrdersGrid = closeOrdersGrid;
            FormsHostCloseDealGrid.Child = closeOrdersGrid;
            _closeOrdersGrid.DataError += _mainPosGrid_DataError;
        }

        private DataGridView CreateOrderTable()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridOrder(false);
            newGrid.ScrollBars = ScrollBars.Vertical;
            newGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            newGrid.AutoResizeColumnHeadersHeight();
            return newGrid;
        }

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

            DataGridViewButtonCell timeButton = new DataGridViewButtonCell();
            nRow.Cells.Add(timeButton);
            nRow.Cells[2].Value = order.TimeCallBack.ToString(_currentCulture);

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[3].Value = order.SecurityNameCode;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[4].Value = order.PortfolioNumber;

            DataGridViewComboBoxCell dirCell = new DataGridViewComboBoxCell();
            dirCell.Items.Add(Side.Buy.ToString());
            dirCell.Items.Add(Side.Sell.ToString());
            dirCell.Items.Add(Side.None.ToString());
            nRow.Cells.Add(dirCell);
            nRow.Cells[5].Value = order.Side.ToString();

            DataGridViewComboBoxCell stateCell = new DataGridViewComboBoxCell();

            stateCell.Items.Add(OrderStateType.None.ToString());
            stateCell.Items.Add(OrderStateType.Active.ToString());
            stateCell.Items.Add(OrderStateType.Cancel.ToString());
            stateCell.Items.Add(OrderStateType.Done.ToString());
            stateCell.Items.Add(OrderStateType.Fail.ToString());
            stateCell.Items.Add(OrderStateType.Partial.ToString());
            stateCell.Items.Add(OrderStateType.Pending.ToString());
            nRow.Cells.Add(stateCell);
            nRow.Cells[6].Value = order.State.ToString();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[7].Value = order.Price.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[8].Value = order.PriceReal.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[9].Value = order.Volume;

            DataGridViewComboBoxCell typeCell = new DataGridViewComboBoxCell();
            typeCell.Items.Add(OrderPriceType.Limit.ToString());
            typeCell.Items.Add(OrderPriceType.Market.ToString());
            typeCell.Items.Add(OrderPriceType.Iceberg.ToString());
            nRow.Cells.Add(typeCell);
            nRow.Cells[10].Value = order.TypeOrder.ToString();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[11].Value = order.TimeRoundTrip.TotalMilliseconds;

            return nRow;
        }

        private void PaintOrderTable()
        {
            _openOrdersGrid.Rows.Clear();

            for (int i = 0; _position.OpenOrders != null && i < _position.OpenOrders.Count; i++)
            {
                _openOrdersGrid.Rows.Add(CreateOrderRow(_position.OpenOrders[i]));
            }

            _closeOrdersGrid.Rows.Clear();

            for (int i = 0; _position.CloseOrders != null && i < _position.CloseOrders.Count; i++)
            {
                _closeOrdersGrid.Rows.Add(CreateOrderRow(_position.CloseOrders[i]));
            }
        }

        private void CloseOrdersGrid_Click(object sender, EventArgs e)
        {
            MouseEventArgs mouse = (MouseEventArgs)e;
            if (mouse.Button != MouseButtons.Right)
            {
                CheckOrdersTimeButtonClick(_position.CloseOrders, _closeOrdersGrid);
                return;
            }

            try
            {
                List<ToolStripMenuItem> items = new List<ToolStripMenuItem>();

                items.Add(new ToolStripMenuItem { Text = OsLocalization.Entity.OrderContextMenuItem1 });
                items[0].Click += CloseOrdersAddOrder_Click;

                items.Add(new ToolStripMenuItem { Text = OsLocalization.Entity.OrderContextMenuItem2 });
                items[1].Click += CloseOrdersDeleteOrder_Click;

                ContextMenuStrip menu = new ContextMenuStrip(); menu.Items.AddRange(items.ToArray());

                _closeOrdersGrid.ContextMenuStrip = menu;
                _closeOrdersGrid.ContextMenuStrip.Show(_closeOrdersGrid, new System.Drawing.Point(mouse.X, mouse.Y));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void CloseOrdersAddOrder_Click(object sender, EventArgs e)
        {
            try
            {
                Order newOrder = new Order();
                newOrder.NumberUser = NumberGen.GetNumberOrder(_startProgram);

                if (_position.Direction == Side.Buy)
                {
                    newOrder.Side = Side.Sell;
                }
                else
                {
                    newOrder.Side = Side.Buy;
                }

                newOrder.NumberMarket = NumberGen.GetNumberOrder(_startProgram).ToString();
                newOrder.TypeOrder = OrderPriceType.Limit;
                newOrder.PortfolioNumber = GetPortfolioName();
                newOrder.PositionConditionType = OrderPositionConditionType.Close;

                if (string.IsNullOrEmpty(_position.SecurityName) == false)
                {
                    newOrder.SecurityNameCode = _position.SecurityName;
                }

                _position.AddNewCloseOrder(newOrder);

                SyncPositionWithOrdersAndMyTrades();
                PaintOrderTable();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void CloseOrdersDeleteOrder_Click(object sender, EventArgs e)
        {
            try
            {
                if (_position.CloseOrders == null)
                {
                    return;
                }
                if (_closeOrdersGrid.Rows.Count == 0)
                {
                    return;
                }

                int number;
                try
                {
                    number = _closeOrdersGrid.CurrentCell.RowIndex;
                }
                catch (Exception)
                {
                    return;
                }

                if (number >= _position.CloseOrders.Count)
                {
                    return;
                }

                if (ActionDeleteIsAccepted() == false)
                {
                    return;
                }

                _position.CloseOrders.RemoveAt(number);
                RePaint();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void OpenOrdersGrid_Click(object sender, EventArgs e)
        {
            try
            {
                MouseEventArgs mouse = (MouseEventArgs)e;
                if (mouse.Button != MouseButtons.Right)
                {
                    CheckOrdersTimeButtonClick(_position.OpenOrders, _openOrdersGrid);
                    return;
                }

                List<ToolStripMenuItem> items = new List<ToolStripMenuItem>();

                items.Add(new ToolStripMenuItem { Text = OsLocalization.Entity.OrderContextMenuItem1 });
                items[0].Click += OpenOrdersAddOrder_Click;

                items.Add(new ToolStripMenuItem { Text = OsLocalization.Entity.OrderContextMenuItem2 });
                items[1].Click += OpenOrdersDeleteOrder_Click;

                ContextMenuStrip menu = new ContextMenuStrip(); menu.Items.AddRange(items.ToArray());

                _openOrdersGrid.ContextMenuStrip = menu;
                _openOrdersGrid.ContextMenuStrip.Show(_openOrdersGrid, new System.Drawing.Point(mouse.X, mouse.Y));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void CheckOrdersTimeButtonClick(List<Order> orders, DataGridView grid)
        {
            try
            {
                if (orders == null ||
                orders.Count == 0)
                {
                    return;
                }
                if (grid.SelectedCells == null ||
                    grid.SelectedCells.Count == 0)
                {
                    return;
                }
                int tabRow = grid.SelectedCells[0].RowIndex;
                int tabColumn = grid.SelectedCells[0].ColumnIndex;

                if (tabColumn == 2)
                {
                    if (tabRow >= orders.Count)
                    {
                        return;
                    }
                    Order myOrder = orders[tabRow];

                    DateTime time = myOrder.TimeCallBack;

                    if (myOrder.TimeCallBack == DateTime.MinValue)
                    {
                        time = DateTime.Now;
                    }
                    else
                    {
                        time = myOrder.TimeCallBack;
                    }

                    DateTimeSelectionDialog dialog = new DateTimeSelectionDialog(time);
                    dialog.ShowDialog();

                    if (dialog.IsSaved)
                    {
                        myOrder.TimeCallBack = dialog.Time;
                        myOrder.TimeCreate = dialog.Time;
                        grid.Rows[tabRow].Cells[tabColumn].Value = myOrder.TimeCallBack.ToString(_currentCulture);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void OpenOrdersAddOrder_Click(object sender, EventArgs e)
        {
            try
            {
                Order newOrder = new Order();
                newOrder.NumberUser = NumberGen.GetNumberOrder(_startProgram);
                newOrder.Side = _position.Direction;
                newOrder.NumberMarket = NumberGen.GetNumberOrder(_startProgram).ToString();
                newOrder.TypeOrder = OrderPriceType.Limit;
                newOrder.PortfolioNumber = GetPortfolioName();
                newOrder.PositionConditionType = OrderPositionConditionType.Open;

                if(string.IsNullOrEmpty(_position.SecurityName) == false)
                {
                    newOrder.SecurityNameCode = _position.SecurityName;
                }

                _position.AddNewOpenOrder(newOrder);

                SyncPositionWithOrdersAndMyTrades();
                PaintOrderTable();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void OpenOrdersDeleteOrder_Click(object sender, EventArgs e)
        {
            try
            {
                if (_position.OpenOrders == null)
                {
                    return;
                }
                if (_openOrdersGrid.Rows.Count == 0)
                {
                    return;
                }

                int number;
                try
                {
                    number = _openOrdersGrid.CurrentCell.RowIndex;
                }
                catch (Exception)
                {
                    return;
                }

                if (number >= _position.OpenOrders.Count)
                {
                    return;
                }

                _position.OpenOrders.RemoveAt(number);
                RePaint();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        #endregion

        #region Trades

        private DataGridView _tradesGrid;

        private void CreateTradeTable()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridMyTrade(false);
            _tradesGrid = newGrid;
            _tradesGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            FormsHostTreid.Child = _tradesGrid;
            _tradesGrid.Click += _tradesGrid_Click;
            _tradesGrid.DataError += _mainPosGrid_DataError;
        }

        private void PaintTradeTable()
        {
            _tradesGrid.Rows.Clear();
            for (int i = 0; _position.OpenOrders != null && i < _position.OpenOrders.Count; i++)
            {
                for (int i2 = 0; _position.OpenOrders[i].MyTrades != null && i2 < _position.OpenOrders[i].MyTrades.Count; i2++)
                {
                    _tradesGrid.Rows.Add(CreateTradeRow(_position.OpenOrders[i].MyTrades[i2]));
                }
            }

            for (int i = 0; _position.CloseOrders != null && i < _position.CloseOrders.Count; i++)
            {
                for (int i2 = 0; _position.CloseOrders[i].MyTrades != null && i2 < _position.CloseOrders[i].MyTrades.Count; i2++)
                {
                    _tradesGrid.Rows.Add(CreateTradeRow(_position.CloseOrders[i].MyTrades[i2]));
                }
            }
        }

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

            DataGridViewButtonCell timeButton = new DataGridViewButtonCell();
            nRow.Cells.Add(timeButton);
            nRow.Cells[3].Value = trade.Time.ToString(_currentCulture);

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[4].Value = trade.Price.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[5].Value = trade.Volume;

            DataGridViewComboBoxCell dirCell = new DataGridViewComboBoxCell();
            dirCell.Items.Add(Side.Buy.ToString());
            dirCell.Items.Add(Side.Sell.ToString());
            dirCell.Items.Add(Side.None.ToString());
            nRow.Cells.Add(dirCell);
            nRow.Cells[6].Value = trade.Side.ToString();

            return nRow;
        }

        private void CheckMyTradeTimeButtonClick(List<MyTrade> trades, DataGridView grid)
        {
            try
            {
                if (grid.SelectedCells == null ||
              grid.SelectedCells.Count == 0)
                {
                    return;
                }

                if (trades == null ||
                    trades.Count == 0)
                {
                    return;
                }

                int tabRow = grid.SelectedCells[0].RowIndex;
                int tabColumn = grid.SelectedCells[0].ColumnIndex;

                if (tabColumn == 3)
                {
                    if (tabRow >= trades.Count)
                    {
                        return;
                    }
                    MyTrade myOrder = trades[tabRow];

                    DateTime time = myOrder.Time;

                    if (myOrder.Time == DateTime.MinValue)
                    {
                        time = DateTime.Now;
                    }
                    else
                    {
                        time = myOrder.Time;
                    }

                    DateTimeSelectionDialog dialog = new DateTimeSelectionDialog(time);
                    dialog.ShowDialog();

                    if (dialog.IsSaved)
                    {
                        myOrder.Time = dialog.Time;
                        grid.Rows[tabRow].Cells[tabColumn].Value = myOrder.Time.ToString(_currentCulture);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private List<MyTrade> GetMyTrades()
        {
            List<MyTrade> trades = new List<MyTrade>();

            List<Order> ordersOpen = _position.OpenOrders;
            List<Order> ordersClose = _position.CloseOrders;

            if (ordersOpen != null && ordersOpen.Count != 0)
            {
                for (int i = 0; i < ordersOpen.Count; i++)
                {
                    if (ordersOpen[i].MyTrades == null ||
                        ordersOpen[i].MyTrades.Count == 0)
                    {
                        continue;
                    }
                    trades.AddRange(ordersOpen[i].MyTrades);
                }
            }

            if (ordersClose != null && ordersClose.Count != 0)
            {
                for (int i = 0; i < ordersClose.Count; i++)
                {
                    if (ordersClose[i].MyTrades == null ||
                        ordersClose[i].MyTrades.Count == 0)
                    {
                        continue;
                    }
                    trades.AddRange(ordersClose[i].MyTrades);
                }
            }

            return trades;
        }

        private void _tradesGrid_Click(object sender, EventArgs e)
        {
            try
            {
                MouseEventArgs mouse = (MouseEventArgs)e;
                if (mouse.Button != MouseButtons.Right)
                {
                    if (_tradesGrid.ContextMenuStrip != null)
                    {
                        _tradesGrid.ContextMenuStrip = null;
                    }

                    CheckMyTradeTimeButtonClick(GetMyTrades(), _tradesGrid);
                    return;
                }

                List<ToolStripMenuItem> items = new List<ToolStripMenuItem>();

                List<Order> ordersOpen = _position.OpenOrders;
                List<Order> ordersClose = _position.CloseOrders;

                if (ordersOpen != null && ordersOpen.Count != 0)
                {
                    List<ToolStripMenuItem> itemsOrdersOpen = new List<ToolStripMenuItem>();
                    for (int i = 0; i < ordersOpen.Count; i++)
                    {
                        itemsOrdersOpen.Add(new ToolStripMenuItem { Text = "Num " + ordersOpen[i].NumberUser });
                        itemsOrdersOpen[itemsOrdersOpen.Count - 1].Click += MyTradeAddInOpenOrders_Click;
                    }

                    var item2 = new ToolStripMenuItem(OsLocalization.Entity.OrderContextMenuItem3);
                    item2.DropDownItems.AddRange(itemsOrdersOpen.ToArray());
                    items.Add(item2);
                }

                if (ordersClose != null && ordersClose.Count != 0)
                {
                    List<ToolStripMenuItem> itemsOrdersClose = new List<ToolStripMenuItem>();
                    for (int i = 0; i < ordersClose.Count; i++)
                    {
                        itemsOrdersClose.Add(new ToolStripMenuItem { Text = "Num " + ordersClose[i].NumberUser });
                        itemsOrdersClose[itemsOrdersClose.Count - 1].Click += MyTradeAddInCloseOrders_Click;
                    }

                    var item2 = new ToolStripMenuItem(OsLocalization.Entity.OrderContextMenuItem4);
                    item2.DropDownItems.AddRange(itemsOrdersClose.ToArray());
                    items.Add(item2);
                }

                items.Add(new ToolStripMenuItem { Text = OsLocalization.Entity.OrderContextMenuItem5 });
                items[items.Count - 1].Click += MyTradeDelete_Click;

                ContextMenuStrip menu = new ContextMenuStrip(); menu.Items.AddRange(items.ToArray());

                _tradesGrid.ContextMenuStrip = menu;
                _tradesGrid.ContextMenuStrip.Show(_tradesGrid, new System.Drawing.Point(mouse.X, mouse.Y));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void MyTradeAddInOpenOrders_Click(object sender, EventArgs e)
        {
            try
            {
                string str = ((ToolStripMenuItem)sender).Text.ToString().Split(' ')[1];
                int ordNum = Convert.ToInt32(str);
                Order myOrd = _position.OpenOrders.Find(o => o.NumberUser == ordNum);

                if (myOrd == null)
                {
                    return;
                }

                MyTrade trade = new MyTrade();
                trade.SecurityNameCode = myOrd.SecurityNameCode;
                trade.Side = myOrd.Side;
                trade.NumberOrderParent = myOrd.NumberMarket.ToString();
                trade.NumberPosition = _position.Number.ToString();
                trade.NumberTrade = NumberGen.GetNumberOrder(_startProgram).ToString();

                if(myOrd.MyTrades != null 
                    && myOrd.MyTrades.Count != 0)
                {
                    MyTrade firstTrade = myOrd.MyTrades[0];

                    if(firstTrade != null)
                    {
                        trade.Time = firstTrade.Time;
                        trade.Price = firstTrade.Price;
                    }
                }

                myOrd.SetTrade(trade);

                SyncPositionWithOrdersAndMyTrades();
                RePaint();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void MyTradeAddInCloseOrders_Click(object sender, EventArgs e)
        {
            try
            {
                if (_position.CloseOrders == null ||
                    _position.CloseOrders.Count == 0)
                {
                    return;
                }

                string str = ((ToolStripMenuItem)sender).Text.ToString().Split(' ')[1];
                int ordNum = Convert.ToInt32(str);
                Order myOrd = _position.CloseOrders.Find(o => o.NumberUser == ordNum);

                if (myOrd == null)
                {
                    return;
                }

                MyTrade trade = new MyTrade();
                trade.SecurityNameCode = myOrd.SecurityNameCode;
                trade.Side = myOrd.Side;
                trade.NumberOrderParent = myOrd.NumberMarket.ToString();
                trade.NumberPosition = _position.Number.ToString();
                trade.NumberTrade = NumberGen.GetNumberOrder(_startProgram).ToString();

                myOrd.SetTrade(trade);

                SyncPositionWithOrdersAndMyTrades();
                RePaint();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void MyTradeDelete_Click(object sender, EventArgs e)
        {
            try
            {
                if (_position.OpenOrders == null)
                {
                    return;
                }
                if (_openOrdersGrid.Rows.Count == 0)
                {
                    return;
                }

                int number;
                try
                {
                    number = _tradesGrid.CurrentCell.RowIndex;
                }
                catch (Exception)
                {
                    return;
                }

                if (number >= _tradesGrid.Rows.Count)
                {
                    return;
                }

                if (ActionDeleteIsAccepted() == false)
                {
                    return;
                }

                string strNum = _tradesGrid.Rows[number].Cells[0].Value.ToString();

                List<Order> openOrders = _position.OpenOrders;
                List<Order> closeOrders = _position.CloseOrders;

                bool isInArray = false;

                for (int i = 0; openOrders != null && i < openOrders.Count; i++)
                {
                    if (isInArray == true)
                    {
                        break;
                    }

                    Order curOrd = openOrders[i];

                    for (int i2 = 0; i2 < curOrd.MyTrades.Count; i2++)
                    {
                        MyTrade curTrade = curOrd.MyTrades[i2];

                        if (curTrade.NumberTrade == strNum)
                        {
                            curOrd.MyTrades.RemoveAt(i2);
                            isInArray = true;
                            break;
                        }
                    }
                    curOrd.ReCalculateVolume();
                }

                for (int i = 0; closeOrders != null && i < closeOrders.Count; i++)
                {
                    if (isInArray == true)
                    {
                        break;
                    }

                    Order curOrd = closeOrders[i];

                    for (int i2 = 0; i2 < curOrd.MyTrades.Count; i2++)
                    {
                        MyTrade curTrade = curOrd.MyTrades[i2];

                        if (curTrade.NumberTrade == strNum)
                        {
                            curOrd.MyTrades.RemoveAt(i2);
                            isInArray = true;
                            break;
                        }
                    }
                    curOrd.ReCalculateVolume();
                }

                RePaint();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        #endregion

        #region Accept ui

        private bool ActionDeleteIsAccepted()
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Entity.MessageAcceptDeleteAction);
            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                return false;
            }

            return true;
        }

        private bool ActionSaveIsAccepted()
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Entity.MessageAcceptSaveAction);
            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region Save changes

        private string GetPortfolioName()
        {
            string result = "";

            for (int i = 0; _position.OpenOrders != null && i < _position.OpenOrders.Count; i++)
            {
                if (string.IsNullOrEmpty(_position.OpenOrders[i].PortfolioNumber) == false)
                {
                    result = _position.OpenOrders[i].PortfolioNumber;
                }
            }

            return result;
        }

        private void SyncPositionWithOrdersAndMyTrades()
        {
            List<Order> openOrders = _position.OpenOrders;
            List<Order> closeOrders = _position.CloseOrders;

            if (_mainPosGrid.Rows[0].Cells[4].Value == null)
            {
                return;
            }

            string securityName = _mainPosGrid.Rows[0].Cells[4].Value.ToString();

            for (int i = 0; openOrders != null && i < openOrders.Count; i++)
            {
                Order curOrd = openOrders[i];
                curOrd.ReCalculateVolume();
                curOrd.SecurityNameCode = securityName;

                List<MyTrade> trades = openOrders[i].MyTrades;

                for (int i2 = 0; trades != null && i2 < trades.Count; i2++)
                {
                    trades[i2].SecurityNameCode = securityName;
                }
            }

            for (int i = 0; closeOrders != null && i < closeOrders.Count; i++)
            {
                Order curOrd = closeOrders[i];
                curOrd.ReCalculateVolume();
                curOrd.SecurityNameCode = securityName;

                List<MyTrade> trades = closeOrders[i].MyTrades;

                for (int i2 = 0; trades != null && i2 < trades.Count; i2++)
                {
                    trades[i2].SecurityNameCode = securityName;
                }
            }
        }

        private void SaveChangesButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                if (ActionSaveIsAccepted() == false)
                {
                    return;
                }

                try
                {
                    _position.PortfolioValueOnOpenPosition = Convert.ToDecimal(TextBoxStartDepo.Text);
                }
                catch
                {
                    // ignore
                }


                SyncPositionWithOrdersAndMyTrades();
                SavePosition();
                SaveMyTrades();
                SaveOrders(_position.OpenOrders, _openOrdersGrid.Rows);
                SaveOrders(_position.CloseOrders, _closeOrdersGrid.Rows);
              

                PositionChanged = true;

                Close();
            }
            catch (Exception ex)
            {
                CustomMessageBoxUi box = new CustomMessageBoxUi(ex.Message);
                box.ShowDialog();
                return;
            }
        }

        public bool PositionChanged;

        private void SaveOrders(List<Order> orders, DataGridViewRowCollection rows)
        {
            if (orders == null || orders.Count == 0)
            {
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                Order order = orders[i];
                DataGridViewRow row = rows[i];

                order.PortfolioNumber = row.Cells[4].Value.ToString();

                Enum.TryParse(row.Cells[5].Value.ToString(), out order.Side);

                OrderStateType state;

                if (Enum.TryParse(row.Cells[6].Value.ToString(), out state))
                {
                    order.State = state;
                }

                order.Price = row.Cells[7].Value.ToString().ToDecimal();
                order.Volume = row.Cells[9].Value.ToString().ToDecimal();

                Enum.TryParse(row.Cells[10].Value.ToString(), out order.TypeOrder);
            }
        }

        private void SavePosition()
        {
            Position position = _position;

            DataGridViewRow nRow = _mainPosGrid.Rows[0];

            if (nRow.Cells[4].Value != null)
            {
                position.SecurityName = nRow.Cells[4].Value.ToString();
            }

            Enum.TryParse(nRow.Cells[5].Value.ToString(), out position.Direction);

            PositionStateType newState;

            if (Enum.TryParse(nRow.Cells[6].Value.ToString(), out newState))
            {
                position.State = newState;
            }

            try
            {
                position.StopOrderRedLine = nRow.Cells[13].Value.ToString().ToDecimal();
                position.StopOrderPrice = nRow.Cells[14].Value.ToString().ToDecimal();
                position.ProfitOrderRedLine = nRow.Cells[15].Value.ToString().ToDecimal();
                position.ProfitOrderPrice = nRow.Cells[16].Value.ToString().ToDecimal();
                if (nRow.Cells[17].Value != null)
                {
                    position.SignalTypeOpen = nRow.Cells[17].Value.ToString().RemoveExcessFromSecurityName();
                }

                if (nRow.Cells[18].Value != null)
                {
                    position.SignalTypeClose = nRow.Cells[18].Value.ToString().RemoveExcessFromSecurityName();
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
        }

        private void SaveMyTrades()
        {
            List<MyTrade> allTrades = new List<MyTrade>();

            for (int i = 0; _position.OpenOrders != null && i < _position.OpenOrders.Count; i++)
            {
                List<MyTrade> trades = _position.OpenOrders[i].MyTrades;

                if (trades == null || trades.Count == 0)
                {
                    continue;
                }

                allTrades.AddRange(trades);
            }

            for (int i = 0; _position.CloseOrders != null && i < _position.CloseOrders.Count; i++)
            {
                List<MyTrade> trades = _position.CloseOrders[i].MyTrades;

                if (trades == null || trades.Count == 0)
                {
                    continue;
                }

                allTrades.AddRange(trades);
            }

            for (int i = 0; i < allTrades.Count; i++)
            {
                SaveMyTradeState(allTrades[i]);
            }
        }

        private void SaveMyTradeState(MyTrade trade)
        {
            DataGridViewRowCollection rows = _tradesGrid.Rows;

            for (int i = 0; i < rows.Count; i++)
            {
                string num = rows[i].Cells[0].Value.ToString();

                if (trade.NumberTrade == num)
                {
                    trade.Price = rows[i].Cells[4].Value.ToString().ToDecimal();
                    trade.Volume = rows[i].Cells[5].Value.ToString().ToDecimal();
                    Enum.TryParse(rows[i].Cells[6].Value.ToString(), out trade.Side);
                }
            }
        }

        #endregion
    }
}
