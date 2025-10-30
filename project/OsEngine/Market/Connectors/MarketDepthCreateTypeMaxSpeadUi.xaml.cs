/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Windows;
using System.Windows.Controls;

namespace OsEngine.Market.Connectors
{
    /// <summary>
    /// Interaction logic for MarketDepthCreateTypeMaxSpeadUi.xaml
    /// </summary>
    public partial class MarketDepthCreateTypeMaxSpreadUi : Window
    {

        private TimeFrameBuilder _timeFrameBuilder;

        private MassSourcesCreator _creator;

        private BotTabScreener _screener;

        public MarketDepthCreateTypeMaxSpreadUi(TimeFrameBuilder timeFrameBuilder)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _timeFrameBuilder = timeFrameBuilder;

            CheckBoxMarketDepthBuildMaxSpreadIsOn.IsChecked = _timeFrameBuilder.MarketDepthBuildMaxSpreadIsOn;
            CheckBoxMarketDepthBuildMaxSpreadIsOn.Checked += CheckBoxMarketDepthBuildMaxSpreadIsOn_Checked;
            CheckBoxMarketDepthBuildMaxSpreadIsOn.Unchecked += CheckBoxMarketDepthBuildMaxSpreadIsOn_Checked;

            TextBoxMarketDepthBuildMaxSpread.Text = _timeFrameBuilder.MarketDepthBuildMaxSpread.ToString();
            TextBoxMarketDepthBuildMaxSpread.TextChanged += TextBoxMarketDepthBuildMaxSpread_TextChanged;

            Title = OsLocalization.Market.Label278;
            CheckBoxMarketDepthBuildMaxSpreadIsOn.Content = OsLocalization.Market.Label279;
            LabelMarketDepthBuildMaxSpread.Content = OsLocalization.Market.Label280;

            this.Closed += MarketDepthCreateTypeMaxSpreadUi_Closed;
        }

        public MarketDepthCreateTypeMaxSpreadUi(BotTabScreener screener)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _screener = screener;

            CheckBoxMarketDepthBuildMaxSpreadIsOn.IsChecked = _screener.MarketDepthBuildMaxSpreadIsOn;
            CheckBoxMarketDepthBuildMaxSpreadIsOn.Checked += CheckBoxMarketDepthBuildMaxSpreadIsOn_Checked;
            CheckBoxMarketDepthBuildMaxSpreadIsOn.Unchecked += CheckBoxMarketDepthBuildMaxSpreadIsOn_Checked;

            TextBoxMarketDepthBuildMaxSpread.Text = _screener.MarketDepthBuildMaxSpread.ToString();
            TextBoxMarketDepthBuildMaxSpread.TextChanged += TextBoxMarketDepthBuildMaxSpread_TextChanged;

            Title = OsLocalization.Market.Label278;
            CheckBoxMarketDepthBuildMaxSpreadIsOn.Content = OsLocalization.Market.Label279;
            LabelMarketDepthBuildMaxSpread.Content = OsLocalization.Market.Label280;

            this.Closed += MarketDepthCreateTypeMaxSpreadUi_Closed;
        }

        public MarketDepthCreateTypeMaxSpreadUi(MassSourcesCreator creator)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _creator = creator;

            CheckBoxMarketDepthBuildMaxSpreadIsOn.IsChecked = creator.MarketDepthBuildMaxSpreadIsOn;
            CheckBoxMarketDepthBuildMaxSpreadIsOn.Checked += CheckBoxMarketDepthBuildMaxSpreadIsOn_Checked;
            CheckBoxMarketDepthBuildMaxSpreadIsOn.Unchecked += CheckBoxMarketDepthBuildMaxSpreadIsOn_Checked;

            TextBoxMarketDepthBuildMaxSpread.Text = creator.MarketDepthBuildMaxSpread.ToString();
            TextBoxMarketDepthBuildMaxSpread.TextChanged += TextBoxMarketDepthBuildMaxSpread_TextChanged;

            Title = OsLocalization.Market.Label278;
            CheckBoxMarketDepthBuildMaxSpreadIsOn.Content = OsLocalization.Market.Label279;
            LabelMarketDepthBuildMaxSpread.Content = OsLocalization.Market.Label280;

            this.Closed += MarketDepthCreateTypeMaxSpreadUi_Closed;
        }

        private void MarketDepthCreateTypeMaxSpreadUi_Closed(object sender, EventArgs e)
        {
            try
            {
                CheckBoxMarketDepthBuildMaxSpreadIsOn.Checked -= CheckBoxMarketDepthBuildMaxSpreadIsOn_Checked;
                CheckBoxMarketDepthBuildMaxSpreadIsOn.Unchecked -= CheckBoxMarketDepthBuildMaxSpreadIsOn_Checked;
                TextBoxMarketDepthBuildMaxSpread.TextChanged -= TextBoxMarketDepthBuildMaxSpread_TextChanged;

                _timeFrameBuilder = null;
                _creator = null;
                _screener = null;
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxMarketDepthBuildMaxSpread_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if(_timeFrameBuilder != null)
                {
                    _timeFrameBuilder.MarketDepthBuildMaxSpread = TextBoxMarketDepthBuildMaxSpread.Text.ToDecimal();
                }

                if(_creator != null)
                {
                    _creator.MarketDepthBuildMaxSpread = TextBoxMarketDepthBuildMaxSpread.Text.ToDecimal();
                }

                if(_screener != null)
                {
                    _screener.MarketDepthBuildMaxSpread = TextBoxMarketDepthBuildMaxSpread.Text.ToDecimal();
                }
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxMarketDepthBuildMaxSpreadIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_timeFrameBuilder != null)
                {
                    _timeFrameBuilder.MarketDepthBuildMaxSpreadIsOn = CheckBoxMarketDepthBuildMaxSpreadIsOn.IsChecked.Value;
                }

                if (_creator != null)
                {
                    _creator.MarketDepthBuildMaxSpreadIsOn = CheckBoxMarketDepthBuildMaxSpreadIsOn.IsChecked.Value;
                }

                if (_screener != null)
                {
                    _screener.MarketDepthBuildMaxSpreadIsOn = CheckBoxMarketDepthBuildMaxSpreadIsOn.IsChecked.Value;
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
