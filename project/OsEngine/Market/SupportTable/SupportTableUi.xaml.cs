/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;

namespace OsEngine.Market.SupportTable
{
    /// <summary>
    /// Interaction logic for SupportTableUi.xaml
    /// </summary>
    public partial class SupportTableUi : Window
    {
        public SupportTableUi()
        {
            InitializeComponent();

            CreateTables();
            PaintTables();
            this.Closed += SupportTableUi_Closed;

            Title = OsLocalization.Market.Label77;

            TabItem1.Header = OsLocalization.Market.Label78;
            TabItem2.Header = OsLocalization.Market.Label79;
            TabItem3.Header = OsLocalization.Market.Label80;
            TabItem4.Header = OsLocalization.Market.Label81;

            LabelPrimeSup.Content = OsLocalization.Market.Label74;
            LabelStandartSup.Content = OsLocalization.Market.Label75;
            LabelNoSup.Content = OsLocalization.Market.Label76;
        }

        private void SupportTableUi_Closed(object sender, EventArgs e)
        {
            try
            {
                if (HostMoexConnections != null)
                {
                    HostMoexConnections.Child = null;
                    HostMoexConnections = null;
                }

                if (HostInternationalConnections != null)
                {
                    HostInternationalConnections.Child = null;
                    HostInternationalConnections = null;
                }

                if (HostCryptoConnections != null)
                {
                    HostCryptoConnections.Child = null;
                    HostCryptoConnections = null;
                }

                if (_gridMoex != null)
                {
                    _gridMoex.DataError -= _gridMoex_DataError;
                    DataGridFactory.ClearLinks(_gridMoex);
                    _gridMoex = null;
                }

                if (_gridInternational != null)
                {
                    _gridInternational.DataError -= _gridMoex_DataError;
                    DataGridFactory.ClearLinks(_gridInternational);
                    _gridInternational = null;
                }

                if (_gridCrypto != null)
                {
                    _gridCrypto.CellClick -= _gridCrypto_CellClick1;
                    _gridCrypto.DataError -= _gridMoex_DataError;
                    DataGridFactory.ClearLinks(_gridCrypto);
                    _gridCrypto = null;
                }
            }
            catch
            {
                // ignore
            }
        }

        DataGridView _gridMoex;
        DataGridView _gridInternational;
        DataGridView _gridCrypto;

        private void CreateTables()
        {
            try
            {
                _gridMoex = GetNoDiscountGridSupport();
                HostMoexConnections.Child = _gridMoex;
                _gridMoex.DataError += _gridMoex_DataError;


                _gridInternational = GetNoDiscountGridSupport();
                HostInternationalConnections.Child = _gridInternational;
                _gridInternational.DataError += _gridMoex_DataError;

                _gridCrypto = GetDiscountGridSupport();
                HostCryptoConnections.Child = _gridCrypto;
                _gridCrypto.CellClick += _gridCrypto_CellClick1;
                _gridCrypto.DataError += _gridMoex_DataError;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message.ToString());
            }
        }

