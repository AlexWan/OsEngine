/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Logging;
using System.Drawing;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.OsMiner.Patterns;


namespace OsEngine.OsMiner
{
    /// <summary>
    /// pattern set manager
    /// менеджер сетов паттернов
    /// </summary>
    public class OsMinerMaster
    {
        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="hostLog">log host/хост для лога</param>
        /// <param name="hostSets">pattern set host/хост для сетов паттернов</param>
        /// <param name="hostPatternsInSet">host for patterns within sets/хост для паттернов  внутри сетов</param>
        /// <param name="hostChart">chart drawing host/хост для прорисовки чарта</param>
        /// <param name="rectChart">frames for charts/обрамления для чарта</param>
        public OsMinerMaster(
            WindowsFormsHost hostLog, 
            WindowsFormsHost hostSets,
            WindowsFormsHost hostPatternsInSet,
            WindowsFormsHost hostChart,
             System.Windows.Shapes.Rectangle rectChart)
        {
            _hostSets = hostSets;
            _hostPatternsInSet = hostPatternsInSet;
            _hostChart = hostChart;
            _rectChart = rectChart;

            Log log = new Log("OsMiner", StartProgram.IsOsMiner);
            log.Listen(this);
            log.StartPaint(hostLog);

            Load();

            CreateSetsDataGrid();
            PaintSetsDataGrid();

            PaintActivSet();
        }

        public OsMinerMaster()
        {
            Load();
        }

        /// <summary>
        /// create a new set of patterns
        /// создать новый сет паттернов
        /// </summary>
        public void CreateSet()
        {
            OsMinerSet set = new OsMinerSet();

            OsMinerSetUi ui = new OsMinerSetUi(Sets.Count + 1, set);
            ui.ShowDialog();

            if (ui.IsActivate == false)
            {
                return;
            }

            if (Sets.Find(s => s.Name == set.Name) != null)
            {
                SendNewLogMessage(OsLocalization.Miner.Message1,LogMessageType.Error);
                return;
            }

            // forbidden symbols: # * ? % ^/запрещённые символы: # * ? % ^ ;

            if (set.Name.IndexOf('#') > -1 ||
                set.Name.IndexOf('*') > -1 ||
                set.Name.IndexOf('?') > -1 ||
                set.Name.IndexOf('%') > -1 ||
                set.Name.IndexOf('^') > -1 ||
                set.Name.IndexOf(';') > -1
                )
            {
                SendNewLogMessage(OsLocalization.Miner.Message2, LogMessageType.Error);
                return;
            }

            Sets.Add(set);

            ActivSetNum = Sets.Count - 1;
            PaintSetsDataGrid();
            PaintActivSet();
            Save();
            set.NeadToSaveEvent += set_NeadToSaveEvent;
            set.LogMessageEvent += SendNewLogMessage;
        }

        /// <summary>
        /// incoming event that you need to resave everything
        /// входящее событие о том что нужно всё пересохранить
        /// </summary>
        void set_NeadToSaveEvent()
        {
            Save();
        }

        /// <summary>
        /// delete active pattern set
        /// удалить активный сет паттернов
        /// </summary>
        public void DeleteSet()
        {
            if (Sets == null || Sets.Count == 0)
            {
                return;
            }

            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Miner.Message3);
            ui.ShowDialog();

            if (ui.UserAcceptActioin == false)
            {
                return;
            }

            Sets[ActivSetNum].Delete();
            Sets[ActivSetNum].StopPaint();
            Sets.RemoveAt(ActivSetNum);
            ActivSetNum = 0;
            PaintSetsDataGrid();
            PaintActivSet();
            Save();
        }

        /// <summary>
        /// move the graph to the right, to the first pattern encountered
        /// переместить график вправо, до первого встречанного паттерна
        /// </summary>
        public void GoRight()
        {
            if (_activSetNumber >= Sets.Count)
            {
                return;
            }

            Sets[_activSetNumber].GoRight();
        }

