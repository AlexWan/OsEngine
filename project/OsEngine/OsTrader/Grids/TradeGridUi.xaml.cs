/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Layout;
using System;
using System.Windows;
using System.Windows.Controls;


namespace OsEngine.OsTrader.Grids
{
    /// <summary>
    /// Interaction logic for TradeGridUi.xaml
    /// </summary>
    public partial class TradeGridUi : Window
    {
        public TradeGridUi(TradeGrid tradeGrid)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);

            TradeGrid = tradeGrid;
            Number = TradeGrid.Number;

            Title = Title += " Grid " + tradeGrid.Number;

            Closed += TradeGridUi_Closed;

            this.Activate();
            this.Focus();

            GlobalGUILayout.Listen(this, "TradeGridUi" + tradeGrid.Number + tradeGrid.Tab.TabName);

            // settings prime

            ComboBoxGridType.Items.Add(TradeGridPrimeType.MarketMaking.ToString());
            ComboBoxGridType.Items.Add(TradeGridPrimeType.OpenPosition.ToString());
            ComboBoxGridType.Items.Add(TradeGridPrimeType.ClosePosition.ToString());
            ComboBoxGridType.SelectedItem = tradeGrid.GridType.ToString();
            ComboBoxGridType.SelectionChanged += ComboBoxGridType_SelectionChanged;

            TextBoxClosePositionNumber.Text = tradeGrid.ClosePositionNumber.ToString();
            TextBoxClosePositionNumber.TextChanged += TextBoxClosePositionNumber_TextChanged;

            ComboBoxRegime.Items.Add(TradeGridRegime.Off.ToString());
            ComboBoxRegime.Items.Add(TradeGridRegime.On.ToString());
            ComboBoxRegime.Items.Add(TradeGridRegime.CloseOnly.ToString());
            ComboBoxRegime.Items.Add(TradeGridRegime.CloseForced.ToString());
            ComboBoxRegime.SelectedItem = tradeGrid.Regime.ToString();
            ComboBoxRegime.SelectionChanged += ComboBoxRegime_SelectionChanged;

            ComboBoxRegimeLogicEntry.Items.Add(TradeGridLogicEntryRegime.OnTrade.ToString());
            ComboBoxRegimeLogicEntry.Items.Add(TradeGridLogicEntryRegime.OncePerSecond.ToString());
            ComboBoxRegimeLogicEntry.SelectedItem = tradeGrid.RegimeLogicEntry.ToString();
            ComboBoxRegimeLogicEntry.SelectionChanged += ComboBoxRegimeLogicEntry_SelectionChanged;

            ComboBoxRegimeLogging.Items.Add(TradeGridLoggingRegime.Standard.ToString());
            ComboBoxRegimeLogging.Items.Add(TradeGridLoggingRegime.Debug.ToString());
            ComboBoxRegimeLogging.SelectedItem = tradeGrid.RegimeLogging.ToString();
            ComboBoxRegimeLogging.SelectionChanged += ComboBoxRegimeLogging_SelectionChanged;

            ComboBoxAutoClearJournal.Items.Add("True");
            ComboBoxAutoClearJournal.Items.Add("False");
            ComboBoxAutoClearJournal.SelectedItem = tradeGrid.AutoClearJournalIsOn.ToString();
            ComboBoxAutoClearJournal.SelectionChanged += ComboBoxAutoClearJournal_SelectionChanged;

            TextBoxMaxClosePositionsInJournal.Text = tradeGrid.MaxClosePositionsInJournal.ToString();
            TextBoxMaxClosePositionsInJournal.TextChanged += TextBoxMaxClosePositionsInJournal_TextChanged;

            // non trade periods

            CheckBoxNonTradePeriod1OnOff.IsChecked = tradeGrid.NonTradePeriod1OnOff;
            CheckBoxNonTradePeriod1OnOff.Checked += CheckBoxNonTradePeriod1OnOff_Checked;

