/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.OsTrader.Panels.Tab.Internal
{
    /// <summary>
    /// Логика взаимодействия для PositionStopDialog.xaml
    /// </summary>
    public partial class PositionStopUi
    {
        /// <summary>
        /// создать окно для смены стопа / профита по сделке
        /// </summary>
        /// <param name="position">позиция для которой меняем стоп/профит</param>
        /// <param name="lastSecurityPrice">последняя цена по бумаге</param>
        /// <param name="title">название окна</param>
        public PositionStopUi(Position position, decimal lastSecurityPrice, string title)
        {
            InitializeComponent();
            Title = title;

            TextBoxPriceOrder.Text = lastSecurityPrice.ToString(new CultureInfo("ru-RU"));
            TextBoxPriceActivation.Text = lastSecurityPrice.ToString(new CultureInfo("ru-RU"));
            TextBoxSecurity.Text = position.OpenOrders[0].SecurityNameCode;
            TextBoxPositionNumber.Text = position.Number.ToString(new CultureInfo("ru-RU"));
            TextBoxVolume.Text = position.OpenVolume.ToString(new CultureInfo("ru-RU"));

            LabelPositionNumber.Content = OsLocalization.Trader.Label101;
            LabelSecurity.Content = OsLocalization.Trader.Label102;
            LabelVolume.Content = OsLocalization.Trader.Label30;
            ButtonAccept.Content = OsLocalization.Trader.Label17;
            LabelActivationPrice.Content = OsLocalization.Trader.Label108;
            LabelOrderPrice.Content = OsLocalization.Trader.Label109;
        }

// результаты 

        /// <summary>
        /// нужно ли выставлять стоп после закрытия окна
        /// </summary>
        public bool IsAccept;

        /// <summary>
        /// цена после которой будет выставлен ордер на закрытие позиции
        /// </summary>
        public decimal PriceOrder;

        /// <summary>
        /// цена ордера для закрытия позиции
        /// </summary>
        public decimal PriceActivate;

        /// <summary>
        /// кнопка Принять
        /// </summary>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PriceOrder = Convert.ToDecimal(TextBoxPriceOrder.Text);
                PriceActivate = Convert.ToDecimal(TextBoxPriceActivation.Text);
                IsAccept = true;
            }
            catch
            {
                MessageBox.Show(OsLocalization.Trader.Label13);
                return;
            }
            Close();
        }
    }
}
