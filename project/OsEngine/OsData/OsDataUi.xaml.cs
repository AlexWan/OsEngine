/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using System.Windows;
using OsEngine.Language;

namespace OsEngine.OsData
{
    public partial class OsDataUi
    {
        private OsDataMasterPainter _osDataMaster;

        public OsDataUi()
        {
            InitializeComponent();
            LabelTimeEndValue.Content = "";
            LabelSetNameValue.Content = "";
            LabelTimeStartValue.Content = "";
            Layout.StickyBorders.Listen(this);

            OsDataMaster master = new OsDataMaster();

            _osDataMaster = new OsDataMasterPainter(master, 
                ChartHostPanel, HostLog, HostSource,
                HostSet, LabelSetNameValue, LabelTimeStartValue,
                LabelTimeEndValue, ProgressBarLoadProgress, TextBoxSearchSource);

            LabelOsa.Content = "V_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Closing += OsDataUi_Closing;
            Label4.Content = OsLocalization.Data.Label4;
            Label24.Content = OsLocalization.Data.Label24;
            Label26.Header = OsLocalization.Data.Label26;
            NewDataSetButton.Content = OsLocalization.Data.Label30;
            LabelSetName.Content = OsLocalization.Data.Label31;
            LabelStartTimeStr.Content = OsLocalization.Data.Label18;
            LabelTimeEndStr.Content = OsLocalization.Data.Label19;
            TextBoxSearchSource.Text = OsLocalization.Market.Label64;

            this.Activate();
            this.Focus();

            _osDataMaster.StartPaintActiveSet();
        }

        private void OsDataUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Data.Label27);
            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                e.Cancel = true;
            }
        }

        private void NewDataSetButton_Click(object sender, RoutedEventArgs e)
        {
            _osDataMaster.CreateNewSetDialog();
        }
    }
}