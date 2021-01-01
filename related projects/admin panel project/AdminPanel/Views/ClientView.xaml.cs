using System.Linq;
using AdminPanel.ViewModels;
using System.Windows;
using System.Windows.Controls;
using AdminPanel.Entity;
using AdminPanel.Language;

namespace AdminPanel.Views
{
    /// <summary>
    /// Логика взаимодействия для ClientView.xaml
    /// </summary>
    public partial class ClientView : UserControl
    {
        public ClientView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private ClientViewModel _vm;

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ChangeContext();
        }

        private void ChangeContext()
        {
            _vm = (ClientViewModel)DataContext;

            if (_vm == null)
            {
                return;
            }

            TabServers.Items.Clear();
            TabPositions.Items.Clear();
            TabPortfolios.Items.Clear();
            TabEngineOrders.Items.Clear();
            TabEngineRobots.Items.Clear();
            
            foreach (var engineViewModel in _vm.Engines)
            {
                var tabServers = GetTab(engineViewModel.EngineName);

                var serversView = new ServersView();
                serversView.DataContext = engineViewModel.ServersVm;
                tabServers.Content = serversView;
                TabServers.Items.Add(tabServers);

                var tabPositions = GetTab(engineViewModel.EngineName);

                var positionsView = new PositionsView();
                positionsView.DataContext = engineViewModel.PositionsVm;
                tabPositions.Content = positionsView;
                TabPositions.Items.Add(tabPositions);

                var tabPortfolios = GetTab(engineViewModel.EngineName);

                var portfoliosView = new PortfoliosView();
                portfoliosView.DataContext = engineViewModel.PortfoliosVm;
                tabPortfolios.Content = portfoliosView;
                TabPortfolios.Items.Add(tabPortfolios);

                var tabOrders = GetTab(engineViewModel.EngineName);

                var ordersView = new OrdersView();
                ordersView.DataContext = engineViewModel.OrdersVm;
                tabOrders.Content = ordersView;
                TabEngineOrders.Items.Add(tabOrders);

                var tabRobots = GetTab(engineViewModel.EngineName);
                
                var robotsView = new RobotsView();
                robotsView.DataContext = engineViewModel.RobotsVm;
                tabRobots.Content = robotsView;
                TabEngineRobots.Items.Add(tabRobots);
            }
        }

        private TabItem GetTab(string name)
        {
            var tab = new TabItem()
            {
                Name = name,
                Header = name,
                Width = 140,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            return tab;
        }

        private void ButtonAdd_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null)
            {
                return;
            }
            var engine = new EngineViewModel();
            AddEngineForm form = new AddEngineForm(OsLocalization.Entity.BtnAdd, engine, _vm.Engines.ToList());
            var result = form.ShowDialog();

            if (result.HasValue && result.Value == true)
            {
                _vm.AddEngine(engine);
            }
            ChangeContext();
        }

        private void ButtonRemove_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null)
            {
                return;
            }
            if (ShowAcceptDialog(OsLocalization.MainWindow.DeleteLabel2) == false)
            {
                return;
            }
            _vm.RemoveSelected();
            ChangeContext();
        }

        private bool ShowAcceptDialog(string message)
        {
            AcceptDialogUi ui = new AcceptDialogUi(message);
            ui.ShowDialog();

            return ui.UserAcceptActioin;
        }

        private void ButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null)
            {
                return;
            }
            AddEngineForm form = new AddEngineForm(OsLocalization.Entity.BtnEdit, _vm.SelectedEngine, null);
            var result = form.ShowDialog();
            _vm.OnChanged();
            ChangeContext();
        }

        private void ButtonReboot_OnClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null)
            {
                return;
            }
            var item = btn.DataContext as EngineViewModel;

            var clientVm = DataContext as ClientViewModel;

            if (clientVm != null && item != null)
            {
                clientVm?.Reboot(item.ProcessId);
            }
        }
    }
}
