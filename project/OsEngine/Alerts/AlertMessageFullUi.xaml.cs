/*
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using System.Windows.Forms;
using OsEngine.Language;

namespace OsEngine.Alerts
{
    /// <summary>
    /// Логика взаимодействия для AlertMessageUi.xaml
    /// </summary>
    public partial class AlertMessageFullUi
    {
        public AlertMessageFullUi(DataGridView grid)
        {
            InitializeComponent();
            HostAlerts.Child = grid;
            HostAlerts.Child.Show();
            Title = OsLocalization.Alerts.TitleAlertMessageFullUi;

            OsLocalization.LocalizationTypeChangeEvent += delegate { Title = OsLocalization.Alerts.TitleAlertMessageFullUi; };
        }
    }
}