            CheckBoxNonTradePeriod2OnOff.IsChecked = tradeGrid.NonTradePeriod2OnOff;
            CheckBoxNonTradePeriod2OnOff.Checked += CheckBoxNonTradePeriod2OnOff_Checked;

            CheckBoxNonTradePeriod3OnOff.IsChecked = tradeGrid.NonTradePeriod3OnOff;
            CheckBoxNonTradePeriod3OnOff.Checked += CheckBoxNonTradePeriod3OnOff_Checked;

            CheckBoxNonTradePeriod4OnOff.IsChecked = tradeGrid.NonTradePeriod4OnOff;
            CheckBoxNonTradePeriod4OnOff.Checked += CheckBoxNonTradePeriod4OnOff_Checked; 

            CheckBoxNonTradePeriod5OnOff.IsChecked = tradeGrid.NonTradePeriod5OnOff;
            CheckBoxNonTradePeriod5OnOff.Checked += CheckBoxNonTradePeriod5OnOff_Checked;

            TextBoxNonTradePeriod1Start.Text = tradeGrid.NonTradePeriod1Start.ToString();
            TextBoxNonTradePeriod1Start.TextChanged += TextBoxNonTradePeriod1Start_TextChanged;

            TextBoxNonTradePeriod2Start.Text = tradeGrid.NonTradePeriod2Start.ToString();
            TextBoxNonTradePeriod2Start.TextChanged += TextBoxNonTradePeriod2Start_TextChanged;

            TextBoxNonTradePeriod3Start.Text = tradeGrid.NonTradePeriod3Start.ToString();
            TextBoxNonTradePeriod3Start.TextChanged += TextBoxNonTradePeriod3Start_TextChanged; 

            TextBoxNonTradePeriod4Start.Text = tradeGrid.NonTradePeriod4Start.ToString();
            TextBoxNonTradePeriod4Start.TextChanged += TextBoxNonTradePeriod4Start_TextChanged; 

            TextBoxNonTradePeriod5Start.Text = tradeGrid.NonTradePeriod5Start.ToString();
            TextBoxNonTradePeriod5Start.TextChanged += TextBoxNonTradePeriod5Start_TextChanged;

            TextBoxNonTradePeriod1End.Text = tradeGrid.NonTradePeriod1End.ToString();
            TextBoxNonTradePeriod1End.TextChanged += TextBoxNonTradePeriod1End_TextChanged;

            TextBoxNonTradePeriod2End.Text = tradeGrid.NonTradePeriod2End.ToString();
            TextBoxNonTradePeriod2End.TextChanged += TextBoxNonTradePeriod2End_TextChanged;

            TextBoxNonTradePeriod3End.Text = tradeGrid.NonTradePeriod3End.ToString();
            TextBoxNonTradePeriod3End.TextChanged += TextBoxNonTradePeriod3End_TextChanged;

            TextBoxNonTradePeriod4End.Text = tradeGrid.NonTradePeriod4End.ToString();
            TextBoxNonTradePeriod4End.TextChanged += TextBoxNonTradePeriod4End_TextChanged;

            TextBoxNonTradePeriod5End.Text = tradeGrid.NonTradePeriod5End.ToString();
            TextBoxNonTradePeriod5End.TextChanged += TextBoxNonTradePeriod5End_TextChanged;

            // trade days 

            CheckBoxTradeInMonday.IsChecked = tradeGrid.TradeInMonday;
            CheckBoxTradeInMonday.Checked += CheckBoxTradeInMonday_Checked;

            CheckBoxTradeInTuesday.IsChecked = tradeGrid.TradeInTuesday;
            CheckBoxTradeInTuesday.Checked += CheckBoxTradeInTuesday_Checked;

            CheckBoxTradeInWednesday.IsChecked = tradeGrid.TradeInWednesday;
            CheckBoxTradeInWednesday.Checked += CheckBoxTradeInWednesday_Checked;

