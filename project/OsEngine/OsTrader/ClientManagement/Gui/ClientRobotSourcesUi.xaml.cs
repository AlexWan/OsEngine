/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Windows;
using System.Windows.Controls;
using static Google.Rpc.Context.AttributeContext.Types;

namespace OsEngine.OsTrader.ClientManagement.Gui
{
    public partial class ClientRobotSourcesUi : Window
    {
        private TradeClient _client;
        private TradeClientRobot _robot;
        private TradeClientSourceSettings _source;

        public ClientRobotSourcesUi(TradeClientRobot robot, 
            TradeClient client)
        {
            InitializeComponent();

            _robot = robot;
            _client = client;

            StickyBorders.Listen(this);
            GlobalGUILayout.Listen(this, "TradeClientRobotSources" + robot.Number);

            this.Closed += ClientRobotSourcesUi_Closed;

            if(_robot.SourceSettings == null
                || _robot.SourceSettings.Count == 0)
            {
                return;
            }

            TextBoxSelectedRobot.Text = robot.BotClassName;

            for(int i = 0;i < _robot.SourceSettings.Count;i++)
            {
                BotTabType botTab = _robot.SourceSettings[i].BotTabType;

                if(botTab == BotTabType.Simple)
                {
                    ComboBoxSources.Items.Add(i + "#Simple");
                }
                else if (botTab == BotTabType.Screener)
                {
                    ComboBoxSources.Items.Add(i + "#Screener");
                }
                else if (botTab == BotTabType.Index)
                {
                    ComboBoxSources.Items.Add(i + "#Index");
                }
                else if (botTab == BotTabType.Pair)
                {
                    ComboBoxSources.Items.Add(i + "#Pair");
                }
            }

            ComboBoxSources.SelectedIndex = 0;
            ComboBoxSources.SelectionChanged += ComboBoxSources_SelectionChanged;

            _source = _robot.SourceSettings[0];
            SetSourceOnForm(_source);

            LabelRobotType.Content = OsLocalization.Trader.Label166;
            LabelRobotSource.Content = OsLocalization.Trader.Label409;
            this.Title = OsLocalization.Trader.Label609 + " #" + _robot.Number;

            TabItemCommonSettings.Header = OsLocalization.Trader.Label232;
            TabItemSecurities.Header = OsLocalization.Trader.Label164;
            TabItemIndexSettings.Header = OsLocalization.Trader.Label611;

            TabItemIndexSettings.IsEnabled = false;

            if (_source.BotTabType == BotTabType.Index)
            {
                TabItemIndexSettings.IsEnabled = true;
            }
            if (_source.BotTabType == BotTabType.Pair)
            {
                TabItemIndexSettings.IsEnabled = true;
            }

            ComboBoxRegimeIndexBuilder.Items.Add(IndexAutoFormulaBuilderRegime.Off.ToString());
            ComboBoxRegimeIndexBuilder.Items.Add(IndexAutoFormulaBuilderRegime.OncePerWeek.ToString());
            ComboBoxRegimeIndexBuilder.Items.Add(IndexAutoFormulaBuilderRegime.OncePerDay.ToString());
            ComboBoxRegimeIndexBuilder.Items.Add(IndexAutoFormulaBuilderRegime.OncePerHour.ToString());
            ComboBoxRegimeIndexBuilder.SelectedItem = _source.RegimeIndexBuilder.ToString();
            ComboBoxRegimeIndexBuilder.SelectionChanged += ComboBoxRegimeIndexBuilder_SelectionChanged;

            ComboBoxDayOfWeekToRebuildIndex.Items.Add(DayOfWeek.Monday.ToString());
            ComboBoxDayOfWeekToRebuildIndex.Items.Add(DayOfWeek.Tuesday.ToString());
            ComboBoxDayOfWeekToRebuildIndex.Items.Add(DayOfWeek.Wednesday.ToString());
            ComboBoxDayOfWeekToRebuildIndex.Items.Add(DayOfWeek.Thursday.ToString());
            ComboBoxDayOfWeekToRebuildIndex.Items.Add(DayOfWeek.Friday.ToString());
            ComboBoxDayOfWeekToRebuildIndex.Items.Add(DayOfWeek.Saturday.ToString());
            ComboBoxDayOfWeekToRebuildIndex.Items.Add(DayOfWeek.Sunday.ToString());
            ComboBoxDayOfWeekToRebuildIndex.SelectedItem = _source.DayOfWeekToRebuildIndex.ToString();
            ComboBoxDayOfWeekToRebuildIndex.SelectionChanged += ComboBoxDayOfWeekToRebuildIndex_SelectionChanged;

            for (int i = 0; i < 24; i++)
            {
                ComboBoxHourInDayToRebuildIndex.Items.Add(i.ToString());
            }
            ComboBoxHourInDayToRebuildIndex.SelectedItem = _source.HourInDayToRebuildIndex.ToString();
            ComboBoxHourInDayToRebuildIndex.SelectionChanged += ComboBoxHourInDayToRebuildIndex_SelectionChanged;

            CheckBoxPercentNormalization.IsChecked = _source.PercentNormalization;
            CheckBoxPercentNormalization.Checked += CheckBoxPercentNormalization_Checked;
            CheckBoxPercentNormalization.Unchecked += CheckBoxPercentNormalization_Checked;

            TextBoxDepth.Text = _source.CalculationDepth.ToString();
            TextBoxDepth.TextChanged += TextBoxDepth_TextChanged;

            ComboBoxIndexSortType.Items.Add(SecuritySortType.FirstInArray.ToString());
            ComboBoxIndexSortType.Items.Add(SecuritySortType.VolumeWeighted.ToString());
            ComboBoxIndexSortType.Items.Add(SecuritySortType.MaxVolatilityWeighted.ToString());
            ComboBoxIndexSortType.Items.Add(SecuritySortType.MinVolatilityWeighted.ToString());
            ComboBoxIndexSortType.SelectedItem = _source.IndexSortType.ToString();
            ComboBoxIndexSortType.SelectionChanged += ComboBoxIndexSortType_SelectionChanged;

            for (int i = 1; i < 301; i++)
            {
                ComboBoxIndexSecCount.Items.Add(i.ToString());
            }
            ComboBoxIndexSecCount.SelectedItem = _source.IndexSecCount.ToString();
            ComboBoxIndexSecCount.SelectionChanged += ComboBoxIndexSecCount_SelectionChanged;

            ComboBoxIndexMultType.Items.Add(IndexMultType.PriceWeighted.ToString());
            ComboBoxIndexMultType.Items.Add(IndexMultType.VolumeWeighted.ToString());
            ComboBoxIndexMultType.Items.Add(IndexMultType.EqualWeighted.ToString());
            ComboBoxIndexMultType.Items.Add(IndexMultType.Cointegration.ToString());
            ComboBoxIndexMultType.SelectedItem = _source.IndexMultType.ToString();
            ComboBoxIndexMultType.SelectionChanged += ComboBoxIndexMultType_SelectionChanged;

            for (int i = 1; i < 301; i++)
            {
                ComboBoxDaysLookBackInBuilding.Items.Add(i.ToString());
            }
            ComboBoxDaysLookBackInBuilding.SelectedItem = _source.DaysLookBackInBuilding.ToString();
            ComboBoxDaysLookBackInBuilding.SelectionChanged += ComboBoxDaysLookBackInBuilding_SelectionChanged;

            TextboxUserFormula.Text = _source.UserFormula;
            TextboxUserFormula.TextChanged += TextboxUserFormula_TextChanged;

        }

