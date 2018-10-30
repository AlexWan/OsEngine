/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using MessageBox = System.Windows.Forms.MessageBox;
using Point = System.Drawing.Point;

namespace OsEngine.OsData
{

    /// <summary>
    /// мастер хранения сетов 
    /// </summary>
    public class OsDataMaster
    {
        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="hostChart">хост для чарта</param>
        /// <param name="hostLog">хост для лога</param>
        /// <param name="hostSource">хост для источников</param>
        /// <param name="hostSets">хост для сетов</param>
        /// <param name="comboBoxSecurity">меню выбора бумаг</param>
        /// <param name="comboBoxTimeFrame">меню выбора таймфрейма</param>
        /// <param name="rectangle">квадрат для подложки</param>
        public OsDataMaster(WindowsFormsHost hostChart, WindowsFormsHost hostLog,
            WindowsFormsHost hostSource, WindowsFormsHost hostSets, System.Windows.Controls.ComboBox comboBoxSecurity,
            System.Windows.Controls.ComboBox comboBoxTimeFrame, System.Windows.Shapes.Rectangle rectangle)
        {
            _hostChart = hostChart;
            _hostSource = hostSource;
            _hostSets = hostSets;
            _comboBoxSecurity = comboBoxSecurity;
            _comboBoxTimeFrame = comboBoxTimeFrame;
            _rectangle = rectangle;

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
                _selectSet.StartPaint(_hostChart, _rectangle);
            }
            
        }

        void OsDataMaster_LogMessageEvent(string message, LogMessageType type)
        {
            SendNewLogMessage(message, type);
        }

        /// <summary>
        /// хост для чарта
        /// </summary>
        private WindowsFormsHost _hostChart;

        /// <summary>
        /// хост для серверов
        /// </summary>
        private WindowsFormsHost _hostSource;

        /// <summary>
        /// хост для сетов
        /// </summary>
        private WindowsFormsHost _hostSets;

        /// <summary>
        /// прямоугольник для подложки
        /// </summary>
        private System.Windows.Shapes.Rectangle _rectangle;

        /// <summary>
        /// меню выбора инструмента
        /// </summary>
        private System.Windows.Controls.ComboBox _comboBoxSecurity;

        /// <summary>
        /// меню выбора таймфрейма
        /// </summary>
        private System.Windows.Controls.ComboBox _comboBoxTimeFrame;

        /// <summary>
        /// загрузить настройки из файла
        /// </summary>
        private void Load()
        {
            if (!Directory.Exists("Data"))
            {
                Directory.CreateDirectory("Data");
            }

            // название папок это у нас название сетов

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
        /// лог
        /// </summary>
        private Log _log;

        /// <summary>
        /// выслать новое сообщение в лог
        /// </summary>
        void SendNewLogMessage(string message, LogMessageType type)
        {
            if (NewLogMessageEvent != null)
            {
                NewLogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// событие нового сообщения в лог
        /// </summary>
        public event Action<string, LogMessageType> NewLogMessageEvent;

        /// <summary>
        /// таблица источников
        /// </summary>
        private DataGridView _gridSources;

        /// <summary>
        /// создать таблицу источников
        /// </summary>
        private void CreateSourceGrid()
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
            colum0.HeaderText = @"Источник";
            colum0.ReadOnly = true;
            colum0.Width = 100;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colu = new DataGridViewColumn();
            colu.CellTemplate = cell0;
            colu.HeaderText = @"Статус";
            colu.ReadOnly = true;
            colu.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colu);

            _gridSources = newGrid;
            _gridSources.DoubleClick += _gridSources_DoubleClick;
            _hostSource.Child = _gridSources;
            _hostSource.VerticalAlignment = VerticalAlignment.Top;
        }

        /// <summary>
        /// перерисовать таблицу источников
        /// </summary>
        private void RePaintSourceGrid()
        {
            if (_gridSources.InvokeRequired)
            {
                _gridSources.Invoke(new Action(RePaintSourceGrid));
                return;
            }

            _gridSources.Rows.Clear();

            List<ServerType> servers = new ServerType[]
            {
               ServerType.Finam, ServerType.QuikDde, ServerType.QuikLua, ServerType.SmartCom, ServerType.Plaza, 
                ServerType.Oanda, ServerType.BitMex, ServerType.Kraken ,ServerType.Binance,ServerType.BitStamp,ServerType.NinjaTrader
            }.ToList();

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
                    style.BackColor = Color.DarkSeaGreen;
                    style.SelectionBackColor = Color.SeaGreen;
                    row1.Cells[1].Style = style;
                    row1.Cells[0].Style = style;
                }
                else
                {
                    row1.Cells[1].Value = "Disconnect";
                    DataGridViewCellStyle style = new DataGridViewCellStyle();
                    style.BackColor = Color.FloralWhite;
                    style.SelectionBackColor = Color.DarkSalmon;
                    row1.Cells[1].Style = style;
                    row1.Cells[0].Style = style;
                }

                _gridSources.Rows.Add(row1);
            }
            _gridSources[1, 0].Selected = true; // Выбрать невидимую строку, чтобы убрать выделение по умолчанию с грида.
            _gridSources.ClearSelection();

        }

        /// <summary>
        /// событие двойного клика на таблицу источников
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
        /// событие изменения статуса сервера
        /// </summary>
        /// <param name="newState"></param>
        void ServerStatusChangeEvent(string newState)
        {
            RePaintSourceGrid();
        }

