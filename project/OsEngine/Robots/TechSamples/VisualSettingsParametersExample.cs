/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.IO;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.Language;

/* Description
TechSample robot for OsEngine

This is an example of working with settings for visual design of Parameters window
 */

namespace OsEngine.Robots.TechSamples
{
    [Bot("VisualSettingsParametersExample")] // We create an attribute so that we don't write anything to the BotFactory
    public class VisualSettingsParametersExample : BotPanel
    {   
        private BotTabSimple _tab;
        private DateTime _timeLastUpdParameters;

        // Parameters:
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _volumeLong;
        private StrategyParameterDecimal _stopLong;
        private StrategyParameterDecimal _takeLong;
        private StrategyParameterDecimal _volumeShort;
        private StrategyParameterDecimal _stopShort;
        private StrategyParameterDecimal _takeShort;
        private StrategyParameterString _weightBidAsk;

        public VisualSettingsParametersExample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create tab and subscribe to MarketDepth update:
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;

            // Parameters:
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort" });
            _volumeLong = CreateParameter("VolumeLong", 100m, 10m, 1000m, 1m);
            _stopLong = CreateParameter("StopLong", 10m, 10m, 1000m, 1m);
            _takeLong = CreateParameter("TakeLong", 50m, 10m, 1000m, 1m);
            _volumeShort = CreateParameter("VolumeShort", 100m, 10m, 1000m, 1m);
            _stopShort = CreateParameter("StopShort", 10m, 10m, 1000m, 1m);
            _takeShort = CreateParameter("TakeShort", 50m, 10m, 1000m, 1m);
            _weightBidAsk = CreateParameter("WeightBidAsk", "");

            // Setting colors Parameters for Long:
            this.ParamGuiSettings.SetForeColorParameter("VolumeLong", System.Drawing.Color.Green);
            this.ParamGuiSettings.SetForeColorParameter("StopLong", System.Drawing.Color.Green);
            this.ParamGuiSettings.SetForeColorParameter("TakeLong", System.Drawing.Color.Green);
            this.ParamGuiSettings.SetSelectionColorParameter("VolumeLong", System.Drawing.Color.LightGreen);
            this.ParamGuiSettings.SetSelectionColorParameter("StopLong", System.Drawing.Color.LightGreen);
            this.ParamGuiSettings.SetSelectionColorParameter("TakeLong", System.Drawing.Color.LightGreen);

            // Setting colors Parameters for Short:
            this.ParamGuiSettings.SetForeColorParameter("VolumeShort", System.Drawing.Color.DarkRed);
            this.ParamGuiSettings.SetForeColorParameter("StopShort", System.Drawing.Color.DarkRed);
            this.ParamGuiSettings.SetForeColorParameter("TakeShort", System.Drawing.Color.DarkRed);
            this.ParamGuiSettings.SetSelectionColorParameter("VolumeShort", System.Drawing.Color.Red);
            this.ParamGuiSettings.SetSelectionColorParameter("StopShort", System.Drawing.Color.Red);
            this.ParamGuiSettings.SetSelectionColorParameter("TakeShort", System.Drawing.Color.Red);

            // Set separators between Parameters:
            this.ParamGuiSettings.SetBorderUnderParameter("Regime", System.Drawing.Color.LightGray, 1);
            this.ParamGuiSettings.SetBorderUnderParameter("TakeLong", System.Drawing.Color.LightGray, 1);
            this.ParamGuiSettings.SetBorderUnderParameter("TakeShort", System.Drawing.Color.LightGray, 1);

            DeleteEvent += Strategy_DeleteEvent;

            Description = OsLocalization.Description.DescriptionLabel110;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "VisualSettingsParametersExample";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Delete bot event
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        private void _tab_MarketDepthUpdateEvent(MarketDepth marketDepth)
        {
            if (marketDepth.Asks == null || marketDepth.Asks.Count == 0 || marketDepth.Bids == null || marketDepth.Bids.Count == 0)
            {
                return;
            }

            MarketDepth depth = marketDepth.GetCopy();

            // If 100 ms have not passed - return
            if (depth.Time < _timeLastUpdParameters.AddMilliseconds(100))
            {
                return;          
            }

            //  Calculate sum volume in Bids and Asks:
            decimal sumBidsVolume = 0m;
            decimal sumAsksVolume = 0m;

            for (int i = 0; i < depth.Bids.Count; i++)
            {
                sumBidsVolume += depth.Bids[i].Bid.ToDecimal();
            }

            for (int i = 0; i < depth.Asks.Count; i++)
            {
                sumAsksVolume += depth.Asks[i].Ask.ToDecimal();
            }

            // Calculate weight and color it in desired color:
            if (sumBidsVolume > sumAsksVolume)
            {
                decimal weightBids = Math.Round(sumBidsVolume / (sumBidsVolume + sumAsksVolume) * 100m, 1);
                _weightBidAsk.ValueString = weightBids.ToString() + "%";

                this.ParamGuiSettings.SetForeColorParameter("WeightBidAsk", System.Drawing.Color.Green);
                this.ParamGuiSettings.RePaintParameterTables();
            }
            else if (sumBidsVolume < sumAsksVolume)
            {
                decimal weightAsks = Math.Round(sumAsksVolume / (sumBidsVolume + sumAsksVolume) * 100m, 1);
                _weightBidAsk.ValueString = weightAsks.ToString() + "%";

                this.ParamGuiSettings.SetForeColorParameter("WeightBidAsk", System.Drawing.Color.Red);
                this.ParamGuiSettings.RePaintParameterTables();
            }
            else
            {
                _weightBidAsk.ValueString = "50/50";

                this.ParamGuiSettings.SetForeColorParameter("WeightBidAsk", System.Drawing.Color.Yellow);
                this.ParamGuiSettings.RePaintParameterTables();
            }

            _timeLastUpdParameters = depth.Time;     // fixing update time
        }
    }
}