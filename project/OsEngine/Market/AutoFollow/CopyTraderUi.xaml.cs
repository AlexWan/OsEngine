/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using OsEngine.Logging;
using OsEngine.OsTrader.Panels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading;

namespace OsEngine.Market.AutoFollow
{
    /// <summary>
    /// Interaction logic for CopyTraderUi.xaml
    /// </summary>
    public partial class CopyTraderUi : Window
    {
        public CopyTrader CopyTraderInstance;

        public int TraderNumber;

        public CopyTraderUi(CopyTrader copyTrader)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            GlobalGUILayout.Listen(this, "copyTraderUi " + copyTrader.Number);

            CopyTraderInstance = copyTrader;
            TraderNumber = copyTrader.Number;
            Title = OsLocalization.Market.Label201 + " # " + CopyTraderInstance.Number + " " + CopyTraderInstance.Name;

            CopyTraderInstance.DeleteEvent += CopyTraderClass_DeleteEvent;

            this.Closed += CopyTraderUi_Closed;

            // 1 Base settings

            ComboBoxIsOn.Items.Add(true.ToString());
            ComboBoxIsOn.Items.Add(false.ToString());
            ComboBoxIsOn.SelectedItem = CopyTraderInstance.IsOn.ToString();
            ComboBoxIsOn.SelectionChanged += ComboBoxIsOn_SelectionChanged;

            TextBoxName.Text = copyTrader.Name;
            TextBoxName.TextChanged += TextBoxName_TextChanged;

            ComboBoxWorkType.Items.Add(CopyTraderType.None.ToString());
            ComboBoxWorkType.Items.Add(CopyTraderType.Portfolio.ToString());
            ComboBoxWorkType.Items.Add(CopyTraderType.Robot.ToString());
            ComboBoxWorkType.SelectedItem = copyTrader.WorkType.ToString();
            ComboBoxWorkType.SelectionChanged += ComboBoxWorkType_SelectionChanged;

            // 2 Robots to copy

            CreateRobotsGrid();
            UpdateGridRobots();

            // 6 Localization

            LabelIsOn.Content = OsLocalization.Market.Label182; 
            LabelName.Content = OsLocalization.Market.Label70;
            LabelWorkType.Content = OsLocalization.Market.Label200;
            LabelRobotsGrid.Content = OsLocalization.Market.Label208;
            LabelSlaveGrid.Content = OsLocalization.Market.Label209;
            LabelSecuritiesGrid.Content = OsLocalization.Market.Label210;
            LabelJournal.Content = OsLocalization.Market.Label211;

            CopyTraderInstance.LogCopyTrader.StartPaint(HostLog);

            LoadPanelsPositions();

            Thread painterThread = new Thread(PainterThreadArea);
            painterThread.Start();
        }

        private void CopyTraderUi_Closed(object sender, EventArgs e)
        {
            _windowIsClosed = true;

            CopyTraderInstance.LogCopyTrader.StopPaint();

            CopyTraderInstance.DeleteEvent -= CopyTraderClass_DeleteEvent;
            CopyTraderInstance = null;
        }

        private void CopyTraderClass_DeleteEvent()
        {
            Close();
        }

        public event Action NeedToUpdateCopyTradersGridEvent;

        #region Base settings

        private void ComboBoxIsOn_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                CopyTraderType type;

                bool isOn = Convert.ToBoolean(ComboBoxIsOn.SelectedItem.ToString());
                CopyTraderInstance.IsOn = isOn;
                ServerMaster.SaveCopyMaster();

