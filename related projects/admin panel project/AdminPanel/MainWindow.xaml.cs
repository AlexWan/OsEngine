using AdminPanel.Entity;
using AdminPanel.Language;
using AdminPanel.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AdminPanel.Utils;

namespace AdminPanel
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ApplicationViewModel _appVm;

        public MainWindow()
        {
            InitializeComponent();

            CreateDir();
            _appVm = new ApplicationViewModel(this);
            _appVm.Load();
            _appVm.SelectedClient = _appVm.Clients.Count != 0 ? _appVm.Clients[0] : null;

            DataContext = _appVm;

            List<OsLocalization.OsLocalType> localizations = OsLocalization.GetExistLocalizationTypes();
            Closing += RobotUi_Closing;

            for (int i = 0; i < localizations.Count; i++)
            {
                ComboBoxLocalization.Items.Add(localizations[i].ToString());
            }

            ComboBoxLocalization.SelectedItem = OsLocalization.CurLocalization.ToString();
            ComboBoxLocalization.SelectionChanged += delegate
            {
                OsLocalization.OsLocalType newType;

                if (Enum.TryParse(ComboBoxLocalization.SelectedItem.ToString(), out newType))
                {
                    if (OsLocalization.CurLocalization != newType)
                    {
                        OsLocalization.CurLocalization = newType;
                        Local();
                    }
                }
            };
            Local();
        }

        public void SetActiveTab(string clientName)
        {
            TabClients.IsSelected = true;
            _appVm.SetSelectedClient(clientName);
        }

        private void Local()
        {
            Title = OsLocalization.MainWindow.Title;
            TabOverview.Header = OsLocalization.MainWindow.TabOverview;
            TabClients.Header = OsLocalization.MainWindow.TabClients;
            TabSettings.Header = OsLocalization.MainWindow.TabSettings;

            LabelTelegramMessages.Content = OsLocalization.SettingsLocal.LabelTelegramMessages;
            LabelKey.Content = OsLocalization.SettingsLocal.LabelKey;
            LabelToken.Content = OsLocalization.SettingsLocal.LabelToken;
            LabelPhone.Content = OsLocalization.SettingsLocal.LabelPhone;
            LabelReceiver.Content = OsLocalization.SettingsLocal.LabelReceiver;
            BtnTestMsg.Content = OsLocalization.SettingsLocal.BtnTestMsg;
            BtnConnect.Content = OsLocalization.SettingsLocal.BtnConnect; 
            LabelSound.Content = OsLocalization.SettingsLocal.LabelSound;
            LabelAlertsType.Content = OsLocalization.SettingsLocal.LabelAlertsType;
            LabelDanger.Content = OsLocalization.SettingsLocal.LabelDanger;
            LabelErrors.Content = OsLocalization.SettingsLocal.LabelErrors;
            LabelLanguage.Content = OsLocalization.SettingsLocal.LabelLanguage;

            BtnAddClient.Content = OsLocalization.Entity.BtnAdd;
            BtnDeleteClient.Content = OsLocalization.Entity.BtnDelete;

            _appVm.ChangeLocal();
        }
        
        void RobotUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (ShowAcceptDialog(OsLocalization.MainWindow.CloseLabel) == false)
            {
                e.Cancel = true;
                return;
            }
            _appVm.Close();
        }

        private bool ShowAcceptDialog(string message)
        {
            AcceptDialogUi ui = new AcceptDialogUi(message);
            ui.ShowDialog();

            return ui.UserAcceptActioin;
        }

        private void BtnAddClient_Click(object sender, RoutedEventArgs e)
        {
            AddClientDialogUi ui = new AddClientDialogUi(_appVm.Clients.ToList());
            ui.ShowDialog();

            var name = ui.CreatedName;

            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            var client = new ClientViewModel(this, _appVm);
            client.Name = name;
            _appVm.AddClient(client);
            _appVm.Save();
        }

        private void BtnDeleteClient_Click(object sender, RoutedEventArgs e)
        {
            if (ShowAcceptDialog(OsLocalization.MainWindow.DeleteLabel) == false)
            {
                return;
            }

            _appVm.RemoveSelectedClient();
            _appVm.Save();
        }

        private void CreateDir()
        {
            if (!Directory.Exists("Engine"))
            {
                Directory.CreateDirectory("Engine");
            }
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null)
            {
                return;
            }
            var item = btn.DataContext as ClientViewModel;
            var index = ClientsGrid.Items.IndexOf(item);

            DataGridRow selectedRow = ClientsGrid.ItemContainerGenerator.ContainerFromIndex(index) as DataGridRow;

            if (selectedRow.DetailsVisibility == Visibility.Collapsed)
            {
                selectedRow.DetailsVisibility = Visibility.Visible;
            }
            else
            {
                selectedRow.DetailsVisibility = Visibility.Collapsed;
            }
        }

        private void BtnTestMsg_OnClick(object sender, RoutedEventArgs e)
        {
            _appVm.TlClient.SendMessage("Test message from admin panel", TextBoxReceiver.Text);
        }

        private async void BtnLogIn_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextBoxKey.Text) ||
                string.IsNullOrEmpty(TextBoxToken.Text) ||
                string.IsNullOrEmpty(TextBoxPhone.Text))
            {
                return;
            }

            var key = Convert.ToInt32(TextBoxKey.Text);
            var token = TextBoxToken.Text;
            var phone = TextBoxPhone.Text;

            var res = await Task.Run(() => _appVm.TlClient.LogIn(key, token, phone));
            if (res)
            {
                LabelState.Content = "Connected";
            }
            else
            {
                MessageBox.Show(OsLocalization.MainWindow.TlConnectError);
                LabelState.Content = "Disconnected";
            }
        }
    }
}
