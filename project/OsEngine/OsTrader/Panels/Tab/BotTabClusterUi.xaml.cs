/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OsEngine.Entity;
using OsEngine.Instructions;
using OsEngine.Language;
using OsEngine.Market;

namespace OsEngine.OsTrader.Panels.Tab
{
    public partial class BotTabClusterUi
    {
        private BotTabCluster _tab;

        public BotTabClusterUi(BotTabCluster tab)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _tab = tab;

            TextBoxStep.Text = _tab.LineStep.ToString();
            TextBoxStep.TextChanged += TextBoxStep_TextChanged;
            _lineStep = _tab.LineStep;

            ComboBoxChartType.Items.Add(ClusterType.SummVolume.ToString());
            ComboBoxChartType.Items.Add(ClusterType.BuyVolume.ToString());
            ComboBoxChartType.Items.Add(ClusterType.SellVolume.ToString());
            ComboBoxChartType.Items.Add(ClusterType.DeltaVolume.ToString());
            ComboBoxChartType.SelectedItem = tab.ChartType.ToString();

            Title = OsLocalization.Trader.Label77;
            ButtonConnectorDialog.Content = OsLocalization.Trader.Label78;
            LabelShowType.Content = OsLocalization.Trader.Label79;
            LabelLinesStep.Content = OsLocalization.Trader.Label80;
            ButtonAccept.Content = OsLocalization.Trader.Label17;

            this.Activate();
            this.Focus();

            if (InteractiveInstructions.ClusterPosts.AllInstructionsInClass == null
             || InteractiveInstructions.ClusterPosts.AllInstructionsInClass.Count == 0)
            {
                ButtonPostsCluster.Visibility = Visibility.Hidden;
            }
            else
            {
                ButtonPostsCluster.Click += ButtonPostsCluster_Click;
            }

            StartButtonBlinkAnimation();
        }

        private void StartButtonBlinkAnimation()
        {
            try
            {
                DispatcherTimer timer = new DispatcherTimer();
                int blinkCount = 0;
                bool isGreenVisible = true;

                timer.Interval = TimeSpan.FromMilliseconds(300);
                timer.Tick += (s, e) =>
                {
                    try
                    {
                        if (blinkCount >= 20)
                        {
                            timer.Stop();
                            GreenCollectionCluster.Opacity = 1;
                            WhiteCollectionCluster.Opacity = 0;
                            return;
                        }

                        if (isGreenVisible)
                        {
                            GreenCollectionCluster.Opacity = 0;
                            WhiteCollectionCluster.Opacity = 1;
                        }
                        else
                        {
                            GreenCollectionCluster.Opacity = 1;
                            WhiteCollectionCluster.Opacity = 0;
                        }

                        isGreenVisible = !isGreenVisible;
                        blinkCount++;
                    }
                    catch (Exception ex)
                    {
                        ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                        timer.Stop();
                    }
                };

                timer.Start();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxStep_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TextBoxStep.Text.EndsWith(",") ||
                TextBoxStep.Text.EndsWith("."))
            {
                return;
            }

            try
            {
                _lineStep = TextBoxStep.Text.ToDecimal();
            }
            catch (Exception)
            {
                TextBoxStep.Text = _tab.LineStep.ToString();
            }
        }

        private decimal _lineStep;

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            _tab.LineStep = _lineStep;

            ClusterType chartType;
            Enum.TryParse(ComboBoxChartType.Text, out chartType);
            _tab.ChartType = chartType;

            Close();
        }

        private void ButtonConnectorDialog_Click(object sender, RoutedEventArgs e)
        {
            _tab.ShowCandlesDialog();
        }

        #region Posts collection

        private InstructionsUi _instructionsUi;

        private void ButtonPostsCluster_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_instructionsUi == null)
                {
                    _instructionsUi = new InstructionsUi(
                        InteractiveInstructions.ClusterPosts.AllInstructionsInClass, InteractiveInstructions.ClusterPosts.AllInstructionsInClassDescription);
                    _instructionsUi.Show();
                    _instructionsUi.Closed += _instructionsUi_Closed;
                }
                else
                {
                    if (_instructionsUi.WindowState == WindowState.Minimized)
                    {
                        _instructionsUi.WindowState = WindowState.Normal;
                    }
                    _instructionsUi.Activate();
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _instructionsUi_Closed(object sender, EventArgs e)
        {
            try
            {
                _instructionsUi.Closed -= _instructionsUi_Closed;
                _instructionsUi = null;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion       
    }
}
