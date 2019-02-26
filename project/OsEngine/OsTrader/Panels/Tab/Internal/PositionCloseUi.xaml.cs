/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.OsTrader.Panels.Tab.Internal
{
    public partial class ClosePositionUi
    {
        public ClosePositionUi(Position position, decimal lastSecurityPrice)
        {

            InitializeComponent();

            TextBoxPrice.Text = lastSecurityPrice.ToString(new CultureInfo("ru-RU"));
            TextBoxSecurity.Text = position.OpenOrders[0].SecurityNameCode;
            TextBoxPositionNumber.Text = position.Number.ToString(new CultureInfo("ru-RU"));
            TextBoxVolume.Text = position.OpenVolume.ToString(new CultureInfo("ru-RU"));
            TextBoxAcebergOrdersCount.Text = "1";


            ComboBoxOrderType.Items.Add(PositionOpenType.Limit);
            ComboBoxOrderType.Items.Add(PositionOpenType.Market);
            ComboBoxOrderType.Items.Add(PositionOpenType.Aceberg);
            ComboBoxOrderType.SelectedItem = PositionOpenType.Limit;
            ComboBoxOrderType.SelectionChanged += ComboBoxOrderType_SelectionChanged;

            Title = OsLocalization.Trader.Label100;
            LabelPositionNumber.Content = OsLocalization.Trader.Label101;
            LabelSecurity.Content = OsLocalization.Trader.Label102;
            LabelVolume.Content = OsLocalization.Trader.Label30;
            LabelPrice.Content = OsLocalization.Trader.Label31;
            LabelOrderType.Content = OsLocalization.Trader.Label103;
            LabelOrdersToIceberg.Content = OsLocalization.Trader.Label104;
            ButtonAccept.Content = OsLocalization.Trader.Label17;
        }

        void ComboBoxOrderType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                PositionOpenType type;
                try
                {
                    Enum.TryParse(ComboBoxOrderType.SelectedItem.ToString(), true, out type);
                }
                catch (Exception)
                {
                    return;
                }

                if (type == PositionOpenType.Limit)
                {
                    TextBoxPrice.IsEnabled = true;
                }
                if (type == PositionOpenType.Market)
                {
                    TextBoxPrice.IsEnabled = false;
                }
                if (type == PositionOpenType.Aceberg)
                {
                    TextBoxAcebergOrdersCount.IsEnabled = true;
                    TextBoxPrice.IsEnabled = true;
                }
                else
                {
                    TextBoxAcebergOrdersCount.IsEnabled = false;
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
        }

        public bool IsAccept;

        public decimal Price;

        public int CountAcebertOrder;

        public PositionOpenType OpenType;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Price = Convert.ToDecimal(TextBoxPrice.Text);

                Enum.TryParse(ComboBoxOrderType.Text, true, out OpenType);

                IsAccept = true;
            }
            catch (Exception)
            {
                MessageBox.Show(OsLocalization.Trader.Label13);
                return;
            }

            if (OpenType == PositionOpenType.Aceberg)
            {
                try
                {
                    CountAcebertOrder = Convert.ToInt32(TextBoxAcebergOrdersCount.Text);
                }
                catch (Exception)
                {
                    MessageBox.Show(OsLocalization.Trader.Label13);
                    return;
                }
            }


            Close();
        }

    }
}
