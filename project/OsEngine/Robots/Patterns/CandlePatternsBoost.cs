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
Trading robot for osengine.

Trend strategy on Candle Pattern Boost. 

Buy:
1. The current closing price is above the level of _lastVGDevUp.
2. The percentage movement upward over this period exceeds the specified threshold.

Sell:
1. The current closing price is below the level of _lastVGDevDown.
2. The percentage decline over this period exceeds the threshold.

Exit: according to the selected method.
*/

namespace OsEngine.Robots.patt
{
    [Bot("CandlePatternBoost")] //We create an attribute so that we don't write anything in the Boot factory
    public class CandlePatternBoost : BotPanel
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

        // Candle for boost settings
        private StrategyParameterInt _candleForBoost;
        private StrategyParameterDecimal _candleForBoostPersent;

        // Van_Gerch settings
        private StrategyParameterInt _periodVGUp;
        private StrategyParameterDecimal _deviaitonVGUp;

        // SmaFilter settings
        private StrategyParameterBool _smaPositionFilterIsOn;
        private StrategyParameterInt _smaLengthFilter;
        private StrategyParameterBool _smaSlopeFilterIsOn;
        
        // Indicator
        private Aindicator _smaFilter;
        private Aindicator _van_Gerch;

        // Exit settings
        private StrategyParameterString _regimeExit;
        private StrategyParameterInt _exitCandlesCount;

        // The last value of the indicator and price
        private decimal _lastPriceClose;
        private decimal _lastPCVGUp;
        private decimal _lastPCVGDown;
        private decimal _lastVGDevUp;
        private decimal _lastVGDevDown;

        public CandlePatternBoost(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _timeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _timeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Volume");
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4, "Volume");
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Volume");

            // Candle for boost settings
            _candleForBoost = CreateParameter("Boost for the number of candles", 2, 1, 10, 1, "Robot parameters");
            _candleForBoostPersent = CreateParameter("Minimum overlap of the deflection channel, % ", 60m, 30, 100, 10, "Robot parameters");

            // Van_Gerch settings
            _periodVGUp = CreateParameter("Period VanGerchik indicator", 50, 1, 10, 1, "Robot parameters");
            _deviaitonVGUp = CreateParameter("Deviation VanGerchik indicator %", 1, 1.0m, 50, 5, "Robot parameters");

            // Exit settings
            _regimeExit = CreateParameter("Regime exit", "Traling stop", new[] { "Traling stop", "Candle count" }, "Robot parameters");
            _exitCandlesCount = CreateParameter("Exit Candles Count", 2, 1, 50, 4, "Robot parameters");

            // SmaFilter settings
            _smaLengthFilter = CreateParameter("Sma Length Filter", 100, 10, 500, 1, "Filter parameters");
            _smaPositionFilterIsOn = CreateParameter("Is SMA Filter On", false, "Filter parameters");
            _smaSlopeFilterIsOn = CreateParameter("Is Sma Slope Filter On", false, "Filter parameters");

            // Create indicator VanGerchik
            _van_Gerch = IndicatorsFactory.CreateIndicatorByName(nameClass: "VanGerchik_indicator", name: name + "VanGerchik_indicator", canDelete: false);
            _van_Gerch = (Aindicator)_tab.CreateCandleIndicator(_van_Gerch, nameArea: "Prime");
            _van_Gerch.DataSeries[0].Color = System.Drawing.Color.Azure;
            _van_Gerch.ParametersDigit[0].Value = _periodVGUp.ValueInt;
            _van_Gerch.ParametersDigit[1].Value = _deviaitonVGUp.ValueDecimal;
            _van_Gerch.Save();

            // Create indicator Sma
            _smaFilter = IndicatorsFactory.CreateIndicatorByName(nameClass: "Sma", name: name + "Sma_Filter", canDelete: false);
            _smaFilter = (Aindicator)_tab.CreateCandleIndicator(_smaFilter, nameArea: "Prime");
            _smaFilter.DataSeries[0].Color = System.Drawing.Color.Azure;
            _smaFilter.ParametersDigit[0].Value = _smaLengthFilter.ValueInt;
            _smaFilter.Save();

