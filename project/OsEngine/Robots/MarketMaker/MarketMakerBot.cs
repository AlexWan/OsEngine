/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.IO;

/* Description 
MarketMaker trading for OsEngine.

Buy: If the price crosses above a line from below, it is considered a buy signal.

Sell: If the price crosses below a line from above, it is considered a sell signal.

Exit: Opposite signal.
*/

namespace OsEngine.Robots.MarketMaker
{
    [Bot("MarketMakerBot")] // We create an attribute so that we don't write anything to the BotFactory
    public class MarketMakerBot : BotPanel
    {
        private BotTabSimple _tab;

        // Basic setting
        public BotTradeRegime Regime;

        // GetVolume settings
        public decimal Volume;
        public string VolumeType;
        public string TradeAssetInPortfolio;

        // Line settings
        public decimal PersentToSpreadLines;
        public bool PaintOn;

        public MarketMakerBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = BotTradeRegime.On;
            PersentToSpreadLines = 0.5m;
            Volume = 1;
            VolumeType = "Deposit percent";
            TradeAssetInPortfolio = "Prime";

            Load();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            DeleteEvent += Strategy_DeleteEvent;

            Description = OsLocalization.Description.DescriptionLabel48;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "MarketMakerBot";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
            MarketMakerBotUi ui = new MarketMakerBotUi(this);
            ui.ShowDialog();
        }

        // save settings in .txt file
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(VolumeType);
                    writer.WriteLine(TradeAssetInPortfolio);
                    writer.WriteLine(Regime);
                    writer.WriteLine(Volume);
                    writer.WriteLine(PersentToSpreadLines);
                    writer.WriteLine(PaintOn);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        // Load settins from .txt file
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
                    VolumeType = Convert.ToString(reader.ReadLine());
                    TradeAssetInPortfolio = Convert.ToString(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Volume = Convert.ToDecimal(reader.ReadLine());
                    PersentToSpreadLines = Convert.ToDecimal(reader.ReadLine());
                    PaintOn = Convert.ToBoolean(reader.ReadLine());
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

        //variables needed for trading

        private DateTime _lastReloadLineTime = DateTime.MinValue;

        private List<decimal> _lines;

        private List<LineHorisontal> _lineElements;

        // Logic
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime == BotTradeRegime.Off)
            {
                ClearLines();
                return;
            }

            if (candles.Count < 2)
            {
                return;
            }

            List<Position> openPosition = _tab.PositionsOpenAll;

            if (candles[candles.Count - 1].TimeStart.DayOfWeek == DayOfWeek.Friday &&
             candles[candles.Count - 1].TimeStart.Hour >= 18)
            {//if we have friday evening
                if (openPosition != null && openPosition.Count != 0)
                {
                    _tab.CloseAllAtMarket();
                }
                return;
            }

            if (_lastReloadLineTime == DateTime.MinValue ||
                candles[candles.Count - 1].TimeStart.DayOfWeek == DayOfWeek.Monday &&
                candles[candles.Count - 1].TimeStart.Hour < 11 &&
                _lastReloadLineTime.Day != candles[candles.Count - 1].TimeStart.Day)
            {//if we have monday morning
                _lastReloadLineTime = candles[candles.Count - 1].TimeStart;
                ReloadLines(candles);
            }

            if (PaintOn)
            {
                RepaintLines();
            }
            else
            {
                ClearLines();
            }

            if (Regime == BotTradeRegime.OnlyClosePosition)
            {
                // if the bot has the "close only" mode enabled
                return;
            }

