using AdminPanel.Entity;
using AdminPanel.Language;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Linq;

namespace AdminPanel.ViewModels
{
    public class PortfoliosViewModel : NotificationObject, ILocalization
    {
        private ObservableCollection<Portfolio> _portfolios = new ObservableCollection<Portfolio>();

        public ObservableCollection<Portfolio> Portfolios
        {
            get { return _portfolios; }
            set { SetProperty(ref _portfolios, value, () => Portfolios); }
        }

        #region Local

        private string _nameHeader;

        public string NameHeader
        {
            get => _nameHeader;
            set { SetProperty(ref _nameHeader, value, () => NameHeader); }
        }

        private string _valueBeginHeader;

        public string ValueBeginHeader
        {
            get => _valueBeginHeader;
            set { SetProperty(ref _valueBeginHeader, value, () => ValueBeginHeader); }
        }

        private string _valueCurrentHeader;

        public string ValueCurrentHeader
        {
            get => _valueCurrentHeader;
            set { SetProperty(ref _valueCurrentHeader, value, () => ValueCurrentHeader); }
        }

        private string _valueBlockedHeader;

        public string ValueBlockedHeader
        {
            get => _valueBlockedHeader;
            set { SetProperty(ref _valueBlockedHeader, value, () => ValueBlockedHeader); }
        }

        private string _posSecurityNameHeader;

        public string PosSecurityNameHeader
        {
            get => _posSecurityNameHeader;
            set { SetProperty(ref _posSecurityNameHeader, value, () => PosSecurityNameHeader); }
        }

        private string _posValueBeginHeader;

        public string PosValueBeginHeader
        {
            get => _posValueBeginHeader;
            set { SetProperty(ref _posValueBeginHeader, value, () => PosValueBeginHeader); }
        }

        private string _posValueCurrentHeader;

        public string PosValueCurrentHeader
        {
            get => _posValueCurrentHeader;
            set { SetProperty(ref _posValueCurrentHeader, value, () => PosValueCurrentHeader); }
        }

        private string _posValueBlockedHeader;

        public string PosValueBlockedHeader
        {
            get => _posValueBlockedHeader;
            set { SetProperty(ref _posValueBlockedHeader, value, () => PosValueBlockedHeader); }
        }

        public void ChangeLocal()
        {
            NameHeader = OsLocalization.Entity.ColumnPortfolio1;
            ValueBeginHeader = OsLocalization.Entity.ColumnPortfolio2;
            ValueCurrentHeader = OsLocalization.Entity.ColumnPortfolio3;
            ValueBlockedHeader = OsLocalization.Entity.ColumnPortfolio4;
            PosSecurityNameHeader = OsLocalization.Entity.ColumnPortfolio5;
            PosValueBeginHeader = OsLocalization.Entity.ColumnPortfolio6;
            PosValueCurrentHeader = OsLocalization.Entity.ColumnPortfolio7;
            PosValueBlockedHeader = OsLocalization.Entity.ColumnPortfolio8;
        }

        #endregion

        public void UpdateTable(JArray jArray)
        {
            foreach (var jOrder in jArray)
            {
                var name = jOrder["Number"].Value<string>();

                var needPortfolio = Portfolios.FirstOrDefault(p => p.Name == name);

                if (needPortfolio == null)
                {
                    needPortfolio = new Portfolio(name);
                    
                    AddElement(needPortfolio, Portfolios);
                }

                needPortfolio.ValueBegin = jOrder["ValueBegin"].Value<decimal>();
                needPortfolio.ValueCurrent = jOrder["ValueCurrent"].Value<decimal>();
                needPortfolio.ValueBlocked = jOrder["ValueBlocked"].Value<decimal>();

                if (jOrder.Value<JArray>("PositionsOnBoard") != null)
                {
                    var emptyPos = needPortfolio.PositionsOnBoard.FirstOrDefault(p => p.SecurityNameCode == "No positions");
                    if (emptyPos != null)
                    {
                        RemoveElement(emptyPos, needPortfolio.PositionsOnBoard);
                    }

                    var posOnBoard = jOrder["PositionsOnBoard"].Value<JArray>();

                    foreach (var position in posOnBoard)
                    {
                        var sec = position["SecurityNameCode"].Value<string>();
                        var needPos = needPortfolio.PositionsOnBoard.FirstOrDefault(p => p.SecurityNameCode == sec);
                        if (needPos == null)
                        {
                            needPos = new PositionOnBoard(sec, name);
                            AddElement(needPos, needPortfolio.PositionsOnBoard);
                        }
                        needPos.ValueBegin = position["ValueBegin"].Value<decimal>();
                        needPos.ValueCurrent = position["ValueCurrent"].Value<decimal>();
                        needPos.ValueBlocked = position["ValueBlocked"].Value<decimal>();
                    }
                }
                else
                {
                    var needPos = needPortfolio.PositionsOnBoard.FirstOrDefault(p => p.SecurityNameCode == "No positions");
                    if (needPos == null)
                    {
                        needPos = new PositionOnBoard("No positions", name);
                        Clear(needPortfolio.PositionsOnBoard);
                        AddElement(needPos, needPortfolio.PositionsOnBoard);
                    }
                }
            }
        }
    }
}
