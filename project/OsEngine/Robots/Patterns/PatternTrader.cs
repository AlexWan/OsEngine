/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsMiner;
using OsEngine.OsMiner.Patterns;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.IO;

namespace OsEngine.Robots.Patterns
{
    [Bot("PatternTrader")] // We create an attribute so that we don't write anything to the BotFactory
    public class PatternTrader : BotPanel
    {
        private BotTabSimple _tab;

        // Basic settings
        public BotTradeRegime Regime;
        public Side SideInter;
        public decimal WeightToInter;
        public int InterToPatternSleepage;
        public int MaxPosition;

        // GetVolume settings
        public decimal _volume;
        public string _volumeType;
        public string _tradeAssetInPortfolio;

        // Patterns settings
        public string NameGroupPatternsToTrade;
        public string NameSetToTrade;

        // Exit settings
        public decimal WeightToExit;
        public bool StopOrderIsOn;
        public decimal StopOrderValue;
        public int StopOrderSleepage;
        public bool ProfitOrderIsOn;
        public decimal ProfitOrderValue;
        public int ProfitOrderSleepage;
        public bool ExitFromSomeCandlesIsOn;
        public int ExitFromSomeCandlesValue;
        public int ExitToPatternsSleepage;
        public decimal TreilingStopValue;
        public bool TrailingStopIsOn;
        public int TreilingStopSleepage;
        public int ExitFromSomeCandlesSleepage;

        // pattern store
        private OsMinerMaster _minerMaster;
        
        // List patterns
        public List<IPattern> PatternsToOpen = new List<IPattern>();
        public List<IPattern> PatternsToClose = new List<IPattern>();

        public PatternTrader(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
            _minerMaster = new OsMinerMaster();
            _minerMaster.LogMessageEvent += _minerMaster_LogMessageEvent;

            DeleteEvent += Strategy_DeleteEvent;

            // Basic settings
            Regime= BotTradeRegime.Off;
            MaxPosition = 3;
            WeightToInter = 1;

            // GetVolume settings
            _volumeType = "Deposit percent";
            _volume = 1;

            // Exit settings
            WeightToExit = 1;
            StopOrderIsOn = false;
            StopOrderValue = 20;
            StopOrderSleepage = 0;
            ProfitOrderIsOn = false;
            ProfitOrderValue = 20;
            ProfitOrderSleepage = 0;
            ExitFromSomeCandlesIsOn = false;
            ExitFromSomeCandlesValue = 10;
            ExitFromSomeCandlesSleepage = 0;
            TrailingStopIsOn = false;
            TreilingStopValue = 20;
            TreilingStopSleepage = 0;
            
            Load();

            if (NameGroupPatternsToTrade != null)
            {
                GetPatterns();
            }

            Description = "Pattern trading robot";
        }

        // incoming message from the pattern store
        void _minerMaster_LogMessageEvent(string message, Logging.LogMessageType messageType)
        {
            _tab.SetNewLogMessage(message, messageType);
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "PatternTrader";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
            PatternTraderUi ui = new PatternTraderUi(this);
            ui.ShowDialog();
        }

        // Update patterns
        public void GetPatterns()
        {
            PatternsToOpen = _minerMaster.GetPatternsToInter(NameGroupPatternsToTrade);
            PatternsToClose = _minerMaster.GetPatternsToExit(NameGroupPatternsToTrade);
        }

        // Take list of pattern groups from storage
        public List<string> GetListPatternsNames(string nameSet)
        {
            return _minerMaster.GetListPatternsNames(nameSet);
        }

        // Take a list of set names
        public List<string> GetListSetsName()
        {
            List<string> names = new List<string>();

            for (int i = 0; i < _minerMaster.Sets.Count; i++)
            {
                names.Add(_minerMaster.Sets[i].Name);
            }
            return names;
        }

        // work with file system

