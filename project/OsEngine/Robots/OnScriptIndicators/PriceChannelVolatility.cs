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
using System;
using System.Collections.Generic;

/* Description
trading robot for osengine

Breakthrough of the channel built by PriceChannel + -ATR * coefficient,additional input when the price leaves below the channel line by ATR * coefficient.

Trailing stop on the bottom line of the PriceChannel channel.
 */

[Bot("PriceChannelVolatility")] // We create an attribute so that we don't write anything to the BotFactory
public class PriceChannelVolatility : BotPanel
{
    private BotTabSimple _tab;

    // Basic settings
    private StrategyParameterString _regime;
    private StrategyParameterInt _slippage;

    // GetVolume settings
    private StrategyParameterDecimal _volumeFix1;
    private StrategyParameterDecimal _volumeFix2;
    private StrategyParameterString _volumeType;
    private StrategyParameterString _tradeAssetInPortfolio;

    // Indicator settings
    private StrategyParameterInt _lengthAtr;
    private StrategyParameterInt _lengthChannelUp;
    private StrategyParameterInt _lengthChannelDown;
    private StrategyParameterDecimal _kofAtr;

    // Indicator
    private Aindicator _atr;
    private Aindicator _pc;

    // The last value of the indicator
    private decimal _lastPcUp;
    private decimal _lastPcDown;
    private decimal _lastAtr;

    public PriceChannelVolatility(string name, StartProgram startProgram) : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        // Basic settings
        _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
        _slippage = CreateParameter("Slippage", 0, 0, 20, 1);
        
        // GetVolume settings
        _volumeFix1 = CreateParameter("Volume 1", 3, 1.0m, 50, 4);
        _volumeFix2 = CreateParameter("Volume 2", 3, 1.0m, 50, 4);
        _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
        _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

        // Indicator settings
        _lengthAtr = CreateParameter("Length Atr", 14, 5, 80, 3);
        _kofAtr = CreateParameter("Atr mult", 0.5m, 0.1m, 5, 0.1m);
        _lengthChannelUp = CreateParameter("Length Channel Up", 12, 5, 80, 2);
        _lengthChannelDown = CreateParameter("Length Channel Down", 12, 5, 80, 2);

        // Create indicator PriceChannel
        _pc = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PriceChannel", false);
        _pc.ParametersDigit[0].Value = _lengthChannelUp.ValueInt;
        _pc.ParametersDigit[1].Value = _lengthChannelDown.ValueInt;
        _pc = (Aindicator)_tab.CreateCandleIndicator(_pc, "Prime");
        _pc.Save();

        // Create indicator ATR
        _atr = IndicatorsFactory.CreateIndicatorByName("ATR",name + "ATR", false);
        _atr.ParametersDigit[0].Value = _lengthAtr.ValueInt;
        _atr = (Aindicator)_tab.CreateCandleIndicator(_atr, "Second");
        _atr.Save();

        // Subscribe to the candle finished event
        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

        // Subscribe to the indicator update event
        ParametrsChangeByUser += Event_ParametrsChangeByUser;

