using OsEngine.Language;
using System.Windows;

namespace OsEngine.OsData
{
    public partial class LqdtDataUi : Window
    {
        OsDataSet _set;

        OsDataSetPainter _setPainter;

        public LqdtDataUi(OsDataSet set, OsDataSetPainter setPainter)
        {
            InitializeComponent();

            _set = set;
            _setPainter = setPainter;

            Title = OsLocalization.Data.TitleAddLqdt;
            ExchangeLabel.Content = OsLocalization.Data.Label60;
            CreateButton.Content = OsLocalization.Data.ButtonCreate;

            Activate();
            Focus();
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (ComboBoxExchange.Text == "MOEX")
            {
                _set.AddLqdtMoex();
            }
            else // NYSE
            {
                _set.AddLqdtNyse();
            }

            _setPainter.RePaintInterface();

            Close();
        }
    }
}
