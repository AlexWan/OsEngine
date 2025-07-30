/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace OsEngine.Market.AutoFollow
{
    public partial class CopyPortfolioUi : Window
    {
        private PortfolioToCopy _portfolioToCopy;

        public string UniqueName;

        public CopyPortfolioUi(PortfolioToCopy portfolioToCopy)
        {
            InitializeComponent();

            _portfolioToCopy = portfolioToCopy;
            UniqueName = _portfolioToCopy.UniqueName;

            // localization

            LabelIsOn.Content = OsLocalization.Market.Label182;
            LabelCopyType.Content = OsLocalization.Market.Label216;
            LabelOrderType.Content = OsLocalization.Market.Label217;
            LabelIcebergCount.Content = OsLocalization.Market.Label218;
            LabelVolumeType.Content = OsLocalization.Market.Label212;
            LabelVolumeMult.Content = OsLocalization.Market.Label213;
            LabelMasterAsset.Content = OsLocalization.Market.Label214;
            LabelSlaveAsset.Content = OsLocalization.Market.Label215;

            LabelSecuritiesGrid.Content = OsLocalization.Market.Label210;
            LabelJournalGrid.Content = OsLocalization.Market.Label211;

            Title = OsLocalization.Market.Label201 
                + " " + OsLocalization.Market.Label219 +": " + portfolioToCopy.ServerName
                + " " + OsLocalization.Market.Label140 + ": " + portfolioToCopy.PortfolioName;



        }

        


    }
}
