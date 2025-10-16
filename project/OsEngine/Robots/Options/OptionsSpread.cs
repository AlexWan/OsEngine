using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OsEngine.Robots.Options
{
    [Bot("OptionsSpread")]
    public class OptionsSpread : BotPanel
    {
        #region Constructor

        private BotTabOptions _tab;
        private StrategyParameterCheckBox _setBoxFirstOption;
        private StrategyParameterCheckBox _setBoxSecondOption;
        private StrategyParameterString _setTypeFirstOption;
        private StrategyParameterString _setTypeSecondOption;
        private StrategyParameterString _setSideFirstOption;
        private StrategyParameterString _setSideSecondOption;
        private StrategyParameterInt _setStrikeFirstOption;
        private StrategyParameterInt _setStrikeSecondOption;
        private StrategyParameterDecimal _setVolumeFirstOption;
        private StrategyParameterDecimal _setVolumeSecondOption;
        private StrategyParameterButton _setButtonAssembly;
        private StrategyParameterButton _setButtonDisassembly;
        private BotTabSimple _tabFirstOption;
        private BotTabSimple _tabSecondOption;

        public OptionsSpread(string name, StartProgram startProgram) : base(name, startProgram)
        {
            _tab = (BotTabOptions)TabCreate(BotTabType.Options);

            this.ParamGuiSettings.Title = "Options Spread";
            this.ParamGuiSettings.Height = 400;
            this.ParamGuiSettings.Width = 400;

            string tabName = " Parameters ";

            _setBoxFirstOption = CreateParameterCheckBox("On/Off First Option", false, tabName);
            _setTypeFirstOption = CreateParameter("Type Option First Option", OptionType.Call.ToString(), new string[] { OptionType.Call.ToString(), OptionType.Put.ToString() }, tabName);
            _setSideFirstOption = CreateParameter("Side Option First Option", Side.Buy.ToString(), new string[] { Side.Buy.ToString(), Side.Sell.ToString() }, tabName);
            _setStrikeFirstOption = CreateParameter("Step from the central strike First Option", 0, 0, 0, 0, tabName);
            _setVolumeFirstOption = CreateParameter("Volume First Option", 0m, 0m, 0m, 0m, tabName);

            _setBoxSecondOption = CreateParameterCheckBox("On/Off Second Option", false, tabName);
            _setTypeSecondOption = CreateParameter("Type Option Second Option", OptionType.Call.ToString(), new string[] { OptionType.Call.ToString(), OptionType.Put.ToString() }, tabName);
            _setSideSecondOption = CreateParameter("Side Option Second Option", Side.Buy.ToString(), new string[] { Side.Buy.ToString(), Side.Sell.ToString() }, tabName);
            _setStrikeSecondOption = CreateParameter("Step from the central strike Second Option", 0, 0, 0, 0, tabName);
            _setVolumeSecondOption = CreateParameter("Volume Second Option", 0m, 0m, 0m, 0m, tabName);

            _setButtonAssembly = CreateParameterButton("Assemble the construct", tabName);
            _setButtonDisassembly = CreateParameterButton("Disassemble all construct", tabName);

            _setButtonAssembly.UserClickOnButtonEvent += _setButton_UserClickOnButtonEvent;
            _setButtonDisassembly.UserClickOnButtonEvent += _setButtonDisassembly_UserClickOnButtonEvent;
        }
               
        private void _setButton_UserClickOnButtonEvent()
        {
            TradeLogic();
        }

        private void _setButtonDisassembly_UserClickOnButtonEvent()
        {
            DisassemblyLogic();
        }

        #endregion

        private void TradeLogic()
        {
            _tabFirstOption = GetTabsForTrade(_setTypeFirstOption, _setStrikeFirstOption);
            _tabSecondOption = GetTabsForTrade(_setTypeSecondOption, _setStrikeSecondOption);

            if (!CheckSettings()) return;

            if (_setBoxFirstOption)
            {
                if (_setSideFirstOption == "Buy")
                {
                    _tabFirstOption.BuyAtMarket(_setVolumeFirstOption);
                }
                else
                {
                    _tabFirstOption.SellAtMarket(_setVolumeFirstOption);
                }
            }

            if (_setBoxSecondOption)
            {
                if (_setSideSecondOption == "Buy")
                {
                    _tabSecondOption.BuyAtMarket(_setVolumeSecondOption);
                }
                else
                {
                    _tabSecondOption.SellAtMarket(_setVolumeSecondOption);
                }
            }

            SendNewLogMessage("The construct has been assembled.", Logging.LogMessageType.Error);
        }

        private bool CheckSettings()
        {
            if (!_setBoxFirstOption && !_setBoxSecondOption)
            {
                SendNewLogMessage("No options selected.", Logging.LogMessageType.Error);
                return false;
            }

            if (_setBoxFirstOption && _tabFirstOption == null)
            {
                SendNewLogMessage("Failed to set the Strike of the First option.", Logging.LogMessageType.Error);
                return false;
            }

            if (_setBoxSecondOption && _tabSecondOption == null)
            {
                SendNewLogMessage("Failed to set the Strike of the First option.", Logging.LogMessageType.Error);
                return false;
            }

            if (_setBoxFirstOption && _setVolumeFirstOption == 0)
            {
                SendNewLogMessage("The volume of the first option is not set.", Logging.LogMessageType.Error);
                return false;
            }

            if (_setBoxSecondOption && _setVolumeSecondOption == 0)
            {
                SendNewLogMessage("The volume of the second option is not set.", Logging.LogMessageType.Error);
                return false;
            }

            return true;
        }

        private BotTabSimple GetTabsForTrade(string type, int stepFromStrike)
        {
            OptionType optionType = OptionType.Call;

            if (type == OptionType.Put.ToString())
            {
                optionType = OptionType.Put;
            }

            if (_tab.UnderlyingAssets.Count == 0) return null;
                        
            string asset = _tab.UnderlyingAssets[0];
            BotTabSimple tabAsset = _tab.GetUnderlyingAssetTab(asset);

            if (tabAsset == null) return null;
            if (tabAsset.Security == null) return null;

            DateTime expiration = tabAsset.Security.Expiration;
            double centralStrike = _tab.GetAtmStrike(asset, expiration);

            double strike = GetStrikeFromAtmStrike(asset, expiration, optionType, centralStrike, stepFromStrike);

            return _tab.GetOptionTab(asset, optionType, strike, expiration);
        }

        private double GetStrikeFromAtmStrike(string asset, DateTime expiration, OptionType type, double centralStrike, int countStrike)
        {
            try
            {
                List<BotTabSimple> sortedTabs = _tab.Tabs?
                    .Where(x => 
                        x.Security?.UnderlyingAsset == asset &&
                        x.Security?.Expiration == expiration &&
                        x.Security?.OptionType == type)
                    .Select(x => x)
                    .OrderBy(x => x.Security?.Strike)
                    .ToList();

                for (int i = 0; i < sortedTabs.Count; i++)
                {                    
                    if (sortedTabs[i].Security.Strike == (decimal)centralStrike)
                    {
                        if (i + countStrike >= sortedTabs.Count) return 0;
                        if (i - countStrike < 0) return 0;

                        return (double)sortedTabs[i + countStrike].Security.Strike;
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                return 0;
            }
        }

        private void DisassemblyLogic()
        {
            for (int i = 0; i < _tab.Tabs.Count; i++)
            {
                if (_tab.Tabs[i].PositionsOpenAll.Count == 0) continue;

                for (int j = 0; j < _tab.Tabs[i].PositionsOpenAll.Count; j++)
                {
                    Position position = _tab.Tabs[i].PositionsOpenAll[j];
                    
                    _tab.Tabs[i].CloseAtMarket(position, position.OpenVolume);
                }                
            }

            SendNewLogMessage("The construct has been disassembled.", Logging.LogMessageType.Error);
        }
    }
}
