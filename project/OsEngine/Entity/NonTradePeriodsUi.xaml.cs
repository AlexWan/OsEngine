/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System;
using System.Windows;
using System.Windows.Controls;

namespace OsEngine.Entity
{
    public partial class NonTradePeriodsUi : Window
    {
        private NonTradePeriods _periods;

        public NonTradePeriodsUi(NonTradePeriods periods)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCorner(this);

            _periods = periods;

            CheckBoxTradeInMonday.IsChecked = _periods.TradeInMonday;
            CheckBoxTradeInMonday.Checked += CheckBoxTradeInMonday_Checked;
            CheckBoxTradeInMonday.Unchecked += CheckBoxTradeInMonday_Checked;

            CheckBoxTradeInTuesday.IsChecked = _periods.TradeInTuesday;
            CheckBoxTradeInTuesday.Checked += CheckBoxTradeInTuesday_Checked;
            CheckBoxTradeInTuesday.Unchecked += CheckBoxTradeInTuesday_Checked;

            CheckBoxTradeInWednesday.IsChecked = _periods.TradeInWednesday;
            CheckBoxTradeInWednesday.Checked += CheckBoxTradeInWednesday_Checked;
            CheckBoxTradeInWednesday.Unchecked += CheckBoxTradeInWednesday_Checked;

            CheckBoxTradeInThursday.IsChecked = _periods.TradeInThursday;
            CheckBoxTradeInThursday.Checked += CheckBoxTradeInThursday_Checked;
            CheckBoxTradeInThursday.Unchecked += CheckBoxTradeInThursday_Checked;

            CheckBoxTradeInFriday.IsChecked = _periods.TradeInFriday;
            CheckBoxTradeInFriday.Checked += CheckBoxTradeInFriday_Checked;
            CheckBoxTradeInFriday.Unchecked += CheckBoxTradeInFriday_Checked;

            CheckBoxTradeInSaturday.IsChecked = _periods.TradeInSaturday;
            CheckBoxTradeInSaturday.Checked += CheckBoxTradeInSaturday_Checked;
            CheckBoxTradeInSaturday.Unchecked += CheckBoxTradeInSaturday_Checked;

            CheckBoxTradeInSunday.IsChecked = _periods.TradeInSunday;
            CheckBoxTradeInSunday.Checked += CheckBoxTradeInSunday_Checked;
            CheckBoxTradeInSunday.Unchecked += CheckBoxTradeInSunday_Checked;

            // non trade periods

            CheckBoxNonTradePeriod1OnOff.IsChecked = _periods.NonTradePeriod1OnOff;
            CheckBoxNonTradePeriod1OnOff.Checked += CheckBoxNonTradePeriod1OnOff_Checked;
            CheckBoxNonTradePeriod1OnOff.Unchecked += CheckBoxNonTradePeriod1OnOff_Checked;

            CheckBoxNonTradePeriod2OnOff.IsChecked = _periods.NonTradePeriod2OnOff;
            CheckBoxNonTradePeriod2OnOff.Checked += CheckBoxNonTradePeriod2OnOff_Checked;
            CheckBoxNonTradePeriod2OnOff.Unchecked += CheckBoxNonTradePeriod2OnOff_Checked;

            CheckBoxNonTradePeriod3OnOff.IsChecked = _periods.NonTradePeriod3OnOff;
            CheckBoxNonTradePeriod3OnOff.Checked += CheckBoxNonTradePeriod3OnOff_Checked;
            CheckBoxNonTradePeriod3OnOff.Unchecked += CheckBoxNonTradePeriod3OnOff_Checked;

            CheckBoxNonTradePeriod4OnOff.IsChecked = _periods.NonTradePeriod4OnOff;
            CheckBoxNonTradePeriod4OnOff.Checked += CheckBoxNonTradePeriod4OnOff_Checked;
            CheckBoxNonTradePeriod4OnOff.Unchecked += CheckBoxNonTradePeriod4OnOff_Checked;

            CheckBoxNonTradePeriod5OnOff.IsChecked = _periods.NonTradePeriod5OnOff;
            CheckBoxNonTradePeriod5OnOff.Checked += CheckBoxNonTradePeriod5OnOff_Checked;
            CheckBoxNonTradePeriod5OnOff.Unchecked += CheckBoxNonTradePeriod5OnOff_Checked;

            TextBoxNonTradePeriod1Start.Text = _periods.NonTradePeriod1Start.ToString();
            TextBoxNonTradePeriod1Start.TextChanged += TextBoxNonTradePeriod1Start_TextChanged;

            TextBoxNonTradePeriod2Start.Text = _periods.NonTradePeriod2Start.ToString();
            TextBoxNonTradePeriod2Start.TextChanged += TextBoxNonTradePeriod2Start_TextChanged;

            TextBoxNonTradePeriod3Start.Text = _periods.NonTradePeriod3Start.ToString();
            TextBoxNonTradePeriod3Start.TextChanged += TextBoxNonTradePeriod3Start_TextChanged;

            TextBoxNonTradePeriod4Start.Text = _periods.NonTradePeriod4Start.ToString();
            TextBoxNonTradePeriod4Start.TextChanged += TextBoxNonTradePeriod4Start_TextChanged;

            TextBoxNonTradePeriod5Start.Text = _periods.NonTradePeriod5Start.ToString();
            TextBoxNonTradePeriod5Start.TextChanged += TextBoxNonTradePeriod5Start_TextChanged;

            TextBoxNonTradePeriod1End.Text = _periods.NonTradePeriod1End.ToString();
            TextBoxNonTradePeriod1End.TextChanged += TextBoxNonTradePeriod1End_TextChanged;