        // Save settings
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false))
                {
                    writer.WriteLine(Regime);
                    writer.WriteLine(SideInter);
                    writer.WriteLine(WeightToInter);
                    writer.WriteLine(WeightToExit);
                    writer.WriteLine(StopOrderIsOn);
                    writer.WriteLine(StopOrderValue);
                    writer.WriteLine(StopOrderSleepage);
                    writer.WriteLine(ProfitOrderIsOn);
                    writer.WriteLine(ProfitOrderValue);
                    writer.WriteLine(ProfitOrderSleepage);
                    writer.WriteLine(ExitFromSomeCandlesIsOn);
                    writer.WriteLine(ExitFromSomeCandlesValue);
                    writer.WriteLine(ExitFromSomeCandlesSleepage);
                    writer.WriteLine(TrailingStopIsOn);
                    writer.WriteLine(TreilingStopValue);
                    writer.WriteLine(TreilingStopSleepage);
                    writer.WriteLine(NameGroupPatternsToTrade);
                    writer.WriteLine(InterToPatternSleepage);
                    writer.WriteLine(ExitToPatternsSleepage);
                    writer.WriteLine(MaxPosition );
                    writer.WriteLine(_volume);
                    writer.WriteLine(NameSetToTrade);
                    writer.WriteLine(_volumeType);
                    writer.WriteLine(_tradeAssetInPortfolio);
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
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Enum.TryParse(reader.ReadLine(), true, out SideInter);

                    WeightToInter = Convert.ToDecimal(reader.ReadLine());
                    WeightToExit = Convert.ToDecimal(reader.ReadLine());
                    StopOrderIsOn = Convert.ToBoolean(reader.ReadLine());
                    StopOrderValue = Convert.ToDecimal(reader.ReadLine());
                    StopOrderSleepage = Convert.ToInt32(reader.ReadLine());
                    ProfitOrderIsOn = Convert.ToBoolean(reader.ReadLine());
                    ProfitOrderValue = Convert.ToDecimal(reader.ReadLine());
                    ProfitOrderSleepage = Convert.ToInt32(reader.ReadLine());
                    ExitFromSomeCandlesIsOn = Convert.ToBoolean(reader.ReadLine());
                    ExitFromSomeCandlesValue = Convert.ToInt32(reader.ReadLine());
                    ExitFromSomeCandlesSleepage = Convert.ToInt32(reader.ReadLine());
                    TrailingStopIsOn = Convert.ToBoolean(reader.ReadLine());
                    TreilingStopValue = Convert.ToDecimal(reader.ReadLine());
                    TreilingStopSleepage = Convert.ToInt32(reader.ReadLine());
                    NameGroupPatternsToTrade = reader.ReadLine();
                    InterToPatternSleepage = Convert.ToInt32(reader.ReadLine());
                    ExitToPatternsSleepage = Convert.ToInt32(reader.ReadLine());
                    MaxPosition = Convert.ToInt32(reader.ReadLine());
                    _volume = Convert.ToDecimal(reader.ReadLine());
                    NameSetToTrade = reader.ReadLine();
                    _volumeType = reader.ReadLine();
                    _tradeAssetInPortfolio = reader.ReadLine();
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        // Delete file with save
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // Trading logic

        // Candle completion event
        void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            List<Position> positions = _tab.PositionsOpenAll;

            if (positions.Count < MaxPosition)
            {
                if (CheckInter(PatternsToOpen, candles, candles.Count - 1, WeightToInter))
                {
                    InterInNewPosition(candles[candles.Count-1].Close);
                }
            }

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                decimal priceExit;

                priceExit = CheckExit(positions[i], PatternsToClose, candles, candles.Count - 1, candles[candles.Count-1].Close);

                if (priceExit == 0)
                {
                    continue;
                }

                _tab.CloseAtLimit(positions[i],priceExit,positions[i].OpenVolume);
            }
        }

        // Enter position
        private void InterInNewPosition(decimal price)
        {
            if (SideInter == Side.Buy)
            {
                _tab.BuyAtLimit(GetVolume(_tab), price + _tab.Security.PriceStep * InterToPatternSleepage);
            }
            else
            {
                _tab.SellAtLimit(GetVolume(_tab), price - _tab.Security.PriceStep * InterToPatternSleepage);
            }
        }

        // Event of opening a new position
        void _tab_PositionOpeningSuccesEvent(Position position)
        {
            if (ProfitOrderIsOn)
            {
                if (position.Direction == Side.Buy)
                {
                    decimal stopPrice = position.EntryPrice + position.EntryPrice * (ProfitOrderValue / 100);
                    decimal stopOrderPrice = stopPrice - _tab.Security.PriceStep * ProfitOrderSleepage;

                    _tab.CloseAtProfit(position, stopPrice, stopOrderPrice);
                }
                else if (position.Direction == Side.Sell)
                {
                    decimal stopPrice = position.EntryPrice - position.EntryPrice * (ProfitOrderValue / 100);
                    decimal stopOrderPrice = stopPrice + _tab.Security.PriceStep * ProfitOrderSleepage;

                    _tab.CloseAtProfit(position, stopPrice, stopOrderPrice);
                }
            }

            if (StopOrderIsOn)
            {
                if (position.Direction == Side.Buy)
                {
                    decimal stopPrice = position.EntryPrice - position.EntryPrice * (StopOrderValue/100);
                    decimal stopOrderPrice = stopPrice - _tab.Security.PriceStep * StopOrderSleepage;

                    _tab.CloseAtStop(position, stopPrice, stopOrderPrice);
                }
                else if (position.Direction == Side.Sell)
                {
                    decimal stopPrice = position.EntryPrice + position.EntryPrice * (StopOrderValue / 100);
                    decimal stopOrderPrice = stopPrice + _tab.Security.PriceStep * StopOrderSleepage;

                    _tab.CloseAtStop(position, stopPrice, stopOrderPrice);
                }
            }
        }

        // Check patterns at entry
        private bool CheckInter(List<IPattern> patterns, List<Candle> series, int index, decimal weightToInterOrExit)
        {
            if (patterns == null ||
                patterns.Count == 0)
            {
                return false;
            }

            decimal weight = 0;

            for (int i = 0; i < patterns.Count; i++)
            {
                if (patterns[i].ThisIsIt(series, _tab.Indicators, index))
                {
                    weight += patterns[i].Weight;
                }
            }

            if (weight >= weightToInterOrExit)
            {
                return true;
            }

            return false;
        }

        // Check out of position
        private decimal CheckExit(Position position, List<IPattern> patterns, List<Candle> candles, int index, decimal price)
        {
            if (CheckInter(patterns, candles, index, WeightToExit))
            {
                return GetPriceExit(position,price,ExitToPatternsSleepage);
            }

            if (TrailingStopIsOn)
            {
                if (position.Direction == Side.Buy)
                {
                    decimal newTrail = candles[candles.Count - 1].Close - candles[candles.Count - 1].Close * (TreilingStopValue / 100);

                    _tab.CloseAtTrailingStop(position,newTrail,newTrail - _tab.Security.PriceStep * StopOrderSleepage);
                }
                else
                {
                    decimal newTrail = candles[candles.Count - 1].Close + candles[candles.Count - 1].Close * (TreilingStopValue / 100);

                    _tab.CloseAtTrailingStop(position, newTrail, newTrail + _tab.Security.PriceStep * StopOrderSleepage);
                }
            }

            if (ExitFromSomeCandlesIsOn)
            {
                if (GetIndexInter(position.TimeOpen, candles) + ExitFromSomeCandlesValue <= index)
                {
                    return GetPriceExit(position, price,ExitFromSomeCandlesSleepage);
                }
            }

            return 0;
        }

        // Take the candle index by time
        private int GetIndexInter(DateTime time, List<Candle> candles)
        {
            for (int i = candles.Count - 1; i > 0; i--)
            {
                if (candles[i].TimeStart <= time)
                {
                    return i;
                }
            }

            return 0;
        }

        // Take the exit price
        private decimal GetPriceExit(Position position, decimal price, int sleepage)
        {
            if (position.Direction == Side.Buy)
            {
                return price - _tab.Security.PriceStep*sleepage;
            }
            else
            {
                return price + _tab.Security.PriceStep * sleepage;
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