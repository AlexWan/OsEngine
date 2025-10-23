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
using System.IO;

/*Discription
Trading robot for osengine

Trend robot on the Pivot Points Robot.

Buy:
1. The closing price must be **above** resistance level R1.
2. The opening price must be **below** this level.

Sell:
1. The closing price must be **below** support level S1.
2. The opening price must be **above** this level.

Exit Long:
1. If the closing price exceeds resistance level R3.
2. If the price drops below the entry level by a specified percentage (Stop).

Exit Short:
1. If the closing price drops below support level S3.
2. If the price rises above the entry level by a specified percentage (Stop).
*/

namespace OsEngine.Robots.Patterns
{
    [Bot("PivotPointsRobot")] // We create an attribute so that we don't write anything to the BotFactory
    public class PivotPointsRobot : BotPanel
    {
        private BotTabSimple _tab;

        // Basic settings
        public BotTradeRegime Regime;
        public decimal Slippage;

        // GetVolume settings 
        public decimal _volume;
        public string _volumeType;
        public string _tradeAssetInPortfolio;

        // Indicator
        private Aindicator _pivotFloor;

        // Exit settings
        public decimal Stop;

        // The last value indicator and price
        private decimal _lastPriceO;
        private decimal _lastPriceC;
        private decimal _pivotR1;
        private decimal _pivotR3;
        private decimal _pivotS1;
        private decimal _pivotS3;

        public PivotPointsRobot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Create indicator PivotFloor
            _pivotFloor = IndicatorsFactory.CreateIndicatorByName("PivotFloor", name + "PivotFloor", false);
            _pivotFloor = (Aindicator)_tab.CreateCandleIndicator(_pivotFloor, "Prime");
            _pivotFloor.Save();

            // Settings
            Slippage = 0;
            _volume = 1;
            Stop = 0.5m; 
            _volumeType = "Deposit percent";

            Load();

            DeleteEvent += Strategy_DeleteEvent;
            
            _tab.CandleFinishedEvent += TradeLogic;

            Description = OsLocalization.Description.DescriptionLabel75;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "PivotPointsRobot";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
            PivotPointsRobotUi ui = new PivotPointsRobotUi(this);
            ui.ShowDialog();
        }

        // Save settings
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(_volumeType);
                    writer.WriteLine(_tradeAssetInPortfolio);
                    writer.WriteLine(Slippage);
                    writer.WriteLine(_volume);
                    writer.WriteLine(Regime);
                    writer.WriteLine(Stop);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        // Load settings
        private void Load()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    _volumeType = Convert.ToString(reader.ReadLine());
                    _tradeAssetInPortfolio = Convert.ToString(reader.ReadLine());
                    Slippage = Convert.ToDecimal(reader.ReadLine());
                    _volume = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Stop = Convert.ToDecimal(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        // Delete save file
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // logic
        private void TradeLogic(List<Candle> candles)
        {
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            _lastPriceO = candles[candles.Count - 1].Open;
            _lastPriceC = candles[candles.Count - 1].Close;
            _pivotR1 = _pivotFloor.DataSeries[1].Last;
            _pivotS1 = _pivotFloor.DataSeries[4].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);
                }
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }

            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        // Logic open position
        private void LogicOpenPosition(List<Candle> candles, List<Position> openPositions)
        {
            if (_lastPriceC > _pivotR1 && _lastPriceO < _pivotR1 && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(GetVolume(_tab), _lastPriceC + Slippage);
                _pivotR3 = _pivotFloor.DataSeries[3].Last;
            }

            if (_lastPriceC < _pivotS1 && _lastPriceO > _pivotS1 && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(GetVolume(_tab), _lastPriceC - Slippage);
                _pivotS3 = _pivotFloor.DataSeries[6].Last;
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles, Position openPosition)
        {
            if (openPosition.Direction == Side.Buy)
            {
                if (_lastPriceC > _pivotR3)
                {
                    _tab.CloseAtLimit(openPosition, _lastPriceC - Slippage, openPosition.OpenVolume);
                }

                if (_lastPriceC < openPosition.EntryPrice - openPosition.EntryPrice / 100m * Stop)
                {
                    _tab.CloseAtMarket(openPosition, openPosition.OpenVolume);
                }
            }

            if (openPosition.Direction == Side.Sell)
            {
                if (_lastPriceC < _pivotS3)
                {
                    _tab.CloseAtLimit(openPosition, _lastPriceC + Slippage, openPosition.OpenVolume);
                }

                if (_lastPriceC > openPosition.EntryPrice + openPosition.EntryPrice / 100m * Stop)
                {
                    _tab.CloseAtMarket(openPosition, openPosition.OpenVolume);
                }
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (_volumeType == "Contracts")
            {
                volume = _volume;
            }
            else if (_volumeType == "Contract currency")
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
            else if (_volumeType == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (_tradeAssetInPortfolio == "Prime")
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
                        if (positionOnBoard[i].SecurityNameCode == _tradeAssetInPortfolio)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio, Logging.LogMessageType.Error);
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
}