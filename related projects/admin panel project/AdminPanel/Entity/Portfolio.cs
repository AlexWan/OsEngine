using System.Collections.ObjectModel;

namespace AdminPanel.Entity
{
    public class Portfolio : NotificationObject
    {
        public Portfolio(string name)
        {
            Name = name;
        }

        public string Name { get; set; }

        private decimal _valueBegin;

        public decimal ValueBegin
        {
            get { return _valueBegin; }
            set { SetProperty(ref _valueBegin, value, () => ValueBegin); }
        }

        private decimal _valueCurrent;

        public decimal ValueCurrent
        {
            get { return _valueCurrent; }
            set { SetProperty(ref _valueCurrent, value, () => ValueCurrent); }
        }

        private decimal _valueBlocked;

        public decimal ValueBlocked
        {
            get { return _valueBlocked; }
            set { SetProperty(ref _valueBlocked, value, () => ValueBlocked); }
        }

        private ObservableCollection<PositionOnBoard> _positionsOnBoard = new ObservableCollection<PositionOnBoard>();

        public ObservableCollection<PositionOnBoard> PositionsOnBoard
        {
            get { return _positionsOnBoard; }
            set { SetProperty(ref _positionsOnBoard, value, () => PositionsOnBoard); }
        }
    }

    public class PositionOnBoard : NotificationObject
    {
        public PositionOnBoard(string securityNameCode, string portfolioName)
        {
            SecurityNameCode = securityNameCode;
            PortfolioName = portfolioName;
        }

        private decimal _valueBegin;

        public decimal ValueBegin
        {
            get { return _valueBegin; }
            set { SetProperty(ref _valueBegin, value, () => ValueBegin); }
        }

        private decimal _valueCurrent;

        public decimal ValueCurrent
        {
            get { return _valueCurrent; }
            set { SetProperty(ref _valueCurrent, value, () => ValueCurrent); }
        }

        private decimal _valueBlocked;

        public decimal ValueBlocked
        {
            get { return _valueBlocked; }
            set { SetProperty(ref _valueBlocked, value, () => ValueBlocked); }
        }

        public string SecurityNameCode { get; set; }

        public string PortfolioName { get; set; }
    }
}
