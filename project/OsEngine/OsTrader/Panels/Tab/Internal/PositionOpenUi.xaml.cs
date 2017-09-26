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
    /// Логика взаимодействия для PositionOpenDialog.xaml
    /// </summary>
    public partial class PositionOpenUi
    {
        public PositionOpenUi(decimal lastPrice, string nameSecurity)
        {
            InitializeComponent();

            ComboBoxSide.Items.Add(Side.Buy);
            ComboBoxSide.Items.Add(Side.Sell);
            ComboBoxSide.SelectedItem = Side.Buy;

            TextBoxSecurity.Text = nameSecurity;
            TextBoxVolume.Text = "1";
            TextBoxPrice.Text = lastPrice.ToString(new CultureInfo("ru-RU"));
            TextBoxAcebergOrdersCount.Text = "2";

            ComboBoxOrderType.Items.Add(PositionOpenType.Limit);
            ComboBoxOrderType.Items.Add(PositionOpenType.Market);
            ComboBoxOrderType.Items.Add(PositionOpenType.Aceberg);
            ComboBoxOrderType.SelectedItem = PositionOpenType.Limit;
            ComboBoxOrderType.SelectionChanged += ComboBoxOrderType_SelectionChanged;

        }

        /// <summary>
        /// пользователь изменил тип открытия позиции
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
        /// нужно ли открывать позицию после закрытия окна
        /// </summary>
        public bool IsAccept;

        /// <summary>
        /// цена
        /// </summary>
        public decimal Price;

        /// <summary>
        /// объём
        /// </summary>
        public decimal Volume;
        
        /// <summary>
        /// тип открытия позиции
        /// </summary>
        public PositionOpenType OpenType;

        /// <summary>
        /// направление
        /// </summary>
        public Side Side;

        /// <summary>
        /// количество ордеров для айсберга
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
