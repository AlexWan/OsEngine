/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using ContextMenu = System.Windows.Forms.ContextMenu;
using DataGrid = System.Windows.Forms.DataGrid;
using MenuItem = System.Windows.Forms.MenuItem;
using MessageBox = System.Windows.Forms.MessageBox;
using Point = System.Drawing.Point;

namespace OsEngine.OsData
{

    /// <summary>
    /// Set Storage Wizard/мастер хранения сетов 
    /// </summary>
    public class OsDataMaster
    {
        /// <summary>
        /// constructor/конструктор
        /// </summary>
        /// <param name="hostChart">chart host/хост для чарта</param>
        /// <param name="hostLog">log host/хост для лога</param>
        /// <param name="hostSource">source host/хост для источников</param>
        /// <param name="hostSets">host for sets/хост для сетов</param>
        /// <param name="comboBoxSecurity">paper selection menu/меню выбора бумаг</param>
        /// <param name="comboBoxTimeFrame">time frame selection menu/меню выбора таймфрейма</param>
        /// <param name="rectangle">square for substrate/квадрат для подложки</param>
        public OsDataMaster(WindowsFormsHost hostChart, WindowsFormsHost hostLog,
            WindowsFormsHost hostSource, WindowsFormsHost hostSets, System.Windows.Controls.ComboBox comboBoxSecurity,
            System.Windows.Controls.ComboBox comboBoxTimeFrame, System.Windows.Shapes.Rectangle rectangle, Grid greedChartPanel)
        {
            _hostChart = hostChart;
            _hostSource = hostSource;
            _hostSets = hostSets;
            _comboBoxSecurity = comboBoxSecurity;
            _comboBoxTimeFrame = comboBoxTimeFrame;
            _rectangle = rectangle;
            _greedChartPanel = greedChartPanel;
            _log = new Log("OsDataMaster", StartProgram.IsOsData);
            _log.StartPaint(hostLog);
            _log.Listen(this);

            Load();
            LoadSettings();

            CreateSetGrid();
            RePaintSetGrid();

            CreateSourceGrid();
            RePaintSourceGrid();

            ServerMaster.ServerCreateEvent += ServerMaster_ServerCreateEvent;
        }

        void ServerMaster_ServerCreateEvent(IServer server)
        {
            List<IServer> servers = ServerMaster.GetServers();

            for (int i = 0; i < servers.Count; i++)
            {
                if (servers[i].ServerType == ServerType.Optimizer)
                {
                    continue;
                }
                servers[i].ConnectStatusChangeEvent -= ServerStatusChangeEvent;
                servers[i].LogMessageEvent -= OsDataMaster_LogMessageEvent;

                servers[i].ConnectStatusChangeEvent += ServerStatusChangeEvent;
                servers[i].LogMessageEvent += OsDataMaster_LogMessageEvent;

            }
            RePaintSourceGrid();
        }

        public void StopPaint()
        {
			_isPaintEnabled = false;
            if (_selectSet != null)
            {
                _selectSet.StopPaint();
            }
            
        }

        public void StartPaint()
        {
			_isPaintEnabled = true;
            if (_selectSet != null)
            {
                _selectSet.StartPaint(_hostChart, _rectangle, _greedChartPanel);
            }
            
        }

        void OsDataMaster_LogMessageEvent(string message, LogMessageType type)
        {
            SendNewLogMessage(message, type);
        }

        /// <summary>
        /// chart host/хост для чарта
        /// </summary>
        private WindowsFormsHost _hostChart;

        /// <summary>
        /// server host/хост для серверов
        /// </summary>
        private WindowsFormsHost _hostSource;

        /// <summary>
        /// host for sets/хост для сетов
        /// </summary>
        private WindowsFormsHost _hostSets;

        /// <summary>
        /// дата грид для других видов чарта
        /// </summary>
        private Grid _greedChartPanel;

        /// <summary>
        /// rectangle for the substrate/прямоугольник для подложки
        /// </summary>
        private System.Windows.Shapes.Rectangle _rectangle;

        /// <summary>
        /// tool selection menu/меню выбора инструмента
        /// </summary>
        private System.Windows.Controls.ComboBox _comboBoxSecurity;

        /// <summary>
        /// time frame selection menu/меню выбора таймфрейма
        /// </summary>
        private System.Windows.Controls.ComboBox _comboBoxTimeFrame;

