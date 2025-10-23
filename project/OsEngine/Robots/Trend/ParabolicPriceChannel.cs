/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

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

/* Description
Trading robot for osengine.

Trend strategy on Parabolic PriceChannel. 

Buy: if the current closing price is below the upper level of the PriceChannel.  

Sell: if the current closing price is above the lower level of the PriceChannel. 

Exit: by trailing stop.
*/

[Bot("ParabolicPriceChannel")] //We create an attribute so that we don't write anything in the Boot factory
public class ParabolicPriceChannel : BotPanel
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

    // Indicator SmaFilter settings
    private StrategyParameterBool _smaPositionFilterIsOn;
    private StrategyParameterInt _smaLengthFilter;
    private StrategyParameterBool _smaSlopeFilterIsOn;

    // Indicator ParabolicPriceChannel settings
    private StrategyParameterInt _periodUpPPC;
    private StrategyParameterInt _periodDownPPC;

    // Volatility settings
    private StrategyParameterString _periodVolatility;
    private StrategyParameterInt _averagingPeriod;
    private StrategyParameterDecimal _multiVol;
    
    // Indicator
    private Aindicator _parabolicPC;
    private Aindicator _smaFilter;

    // The last value of the indicator
    private decimal _lastPriceClose;
    private decimal _lastPriceChannelUp;
    private decimal _lastPriceChannelDown;
    private decimal _lastParabolicStop;

    public ParabolicPriceChannel(string name, StartProgram startProgram) : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        // Basic settings
        _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
        _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
        _timeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
        _timeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

        // GetVolume settings
        _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Base");
        _volume = CreateParameter("Volume", 20, 1.0m, 50, 4, "Base");
        _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Base");

        // Indicator ParabolicPriceChannel settings
        _periodUpPPC = CreateParameter("Period Up line Parabolic Price Channel", 50, 50, 200, 25, "Indicators settings");
        _periodDownPPC = CreateParameter("Period Down Parabolic Price Channel", 50, 50, 200, 25, "Indicators settings");

        // Volatility settings
        _periodVolatility = CreateParameter("Volatility calculation period", "Day", new[] { "Day", "Week", "Month" }, "Indicators settings");
        _averagingPeriod = CreateParameter("Averaging Period Volatility", 15, 50, 200, 5, "Indicators settings");
        _multiVol = CreateParameter("Volatility multi", 0.2m, 3, 5, 1, "Indicators settings");

        // Create indicator ParabolicPriceChannel
        _parabolicPC = IndicatorsFactory.CreateIndicatorByName(nameClass: "ParabolicPriceChannel_indicator", name: name + "ParabolicPC", canDelete: false);
        _parabolicPC = (Aindicator)_tab.CreateCandleIndicator(_parabolicPC, nameArea: "Prime");
        _parabolicPC.ParametersDigit[0].Value = _periodUpPPC.ValueInt;
        _parabolicPC.ParametersDigit[1].Value = _periodDownPPC.ValueInt;
        ((IndicatorParameterString)_parabolicPC.Parameters[2]).ValueString = _periodVolatility.ValueString;
        _parabolicPC.ParametersDigit[2].Value = _averagingPeriod.ValueInt;
        _parabolicPC.ParametersDigit[3].Value = _multiVol.ValueDecimal;
        _parabolicPC.Save();

        // Indicator SmaFilter settings
        _smaLengthFilter = CreateParameter("Sma Length Filter", 100, 10, 500, 1, "Filter parameters");
        _smaPositionFilterIsOn = CreateParameter("Is SMA Filter On", false, "Filter parameters");
        _smaSlopeFilterIsOn = CreateParameter("Is Sma Slope Filter On", false, "Filter parameters");

        // Create indicator SmaFilter
        _smaFilter = IndicatorsFactory.CreateIndicatorByName(nameClass: "Sma", name: name + "Sma_Filter", canDelete: false);
        _smaFilter = (Aindicator)_tab.CreateCandleIndicator(_smaFilter, nameArea: "Prime");
        _smaFilter.DataSeries[0].Color = System.Drawing.Color.Azure;
        _smaFilter.ParametersDigit[0].Value = _smaLengthFilter.ValueInt;
        _smaFilter.Save();

        StopOrActivateIndicators();

        // Subscribe to the indicator update event
        ParametrsChangeByUser += Bot_ParametrsChangeByUser;

        // Subscribe to the candle completion event
        _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

        Bot_ParametrsChangeByUser();

        Description = OsLocalization.Description.DescriptionLabel115;
    }

    private void Bot_ParametrsChangeByUser()
    {
        StopOrActivateIndicators();

        if (_parabolicPC.ParametersDigit[0].Value != _periodUpPPC.ValueInt ||
        _parabolicPC.ParametersDigit[1].Value != _periodDownPPC.ValueInt ||
        ((IndicatorParameterString)_parabolicPC.Parameters[2]).ValueString != _periodVolatility.ValueString ||
        _parabolicPC.ParametersDigit[2].Value != _averagingPeriod.ValueInt ||
        _parabolicPC.ParametersDigit[3].Value != _multiVol.ValueDecimal)
        {
            _parabolicPC.ParametersDigit[0].Value = _periodUpPPC.ValueInt;
            _parabolicPC.ParametersDigit[1].Value = _periodDownPPC.ValueInt;
            ((IndicatorParameterString)_parabolicPC.Parameters[2]).ValueString = _periodVolatility.ValueString;
            _parabolicPC.ParametersDigit[2].Value = _averagingPeriod.ValueInt;
            _parabolicPC.ParametersDigit[3].Value = _multiVol.ValueDecimal;
            _parabolicPC.Reload();
            _parabolicPC.Save();
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
        return "ParabolicPriceChannel";
    }

    // Show settings GUI
    public override void ShowIndividualSettingsDialog()
    {

    }

    // Logic
    private void _tab_CandleFinishedEvent(List<Candle> candles)
    {
        if (_timeStart.Value > _tab.TimeServerCurrent ||
          _timeEnd.Value < _tab.TimeServerCurrent)
        {
            return;
        }

        if (_smaLengthFilter.ValueInt >= candles.Count || _periodUpPPC.ValueInt >= candles.Count || _periodDownPPC.ValueInt >= candles.Count)
        {
            return;
        }

        if (!GetValueVariables(candles))
        {
            return;
        }

        List<Position> positions = _tab.PositionsOpenAll;

        for (int i = 0; i < positions.Count; i++)
        {
            ClosePosition(candles, positions[i]);
        }

        if (positions == null || positions.Count == 0)
        {
            OpenPosotion(candles);
        }
    }

    // Get last value indicator and price
    private bool GetValueVariables(List<Candle> candles)
    {
        _lastPriceClose = candles[candles.Count - 1].Close;
        _lastPriceChannelUp = _parabolicPC.DataSeries[0].Last;
        _lastPriceChannelDown = _parabolicPC.DataSeries[1].Last;
        _lastParabolicStop = _parabolicPC.DataSeries[2].Last;

        if (_lastPriceClose == 0 || _lastPriceChannelUp == 0
           || _lastPriceChannelDown == 0 || _lastParabolicStop == 0)
        {
            return false;
        }
        return true;
    }

    // Opening position logic
    private void OpenPosotion(List<Candle> candles)
    {
        if (_lastPriceChannelUp <= _lastParabolicStop || _lastPriceChannelDown >= _lastParabolicStop)
        {
            return;
        }

        decimal slippage = 0;

        if (_lastPriceClose <= _lastPriceChannelUp)
        {
            slippage = _slippage.ValueDecimal * _lastPriceChannelUp / 100;

            if (BuySignalIsFiltered(candles) == false)
            {
                _tab.BuyAtStop(GetVolume(_tab), _lastPriceChannelUp, _lastPriceChannelUp + slippage, StopActivateType.HigherOrEqual, 1);
            }
        }

        if (_lastPriceClose >= _lastPriceChannelDown)
        {
            slippage = _slippage.ValueDecimal * _lastPriceChannelDown / 100;

            if (SellSignalIsFiltered(candles) == false)
            {
                _tab.SellAtStop(GetVolume(_tab), _lastPriceChannelDown, _lastPriceChannelDown - slippage, StopActivateType.LowerOrEqual, 1);
            }
        }

        if (BuySignalIsFiltered(candles))
        {
            _tab.BuyAtStopCancel();
        }

        if (SellSignalIsFiltered(candles))
        {
            _tab.SellAtStopCancel();
        }
    }

    // Close position logic
    private void ClosePosition(List<Candle> candles, Position position)
    {
        List<Position> positions = _tab.PositionsOpenAll;

        if (positions == null || positions.Count == 0)
        {
            return;
        }

        if (position.State == PositionStateType.Open ||
            position.State == PositionStateType.ClosingFail)
        {
            if (position.CloseActive == true ||
                (position.CloseOrders != null && position.CloseOrders.Count > 0))
            {
                return;
            }

            decimal slippage = _slippage.ValueDecimal * _lastPriceClose / 100;

            if (position.Direction == Side.Buy)
            {
                _tab.CloseAtTrailingStop(position, _lastParabolicStop, _lastParabolicStop - slippage);
            }

            if (position.Direction == Side.Sell)
            {

                _tab.CloseAtTrailingStop(position, _lastParabolicStop, _lastParabolicStop + slippage);
            }
        }
    }

    #region GetVolume()

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

    #endregion

    // Filter buy signal
    private bool BuySignalIsFiltered(List<Candle> candles)
    {
        decimal lastSma = _smaFilter.DataSeries[0].Last;
        decimal _lastPrice = candles[candles.Count - 1].Close;

        // if regime off, return true
        if (_regime.ValueString == "Off" ||
            _regime.ValueString == "OnlyShort" ||
            _regime.ValueString == "OnlyClosePosition")
        {
            return true;
        }

        if (_smaPositionFilterIsOn.ValueBool)
        {
            // if the price is lower than the last sma - return to the top true

            if (_lastPrice < lastSma)
            {
                return true;
            }

        }

        if (_smaSlopeFilterIsOn.ValueBool)
        {
            // if the last sma is lower than the previous sma - return to the top true
            decimal previousSma = _smaFilter.DataSeries[0].Values[_smaFilter.DataSeries[0].Values.Count - 2];

            if (lastSma < previousSma)
            {
                return true;
            }
        }
        return false;
    }

    // Filter sell signal
    private bool SellSignalIsFiltered(List<Candle> candles)
    {
        decimal _lastPrice = candles[candles.Count - 1].Close;
        decimal lastSma = _smaFilter.DataSeries[0].Last;

        // if regime off, return true
        if (_regime.ValueString == "Off" ||
            _regime.ValueString == "OnlyLong" ||
            _regime.ValueString == "OnlyClosePosition")
        {
            return true;
        }

        if (_smaPositionFilterIsOn.ValueBool)
        {
            // if the price is higher than the last sma - return to the top true

            if (_lastPrice > lastSma)
            {
                return true;
            }

        }

        if (_smaSlopeFilterIsOn.ValueBool)
        {
            // if the last sma is higher than the previous sma - return to the top true
            decimal previousSma = _smaFilter.DataSeries[0].Values[_smaFilter.DataSeries[0].Values.Count - 2];

            if (lastSma > previousSma)
            {
                return true;
            }

        }
        return false;
    }
}