        private void TextboxUserFormula_TextChanged(object sender, TextChangedEventArgs e) 
        {
            try
            {
                _source.UserFormula = TextboxUserFormula.Text;
                _client.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxDaysLookBackInBuilding_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                _source.DaysLookBackInBuilding = Convert.ToInt32(ComboBoxDaysLookBackInBuilding.SelectedItem.ToString());
                _client.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxIndexMultType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxIndexMultType.SelectedItem.ToString(), out _source.IndexMultType);
                _client.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxIndexSecCount_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                _source.IndexSecCount = Convert.ToInt32(ComboBoxIndexSecCount.SelectedItem.ToString());
                _client.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxIndexSortType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxIndexSortType.SelectedItem.ToString(), out _source.IndexSortType);
                _client.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxDepth_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _source.CalculationDepth = Convert.ToInt32(TextBoxDepth.Text);
                _client.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxPercentNormalization_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                _source.PercentNormalization = CheckBoxPercentNormalization.IsChecked.Value;
                _client.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxHourInDayToRebuildIndex_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                _source.HourInDayToRebuildIndex = Convert.ToInt32(ComboBoxHourInDayToRebuildIndex.SelectedItem.ToString());
                _client.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxDayOfWeekToRebuildIndex_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxDayOfWeekToRebuildIndex.SelectedItem.ToString(), out _source.DayOfWeekToRebuildIndex);
                _client.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxRegimeIndexBuilder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxRegimeIndexBuilder.SelectedItem.ToString(), out _source.RegimeIndexBuilder);
                _client.Save();
            }
            catch
            {
                // ignore
            }
        }

        #region Common settings

        private void ClientRobotSourcesUi_Closed(object sender, EventArgs e)
        {
            _robot = null;
            _client = null;
        }

        private void ComboBoxSources_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int number = ComboBoxSources.SelectedIndex;

            _source = _robot.SourceSettings[number];

            TabItemIndexSettings.IsEnabled = false;

            if(_source.BotTabType == BotTabType.Index)
            {
                TabItemIndexSettings.IsEnabled = true;
            }
            if (_source.BotTabType == BotTabType.Pair)
            {
                TabItemIndexSettings.IsEnabled = true;
            }

            SetSourceOnForm(_source);
        }

        private void SetSourceOnForm(TradeClientSourceSettings source)
        {
            // 1 отписываемся от событий изменений в контролах

            TextBoxServerNum.TextChanged -= TextBoxServerNum_TextChanged;
            ComboBoxCommissionType.SelectionChanged -= ComboBoxCommissionType_SelectionChanged;
            TextBoxCommissionValue.TextChanged -= TextBoxCommissionValue_TextChanged;
            ComboBoxCandleMarketDataType.SelectionChanged -= ComboBoxCandleMarketDataType_SelectionChanged;
            CheckBoxSaveTradesInCandle.Checked -= CheckBoxSaveTradesInCandle_Checked;
            CheckBoxSaveTradesInCandle.Unchecked -= CheckBoxSaveTradesInCandle_Checked; 
            ComboBoxTimeFrame.SelectionChanged -= ComboBoxTimeFrame_SelectionChanged;

            // 2 обновляем данные

            TextBoxServerNum.Text = source.ClientServerNum.ToString();

            ComboBoxCommissionType.Items.Clear();
            ComboBoxCommissionType.Items.Add(CommissionType.None.ToString());
            ComboBoxCommissionType.Items.Add(CommissionType.OneLotFix.ToString());
            ComboBoxCommissionType.Items.Add(CommissionType.Percent.ToString());
            ComboBoxCommissionType.SelectedItem = source.CommissionType.ToString();
           
            TextBoxCommissionValue.Text = source.CommissionValue.ToString();

            ComboBoxCandleMarketDataType.Items.Clear();
            ComboBoxCandleMarketDataType.Items.Add(CandleMarketDataType.Tick.ToString());
            ComboBoxCandleMarketDataType.Items.Add(CandleMarketDataType.MarketDepth.ToString());
            ComboBoxCandleMarketDataType.SelectedItem = source.CandleMarketDataType.ToString();

            CheckBoxSaveTradesInCandle.IsChecked = source.SaveTradesInCandle;

            ComboBoxTimeFrame.Items.Clear();
            ComboBoxTimeFrame.Items.Add(TimeFrame.Hour2.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Hour1.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Min30.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Min15.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Min10.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Min5.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Min1.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Sec30.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Sec15.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Sec10.ToString());
            ComboBoxTimeFrame.Items.Add(TimeFrame.Sec5.ToString());
            ComboBoxTimeFrame.SelectedItem = _source.TimeFrame.ToString();

            // 3 подписываемся

            TextBoxServerNum.TextChanged += TextBoxServerNum_TextChanged;
            ComboBoxCommissionType.SelectionChanged += ComboBoxCommissionType_SelectionChanged;
            TextBoxCommissionValue.TextChanged += TextBoxCommissionValue_TextChanged;
            ComboBoxCandleMarketDataType.SelectionChanged += ComboBoxCandleMarketDataType_SelectionChanged;
            CheckBoxSaveTradesInCandle.Checked += CheckBoxSaveTradesInCandle_Checked;
            CheckBoxSaveTradesInCandle.Unchecked += CheckBoxSaveTradesInCandle_Checked;
            ComboBoxTimeFrame.SelectionChanged += ComboBoxTimeFrame_SelectionChanged;
        }

        private void ComboBoxTimeFrame_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxTimeFrame.SelectedItem.ToString(), out _source.TimeFrame);
                _client.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxSaveTradesInCandle_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                _source.SaveTradesInCandle = CheckBoxSaveTradesInCandle.IsChecked.Value;
                _client.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxCandleMarketDataType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxCandleMarketDataType.SelectedItem.ToString(), out _source.CandleMarketDataType);
                _client.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxCommissionValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _source.CommissionValue = Convert.ToInt32(TextBoxCommissionValue.Text);
                _client.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxCommissionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxCommissionType.SelectedItem.ToString(), out _source.CommissionType);
                _client.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxServerNum_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _source.ClientServerNum = Convert.ToInt32(TextBoxServerNum.Text);
                _client.Save();
            }
            catch
            {
                // ignore
            }
        }

        #endregion


    }
}