        /// <summary>
        /// load settings from file/загрузить настройки из файла
        /// </summary>
        private void Load()
        {
            if (!Directory.Exists("Data"))
            {
                Directory.CreateDirectory("Data");
            }

            // folder name is our name of the set/название папок это у нас название сетов

            string[] folders = Directory.GetDirectories("Data");

            if (folders == null ||
                folders.Length == 0)
            {
                return;
            }

            string[] nameFolders = new string[folders.Length];

            for (int i = 0; i < folders.Length; i++)
            {
                nameFolders[i] = folders[i].Split('\\')[1];
            }

                _sets = new List<OsDataSet>();

                for (int i = 0; i < nameFolders.Length; i++)
            {
                if (nameFolders[i].Split('_')[0] == "Set")
                {
                    _sets.Add(new OsDataSet(nameFolders[i],_comboBoxSecurity,_comboBoxTimeFrame));
                    _sets[_sets.Count-1].NewLogMessageEvent += SendNewLogMessage;

                }
            }
        }

        /// <summary>
        /// log/лог
        /// </summary>
        private Log _log;

        /// <summary>
        /// send new message to log/выслать новое сообщение в лог
        /// </summary>
        void SendNewLogMessage(string message, LogMessageType type)
        {
            if (NewLogMessageEvent != null)
            {
                NewLogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// new message event to log/событие нового сообщения в лог
        /// </summary>
        public event Action<string, LogMessageType> NewLogMessageEvent;

        /// <summary>
        /// source table/таблица источников
        /// </summary>
        private DataGridView _gridSources;

        /// <summary>
        /// create source table/создать таблицу источников
        /// </summary>
        private void CreateSourceGrid()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Data.Label4;
            colum0.ReadOnly = true;
            colum0.Width = 100;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colu = new DataGridViewColumn();
            colu.CellTemplate = cell0;
            colu.HeaderText = OsLocalization.Data.Label5;
            colu.ReadOnly = true;
            colu.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colu);

            _gridSources = newGrid;
            _gridSources.DoubleClick += _gridSources_DoubleClick;
            _hostSource.Child = _gridSources;
            _hostSource.VerticalAlignment = VerticalAlignment.Top;
        }

        /// <summary>
        /// redraw the source table/перерисовать таблицу источников
        /// </summary>
        private void RePaintSourceGrid()
        {
            if (_gridSources.InvokeRequired)
            {
                _gridSources.Invoke(new Action(RePaintSourceGrid));
                return;
            }

            _gridSources.Rows.Clear();

            List<ServerType> servers = ServerMaster.ServersTypes;

            List<IServer> serversCreate = ServerMaster.GetServers();

            if (serversCreate == null)
            {
                serversCreate = new List<IServer>();
            }

            for (int i = 0; i < servers.Count; i++)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = servers[i];
                row1.Cells.Add(new DataGridViewTextBoxCell());

                IServer server = serversCreate.Find(s => s.ServerType == servers[i]);

                if (server == null)
                {
                    row1.Cells[1].Value = "Disabled";
                }
                else if (server != null && server.ServerStatus == ServerConnectStatus.Connect)
                {
                    row1.Cells[1].Value = "Connect";
                    DataGridViewCellStyle style = new DataGridViewCellStyle();
                    style.BackColor = Color.MediumSeaGreen;
                    style.SelectionBackColor = Color.Green;
                    style.ForeColor = Color.Black;
                    style.SelectionForeColor = Color.Black;
                    row1.Cells[1].Style = style;
                    row1.Cells[0].Style = style;
                }
                else
                {
                    row1.Cells[1].Value = "Disconnect";
                    DataGridViewCellStyle style = new DataGridViewCellStyle();
                    style.BackColor = Color.Coral;
                    style.SelectionBackColor = Color.Chocolate;
                    style.ForeColor = Color.Black;
                    style.SelectionForeColor = Color.Black;
                    row1.Cells[1].Style = style;
                    row1.Cells[0].Style = style;
                }

                _gridSources.Rows.Add(row1);
            }
            _gridSources[1, 0].Selected = true; // Select an invisible line to remove the default selection from the grid./Выбрать невидимую строку, чтобы убрать выделение по умолчанию с грида.
            _gridSources.ClearSelection();

        }

        /// <summary>
        /// double click event on the source table/событие двойного клика на таблицу источников
        /// </summary>
        void _gridSources_DoubleClick(object sender, EventArgs e)
        {
            if (_gridSources.CurrentCell.RowIndex <= -1)
            {
                return;
            }

            ServerType type;
            Enum.TryParse(_gridSources.Rows[_gridSources.CurrentCell.RowIndex].Cells[0].Value.ToString(), out type);

            if (ServerMaster.GetServers() == null)
            {
                ServerMaster.CreateServer(type,false);
            }

            IServer server = ServerMaster.GetServers().Find(s => s.ServerType == type);

            if (server == null)
            {
                ServerMaster.CreateServer(type, false);
                server = ServerMaster.GetServers().Find(s => s.ServerType == type);
            }

            server.ShowDialog();
        }