        Description = OsLocalization.Description.DescriptionLabel64;
    }

    void Event_ParametrsChangeByUser()
    {
        if (_atr.ParametersDigit[0].Value != _lengthAtr.ValueInt)
        {
            _atr.ParametersDigit[0].Value = _lengthAtr.ValueInt;
            _atr.Reload();
        }

        if (_pc.ParametersDigit[0].Value != _lengthChannelUp.ValueInt)
        {
            _pc.ParametersDigit[0].Value = _lengthChannelUp.ValueInt;
            _pc.Reload();
        }

        if (_pc.ParametersDigit[1].Value != _lengthChannelDown.ValueInt)
        {
            _pc.ParametersDigit[1].Value = _lengthChannelDown.ValueInt;
            _pc.Reload();
        }
    }

    // The name of the robot in OsEngine
    public override string GetNameStrategyType()
    {
        return "PriceChannelVolatility";
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

        _lastPcUp = _pc.DataSeries[0].Last;
        _lastPcDown = _pc.DataSeries[1].Last;
        _lastAtr = _atr.DataSeries[0].Last;

        if (_pc.DataSeries[0].Values == null || _pc.DataSeries[1].Values == null || 
            _pc.DataSeries[0].Values.Count < _pc.ParametersDigit[0].Value + 1 ||
            _pc.DataSeries[1].Values.Count < _pc.ParametersDigit[1].Value + 1 ||
            _atr.DataSeries[0].Values == null || _atr.DataSeries[0].Values.Count < _atr.ParametersDigit[0].Value + 1)
        {
            return;
        }

        List<Position> openPositions = _tab.PositionsOpenAll;

        if (openPositions != null && openPositions.Count != 0)
        {
            LogicClosePosition();
        }

        if (_regime.ValueString == "OnlyClosePosition")
        {
            return;
        }

        if (openPositions == null || openPositions.Count == 0)
        {
            LogicOpenPosition(candles);
        }
    }

    // Logic open position
    private void LogicOpenPosition(List<Candle> candles)
    {
        List<Position> openPositions = _tab.PositionsOpenAll;

        if (openPositions == null || openPositions.Count == 0)
        {
            _tab.BuyAtStopCancel();
            _tab.SellAtStopCancel();

            // long
            if (_regime.ValueString != "OnlyShort")
            {
                decimal priceEnter = _lastPcUp;

                _tab.BuyAtStop(GetVolume(_tab, _volumeFix1.ValueDecimal),
                    priceEnter + _slippage.ValueInt * _tab.Security.PriceStep,
                    priceEnter, StopActivateType.HigherOrEqual);
            }

            // Short
            if (_regime.ValueString != "OnlyLong")
            {
                decimal priceEnter = _lastPcDown;

                _tab.SellAtStop(GetVolume(_tab, _volumeFix1.ValueDecimal),
                    priceEnter - _slippage.ValueInt * _tab.Security.PriceStep,
                    priceEnter, StopActivateType.LowerOrEqual);
            }

            return;
        }

        if (openPositions.Count == 1)
        {
            _tab.BuyAtStopCancel();
            _tab.SellAtStopCancel();

            if (openPositions[0].Direction == Side.Buy)
            {
                decimal priceEnter = _lastPcUp + (_lastAtr * _kofAtr.ValueDecimal);

                _tab.BuyAtStop(GetVolume(_tab, _volumeFix2.ValueDecimal),
                    priceEnter + _slippage.ValueInt * _tab.Security.PriceStep,
                    priceEnter, StopActivateType.HigherOrEqual);
            }
            else
            {
                decimal priceEnter = _lastPcDown - (_lastAtr * _kofAtr.ValueDecimal);

                _tab.SellAtStop(GetVolume(_tab, _volumeFix2.ValueDecimal),
                    priceEnter - _slippage.ValueInt * _tab.Security.PriceStep,
                    priceEnter, StopActivateType.LowerOrEqual);
            }
        }
    }

    // Logic close position
    private void LogicClosePosition()
    {
        List<Position> openPositions = _tab.PositionsOpenAll;

        for (int i = 0; openPositions != null && i < openPositions.Count; i++)
        {
            if (openPositions[i].State != PositionStateType.Open)
            {
                continue;
            }

            if (openPositions[i].Direction == Side.Buy)
            {
                decimal priceClose = _lastPcDown;

                _tab.CloseAtTrailingStop(openPositions[i], priceClose,
                    priceClose - _slippage.ValueInt * _tab.Security.PriceStep);
            }
            else
            {
                decimal priceClose = _lastPcUp;

                _tab.CloseAtTrailingStop(openPositions[i], priceClose,
                    priceClose + _slippage.ValueInt * _tab.Security.PriceStep);
            }
        }
    }

    // Method for calculating the volume of entry into a position
    private decimal GetVolume(BotTabSimple tab, decimal _volume)
    {
        decimal volume = 0;

        if (_volumeType.ValueString == "Contracts")
        {
            volume = _volume;
        }
        else if (_volumeType.ValueString == "Contract currency")
        {
            decimal contractPrice = tab.PriceBestAsk;
            volume = _volume / contractPrice;

            if (StartProgram == StartProgram.IsOsTrader)
            {
                IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                if (serverPermission != null &&
                    serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                    tab.Security.Lot > 1)
                {
                    volume = _volume / (contractPrice * tab.Security.Lot);
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

            decimal moneyOnPosition = portfolioPrimeAsset * (_volume / 100);

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