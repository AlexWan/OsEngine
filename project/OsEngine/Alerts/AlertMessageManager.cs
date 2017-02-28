/*
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.IO;
using System.Media;
using System.Windows.Forms;

namespace OsEngine.Alerts
{
    public class AlertMessageManager
    {
        /// <summary>
        /// выбросить новый алерт
        /// </summary>
        /// <param name="stream">музыка, которая будет проигрываться</param>
        /// <param name="botName">имя робота выбросившего алерт</param>
        /// <param name="message">сообщение ради которого сыр бор</param>
        public static void ThrowAlert(Stream stream, string botName, string message)
        {
            if (!TextBoxFromStaThread.Dispatcher.CheckAccess())
            {
                TextBoxFromStaThread.Dispatcher.Invoke(
                    new Action<Stream, string, string>(ThrowAlert), stream, botName, message);
                return;

            }
            if (_grid == null)
            {// если наша таблица не создана, вызываем метод её создания
                CreateGrid();
            }

            // добавляем новую строку

            DataGridViewRow row = new DataGridViewRow();
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[0].Value = DateTime.Now.ToLongTimeString();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[1].Value = botName;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[2].Value = message;

            _grid.Rows.Insert(0, row);

            // включаем музыку

            if (stream != null)
            {
                SoundPlayer player = new SoundPlayer(stream);
                player.Play();
            }

            if (_ui == null)
            {// если наше окно не висит уже посреди экрана создаём его
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
        /// показать окно с алертами
        /// </summary>
        public static void ShowAlertDialog()
        {
            if (_grid == null)
            {
                CreateGrid();
            }

            if (_ui == null)
            {// если наше окно не висит уже посреди экрана создаём его
                _ui = new AlertMessageFullUi(_grid);
                _ui.Show();
                _ui.Closed += _ui_Closed;
            }
        }

        /// <summary>
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
            column0.HeaderText = @"Время";
            column0.ReadOnly = true;
            column0.Width = 80;

            _grid.Columns.Add(column0);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = @"Бот";
            column.ReadOnly = true;
            column.Width = 80;
            _grid.Columns.Add(column);

            _grid.Rows.Add(null, null);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = @"Сообщение";
            column1.ReadOnly = true;
            column1.Width = 400;
            _grid.Columns.Add(column1);

            _grid.Rows.Add(null, null);
        }

        /// <summary>
        /// таблица
        /// </summary>
        private static DataGridView _grid;

        /// <summary>
        /// окно
        /// </summary>
        private static AlertMessageFullUi _ui;

        public static System.Windows.Controls.TextBox TextBoxFromStaThread;

    }
}
