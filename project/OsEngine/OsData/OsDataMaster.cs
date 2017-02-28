/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Logging;
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

            _log = new Log("OsDataMaster");
            _log.StartPaint(hostLog);
            _log.Listen(this);

            Load();

            CreateSetGrid();
            RePaintSetGrid();

            try
            {
                ServerMaster.CreateServer(ServerType.Finam, false);
                ServerMaster.CreateServer(ServerType.Quik,false);
                ServerMaster.CreateServer(ServerType.Plaza, false);
                ServerMaster.CreateServer(ServerType.SmartCom, false);
                ServerMaster.CreateServer(ServerType.InteractivBrokers, false);
               

                List<IServer> servers = ServerMaster.GetServers();

                for (int i = 0; i < servers.Count; i++)
                {
                    servers[i].ConnectStatusChangeEvent += ServerStatusChangeEvent;
                    servers[i].LogMessageEvent += OsDataMaster_LogMessageEvent;
                }
                
                SendNewLogMessage("Сервера успешно развёрнуты", LogMessageType.System);


            }
            catch (Exception)
            {
                SendNewLogMessage("Ошибка при создании серверов", LogMessageType.Error);
            }

            CreateSourceGrid();
            RePaintSourceGrid();
            ChangeActivSet(0);
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
        /// сохдать таблицу источников
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

            List<IServer> servers = ServerMaster.GetServers();

            for (int i = 0; i < servers.Count; i++)
            {
                DataGridViewRow row1 = new DataGridViewRow();
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[0].Value = servers[i].ServerType;
                row1.Cells.Add(new DataGridViewTextBoxCell());
                row1.Cells[1].Value = servers[i].ServerStatus;

                if (servers[i].ServerStatus == ServerConnectStatus.Connect)
                {
                    DataGridViewCellStyle style = new DataGridViewCellStyle();
                    style.BackColor = Color.DarkSeaGreen;
                    style.SelectionBackColor = Color.SeaGreen;
                    row1.Cells[1].Style = style;
                    row1.Cells[0].Style = style;
                }

                _gridSources.Rows.Add(row1);
            }


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
            ServerMaster.GetServers()[_gridSources.CurrentCell.RowIndex].ShowDialog();
        }

        /// <summary>
        /// событие измениня статуса сервера
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
            colum0.HeaderText = @"Назавние";
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
            if (_gridset.InvokeRequired)
            {
                _gridset.Invoke(new Action(RePaintSetGrid));
                return;
            }
            _gridset.Rows.Clear();

            for (int i = 0;_sets != null && i < _sets.Count; i++)
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
            RedactThisSet(_gridset.CurrentCell.RowIndex);
            RePaintSetGrid();
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
            if (_gridset.CurrentCell.RowIndex <= -1)
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
            if (_gridset.CurrentCell.RowIndex <= -1)
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
            currentSet.StartPaint(_hostChart,_rectangle);
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
            set.ShowDialog();

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

            _sets[num].ShowDialog();
            _sets[num].Save();
        }
    }
}