            CheckBoxTradeInThursday.IsChecked = tradeGrid.TradeInThursday;
            CheckBoxTradeInThursday.Checked += CheckBoxTradeInThursday_Checked; 
             
            CheckBoxTradeInFriday.IsChecked = tradeGrid.TradeInFriday;
            CheckBoxTradeInFriday.Checked += CheckBoxTradeInFriday_Checked;

            CheckBoxTradeInSaturday.IsChecked = tradeGrid.TradeInSaturday;
            CheckBoxTradeInSaturday.Checked += CheckBoxTradeInSaturday_Checked; 

            CheckBoxTradeInSunday.IsChecked = tradeGrid.TradeInSunday;
            CheckBoxTradeInSunday.Checked += CheckBoxTradeInSunday_Checked;

            // stop grid by event

            CheckBoxStopGridByMoveUpIsOn.IsChecked = tradeGrid.StopGridByMoveUpIsOn;
            CheckBoxStopGridByMoveUpIsOn.Checked += CheckBoxStopGridByMoveUpIsOn_Checked;
            TextBoxStopGridByMoveUpValuePercent.Text = tradeGrid.StopGridByMoveUpValuePercent.ToString();
            TextBoxStopGridByMoveUpValuePercent.TextChanged += TextBoxStopGridByMoveUpValuePercent_TextChanged;

            CheckBoxStopGridByMoveDownIsOn.IsChecked = tradeGrid.StopGridByMoveDownIsOn;
            CheckBoxStopGridByMoveDownIsOn.Checked += CheckBoxStopGridByMoveDownIsOn_Checked;
            TextBoxStopGridByMoveDownValuePercent.Text = tradeGrid.StopGridByMoveDownValuePercent.ToString();
            TextBoxStopGridByMoveDownValuePercent.TextChanged += TextBoxStopGridByMoveDownValuePercent_TextChanged;

            CheckBoxStopGridByPositionsCountIsOn.IsChecked = tradeGrid.StopGridByPositionsCountIsOn;
            CheckBoxStopGridByPositionsCountIsOn.Checked += CheckBoxStopGridByPositionsCountIsOn_Checked;
            TextBoxStopGridByPositionsCountValue.Text = tradeGrid.StopGridByPositionsCountValue.ToString();
            TextBoxStopGridByPositionsCountValue.TextChanged += TextBoxStopGridByPositionsCountValue_TextChanged;

            // grid lines creation

            ComboBoxGridSide.Items.Add(Side.Buy.ToString());
            ComboBoxGridSide.Items.Add(Side.Sell.ToString());
            ComboBoxGridSide.SelectedItem = tradeGrid.GridSide.ToString();
            ComboBoxGridSide.SelectionChanged += ComboBoxGridSide_SelectionChanged;

            TextBoxFirstPrice.Text = tradeGrid.FirstPrice.ToString();
            TextBoxFirstPrice.TextChanged += TextBoxFirstPrice_TextChanged;

            TextBoxLineCountStart.Text = tradeGrid.LineCountStart.ToString();
            TextBoxLineCountStart.TextChanged += TextBoxLineCountStart_TextChanged;

            TextBoxMaxOrdersInMarket.Text = tradeGrid.MaxOrdersInMarket.ToString();
            TextBoxMaxOrdersInMarket.TextChanged += TextBoxMaxOrdersInMarket_TextChanged;

            ComboBoxTypeStep.Items.Add(TradeGridValueType.Percent.ToString());
            ComboBoxTypeStep.Items.Add(TradeGridValueType.Absolute.ToString());
            ComboBoxTypeStep.SelectedItem = tradeGrid.TypeStep.ToString();
            ComboBoxTypeStep.SelectionChanged += ComboBoxTypeStep_SelectionChanged;

            TextBoxLineStep.Text = tradeGrid.LineStep.ToString();
            TextBoxLineStep.TextChanged += TextBoxLineStep_TextChanged;

            TextBoxStepMultiplicator.Text = tradeGrid.StepMultiplicator.ToString();
            TextBoxStepMultiplicator.TextChanged += TextBoxStepMultiplicator_TextChanged;

