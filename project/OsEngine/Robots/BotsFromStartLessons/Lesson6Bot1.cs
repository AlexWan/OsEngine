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
    [Bot("Lesson6Bot1")]
    public class Lesson6Bot1 : BotPanel
    {
        BotTabSimple _tabToTrade;

        StrategyParameterString _regime;

        StrategyParameterString _volumeType;
        StrategyParameterDecimal _volume;
        StrategyParameterString _tradeAssetInPortfolio;

        StrategyParameterInt _lengthBollinger;
        StrategyParameterDecimal _bollingerDeviation;
        StrategyParameterInt _atrLength;

        StrategyParameterDecimal _multOne;
        StrategyParameterDecimal _multTwo;

        Aindicator _bollinger;
        Aindicator _atr;

        public Lesson6Bot1(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");
            _multOne = CreateParameter("Mult 1", 0.5m, 1.0m, 50, 4);
            _multTwo = CreateParameter("Mult 2", 1, 1.0m, 50, 4);

            _lengthBollinger = CreateParameter("Bollinger len", 21, 10, 100, 2);
            _bollingerDeviation = CreateParameter("Bollinger deviation", 1.5m, 10, 100, 2);

            _atrLength = CreateParameter("Length ATR", 14, 10, 100, 2);

            _bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
            _bollinger = (Aindicator)_tabToTrade.CreateCandleIndicator(_bollinger, "Prime");
            _bollinger.ParametersDigit[0].Value = _lengthBollinger.ValueInt;
            _bollinger.ParametersDigit[1].Value = _bollingerDeviation.ValueDecimal;

            _atr = IndicatorsFactory.CreateIndicatorByName("ATR", name + "ATR", false);
            _atr = (Aindicator)_tabToTrade.CreateCandleIndicator(_atr, "Atr Area");
            _atr.ParametersDigit[0].Value = _atrLength.ValueInt;

            this.ParametrsChangeByUser += Lesson6Bot1_ParametrsChangeByUser;
        }

        private void Lesson6Bot1_ParametrsChangeByUser()
        {
            _bollinger.ParametersDigit[0].Value = _lengthBollinger.ValueInt;
            _bollinger.ParametersDigit[1].Value = _bollingerDeviation.ValueDecimal;
            _bollinger.Reload();
            _bollinger.Save();

            _atr.ParametersDigit[0].Value = _atrLength.ValueInt;
            _atr.Reload();
            _atr.Save();
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
            { // логика открытия первой позиции

                decimal bollingerUpLine = _bollinger.DataSeries[0].Last;

                if (bollingerUpLine == 0)
                {
                    return;
                }

                decimal volume = GetVolume(_tabToTrade);

                _tabToTrade.BuyAtStop(volume, bollingerUpLine, bollingerUpLine, StopActivateType.HigherOrEqual);
            }
            else if (positions.Count == 1)
            { // логика открытия второй позиции
                decimal entryPriceFirstPosition = positions[0].EntryPrice;

                decimal atrValue = _atr.DataSeries[0].Last;

                if (atrValue == 0)
                {
                    return;
                }

                decimal newEntryPrice = entryPriceFirstPosition + atrValue * _multOne.ValueDecimal;

                decimal volume = GetVolume(_tabToTrade);

                _tabToTrade.BuyAtStop(volume, newEntryPrice, newEntryPrice, StopActivateType.HigherOrEqual);
            }
            else if (positions.Count == 2)
            { // логика открытия третей позиции
                decimal entryPriceFirstPosition = positions[0].EntryPrice;

                decimal atrValue = _atr.DataSeries[0].Last;

                if (atrValue == 0)
                {
                    return;
                }

                decimal newEntryPrice = entryPriceFirstPosition + atrValue * _multTwo.ValueDecimal;

                decimal volume = GetVolume(_tabToTrade);

                _tabToTrade.BuyAtStop(volume, newEntryPrice, newEntryPrice, StopActivateType.HigherOrEqual);
            }

            if (positions.Count > 0)
            { // уставливаем трейлинг стоп на все позиции

                decimal bollingerDownLine = _bollinger.DataSeries[1].Last;

                if (bollingerDownLine == 0)
                {
                    return;
                }

                for (int i = 0; i < positions.Count; i++)
                {
                    Position currentPos = positions[i];

                    _tabToTrade.CloseAtTrailingStop(currentPos, bollingerDownLine, bollingerDownLine);
                }
            }
        }

        public override string GetNameStrategyType()
        {
            return "Lesson6Bot1";
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