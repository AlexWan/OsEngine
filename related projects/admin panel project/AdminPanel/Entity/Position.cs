using System;

namespace AdminPanel.Entity
{
    public class Position : NotificationObject
    {
        public Position(int number)
        {
            Number = number;
        }

        public int Number { get; }
        public DateTime OpenTime { get; set; }
        public string Bot { get; set; }
        public string Security { get; set; }
        public string Direction { get; set; }


        private DateTime _closeTime = new DateTime();

        public DateTime CloseTime
        {
            get { return _closeTime; }
            set { SetProperty(ref _closeTime, value, () => CloseTime); }
        }

        private string _state;

        public string State
        {
            get { return _state; }
            set { SetProperty(ref _state, value, () => State); }
        } 
        
        private decimal _volume;

        public decimal Volume
        {
            get { return _volume; }
            set { SetProperty(ref _volume, value, () => Volume); }
        }
        
        private decimal _openVolume;

        public decimal OpenVolume
        {
            get { return _openVolume; }
            set { SetProperty(ref _openVolume, value, () => OpenVolume); }
        }
        private decimal _waitVolume;

        public decimal WaitVolume
        {
            get { return _waitVolume; }
            set { SetProperty(ref _waitVolume, value, () => WaitVolume); }
        }
        private decimal _enterPrice;

        public decimal EnterPrice
        {
            get { return _enterPrice; }
            set { SetProperty(ref _enterPrice, value, () => EnterPrice); }
        }
        private decimal _exitPrice;

        public decimal ExitPrice
        {
            get { return _exitPrice; }
            set { SetProperty(ref _exitPrice, value, () => ExitPrice); }
        }

        private decimal _profit;

        public decimal Profit
        {
            get { return _profit; }
            set { SetProperty(ref _profit, value, () => Profit); }
        }
        private decimal _stopActivation;

        public decimal StopActivation
        {
            get { return _stopActivation; }
            set { SetProperty(ref _stopActivation, value, () => StopActivation); }
        }
        private decimal _stopPrice;

        public decimal StopPrice
        {
            get { return _stopPrice; }
            set { SetProperty(ref _stopPrice, value, () => StopPrice); }
        }
        private decimal _profitActivation;

        public decimal ProfitActivation
        {
            get { return _profitActivation; }
            set { SetProperty(ref _profitActivation, value, () => ProfitActivation); }
        }
        private decimal _profitPrice;

        public decimal ProfitPrice
        {
            get { return _profitPrice; }
            set { SetProperty(ref _profitPrice, value, () => ProfitPrice); }
        }
    }
}
