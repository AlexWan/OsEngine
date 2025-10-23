/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Gui;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

/* Description
Trading robot for osengine.

Trend strategy on Break LinearRegression Channel. 

Buy: If the closing price of the last candle is above the upper line of the PriceChannel.  

Sell: If the closing price of the last candle is below the lower line of the PriceChannel. 

Exit:  
1. Place a stop order to sell at the level of the lower line of the PriceChannel.  
2. Place a stop order to sell at the level of the upper line of the PriceChannel.
*/

[Bot("BreakLinearRegressionChannel")] //We create an attribute so that we don't write anything in the Boot factory
public class BreakLinearRegressionChannel : BotPanel
{
    private BotTabSimple _tab;

    // Basic settings
    private StrategyParameterString _regime;
    private StrategyParameterDecimal _slippage;
    private StrategyParameterTimeOfDay _timeStart;
    private StrategyParameterTimeOfDay _timeEnd;

    // GetVolume settings
    private StrategyParameterString _volumeType;
    private StrategyParameterDecimal _volume;
    private StrategyParameterString _tradeAssetInPortfolio;

    // Indicator LinearRegression settings
    private StrategyParameterDecimal _upDeviation;
    private StrategyParameterInt _periodLR;

    // SmaFilter settings
    private StrategyParameterInt _smaLengthFilter;
    private StrategyParameterBool _smaPositionFilterIsOn;
    private StrategyParameterBool _smaSlopeFilterIsOn;
    
    // Indicators
    private Aindicator _linearRegression;
    private Aindicator _smaFilter;