            TextBoxNonTradePeriod2End.Text = _periods.NonTradePeriod2End.ToString();
            TextBoxNonTradePeriod2End.TextChanged += TextBoxNonTradePeriod2End_TextChanged;

            TextBoxNonTradePeriod3End.Text = _periods.NonTradePeriod3End.ToString();
            TextBoxNonTradePeriod3End.TextChanged += TextBoxNonTradePeriod3End_TextChanged;

            TextBoxNonTradePeriod4End.Text = _periods.NonTradePeriod4End.ToString();
            TextBoxNonTradePeriod4End.TextChanged += TextBoxNonTradePeriod4End_TextChanged;

            TextBoxNonTradePeriod5End.Text = _periods.NonTradePeriod5End.ToString();
            TextBoxNonTradePeriod5End.TextChanged += TextBoxNonTradePeriod5End_TextChanged;

            // localization

            Title = OsLocalization.Trader.Label575;
            TabItemTradeDays.Header = OsLocalization.Trader.Label461;
            TabItemNonTradePeriods.Header = OsLocalization.Trader.Label462;

            // non trade periods
            CheckBoxNonTradePeriod1OnOff.Content = OsLocalization.Trader.Label473 + " 1";
            CheckBoxNonTradePeriod2OnOff.Content = OsLocalization.Trader.Label473 + " 2";
            CheckBoxNonTradePeriod3OnOff.Content = OsLocalization.Trader.Label473 + " 3";
            CheckBoxNonTradePeriod4OnOff.Content = OsLocalization.Trader.Label473 + " 4";
            CheckBoxNonTradePeriod5OnOff.Content = OsLocalization.Trader.Label473 + " 5";

            // trade days 
            CheckBoxTradeInMonday.Content = OsLocalization.Trader.Label474;
            CheckBoxTradeInTuesday.Content = OsLocalization.Trader.Label475;
            CheckBoxTradeInWednesday.Content = OsLocalization.Trader.Label476;
            CheckBoxTradeInThursday.Content = OsLocalization.Trader.Label477;
            CheckBoxTradeInFriday.Content = OsLocalization.Trader.Label478;
            CheckBoxTradeInSaturday.Content = OsLocalization.Trader.Label479;
            CheckBoxTradeInSunday.Content = OsLocalization.Trader.Label480;

            this.Closed += NonTradePeriodsUi_Closed;
        }

        private void NonTradePeriodsUi_Closed(object sender, EventArgs e)
        {
            _periods = null;

        }

        #region Trade days 

        private void CheckBoxTradeInMonday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                _periods.TradeInMonday = CheckBoxTradeInMonday.IsChecked.Value;
                _periods.Save();
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
                _periods.TradeInTuesday = CheckBoxTradeInTuesday.IsChecked.Value;
                _periods.Save();
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
                _periods.TradeInWednesday = CheckBoxTradeInWednesday.IsChecked.Value;
                _periods.Save();
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
                _periods.TradeInThursday = CheckBoxTradeInThursday.IsChecked.Value;
                _periods.Save();
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
                _periods.TradeInFriday = CheckBoxTradeInFriday.IsChecked.Value;
                _periods.Save();
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
                _periods.TradeInSaturday = CheckBoxTradeInSaturday.IsChecked.Value;
                _periods.Save();
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
                _periods.TradeInSunday = CheckBoxTradeInSunday.IsChecked.Value;
                _periods.Save();
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
            _periods.NonTradePeriod1OnOff = CheckBoxNonTradePeriod1OnOff.IsChecked.Value;
            _periods.Save();
        }

        private void TextBoxNonTradePeriod1Start_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod1Start.Text))
                {
                    return;
                }

                _periods.NonTradePeriod1Start.LoadFromString(TextBoxNonTradePeriod1Start.Text);
                _periods.Save();
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

                _periods.NonTradePeriod1End.LoadFromString(TextBoxNonTradePeriod1End.Text);
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxNonTradePeriod2OnOff_Checked(object sender, RoutedEventArgs e)
        {
            _periods.NonTradePeriod2OnOff = CheckBoxNonTradePeriod2OnOff.IsChecked.Value;
            _periods.Save();
        }

        private void TextBoxNonTradePeriod2Start_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod2Start.Text))
                {
                    return;
                }

                _periods.NonTradePeriod2Start.LoadFromString(TextBoxNonTradePeriod2Start.Text);
                _periods.Save();
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

                _periods.NonTradePeriod2End.LoadFromString(TextBoxNonTradePeriod2End.Text);
                _periods.Save();
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
                _periods.NonTradePeriod3OnOff = CheckBoxNonTradePeriod3OnOff.IsChecked.Value;
                _periods.Save();
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

                _periods.NonTradePeriod3Start.LoadFromString(TextBoxNonTradePeriod3Start.Text);
                _periods.Save();
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

                _periods.NonTradePeriod3End.LoadFromString(TextBoxNonTradePeriod3End.Text);
                _periods.Save();
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
                _periods.NonTradePeriod4OnOff = CheckBoxNonTradePeriod4OnOff.IsChecked.Value;
                _periods.Save();
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

                _periods.NonTradePeriod4Start.LoadFromString(TextBoxNonTradePeriod4Start.Text);
                _periods.Save();
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

                _periods.NonTradePeriod4End.LoadFromString(TextBoxNonTradePeriod4End.Text);
                _periods.Save();
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
                _periods.NonTradePeriod5OnOff = CheckBoxNonTradePeriod5OnOff.IsChecked.Value;
                _periods.Save();
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

                _periods.NonTradePeriod5Start.LoadFromString(TextBoxNonTradePeriod5Start.Text);
                _periods.Save();
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

                _periods.NonTradePeriod5End.LoadFromString(TextBoxNonTradePeriod5End.Text);
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        #endregion

    }
}
