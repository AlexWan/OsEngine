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
    /// Логика взаимодействия для PositionCloseDialog.xaml
    /// </summary>
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
        }

        /// <summary>
        /// пользователь сменил тип ордера
        /// </summary>
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

// результаты 

        /// <summary>
        /// нужно ли сохранять данные после закрытия формы
        /// </summary>
        public bool IsAccept;

        /// <summary>
        /// цена ордера
        /// </summary>
        public decimal Price;

        /// <summary>
        /// кол-во ордеров в айсберге
        /// </summary>
        public int CountAcebertOrder;

        /// <summary>
        /// тип открытия сделки
        /// </summary>
        public PositionOpenType OpenType;

        /// <summary>
        /// кнопка принять
        /// </summary>
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
