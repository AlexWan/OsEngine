/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;

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

            _set = set;

            if (set.SetName != "Set_")
            {
                TextBoxFolderName.IsEnabled = false;
                DatePickerTimeEnd.IsEnabled = false;
                DatePickerTimeStart.IsEnabled = false;
               
            }

            TextBoxFolderName.Text = set.SetName.Split('_')[1];

            ComboBoxRegime.Items.Add(DataSetState.Off);
            ComboBoxRegime.Items.Add(DataSetState.On);
            ComboBoxRegime.SelectedItem = _set.Regime;
            ComboBoxRegime.SelectionChanged += ComboBoxRegime_SelectionChanged;

            CheckBoxTf1SecondIsOn.IsChecked = set.Tf1SecondIsOn;
            CheckBoxTf2SecondIsOn.IsChecked = set.Tf2SecondIsOn;
            CheckBoxTf5SecondIsOn.IsChecked = set.Tf5SecondIsOn;
            CheckBoxTf10SecondIsOn.IsChecked = set.Tf10SecondIsOn;
            CheckBoxTf15SecondIsOn.IsChecked = set.Tf15SecondIsOn;
            CheckBoxTf20SecondIsOn.IsChecked = set.Tf20SecondIsOn;
            CheckBoxTf30SecondIsOn.IsChecked = set.Tf30SecondIsOn;
            CheckBoxTf1MinuteIsOn.IsChecked = set.Tf1MinuteIsOn;
            CheckBoxTf2MinuteIsOn.IsChecked = set.Tf2MinuteIsOn;
            CheckBoxTf5MinuteIsOn.IsChecked = set.Tf5MinuteIsOn;
            CheckBoxTf10MinuteIsOn.IsChecked = set.Tf10MinuteIsOn;
            CheckBoxTf15MinuteIsOn.IsChecked = set.Tf15MinuteIsOn;
            CheckBoxTf30MinuteIsOn.IsChecked = set.Tf30MinuteIsOn;
            CheckBoxTf1HourIsOn.IsChecked = set.Tf1HourIsOn;
            CheckBoxTf2HourIsOn.IsChecked = set.Tf2HourIsOn;
            CheckBoxTf4HourIsOn.IsChecked = set.Tf4HourIsOn;
            CheckBoxTfTickIsOn.IsChecked = set.TfTickIsOn;
            CheckBoxTfMarketDepthIsOn.IsChecked = set.TfMarketDepthIsOn;

            CheckBoxNeadToLoadDataInServers.IsChecked = set.NeadToLoadDataInServers;

            List < ServerType > serverTypes = ServerMaster.ServersTypes;
            ComboBoxSource.Items.Add(ServerType.None);

            for (int i = 0; i < serverTypes.Count; i++)
            {
                ComboBoxSource.Items.Add(serverTypes[i]);
            }

            ComboBoxSource.SelectedItem = _set.Source;
            ComboBoxSource.SelectionChanged += ComboBoxSource_SelectionChanged;
            DatePickerTimeStart.SelectedDate = _set.TimeStart;
            DatePickerTimeEnd.SelectedDate = _set.TimeEnd;

            ComboBoxCandleCreateType.Items.Add(CandleMarketDataType.Tick);
            ComboBoxCandleCreateType.Items.Add(CandleMarketDataType.MarketDepth);
            ComboBoxCandleCreateType.SelectedItem = _set.CandleCreateType;

            CheckBoxNeadToUpDate.IsChecked = _set.NeadToUpdate;

            for (int i = 1; i < 26; i++)
            {
                ComboBoxMarketDepthDepth.Items.Add(i);
            }

            if (_set.MarketDepthDepth == 0)
            {
                _set.MarketDepthDepth = 1;
            }

            ComboBoxMarketDepthDepth.SelectedItem = _set.MarketDepthDepth;


            CreateSecuritiesTable();
            ReloadSecuritiesOnTable();
            CheckButtons();
            Title = OsLocalization.Data.TitleDataSet;
            Label3.Content = OsLocalization.Data.Label3;
            Label4.Content = OsLocalization.Data.Label4;
            Label15.Content = OsLocalization.Data.Label15;
            Label16.Content = OsLocalization.Data.Label16;
            Label17.Content = OsLocalization.Data.Label17;
            Label18.Content = OsLocalization.Data.Label18;
            Label19.Content = OsLocalization.Data.Label19;
            Label20.Content = OsLocalization.Data.Label20;
            ButtonAccept.Content = OsLocalization.Data.ButtonAccept;
            CheckBoxNeadToLoadDataInServers.Content = OsLocalization.Data.Label21;
            CheckBoxNeadToUpDate.Content = OsLocalization.Data.Label22;

        }

        /// <summary>
        /// switched source
        /// переключили источник
        /// </summary>
        void ComboBoxSource_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
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
                DisableControls();
            }
            else
            {
                EnableControls();
                if (ComboBoxSource.SelectedItem != null && 
                    ComboBoxSource.SelectedItem.ToString() == "Finam")
                {
                    CheckBoxTf2HourIsOn.IsEnabled = false;
                    CheckBoxTf2HourIsOn.IsChecked = false;

                    CheckBoxTf4HourIsOn.IsEnabled = false;
                    CheckBoxTf4HourIsOn.IsChecked = false;

                    CheckBoxTf2MinuteIsOn.IsChecked = false;
                    CheckBoxTf2MinuteIsOn.IsEnabled = false;

                    CheckBoxTfMarketDepthIsOn.IsChecked = false;
                    CheckBoxTfMarketDepthIsOn.IsEnabled = false;
                }
                else
                {
                    CheckBoxTf2HourIsOn.IsEnabled = true;
                    CheckBoxTf4HourIsOn.IsEnabled = true;
                    CheckBoxTf2MinuteIsOn.IsEnabled = true;
                    CheckBoxTfMarketDepthIsOn.IsEnabled = true;
                }
            }


        }

        /// <summary>
        /// prevent user from touching controls
        /// запретить пользователю трогать контролы
        /// </summary>
        private void DisableControls()
        {
            CheckBoxTf1SecondIsOn.IsEnabled = false;
            CheckBoxTf2SecondIsOn.IsEnabled = false;
            CheckBoxTf5SecondIsOn.IsEnabled = false;
            CheckBoxTf10SecondIsOn.IsEnabled = false;
            CheckBoxTf15SecondIsOn.IsEnabled = false;
            CheckBoxTf20SecondIsOn.IsEnabled = false;
            CheckBoxTf30SecondIsOn.IsEnabled = false;
            CheckBoxTf1MinuteIsOn.IsEnabled = false;
            CheckBoxTf2MinuteIsOn.IsEnabled = false;
            CheckBoxTf5MinuteIsOn.IsEnabled = false;
            CheckBoxTf10MinuteIsOn.IsEnabled = false;
            CheckBoxTf15MinuteIsOn.IsEnabled = false;
            CheckBoxTf30MinuteIsOn.IsEnabled = false;
            CheckBoxTf1HourIsOn.IsEnabled = false;
            CheckBoxTf2HourIsOn.IsEnabled = false;
            CheckBoxTf4HourIsOn.IsEnabled = false;
            CheckBoxTfTickIsOn.IsEnabled = false;
            CheckBoxTfMarketDepthIsOn.IsEnabled = false;
            ComboBoxSource.IsEnabled = false;
            DatePickerTimeStart.IsEnabled = false;
            DatePickerTimeEnd.IsEnabled = false;
            ButtonAddSecurity.IsEnabled = false;
            ButtonDelSecurity.IsEnabled = false;
            ComboBoxCandleCreateType.IsEnabled = false;
            ComboBoxMarketDepthDepth.IsEnabled = false;
            CheckBoxTf2HourIsOn.IsEnabled = false;
            CheckBoxTf4HourIsOn.IsEnabled = false;
            CheckBoxTf2MinuteIsOn.IsEnabled = false;
            CheckBoxTfMarketDepthIsOn.IsEnabled = false;
            CheckBoxNeadToUpDate.IsEnabled = false;
            CheckBoxNeadToLoadDataInServers.IsEnabled = false;
        }

        /// <summary>
        /// allow user to touch controls
        /// разрешить пользователю трогать контролы
        /// </summary>
        private void EnableControls()
        {
            CheckBoxTf1SecondIsOn.IsEnabled = true;
            CheckBoxTf2SecondIsOn.IsEnabled = true;
            CheckBoxTf5SecondIsOn.IsEnabled = true;
            CheckBoxTf10SecondIsOn.IsEnabled = true;
            CheckBoxTf15SecondIsOn.IsEnabled = true;
            CheckBoxTf20SecondIsOn.IsEnabled = true;
            CheckBoxTf30SecondIsOn.IsEnabled = true;
            CheckBoxTf1MinuteIsOn.IsEnabled = true;
            CheckBoxTf2MinuteIsOn.IsEnabled = true;
            CheckBoxTf5MinuteIsOn.IsEnabled = true;
            CheckBoxTf10MinuteIsOn.IsEnabled = true;
            CheckBoxTf15MinuteIsOn.IsEnabled = true;
            CheckBoxTf30MinuteIsOn.IsEnabled = true;
            CheckBoxTf1HourIsOn.IsEnabled = true;
            CheckBoxTf2HourIsOn.IsEnabled = true;
            CheckBoxTf4HourIsOn.IsEnabled = true;
            CheckBoxTfTickIsOn.IsEnabled = true;
            CheckBoxTfMarketDepthIsOn.IsEnabled = true;
            ComboBoxSource.IsEnabled = true;
            CheckBoxNeadToUpDate.IsEnabled = true;
            ButtonAddSecurity.IsEnabled = true;
            ButtonDelSecurity.IsEnabled = true;
            ComboBoxCandleCreateType.IsEnabled = true;
            ComboBoxMarketDepthDepth.IsEnabled = true;
            CheckBoxNeadToLoadDataInServers.IsEnabled = true;

            if (TextBoxFolderName.IsEnabled == false)
            {
                DatePickerTimeEnd.IsEnabled = false;
                DatePickerTimeStart.IsEnabled = false;
            }
            else
            {
                DatePickerTimeStart.IsEnabled = true;
                DatePickerTimeEnd.IsEnabled = true;
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
            _set.Regime = regime;

            _set.Tf1SecondIsOn = CheckBoxTf1SecondIsOn.IsChecked.Value;
            _set.Tf2SecondIsOn = CheckBoxTf2SecondIsOn.IsChecked.Value;
            _set.Tf5SecondIsOn = CheckBoxTf5SecondIsOn.IsChecked.Value;
            _set.Tf10SecondIsOn = CheckBoxTf10SecondIsOn.IsChecked.Value;
            _set.Tf15SecondIsOn = CheckBoxTf15SecondIsOn.IsChecked.Value;
            _set.Tf20SecondIsOn = CheckBoxTf20SecondIsOn.IsChecked.Value;
            _set.Tf30SecondIsOn = CheckBoxTf30SecondIsOn.IsChecked.Value;
            _set.Tf1MinuteIsOn = CheckBoxTf1MinuteIsOn.IsChecked.Value;
            _set.Tf2MinuteIsOn = CheckBoxTf2MinuteIsOn.IsChecked.Value;
            _set.Tf5MinuteIsOn = CheckBoxTf5MinuteIsOn.IsChecked.Value;
            _set.Tf10MinuteIsOn = CheckBoxTf10MinuteIsOn.IsChecked.Value;
            _set.Tf15MinuteIsOn = CheckBoxTf15MinuteIsOn.IsChecked.Value;
            _set.Tf30MinuteIsOn = CheckBoxTf30MinuteIsOn.IsChecked.Value;
            _set.Tf1HourIsOn = CheckBoxTf1HourIsOn.IsChecked.Value;
            _set.Tf2HourIsOn = CheckBoxTf2HourIsOn.IsChecked.Value;
            _set.Tf4HourIsOn = CheckBoxTf4HourIsOn.IsChecked.Value;
            _set.TfTickIsOn = CheckBoxTfTickIsOn.IsChecked.Value;
            _set.TfMarketDepthIsOn = CheckBoxTfMarketDepthIsOn.IsChecked.Value;
            _set.MarketDepthDepth = Convert.ToInt32(ComboBoxMarketDepthDepth.SelectedValue.ToString());

            Enum.TryParse(ComboBoxCandleCreateType.Text, out _set.CandleCreateType);

            Enum.TryParse(ComboBoxSource.SelectedItem.ToString(), out _set.Source);

            _set.TimeStart = DatePickerTimeStart.SelectedDate.Value;
            _set.TimeEnd = DatePickerTimeEnd.SelectedDate.Value;

            _set.NeadToUpdate = CheckBoxNeadToUpDate.IsChecked.Value;

            _set.NeadToLoadDataInServers =  CheckBoxNeadToLoadDataInServers.IsChecked.Value;

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
            _grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None);

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
            List<SecurityToLoad> names = _set.SecuritiesNames;

            for (int i = 0;names != null &&  i < names.Count; i++)
            {
                DataGridViewRow row = new DataGridViewRow();
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = names[i].Name;
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

            TextBoxFolderName.Text = TextBoxFolderName.Text.Replace("_", "");
            TextBoxFolderName.Text = TextBoxFolderName.Text.Replace("\\", "");
            TextBoxFolderName.Text = TextBoxFolderName.Text.Replace("/", "");

            SaveSettings();

            IsSaved = true;
            Close();
        }
    }
}
