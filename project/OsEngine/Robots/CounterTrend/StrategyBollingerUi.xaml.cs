﻿/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;

namespace OsEngine.Robots.CounterTrend
{
    public partial class StrategyBollingerUi
    {
        private StrategyBollinger _strategy;

        public StrategyBollingerUi(StrategyBollinger strategy)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _strategy = strategy;

            TextBoxVolumeOne.Text = _strategy.Volume.ToString();
            TextBoxSlippage.Text = _strategy.Slippage.ToString(new CultureInfo("ru-RU"));

            ComboBoxRegime.Items.Add(BotTradeRegime.Off);
            ComboBoxRegime.Items.Add(BotTradeRegime.On);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyClosePosition);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyLong);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyShort);
            ComboBoxRegime.SelectedItem = _strategy.Regime;

            LabelVolume.Content = OsLocalization.Trader.Label30;
            LabelRegime.Content = OsLocalization.Trader.Label115;
            LabelSlippage.Content = OsLocalization.Trader.Label92;
            ButtonAccept.Content = OsLocalization.Trader.Label17;

            this.Activate();
            this.Focus();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                if (Convert.ToDecimal(TextBoxVolumeOne.Text) <= 0 ||
                    Convert.ToDecimal(TextBoxSlippage.Text) < 0)
                {
                    throw new Exception("");
                }
            }
            catch (Exception)
            {
                MessageBox.Show(OsLocalization.Trader.Label13);
                return;
            }

            _strategy.Volume = Convert.ToDecimal(TextBoxVolumeOne.Text);
            _strategy.Slippage = Convert.ToDecimal(TextBoxSlippage.Text);

            Enum.TryParse(ComboBoxRegime.Text, true, out _strategy.Regime);

            _strategy.Save();
            Close();
        }

        private void ButtonAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(OsLocalization.Trader.Label129);
        }
    }
}