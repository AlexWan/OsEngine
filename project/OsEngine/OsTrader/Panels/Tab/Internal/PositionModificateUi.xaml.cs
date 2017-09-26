/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;
using OsEngine.Entity;

namespace OsEngine.OsTrader.Panels.Tab.Internal
{
    /// <summary>
    /// Логика взаимодействия для PositionModificateUi.xaml
    /// </summary>
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

        }

        /// <summary>
        /// пользователь переключил тип открытия сделки
        /// </summary>
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

// результат

        /// <summary>
        /// нужно ли модифицировать позицию после закрытия окна
        /// </summary>
        public bool IsAccept;

        /// <summary>
        /// цена ордера
        /// </summary>
        public decimal Price;

        /// <summary>
        /// объём
        /// </summary>
        public decimal Volume;

        /// <summary>
        /// тип модификации позиции
        /// </summary>
        public PositionOpenType OpenType;

        /// <summary>
        /// сторона ордера
        /// </summary>
        public Side Side;

        /// <summary>
        /// кол-во ордеров в сделке
        /// </summary>
        public int CountAcebertOrder;

        /// <summary>
        /// кнопка принять
        /// </summary>
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
                MessageBox.Show("В одном из полей недопустимое значение. Операция прервана.");
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
                    MessageBox.Show("В одном из полей недопустимое значение. Операция прервана.");
                    return;
                }
            }

            Close();
        }
    }
}
