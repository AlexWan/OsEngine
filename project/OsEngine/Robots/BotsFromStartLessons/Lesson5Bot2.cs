/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Robot example from the lecture course "C# for algotreader".

Buy:
if alligator lips > teeth > jaw (lips - fast, teeth - medium, jaw - slow),
additional open if last value AO > previous value AO and previous value AO > previous previous value AO.

Exit: Close At Trailing Stop Market.
*/

namespace OsEngine.Robots.BotsFromStartLessons
{
    [Bot("Lesson5Bot2")]
    public class Lesson5Bot2 : BotPanel
    {
        private BotTabSimple _tabToTrade;

        // Basic settings
        private StrategyParameterString _mode;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator alligator settings
        private StrategyParameterInt _lengthJaw;
        private StrategyParameterInt _lengthTeeth;
        private StrategyParameterInt _lengthLips;

        // Indicator priceChannel setting
        private StrategyParameterInt _lengthPriceChannel;

        // Indicator AO lengthFastLine and lengthSlowLine settings
        private StrategyParameterInt _lengthFastLineAO;
        private StrategyParameterInt _lengthSlowLineAO;

        // Indicators
        private Aindicator _alligator;
        private Aindicator _priceChannel;
        private Aindicator _aO;

        public Lesson5Bot2(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];

            // Basic settings
            _mode = CreateParameter("Mode", "Off", new[] { "Off", "On" });

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Alligator settings
            _lengthJaw = CreateParameter("Alligator Jaw", 13, 10, 100, 2);
            _lengthTeeth = CreateParameter("Alligator Teeth", 8, 8, 100, 2);
            _lengthLips = CreateParameter("Alligator Lips", 5, 10, 100, 2);

            // lengthPrice setting
            _lengthPriceChannel = CreateParameter("Length price channel", 21, 10, 100, 2);

            // AO settings
            _lengthFastLineAO = CreateParameter("AO fast line", 5, 10, 100, 2);
            _lengthSlowLineAO = CreateParameter("AO slow line", 32, 10, 100, 2);

            // Create indicator Alligator
            _alligator = IndicatorsFactory.CreateIndicatorByName("Alligator", name + "Alligator", false);
            _alligator = (Aindicator)_tabToTrade.CreateCandleIndicator(_alligator, "Prime");
            _alligator.ParametersDigit[0].Value = _lengthJaw.ValueInt;
            _alligator.ParametersDigit[1].Value = _lengthTeeth.ValueInt;
            _alligator.ParametersDigit[2].Value = _lengthLips.ValueInt;

            // Create indicator PriceChannel
            _priceChannel = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PriceChannel", false);
            _priceChannel = (Aindicator)_tabToTrade.CreateCandleIndicator(_priceChannel, "Prime");
            _priceChannel.ParametersDigit[0].Value = _lengthPriceChannel.ValueInt;
            _priceChannel.ParametersDigit[1].Value = _lengthPriceChannel.ValueInt;

            // Create indicator AO
            _aO = IndicatorsFactory.CreateIndicatorByName("AO", name + "AO", false);
            _aO = (Aindicator)_tabToTrade.CreateCandleIndicator(_aO, "AreaAO");
            _aO.ParametersDigit[0].Value = _lengthFastLineAO.ValueInt;
            _aO.ParametersDigit[1].Value = _lengthSlowLineAO.ValueInt;
            
            // Subscribe handler to track robot parameter changes
            ParametrsChangeByUser += Lesson5Bot2_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;
        }

        private void Lesson5Bot2_ParametrsChangeByUser()
        {
            _alligator.ParametersDigit[0].Value = _lengthJaw.ValueInt;
            _alligator.ParametersDigit[1].Value = _lengthTeeth.ValueInt;
            _alligator.ParametersDigit[2].Value = _lengthLips.ValueInt;
            _alligator.Reload();
            _alligator.Save();

            _priceChannel.ParametersDigit[0].Value = _lengthPriceChannel.ValueInt;
            _priceChannel.ParametersDigit[1].Value = _lengthPriceChannel.ValueInt;
            _priceChannel.Reload();
            _priceChannel.Save();

            _aO.ParametersDigit[0].Value = _lengthFastLineAO.ValueInt;
            _aO.ParametersDigit[1].Value = _lengthSlowLineAO.ValueInt;
            _aO.Reload();
            _aO.Save();
        }

        private void _tabToTrade_CandleFinishedEvent(List<Candle> candles)
        {
            // called on each new candle

            if (_mode.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 10)
            {
                return;
            }

            List<Position> positions = _tabToTrade.PositionsOpenAll;

            if (positions.Count == 0) // no positions. True!
            { // opening position

                decimal jaw = _alligator.DataSeries[0].Last;   // Long
                decimal teeth = _alligator.DataSeries[1].Last; // Medium
                decimal lips = _alligator.DataSeries[2].Last;  // Short

                if (jaw == 0
                    || // Operator OR
                    teeth == 0
                    || lips == 0)
                {// If something is true - exit the method. Indicator not ready
                    return;
                }

                if (lips > teeth
                    && // Operator AND
                    teeth > jaw)
                {// If both expressions are true, enter the position

                    decimal volume = GetVolume(_tabToTrade);
                    _tabToTrade.BuyAtMarket(volume);
                }
            }
            else if (positions[0].OpenOrders.Count == 1) // order to open only one. True!
            { // additional open

                decimal lastAO = _aO.DataSeries[0].Values[_aO.DataSeries[0].Values.Count - 1];
                decimal prevAO = _aO.DataSeries[0].Values[_aO.DataSeries[0].Values.Count - 2];
                decimal prevPrevAO = _aO.DataSeries[0].Values[_aO.DataSeries[0].Values.Count - 3];

                if (lastAO == 0
                    || prevAO == 0
                    || prevPrevAO == 0)
                {
                    return;
                }

                if (prevAO < lastAO
                    && prevAO < prevPrevAO)
                {
                    decimal volume = GetVolume(_tabToTrade);
                    _tabToTrade.BuyAtMarketToPosition(_tabToTrade.PositionsOpenAll[0], volume);
                }
            }

            if (positions.Count == 1) // position is open
            {
                // use trailling stop

                decimal pcLow = _priceChannel.DataSeries[1].Last;

                if (pcLow == 0)
                {
                    return;
                }

                Position position = positions[0];

                _tabToTrade.CloseAtTrailingStopMarket(position, pcLow);
            }
        }

        public override string GetNameStrategyType()
        {
            return "Lesson5Bot2";
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