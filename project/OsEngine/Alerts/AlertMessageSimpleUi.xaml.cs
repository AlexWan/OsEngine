/*
 *Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/


using OsEngine.Language;

namespace OsEngine.Alerts
{
    /// <summary>
    /// Message box
    /// Окно сообщений
    /// </summary>
    public partial class AlertMessageSimpleUi
    {

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="message">сообщение</param>
        public AlertMessageSimpleUi(string message)
        {
            InitializeComponent();
            TextBoxMessage.Text = message;

            Title = OsLocalization.Alerts.TitleAlertMessageSimpleUi;
        }
    }
}