        /// <summary>
        /// move the graph to the left, to the first pattern encountered
        /// переместить график влево, до первого встречанного паттерна
        /// </summary>
        public void GoLeft()
        {
            if (_activSetNumber >= Sets.Count)
            {
                return;
            }

            Sets[_activSetNumber].GoLeft();
        }

        /// <summary>
        /// load patterns from file system
        /// загрузить сеты паттернов из файловой системы
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + @"OsMinerMasterSettings.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"OsMinerMasterSettings.txt"))
                {

                    string readLine = reader.ReadLine();
                    if (readLine != null)
                    {
                        string[] save = readLine.Split('#');

                        for (int i = 0; i < save.Length-1; i++)
                        {
                            if (save[i] == "")
                            {
                                continue;
                            }
                            OsMinerSet set = new OsMinerSet();
                            set.Load(save[i]);
                            Sets.Add(set);
                            set.NeadToSaveEvent += set_NeadToSaveEvent;
                            set.LogMessageEvent += SendNewLogMessage;
                        }
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // send to log
            }
        }

        /// <summary>
        /// save patterns to file system
        /// сохранить сеты паттернов в  файловую ситему
        /// </summary>
        private void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"OsMinerMasterSettings.txt", false)
                    )
                {
                    string saveString = "";
                    for (int i = 0; i < Sets.Count; i++)
                    {
                        saveString += Sets[i].GetSaveString() + "#";
                    }

                    writer.WriteLine(saveString);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // send to log
            }
        }

        /// <summary>
        /// pattern set
        /// набор сетов паттернов
        /// </summary>
        public List<OsMinerSet> Sets = new List<OsMinerSet>();

        public List<IPattern> GetPatternsToInter(string patternName)
        {
            for (int i = 0; i < Sets.Count; i++)
            {
                if (Sets[i].GetPatternsToInter(patternName) != null)
                {
                    return Sets[i].GetPatternsToInter(patternName);
                }
            }

            return null;
        }

        public List<IPattern> GetPatternsToExit(string patternName)
        {
            for (int i = 0; i < Sets.Count; i++)
            {
                if (Sets[i].GetPatternsToExit(patternName) != null)
                {
                    return Sets[i].GetPatternsToExit(patternName);
                }
            }

            return null;
        }

        public List<string> GetListPatternsNames(string nameSet)
        {
            List<string> names = new List<string>();
            for (int i = 0; i < Sets.Count; i++)
            {
                if (nameSet == Sets[i].Name)
                {
                    names.AddRange(Sets[i].GetListPatternsNames());
                }
            }
            return names;
        }