                if(NeedToUpdateCopyTradersGridEvent != null)
                {
                    NeedToUpdateCopyTradersGridEvent();
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxWorkType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                CopyTraderType type;

                if(Enum.TryParse(ComboBoxWorkType.SelectedItem.ToString(), out type))
                {
                    CopyTraderInstance.WorkType = type;
                    ServerMaster.SaveCopyMaster();

                    if (NeedToUpdateCopyTradersGridEvent != null)
                    {
                        NeedToUpdateCopyTradersGridEvent();
                    }
                }              
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxName_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                CopyTraderInstance.Name = TextBoxName.Text;
                ServerMaster.SaveCopyMaster();

                Title = OsLocalization.Market.Label201 + " # " + CopyTraderInstance.Number + " " + CopyTraderInstance.Name;

                if (NeedToUpdateCopyTradersGridEvent != null)
                {
                    NeedToUpdateCopyTradersGridEvent();
                }
            }
            catch(Exception ex)
            {
                SendNewLogMessage(ex.ToString(),Logging.LogMessageType.Error);
            }
        }

        private void SavePanelsPosition()
        {
            try
            {
                string result = "";

                if (GridFollowSettings.RowDefinitions[1].Height.Value == 25)
                {// robots 
                    result += "0,";
                }
                else
                {
                    result += "1,";
                }

                if (GridFollowSettings.RowDefinitions[2].Height.Value == 25)
                {// slave
                    result += "0,";
                }
                else
                {
                    result += "1,";
                }

                if (GridFollowSettings.RowDefinitions[3].Height.Value == 25)
                {// securities
                    result += "0,";
                }
                else
                {
                    result += "1,";
                }

                if (GridFollowSettings.RowDefinitions[4].Height.Value == 25)
                {// journal
                    result += "0,";
                }
                else
                {
                    result += "1,";
                }

                if (GridPrime.RowDefinitions[1].Height.Value == 25)
                {// log
                    result += "0";
                }
                else if (GridPrime.RowDefinitions[1].Height.Value == 83)
                {
                    result += "1";
                }
                else if (GridPrime.RowDefinitions[1].Height.Value == 250)
                {
                    result += "2";
                }

                CopyTraderInstance.PanelsPosition = result;
                ServerMaster.SaveCopyMaster();
            }
            catch (Exception ex)
            {
                CopyTraderInstance.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void LoadPanelsPositions()
        {
            if(string.IsNullOrEmpty(CopyTraderInstance.PanelsPosition))
            {
                return;
            }

            string[] save = CopyTraderInstance.PanelsPosition.Split(',');

            if (save[0] == "0")
            {
                GridFollowSettings.RowDefinitions[1].Height = new GridLength(25, GridUnitType.Pixel);
                ButtonRobotsGridDown.IsEnabled = false;
            }
            else
            {
                ButtonRobotsGridUp.IsEnabled = false;
            }

            if (save[1] == "0")
            {
                GridFollowSettings.RowDefinitions[2].Height = new GridLength(25, GridUnitType.Pixel);
                ButtonSlaveGridDown.IsEnabled = false;
            }
            else
            {
                ButtonSlaveGridUp.IsEnabled = false;
            }

            if (save[2] == "0")
            {
                GridFollowSettings.RowDefinitions[3].Height = new GridLength(25, GridUnitType.Pixel);
                ButtonSecuritiesGridDown.IsEnabled = false;
            }
            else
            {
                ButtonSecuritiesGridUp.IsEnabled = false;
            }

            if (save[3] == "0")
            {
                GridFollowSettings.RowDefinitions[4].Height = new GridLength(25, GridUnitType.Pixel);
                ButtonJournalGridDown.IsEnabled = false;
            }
            else
            {
                ButtonJournalGridUp.IsEnabled = false;
            }

            if (save[4] == "0")
            {
                GridPrime.RowDefinitions[1].Height = new GridLength(25, GridUnitType.Pixel);
                ButtonLogDown.IsEnabled = false;
            }
            else if (save[4] == "1")
            {
                GridPrime.RowDefinitions[1].Height = new GridLength(83, GridUnitType.Star);
            }
            else
            {
                GridPrime.RowDefinitions[1].Height = new GridLength(250, GridUnitType.Star);
                ButtonLogUp.IsEnabled = false;
            }
        }

        #endregion

        #region Painter thread

        private bool _windowIsClosed;

        private void PainterThreadArea()
        {
            while(true)
            {
                try
                {
                    Thread.Sleep(3000);

                    if (_windowIsClosed == true)
                    {
                        return;
                    }

                    UpdateGridRobots();
                }
                catch(Exception ex)
                {
                    CopyTraderInstance.SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        #endregion

        #region Robots grid

        private DataGridView _gridRobots;

        private void CreateRobotsGrid()
        {
            _gridRobots = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect,
  DataGridViewAutoSizeRowsMode.AllCells);
            _gridRobots.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridRobots.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = "#"; // num
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _gridRobots.Columns.Add(column0);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = OsLocalization.Market.Label164; // Name
            column2.ReadOnly = true;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _gridRobots.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = OsLocalization.Market.Label200; // Type
            column3.ReadOnly = true;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridRobots.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = OsLocalization.Market.Label182; // Is On
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            column4.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _gridRobots.Columns.Add(column4);

            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            column5.HeaderText = OsLocalization.Market.Label7; // Security
            column5.ReadOnly = true;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridRobots.Columns.Add(column5);

            DataGridViewColumn column6 = new DataGridViewColumn();
            column6.CellTemplate = cell0;
            column6.HeaderText = OsLocalization.Market.Label205; // Long
            column6.ReadOnly = true;
            column6.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _gridRobots.Columns.Add(column6);

            DataGridViewColumn column7 = new DataGridViewColumn();
            column7.CellTemplate = cell0;
            column7.HeaderText = OsLocalization.Market.Label206; // Short
            column7.ReadOnly = true;
            column7.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _gridRobots.Columns.Add(column7);

            DataGridViewColumn column8 = new DataGridViewColumn();
            column8.CellTemplate = cell0;
            column8.HeaderText = OsLocalization.Market.Label207; // Abs
            column8.ReadOnly = true;
            column8.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _gridRobots.Columns.Add(column8);

            HostRobots.Child = _gridRobots;
            _gridRobots.CellValueChanged += _gridRobots_CellValueChanged;
            _gridRobots.DataError += _gridRobots_DataError;
        }

        private void _gridRobots_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            CopyTraderInstance.SendLogMessage("_gridRobots_DataError \n"
                + e.Exception.ToString(), Logging.LogMessageType.Error);
        }

        private void _gridRobots_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                // 1 сохранение активированых к копированию роботов

                List<string> namesOnRobots = new List<string>();

                for (int i = 0; i < _gridRobots.Rows.Count; i++)
                {
                    DataGridViewCell cell = _gridRobots.Rows[i].Cells[0];

                    if(cell == null)
                    {
                        continue;
                    }

                    bool isOn = Convert.ToBoolean(_gridRobots.Rows[i].Cells[3].Value);

                    if(isOn == true)
                    {
                        namesOnRobots.Add(_gridRobots.Rows[i].Cells[1].Value.ToString());
                    }
                }

                CopyTraderInstance.OnRobotsNames = namesOnRobots;

                ServerMaster.SaveCopyMaster();
            }
            catch (Exception ex)
            {
                CopyTraderInstance.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }
         
        private void UpdateGridRobots()
        {
            try
            {
                if (_gridRobots.InvokeRequired)
                {
                    _gridRobots.Invoke(new Action(UpdateGridRobots));
                    return;
                }

                // 0 num
                // 1 Name
                // 2 Type
                // 3 Is On
                // 4 Security
                // 5 Long
                // 6 Short
                // 7 Abs

                List<BotPanel> bots = ServerMaster.GetAllBotsFromBotStation();

                List<DataGridViewRow> rowsNow = new List<DataGridViewRow>();

                for(int i = 0; i < bots.Count;i++)
                {
                    List<DataGridViewRow> botRows = GetRowsByRobot(bots[i], i);

                    if(botRows!= null &&
                        botRows.Count > 0)
                    {
                        rowsNow.AddRange(botRows);
                    }
                }

                if(rowsNow.Count != _gridRobots.Rows.Count)
                { // 1 перерисовываем целиком
                    _gridRobots.Rows.Clear();

                    for(int i = 0;i < rowsNow.Count;i++)
                    {
                        _gridRobots.Rows.Add(rowsNow[i]);
                    }
                }
                else
                { // 2 перерисовываем по линиям
                    for(int i = 0;i < _gridRobots.Rows.Count;i++)
                    {
                        TryRePaintRobotRow(_gridRobots.Rows[i], rowsNow[i]);

                    }
                }
            }
            catch (Exception ex)
            {
                CopyTraderInstance.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TryRePaintRobotRow(DataGridViewRow rowInGrid, DataGridViewRow rowNew)
        {
            if (rowInGrid.Cells[3].Value != null)
            {
                if(rowInGrid.Cells[3].Value.ToString() != rowNew.Cells[3].Value.ToString())
                {
                    rowInGrid.Cells[3].Value = rowNew.Cells[3].Value;
                }
                
                if (rowInGrid.Cells[3].Style.ForeColor != rowNew.Cells[3].Style.ForeColor)
                {
                    rowInGrid.Cells[3].Style.ForeColor = rowNew.Cells[3].Style.ForeColor;
                }
            }

            if (rowInGrid.Cells[5].Value != null)
            {
                if(rowInGrid.Cells[5].Value.ToString() != rowNew.Cells[5].Value.ToString())
                {
                    rowInGrid.Cells[5].Value = rowNew.Cells[5].Value;
                }
                if (rowInGrid.Cells[5].Style.ForeColor != rowNew.Cells[5].Style.ForeColor)
                {
                    rowInGrid.Cells[5].Style.ForeColor = rowNew.Cells[5].Style.ForeColor;
                }
            }

            if (rowInGrid.Cells[6].Value != null)
            {
                if (rowInGrid.Cells[6].Value.ToString() != rowNew.Cells[6].Value.ToString())
                {
                    rowInGrid.Cells[6].Value = rowNew.Cells[6].Value;
                }
                if (rowInGrid.Cells[6].Style.ForeColor != rowNew.Cells[6].Style.ForeColor)
                {
                    rowInGrid.Cells[6].Style.ForeColor = rowNew.Cells[6].Style.ForeColor;
                }
            }

            if (rowInGrid.Cells[7].Value != null)
            {
                if (rowInGrid.Cells[7].Value.ToString() != rowNew.Cells[7].Value.ToString())
                {
                    rowInGrid.Cells[7].Value = rowNew.Cells[7].Value;
                }
                if (rowInGrid.Cells[7].Style.ForeColor != rowNew.Cells[7].Style.ForeColor)
                {
                    rowInGrid.Cells[7].Style.ForeColor = rowNew.Cells[7].Style.ForeColor;
                }
            }

        }

        private List<DataGridViewRow> GetRowsByRobot(BotPanel bot, int number)
        {
            // 0 num
            // 1 Name
            // 2 Type
            // 3 Is On
            // 4 Security
            // 5 Long
            // 6 Short
            // 7 Abs

            List<DataGridViewRow> botRow = new List<DataGridViewRow>();

            // 1 формируем первую строку по роботу

            DataGridViewRow rowFirst = new DataGridViewRow();

            rowFirst.Cells.Add(new DataGridViewTextBoxCell());
            rowFirst.Cells[rowFirst.Cells.Count - 1].Value = number;

            rowFirst.Cells.Add(new DataGridViewTextBoxCell());
            rowFirst.Cells[rowFirst.Cells.Count - 1].Value = bot.NameStrategyUniq;

            rowFirst.Cells.Add(new DataGridViewTextBoxCell());
            rowFirst.Cells[rowFirst.Cells.Count - 1].Value = bot.GetType().Name;

            bool botIsOnToCopy = CopyTraderInstance.BotIsOnToCopy(bot);

            DataGridViewComboBoxCell cellIsOn = new DataGridViewComboBoxCell();
            cellIsOn.Items.Add("True");
            cellIsOn.Items.Add("False");
            cellIsOn.Value = botIsOnToCopy.ToString();
            cellIsOn.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            rowFirst.Cells.Add(cellIsOn);

            if (botIsOnToCopy == true)
            {
                rowFirst.Cells[rowFirst.Cells.Count - 1].Style.ForeColor = System.Drawing.Color.Green;
            }

            rowFirst.Cells.Add(new DataGridViewTextBoxCell());
            rowFirst.Cells.Add(new DataGridViewTextBoxCell());
            rowFirst.Cells.Add(new DataGridViewTextBoxCell());
            rowFirst.Cells.Add(new DataGridViewTextBoxCell());

            botRow.Add(rowFirst);

            // 2 формируем записи по бумагам у робота

            List<Security> securities = bot.GetSecuritiesInTradeSources();

            for(int i = 0;i < securities.Count;i++)
            {
                botRow.Add(GetRowBySecurity(securities[i],bot));
            }

            return botRow;
        }

        private DataGridViewRow GetRowBySecurity(Security security, BotPanel bot)
        {
            // 0 num
            // 1 Name
            // 2 Type
            // 3 Is On
            // 4 Security
            // 5 Long
            // 6 Short
            // 7 Abs

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells.Add(new DataGridViewTextBoxCell());

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[row.Cells.Count - 1].Value = security.Name + "_" + security.NameClass;

            List<Position> poses = bot.GetPositionsBySecurity(security);

            decimal longPosition = 0;
            decimal shortPosition = 0;
            decimal absPosition = 0;

            for(int i = 0;i < poses.Count;i++)
            {
                Position position = poses[i];

                decimal openVolume = position.OpenVolume;

                if(openVolume == 0)
                {
                    continue;
                }

                if(position.Direction == Side.Buy)
                {
                    longPosition += openVolume;
                    absPosition += openVolume;
                }
                else
                {
                    shortPosition -= openVolume;
                    absPosition -= openVolume;
                }
            }

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[row.Cells.Count - 1].Value = longPosition;

            if (longPosition != 0)
            {
                row.Cells[row.Cells.Count - 1].Style.ForeColor = System.Drawing.Color.Green;
            }

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[row.Cells.Count - 1].Value = shortPosition;

            if (shortPosition != 0)
            {
                row.Cells[row.Cells.Count - 1].Style.ForeColor = System.Drawing.Color.DarkRed;
            }

            row.Cells.Add(new DataGridViewTextBoxCell()); 
            row.Cells[row.Cells.Count - 1].Value = absPosition;

            if (absPosition != 0)
            {
                row.Cells[row.Cells.Count - 1].Style.ForeColor = System.Drawing.Color.White;
            }

            return row;
        }

        private void ButtonRobotsGridDown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonRobotsGridUp.IsEnabled = true;
                GridFollowSettings.RowDefinitions[1].Height = new GridLength(25, GridUnitType.Pixel);
                ButtonRobotsGridDown.IsEnabled = false;
            }
            catch (Exception ex)
            {
                CopyTraderInstance.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
            SavePanelsPosition();
        }

        private void ButtonRobotsGridUp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonRobotsGridDown.IsEnabled = true;
                GridFollowSettings.RowDefinitions[1].Height = new GridLength(185, GridUnitType.Star);
                ButtonRobotsGridUp.IsEnabled = false;

            }
            catch (Exception ex)
            {
                CopyTraderInstance.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
            SavePanelsPosition();
        }

        #endregion

        #region Slave grid

        private void ButtonSlaveGridDown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonSlaveGridUp.IsEnabled = true;
                GridFollowSettings.RowDefinitions[2].Height = new GridLength(25, GridUnitType.Pixel);
                ButtonSlaveGridDown.IsEnabled = false;
            }
            catch (Exception ex)
            {
                CopyTraderInstance.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
            SavePanelsPosition();
        }

        private void ButtonSlaveGridUp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonSlaveGridDown.IsEnabled = true;
                GridFollowSettings.RowDefinitions[2].Height = new GridLength(185, GridUnitType.Star);
                ButtonSlaveGridUp.IsEnabled = false;
            }
            catch (Exception ex)
            {
                CopyTraderInstance.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
            SavePanelsPosition();
        }

        #endregion

        #region Securities grid

        private void ButtonSecuritiesGridDown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonSecuritiesGridUp.IsEnabled = true;
                GridFollowSettings.RowDefinitions[3].Height = new GridLength(25, GridUnitType.Pixel);
                ButtonSecuritiesGridDown.IsEnabled = false;
            }
            catch (Exception ex)
            {
                CopyTraderInstance.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
            SavePanelsPosition();
        }

        private void ButtonSecuritiesGridUp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonSecuritiesGridDown.IsEnabled = true;
                GridFollowSettings.RowDefinitions[3].Height = new GridLength(185, GridUnitType.Star);
                ButtonSecuritiesGridUp.IsEnabled = false;
            }
            catch (Exception ex)
            {
                CopyTraderInstance.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
            SavePanelsPosition();
        }

        #endregion

        #region Journal grid

        private void ButtonJournalGridDown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonJournalGridUp.IsEnabled = true;
                GridFollowSettings.RowDefinitions[4].Height = new GridLength(25, GridUnitType.Pixel);
                ButtonJournalGridDown.IsEnabled = false;
            }
            catch (Exception ex)
            {
                CopyTraderInstance.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
            SavePanelsPosition();
        }

        private void ButtonJournalGridUp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonJournalGridDown.IsEnabled = true;
                GridFollowSettings.RowDefinitions[4].Height = new GridLength(185, GridUnitType.Star);
                ButtonJournalGridUp.IsEnabled = false;
            }
            catch (Exception ex)
            {
                CopyTraderInstance.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
            SavePanelsPosition();
        }

        #endregion

        #region Log

        private void ButtonLogDown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonLogUp.IsEnabled = true;

                if (GridPrime.RowDefinitions[1].Height.Value == 250)
                {
                    GridPrime.RowDefinitions[1].Height = new GridLength(83, GridUnitType.Star);
                }
                else // if (GridPrime.RowDefinitions[1].Height.Value == 500)
                {
                    GridPrime.RowDefinitions[1].Height = new GridLength(25, GridUnitType.Pixel);

                    ButtonLogDown.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                CopyTraderInstance.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
            SavePanelsPosition();
        }

        private void ButtonLogUp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ButtonLogDown.IsEnabled = true;

                if (GridPrime.RowDefinitions[1].Height.Value == 83)
                {
                    GridPrime.RowDefinitions[1].Height = new GridLength(250, GridUnitType.Star);
                    ButtonLogUp.IsEnabled = false;
                }
                else //if (GridPrime.RowDefinitions[1].Height.Value != 800)
                {
                    GridPrime.RowDefinitions[1].Height = new GridLength(83, GridUnitType.Star);
                }
            }
            catch (Exception ex)
            {
                CopyTraderInstance.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
            SavePanelsPosition();
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        public void SendNewLogMessage(string message, LogMessageType messageType)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent.Invoke(message, messageType);
            }
            else
            {
                ServerMaster.SendNewLogMessage(message, messageType);
            }
        }

        #endregion

    }
}
