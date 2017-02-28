/*
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/


namespace OsEngine.Alerts
{
    /// <summary>
    /// Окно сообщений
    /// </summary>
    public partial class AlertMessageSimpleUi
    {

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="message">сообщение</param>
        public AlertMessageSimpleUi(string message)
        {
            InitializeComponent();
            TextBoxMessage.Text = message;
        }
    }
}