            LogicOpenPosition(candles);
        }

        // Reload lines
        private void ReloadLines(List<Candle> candles)
        {
            _lines = new List<decimal>();

            _lines.Add(candles[candles.Count - 1].Close);

            decimal concateValue = candles[candles.Count - 1].Close / 100 * PersentToSpreadLines;

            for (int i = 1; i < 21; i++)
            {
                _lines.Add(candles[candles.Count - 1].Close - concateValue * i);
            }

            for (int i = 1; i < 21; i++)
            {
                _lines.Insert(0, candles[candles.Count - 1].Close + concateValue * i);
            }
        }

        // Redraw lines
        private void RepaintLines()
        {
            if (_lineElements == null ||
                _lines.Count != _lineElements.Count)
            { 
                _lineElements = new List<LineHorisontal>();

                for (int i = 0; i < _lines.Count; i++)
                {
                    _lineElements.Add(new LineHorisontal(NameStrategyUniq + "Line" + i, "Prime", false) { Value = _lines[i] });
                    _tab.SetChartElement(_lineElements[i]);
                }
            }
            else
            { 
                for (int i = 0; i < _lineElements.Count; i++)
                {
                    if (_lineElements[i].Value != _lines[i])
                    {
                        _lineElements[i].Value = _lines[i];
                    }
                    _lineElements[i].Refresh();
                }
            }
        }

        // Clear lines from the chart
        private void ClearLines()
        {
            if (_lineElements == null ||
                _lineElements.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _lineElements.Count; i++)
            {
                _lineElements[i].Delete();
            }
        }

        // Trade logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            if (_lines == null ||
                _lines.Count == 0)
            {
                return;
            }

            // 1 find out how much and in what direction we need to go

            decimal totalDeal = 0;

            decimal lastPrice = candles[candles.Count - 2].Close;
            decimal nowPrice = candles[candles.Count - 1].Close;

            for (int i = 0; i < _lines.Count; i++)
            {
                if (lastPrice < _lines[i] &&
                    nowPrice > _lines[i])
                { 
                    totalDeal -= GetVolume(_tab);
                }

                if (lastPrice > _lines[i] &&
                    nowPrice < _lines[i])
                { 
                    totalDeal += GetVolume(_tab);
                }
            }

            if (totalDeal == 0)
            {
                return;
            }

            // 2 go in the right direction

            if (totalDeal > 0)
            { 
                List<Position> positionsShort = _tab.PositionOpenShort;

                if (positionsShort != null && positionsShort.Count != 0)
                {
                    if (positionsShort[0].OpenVolume <= totalDeal)
                    {
                        _tab.CloseAtMarket(positionsShort[0], positionsShort[0].OpenVolume);
                    }
                    else
                    {
                        _tab.CloseAtMarket(positionsShort[0], totalDeal);
                    }
                }

                if (totalDeal > 0 && totalDeal != 0)
                {
                    List<Position> positionsLong = _tab.PositionOpenLong;

                    if (positionsLong != null && positionsLong.Count != 0)
                    {
                        if(totalDeal - positionsLong[0].OpenVolume <= 0)
                        {
                            return;
                        }
                        _tab.BuyAtMarketToPosition(positionsLong[0], totalDeal - positionsLong[0].OpenVolume);
                    }
                    else
                    {
                        _tab.BuyAtMarket(totalDeal);
                    }
                }
            }

            if (totalDeal < 0)
            {
                totalDeal = Math.Abs(totalDeal);

                List<Position> positionsLong = _tab.PositionOpenLong;

                if (positionsLong != null && positionsLong.Count != 0)
                {
                    if (positionsLong[0].OpenVolume <= totalDeal)
                    {
                        _tab.CloseAtMarket(positionsLong[0], positionsLong[0].OpenVolume);
                    }
                    else
                    {
                        _tab.CloseAtMarket(positionsLong[0], totalDeal);
                    }
                }

                if (totalDeal > 0)
                {
                    List<Position> positionsShort = _tab.PositionOpenShort;

                    if (positionsShort != null && positionsShort.Count != 0)
                    {
                        if (totalDeal - positionsShort[0].OpenVolume <= 0)
                        {
                            return;
                        }

                        _tab.SellAtMarketToPosition(positionsShort[0], totalDeal - positionsShort[0].OpenVolume);
                    }
                    else
                    {
                        _tab.SellAtMarket(totalDeal);
                    }
                }
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (VolumeType == "Contracts")
            {
                volume = Volume;
            }
            else if (VolumeType == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = Volume / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = Volume / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (VolumeType == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (TradeAssetInPortfolio == "Prime")
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
                        if (positionOnBoard[i].SecurityNameCode == TradeAssetInPortfolio)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + TradeAssetInPortfolio, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (Volume / 100);

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