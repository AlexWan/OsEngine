/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;

/* Description
trading robot for osengine

Counter Trend Strategy Based on CCI Indicator. Max - 3 poses.

Buy: CCI is less than DownLine. 

Sell: CCI more UpLine.

Exit: on the return signal.
 */

[Bot("CciTrade")] // We create an attribute so that we don't write anything to the BotFactory
public class CciTrade : BotPanel
{
    private BotTabSimple _tab;

    // Basic settings
    private StrategyParameterString _regime;
    private StrategyParameterInt _slippage;

    // GetVolume settings
    private StrategyParameterString _volumeType;
    private StrategyParameterDecimal _volume;
    private StrategyParameterString _tradeAssetInPortfolio;

    // Line settings
    private StrategyParameterDecimal _upLineValue;
    private StrategyParameterDecimal _downLineValue;

    // Indicator settings
    private StrategyParameterInt _cciLength;

    // Indicator area line
    private LineHorisontal _upline;
    private LineHorisontal _downline;
    
    // Indicator
    private Aindicator _cci;

    // The last value of the indicator and price
    private decimal _lastPrice;
    private decimal _lastCci;

    public CciTrade(string name, StartProgram startProgram) : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        // Basic settings
        _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
        _slippage = CreateParameter("Slippage", 0, 0, 20, 1);

        // GetVolume settings
        _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
        _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
        _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

        // Indicator settings
        _cciLength = CreateParameter("Cci Length", 20, 10, 40, 2);

        // Line settings
        _upLineValue = CreateParameter("Up Line Value", 150, 50.0m, 300, 20m);
        _downLineValue = CreateParameter("Down Line Value", -150, -300.0m, -50, 20);

        // Create indicator CCI
        _cci = IndicatorsFactory.CreateIndicatorByName("CCI",name + "Cci", false);
        _cci.ParametersDigit[0].Value = _cciLength.ValueInt;
        _cci = (Aindicator)_tab.CreateCandleIndicator(_cci, "CciArea");
        _cci.Save();

        // Create upLine
        _upline = new LineHorisontal("upline", "CciArea", false)
        {
            Color = Color.Green,
            Value = 0,

        };
        _tab.SetChartElement(_upline);
        _upline.Value = _upLineValue.ValueDecimal;

        // Create downLine
        _downline = new LineHorisontal("downline", "CciArea", false)
        {
            Color = Color.Yellow,
            Value = 0

        };
        _tab.SetChartElement(_downline); 
        _downline.Value = _downLineValue.ValueDecimal;

        // Subscribe to the candle finished event
        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

        // Subscribe to the indicator update event
        ParametrsChangeByUser += Event_ParametrsChangeByUser;

