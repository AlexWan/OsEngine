﻿using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;

namespace OsEngine.Robots
{
    /// <summary>
    /// Interaction logic for BotCreateUi2.xaml
    /// </summary>
    public partial class BotCreateUi2 : Window
    {
        public BotCreateUi2(List<string> botsIncluded, List<string> botsFromScript, StartProgram startProgram)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            for (int i = 0; i < botsIncluded.Count; i++)
            {
                for (int i2 = 0; i2 < botsFromScript.Count; i2++)
                {
                    if (botsIncluded[i].Equals(botsFromScript[i2]))
                    {
                        botsIncluded.RemoveAt(i);
                        i--;
                        break;
                    }
                }
            }

            _botsIncluded = botsIncluded;
            _botsFromScript = botsFromScript;

            if (startProgram == StartProgram.IsOsTrader)
            {
                TextBoxName.Text = "MyNewBot";
            }

            Title = OsLocalization.Trader.Label59;
            LabelName.Content = OsLocalization.Trader.Label61;
            ButtonAccept.Content = OsLocalization.Trader.Label17;

            this.Activate();
            this.Focus();

            Closed += BotCreateUi2_Closed;

            ComboBoxLockation.Items.Add(BotCreationType.All.ToString());
            ComboBoxLockation.Items.Add(BotCreationType.Include.ToString());
            ComboBoxLockation.Items.Add(BotCreationType.Script.ToString());
            ComboBoxLockation.SelectedItem = BotCreationType.All.ToString();

            ComboBoxLockation.SelectionChanged += ComboBoxLockation_SelectionChanged;

            LabelLocation.Content = OsLocalization.Trader.Label295;

            CreateTable();
            UpdateTable();

            ButtonUpdateRobots.Content = OsLocalization.Trader.Label303;

            TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
            ButtonRightInSearchResults.Visibility = Visibility.Hidden;
            ButtonLeftInSearchResults.Visibility = Visibility.Hidden;
            LabelCurrentResultShow.Visibility = Visibility.Hidden;
            LabelCommasResultShow.Visibility = Visibility.Hidden;
            LabelCountResultsShow.Visibility = Visibility.Hidden;
            TextBoxSearchSecurity.MouseEnter += TextBoxSearchSecurity_MouseEnter;
            TextBoxSearchSecurity.TextChanged += TextBoxSearchSecurity_TextChanged;
            TextBoxSearchSecurity.MouseLeave += TextBoxSearchSecurity_MouseLeave;
            TextBoxSearchSecurity.LostKeyboardFocus += TextBoxSearchSecurity_LostKeyboardFocus;
            ButtonRightInSearchResults.Click += ButtonRightInSearchResults_Click;
            ButtonLeftInSearchResults.Click += ButtonLeftInSearchResults_Click;

        }

