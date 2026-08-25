/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System.Windows;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    /// <summary>
    /// Test description window for the WServerTester robot
    /// Окно описания теста для робота WServerTester
    /// </summary>
    public partial class WServerTesterDescriptionUi : Window
    {
        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="title">window title / заголовок окна</param>
        /// <param name="text">test description text / текст описания теста</param>
        public WServerTesterDescriptionUi(string title, string text)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            Title = title;
            TextBlockMessage.Text = text;

            ButtonAccept.Content = OsLocalization.Entity.ButtonAccept;

            this.Activate();
            this.Focus();
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
