/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Windows.Markup;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;

namespace OsEngine.OsData
{
    /// <summary>
    /// Interaction Logic for OsDataSetUi.xaml
    /// Логика взаимодействия для OsDataSetUi.xaml
    /// </summary>
    public partial class OsDataSetUi
    {
        /// <summary>
        /// set belonging to this window
        /// сет принадлежащий этому окну
        /// </summary>
        private OsDataSet _set;

        /// <summary>
        /// is the set saved
        /// сохранён ли сет
        /// </summary>
        public bool IsSaved;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="set">set that needs to be managed/сет которым надо управлять</param>
        public OsDataSetUi(OsDataSet set)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _set = set;

            if (set.SetName != "Set_")
            {
                TextBoxFolderName.IsEnabled = false;              
            }

            TextBoxFolderName.Text = set.SetName.Split('_')[1];

            if (_set.BaseSettings.Source == ServerType.None 
                && (set.SetName == null || set.SetName == "Set_"))
            {
                TextBoxFolderName.Text = OsLocalization.Data.Label45;
                ComboBoxSource.Visibility = System.Windows.Visibility.Hidden;
                TextBoxFolderName.TextChanged += TextBoxFolderName_TextChanged;
                TextBoxFolderName.MouseEnter += TextBoxFolderName_MouseEnter;
            }
            
            ComboBoxRegime.Items.Add(DataSetState.Off);
            ComboBoxRegime.Items.Add(DataSetState.On);
            ComboBoxRegime.SelectedItem = _set.BaseSettings.Regime;
            ComboBoxRegime.SelectionChanged += ComboBoxRegime_SelectionChanged;

            CheckBoxTf1SecondIsOn.IsChecked = set.BaseSettings.Tf1SecondIsOn;
            CheckBoxTf2SecondIsOn.IsChecked = set.BaseSettings.Tf2SecondIsOn;
            CheckBoxTf5SecondIsOn.IsChecked = set.BaseSettings.Tf5SecondIsOn;
            CheckBoxTf10SecondIsOn.IsChecked = set.BaseSettings.Tf10SecondIsOn;
            CheckBoxTf15SecondIsOn.IsChecked = set.BaseSettings.Tf15SecondIsOn;
            CheckBoxTf20SecondIsOn.IsChecked = set.BaseSettings.Tf20SecondIsOn;
            CheckBoxTf30SecondIsOn.IsChecked = set.BaseSettings.Tf30SecondIsOn;
            CheckBoxTf1MinuteIsOn.IsChecked = set.BaseSettings.Tf1MinuteIsOn;
            CheckBoxTf2MinuteIsOn.IsChecked = set.BaseSettings.Tf2MinuteIsOn;
            CheckBoxTf5MinuteIsOn.IsChecked = set.BaseSettings.Tf5MinuteIsOn;
            CheckBoxTf10MinuteIsOn.IsChecked = set.BaseSettings.Tf10MinuteIsOn;
            CheckBoxTf15MinuteIsOn.IsChecked = set.BaseSettings.Tf15MinuteIsOn;
            CheckBoxTf30MinuteIsOn.IsChecked = set.BaseSettings.Tf30MinuteIsOn;
            CheckBoxTf1HourIsOn.IsChecked = set.BaseSettings.Tf1HourIsOn;
            CheckBoxTf2HourIsOn.IsChecked = set.BaseSettings.Tf2HourIsOn;
            CheckBoxTf4HourIsOn.IsChecked = set.BaseSettings.Tf4HourIsOn;
            CheckBoxTfTickIsOn.IsChecked = set.BaseSettings.TfTickIsOn;
            CheckBoxTfDayIsOn.IsChecked = set.BaseSettings.TfDayIsOn;
            CheckBoxTfMarketDepthIsOn.IsChecked = set.BaseSettings.TfMarketDepthIsOn;

            List < ServerType > serverTypes = ServerMaster.ActiveServersTypes;

            ComboBoxSource.Items.Add(ServerType.None);

            for (int i = 0; i < serverTypes.Count; i++)
            {
                ComboBoxSource.Items.Add(serverTypes[i]);
            }

            ComboBoxSource.SelectedItem = _set.BaseSettings.Source;