        Description = OsLocalization.Description.DescriptionLabel57;
    }

    void Event_ParametrsChangeByUser()
    {
        if (_cci.ParametersDigit[0].Value != _cciLength.ValueInt)
        {
            _cci.ParametersDigit[0].Value = _cciLength.ValueInt;
            _cci.Reload();
        }

        _upline.Value = _upLineValue.ValueDecimal;
        _upline.Refresh();
        _downline.Value = _downLineValue.ValueDecimal;
        _downline.Refresh();
    }

    // The name of the robot in OsEngine
    public override string GetNameStrategyType()
    {
        return "CciTrade";
    }
    
    // Show settings GUI
    public override void ShowIndividualSettingsDialog()
    {

    }

    // Logic
    private void Strateg_CandleFinishedEvent(List<Candle> candles)
    {
        if (_regime.ValueString == "Off")
        {
            return;
        }

        if (_cci.DataSeries[0].Values == null)
        {
            return;
        }

        _lastPrice = candles[candles.Count - 1].Close;
        _lastCci = _cci.DataSeries[0].Values[_cci.DataSeries[0].Values.Count - 1];

        List<Position> openPositions = _tab.PositionsOpenAll;

        if (openPositions != null && openPositions.Count != 0)
        {
            for (int i = 0; i < openPositions.Count; i++)
            {
                LogicClosePosition(candles, openPositions[i], openPositions);

                _upline.Refresh();
                _downline.Refresh();
            }
        }

        if (_regime.ValueString == "OnlyClosePosition")
        {
            return;
        }

        if (openPositions == null || openPositions.Count == 0)
        {
            LogicOpenPosition(candles, openPositions);
        }
    }

    // Logic open position
    private void LogicOpenPosition(List<Candle> candles, List<Position> position)
    {
        if (_lastCci < _downline.Value
            && _regime.ValueString != "OnlyShort")
        {
            _tab.BuyAtLimit(GetVolume(_tab), _lastPrice + _slippage.ValueInt * _tab.Security.PriceStep);
        }

        if (_lastCci > _upline.Value && _regime.ValueString != "OnlyLong")
        {
            _tab.SellAtLimit(GetVolume(_tab), _lastPrice - _slippage.ValueInt * _tab.Security.PriceStep);
        }
    }

    // Logic close position
    private void LogicClosePosition(List<Candle> candles, Position position, List<Position> allPos)
    {
        if (position.State != PositionStateType.Open)
        {
            return;
        }

        if (position.Direction == Side.Buy)
        {
            if (_lastCci > _upline.Value)
            {
                _tab.CloseAtLimit(position, _lastPrice - _slippage.ValueInt * _tab.Security.PriceStep, position.OpenVolume);

                if (_regime.ValueString != "OnlyLong" &&
                    _regime.ValueString != "OnlyClosePosition" &&
                    allPos.Count < 3)
                {
                    _tab.SellAtLimit(GetVolume(_tab), _lastPrice - _slippage.ValueInt * _tab.Security.PriceStep);
                }
            }
        }

        if (position.Direction == Side.Sell)
        {
            if (_lastCci < _downline.Value)
            {
                _tab.CloseAtLimit(position, _lastPrice + _slippage.ValueInt * _tab.Security.PriceStep, position.OpenVolume);

                if (_regime.ValueString != "OnlyShort" &&
                    _regime.ValueString != "OnlyClosePosition" &&
                    allPos.Count < 3)
                {
                    _tab.BuyAtLimit(GetVolume(_tab), _lastPrice + _slippage.ValueInt * _tab.Security.PriceStep);
                }
            }
        }
    }

    // Method for calculating the volume of entry into a position
    private decimal GetVolume(BotTabSimple tab)
    {
        decimal volume = 0;

        if (_volumeType.ValueString == "Contracts")
        {
            volume = _volume.ValueDecimal;
        }
        else if (_volumeType.ValueString == "Contract currency")
        {
            decimal contractPrice = tab.PriceBestAsk;
            volume = _volume.ValueDecimal / contractPrice;

            if (StartProgram == StartProgram.IsOsTrader)
            {
                IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                if (serverPermission != null &&
                    serverPermission.IsUseLotToCalculateProfit &&
                tab.Security.Lot != 0 &&
                    tab.Security.Lot > 1)
                {
                    volume = _volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                }

                volume = Math.Round(volume, tab.Security.DecimalsVolume);
            }
            else // Tester or Optimizer
            {
                volume = Math.Round(volume, 6);
            }
        }
        else if (_volumeType.ValueString == "Deposit percent")
        {
            Portfolio myPortfolio = tab.Portfolio;

            if (myPortfolio == null)
            {
                return 0;
            }

            decimal portfolioPrimeAsset = 0;

            if (_tradeAssetInPortfolio.ValueString == "Prime")
            {
                portfolioPrimeAsset = myPortfolio.ValueCurrent;
            }
            else
            {
                List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                if (positionOnBoard == null)
                {
                    return 0;
                }

                for (int i = 0; i < positionOnBoard.Count; i++)
                {
                    if (positionOnBoard[i].SecurityNameCode == _tradeAssetInPortfolio.ValueString)
                    {
                        portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                        break;
                    }
                }
            }

            if (portfolioPrimeAsset == 0)
            {
                SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, OsEngine.Logging.LogMessageType.Error);
                return 0;
            }

            decimal moneyOnPosition = portfolioPrimeAsset * (_volume.ValueDecimal / 100);

            decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

            if (tab.StartProgram == StartProgram.IsOsTrader)
            {
                if (tab.Security.UsePriceStepCostToCalculateVolume == true
                  && tab.Security.PriceStep != tab.Security.PriceStepCost
                  && tab.PriceBestAsk != 0
                  && tab.Security.PriceStep != 0
                  && tab.Security.PriceStepCost != 0)
                {// расчёт количества контрактов для фьючерсов и опционов на Мосбирже
                    qty = moneyOnPosition / (tab.PriceBestAsk / tab.Security.PriceStep * tab.Security.PriceStepCost);
                }
                qty = Math.Round(qty, tab.Security.DecimalsVolume);
            }
            else
            {
                qty = Math.Round(qty, 7);
            }

            return qty;
        }

        return volume;
    }
}
