/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using MessageBox = System.Windows.MessageBox;

namespace OsEngine.Robots
{
    public partial class BotCreateUi
    {
        public BotCreateUi(List<string> botsIncluded, List<string> botsFromScript, StartProgram startProgram)
        {
            InitializeComponent();

            _botsIncluded = botsIncluded;
            _botsFromScript = botsFromScript;


            if (startProgram == StartProgram.IsOsTrader)
            {
                TextBoxName.Text = "MyNewBot";
            }
            else if (startProgram == StartProgram.IsOsOptimizer)
            {
                TextBoxName.Text = "No name";
                TextBoxName.IsEnabled = false;
            }

            Title = OsLocalization.Trader.Label59;
            LabelName.Content = OsLocalization.Trader.Label61;
            ButtonAccept.Content = OsLocalization.Trader.Label17;

            _gridIncludeBots = GetDataGridView();
            HostNamesIncludedBots.Child = _gridIncludeBots;

            _gridScriptBots = GetDataGridView();
            HostNamesScriptBots.Child = _gridScriptBots;

            UpdateGrids();

            ItemInclude.Header = OsLocalization.Charts.Label6;
            ItemScript.Header = OsLocalization.Charts.Label7;
        }

        private List<string> _botsIncluded;

        private List<string> _botsFromScript;

        public bool IsAccepted;

        public string NameBot;

        public string NameStrategy;

        public bool IsScript;

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TextBoxName.Text))
            {
                MessageBox.Show(OsLocalization.Trader.Label58);
                return;
            }

            if (IsScript)
            {
                NameStrategy = _gridScriptBots.SelectedCells[0].Value.ToString();
            }
            else if (IsScript == false)
            {
                NameStrategy = _gridIncludeBots.SelectedCells[0].Value.ToString();
            }

            NameBot = TextBoxName.Text;
            IsAccepted = true;
            Close();
        }

        // Grids

        private DataGridView _gridIncludeBots;
        private DataGridView _gridScriptBots;

        private void UpdateGrids()
        {
            if (_gridIncludeBots.InvokeRequired)
            {
                _gridIncludeBots.Invoke(new Action(UpdateGrids));
                return;
            }
            _gridIncludeBots.Rows.Clear();
            List<string> botName = _botsIncluded;

            for (int i = 0; i < botName.Count; i++)
            {
                _gridIncludeBots.Rows.Add(botName[i]);
            }
            _gridIncludeBots.Click += delegate (object sender, EventArgs args) { IsScript = false; };

            _gridScriptBots.Rows.Clear();
            List<string> botNameScript = _botsFromScript;

            for (int i = 0; i < botNameScript.Count; i++)
            {
                _gridScriptBots.Rows.Add(botNameScript[i]);
            }
            _gridScriptBots.Click += delegate (object sender, EventArgs args) { IsScript = true; };

            TabControlRobotNames.SelectionChanged += delegate (object sender, SelectionChangedEventArgs args)
            {
                if (TabControlRobotNames.SelectedIndex == 0)
                {
                    IsScript = false;
                }
                if (TabControlRobotNames.SelectedIndex == 1)
                {
                    IsScript = true;
                }
            };
        }

        private DataGridView GetDataGridView()
        {
            DataGridView grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCells);

            grid.ReadOnly = true;
            grid.ScrollBars = ScrollBars.Vertical;

            DataGridViewColumn column = new DataGridViewColumn();
            column.HeaderText = OsLocalization.Trader.Label60;
            column.CellTemplate = new DataGridViewTextBoxCell();
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(column);

            return grid;
        }

    }
}
