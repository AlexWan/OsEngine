using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.IO;
//using System.Windows.Media;

using OsEngine.Market;
using OsEngine.Language;
using OsEngine.Entity;
using System.Windows.Forms;

namespace OsEngine.OsData
{
    /// <summary>
    /// Логика взаимодействия для OsDataConnectorList.xaml
    /// </summary>
    public partial class OsDataConnectorList : Window
    {
		private DataGridView _grid;
		private List<ServerType> availableServers;

		public OsDataConnectorList()
        {
            InitializeComponent();

			availableServers = ServerMaster.ServersTypes;

			SysInitTheGrid();
			SysPushDataToTheGrid();
		}

        private void ButtonSaveClose_Click(object sender, RoutedEventArgs e)
        {
			this.Close();
        }
		private DataGridViewColumn SysCreateGridColumn(DataGridViewCell inCell, string inHeader)
		{
			DataGridViewColumn ret = new DataGridViewColumn();
			ret.CellTemplate = inCell;
			ret.HeaderText = inHeader;
			ret.ReadOnly = true;
			return ret;
		}
		private void SysInitTheGrid(){
			DataGridViewTextBoxCell cellName = new DataGridViewTextBoxCell();
			DataGridViewCheckBoxCell cellState = new DataGridViewCheckBoxCell();
			DataGridViewColumn currColumn;

			_grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None);
			_grid.AllowUserToResizeColumns = false;
			_grid.AllowUserToResizeRows = false;

			cellState.Style = _grid.DefaultCellStyle;
			cellState.Style = _grid.DefaultCellStyle;

			currColumn = SysCreateGridColumn(cellState, OsLocalization.Data.Label5);
			currColumn.Width = 36;
			_grid.Columns.Add(currColumn);

			currColumn = SysCreateGridColumn(cellName, OsLocalization.Data.Label4);
			currColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
			_grid.Columns.Add(currColumn);

			_grid.Click += CheckBoxPaintOnOff_Click;
			
			hSource.Child = _grid;

			//    _gridSources = newGrid;
			//    _gridSources.DoubleClick += _gridSources_DoubleClick;		//	реализовать это
			//    _hostSource.Child = _gridSources;
			//   _hostSource.VerticalAlignment = VerticalAlignment.Top;
		}


		private void SysPushDataToTheGrid(){
			if(_grid.InvokeRequired){
				_grid.Invoke(new Action(SysPushDataToTheGrid));
				return;
			}

			DataGridViewRow currRow;
			_grid.Rows.Clear();

			List<string> usedServers = UsedServersGet();

			for (int i=0; i< availableServers.Count; i++){
				currRow = new DataGridViewRow();
				currRow.Cells.Add(new DataGridViewCheckBoxCell());
				currRow.Cells.Add(new DataGridViewTextBoxCell());

				currRow.Cells[0].Value = usedServers.Contains(availableServers[i].ToString());
				currRow.Cells[0].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
				currRow.Cells[1].Value = availableServers[i];
				currRow.Cells[1].Style.Alignment = DataGridViewContentAlignment.MiddleLeft;

				_grid.Rows.Add(currRow);
			}
		}

		public void UsedServersSave(List<string> inUsedServers)
		{
			try
			{
				if (!Directory.Exists("Data\\"))
				{
					Directory.CreateDirectory("Data\\");
				}
				using (StreamWriter writer = new StreamWriter("Data\\UsedServers.txt", false))
				{
					foreach (string currStr in inUsedServers)
					{
						writer.WriteLine(currStr);
					}
					writer.Close();
				}
			}
			catch (Exception)
			{
				// ignored
			}
		}

		public static List<string> UsedServersGet()
		{
			List<string> ret = new List<string>();
			string currStr;

			if (!File.Exists("Data\\UsedServers.txt")) { return ret; }

			using (StreamReader reader = new StreamReader("Data\\UsedServers.txt"))
			{
				while (!reader.EndOfStream)
				{
					currStr = reader.ReadLine();
					ret.Add(currStr);
				}
				reader.Close();
			}

			return ret;
		}

		public void CheckBoxPaintOnOff_Click(object sender, EventArgs e){

			DataGridView thisGrid = sender as DataGridView;
			if (thisGrid is null) { return; }

			int cRow = thisGrid.CurrentCell.RowIndex;
			thisGrid.Rows[cRow].Cells[0].Value = !(bool)(thisGrid.Rows[cRow].Cells[0].Value);

			//System.Windows.MessageBox.Show(" selected " + thisGrid.CurrentCell.RowIndex);

			List<string> usedServers = new List<string>();
			foreach(DataGridViewRow currRow in thisGrid.Rows){
				if((bool)currRow.Cells[0].Value == true){
					usedServers.Add(currRow.Cells[1].Value.ToString());
				}
			}
			UsedServersSave(usedServers);
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{

		}
	}
}