// работа с гридом

        /// <summary>
        /// таблица для сетов
        /// </summary>
        private DataGridView _gridset;

        /// <summary>
        /// создать таблицу для сетов
        /// </summary>
        private void CreateSetGrid()
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
            colum0.HeaderText = @"Название";
            colum0.ReadOnly = true;
            colum0.Width = 100;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colu = new DataGridViewColumn();
            colu.CellTemplate = cell0;
            colu.HeaderText = @"Статус";
            colu.ReadOnly = true;
            colu.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colu);

            _gridset = newGrid;
            _gridset.Click += _gridset_Click;
            _gridset.DoubleClick += _gridset_DoubleClick;
            _hostSets.Child = _gridset;
        }

        /// <summary>
        /// перерисовать таблицу сетов
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
                        style.BackColor = Color.DarkSeaGreen;
                        style.SelectionBackColor = Color.SeaGreen;
                        nRow.Cells[1].Style = style;
                        nRow.Cells[0].Style = style;
                    }

                    _gridset.Rows.Add(nRow);
                }
                if (_gridset.Rows.Count != 0)
                {
                    _gridset[0, 0].Selected = true; // Выбрать невидимую строку, чтобы убрать выделение по умолчанию с грида.
                    _gridset.ClearSelection();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// событие двойного клика по таблицу сетов
        /// </summary>
        private void _gridset_DoubleClick(object sender, EventArgs e)
        {
            if (_gridset.CurrentCell.RowIndex <= -1)
            {
                return;
            }
            int _rowIndex = _gridset.CurrentCell.RowIndex;
            RedactThisSet(_rowIndex);
            RePaintSetGrid();
            _gridset.Rows[_rowIndex].Selected = true; // Вернуть фокус на строку, которую редактировал.
        }

        /// <summary>
        /// одиночный клик по таблице сетов
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

            // cоздание контекстного меню

            MenuItem[] items = new MenuItem[3];

            items[0] = new MenuItem();
            items[0].Text = @"Добавить";
            items[0].Click += AddSet_Click;

            items[1] = new MenuItem() { Text = @"Редактировать" };
            items[1].Click += RedactSet_Click;

            items[2] = new MenuItem() { Text = @"Удалить"};
            items[2].Click += DeleteSet_Click;

            ContextMenu menu = new ContextMenu(items);

            _gridset.ContextMenu = menu;
            _gridset.ContextMenu.Show(_gridset, new Point(mouse.X, mouse.Y));
        }

        /// <summary>
        /// создать новый сет
        /// </summary>
        private void AddSet_Click(object sender, EventArgs e)
        {
            CreateNewSet();
        }

        /// <summary>
        /// редактировать сет
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
        /// удалить сет
        /// </summary>
        void DeleteSet_Click(object sender, EventArgs e)
        {
            if (_gridset.CurrentCell == null || 
                _gridset.CurrentCell.RowIndex <= -1)
            {
                return;
            }

            AcceptDialogUi ui = new AcceptDialogUi("Вы собираетесь удалить сет. Вы уверены?");
            ui.ShowDialog();

            if (ui.UserAcceptActioin == false)
            {
                return;
            }
            DeleteThisSet(_gridset.CurrentCell.RowIndex);
        }

        private List<OsDataSet> _sets;

// переключение сетов

        /// <summary>
        /// активный сет
        /// </summary>
        private OsDataSet _selectSet;

        /// <summary>
        /// Включена ли прорисовка графика
        /// </summary>
        private bool _isPaintEnabled = true;
        public bool IsPaintEnabled
        {
            get { return _isPaintEnabled; }
        }

        /// <summary>
        /// сменить активный сет
        /// </summary>
        /// <param name="index">индекс нового</param>
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
                currentSet.StartPaint(_hostChart, _rectangle);
            }
        }

// управление        

        /// <summary>
        /// создать новый сет
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
            { // пользователь не нажал на кнопку принять в форме
                set.Regime = DataSetState.Off;
                set.Delete();
                return;
            }

            if (set.SetName == "Set_")
            {
                set.Regime = DataSetState.Off;
                set.Delete();
                MessageBox.Show(@"Создание сета прервано. Необходимо дать сету имя!");
                return;
            }

            if (_sets.Find(dataSet => dataSet.SetName == set.SetName) != null)
            {
                MessageBox.Show(@"Создание сета прервано. Сет с таким именем уже существует!");
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
        /// удалить сет по индексу
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
        /// редактировать сет по индексу
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
        /// сохранить настройки
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
        /// загрузить настройки
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