            ComboBoxTypeProfit.Items.Add(TradeGridValueType.Percent.ToString());
            ComboBoxTypeProfit.Items.Add(TradeGridValueType.Absolute.ToString());
            ComboBoxTypeProfit.SelectedItem = tradeGrid.TypeProfit.ToString();
            ComboBoxTypeProfit.SelectionChanged += ComboBoxTypeProfit_SelectionChanged;

            TextBoxProfitStep.Text = tradeGrid.ProfitStep.ToString();
            TextBoxProfitStep.TextChanged += TextBoxProfitStep_TextChanged;

            TextBoxProfitMultiplicator.Text = tradeGrid.ProfitMultiplicator.ToString();
            TextBoxProfitMultiplicator.TextChanged += TextBoxProfitMultiplicator_TextChanged;

            TextBoxMartingaleMultiplicator.Text = tradeGrid.ProfitMultiplicator.ToString();
            TextBoxMartingaleMultiplicator.TextChanged += TextBoxMartingaleMultiplicator_TextChanged;

            TextBoxTradeAssetInPortfolio.Text = tradeGrid.TradeAssetInPortfolio;
            TextBoxTradeAssetInPortfolio.TextChanged += TextBoxTradeAssetInPortfolio_TextChanged;

            // stop and profit 

            ComboBoxProfitRegime.Items.Add(TradeGridStopRegime.Off.ToString());
            ComboBoxProfitRegime.Items.Add(TradeGridStopRegime.On.ToString());
            ComboBoxProfitRegime.SelectedItem = tradeGrid.ProfitRegime.ToString();
            ComboBoxProfitRegime.SelectionChanged += ComboBoxProfitRegime_SelectionChanged;

            ComboBoxProfitValueType.Items.Add(TradeGridValueType.Percent.ToString());
            ComboBoxProfitValueType.Items.Add(TradeGridValueType.Absolute.ToString());
            ComboBoxProfitValueType.SelectedItem = tradeGrid.ProfitValueType.ToString();
            ComboBoxProfitValueType.SelectionChanged += ComboBoxProfitValueType_SelectionChanged;

            TextBoxProfitValue.Text = tradeGrid.ProfitValue.ToString();
            TextBoxProfitValue.TextChanged += TextBoxProfitValue_TextChanged;

            ComboBoxStopRegime.Items.Add(TradeGridStopRegime.Off.ToString());
            ComboBoxStopRegime.Items.Add(TradeGridStopRegime.On.ToString());
            ComboBoxStopRegime.SelectedItem = tradeGrid.StopRegime.ToString();
            ComboBoxStopRegime.SelectionChanged += ComboBoxStopRegime_SelectionChanged;

            ComboBoxStopValueType.Items.Add(TradeGridValueType.Percent.ToString());
            ComboBoxStopValueType.Items.Add(TradeGridValueType.Absolute.ToString());
            ComboBoxStopValueType.SelectedItem = tradeGrid.StopValueType.ToString();
            ComboBoxStopValueType.SelectionChanged += ComboBoxStopValueType_SelectionChanged;

