using System;
using AdminPanel.Entity;
using AdminPanel.Language;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace AdminPanel.ViewModels
{
    public class OrdersViewModel : NotificationObject, ILocalization
    {
        private ObservableCollection<Order> _orders = new ObservableCollection<Order>();

        public ObservableCollection<Order> Orders
        {
            get { return _orders; }
            set { SetProperty(ref _orders, value, () => Orders); }
        }

        #region Local

        private string _robotHeader;

        public string RobotHeader
        {
            get => _robotHeader;
            set { SetProperty(ref _robotHeader, value, () => RobotHeader); }
        }

        private string _idHeader;

        public string IdHeader
        {
            get => _idHeader;
            set { SetProperty(ref _idHeader, value, () => IdHeader); }
        }

        private string _idMarketHeader;

        public string IdMarketHeader
        {
            get => _idMarketHeader;
            set { SetProperty(ref _idMarketHeader, value, () => IdMarketHeader); }
        }

        private string _timeCreateHeader;

        public string TimeCreateHeader
        {
            get => _timeCreateHeader;
            set { SetProperty(ref _timeCreateHeader, value, () => TimeCreateHeader); }
        }

        private string _securityNameHeader;

        public string SecurityNameHeader
        {
            get => _securityNameHeader;
            set { SetProperty(ref _securityNameHeader, value, () => SecurityNameHeader); }
        }

        private string _portfolioHeader;

        public string PortfolioHeader
        {
            get => _portfolioHeader;
            set { SetProperty(ref _portfolioHeader, value, () => PortfolioHeader); }
        }

        private string _sideHeader;

        public string SideHeader
        {
            get => _sideHeader;
            set { SetProperty(ref _sideHeader, value, () => SideHeader); }
        }

        private string _stateHeader;

        public string StateHeader
        {
            get => _stateHeader;
            set { SetProperty(ref _stateHeader, value, () => StateHeader); }
        }

        private string _priceHeader;

        public string PriceHeader
        {
            get => _priceHeader;
            set { SetProperty(ref _priceHeader, value, () => PriceHeader); }
        }
        private string _priceRealHeader;

        public string PriceRealHeader
        {
            get => _priceRealHeader;
            set { SetProperty(ref _priceRealHeader, value, () => PriceRealHeader); }
        }
        private string _volumeHeader;

        public string VolumeHeader
        {
            get => _volumeHeader;
            set { SetProperty(ref _volumeHeader, value, () => VolumeHeader); }
        }
        private string _typeHeader;

        public string TypeHeader
        {
            get => _typeHeader;
            set { SetProperty(ref _typeHeader, value, () => TypeHeader); }
        }
        private string _timeRoundTripHeader;

        public string TimeRoundTripHeader
        {
            get => _timeRoundTripHeader;
            set { SetProperty(ref _timeRoundTripHeader, value, () => TimeRoundTripHeader); }
        }

        public void ChangeLocal()
        {
            RobotHeader = OsLocalization.Entity.OrderColumn0;
            IdHeader = OsLocalization.Entity.OrderColumn1;
            IdMarketHeader = OsLocalization.Entity.OrderColumn2;
            TimeCreateHeader = OsLocalization.Entity.OrderColumn3;
            SecurityNameHeader = OsLocalization.Entity.OrderColumn4;
            PortfolioHeader = OsLocalization.Entity.OrderColumn5;
            SideHeader = OsLocalization.Entity.OrderColumn6;
            StateHeader = OsLocalization.Entity.OrderColumn7;
            PriceHeader = OsLocalization.Entity.OrderColumn8;
            PriceRealHeader = OsLocalization.Entity.OrderColumn9;
            VolumeHeader = OsLocalization.Entity.OrderColumn10;
            TypeHeader = OsLocalization.Entity.OrderColumn11;
            TimeRoundTripHeader = OsLocalization.Entity.OrderColumn12;
        }

        #endregion

        public void UpdateTable(JArray jArray)
        {
            foreach (var jOrder in jArray)
            {
                var number = jOrder["NumberUser"].Value<int>();

                var needOrder = Orders.FirstOrDefault(p => p.NumberUser == number);

                if (needOrder == null)
                {
                    needOrder = new Order(number);
                    needOrder.RobotName = jOrder["RobotName"].Value<string>();
                    needOrder.NumberMarket = jOrder["NumberMarket"].Value<string>();
                    needOrder.TimeCreate = DateTime.Parse(jOrder["TimeCreate"].Value<string>(), CultureInfo.CurrentCulture);
                    needOrder.SecurityNameCode = jOrder["SecurityNameCode"].Value<string>();
                    needOrder.PortfolioNumber = jOrder["PortfolioNumber"].Value<string>();
                    needOrder.Side = jOrder["Side"].Value<string>();
                    needOrder.TimeRoundTrip = jOrder["TimeRoundTrip"].Value<string>();
                    needOrder.Type = jOrder["TypeOrder"].Value<string>();
                    
                    AddElement(needOrder, Orders);
                }

                needOrder.State = jOrder["State"].Value<string>();
                needOrder.Price = jOrder["Price"].Value<decimal>();
                needOrder.PriceReal = jOrder["PriceReal"].Value<decimal>();
                needOrder.Volume = jOrder["Volume"].Value<decimal>();
            }
        }
    }
}