        private void ComboBoxLockation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTable();
        }

        private void BotCreateUi2_Closed(object sender, EventArgs e)
        {
            _botsIncluded = null;
            _botsFromScript = null;
            _lastLoadDescriptions = null;

            _grid.CellClick -= _grid_CellClick;
            _grid.Rows.Clear();
            DataGridFactory.ClearLinks(_grid);
            _grid = null;

            TextBoxSearchSecurity.MouseEnter -= TextBoxSearchSecurity_MouseEnter;
            TextBoxSearchSecurity.TextChanged -= TextBoxSearchSecurity_TextChanged;
            TextBoxSearchSecurity.MouseLeave -= TextBoxSearchSecurity_MouseLeave;
            TextBoxSearchSecurity.LostKeyboardFocus -= TextBoxSearchSecurity_LostKeyboardFocus;
            ButtonRightInSearchResults.Click -= ButtonRightInSearchResults_Click;
            ButtonLeftInSearchResults.Click -= ButtonLeftInSearchResults_Click;
        }

        private List<string> _botsIncluded;

        private List<string> _botsFromScript;

        public bool IsAccepted;

        public string NameBot;

        public string NameStrategy;

        public bool IsScript;

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            IsAccepted = true;
            NameBot = TextBoxName.Text;
            Close();
        }

        DataGridView _grid;

        private void CreateTable()
        {
            _grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.AllCells);
            _grid.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _grid.DefaultCellStyle;

            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = "#";
            colum1.ReadOnly = true;
            colum1.Width = 30;
            _grid.Columns.Add(colum1);

            DataGridViewColumn colum2 = new DataGridViewColumn();
            colum2.CellTemplate = cell0;
            colum2.HeaderText = OsLocalization.Trader.Label60;
            colum2.ReadOnly = true;
            colum2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _grid.Columns.Add(colum2);

            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = OsLocalization.Trader.Label295;
            colum3.Width = 110;

            _grid.Columns.Add(colum3);

            DataGridViewColumn colum4 = new DataGridViewColumn();
            colum4.CellTemplate = cell0;
            colum4.HeaderText = OsLocalization.Trader.Label296;
            colum4.ReadOnly = true;
            colum4.Width = 90;

            _grid.Columns.Add(colum4);

            DataGridViewColumn colum5 = new DataGridViewColumn();
            colum5.CellTemplate = cell0;
            colum5.HeaderText = OsLocalization.Trader.Label298;
            colum5.ReadOnly = true;
            colum5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _grid.Columns.Add(colum5);

            DataGridViewColumn colum6 = new DataGridViewColumn();
            colum6.CellTemplate = cell0;
            colum6.HeaderText = OsLocalization.Trader.Label297;
            colum6.ReadOnly = true;
            colum6.Width = 90;

            _grid.Columns.Add(colum6);

            HostBots.Child = _grid;

            _grid.CellClick += _grid_CellClick;
        }

        private void _grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int row = e.RowIndex;
            int col = e.ColumnIndex;

            if (row >= _grid.Rows.Count)
            {
                return;
            }

            Color baseColor = Color.FromArgb(154, 156, 158);

            for (int i = 0;i < _grid.Rows.Count;i++)
            {
                if (i == row)
                {
                    _grid.Rows[i].Cells[1].Style.ForeColor = Color.FromArgb(255, 85, 0);
                    _grid.Rows[i].Cells[1].Style.SelectionForeColor = Color.FromArgb(255, 85, 0);

                    NameStrategy = _grid.Rows[i].Cells[1].Value.ToString();

                    if(_grid.Rows[i].Cells[2].Value.ToString() == BotCreationType.Script.ToString())
                    {
                        IsScript = true;
                    }
                    else
                    {
                        IsScript = false;
                    }
                }
                else
                {
                    if (_grid.Rows[i].Cells[1].Style.ForeColor != baseColor)
                    {
                        _grid.Rows[i].Cells[1].Style.ForeColor = baseColor;
                        _grid.Rows[i].Cells[1].Style.SelectionForeColor = baseColor;
                    }

                }
            }

            try
            {
                if (col == 2)
                {
                    string cellValue = _grid.Rows[row].Cells[col].Value.ToString();

                    if (cellValue == BotCreationType.Script.ToString())
                    {
                        int botNum = Convert.ToInt32(_grid.Rows[row].Cells[0].Value.ToString());

                        string className = _grid.Rows[row].Cells[1].Value.ToString();

                        BotDescription description = null;

                        for (int i = 0; i < _lastLoadDescriptions.Count; i++)
                        {
                            if (_lastLoadDescriptions[i].ClassName == className)
                            {
                                description = _lastLoadDescriptions[i];
                                break;
                            }
                        }

                        string path =
                            System.Windows.Forms.Application.ExecutablePath.Replace("OsEngine.exe", "") 
                            + "Custom\\Robots\\";

                        string filePath = path + className + ".cs";

                        if (!File.Exists(filePath))
                        {
                            return;
                        }

                        string argument = "/select, \"" + filePath + "\"";

                        System.Diagnostics.Process.Start("explorer.exe", argument);

                    }
                }
                else if(col == 5)
                {
                    int botNum = Convert.ToInt32(_grid.Rows[row].Cells[0].Value.ToString());

                    string className = _grid.Rows[row].Cells[1].Value.ToString();

                    BotDescription description = null;

                    for (int i = 0; i < _lastLoadDescriptions.Count; i++)
                    {
                        if (_lastLoadDescriptions[i].ClassName == className)
                        {
                            description = _lastLoadDescriptions[i];
                            break;
                        }
                    }

                    if(description != null &&
                        string.IsNullOrEmpty(description.Description) == false)
                    {
                        CustomMessageBoxUi ui = new CustomMessageBoxUi(description.Description);
                        ui.ShowDialog();
                    }

                }
            }
            catch(Exception ex) 
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(ex.ToString());
                ui.Show();
            }
        }

        private void UpdateTable()
        {
            List<BotDescription> descriptions = GetBotDescriptions();

            for(int i = 0;i < _botsFromScript.Count;i++)
            {
                bool isInArray = false;

                for(int i2 = 0;i2 < descriptions.Count;i2++)
                {
                    if (descriptions[i2].ClassName == _botsFromScript[i])
                    {
                        isInArray = true;
                        break;
                    }
                    
                }

                if(isInArray == false)
                {
                    BotDescription newDesk = new BotDescription();
                    newDesk.ClassName = _botsFromScript[i];
                    newDesk.Description = "Script";
                    descriptions.Insert(0, newDesk);
                }
            }

            for (int i = 0; i < _botsIncluded.Count; i++)
            {
                bool isInArray = false;

                for (int i2 = 0; i2 < descriptions.Count; i2++)
                {
                    if (descriptions[i2].ClassName == _botsIncluded[i])
                    {
                        isInArray = true;
                        break;
                    }
                }

                if (isInArray == false)
                {
                    BotDescription newDesk = new BotDescription();
                    newDesk.ClassName = _botsIncluded[i];
                    newDesk.Description = "Include";
                    descriptions.Insert(0, newDesk);
                }
            }


            _grid.Rows.Clear();

            string lockation = ComboBoxLockation.SelectedItem.ToString();

            for(int i = 0;i < descriptions.Count;i++)
            {
                if(lockation == BotCreationType.All.ToString())
                {// роботы из всех мест
                    _grid.Rows.Add(GetRow(descriptions[i], i + 1));
                    continue;
                }

                if (descriptions[i].Location.ToString() != lockation)
                {// роботы из всех мест
                    continue;
                }

                _grid.Rows.Add(GetRow(descriptions[i], i + 1));
            }

            _lastLoadDescriptions = descriptions;
        }

        private List<BotDescription> _lastLoadDescriptions;

        private DataGridViewRow GetRow(BotDescription description, int num)
        {
            // 1 номер
            // 2 название
            // 3 Рсположение
            // 4 Источники
            // 5 Индикаторы
            // 6 Описание

            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = num.ToString();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = description.ClassName;

            if(description.Location == BotCreationType.Script)
            {
                DataGridViewButtonCell buttonLockation = new DataGridViewButtonCell();
                buttonLockation.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                buttonLockation.Value = description.Location;
                nRow.Cells.Add(buttonLockation);
            }
            else
            {
                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = description.Location;
            }

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[3].Value = description.SourcesToGrid;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[4].Value = description.IndicatorsToGrid;

            if (string.IsNullOrEmpty(description.Description) == false)
            {
                DataGridViewButtonCell button = new DataGridViewButtonCell();
                button.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                button.Value = "?";

                nRow.Cells.Add(button);
            }

            return nRow;
        }

        private List<BotDescription> GetBotDescriptions()
        {
            List<BotDescription> descriptions = GetBotDescriptionsFromFile();

            if(descriptions == null || descriptions.Count == 0)
            {
                descriptions = GetBotDesctiptionsAsunc();
                SaveDesctiptionsInFile(descriptions);
            }

            return descriptions;
        }

        private List<BotDescription> GetBotDescriptionsFromFile()
        {
            List <BotDescription> descriptions = new List<BotDescription>();

            if (!File.Exists(@"BotsDescriprion.txt"))
            {
                return descriptions;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"BotsDescriprion.txt"))
                {
                    while(reader.EndOfStream == false)
                    {
                        string saveStr = reader.ReadLine();

                        BotDescription description = new BotDescription();
                        description.LoadFromSaveStr(saveStr);

                        descriptions.Add(description);
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }

            return descriptions;
        }

        private void SaveDesctiptionsInFile(List<BotDescription> descriptions)
        {

            try
            {
                using (StreamWriter writer = new StreamWriter(@"BotsDescriprion.txt", false)
                    )
                {
                    for (int i = 0; i < descriptions.Count; i++)
                    {
                        writer.WriteLine(descriptions[i].GetStringToSave());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private List<string> GetIndicatorsFromBot(BotPanel bot)
        {
            List<string> indicators = new List<string>();

            if (bot.TabsSimple != null &&
               bot.TabsSimple.Count > 0)
            {
                for(int i = 0;i < bot.TabsSimple.Count;i++)
                {
                    List<string> curInd = GetIndicatorsNamesFromSimpleSource(bot.TabsSimple[i]);

                    if(curInd != null 
                        && curInd.Count > 0)
                    {
                        indicators.AddRange(curInd);
                    }
                }
            }

            if (bot.TabsIndex != null &&
                bot.TabsIndex.Count > 0)
            {
                for (int i = 0; i < bot.TabsIndex.Count; i++)
                {
                    List<string> curInd = GetIndicatorsNamesFromIndexSource(bot.TabsIndex[i]);

                    if (curInd != null
                        && curInd.Count > 0)
                    {
                        indicators.AddRange(curInd);
                    }
                }
            }

            if (bot.TabsScreener != null &&
                bot.TabsScreener.Count > 0)
            {
                for (int i = 0; i < bot.TabsScreener.Count; i++)
                {
                    List<string> curInd = GetIndicatorsNamesFromScreenerSource(bot.TabsScreener[i]);

                    if (curInd != null
                        && curInd.Count > 0)
                    {
                        indicators.AddRange(curInd);
                    }
                }
            }
            return indicators;
        }

        private List<string> GetIndicatorsNamesFromSimpleSource(BotTabSimple tab)
        {
            List<string> indicators = new List<string>();

            for(int i = 0;i <tab.Indicators.Count;i++)
            {
                string curInd = tab.Indicators[i].GetType().Name;

                indicators.Add(curInd);
            }

            return indicators;
        }

        private List<string> GetIndicatorsNamesFromIndexSource(BotTabIndex tab)
        {
            List<string> indicators = new List<string>();

            for (int i = 0; i < tab.Indicators.Count; i++)
            {
                string curInd = tab.Indicators[i].GetType().Name;

                indicators.Add(curInd);
            }

            return indicators;
        }

        private List<string> GetIndicatorsNamesFromScreenerSource(BotTabScreener tab)
        {
            List<string> indicators = new List<string>();

            for (int i = 0; i < tab._indicators.Count; i++)
            {
                string curInd = tab._indicators[i].Type;

                indicators.Add(curInd);
            }

            return indicators;
        }

        private List<string> GetSourcesFromBot(BotPanel bot)
        {
            List<string> sourcesList = new List<string>();

            if (bot.TabsSimple != null &&
                bot.TabsSimple.Count > 0)
            {
                sourcesList.Add(BotTabType.Simple + " " + bot.TabsSimple.Count);
            }

            if (bot.TabsIndex != null &&
                bot.TabsIndex.Count > 0)
            {
                sourcesList.Add(BotTabType.Index + " " + bot.TabsIndex.Count);
            }

            if (bot.TabsCluster != null &&
                bot.TabsCluster.Count > 0)
            {
                sourcesList.Add(BotTabType.Cluster + " " + bot.TabsCluster.Count);
            }

            if (bot.TabsPair != null &&
                bot.TabsPair.Count > 0)
            {
                sourcesList.Add(BotTabType.Pair + " " + bot.TabsPair.Count);
            }

            if (bot.TabsScreener != null &&
                bot.TabsScreener.Count > 0)
            {
                sourcesList.Add(BotTabType.Screener + " " + bot.TabsScreener.Count);
            }

            return sourcesList;
        }

        private void ButtonUpdateRobots_Click(object sender, RoutedEventArgs e)
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label305);

            ui.ShowDialog();

            if(ui.UserAcceptActioin == false)
            {
                return;
            }

            List<BotDescription> descriptions = GetBotDesctiptionsAsunc();
            SaveDesctiptionsInFile(descriptions);
            UpdateTable();
        }

        // загрузка описаний роботов из BotFactory

        private List<BotDescription> GetBotDesctiptionsAsunc()
        {
            _awaitUiBotsInfoLoading = new AwaitObject(OsLocalization.Trader.Label300, 100, 0, true);
            AwaitUi ui = new AwaitUi(_awaitUiBotsInfoLoading);

           

            _descriptionsFromBotFactoryLast = null;

            Thread worker = new Thread(GetBotDescriptionsFromBotFactory);
            worker.Start();

            ui.ShowDialog();

            return _descriptionsFromBotFactoryLast;
        }

        AwaitObject _awaitUiBotsInfoLoading;

        private void GetBotDescriptionsFromBotFactory()
        {
            List<BotDescription> descriptions = new List<BotDescription>();

            for (int i = 0; i < _botsIncluded.Count; i++)
            {
                BotDescription curDescription = GetBotDescription(_botsIncluded[i], false);

                if (curDescription != null)
                {
                    descriptions.Add(curDescription);
                }
            }

            for (int i = 0; i < _botsFromScript.Count; i++)
            {
                BotDescription curDescription = GetBotDescription(_botsFromScript[i], true);

                if (curDescription != null)
                {
                    descriptions.Add(curDescription);
                }
            }

            _descriptionsFromBotFactoryLast = descriptions;
            _awaitUiBotsInfoLoading.Dispose();
        }

        List<BotDescription> _descriptionsFromBotFactoryLast;

        private BotDescription GetBotDescription(string className, bool isScript)
        {
            BotPanel bot = null;

            try
            {
                bot = BotFactory.GetStrategyForName(className, "", StartProgram.IsTester, isScript);
            }
            catch
            {
                return null;
            }

            BotDescription description = new BotDescription();

            description.ClassName = className;

            description.Description = bot.Description;

            if (isScript)
            {
                description.Location = BotCreationType.Script;
            }
            else
            {
                description.Location = BotCreationType.Include;
            }

            description.Sources = GetSourcesFromBot(bot);

            description.Indicators = GetIndicatorsFromBot(bot);

            bot.Delete();

            return description;
        }

        #region поиск по таблице

        private void TextBoxSearchSecurity_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (TextBoxSearchSecurity.Text == ""
                && TextBoxSearchSecurity.IsKeyboardFocused == false)
            {
                TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
            }
        }

        private void TextBoxSearchSecurity_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (TextBoxSearchSecurity.Text == OsLocalization.Market.Label64)
            {
                TextBoxSearchSecurity.Text = "";
            }
        }

        private void TextBoxSearchSecurity_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (TextBoxSearchSecurity.Text == "")
            {
                TextBoxSearchSecurity.Text = OsLocalization.Market.Label64;
            }
        }

        List<int> _searchResults = new List<int>();

        private void TextBoxSearchSecurity_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateSearchResults();
            UpdateSearchPanel();
        }

        private void UpdateSearchResults()
        {
            _searchResults.Clear();

            string key = TextBoxSearchSecurity.Text;

            if (key == "")
            {
                UpdateSearchPanel();
                return;
            }

            key = key.ToLower();

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                string strategy = "";
                string secSources = "";
                string indicators = "";

                if (_grid.Rows[i].Cells[1].Value != null)
                {
                    strategy = _grid.Rows[i].Cells[1].Value.ToString();
                }

                if (_grid.Rows[i].Cells[3].Value != null)
                {
                    secSources = _grid.Rows[i].Cells[3].Value.ToString();
                }

                if (_grid.Rows[i].Cells[4].Value != null)
                {
                    indicators = _grid.Rows[i].Cells[4].Value.ToString();
                }

                strategy = strategy.ToLower();
                secSources = secSources.ToLower();
                indicators = indicators.ToLower();

                if (strategy.Contains(key) ||
                    secSources.Contains(key)||
                    indicators.Contains(key))
                {
                    _searchResults.Add(i);
                }
            }
        }

        private void UpdateSearchPanel()
        {
            if (_searchResults.Count == 0)
            {
                ButtonRightInSearchResults.Visibility = Visibility.Hidden;
                ButtonLeftInSearchResults.Visibility = Visibility.Hidden;
                LabelCurrentResultShow.Visibility = Visibility.Hidden;
                LabelCommasResultShow.Visibility = Visibility.Hidden;
                LabelCountResultsShow.Visibility = Visibility.Hidden;
                return;
            }

            int firstRow = _searchResults[0];

            _grid.Rows[firstRow].Selected = true;
            _grid.FirstDisplayedScrollingRowIndex = firstRow;

            if (_searchResults.Count < 2)
            {
                ButtonRightInSearchResults.Visibility = Visibility.Hidden;
                ButtonLeftInSearchResults.Visibility = Visibility.Hidden;
                LabelCurrentResultShow.Visibility = Visibility.Hidden;
                LabelCommasResultShow.Visibility = Visibility.Hidden;
                LabelCountResultsShow.Visibility = Visibility.Hidden;
                return;
            }

            LabelCurrentResultShow.Content = 1.ToString();
            LabelCountResultsShow.Content = (_searchResults.Count).ToString();

            ButtonRightInSearchResults.Visibility = Visibility.Visible;
            ButtonLeftInSearchResults.Visibility = Visibility.Visible;
            LabelCurrentResultShow.Visibility = Visibility.Visible;
            LabelCommasResultShow.Visibility = Visibility.Visible;
            LabelCountResultsShow.Visibility = Visibility.Visible;
        }

        private void ButtonLeftInSearchResults_Click(object sender, RoutedEventArgs e)
        {
            int indexRow = Convert.ToInt32(LabelCurrentResultShow.Content) - 1;

            int maxRowIndex = Convert.ToInt32(LabelCountResultsShow.Content);

            if (indexRow <= 0)
            {
                indexRow = maxRowIndex;
                LabelCurrentResultShow.Content = maxRowIndex.ToString();
            }
            else
            {
                LabelCurrentResultShow.Content = (indexRow).ToString();
            }

            int realInd = _searchResults[indexRow - 1];

            _grid.Rows[realInd].Selected = true;
            _grid.FirstDisplayedScrollingRowIndex = realInd;
        }

        private void ButtonRightInSearchResults_Click(object sender, RoutedEventArgs e)
        {
            int indexRow = Convert.ToInt32(LabelCurrentResultShow.Content) - 1 + 1;

            int maxRowIndex = Convert.ToInt32(LabelCountResultsShow.Content);

            if (indexRow >= maxRowIndex)
            {
                indexRow = 0;
                LabelCurrentResultShow.Content = 1.ToString();
            }
            else
            {
                LabelCurrentResultShow.Content = (indexRow + 1).ToString();
            }

            int realInd = _searchResults[indexRow];

            _grid.Rows[realInd].Selected = true;
            _grid.FirstDisplayedScrollingRowIndex = realInd;
        }

        #endregion

        private void ButtonWhyNeadName_Click(object sender, RoutedEventArgs e)
        {
            CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label301);
            ui.ShowDialog();
        }

        private void ButtonWhyLocation_Click(object sender, RoutedEventArgs e)
        {
            CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label302);
            ui.ShowDialog();
        }
    }

    public class BotDescription
    {
        public string ClassName = "";

        public string Description;

        public BotCreationType Location;

        public string PathToFolder = "";

        public List<string> Sources = new List<string>();

        public string SourcesToGrid
        {
            get
            {
                string res = "";

                for(int i = 0; Sources != null && i < Sources.Count;i++)
                {
                    res += Sources[i];

                    if(i +1 < Sources.Count)
                    {
                        res += "\n";
                    }
                }

                return res;
            }
        }

        public List<string> Indicators = new List<string>();

        public string IndicatorsToGrid
        {
            get
            {
                string res = "";

                for (int i = 0; Indicators != null && i < Indicators.Count; i++)
                {
                    res += Indicators[i];

                    if (i + 1 < Indicators.Count)
                    {
                        res += "\n";
                    }
                }

                return res;
            }
        }

        public string GetStringToSave()
        {
            string saveStr = "";

            saveStr += ClassName + "&";
            saveStr += Description + "&";
            saveStr += Location + "&";
            saveStr += PathToFolder + "&";

            string sources = "";

            for(int i = 0;i < Sources.Count;i++)
            {
                sources += Sources[i].ToString() + "*";
            }

            saveStr += sources + "&";

            string indicators = "";

            for (int i = 0; i < Indicators.Count; i++)
            {
                indicators += Indicators[i].ToString() + "*";
            }

            saveStr += indicators + "&";

            return saveStr;
        }

        public void LoadFromSaveStr(string str)
        {
            string [] saveStr = str.Split('&');

            ClassName = saveStr[0];
            Description = saveStr[1];
            Enum.TryParse(saveStr[2], out Location);
            
            PathToFolder = saveStr[3];

            string[] sources = saveStr[4].Split('*');

            for (int i = 0; i < sources.Length; i++)
            {
                string curSource = sources[i];

                if (string.IsNullOrEmpty(curSource) == false)
                {
                    Sources.Add(curSource);
                }
            }

            string[] indicators = saveStr[5].Split('*');

            for (int i = 0; i < indicators.Length; i++)
            {
                string curInd = indicators[i];

                if (string.IsNullOrEmpty(curInd) == false)
                {
                    Indicators.Add(curInd);
                }
            }
        }

    }

    public enum BotCreationType
    {
        Include,
        Script,
        All
    }
}