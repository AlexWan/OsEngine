using System;
using AdminPanel.Language;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using AdminPanel.Entity;
using Newtonsoft.Json.Linq;

namespace AdminPanel.ViewModels
{
    public class PositionsViewModel : NotificationObject, ILocalization
    {
        private ObservableCollection<Position> _positions = new ObservableCollection<Position>();

        public ObservableCollection<Position> Positions
        {
            get { return _positions; }
            set { SetProperty(ref _positions, value, () => Positions); }
        }

        #region Positions local

        private string _numberHeader = OsLocalization.Entity.PositionColumn1;

        public string NumberHeader
        {
            get { return _numberHeader; }
            set { SetProperty(ref _numberHeader, value, () => NumberHeader); }
        }

        private string _openTimeHeader = OsLocalization.Entity.PositionColumn2;

        public string OpenTimeHeader
        {
            get { return _openTimeHeader; }
            set { SetProperty(ref _openTimeHeader, value, () => OpenTimeHeader); }
        }

        private string _closeTimeHeader = OsLocalization.Entity.PositionColumn3;

        public string CloseTimeHeader
        {
            get { return _closeTimeHeader; }
            set { SetProperty(ref _closeTimeHeader, value, () => CloseTimeHeader); }
        }

        private string _botHeader = OsLocalization.Entity.PositionColumn4;

        public string BotHeader
        {
            get { return _botHeader; }
            set { SetProperty(ref _botHeader, value, () => BotHeader); }
        }

        private string _securityHeader = OsLocalization.Entity.PositionColumn5;

        public string SecurityHeader
        {
            get { return _securityHeader; }
            set { SetProperty(ref _securityHeader, value, () => SecurityHeader); }
        }

        private string _directionHeader = OsLocalization.Entity.PositionColumn6;

        public string DirectionHeader
        {
            get { return _directionHeader; }
            set { SetProperty(ref _directionHeader, value, () => DirectionHeader); }
        }

        private string _stateHeader = OsLocalization.Entity.PositionColumn7;

        public string StateHeader
        {
            get { return _stateHeader; }
            set { SetProperty(ref _stateHeader, value, () => StateHeader); }
        }

        private string _volumeHeader = OsLocalization.Entity.PositionColumn8;

        public string VolumeHeader
        {
            get { return _volumeHeader; }
            set { SetProperty(ref _volumeHeader, value, () => VolumeHeader); }
        }

        private string _nowVolumeHeader = OsLocalization.Entity.PositionColumn9;

        public string NowVolumeHeader
        {
            get { return _nowVolumeHeader; }
            set { SetProperty(ref _nowVolumeHeader, value, () => NowVolumeHeader); }
        }

        private string _waitVolumeHeader = OsLocalization.Entity.PositionColumn10;

        public string WaitVolumeHeader
        {
            get { return _waitVolumeHeader; }
            set { SetProperty(ref _waitVolumeHeader, value, () => WaitVolumeHeader); }
        }

        private string _enterPriceHeader = OsLocalization.Entity.PositionColumn11;

        public string EnterPriceHeader
        {
            get { return _enterPriceHeader; }
            set { SetProperty(ref _enterPriceHeader, value, () => EnterPriceHeader); }
        }

        private string _exitPriceHeader = OsLocalization.Entity.PositionColumn12;

        public string ExitPriceHeader
        {
            get { return _exitPriceHeader; }
            set { SetProperty(ref _exitPriceHeader, value, () => ExitPriceHeader); }
        }

        private string _profitHeader = OsLocalization.Entity.PositionColumn13;

        public string ProfitHeader
        {
            get { return _profitHeader; }
            set { SetProperty(ref _profitHeader, value, () => ProfitHeader); }
        }

        private string _stopActivationHeader = OsLocalization.Entity.PositionColumn14;

        public string StopActivationHeader
        {
            get { return _stopActivationHeader; }
            set { SetProperty(ref _stopActivationHeader, value, () => StopActivationHeader); }
        }