            TextBoxStopValue.Text = tradeGrid.StopValue.ToString();
            TextBoxStopValue.TextChanged += TextBoxStopValue_TextChanged;

        }

        #region Stop and profit 

        private void ComboBoxProfitRegime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxProfitRegime.SelectedItem.ToString(), out TradeGrid.ProfitRegime);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxProfitValueType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxProfitValueType.SelectedItem.ToString(), out TradeGrid.ProfitValueType);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxProfitValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxProfitValue.Text))
                {
                    return;
                }

                TradeGrid.ProfitValue = TextBoxProfitValue.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxStopRegime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxStopRegime.SelectedItem.ToString(), out TradeGrid.StopRegime);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxStopValueType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxStopValueType.SelectedItem.ToString(), out TradeGrid.StopValueType);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxStopValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxStopValue.Text))
                {
                    return;
                }

                TradeGrid.StopValue = TextBoxStopValue.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region Grid lines creation

        private void ComboBoxGridSide_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxGridSide.SelectedItem.ToString(), out TradeGrid.GridSide);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxFirstPrice_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxFirstPrice.Text))
                {
                    return;
                }

                TradeGrid.FirstPrice = TextBoxFirstPrice.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxLineCountStart_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxLineCountStart.Text))
                {
                    return;
                }

                TradeGrid.LineCountStart = Convert.ToInt32(TextBoxLineCountStart.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxMaxOrdersInMarket_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxMaxOrdersInMarket.Text))
                {
                    return;
                }

                TradeGrid.MaxOrdersInMarket = Convert.ToInt32(TextBoxMaxOrdersInMarket.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxTypeStep_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxTypeStep.SelectedItem.ToString(), out TradeGrid.TypeStep);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxLineStep_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxLineStep.Text))
                {
                    return;
                }

                TradeGrid.LineStep = Convert.ToInt32(TextBoxLineStep.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxStepMultiplicator_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxStepMultiplicator.Text))
                {
                    return;
                }

                TradeGrid.StepMultiplicator = TextBoxStepMultiplicator.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxTypeProfit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxTypeProfit.SelectedItem.ToString(), out TradeGrid.TypeProfit);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxProfitStep_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxProfitStep.Text))
                {
                    return;
                }

                TradeGrid.ProfitStep = TextBoxProfitStep.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxProfitMultiplicator_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxProfitMultiplicator.Text))
                {
                    return;
                }

                TradeGrid.ProfitMultiplicator = TextBoxProfitMultiplicator.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxMartingaleMultiplicator_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxMartingaleMultiplicator.Text))
                {
                    return;
                }

                TradeGrid.MartingaleMultiplicator = TextBoxMartingaleMultiplicator.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxTradeAssetInPortfolio_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxTradeAssetInPortfolio.Text))
                {
                    return;
                }

                TradeGrid.TradeAssetInPortfolio = TextBoxTradeAssetInPortfolio.Text;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ButtonCreateGrid_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonDeleteGrid_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonNewLevel_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonRemoveSelected_Click(object sender, RoutedEventArgs e)
        {

        }

        #endregion

        #region Stop grid by event

        private void CheckBoxStopGridByMoveUpIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.StopGridByMoveUpIsOn = CheckBoxStopGridByMoveUpIsOn.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxStopGridByMoveUpValuePercent_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxStopGridByMoveUpValuePercent.Text))
                {
                    return;
                }

                TradeGrid.StopGridByMoveUpValuePercent = TextBoxStopGridByMoveUpValuePercent.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxStopGridByMoveDownIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.StopGridByMoveDownIsOn = CheckBoxStopGridByMoveDownIsOn.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxStopGridByMoveDownValuePercent_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxStopGridByMoveDownValuePercent.Text))
                {
                    return;
                }

                TradeGrid.StopGridByMoveDownValuePercent = TextBoxStopGridByMoveDownValuePercent.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxStopGridByPositionsCountIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.StopGridByPositionsCountIsOn = CheckBoxStopGridByPositionsCountIsOn.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxStopGridByPositionsCountValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxStopGridByPositionsCountValue.Text))
                {
                    return;
                }

                TradeGrid.StopGridByPositionsCountValue = Convert.ToInt32(TextBoxStopGridByPositionsCountValue.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region Trade days 

        private void CheckBoxTradeInMonday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.TradeInMonday = CheckBoxTradeInMonday.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTradeInTuesday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.TradeInTuesday = CheckBoxTradeInTuesday.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTradeInWednesday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.TradeInWednesday = CheckBoxTradeInWednesday.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTradeInThursday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.TradeInThursday = CheckBoxTradeInThursday.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTradeInFriday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.TradeInFriday = CheckBoxTradeInFriday.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTradeInSaturday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.TradeInSaturday = CheckBoxTradeInSaturday.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTradeInSunday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.TradeInSunday = CheckBoxTradeInSunday.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region Non trade periods

        private void CheckBoxNonTradePeriod1OnOff_Checked(object sender, RoutedEventArgs e)
        {
            TradeGrid.NonTradePeriod1OnOff = CheckBoxNonTradePeriod1OnOff.IsChecked.Value;
            TradeGrid.Save();
        }

        private void TextBoxNonTradePeriod1Start_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod1Start.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriod1Start.LoadFromString(TextBoxNonTradePeriod1Start.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod1End_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod1End.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriod1End.LoadFromString(TextBoxNonTradePeriod1End.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxNonTradePeriod2OnOff_Checked(object sender, RoutedEventArgs e)
        {
            TradeGrid.NonTradePeriod2OnOff = CheckBoxNonTradePeriod2OnOff.IsChecked.Value;
            TradeGrid.Save();
        }

        private void TextBoxNonTradePeriod2Start_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod2Start.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriod2Start.LoadFromString(TextBoxNonTradePeriod2Start.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod2End_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod2End.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriod2End.LoadFromString(TextBoxNonTradePeriod2End.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxNonTradePeriod3OnOff_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.NonTradePeriod3OnOff = CheckBoxNonTradePeriod3OnOff.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod3Start_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod3Start.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriod3Start.LoadFromString(TextBoxNonTradePeriod3Start.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod3End_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod3End.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriod3End.LoadFromString(TextBoxNonTradePeriod3End.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxNonTradePeriod4OnOff_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.NonTradePeriod4OnOff = CheckBoxNonTradePeriod4OnOff.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod4Start_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod4Start.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriod4Start.LoadFromString(TextBoxNonTradePeriod4Start.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod4End_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod4End.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriod4End.LoadFromString(TextBoxNonTradePeriod4End.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxNonTradePeriod5OnOff_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.NonTradePeriod5OnOff = CheckBoxNonTradePeriod5OnOff.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod5Start_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod5Start.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriod5Start.LoadFromString(TextBoxNonTradePeriod5Start.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod5End_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod5End.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriod5End.LoadFromString(TextBoxNonTradePeriod5End.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region Regime Tab

        private void TextBoxMaxClosePositionsInJournal_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxMaxClosePositionsInJournal.Text))
                {
                    return;
                }

                TradeGrid.MaxClosePositionsInJournal = Convert.ToInt32(TextBoxMaxClosePositionsInJournal.Text);
                TradeGrid.Save();
            }
            catch
            {
               // ignore
            }
        }

        private void ComboBoxAutoClearJournal_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxAutoClearJournal.SelectedItem.ToString(), out TradeGrid.AutoClearJournalIsOn);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxRegimeLogging_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxRegimeLogging.SelectedItem.ToString(), out TradeGrid.RegimeLogging);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxRegimeLogicEntry_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxRegimeLogicEntry.SelectedItem.ToString(), out TradeGrid.RegimeLogicEntry);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxRegime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxRegime.SelectedItem.ToString(), out TradeGrid.Regime);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxClosePositionNumber_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if(string.IsNullOrEmpty(TextBoxClosePositionNumber.Text)) 
                { 
                    return; 
                }

                TradeGrid.ClosePositionNumber = Convert.ToInt32(TextBoxClosePositionNumber.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxGridType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Enum.TryParse(ComboBoxGridType.SelectedItem.ToString(),out TradeGrid.GridType);
            TradeGrid.Save();
        }

        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonStop_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonLoad_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {

        }

        #endregion

        private void TradeGridUi_Closed(object sender, EventArgs e)
        {
            TradeGrid = null;

            ComboBoxGridType.SelectionChanged -= ComboBoxGridType_SelectionChanged;
            TextBoxClosePositionNumber.TextChanged -= TextBoxClosePositionNumber_TextChanged;
            ComboBoxRegime.SelectionChanged -= ComboBoxRegime_SelectionChanged;
            ComboBoxRegimeLogicEntry.SelectionChanged -= ComboBoxRegimeLogicEntry_SelectionChanged;
            ComboBoxRegimeLogging.SelectionChanged -= ComboBoxRegimeLogging_SelectionChanged;
            ComboBoxAutoClearJournal.SelectionChanged -= ComboBoxAutoClearJournal_SelectionChanged;
            TextBoxMaxClosePositionsInJournal.TextChanged -= TextBoxMaxClosePositionsInJournal_TextChanged;

            CheckBoxNonTradePeriod1OnOff.Checked -= CheckBoxNonTradePeriod1OnOff_Checked;
            CheckBoxNonTradePeriod2OnOff.Checked -= CheckBoxNonTradePeriod2OnOff_Checked;
            CheckBoxNonTradePeriod3OnOff.Checked -= CheckBoxNonTradePeriod3OnOff_Checked;
            CheckBoxNonTradePeriod4OnOff.Checked -= CheckBoxNonTradePeriod4OnOff_Checked;
            CheckBoxNonTradePeriod5OnOff.Checked -= CheckBoxNonTradePeriod5OnOff_Checked;
            TextBoxNonTradePeriod1Start.TextChanged -= TextBoxNonTradePeriod1Start_TextChanged;
            TextBoxNonTradePeriod2Start.TextChanged -= TextBoxNonTradePeriod2Start_TextChanged;
            TextBoxNonTradePeriod3Start.TextChanged -= TextBoxNonTradePeriod3Start_TextChanged;
            TextBoxNonTradePeriod4Start.TextChanged -= TextBoxNonTradePeriod4Start_TextChanged;
            TextBoxNonTradePeriod5Start.TextChanged -= TextBoxNonTradePeriod5Start_TextChanged;
            TextBoxNonTradePeriod1End.TextChanged -= TextBoxNonTradePeriod1End_TextChanged;
            TextBoxNonTradePeriod2End.TextChanged -= TextBoxNonTradePeriod2End_TextChanged;
            TextBoxNonTradePeriod3End.TextChanged -= TextBoxNonTradePeriod3End_TextChanged;
            TextBoxNonTradePeriod4End.TextChanged -= TextBoxNonTradePeriod4End_TextChanged;
            TextBoxNonTradePeriod5End.TextChanged -= TextBoxNonTradePeriod5End_TextChanged;

            CheckBoxTradeInMonday.Checked -= CheckBoxTradeInMonday_Checked;
            CheckBoxTradeInTuesday.Checked -= CheckBoxTradeInTuesday_Checked;
            CheckBoxTradeInWednesday.Checked -= CheckBoxTradeInWednesday_Checked;
            CheckBoxTradeInThursday.Checked -= CheckBoxTradeInThursday_Checked;
            CheckBoxTradeInFriday.Checked -= CheckBoxTradeInFriday_Checked;
            CheckBoxTradeInSaturday.Checked -= CheckBoxTradeInSaturday_Checked;
            CheckBoxTradeInSunday.Checked -= CheckBoxTradeInSunday_Checked;

            CheckBoxStopGridByMoveUpIsOn.Checked -= CheckBoxStopGridByMoveUpIsOn_Checked;
            TextBoxStopGridByMoveUpValuePercent.TextChanged -= TextBoxStopGridByMoveUpValuePercent_TextChanged;
            CheckBoxStopGridByMoveDownIsOn.Checked -= CheckBoxStopGridByMoveDownIsOn_Checked;
            TextBoxStopGridByMoveDownValuePercent.TextChanged -= TextBoxStopGridByMoveDownValuePercent_TextChanged;
            CheckBoxStopGridByPositionsCountIsOn.Checked -= CheckBoxStopGridByPositionsCountIsOn_Checked;
            TextBoxStopGridByPositionsCountValue.TextChanged -= TextBoxStopGridByPositionsCountValue_TextChanged;

        }

        public TradeGrid TradeGrid;

        public int Number;

    }
}