            if (ComboBoxSource.SelectedItem == null)
            {
                ComboBoxSource.Items.Add(_set.BaseSettings.Source);
                ComboBoxSource.SelectedItem = _set.BaseSettings.Source;
            }

            ComboBoxSource.SelectionChanged += ComboBoxSource_SelectionChanged;

            DatePickerTimeStart.SelectedDate = _set.BaseSettings.TimeStart;
            DatePickerTimeEnd.SelectedDate = _set.BaseSettings.TimeEnd;

            CheckBoxNeadToUpDate.IsChecked = _set.BaseSettings.NeadToUpdate;

            for (int i = 1; i < 26; i++)
            {
                ComboBoxMarketDepthDepth.Items.Add(i);
            }

            if (_set.BaseSettings.MarketDepthDepth == 0)
            {
                _set.BaseSettings.MarketDepthDepth = 1;
            }

            ComboBoxMarketDepthDepth.SelectedItem = _set.BaseSettings.MarketDepthDepth;

            CreateSecuritiesTable();
            ReloadSecuritiesOnTable();
            CheckButtons();
            Title = OsLocalization.Data.TitleDataSet;
            Label3.Content = OsLocalization.Data.Label3;
            Label4.Content = OsLocalization.Data.Label4;
            Label16.Content = OsLocalization.Data.Label16;
            Label17.Content = OsLocalization.Data.Label17;
            Label18.Content = OsLocalization.Data.Label18;
            Label19.Content = OsLocalization.Data.Label19;
            Label20.Content = OsLocalization.Data.Label20;
            ButtonAccept.Content = OsLocalization.Data.ButtonAccept;
            CheckBoxNeadToUpDate.Content = OsLocalization.Data.Label22;

            this.Activate();
            this.Focus();
        }

