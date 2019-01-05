﻿/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.OsMiner.Patterns;
using System.Drawing;

namespace OsEngine.OsMiner
{

    /// <summary>
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
        /// взять строку сохранения
        /// </summary>
        /// <returns></returns>
        public string GetSaveString()
        {
            string saveStr = Name;
            // разделители на предыдущих уровнях: #
            for (int i = 0; i < Patterns.Count; i++)
            {
                saveStr += "*" + Patterns[i].GetSaveString();
            }

            return saveStr;
        }

        /// <summary>
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
                SendNewLogMessage("Сет с таким именем уже создан", LogMessageType.Error);
                return;
            }

            // запрещённые символы: # * ? % ^ ;

            if (ui.NamePattern.IndexOf('#') > -1 ||
                ui.NamePattern.IndexOf('*') > -1 ||
                ui.NamePattern.IndexOf('?') > -1 ||
                ui.NamePattern.IndexOf('%') > -1 ||
                ui.NamePattern.IndexOf('^') > -1 ||
                ui.NamePattern.IndexOf(';') > -1
                )
            {
                SendNewLogMessage("Символы # * ? % ^ ; запрещены в названиях", LogMessageType.Error);
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
        /// удалить активный паттерн
        /// </summary>
        public void DeletePattern()
        {
            if (Patterns == null || Patterns.Count == 0)
            {
                return;
            }

            AcceptDialogUi ui = new AcceptDialogUi("Вы собираетесь удалить паттерн. Вы уверены?");
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
        /// номер активного паттерна
        /// </summary>
        private int _activPatternNum;

        /// <summary>
        /// паттерны в наборе
        /// </summary>
        public List<PatternController> Patterns;

        /// <summary>
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

// прорисовка сета

        /// <summary>
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
        /// таблица сета паттернов
        /// </summary>
        private DataGridView _gridPatternsInSet;

        /// <summary>
        /// хост для таблицы
        /// </summary>
        private WindowsFormsHost _hostPatternsInSet;

        /// <summary>
        /// хост для чарта
        /// </summary>
        private WindowsFormsHost _hostChart;

        /// <summary>
        /// прямоугольник под чартом
        /// </summary>
        private System.Windows.Shapes.Rectangle _rectChart;

        /// <summary>
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
            column.HeaderText = @"Имя";
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridPatternsInSet.Columns.Add(column);

            _gridPatternsInSet.Rows.Add(null, null);
            _gridPatternsInSet.DoubleClick += _gridPatternsInSet_DoubleClick;
            _gridPatternsInSet.Click += _gridPatternsInSet_Click;
            _gridPatternsInSet.MouseClick += _gridPatternsInSet_MouseClick;
        }

        /// <summary>
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

                items[0] = new MenuItem {Text = @"Добавить"};
                items[0].Click += OsMinerSetAdd_Click;

                items[1] = new MenuItem {Text = @"Редактировать"};
                items[1].Click += OsMinerSetRedact_Click;

                items[2] = new MenuItem { Text = @"Удалить" };
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
        /// пользователь хочет удалить активный паттерн из сета
        /// </summary>
        void OsMinerSetDelete_Click(object sender, EventArgs e)
        {
            DeletePattern();
        }

        /// <summary>
        /// пользователь хочет редактировать активный паттерн
        /// </summary>
        void OsMinerSetRedact_Click(object sender, EventArgs e)
        {
            RedactPattern();
        }

        /// <summary>
        /// пользователь хочет добавить новый паттерн в сет
        /// </summary>
        void OsMinerSetAdd_Click(object sender, EventArgs e)
        {
            CreatePattern();
            Patterns[_activPatternNum].Paint(_hostChart, _rectChart);
        }

        /// <summary>
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
        /// пользователь дважды кликнул мышью по таблице сета
        /// </summary>
        void _gridPatternsInSet_DoubleClick(object sender, EventArgs e)
        {
            RedactPattern();
        }

        /// <summary>
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

// логирование

        /// <summary>
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
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