    public BreakLinearRegressionChannel(string name, StartProgram startProgram) : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];
       
        // Basic settings
        _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
        _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
        _timeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
        _timeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");
        
        // GetVolume Settings
        _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Volume");
        _volume = CreateParameter("Volume", 20, 1.0m, 50, 4, "Volume");
        _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Volume");

        // Indicator LinearRegression settings
        _periodLR = CreateParameter("Period Linear Regression", 50, 50, 300, 1, "Robot parameters");
        _upDeviation = CreateParameter("Deviation LR", 1, 0.1m, 3, 0.1m, "Robot parameters");

        // SmaFilter settings
        _smaLengthFilter = CreateParameter("Sma Length Filter", 100, 10, 500, 1, "Filters");
        _smaPositionFilterIsOn = CreateParameter("Is SMA Filter On", false, "Filters");
        _smaSlopeFilterIsOn = CreateParameter("Is Sma Slope Filter On", false, "Filters");

        // Create indicator SmaFilter
        _smaFilter = IndicatorsFactory.CreateIndicatorByName(nameClass: "Sma", name: name + "Sma_Filter", canDelete: false);
        _smaFilter = (Aindicator)_tab.CreateCandleIndicator(_smaFilter, nameArea: "Prime");
        _smaFilter.DataSeries[0].Color = System.Drawing.Color.Azure;
        _smaFilter.ParametersDigit[0].Value = _smaLengthFilter.ValueInt;
        _smaFilter.Save();

        // Create indicator LinearRegressionChannelFast_Indicator
        _linearRegression = IndicatorsFactory.CreateIndicatorByName("LinearRegressionChannelFast_Indicator", name + "LinearRegressionChannel", false);
        _linearRegression = (Aindicator)_tab.CreateCandleIndicator(_linearRegression, "Prime");
        _linearRegression.ParametersDigit[0].Value = _periodLR.ValueInt;
        _linearRegression.ParametersDigit[1].Value = _upDeviation.ValueDecimal;
        _linearRegression.ParametersDigit[2].Value = _upDeviation.ValueDecimal;
        _linearRegression.Save();

        StopOrActivateIndicators();

        // Subscribe to the candle completion event
        _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

        // Subscribe to the indicator update event
        ParametrsChangeByUser += LinearRegressionTraderParam_ParametrsChangeByUser;

        LinearRegressionTraderParam_ParametrsChangeByUser();

        Description = OsLocalization.Description.DescriptionLabel111;
    }

    private void LinearRegressionTraderParam_ParametrsChangeByUser()
    {
        StopOrActivateIndicators();

        if (_linearRegression.ParametersDigit[0].Value != _periodLR.ValueInt ||
        _linearRegression.ParametersDigit[1].Value != _upDeviation.ValueDecimal ||
        _linearRegression.ParametersDigit[2].Value != _upDeviation.ValueDecimal)
        {
            _linearRegression.ParametersDigit[0].Value = _periodLR.ValueInt;
            _linearRegression.ParametersDigit[1].Value = _upDeviation.ValueDecimal;
            _linearRegression.ParametersDigit[2].Value = _upDeviation.ValueDecimal;
            _linearRegression.Save();
            _linearRegression.Reload();
        }

        if (_smaFilter.DataSeries.Count == 0)
        {
            return;
        }

        if (_smaFilter.ParametersDigit[0].Value != _smaLengthFilter.ValueInt)
        {
            _smaFilter.ParametersDigit[0].Value = _smaLengthFilter.ValueInt;
            _smaFilter.Reload();
            _smaFilter.Save();
        }

        if (_smaFilter.DataSeries != null && _smaFilter.DataSeries.Count > 0)
        {
            if (!_smaPositionFilterIsOn.ValueBool)
            {
                _smaFilter.DataSeries[0].IsPaint = false;
            }
            else
            {
                _smaFilter.DataSeries[0].IsPaint = true;
            }
        }
    }

    private void StopOrActivateIndicators()
    {
        if (_smaPositionFilterIsOn.ValueBool == false
                  && _smaSlopeFilterIsOn.ValueBool == false
                  && _smaFilter.IsOn == true)
        {
            _smaFilter.IsOn = false;
            _smaFilter.Reload();
        }
        else if ((_smaPositionFilterIsOn.ValueBool == true
            || _smaSlopeFilterIsOn.ValueBool == true)
            && _smaFilter.IsOn == false)
        {
            _smaFilter.IsOn = true;
            _smaFilter.Reload();
        }
    }

    // The name of the robot in OsEngine
    public override string GetNameStrategyType()
    {
        return "BreakLinearRegressionChannel";
    }

    // Show settings GUI
    public override void ShowIndividualSettingsDialog()
    {

    }

    // Logic
    private void _tab_CandleFinishedEvent(List<Candle> candles)
    {
        // usage
        if (_timeStart.Value > _tab.TimeServerCurrent ||
            _timeEnd.Value < _tab.TimeServerCurrent)
        {
            CancelStopsAndProfits();
            return;
        }

        if (_smaLengthFilter.ValueInt >= candles.Count)
        {
            return;
        }

        if (candles.Count < 20)
        {
            return;
        }

        List<Position> positions = _tab.PositionsOpenAll;

        if (positions.Count == 0)
        {
            TryOpenPosition(candles);
        }
        else
        {
            TryClosePosition(positions[0], candles);
        }
    }

    // Filter buy signal
    private bool BuySignalIsFiltered(List<Candle> candles)
    {
        decimal lastPrice = candles[candles.Count - 1].Close;
        decimal lastSma = _smaFilter.DataSeries[0].Last;

        // filter for buy
        if (_regime.ValueString == "Off" ||
            _regime.ValueString == "OnlyShort" ||
            _regime.ValueString == "OnlyClosePosition")
        {
            return true;
            // if the robot's operating mode does not correspond to the position direction
        }

        if (_smaPositionFilterIsOn.ValueBool)
        {
            if (_smaFilter.DataSeries[0].Last > lastPrice)
            {
                return true;
            }
            // if the price is lower than the last sma - return to the top true
        }

        if (_smaSlopeFilterIsOn.ValueBool)
        {
            decimal prevSma = _smaFilter.DataSeries[0].Values[_smaFilter.DataSeries[0].Values.Count - 2];

            if (lastSma < prevSma)
            {
                return true;
            }
            // if the last sma is lower than the previous sma - return to the top true
        }

        return false;
    }

    // Filter sell signal
    private bool SellSignalIsFiltered(List<Candle> candles)
    {
        decimal lastPrice = candles[candles.Count - 1].Close;
        decimal lastSma = _smaFilter.DataSeries[0].Last;
        // filter for sell
        if (_regime.ValueString == "Off" ||
            _regime.ValueString == "OnlyLong" ||
            _regime.ValueString == "OnlyClosePosition")
        {
            return true;
            // if the robot's operating mode does not correspond to the position direction
        }

        if (_smaPositionFilterIsOn.ValueBool)
        {
            if (lastSma < lastPrice)
            {
                return true;
            }
            // if the price is higher than the last sma - return to the top true
        }

        if (_smaSlopeFilterIsOn.ValueBool)
        {
            decimal prevSma = _smaFilter.DataSeries[0].Values[_smaFilter.DataSeries[0].Values.Count - 2];

            if (lastSma > prevSma)
            {
                return true;
            }
            // if the last sma is higher than the previous sma - return to the top true
        }

        return false;
    }

    // Logic open position
    private void TryOpenPosition(List<Candle> candles)
    {
        decimal upChannel = _linearRegression.DataSeries[0].Values[_linearRegression.DataSeries[0].Values.Count - 1];
        decimal downChannel = _linearRegression.DataSeries[2].Values[_linearRegression.DataSeries[2].Values.Count - 1];

        if (upChannel == 0 ||
            downChannel == 0)
        {
            return;
        }

        bool signalBuy = candles[candles.Count - 1].Close > upChannel;
        bool signalShort = candles[candles.Count - 1].Close < downChannel;

        if (signalBuy) // When receiving a signal to enter a long position
        {
            if(!BuySignalIsFiltered(candles))// if the method returns false you can enter into a deal
                _tab.BuyAtMarket(GetVolume(_tab)); // Buy at market at the opening of the next candle
        }
        else if (signalShort) // When receiving a signal to enter a Short position
        {
            if(!SellSignalIsFiltered(candles))// if the method returns false you can enter into a deal
                _tab.SellAtMarket(GetVolume(_tab)); // Sell at market at the opening of the next candle
        }
    }

    // Logic close position
    private void TryClosePosition(Position position, List<Candle> candles)
    {
        decimal upChannel = _linearRegression.DataSeries[0].Values[_linearRegression.DataSeries[0].Values.Count - 1];
        decimal downChannel = _linearRegression.DataSeries[2].Values[_linearRegression.DataSeries[2].Values.Count - 1];

        if (upChannel == 0 ||
            downChannel == 0)
        {
            return;
        }

        decimal extPrice = 0;

        if (position.Direction == Side.Buy)
        {
            extPrice = downChannel;
            decimal _slippage = this._slippage.ValueDecimal * extPrice / 100;
            _tab.CloseAtStop(position, extPrice, extPrice - _slippage);
        }
        else if (position.Direction == Side.Sell)
        {
            extPrice = upChannel;
            decimal _slippage = this._slippage.ValueDecimal * extPrice / 100;
            _tab.CloseAtStop(position, extPrice, extPrice + _slippage);
        }
    }

    // Logic close position by stop and profit
    private void CancelStopsAndProfits()
    {
        List<Position> positions = _tab.PositionsOpenAll;

        for (int i = 0; i < positions.Count; i++)
        {
            Position pos = positions[i];

            pos.StopOrderIsActive = false;
            pos.ProfitOrderIsActive = false;
        }

        _tab.BuyAtStopCancel();
        _tab.SellAtStopCancel();
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