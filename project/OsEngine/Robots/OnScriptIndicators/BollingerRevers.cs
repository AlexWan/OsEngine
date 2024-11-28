using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using System;
using OsEngine.Market.Servers;
using OsEngine.Logging;
using OsEngine.Market;

/// <summary>
/// Trend Strategy Based on Breaking Bollinger Lines
/// Трендовая стратегия на основе пробития линий болинджера
/// </summary>
[Bot("BollingerRevers")]
public class BollingerRevers : BotPanel
{
    public BollingerRevers(string name, StartProgram startProgram)
        : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });

        VolumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
        Volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
        TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

        Slippage = CreateParameter("Slippage", 0, 0, 20, 1);

        BollingerLength = CreateParameter("Bollinger Length", 12, 4, 100, 2);
        BollingerDeviation = CreateParameter("Bollinger Deviation", 2, 0.5m, 4, 0.1m);

        _bol = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
        _bol = (Aindicator)_tab.CreateCandleIndicator(_bol, "Prime");

        _bol.ParametersDigit[0].Value = BollingerLength.ValueInt;
        _bol.ParametersDigit[1].Value = BollingerDeviation.ValueDecimal;

        _bol.Save();

        _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
        ParametrsChangeByUser += Event_ParametrsChangeByUser;

        Description = "Trend Strategy Based on Breaking Bollinger Lines " +
                "Buy: " +
                "1. The price is more than BollingerUpLine. " +
                "Sell: " +
                "1. Price below BollingerDownLine." +
                "Exit: " +
                "1. At the intersection of Sma with the price";
    }

    void Event_ParametrsChangeByUser()
    {
        _bol.ParametersDigit[0].Value = BollingerLength.ValueInt;
        _bol.ParametersDigit[1].Value = BollingerDeviation.ValueDecimal;
        _bol.Reload();
    }

    /// <summary>
    /// bot name
    /// взять уникальное имя
    /// </summary>
    public override string GetNameStrategyType()
    {
        return "BollingerRevers";
    }

    /// <summary>
    /// strategy name
    /// показать окно настроек
    /// </summary>
    public override void ShowIndividualSettingsDialog()
    {

    }

    /// <summary>
    /// trade tab
    /// вкладка для торговли
    /// </summary>
    private BotTabSimple _tab;

    //indicators индикаторы

    private Aindicator _bol;

    //settings настройки публичные

    public StrategyParameterInt Slippage;

    StrategyParameterString VolumeType;
    StrategyParameterDecimal Volume;
    StrategyParameterString TradeAssetInPortfolio;

    public StrategyParameterString Regime;

    public StrategyParameterDecimal BollingerDeviation;

    public StrategyParameterInt BollingerLength;

    private decimal _lastPrice;
    private decimal _bolLastUp;
    private decimal _bolLastDown;

    // logic логика

    /// <summary>
    /// candle finished event
    /// событие завершения свечи
    /// </summary>
    private void Strateg_CandleFinishedEvent(List<Candle> candles)
    {
        if (Regime.ValueString == "Off")
        {
            return;
        }

        if (_bol.DataSeries[0].Values == null || candles.Count < _bol.ParametersDigit[0].Value + 2)
        {
            return;
        }

        _lastPrice = candles[candles.Count - 1].Close;
        _bolLastUp = _bol.DataSeries[0].Last;
        _bolLastDown = _bol.DataSeries[1].Last;

        List<Position> openPositions = _tab.PositionsOpenAll;

        if (openPositions != null && openPositions.Count != 0)
        {
            for (int i = 0; i < openPositions.Count; i++)
            {
                LogicClosePosition(candles, openPositions[i]);
            }
        }

        if (Regime.ValueString == "OnlyClosePosition")
        {
            return;
        }
        if (openPositions == null || openPositions.Count == 0)
        {
            LogicOpenPosition(candles, openPositions);
        }
    }

    /// <summary>
    /// logic open position
    /// логика открытия первой позиции
    /// </summary>
    private void LogicOpenPosition(List<Candle> candles, List<Position> position)
    {
        if (_lastPrice > _bolLastUp
            && Regime.ValueString != "OnlyShort")
        {
            _tab.BuyAtLimit(GetVolume(_tab), _lastPrice + Slippage.ValueInt * _tab.Security.PriceStep);
        }

        if (_lastPrice < _bolLastDown
            && Regime.ValueString != "OnlyLong")
        {
            _tab.SellAtLimit(GetVolume(_tab), _lastPrice - Slippage.ValueInt * _tab.Security.PriceStep);
        }

    }

    /// <summary>
    /// logic close position
    /// логика зыкрытия позиции и открытие по реверсивной системе
    /// </summary>
    private void LogicClosePosition(List<Candle> candles, Position position)
    {
        if (position.State == PositionStateType.Closing ||
            position.CloseActiv == true ||
            (position.CloseOrders != null && position.CloseOrders.Count > 0))
        {
            return;
        }

        if (position.Direction == Side.Buy)
        {
            if (_lastPrice < _bolLastDown)
            {
                _tab.CloseAtLimit(
                    position,
                    _lastPrice - Slippage.ValueInt * _tab.Security.PriceStep,
                    position.OpenVolume);

                if (Regime.ValueString != "OnlyLong"
                    && Regime.ValueString != "OnlyClosePosition")
                {
                    _tab.SellAtLimit(GetVolume(_tab), _lastPrice - Slippage.ValueInt * _tab.Security.PriceStep);
                }
            }
        }
        if (position.Direction == Side.Sell)
        {
            if (_lastPrice > _bolLastUp)
            {
                _tab.CloseAtLimit(
                    position,
                    _lastPrice + Slippage.ValueInt * _tab.Security.PriceStep,
                    position.OpenVolume);

                if (Regime.ValueString != "OnlyShort"
                    && Regime.ValueString != "OnlyClosePosition")
                {
                    _tab.BuyAtLimit(GetVolume(_tab), _lastPrice + Slippage.ValueInt * _tab.Security.PriceStep);
                }

            }
        }
    }

    private decimal GetVolume(BotTabSimple tab)
    {
        decimal volume = 0;

        if (VolumeType.ValueString == "Contracts")
        {
            volume = Volume.ValueDecimal;
        }
        else if (VolumeType.ValueString == "Contract currency")
        {
            decimal contractPrice = tab.PriceBestAsk;
            volume = Volume.ValueDecimal / contractPrice;

            if (StartProgram == StartProgram.IsOsTrader)
            {
                IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                if (serverPermission != null &&
                    serverPermission.IsUseLotToCalculateProfit &&
                tab.Security.Lot != 0 &&
                    tab.Security.Lot > 1)
                {
                    volume = Volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                }

                volume = Math.Round(volume, tab.Security.DecimalsVolume);
            }
            else // Tester or Optimizer
            {
                volume = Math.Round(volume, 6);
            }
        }
        else if (VolumeType.ValueString == "Deposit percent")
        {
            Portfolio myPortfolio = tab.Portfolio;

            if (myPortfolio == null)
            {
                return 0;
            }

            decimal portfolioPrimeAsset = 0;

            if (TradeAssetInPortfolio.ValueString == "Prime")
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
                    if (positionOnBoard[i].SecurityNameCode == TradeAssetInPortfolio.ValueString)
                    {
                        portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                        break;
                    }
                }
            }

            if (portfolioPrimeAsset == 0)
            {
                SendNewLogMessage("Can`t found portfolio " + TradeAssetInPortfolio.ValueString, LogMessageType.Error);
                return 0;
            }
            decimal moneyOnPosition = portfolioPrimeAsset * (Volume.ValueDecimal / 100);

            decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

            if (tab.StartProgram == StartProgram.IsOsTrader)
            {
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