        private void _gridMoex_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            System.Windows.MessageBox.Show(e.ToString());
        }

        private DataGridView GetDiscountGridSupport()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCells);

            // 0 лого
            // 1 Имя
            // 2 Поддержка
            // 3 Скидка

            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            newGrid.ScrollBars = ScrollBars.Vertical;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;


            // Logo
            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Market.Label69;
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colum0.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            newGrid.Columns.Add(colum0);

            // Name
            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = OsLocalization.Market.Label70;
            colum1.ReadOnly = true;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colum1.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            newGrid.Columns.Add(colum1);

            // Support
            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = OsLocalization.Market.Label71;
            colum2.ReadOnly = true;
            colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colum2.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            newGrid.Columns.Add(colum2);

            // Discount
            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = OsLocalization.Market.Label72;
            colum3.ReadOnly = true;
            colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colum3.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            newGrid.Columns.Add(colum3);

            // Get discount
            DataGridViewColumn colum4 = new DataGridViewColumn();
            colum4.CellTemplate = cell0;
            colum4.ReadOnly = true;
            colum4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colum4.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            newGrid.Columns.Add(colum4);

            return newGrid;
        }

        private DataGridView GetNoDiscountGridSupport()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, 
                DataGridViewAutoSizeRowsMode.AllCells);

            // 0 лого
            // 1 Имя
            // 2 Поддержка
            // 3 Скидка

            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            newGrid.ScrollBars = ScrollBars.Vertical;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;
            
            // Logo
            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Market.Label69;
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colum0.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            newGrid.Columns.Add(colum0);

            // Name
            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = OsLocalization.Market.Label70;
            colum1.ReadOnly = true;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colum1.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            newGrid.Columns.Add(colum1);

            // Support
            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = OsLocalization.Market.Label71;
            colum2.ReadOnly = true;
            colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colum2.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            newGrid.Columns.Add(colum2);

            return newGrid;
        }

        private void PaintTables()
        {
            try
            {
                PaintTableNoDiscount(_gridMoex, SupportTableBase.GetMoexSupportList());
                PaintTableNoDiscount(_gridInternational, SupportTableBase.GetInternationalSupportList());
                PaintTableWithDiscount(_gridCrypto, SupportTableBase.GetCryptoSupportList());
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message.ToString());
            }
        }

        private void PaintTableNoDiscount(DataGridView grid, List<SupportConnection> connections)
        {
            grid.Rows.Clear();

            for(int i = 0;i < connections.Count;i++)
            {
                DataGridViewRow row = GetRowNoDiscount(connections[i]);
                grid.Rows.Add(row);
            }
        }

        private void PaintTableWithDiscount(DataGridView grid, List<SupportConnection> connections)
        {
            grid.Rows.Clear();

            for (int i = 0; i < connections.Count; i++)
            {
                DataGridViewRow row = GetRowWithDiscount(connections[i]);
                grid.Rows.Add(row);
            }
        }

        private DataGridViewRow GetRowNoDiscount(SupportConnection connection)
        {
            // 0 лого
            // 1 Имя
            // 2 Поддержка

            DataGridViewRow nRow = new DataGridViewRow();

            DataGridViewImageCell cellImage = new DataGridViewImageCell();

            // Сначала запихать файл в файловую систему???

            System.Drawing.Image image = System.Drawing.Image.FromFile(Environment.CurrentDirectory + connection.LinqToLogo);

            cellImage.Value = image;
            
            nRow.Cells.Add(cellImage);
            nRow.Cells[0].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = connection.ServerType.ToString();
            nRow.Cells[1].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            nRow.Cells.Add(new DataGridViewImageCell());
            nRow.Cells[2].Value = GetSupprotImage(connection.SupportType);
            nRow.Cells[2].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            nRow.Height = nRow.Height + 5;

            return nRow;
        }

        private DataGridViewRow GetRowWithDiscount(SupportConnection connection)
        {
            // 0 лого
            // 1 Имя
            // 2 Поддержка
            // 3 Скидка
            // 4 Получить скидку кнопка

            DataGridViewRow nRow = new DataGridViewRow();

            DataGridViewImageCell cellImage = new DataGridViewImageCell();

            // Сначала запихать файл в файловую систему???

            System.Drawing.Image image = System.Drawing.Image.FromFile(Environment.CurrentDirectory + connection.LinqToLogo);

            cellImage.Value = image;

            nRow.Cells.Add(cellImage);
            nRow.Cells[0].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = connection.ServerType.ToString();
            nRow.Cells[1].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            nRow.Cells.Add(new DataGridViewImageCell());
            nRow.Cells[2].Value = GetSupprotImage(connection.SupportType);
            nRow.Cells[2].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            nRow.Cells.Add(new DataGridViewImageCell());
            nRow.Cells[3].Value = GetDiscountImage(connection.Discount);
            nRow.Cells[3].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            if(connection.Discount != 0)
            {
                nRow.Cells.Add(new DataGridViewButtonCell());
                nRow.Cells[4].Value = OsLocalization.Market.Label73;
                nRow.Cells[4].ToolTipText = connection.LingSiteUrl;
                nRow.Cells[4].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            else
            {
                nRow.Cells.Add(new DataGridViewTextBoxCell());
            }

            nRow.Height = nRow.Height + 5;

            return nRow;
        }

        private System.Drawing.Image GetDiscountImage(int discount)
        {
            string pref = "";

            if (discount == 0)
            {
                pref = "\\Images\\Connections\\Support\\No.png";
            }
            else
            {
                pref = "\\Images\\Connections\\Discounts\\" + discount.ToString() + ".png";
            }

            System.Drawing.Image image = 
                System.Drawing.Image.FromFile(Environment.CurrentDirectory + pref);

            return image;
        }

        private System.Drawing.Image GetSupprotImage(SupportServerType typeSup)
        {
            string pref = "";

            if (typeSup == SupportServerType.No)
            {
                pref = "\\Images\\Connections\\Support\\No.png";
            }
            else if (typeSup == SupportServerType.Standart)
            {
                pref = "\\Images\\Connections\\Support\\Standart.png";
            }
            else if (typeSup == SupportServerType.Prime)
            {
                pref = "\\Images\\Connections\\Support\\Prime.png";
            }

            System.Drawing.Image image =
                System.Drawing.Image.FromFile(Environment.CurrentDirectory + pref);

            return image;
        }

        private void _gridCrypto_CellClick1(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;
                int col = e.ColumnIndex;

                if (col != 4)
                {
                    return;
                }

                if (row >= _gridCrypto.Rows.Count)
                {
                    return;
                }

                string link = _gridCrypto.Rows[row].Cells[4].ToolTipText;

                Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message.ToString());
            }
        }
    }
}
