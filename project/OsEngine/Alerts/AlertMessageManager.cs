/*
 *Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdff
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.IO;
using System.Media;
using System.Windows.Forms;
using OsEngine.Language;

namespace OsEngine.Alerts
{
    public class AlertMessageManager
    {
        /// <summary>
        /// throw new alert
        /// выбросить новый алерт
        /// </summary>
        /// <param name="stream">music to be played/музыка, которая будет проигрываться</param>
        /// <param name="botName">name of robot that threw out alert/имя робота выбросившего алерт</param>
        /// <param name="message"> message being created/сообщение ради которого сыр бор</param>
        public static void ThrowAlert(Stream stream, string botName, string message)
        {
            if (!TextBoxFromStaThread.Dispatcher.CheckAccess())
            {
                TextBoxFromStaThread.Dispatcher.Invoke(
                    new Action<Stream, string, string>(ThrowAlert), stream, botName, message);
                return;

            }
            if (_grid == null)
            {
                // if our chart is not created, we call its creation method
                // если наша таблица не создана, вызываем метод её создания
                CreateGrid();
            }
            // add new line
            // добавляем новую строку

            DataGridViewRow row = new DataGridViewRow();
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[0].Value = DateTime.Now.ToLongTimeString();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[1].Value = botName;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[2].Value = message;

            _grid.Rows.Insert(0, row);

            // turn on music
            // включаем музыку

            if (stream != null)
            {
                SoundPlayer player = new SoundPlayer(stream);
                player.Play();
            }

            if (_ui == null)
            {
                // if our window is not hanging in middle of screen we create it
                // если наше окно не висит уже посреди экрана создаём его
                _ui = new AlertMessageFullUi(_grid);
                _ui.Show();
                _ui.Closed += _ui_Closed;
            }
        }

        static void _ui_Closed(object sender, EventArgs e)
        {
            _ui = null;
        }

        /// <summary>
        /// show window with alerts
        /// показать окно с алертами
        /// </summary>
        public static void ShowAlertDialog()
        {
            if (_grid == null)
            {
                CreateGrid();
            }

            if (_ui == null)
            {
                // if our window is not hanging in middle of screen we create it
                // если наше окно не висит уже посреди экрана создаём его
                _ui = new AlertMessageFullUi(_grid);
                _ui.Show();
                _ui.Closed += _ui_Closed;
            }
        }

        /// <summary>
        /// create chart
        /// создать таблицу
        /// </summary>
        private static void CreateGrid()
        {
            _grid = new DataGridView();

            _grid.AllowUserToOrderColumns = true;
            _grid.AllowUserToResizeRows = true;
            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToAddRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;

            DataGridViewCellStyle style = new DataGridViewCellStyle();
            style.Alignment = DataGridViewContentAlignment.TopLeft;
            style.WrapMode = DataGridViewTriState.True;
            _grid.DefaultCellStyle = style;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Alerts.GridHeader3;
            column0.ReadOnly = true;
            column0.Width = 80;

            _grid.Columns.Add(column0);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = OsLocalization.Alerts.GridHeader4;
            column.ReadOnly = true;
            column.Width = 80;
            _grid.Columns.Add(column);

            _grid.Rows.Add(null, null);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Alerts.GridHeader5;
            column1.ReadOnly = true;
            column1.Width = 400;
            _grid.Columns.Add(column1);

            _grid.Rows.Add(null, null);
        }

        /// <summary>
        /// chart
        /// таблица
        /// </summary>
        private static DataGridView _grid;

        /// <summary>
        /// window
        /// окно
        /// </summary>
        private static AlertMessageFullUi _ui;

        public static System.Windows.Controls.TextBox TextBoxFromStaThread;

    }
}
