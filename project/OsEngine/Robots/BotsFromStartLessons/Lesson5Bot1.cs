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
    [Bot("Lesson5Bot1")]
    public class Lesson5Bot1 : BotPanel
    {
        BotTabSimple _tabToTrade;

        StrategyParameterString _regime;
        StrategyParameterInt _smaLenFast;
        StrategyParameterInt _minutesInPosition;

        StrategyParameterString _volumeType;
        StrategyParameterDecimal _volume;
        StrategyParameterString _tradeAssetInPortfolio;

        Aindicator _sma;

        public Lesson5Bot1(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _smaLenFast = CreateParameter("Sma len", 15, 1, 10, 1);
            _minutesInPosition = CreateParameter("Minutes in position", 90, 30, 500, 30);

            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma", false);
            _sma = (Aindicator)_tabToTrade.CreateCandleIndicator(_sma, "Prime");
            _sma.ParametersDigit[0].Value = _smaLenFast.ValueInt;

            ParametrsChangeByUser += Lesson5Bot1_ParametrsChangeByUser;
        }

        private void Lesson5Bot1_ParametrsChangeByUser()
        {
            _sma.ParametersDigit[0].Value = _smaLenFast.ValueInt;
            _sma.Reload();
            _sma.Save();
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

            if (positions.Count == 0) // позиций нет. Правда!
            {// логика открытия позиции

                decimal lastSma = _sma.DataSeries[0].Last;

                if (lastSma == 0)
                {
                    return;
                }

                Candle candle = candles[candles.Count - 1];
                decimal closeCandle = candle.Close;
                decimal highCandle = candle.High;
                decimal lowCandle = candle.Low;

                if (
                    lowCandle < lastSma
                    &&
                    closeCandle > lastSma
                    )
                { // открываем лонг
                    decimal volume = GetVolume(_tabToTrade);
                    _tabToTrade.BuyAtMarket(volume);
                }
                else if (
                    highCandle > lastSma
                    &&
                    closeCandle < lastSma)
                { // открываем шорт
                    decimal volume = GetVolume(_tabToTrade);
                    _tabToTrade.SellAtMarket(volume);
                }
                else
                {
                    // do nothin
                }
            }
            else
            {// логика закрытия позиции
                Position position = positions[0];

                DateTime positionEntryTime = position.TimeOpen;

                DateTime timeExtPosition = positionEntryTime.AddMinutes(_minutesInPosition.ValueInt);

                DateTime candleTime = candles[candles.Count - 1].TimeStart;

                if (candleTime > timeExtPosition)
                { // выход из позиции. Время свечи больше времени открытия позиции + сколько то минут
                    if (position.State == PositionStateType.Open)
                    {
                        _tabToTrade.CloseAtMarket(position, position.OpenVolume);
                    }
                }
            }
        }

        public override string GetNameStrategyType()
        {
            return "Lesson5Bot1";
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
                    tab.Securiti.Lot != 0 &&
                        tab.Securiti.Lot > 1)
                    {
                        volume = _volume.ValueDecimal / (contractPrice * tab.Securiti.Lot);
                    }

                    volume = Math.Round(volume, tab.Securiti.DecimalsVolume);
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

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Securiti.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    qty = Math.Round(qty, tab.Securiti.DecimalsVolume);
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