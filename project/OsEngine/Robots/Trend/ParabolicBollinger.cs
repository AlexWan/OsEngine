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
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Robots;
using System;
using System.Collections.Generic;
using System.Drawing;

/* Description
Trading robot for osengine.

Trend strategy on Parabolic and Bollinger. 

Buy: if the closing price is below the Bollinger Upper level.  
Sell: if the closing price is above the Bollinger Lower level.  

Exits are managed through trailing stops that follow the Parabolic Stop level.
*/

[Bot("ParabolicBollinger")] //We create an attribute so that we don't write anything in the Boot factory
public class ParabolicBollinger : BotPanel
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

    // SmaFilter settings
    private StrategyParameterBool _smaPositionFilterIsOn;
    private StrategyParameterInt _smaLengthFilter;
    private StrategyParameterBool _smaSlopeFilterIsOn;

    // Indicator ParabolicBollinger settings
    private StrategyParameterInt _periodBoll;
    private StrategyParameterDecimal _deviationBoll;
    private StrategyParameterString _periodVolatility;
    private StrategyParameterInt _averagingPeriod;
    private StrategyParameterDecimal _multiVol;

    // Label on parameter
    private StrategyParameterLabel _label1;
    private StrategyParameterLabel _label2;
    private StrategyParameterLabel _label3;

    // Indicator
    private Aindicator _smaFilter;
    private Aindicator _parabolicBollinger;

    // The last value of the indicator and price
    private decimal _lastPriceClose;
    private decimal _lastBollingerUp;
    private decimal _lastBollingerDown;
    private decimal _lastParabolicStop;

    public ParabolicBollinger(string name, StartProgram startProgram) : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        // Basic settings
        _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");

        _label2 = CreateParameterLabel("label2", "--------", "--------", 10, 5, Color.White, "Base");
        _timeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
        _timeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

        _label1 = CreateParameterLabel("label1", "--------", "--------", 10, 5, Color.White, "Base");
        _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");

        // GetVolume settings
        _label3 = CreateParameterLabel("label3", "--------", "--------", 10, 5, Color.White, "Base");
        _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Base");
        _volume = CreateParameter("Volume", 20, 1.0m, 50, 4, "Base");
        _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Base");

        // Indicator ParabolicBollinger settings
        _periodBoll = CreateParameter("Period Parabolic Bollinger", 28, 28, 200, 14, "Indicators settings");
        _deviationBoll = CreateParameter("Deviation Parabolic Bollinger", 2, 1.5m, 5, 1, "Indicators settings");

        _periodVolatility = CreateParameter("Volatility calculation period", "Day", new[] { "Day", "Week", "Month" }, "Indicators settings");
        _averagingPeriod = CreateParameter("Averaging Period Volatility", 15, 50, 200, 5, "Indicators settings");
        _multiVol = CreateParameter("Volatility multi", 0.2m, 3, 5, 1, "Indicators settings");

        // Create indicator ParabolicBollinger
        _parabolicBollinger = IndicatorsFactory.CreateIndicatorByName(nameClass: "ParabolicBollinger_indicator", name: name + "ParabolicBollinger_indicator", canDelete: false);
        _parabolicBollinger = (Aindicator)_tab.CreateCandleIndicator(_parabolicBollinger, nameArea: "Prime");
        _parabolicBollinger.ParametersDigit[0].Value = _periodBoll.ValueInt;
        _parabolicBollinger.ParametersDigit[1].Value = _deviationBoll.ValueDecimal;
        ((IndicatorParameterString)_parabolicBollinger.Parameters[2]).ValueString = _periodVolatility.ValueString;
        _parabolicBollinger.ParametersDigit[2].Value = _averagingPeriod.ValueInt;
        _parabolicBollinger.ParametersDigit[3].Value = _multiVol.ValueDecimal;
        _parabolicBollinger.Save();

        // SmaFilter settings
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

        Description = OsLocalization.Description.DescriptionLabel114;
    }

    private void Bot_ParametrsChangeByUser()
    {
        StopOrActivateIndicators();

        if (_parabolicBollinger.ParametersDigit[0].Value != _periodBoll.ValueInt ||
        _parabolicBollinger.ParametersDigit[1].Value != _deviationBoll.ValueDecimal ||
        ((IndicatorParameterString)_parabolicBollinger.Parameters[2]).ValueString != _periodVolatility.ValueString ||
        _parabolicBollinger.ParametersDigit[2].Value != _averagingPeriod.ValueInt ||
        _parabolicBollinger.ParametersDigit[3].Value != _multiVol.ValueDecimal)
        {
            _parabolicBollinger.ParametersDigit[0].Value = _periodBoll.ValueInt;
            _parabolicBollinger.ParametersDigit[1].Value = _deviationBoll.ValueDecimal;
            ((IndicatorParameterString)_parabolicBollinger.Parameters[2]).ValueString = _periodVolatility.ValueString;
            _parabolicBollinger.ParametersDigit[2].Value = _averagingPeriod.ValueInt;
            _parabolicBollinger.ParametersDigit[3].Value = _multiVol.ValueDecimal;
            _parabolicBollinger.Reload();
            _parabolicBollinger.Save();
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
        return "ParabolicBollinger";
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

        if (_smaLengthFilter.ValueInt >= candles.Count || _periodBoll.ValueInt >= candles.Count)
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

    // Get last value of the indicator
    private bool GetValueVariables(List<Candle> candles)
    {
        _lastPriceClose = candles[candles.Count - 1].Close;
        _lastBollingerUp = _parabolicBollinger.DataSeries[0].Last;
        _lastBollingerDown = _parabolicBollinger.DataSeries[1].Last;
        _lastParabolicStop = _parabolicBollinger.DataSeries[2].Last;

        if (_lastPriceClose == 0 || _lastBollingerUp == 0 ||
            _lastBollingerDown == 0 || _lastParabolicStop == 0)
        {
            return false;
        }
        return true;
    }

    // Opening position logic
    private void OpenPosotion(List<Candle> candles)
    {
        if (_lastBollingerUp <= _lastParabolicStop || _lastBollingerDown >= _lastParabolicStop)
        {
            return;
        }

        decimal slippage = 0;

        if (_lastPriceClose < _lastBollingerUp)
        {
            slippage = _slippage.ValueDecimal * _lastBollingerUp / 100;

            if (BuySignalIsFiltered(candles) == false)
            {
                _tab.BuyAtStop(GetVolume(_tab), _lastBollingerUp, _lastBollingerUp + slippage, StopActivateType.HigherOrEqual, 1);
            }          
        }

        if (_lastPriceClose > _lastBollingerDown)
        {
            slippage = _slippage.ValueDecimal * _lastBollingerDown / 100;

            if (SellSignalIsFiltered(candles) == false)
            {
                _tab.SellAtStop(GetVolume(_tab), _lastBollingerDown, _lastBollingerDown - slippage, StopActivateType.LowerOrEqual, 1);
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

    // Filter for buy signal
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

    // Filter for sell signal
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