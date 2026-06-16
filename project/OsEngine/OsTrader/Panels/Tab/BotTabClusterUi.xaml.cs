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

            Closed += BotTabClusterUi_Closed;
        }

        private void BotTabClusterUi_Closed(object sender, EventArgs e)
        {
            try
            {
                if (_blinkTimer != null)
                {
                    _blinkTimer.Stop();
                    _blinkTimer.Tick -= _blinkTimer_Tick;
                    _blinkTimer = null;
                }

                TextBoxStep.TextChanged -= TextBoxStep_TextChanged;
                ButtonPostsCluster.Click -= ButtonPostsCluster_Click;

                if (_instructionsUi != null)
                {
                    _instructionsUi.Closed -= _instructionsUi_Closed;
                    _instructionsUi = null;
                }

                _tab = null;
                _lineStep = 0;

                Closed -= BotTabClusterUi_Closed;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private DispatcherTimer _blinkTimer;

        private int _blinkCount;

        private bool _isGreenVisible = true;

        private void StartButtonBlinkAnimation()
        {
            try
            {
                _blinkTimer = new DispatcherTimer();
                _blinkTimer.Interval = TimeSpan.FromMilliseconds(300);
                _blinkTimer.Tick += _blinkTimer_Tick;
                _blinkTimer.Start();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _blinkTimer_Tick(object sender, EventArgs e)
        {
            if (_blinkTimer == null)
            {
                return;
            }

            try
            {
                if (_blinkCount >= 20)
                {
                    _blinkTimer.Stop();
                    _blinkTimer.Tick -= _blinkTimer_Tick;
                    _blinkTimer = null;
                    GreenCollectionCluster.Opacity = 1;
                    WhiteCollectionCluster.Opacity = 0;
                    return;
                }

                if (_isGreenVisible)
                {
                    GreenCollectionCluster.Opacity = 0;
                    WhiteCollectionCluster.Opacity = 1;
                }
                else
                {
                    GreenCollectionCluster.Opacity = 1;
                    WhiteCollectionCluster.Opacity = 0;
                }

                _isGreenVisible = !_isGreenVisible;
                _blinkCount++;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                if (_blinkTimer != null)
                {
                    _blinkTimer.Stop();
                }
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