        /// <summary>
        /// server status change event/событие изменения статуса сервера
        /// </summary>
        /// <param name="newState"></param>
        void ServerStatusChangeEvent(string newState)
        {
            RePaintSourceGrid();
        }

        // work with the grid/работа с гридом

        /// <summary>
        /// table for sets/таблица для сетов
        /// </summary>
        private DataGridView _gridset;

        /// <summary>
        /// create table for sets/создать таблицу для сетов
        /// </summary>
        private void CreateSetGrid()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = OsLocalization.Data.Label3;
            colum0.ReadOnly = true;
            colum0.Width = 100;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colu = new DataGridViewColumn();
            colu.CellTemplate = cell0;
            colu.HeaderText = OsLocalization.Data.Label5;
            colu.ReadOnly = true;
            colu.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colu);

            _gridset = newGrid;
            _gridset.Click += _gridset_Click;
            _gridset.DoubleClick += _gridset_DoubleClick;
            _hostSets.Child = _gridset;
        }

        /// <summary>
        /// redraw the set table/перерисовать таблицу сетов
        /// </summary>
        private void RePaintSetGrid()
        {
            try
            {
                if (_gridset.InvokeRequired)
                {
                    _gridset.Invoke(new Action(RePaintSetGrid));
                    return;
                }
                _gridset.Rows.Clear();

                for (int i = 0; _sets != null && i < _sets.Count; i++)
                {
                    DataGridViewRow nRow = new DataGridViewRow();

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[0].Value = _sets[i].SetName;

                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[1].Value = _sets[i].Regime;

                    if (_sets[i].Regime == DataSetState.On)
                    {
                        DataGridViewCellStyle style = new DataGridViewCellStyle();
                        style.BackColor = Color.MediumSeaGreen;
                        style.SelectionBackColor = Color.Green;
                        style.ForeColor = Color.Black;
                        style.SelectionForeColor = Color.Black;
                        nRow.Cells[1].Style = style;
                        nRow.Cells[0].Style = style;
                    }

                    _gridset.Rows.Add(nRow);
                }
                if (_gridset.Rows.Count != 0)
                {
                    _gridset[0, 0].Selected = true; // Select an invisible line to remove the default selection from the grid./Выбрать невидимую строку, чтобы убрать выделение по умолчанию с грида.
                    _gridset.ClearSelection();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// double-click event on the table of sets/событие двойного клика по таблицу сетов
        /// </summary>
        private void _gridset_DoubleClick(object sender, EventArgs e)
        {
            if (_gridset.CurrentCell == null ||
                _gridset.CurrentCell.RowIndex <= -1)
            {
                return;
            }
            int _rowIndex = _gridset.CurrentCell.RowIndex;
            RedactThisSet(_rowIndex);
            RePaintSetGrid();
            _gridset.Rows[_rowIndex].Selected = true; // Return focus to the line you edited./Вернуть фокус на строку, которую редактировал.
        }

        /// <summary>
        /// single click on the table of sets/одиночный клик по таблице сетов
        /// </summary>
        void _gridset_Click(object sender, EventArgs e)
        {
            MouseEventArgs mouse = (MouseEventArgs)e;

            if (mouse.Button != MouseButtons.Right)
            {
                if (_gridset.CurrentCell != null)
                {
                    ChangeActivSet(_gridset.CurrentCell.RowIndex);
                }
                
                return;
            }

            // creating a context menu/cоздание контекстного меню

            MenuItem[] items = new MenuItem[3];

            items[0] = new MenuItem();
            items[0].Text = OsLocalization.Data.Label6;
            items[0].Click += AddSet_Click;

            items[1] = new MenuItem() { Text = OsLocalization.Data.Label7 };
            items[1].Click += RedactSet_Click;

            items[2] = new MenuItem() { Text = OsLocalization.Data.Label8 };
            items[2].Click += DeleteSet_Click;

            ContextMenu menu = new ContextMenu(items);

            _gridset.ContextMenu = menu;
            _gridset.ContextMenu.Show(_gridset, new Point(mouse.X, mouse.Y));
        }

        /// <summary>
        /// create new set/создать новый сет
        /// </summary>
        private void AddSet_Click(object sender, EventArgs e)
        {
            CreateNewSet();
        }

        /// <summary>
        /// edit set/редактировать сет
        /// </summary>
        void RedactSet_Click(object sender, EventArgs e)
        {
            if (_gridset.CurrentCell == null ||
                _gridset.CurrentCell.RowIndex <= -1)
            {
                return;
            }
            RedactThisSet(_gridset.CurrentCell.RowIndex);
        }

        /// <summary>
        /// delete set/удалить сет
        /// </summary>
        void DeleteSet_Click(object sender, EventArgs e)
        {
            if (_gridset.CurrentCell == null || 
                _gridset.CurrentCell.RowIndex <= -1)
            {
                return;
            }

            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Data.Label9);
            ui.ShowDialog();

            if (ui.UserAcceptActioin == false)
            {
                return;
            }
            DeleteThisSet(_gridset.CurrentCell.RowIndex);
        }

        private List<OsDataSet> _sets;

// set switching/переключение сетов

        /// <summary>
        /// active set/активный сет
        /// </summary>
        private OsDataSet _selectSet;

        /// <summary>
        /// Is drawing graphics enabled/Включена ли прорисовка графика
        /// </summary>
        private bool _isPaintEnabled = true;
        public bool IsPaintEnabled
        {
            get { return _isPaintEnabled; }
        }

        /// <summary>
        /// change active set/сменить активный сет
        /// </summary>
        /// <param name="index">new index/индекс нового</param>
        private void ChangeActivSet(int index)
        {
            if (_sets == null ||
                _sets.Count == 0)
            {
                return;
            }

            if (index > _sets.Count)
            {
                return;
            }

            OsDataSet currentSet = _sets[index];
            if (currentSet == _selectSet)
            {
                return;
            }

            if (_selectSet != null && 
                currentSet.SetName != _selectSet.SetName)
            {
                _selectSet.StopPaint();
            }
           

            _selectSet = currentSet;
            if (_isPaintEnabled)
            {
                currentSet.StartPaint(_hostChart, _rectangle, _greedChartPanel);
            }
        }

        // management/управление        

        /// <summary>
        /// create new set/создать новый сет
        /// </summary>
        public void CreateNewSet()
        {
            if (_sets == null)
            {
                _sets = new List<OsDataSet>();
            }
            OsDataSet set = new OsDataSet("Set_",_comboBoxSecurity,_comboBoxTimeFrame);
            set.NewLogMessageEvent += SendNewLogMessage;

            if (!set.ShowDialog())
            { // the user did not press the accept button in the form/пользователь не нажал на кнопку принять в форме
                set.Regime = DataSetState.Off;
                set.Delete();
                return;
            }

            if (set.SetName == "Set_")
            {
                set.Regime = DataSetState.Off;
                set.Delete();
                MessageBox.Show(OsLocalization.Data.Label10);
                return;
            }

            if (_sets.Find(dataSet => dataSet.SetName == set.SetName) != null)
            {
                MessageBox.Show(OsLocalization.Data.Label11);
                set.Regime = DataSetState.Off;
                set.Delete();
                return;
            }

            _sets.Add(set);
            RePaintSetGrid();
            set.Save();
            ChangeActivSet(_sets.Count - 1);
        }

        /// <summary>
        /// delete set by index/удалить сет по индексу
        /// </summary>
        public void DeleteThisSet(int num)
        {
            if (_sets == null)
            {
                _sets = new List<OsDataSet>();
            }

            if (num >= _sets.Count)
            {
                return;
            }
            _sets[num].Delete();
            _sets[num].Regime =  DataSetState.Off;
            _sets.RemoveAt(num);
            RePaintSetGrid();
        }

        /// <summary>
        /// edit set by index/редактировать сет по индексу
        /// </summary>
        public void RedactThisSet(int num)
        {
            if (_sets == null)
            {
                _sets = new List<OsDataSet>();
            }

            if (num >= _sets.Count)
            {
                return;
            }

            if (_sets[num].ShowDialog())
            {
                _sets[num].Save();
            }
        }
        /// <summary>
        /// save settings/сохранить настройки
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                if (!Directory.Exists("Data\\"))
                {
                    Directory.CreateDirectory("Data\\");
                }
                using (StreamWriter writer = new StreamWriter("Data\\Settings.txt", false))
                {
                    writer.WriteLine(_isPaintEnabled);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        /// <summary>
        /// load settings/загрузить настройки
        /// </summary>
        private void LoadSettings()
        {
            if (!File.Exists("Data\\Settings.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader("Data\\Settings.txt"))
                {
                    _isPaintEnabled = Convert.ToBoolean(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }
}
