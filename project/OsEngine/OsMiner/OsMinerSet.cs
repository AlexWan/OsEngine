/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.OsMiner.Patterns;
using System.Drawing;
using OsEngine.Language;

namespace OsEngine.OsMiner
{

    /// <summary>
    /// pattern set
    /// сет паттернов
    /// </summary>
    public class OsMinerSet
    {

        public OsMinerSet()
        {
            Patterns = new List<PatternController>();

            CreatePatternGrid();
        }

        /// <summary>
        /// set name
        /// имя сета
        /// </summary>
        public string Name {
            get { return _name; }
            set
            {
                _name = value;
            } }
        private string _name;

        /// <summary>
        /// load set from string
        /// загрузить  сет из строки
        /// </summary>
        /// <param name="saveStr"></param>
        public void Load(string saveStr)
        {
            string[] patterns = saveStr.Split('*');

            Name = patterns[0];

            for (int i = 1; i < patterns.Length; i ++)
            {
                PatternController pattern = new PatternController();
                pattern.Load(patterns[i]);
                
                Patterns.Add(pattern);
                pattern.NeadToSaveEvent += pattern_NeadToSaveEvent;
                pattern.LogMessageEvent += SendNewLogMessage;
            }
        }

        /// <summary>
        /// delete all network information from the file system
        /// удалить всю информацию по сету из файловой системы
        /// </summary>
        public void Delete()
        {
            for (int i = 0; Patterns != null && i > Patterns.Count; i++)
            {
                Patterns[i].Delete();
            }
        }

        /// <summary>
        /// There have been changes in the pattern. Preservation needed
        /// произошли изменения в паттерне. Необходимо сохранение
        /// </summary>
        void pattern_NeadToSaveEvent()
        {
            if (NeadToSaveEvent != null)
            {
                NeadToSaveEvent();
            }
        }
        public event Action NeadToSaveEvent;

        /// <summary>
        /// take the save string
        /// взять строку сохранения
        /// </summary>
        /// <returns></returns>
        public string GetSaveString()
        {
            string saveStr = Name;
            // separators on previous levels: # 
            // разделители на предыдущих уровнях: #
            for (int i = 0; i < Patterns.Count; i++)
            {
                saveStr += "*" + Patterns[i].GetSaveString();
            }

            return saveStr;
        }

        /// <summary>
        /// create a new pattern
        /// создать новый паттерн
        /// </summary>
        public void CreatePattern()
        {
            int newPatternNum = 1;

            if (Patterns.Count >= newPatternNum)
            {
                newPatternNum = Patterns.Count + 1;
            }

            PatternsCreateUi ui = new PatternsCreateUi(newPatternNum);
            ui.ShowDialog();

            if (ui.IsAccepted == false)
            {
                return;
            }

            if (Patterns.Find(s => s.Name == ui.NamePattern) != null)
            {
                SendNewLogMessage(OsLocalization.Miner.Message1, LogMessageType.Error);
                return;
            }

            // forbidden symbols: # * ? % ^;
            // запрещённые символы: # * ? % ^ ;

            if (ui.NamePattern.IndexOf('#') > -1 ||
                ui.NamePattern.IndexOf('*') > -1 ||
                ui.NamePattern.IndexOf('?') > -1 ||
                ui.NamePattern.IndexOf('%') > -1 ||
                ui.NamePattern.IndexOf('^') > -1 ||
                ui.NamePattern.IndexOf(';') > -1
                )
            {
                SendNewLogMessage(OsLocalization.Miner.Message2, LogMessageType.Error);
                return;
            }

            PatternController newPattern = new PatternController();
            newPattern.NeadToSaveEvent += pattern_NeadToSaveEvent;
            newPattern.LogMessageEvent += SendNewLogMessage;

            newPattern.Name = ui.NamePattern;
            Patterns.Add(newPattern);
            newPattern.ShowDialog();
            _activPatternNum = Patterns.Count - 1;
            PaintSet();
        }

