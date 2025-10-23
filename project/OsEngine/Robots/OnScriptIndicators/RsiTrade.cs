/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.CandleChart.Elements;
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
using System.IO;

/* Description
trading robot for osengine

RSI's concurrent overbought and oversold strategy.

Logic: if RsiSecond <= DownLine - close position and open Long. 

Logic: if RsiSecond >= UpLine - close position and open Short.
 */

[Bot("RsiTrade")]
public class RsiTrade : BotPanel
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

    // Indicator setting
    private StrategyParameterInt _rsiLength;

    // Indicator area line
    private LineHorisontal _upline;
    private LineHorisontal _downline;
    
    // Indicator
    private Aindicator _rsi;
    
    // The last value of the indicator and price
    private decimal _lastPrice;
    private decimal _firstRsi;
    private decimal _secondRsi;

    public RsiTrade(string name, StartProgram startProgram) : base(name, startProgram)
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

        // Line settings
        _upLineValue = CreateParameter("Up Line Value", 65, 60.0m, 90, 0.5m);
        _downLineValue = CreateParameter("Down Line Value", 35, 10.0m, 40, 0.5m);

        // Indicator setting
        _rsiLength = CreateParameter("Rsi Length", 20, 10, 40, 2);

        // Create indicator Rsi
        _rsi = IndicatorsFactory.CreateIndicatorByName("RSI", name + "RSI", false);
        _rsi = (Aindicator)_tab.CreateCandleIndicator(_rsi, "RsiArea");
        _rsi.ParametersDigit[0].Value = _rsiLength.ValueInt;
        _rsi.Save();

        // Create UpLine 
        _upline = new LineHorisontal("upline", "RsiArea", false)
        {
            Color = Color.Green,
            Value = 0,
        };
        _tab.SetChartElement(_upline);
        _upline.Value = _upLineValue.ValueDecimal;
        _upline.TimeEnd = DateTime.Now;

        // Create DownLine
        _downline = new LineHorisontal("downline", "RsiArea", false)
        {
            Color = Color.Yellow,
            Value = 0
        };
        _tab.SetChartElement(_downline);
        _downline.Value = _downLineValue.ValueDecimal;
        _downline.TimeEnd = DateTime.Now;

        // Subscribe to the candle finished event
        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

        // Subscribe to the delete event
        DeleteEvent += Strategy_DeleteEvent;

        // Subscribe to the indicator update event
        ParametrsChangeByUser += Event_ParametrsChangeByUser;

        Description = OsLocalization.Description.DescriptionLabel65;
    }

    void Event_ParametrsChangeByUser()
    {
        if (_rsi.ParametersDigit[0].Value != _rsiLength.ValueInt)
        {
            _rsi.ParametersDigit[0].Value = _rsiLength.ValueInt;
            _rsi.Reload();
        }

        _upline.Value = _upLineValue.ValueDecimal;
        _upline.Refresh();
        _downline.Value = _downLineValue.ValueDecimal;
        _downline.Refresh();
    }

    // The name of the robot in OsEngine
    public override string GetNameStrategyType()
    {
        return "RsiTrade";
    }

    // Show settings GUI
    public override void ShowIndividualSettingsDialog()
    {

    }

    // Delete save file
    void Strategy_DeleteEvent()
    {
        if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
        {
            File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
        }
    }

    // Logic
    private void Strateg_CandleFinishedEvent(List<Candle> candles)
    {
        if (_regime.ValueString == "Off")
        {
            return;
        }

        if (_rsi.DataSeries[0].Values == null)
        {
            return;
        }

        if (_rsi.DataSeries[0].Values.Count < _rsi.ParametersDigit[0].Value + 5)
        {
            return;

        }

        _lastPrice = candles[candles.Count - 1].Close;
        _firstRsi = _rsi.DataSeries[0].Values[_rsi.DataSeries[0].Values.Count - 1];
        _secondRsi = _rsi.DataSeries[0].Values[_rsi.DataSeries[0].Values.Count - 2];

        List<Position> openPositions = _tab.PositionsOpenAll;

        if (openPositions != null && openPositions.Count != 0)
        {
            for (int i = 0; i < openPositions.Count; i++)
            {
                LogicClosePosition(candles, openPositions[i]);

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

    // Logic open first position
    private void LogicOpenPosition(List<Candle> candles, List<Position> position)
    {
        if (_secondRsi < _downline.Value && _firstRsi > _downline.Value
                                            && _regime.ValueString != "OnlyShort")
        {
            _tab.BuyAtLimit(GetVolume(_tab),
                _lastPrice + _slippage.ValueInt * _tab.Security.PriceStep);
        }

        if (_secondRsi > _upline.Value && _firstRsi < _upline.Value
                                          && _regime.ValueString != "OnlyLong")
        {
            _tab.SellAtLimit(GetVolume(_tab),
                _lastPrice - _slippage.ValueInt * _tab.Security.PriceStep);
        }
    }

    // Logic close position
    private void LogicClosePosition(List<Candle> candles, Position position)
    {
        if (position.State == PositionStateType.Closing)
        {
            return;
        }

        if (position.Direction == Side.Buy)
        {
            if (_secondRsi >= _upline.Value && _firstRsi <= _upline.Value)
            {
                _tab.CloseAtLimit(position,
                    _lastPrice - _slippage.ValueInt * _tab.Security.PriceStep, position.OpenVolume);

                if (_regime.ValueString != "OnlyLong" && _regime.ValueString != "OnlyClosePosition")
                {
                    _tab.SellAtLimit(GetVolume(_tab),
                        _lastPrice - _slippage.ValueInt * _tab.Security.PriceStep);
                }
            }
        }

        if (position.Direction == Side.Sell)
        {
            if (_secondRsi <= _downline.Value && _firstRsi >= _downline.Value)
            {
                _tab.CloseAtLimit(position,
                    _lastPrice + _slippage.ValueInt * _tab.Security.PriceStep, position.OpenVolume);

                if (_regime.ValueString != "OnlyShort" && _regime.ValueString != "OnlyClosePosition")
                {
                    _tab.BuyAtLimit(GetVolume(_tab),
                        _lastPrice + _slippage.ValueInt * _tab.Security.PriceStep);
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