// drawing a table with sets/прорисовка таблицы с сетами

        /// <summary>
        /// table with sets of patterns
        /// таблица с сетами паттернов
        /// </summary>
        private DataGridView _gridSets;

        /// <summary>
        /// host for drawing pattern sets
        /// хост для прорисовки сетов паттернов
        /// </summary>
        private WindowsFormsHost _hostSets;

        /// <summary>
        /// creating a pattern set table
        /// создание таблицы сетов паттернов
        /// </summary>
        private void CreateSetsDataGrid()
        {
            _gridSets = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridSets.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = @"№";
            column0.ReadOnly = true;
            column0.Width = 50;

            _gridSets.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Miner.Message4;
            column1.ReadOnly = true;
            column1.Width = 150;

            _gridSets.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = OsLocalization.Miner.Message5;
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridSets.Columns.Add(column);

            _gridSets.Rows.Add(null, null);
            _gridSets.Click += _gridSets_Click;
            _gridSets.MouseClick += _gridSets_MouseClick;

            _hostSets.Child = _gridSets;
        }

        /// <summary>
        /// user clicked the pattern set table
        /// пользователь кликнул по таблице сетов паттернов
        /// </summary>
        void _gridSets_MouseClick(object sender, MouseEventArgs mouse)
        {
            if (mouse.Button != MouseButtons.Right)
            {
                return;
            }

            try
            {
                MenuItem[] items = new MenuItem[2];

                items[0] = new MenuItem { Text = OsLocalization.Miner.Message6 };
                items[0].Click += OsMinerMasterAdd_Click;

                items[1] = new MenuItem { Text = OsLocalization.Miner.Message7 };
                items[1].Click += OsMinerMasterRemove_Click;

                ContextMenu menu = new ContextMenu(items);

                _gridSets.ContextMenu = menu;
                _gridSets.ContextMenu.Show(_gridSets, new Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                if (LogMessageEvent != null)
                {
                    LogMessageEvent(error.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// the user wants to delete the selected set of patterns
        /// пользователь хочет удалить выделенный  сет паттернов
        /// </summary>
        void OsMinerMasterRemove_Click(object sender, EventArgs e)
        {
            DeleteSet();
        }

        /// <summary>
        /// the user wants to add a new set of patterns
        /// пользователь хочет добавить новый сет паттернов
        /// </summary>
        void OsMinerMasterAdd_Click(object sender, EventArgs e)
        {
             CreateSet();
        }

        /// <summary>
        /// the user has clicked on the pattern set table and wants to change the active set
        /// пользователь кликнул по таблице сетов паттернов и хочет сменить активный сет
        /// </summary>
        void _gridSets_Click(object sender, EventArgs e)
        {
            if (_gridSets.SelectedCells.Count == 0)
            {
                return;
            }
            int activPattern = _gridSets.SelectedCells[0].RowIndex;

            if (activPattern >= Sets.Count)
            {
                return;
            }

            Sets[ActivSetNum].StopPaint();
            ActivSetNum = activPattern;
            PaintActivSet();
        }

        /// <summary>
        /// redraw the table with sets of patterns
        /// перерисовать таблицу с сетами паттернов
        /// </summary>
        private void PaintSetsDataGrid()
        {
            _gridSets.Rows.Clear();

            for (int i = Sets.Count - 1; i > -1; i--)
            {
                DataGridViewRow row = new DataGridViewRow();
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = (i + 1).ToString();

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[1].Value = Sets[i].Name;

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[2].Value = Sets[i].Patterns.Count;
                _gridSets.Rows.Insert(0, row);
            }
        }

// drawing a table with set patterns/прорисовка таблицы с паттернами сета

        /// <summary>
        /// active pattern set number
        /// активный номер сета паттернов
        /// </summary>
        public int ActivSetNum
        {
            get { return _activSetNumber; }
            set
            {
                if (_activSetNumber == value)
                {
                    return;
                }

                if (Sets != null && Sets.Count < _activSetNumber)
                {
                    Sets[_activSetNumber].StopPaint();
                }
                _activSetNumber = value;
            }
        }
        private int _activSetNumber;

        /// <summary>
        /// host for drawing patterns in the set
        /// хост для прорисовки паттернов в сете
        /// </summary>
        private WindowsFormsHost _hostPatternsInSet;

        /// <summary>
        /// chart drawing host
        /// хост для прорисовки чарта
        /// </summary>
        private WindowsFormsHost _hostChart;

        /// <summary>
        /// rectangular area under the chart
        /// прямоугольная область под чартом
        /// </summary>
        private System.Windows.Shapes.Rectangle _rectChart;

        /// <summary>
        /// draw an active set of patterns on GUI
        /// прорисовать на ГУЕ активный сет паттернов
        /// </summary>
        private void PaintActivSet()
        {
            if (Sets.Count == 0)
            {
                return;
            }

            if (ActivSetNum >= Sets.Count)
            {
                ActivSetNum = 0;
            }

            Sets[ActivSetNum].Paint(_hostPatternsInSet, _hostChart, _rectChart);
        }

// messages for log/сообщения для лога

        /// <summary>
        /// send a new message to the top
        /// выслать новое сообщение на верх
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// outgoing message for log
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }
}