        /// <summary>
        /// edit active pattern
        /// редактировать активный паттерн
        /// </summary>
        public void RedactPattern()
        {
            if (Patterns == null || Patterns.Count == 0)
            {
                return;
            }


            Patterns[_activPatternNum].ShowDialog();
        }

        /// <summary>
        /// remove active pattern
        /// удалить активный паттерн
        /// </summary>
        public void DeletePattern()
        {
            if (Patterns == null || Patterns.Count == 0)
            {
                return;
            }

            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Miner.Message8);
            ui.ShowDialog();

            if (ui.UserAcceptActioin == false)
            {
                return;
            }

            Patterns[_activPatternNum].StopPaint();
            Patterns[_activPatternNum].DataServer.Delete();
            Patterns.RemoveAt(_activPatternNum);
            _activPatternNum = 0;
            PaintSet();
        }

        /// <summary>
        /// go to the right on the chart, before the first pattern encountered
        /// перейти вправо на графике, до первого встречанного паттерна
        /// </summary>
        public void GoRight()
        {
            if (_activPatternNum >= Patterns.Count)
            {
                return;
            }
            Patterns[_activPatternNum].GoRight();
        }

        /// <summary>
        /// go left on schedule to the first pattern encountered
        /// перейти влево по графику, до первого встречанного паттерна
        /// </summary>
        public void GoLeft()
        {
            if (_activPatternNum >= Patterns.Count)
            {
                return;
            }
            Patterns[_activPatternNum].GoLeft();
        }

        /// <summary>
        /// active pattern number
        /// номер активного паттерна
        /// </summary>
        private int _activPatternNum;

        /// <summary>
        /// patterns in set
        /// паттерны в наборе
        /// </summary>
        public List<PatternController> Patterns;

        /// <summary>
        /// take the names of the patterns
        /// взять имена паттернов
        /// </summary>
        public List<string> GetListPatternsNames()
        {
            List<string> names = new List<string>();

            for (int i = 0; i < Patterns.Count; i++)
            {
                names.Add(Patterns[i].Name);
            }
            return names;
        }

        public List<IPattern> GetPatternsToInter(string patternName)
        {
            for (int i = 0; i < Patterns.Count; i++)
            {
                if (Patterns[i].Name == patternName)
                {
                    return Patterns[i].PatternsToOpen;
                }
            }
            return null;
        }

        public List<IPattern> GetPatternsToExit(string patternName)
        {
            for (int i = 0; i < Patterns.Count; i++)
            {
                if (Patterns[i].Name == patternName)
                {
                    return Patterns[i].PatternsToClose;
                }
            }
            return null;
        }

// drawing a set/прорисовка сета

        /// <summary>
        /// start drawing this set
        /// начать прорисовку данного сета
        /// </summary>
        public void Paint(WindowsFormsHost hostSet, WindowsFormsHost hostChart, System.Windows.Shapes.Rectangle rectChart)
        {
            _hostPatternsInSet = hostSet;

            _hostPatternsInSet.Child = _gridPatternsInSet;
            _hostChart = hostChart;
            _rectChart = rectChart;

            PaintSet();
        }

        /// <summary>
        /// stop drawing the set
        /// остановить прорисовку сета
        /// </summary>
        public void StopPaint()
        {
            _gridPatternsInSet.Rows.Clear();
            _hostPatternsInSet = null;

            if (Patterns != null &&
                Patterns.Count > 0)
            {
                Patterns[_activPatternNum].StopPaint();
            }
        }

        /// <summary>
        /// pattern set table
        /// таблица сета паттернов
        /// </summary>
        private DataGridView _gridPatternsInSet;

        /// <summary>
        /// table host
        /// хост для таблицы
        /// </summary>
        private WindowsFormsHost _hostPatternsInSet;

        /// <summary>
        /// chart host
        /// хост для чарта
        /// </summary>
        private WindowsFormsHost _hostChart;

        /// <summary>
        /// rectangle under the chart
        /// прямоугольник под чартом
        /// </summary>
        private System.Windows.Shapes.Rectangle _rectChart;

