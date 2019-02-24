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
    public partial class PositionModificateUi
    {
        public PositionModificateUi(decimal lastPrice, string nameSecurity)
        {
            InitializeComponent();

            ComboBoxSide.Items.Add(Side.Buy);
            ComboBoxSide.Items.Add(Side.Sell);
            Side = Side.Buy;
            ComboBoxSide.SelectedItem = Side;
            TextBoxAcebergOrdersCount.Text = "2";

            TextBoxSecurity.Text = nameSecurity;
            TextBoxVolume.Text = "1";
            TextBoxPrice.Text = lastPrice.ToString(new CultureInfo("ru-RU"));

            ComboBoxOrderType.Items.Add(PositionOpenType.Limit);
            ComboBoxOrderType.Items.Add(PositionOpenType.Market);
            ComboBoxOrderType.Items.Add(PositionOpenType.Aceberg);
            ComboBoxOrderType.SelectedItem = PositionOpenType.Limit;

            ComboBoxOrderType.SelectionChanged += ComboBoxOrderType_SelectionChanged;


            Title = OsLocalization.Trader.Label105; 
            LabelSecurity.Content = OsLocalization.Trader.Label102;
            LabelVolume.Content = OsLocalization.Trader.Label30;
            LabelPrice.Content = OsLocalization.Trader.Label31;
            LabelOrderType.Content = OsLocalization.Trader.Label103;
            LabelOrdersToIceberg.Content = OsLocalization.Trader.Label104;
            ButtonAccept.Content = OsLocalization.Trader.Label17;
            LabelSide.Content = OsLocalization.Trader.Label106;

        }

        void ComboBoxOrderType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
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

        public bool IsAccept;

        public decimal Price;

        public decimal Volume;

        public PositionOpenType OpenType;

        public Side Side;

        public int CountAcebertOrder;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Price = Convert.ToDecimal(TextBoxPrice.Text);
                Volume = Convert.ToDecimal(TextBoxVolume.Text);

                if (Volume <= 0)
                {
                    throw new Exception();
                }

                Enum.TryParse(ComboBoxOrderType.Text, true, out OpenType);

                Enum.TryParse(ComboBoxSide.Text, true, out Side);
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
