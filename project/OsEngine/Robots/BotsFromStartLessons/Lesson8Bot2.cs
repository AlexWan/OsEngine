using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.BotsFromStartLessons
{
    [Bot("Lesson8Bot2")]
    public class Lesson8Bot2 : BotPanel
    {
        BotTabSimple _tabToTrade;

        StrategyParameterString _regime;
        StrategyParameterString _volumeType;
        StrategyParameterDecimal _volume;
        StrategyParameterString _tradeAssetInPortfolio;

        StrategyParameterInt _priceChannelLen;

        // логика
        // покупаем отложенной заявкой по хаю ценового канала
        // выходим отложенной заявкой по лою ценового канала

        public Lesson8Bot2(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            _priceChannelLen = CreateParameter("Price Channel len", 40, 1, 50, 4);


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

                decimal high = GetHigh(candles, _priceChannelLen.ValueInt);
                decimal volume = GetVolume(_tabToTrade);
                _tabToTrade.BuyAtStop(volume, high, high, StopActivateType.HigherOrEqual);
            }
            else
            {// логика закрытия позиции

                decimal low = GetLow(candles, _priceChannelLen.ValueInt);
                _tabToTrade.CloseAtTrailingStopMarket(positions[0], low);
            }
        }

        private decimal GetHigh(List<Candle> candles, int len)
        {
            decimal high = 0;

            // цикл с конца. Движение назад

            for (int i = candles.Count - 1; i >= 0 && i > candles.Count - 1 - len; i--)
            {
                Candle currentCandle = candles[i];

                if (currentCandle.High > high)
                {
                    high = currentCandle.High;
                }
            }

            return high;
        }

        private decimal GetLow(List<Candle> candles, int len)
        {
            decimal low = decimal.MaxValue;

            // цикл с конца минус длинна. Движение вперёд
            for (int i = candles.Count - 1 - len; i >= 0 && i < candles.Count; i++)
            {
                Candle currentCandle = candles[i];

                if (currentCandle.Low < low)
                {
                    low = currentCandle.Low;
                }
            }

            return low;
        }

        public override string GetNameStrategyType()
        {
            return "Lesson8Bot2";
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