        /// <summary>
        /// create a table for drawing a set
        /// создать таблицу для прорисовки сета
        /// </summary>
        private void CreatePatternGrid()
        {
            _gridPatternsInSet = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridPatternsInSet.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = @"№";
            column0.ReadOnly = true;
            column0.Width = 50;

            _gridPatternsInSet.Columns.Add(column0);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = OsLocalization.Miner.Message4;
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridPatternsInSet.Columns.Add(column);

            _gridPatternsInSet.Rows.Add(null, null);
            _gridPatternsInSet.DoubleClick += _gridPatternsInSet_DoubleClick;
            _gridPatternsInSet.Click += _gridPatternsInSet_Click;
            _gridPatternsInSet.MouseClick += _gridPatternsInSet_MouseClick;
        }

        /// <summary>
        /// user clicked on set table
        /// пользователь кликнул мышью по таблице сета
        /// </summary>
        void _gridPatternsInSet_MouseClick(object sender, MouseEventArgs mouse)
        {
            if (mouse.Button != MouseButtons.Right)
            {
                return;
            }

            try
            {
                MenuItem[] items = new MenuItem[3];

                items[0] = new MenuItem {Text =OsLocalization.Miner.Message6};
                items[0].Click += OsMinerSetAdd_Click;

                items[1] = new MenuItem {Text = OsLocalization.Miner.Message9};
                items[1].Click += OsMinerSetRedact_Click;

                items[2] = new MenuItem { Text = OsLocalization.Miner.Message7};
                items[2].Click += OsMinerSetDelete_Click;

                ContextMenu menu = new ContextMenu(items);

                _gridPatternsInSet.ContextMenu = menu;
                _gridPatternsInSet.ContextMenu.Show(_gridPatternsInSet, new Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

        }

        /// <summary>
        /// the user wants to delete the active pattern from the set
        /// пользователь хочет удалить активный паттерн из сета
        /// </summary>
        void OsMinerSetDelete_Click(object sender, EventArgs e)
        {
            DeletePattern();
        }

        /// <summary>
        /// the user wants to edit the active pattern
        /// пользователь хочет редактировать активный паттерн
        /// </summary>
        void OsMinerSetRedact_Click(object sender, EventArgs e)
        {
            RedactPattern();
        }

        /// <summary>
        /// the user wants to add a new pattern to the set
        /// пользователь хочет добавить новый паттерн в сет
        /// </summary>
        void OsMinerSetAdd_Click(object sender, EventArgs e)
        {
            CreatePattern();
            Patterns[_activPatternNum].Paint(_hostChart, _rectChart);
        }

        /// <summary>
        /// user clicked on set table
        /// пользователь кликнул мышью по таблице сета
        /// </summary>
        void _gridPatternsInSet_Click(object sender, EventArgs e)
        {
            if (_gridPatternsInSet.SelectedCells.Count == 0)
            {
                return;
            }
            int activPattern = _gridPatternsInSet.SelectedCells[0].RowIndex;

            if (activPattern >= Patterns.Count)
            {
                return;
            }

            Patterns[_activPatternNum].StopPaint();
            _activPatternNum = activPattern;

            Patterns[_activPatternNum].Paint(_hostChart, _rectChart);
        }

        /// <summary>
        /// user double clicked on set table
        /// пользователь дважды кликнул мышью по таблице сета
        /// </summary>
        void _gridPatternsInSet_DoubleClick(object sender, EventArgs e)
        {
            RedactPattern();
        }

        /// <summary>
        /// draw this set in the main window
        /// прорисовать данный сет в главном окне
        /// </summary>
        private void PaintSet()
        {
            if (_hostPatternsInSet == null)
            {
                return;
            }

            _gridPatternsInSet.Rows.Clear();

            for (int i = Patterns.Count-1; i >-1; i--)
            {
                DataGridViewRow row = new DataGridViewRow();
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = (i + 1).ToString();

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[1].Value = Patterns[i].Name;
                _gridPatternsInSet.Rows.Insert(0, row);
            }
        }

// logging/логирование

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
