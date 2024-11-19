using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.BotsFromStartLessons
{
    [Bot("Lesson4Bot1")]
    public class Lesson4Bot1 : BotPanel
    {
        BotTabSimple _tabToTrade;

        StrategyParameterString _regime;
        StrategyParameterInt _smaLenFast;
        StrategyParameterInt _smaLenSlow;

        StrategyParameterString _volumeType;
        StrategyParameterDecimal _volume;
        StrategyParameterString _tradeAssetInPortfolio;

        Aindicator _smaFast;
        Aindicator _smaSlow;

        public Lesson4Bot1(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;
            _tabToTrade.CandleUpdateEvent += _tabToTrade_CandleUpdateEvent;
            _tabToTrade.OrderUpdateEvent += _tabToTrade_OrderUpdateEvent;
            _tabToTrade.MarketDepthUpdateEvent += _tabToTrade_MarketDepthUpdateEvent;

            _tabToTrade.PositionOpeningSuccesEvent += _tabToTrade_PositionOpeningSuccesEvent;

            _tabToTrade.NewTickEvent += _tabToTrade_NewTickEvent;

            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });

            _smaLenFast = CreateParameter("Sma fast len", 15, 1, 10, 1);
            _smaLenSlow = CreateParameter("Sma slow len", 100, 1, 10, 1);

            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            _smaFast = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaFast", false);
            _smaFast = (Aindicator)_tabToTrade.CreateCandleIndicator(_smaFast, "Prime");
            _smaFast.ParametersDigit[0].Value = _smaLenFast.ValueInt;

            _smaSlow = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaSlow", false);
            _smaSlow = (Aindicator)_tabToTrade.CreateCandleIndicator(_smaSlow, "Prime");
            _smaSlow.ParametersDigit[0].Value = _smaLenSlow.ValueInt;

            ParametrsChangeByUser += Lesson3Bot3_ParametrsChangeByUser;
        }

        private void _tabToTrade_PositionOpeningSuccesEvent(Position position)
        {// событие успешного открытия позиции

            // Можно стоп выставить.

        }

        private void _tabToTrade_MarketDepthUpdateEvent(MarketDepth marketDepth)
        { // событие обновления стакана

            // ничего не делаем. Для примера

        }

        private void _tabToTrade_OrderUpdateEvent(Order order)
        { // событие изменения ордера по источнику

            // ничего не делаем. Для примера

        }

        private void _tabToTrade_CandleUpdateEvent(List<Candle> candles)
        {// событие обновления свечи. Вызывается очень часто. 

            // ничего не делаем. Для примера

        }

        private void _tabToTrade_NewTickEvent(Trade trade)
        {// событие нового трейда по бумаге

            // ничего не делаем. Для примера
        }

        private void Lesson3Bot3_ParametrsChangeByUser()
        {
            _smaFast.ParametersDigit[0].Value = _smaLenFast.ValueInt;
            _smaFast.Reload();
            _smaFast.Save();

            _smaSlow.ParametersDigit[0].Value = _smaLenSlow.ValueInt;
            _smaSlow.Reload();
            _smaSlow.Save();
        }

        private void _tabToTrade_CandleFinishedEvent(List<Candle> candles)
        {
            // вызывается на каждой новой свече

            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 10)
            {
                return;
            }

            List<Position> positions = _tabToTrade.PositionsOpenAll;

            if (positions.Count == 0)
            {// открытие позиции
                decimal smaFastLast = _smaFast.DataSeries[0].Last;

                if (smaFastLast == 0)
                {
                    return;
                }

                decimal smaSlowLast = _smaSlow.DataSeries[0].Last;

                if (smaSlowLast == 0)
                {
                    return;
                }

                if (smaFastLast > smaSlowLast)
                {
                    decimal volume = GetVolume(_tabToTrade);
                    _tabToTrade.BuyAtMarket(volume);
                }
            }
            else
            { // закрытие позиции
                decimal smaFastLast = _smaFast.DataSeries[0].Last;

                if (smaFastLast == 0)
                {
                    return;
                }

                decimal smaSlowLast = _smaSlow.DataSeries[0].Last;

                if (smaSlowLast == 0)
                {
                    return;
                }

                if (smaFastLast < smaSlowLast)
                {
                    Position position = positions[0]; // берём позицию из массива

                    if (position.State == PositionStateType.Open)
                    {
                        _tabToTrade.CloseAtMarket(position, position.OpenVolume);
                    }
                }
            }
        }

        public override string GetNameStrategyType()
        {
            return "Lesson4Bot1";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

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
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (_volume.ValueDecimal / 100);

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
}