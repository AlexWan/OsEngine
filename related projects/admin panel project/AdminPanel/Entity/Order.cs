using System;

namespace AdminPanel.Entity
{
    public class Order : NotificationObject
    {
        public Order(int numberUser)
        {
            NumberUser = numberUser;
        }
        public string RobotName { get; set; }

        public int NumberUser { get; }

        public string NumberMarket { get; set; }

        public DateTime TimeCreate { get; set; }

        public string SecurityNameCode { get; set; }

        public string PortfolioNumber { get; set; }

        public string Side { get; set; }

        private string _state;

        public string State
        {
            get { return _state; }
            set { SetProperty(ref _state, value, () => State); }
        }

        private decimal _price;

        public decimal Price
        {
            get { return _price; }
            set { SetProperty(ref _price, value, () => Price); }
        }

        private decimal _priceReal;

        public decimal PriceReal
        {
            get { return _priceReal; }
            set { SetProperty(ref _priceReal, value, () => PriceReal); }
        }

        private decimal _volume;

        public decimal Volume
        {
            get { return _volume; }
            set { SetProperty(ref _volume, value, () => Volume); }
        }

        public string Type { get; set; }

        public string TimeRoundTrip { get; set; }
    }
}