            StopOrActivateIndicators();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += CandlePatternBoost_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel72;
        }

        private void CandlePatternBoost_ParametrsChangeByUser()
        {
            StopOrActivateIndicators();

            if (_van_Gerch.ParametersDigit[0].Value != _periodVGUp.ValueInt ||
            _van_Gerch.ParametersDigit[1].Value != _deviaitonVGUp.ValueDecimal
            )
            {
                _van_Gerch.ParametersDigit[0].Value = _periodVGUp.ValueInt;
                _van_Gerch.ParametersDigit[1].Value = _deviaitonVGUp.ValueDecimal;
                _van_Gerch.Save();
                _van_Gerch.Reload();
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
            return "CandlePatternBoost";
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
                CancelStopsAndProfits();
                return;
            }

            if (!GetValueVariables(candles))
            {
                return;
            }

            List<Position> positions = _tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                ClosePosition(positions[i], candles);
            }

            if (positions == null || positions.Count == 0)
            {
                OpenPosotion(candles);
            }
        }

        // Open position logic
        private void OpenPosotion(List<Candle> candles)
        {
            if (HaveSignalUp(candles))
            {
                if (BuySignalIsFiltered(candles) == true)
                {
                    return;
                }

                _tab.BuyAtMarket(GetVolume(_tab), candles[candles.Count - 1].TimeStart.ToString());
            }

            if (CheckSignalDown(candles))
            {
                if (SellSignalIsFiltered(candles) == true)
                {
                    return;
                }

                _tab.SellAtMarket(GetVolume(_tab), candles[candles.Count - 1].TimeStart.ToString());
            }
        }

        // Close position logic
        private void ClosePosition(Position position, List<Candle> candles)
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

                if (_regimeExit.ValueString == "Traling stop")
                {
                    if (position.Direction == Side.Buy)
                    {
                        decimal slippage = _slippage.ValueDecimal * _lastVGDevDown / 100;
                        _tab.CloseAtTrailingStop(position, _lastVGDevDown, _lastVGDevDown - slippage);
                    }

                    if (position.Direction == Side.Sell)
                    {
                        decimal slippage = _slippage.ValueDecimal * _lastVGDevUp / 100;
                        _tab.CloseAtTrailingStop(position, _lastVGDevUp, _lastVGDevUp + slippage);
                    }
                }
                else if (_regimeExit.ValueString == "Candle count")
                {
                    DateTime timeOpenPos = Convert.ToDateTime(position.SignalTypeOpen);

                    int candleCountAfterOpen = -1;

                    for (int i = candles.Count - 1; i > 0; i--)
                    {
                        if (candles[i].TimeStart == timeOpenPos)
                        {
                            candleCountAfterOpen = candles.Count - i;
                            break;
                        }
                    }

                    if (candleCountAfterOpen == -1)
                    {
                        return;
                    }

                    if (candleCountAfterOpen >= _exitCandlesCount.ValueInt)
                    {
                        if (position.Direction == Side.Buy)
                        {
                            decimal slippage = _slippage.ValueDecimal * _lastVGDevDown / 100;
                            _tab.CloseAtLimit(position, _tab.PriceBestBid - slippage, position.OpenVolume);
                        }

                        if (position.Direction == Side.Sell)
                        {
                            decimal slippage = _slippage.ValueDecimal * _lastVGDevUp / 100;
                            _tab.CloseAtLimit(position, _tab.PriceBestAsk + slippage, position.OpenVolume);
                        }
                    }
                }
            }
        }

        // Get last value indicator and price
        private bool GetValueVariables(List<Candle> candles)
        {
            _lastPriceClose = candles[candles.Count - 1].Close;

            _lastPCVGUp = _van_Gerch.DataSeries[0].Last;
            _lastPCVGDown = _van_Gerch.DataSeries[1].Last;
            _lastVGDevUp = _van_Gerch.DataSeries[2].Last;
            _lastVGDevDown = _van_Gerch.DataSeries[3].Last;

            if (_lastPriceClose == 0 || _lastPCVGUp == 0 || _lastPCVGDown == 0 ||
                _lastVGDevUp == 0 || _lastVGDevDown == 0)
            {
                return false;
            }

            return true;
        }

        // Buy signal
        public bool HaveSignalUp(List<Candle> candles)
        {
            decimal widthVGPUp = _lastPCVGUp - _lastPCVGDown;

            decimal start = candles[candles.Count - 1 - _candleForBoost.ValueInt].Open;
            decimal close = candles[candles.Count - 1].Close;

            for (int i = candles.Count - 1 - _candleForBoost.ValueInt; i < candles.Count; i++)
            {
                if (candles[i].Low < start)
                {
                    start = candles[i].Low;
                }
            }

            if (close < start)
            {
                return false;
            }

            decimal overallCandleSizeUp = close - start;

            if(overallCandleSizeUp == 0)
            {
                return false;
            }

            decimal percentMovemetUp = overallCandleSizeUp / (widthVGPUp / 100);

            if (percentMovemetUp >= _candleForBoostPersent.ValueDecimal
                && close > _lastVGDevUp)
            {
                return true;
            }

            return false;
        }

        // Sell signal
        public bool CheckSignalDown(List<Candle> candles)
        {
            decimal widthVGPUp = _lastPCVGUp - _lastPCVGDown;

            decimal start = candles[candles.Count - 1 - _candleForBoost.ValueInt].Open;

            for (int i = candles.Count - 1 - _candleForBoost.ValueInt; i < candles.Count; i++)
            {
                if (candles[i].High > start)
                {
                    start = candles[i].High;
                }
            }

            decimal close = candles[candles.Count - 1].Close;

            if (close > start)
            {
                return false;
            }

            decimal overallCandleSizeUp = start - close;

            if (overallCandleSizeUp == 0)
            {
                return false;
            }

            decimal percentMovemetUp = overallCandleSizeUp / (widthVGPUp / 100);

            if (percentMovemetUp >= _candleForBoostPersent.ValueDecimal
                && close < _lastVGDevDown)
            {
                return true;
            }

            return false;
        }

        // Cancel stop and profit orders
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
                // if the price is lower than the last value - returns to the top true

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
}