        private void TextBoxFolderName_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if(TextBoxFolderName.Text == OsLocalization.Data.Label45)
            {
                TextBoxFolderName.Text = "";
                TextBoxFolderName.MouseEnter -= TextBoxFolderName_MouseEnter;
            }
        }

        private void TextBoxFolderName_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string text = TextBoxFolderName.Text;

            text = text
              .Replace("/", "")
              .Replace("\\", "")
              .Replace("*", "")
              .Replace("-", "")
              .Replace("+", "")
              .Replace(":", "")
              .Replace("@", "")
              .Replace(";", "")
              .Replace("%", "")
              .Replace(">", "")
              .Replace("<", "")
              .Replace("^", "")
              .Replace("{", "")
              .Replace("}", "")
              .Replace("[", "")
              .Replace("]", "")
              .Replace("_", "")
              .Replace("`", "")
              .Replace("(", "")
              .Replace(")", "")
              .Replace("$", "")
              .Replace("#", "")
              .Replace("!", "")
              .Replace("&", "")
              .Replace("?", "")
              .Replace("=", "")
              .Replace(",", "")
              .Replace(".", "")
              .Replace("'", "")
              .Replace("|", "")
              .Replace("~", "")
              .Replace("№", "")
              .Replace("\"", "");

            if(text != TextBoxFolderName.Text)
            {
                TextBoxFolderName.Text = text;
                return;
            }

            if (string.IsNullOrEmpty(text))
            {
                ComboBoxSource.Visibility = System.Windows.Visibility.Hidden;
            }
            else
            {
                ComboBoxSource.Visibility = System.Windows.Visibility.Visible;
            }
        }

        /// <summary>
        /// switched source
        /// переключили источник
        /// </summary>
        void ComboBoxSource_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ComboBoxSource.SelectedItem != null)
            {
                if(ComboBoxSource.SelectedItem.ToString() == "None")
                {
                    return;
                }

                Enum.TryParse(ComboBoxSource.SelectedItem.ToString(), out _set.BaseSettings.Source);
            }

            SaveSettings();

            CheckButtons();
        }

        /// <summary>
        /// set mode changed
        /// изменён режим работы сета
        /// </summary>
        void ComboBoxRegime_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            CheckButtons();
        }

        /// <summary>
        /// check button activity
        /// проверить активность кнопок
        /// </summary>
        private void CheckButtons()
        {
            DataSetState currentState;

            Enum.TryParse(ComboBoxRegime.SelectedItem.ToString(), out currentState);

            if (currentState == DataSetState.On)
            {
                EnableControls(false);
            }
            else
            {
                EnableControls();

                IServerPermission permission = null;

                if (ComboBoxSource.SelectedItem != null)
                {
                    ServerType type;
                    Enum.TryParse(ComboBoxSource.SelectedItem.ToString(), out type);
                    permission = ServerMaster.GetServerPermission(type);
                }

                CheckBoxTf1SecondIsOn.IsEnabled = false;
                CheckBoxTf2SecondIsOn.IsEnabled = false;
                CheckBoxTf5SecondIsOn.IsEnabled = false;
                CheckBoxTf10SecondIsOn.IsEnabled = false;
                CheckBoxTf15SecondIsOn.IsEnabled = false;
                CheckBoxTf20SecondIsOn.IsEnabled = false;
                CheckBoxTf30SecondIsOn.IsEnabled = false;

                CheckBoxTfMarketDepthIsOn.IsEnabled = false;

                if (permission == null)
                {
                    CheckBoxTf1MinuteIsOn.IsEnabled = true;
                    CheckBoxTf2MinuteIsOn.IsEnabled = true;
                    CheckBoxTf5MinuteIsOn.IsEnabled = true;
                    CheckBoxTf10MinuteIsOn.IsEnabled = true;
                    CheckBoxTf15MinuteIsOn.IsEnabled = true;
                    CheckBoxTf30MinuteIsOn.IsEnabled = true;
                    CheckBoxTf1HourIsOn.IsEnabled = true;
                    CheckBoxTf2HourIsOn.IsEnabled = true;
                    CheckBoxTf4HourIsOn.IsEnabled = true;
                    CheckBoxTfDayIsOn.IsEnabled = true;

                    CheckBoxTf1SecondIsOn.IsEnabled = false;
                    CheckBoxTf2SecondIsOn.IsEnabled = false;
                    CheckBoxTf5SecondIsOn.IsEnabled = false;
                    CheckBoxTf10SecondIsOn.IsEnabled = false;
                    CheckBoxTf15SecondIsOn.IsEnabled = false;
                    CheckBoxTf20SecondIsOn.IsEnabled = false;
                    CheckBoxTf30SecondIsOn.IsEnabled = false;

                    CheckBoxTfMarketDepthIsOn.IsEnabled = false;
                    CheckBoxTfTickIsOn.IsEnabled = true;
                }
                else
                {
                    UpdComboBoxToPermission(CheckBoxTf1MinuteIsOn, permission.DataFeedTf1MinuteCanLoad);
                    UpdComboBoxToPermission(CheckBoxTf2MinuteIsOn,permission.DataFeedTf2MinuteCanLoad);
                    UpdComboBoxToPermission(CheckBoxTf5MinuteIsOn,permission.DataFeedTf5MinuteCanLoad);
                    UpdComboBoxToPermission(CheckBoxTf10MinuteIsOn,permission.DataFeedTf10MinuteCanLoad);
                    UpdComboBoxToPermission(CheckBoxTf15MinuteIsOn,permission.DataFeedTf15MinuteCanLoad);
                    UpdComboBoxToPermission(CheckBoxTf30MinuteIsOn,permission.DataFeedTf30MinuteCanLoad);
                    UpdComboBoxToPermission(CheckBoxTf1HourIsOn,permission.DataFeedTf1HourCanLoad);
                    UpdComboBoxToPermission(CheckBoxTf2HourIsOn,permission.DataFeedTf2HourCanLoad);
                    UpdComboBoxToPermission(CheckBoxTf4HourIsOn,permission.DataFeedTf4HourCanLoad);

                    UpdComboBoxToPermission(CheckBoxTf1SecondIsOn,permission.DataFeedTf1SecondCanLoad);
                    UpdComboBoxToPermission(CheckBoxTf2SecondIsOn,permission.DataFeedTf2SecondCanLoad);
                    UpdComboBoxToPermission(CheckBoxTf5SecondIsOn,permission.DataFeedTf5SecondCanLoad);
                    UpdComboBoxToPermission(CheckBoxTf10SecondIsOn,permission.DataFeedTf10SecondCanLoad);
                    UpdComboBoxToPermission(CheckBoxTf15SecondIsOn,permission.DataFeedTf15SecondCanLoad);
                    UpdComboBoxToPermission(CheckBoxTf20SecondIsOn,permission.DataFeedTf20SecondCanLoad);
                    UpdComboBoxToPermission(CheckBoxTf30SecondIsOn, permission.DataFeedTf30SecondCanLoad);

                    UpdComboBoxToPermission(CheckBoxTfMarketDepthIsOn,permission.DataFeedTfMarketDepthCanLoad);
                    UpdComboBoxToPermission(CheckBoxTfTickIsOn,permission.DataFeedTfTickCanLoad);
                    UpdComboBoxToPermission(CheckBoxTfDayIsOn, permission.DataFeedTfDayCanLoad);
                }
            }
        }

        private void UpdComboBoxToPermission(System.Windows.Controls.CheckBox box, bool permission)
        {
            box.IsEnabled = permission;
            if (permission == false)
            {
                box.IsChecked = false;
            }
        }

        /// <summary>
        /// allow user to touch controls
        /// разрешить пользователю трогать контролы
        /// </summary>
        private void EnableControls(bool Enabled=true)
        {
            
            ComboBoxSource.IsEnabled = Enabled;
            DatePickerTimeStart.IsEnabled = Enabled;
            DatePickerTimeEnd.IsEnabled = Enabled;

            ButtonAddSecurity.IsEnabled = Enabled;
            ButtonDelSecurity.IsEnabled = Enabled;
            ComboBoxMarketDepthDepth.IsEnabled = Enabled;

            if(Enabled == false)
            {
                StopUsePanelOne.Width = 338;
                StopUsePanelTwo.Width = 125;
            }
            else
            {
                StopUsePanelOne.Width = 1;
                StopUsePanelTwo.Width = 1;
            }
        }

        /// <summary>
        /// save settings
        /// сохранить настройки
        /// </summary>
        private void SaveSettings()
        {
            TextBoxFolderName.Text = TextBoxFolderName.Text.Replace("_", "");
            TextBoxFolderName.Text = TextBoxFolderName.Text.Replace("Set", "");

            _set.SetName = "Set_" + TextBoxFolderName.Text;

            DataSetState regime;
            Enum.TryParse(ComboBoxRegime.SelectedItem.ToString(), out regime );
            _set.BaseSettings.Regime = regime;

            _set.BaseSettings.Tf1SecondIsOn = CheckBoxTf1SecondIsOn.IsChecked.Value;
            _set.BaseSettings.Tf2SecondIsOn = CheckBoxTf2SecondIsOn.IsChecked.Value;
            _set.BaseSettings.Tf5SecondIsOn = CheckBoxTf5SecondIsOn.IsChecked.Value;
            _set.BaseSettings.Tf10SecondIsOn = CheckBoxTf10SecondIsOn.IsChecked.Value;
            _set.BaseSettings.Tf15SecondIsOn = CheckBoxTf15SecondIsOn.IsChecked.Value;
            _set.BaseSettings.Tf20SecondIsOn = CheckBoxTf20SecondIsOn.IsChecked.Value;
            _set.BaseSettings.Tf30SecondIsOn = CheckBoxTf30SecondIsOn.IsChecked.Value;
            _set.BaseSettings.Tf1MinuteIsOn = CheckBoxTf1MinuteIsOn.IsChecked.Value;
            _set.BaseSettings.Tf2MinuteIsOn = CheckBoxTf2MinuteIsOn.IsChecked.Value;
            _set.BaseSettings.Tf5MinuteIsOn = CheckBoxTf5MinuteIsOn.IsChecked.Value;
            _set.BaseSettings.Tf10MinuteIsOn = CheckBoxTf10MinuteIsOn.IsChecked.Value;
            _set.BaseSettings.Tf15MinuteIsOn = CheckBoxTf15MinuteIsOn.IsChecked.Value;
            _set.BaseSettings.Tf30MinuteIsOn = CheckBoxTf30MinuteIsOn.IsChecked.Value;
            _set.BaseSettings.Tf1HourIsOn = CheckBoxTf1HourIsOn.IsChecked.Value;
            _set.BaseSettings.Tf2HourIsOn = CheckBoxTf2HourIsOn.IsChecked.Value;
            _set.BaseSettings.Tf4HourIsOn = CheckBoxTf4HourIsOn.IsChecked.Value;
            _set.BaseSettings.TfTickIsOn = CheckBoxTfTickIsOn.IsChecked.Value;
            _set.BaseSettings.TfDayIsOn = CheckBoxTfDayIsOn.IsChecked.Value;

            _set.BaseSettings.TfMarketDepthIsOn = CheckBoxTfMarketDepthIsOn.IsChecked.Value;
            _set.BaseSettings.MarketDepthDepth = Convert.ToInt32(ComboBoxMarketDepthDepth.SelectedValue.ToString());

            if (ComboBoxSource.SelectedItem != null)
            {
                Enum.TryParse(ComboBoxSource.SelectedItem.ToString(), out _set.BaseSettings.Source);
                TextBoxFolderName.IsEnabled = false;
            }

            _set.BaseSettings.TimeStart = DatePickerTimeStart.SelectedDate.Value;
            _set.BaseSettings.TimeEnd = DatePickerTimeEnd.SelectedDate.Value;

            _set.BaseSettings.NeadToUpdate = CheckBoxNeadToUpDate.IsChecked.Value;

            if(_set.SecuritiesLoad != null)
            {
                for(int i = 0;i < _set.SecuritiesLoad.Count;i++)
                {
                    _set.SecuritiesLoad[i].CopySettingsFromParam(_set.BaseSettings);
                }
            }

            
            _set.Save();
        }

        // paperwork/работа с бумагами

        /// <summary>
        /// Securities table
        /// таблица бумаг
        /// </summary>
        private DataGridView _grid;

        /// <summary>
        /// create a securities storage table
        /// создать таблицу хранения бумаг
        /// </summary>
        private void CreateSecuritiesTable()
        {
            _grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _grid.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Data.Label14;
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column0);

            HostSecurities.Child = _grid;
        }

        /// <summary>
        /// reload securities storage table
        /// перезагрузить таблицу хранения бумаг
        /// </summary>
        private void ReloadSecuritiesOnTable()
        {
            _grid.Rows.Clear();
            List<SecurityToLoad> names = _set.SecuritiesLoad;

            for (int i = 0;names != null &&  i < names.Count; i++)
            {
                DataGridViewRow row = new DataGridViewRow();
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = names[i].SecName;
                _grid.Rows.Insert(0, row);
            }
        }

        /// <summary>
        /// User clicked on button to add new paper to set
        /// пользоваетль нажал на кнопку добавить новую бумагу к сету
        /// </summary>
        private void ButtonAddSecurity_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _set.AddNewSecurity();
            ReloadSecuritiesOnTable();
        }

        /// <summary>
        /// User is requesting paper removal from the set.
        /// пользователь запрашивает удаление бумаги из сета
        /// </summary>
        private void ButtonDelSecurity_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_grid.CurrentCell == null)
            {
                return;
            }

            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Data.Label42);
            ui.ShowDialog();

            if (ui.UserAcceptActioin == false)
            {
                return;
            }

            _set.DeleteSecurity(_grid.Rows.Count -1 -_grid.CurrentCell.RowIndex);
            ReloadSecuritiesOnTable();
        }

        private void ButtonAccept_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (TextBoxFolderName.Text == "")
            {
                MessageBox.Show(OsLocalization.Data.Label23);
                return;
            }

            if(ComboBoxSource.SelectedItem == null ||
                ComboBoxSource.SelectedItem.ToString() == "None")
            {
                MessageBox.Show(OsLocalization.Data.Label44);
                return;
            }

            TextBoxFolderName.Text = TextBoxFolderName.Text.Replace("_", "");
            TextBoxFolderName.Text = TextBoxFolderName.Text.Replace("\\", "");
            TextBoxFolderName.Text = TextBoxFolderName.Text.Replace("/", "");

            SaveSettings();

            IsSaved = true;
            Close();
        }
    }
}