        private string _stopPriceHeader = OsLocalization.Entity.PositionColumn15;

        public string StopPriceHeader
        {
            get { return _stopPriceHeader; }
            set { SetProperty(ref _stopPriceHeader, value, () => StopPriceHeader); }
        }

        private string _profitActivationHeader = OsLocalization.Entity.PositionColumn16;

        public string ProfitActivationHeader
        {
            get { return _profitActivationHeader; }
            set { SetProperty(ref _profitActivationHeader, value, () => ProfitActivationHeader); }
        }

        private string _profitPriceHeader = OsLocalization.Entity.PositionColumn17;

        public string ProfitPriceHeader
        {
            get { return _profitPriceHeader; }
            set { SetProperty(ref _profitPriceHeader, value, () => ProfitPriceHeader); }
        }

        public void ChangeLocal()
        {
            NumberHeader = OsLocalization.Entity.PositionColumn1;
            OpenTimeHeader = OsLocalization.Entity.PositionColumn2;
            CloseTimeHeader = OsLocalization.Entity.PositionColumn3;
            BotHeader = OsLocalization.Entity.PositionColumn4;
            SecurityHeader = OsLocalization.Entity.PositionColumn5;
            DirectionHeader = OsLocalization.Entity.PositionColumn6;
            StateHeader = OsLocalization.Entity.PositionColumn7;
            VolumeHeader = OsLocalization.Entity.PositionColumn8;
            NowVolumeHeader = OsLocalization.Entity.PositionColumn9;
            WaitVolumeHeader = OsLocalization.Entity.PositionColumn10;
            EnterPriceHeader = OsLocalization.Entity.PositionColumn11;
            ExitPriceHeader = OsLocalization.Entity.PositionColumn12;
            ProfitHeader = OsLocalization.Entity.PositionColumn13;
            StopActivationHeader = OsLocalization.Entity.PositionColumn14;
            StopPriceHeader = OsLocalization.Entity.PositionColumn15;
            ProfitActivationHeader = OsLocalization.Entity.PositionColumn16;
            ProfitPriceHeader = OsLocalization.Entity.PositionColumn17;
        }

        #endregion

        public void UpdateTable(JArray jArray)
        {
            foreach (var jPosition in jArray)
            {
                var number = jPosition["Number"].Value<int>();

                var needPosition = Positions.FirstOrDefault(p => p.Number == number);

                if (needPosition == null)
                {
                    needPosition = new Position(number);
                    needPosition.OpenTime = DateTime.Parse(jPosition["TimeOpen"].Value<string>(), CultureInfo.CurrentCulture);
                    needPosition.Bot = jPosition["NameBot"].Value<string>();
                    needPosition.Security = jPosition["SecurityName"].Value<string>();
                    needPosition.Direction = jPosition["Direction"].Value<string>();

                    AddElement(needPosition, Positions);
                }

                needPosition.CloseTime = DateTime.Parse(jPosition["TimeClose"].Value<string>(), CultureInfo.CurrentCulture);
                needPosition.State = jPosition["State"].Value<string>();
                needPosition.Volume = jPosition["MaxVolume"].Value<decimal>();
                needPosition.OpenVolume = jPosition["OpenVolume"].Value<decimal>();
                needPosition.WaitVolume = jPosition["WaitVolume"].Value<decimal>();
                needPosition.EnterPrice = jPosition["EntryPrice"].Value<decimal>();
                needPosition.ExitPrice = jPosition["ClosePrice"].Value<decimal>();
                needPosition.Profit = jPosition["ProfitPortfolioPunkt"].Value<decimal>();
                needPosition.StopActivation = jPosition["StopOrderRedLine"].Value<decimal>();
                needPosition.StopPrice = jPosition["StopOrderPrice"].Value<decimal>();
                needPosition.ProfitActivation = jPosition["ProfitOrderRedLine"].Value<decimal>();
                needPosition.ProfitPrice = jPosition["ProfitOrderPrice"].Value<decimal>();
            }
        }
    }
}
