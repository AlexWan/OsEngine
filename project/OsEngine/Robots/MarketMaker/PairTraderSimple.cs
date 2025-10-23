/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

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

/*
Pair trading robot for OsEngime

Robot for pair trading. trading two papers based on their acceleration to each other by candle.
*/

namespace OsEngine.Robots.MarketMaker
{
    [Bot("PairTraderSimple")] // We create an attribute so that we don't write anything to the BotFactory
    public class PairTraderSimple : BotPanel
    {
        private BotTabSimple _tab1;
        private BotTabSimple _tab2;

        // Basic settings
        public BotTradeRegime Regime;
        public int CountCandles;
        public decimal Slippage1;
        public decimal Slippage2;
        public decimal SpreadDeviation;

        // GetVolume settings
        public decimal Volume1;
        public decimal Volume2;
        public string TradeAssetInPortfolio1;
        public string TradeAssetInPortfolio2;
        public string VolumeType1;
        public string VolumeType2;
        
        // Exit settings
        public decimal Loss;
        public decimal Profit;

        // Position list
        private List<PairDealStausSaver> _positionNumbers;

        // Ready candles tab1 and tab2
        private List<Candle> _candles1;
        private List<Candle> _candles2;

        public PairTraderSimple(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab1 = TabsSimple[0];
            _tab1.CandleFinishedEvent += _tab1_CandleFinishedEvent;

            TabCreate(BotTabType.Simple);
            _tab2 = TabsSimple[1];
            _tab2.CandleFinishedEvent += _tab2_CandleFinishedEvent;

            Volume1 = 1;
            Volume2 = 1;

            VolumeType1 = "Deposit percent";
            VolumeType2 = "Deposit percent";

            Slippage1 = 0;
            Slippage2 = 0;

            CountCandles = 5;
            SpreadDeviation = 1m;

            Loss = 0.5m;
            Profit = 0.5m;
            _positionNumbers = new List<PairDealStausSaver>();
            Load();

            DeleteEvent += Strategy_DeleteEvent;

            Description = OsLocalization.Description.DescriptionLabel49;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "PairTraderSimple";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
            PairTraderSimpleUi ui = new PairTraderSimpleUi(this);
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
                    writer.WriteLine(VolumeType1);
                    writer.WriteLine(TradeAssetInPortfolio1);

                    writer.WriteLine(VolumeType2);
                    writer.WriteLine(TradeAssetInPortfolio2);

                    writer.WriteLine(Regime);
                    writer.WriteLine(Volume1);
                    writer.WriteLine(Volume2);

                    writer.WriteLine(Slippage1);
                    writer.WriteLine(Slippage2);

                    writer.WriteLine(CountCandles);
                    writer.WriteLine(SpreadDeviation);

                    writer.WriteLine(Loss);
                    writer.WriteLine(Profit);

                    string positions = "";

                    for (int i = 0; _positionNumbers != null && i < _positionNumbers.Count; i++)
                    {
                        positions += _positionNumbers[i].NumberPositions + "$" + _positionNumbers[i].Spred + "%";
                    }

                    writer.WriteLine(positions);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        // load settins from .txt file
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
                    VolumeType1 = Convert.ToString(reader.ReadLine());
                    TradeAssetInPortfolio1 = Convert.ToString(reader.ReadLine());

                    VolumeType2 = Convert.ToString(reader.ReadLine());
                    TradeAssetInPortfolio2 = Convert.ToString(reader.ReadLine());

                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Volume1 = Convert.ToDecimal(reader.ReadLine());
                    Volume2 = Convert.ToDecimal(reader.ReadLine());

                    Slippage1 = Convert.ToDecimal(reader.ReadLine());
                    Slippage2 = Convert.ToDecimal(reader.ReadLine());

                    CountCandles = Convert.ToInt32(reader.ReadLine());

                    SpreadDeviation = Convert.ToDecimal(reader.ReadLine());

                    Loss = Convert.ToDecimal(reader.ReadLine());
                    Profit = Convert.ToDecimal(reader.ReadLine());

                    string[] positions = reader.ReadLine().Split('%');
                    if (positions.Length != 0)
                    {
                        for (int i = 0; i < positions.Length; i++)
                        {
                            string[] pos = positions[i].Split('$');

                            if (pos.Length == 2)
                            {
                                PairDealStausSaver save = new PairDealStausSaver();
                                save.NumberPositions.Add(Convert.ToInt32(pos[0]));
                                save.Spred = Convert.ToDecimal(pos[1]);
                                _positionNumbers.Add(save);
                            }
                        }
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        // delete save file
        void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // New candles in tab1
        void _tab1_CandleFinishedEvent(List<Candle> candles)
        {
            _candles1 = candles;

            if (_candles2 == null ||
                _candles2[_candles2.Count - 1].TimeStart != _candles1[_candles1.Count - 1].TimeStart)
            {
                return;
            }

            CheckExit();
            Trade();
        }

        // New candles tab2
        void _tab2_CandleFinishedEvent(List<Candle> candles)
        {
            _candles2 = candles;

            if (_candles1 == null ||
                _candles2[_candles2.Count - 1].TimeStart != _candles1[_candles1.Count - 1].TimeStart)
            {
                return;
            }

            Trade();
            CheckExit();
        }

        // Enter position logic
        private void Trade()
        {
            if (_candles1.Count - 1 - CountCandles < 1)
            {
                return;
            }

            if (_candles1.Count < 10)
            {
                _positionNumbers = new List<PairDealStausSaver>();
                return;
            }

            if (_positionNumbers == null)
            {
                _positionNumbers = new List<PairDealStausSaver>();
            }

            decimal movePersent1 = 100 / _candles1[_candles1.Count - 1 - CountCandles].Close *
                                   _candles1[_candles1.Count - 1].Close;

            decimal movePersent2 = 100 / _candles2[_candles2.Count - 1 - CountCandles].Close *
                                   _candles2[_candles2.Count - 1].Close;

            if (movePersent1 > movePersent2 &&
                movePersent1 - movePersent2 > SpreadDeviation)
            {
                List<Position> positons1 = _tab1.PositionOpenShort;

                if (positons1 == null || positons1.Count == 0)
                {
                    Position pos1 = _tab1.SellAtLimit(GetVolume(_tab1, Volume1, VolumeType1, TradeAssetInPortfolio1), _candles1[_candles1.Count - 1].Close - Slippage1);
                    Position pos2 = _tab2.BuyAtLimit(GetVolume(_tab2, Volume2, VolumeType2, TradeAssetInPortfolio2), _candles2[_candles2.Count - 1].Close + Slippage2);

                    PairDealStausSaver saver = new PairDealStausSaver();
                    saver.Spred = movePersent1 - movePersent2;
                    saver.NumberPositions.Add(pos1.Number);
                    saver.NumberPositions.Add(pos2.Number);
                    _positionNumbers.Add(saver);
                }
            }

            if (movePersent2 > movePersent1 &&
                movePersent2 - movePersent1 > SpreadDeviation)
            {
                List<Position> positons2 = _tab2.PositionOpenShort;

                if (positons2 == null || positons2.Count == 0)
                {
                    Position pos1 = _tab2.SellAtLimit(GetVolume(_tab2, Volume2, VolumeType2, TradeAssetInPortfolio2), _candles2[_candles2.Count - 1].Close - Slippage2);
                    Position pos2 = _tab1.BuyAtLimit(GetVolume(_tab1, Volume1, VolumeType1, TradeAssetInPortfolio1), _candles1[_candles1.Count - 1].Close + Slippage1);

                    PairDealStausSaver saver = new PairDealStausSaver();
                    saver.Spred = movePersent2 - movePersent1;
                    saver.NumberPositions.Add(pos1.Number);
                    saver.NumberPositions.Add(pos2.Number);
                    _positionNumbers.Add(saver);
                }
            }
        }

        // Exit position logic
        private void CheckExit()
        {
            if (_candles1.Count - 1 - CountCandles < 1)
            {
                return;
            }

            decimal movePersent1 = 100 / _candles1[_candles1.Count - 1 - CountCandles].Close *
                       _candles1[_candles1.Count - 1].Close;

            decimal movePersent2 = 100 / _candles2[_candles2.Count - 1 - CountCandles].Close *
                                   _candles2[_candles2.Count - 1].Close;

            decimal spredNow = Math.Abs(movePersent1 - movePersent2);

            for (int i = 0; _positionNumbers != null && i < _positionNumbers.Count; i++)
            {
                PairDealStausSaver pairDeal = _positionNumbers[i];

                if (spredNow > pairDeal.Spred &&
                    spredNow - pairDeal.Spred > Loss)
                {
                    NeedToClose(pairDeal.NumberPositions[0]);
                    NeedToClose(pairDeal.NumberPositions[1]);
                    _positionNumbers.Remove(pairDeal);
                    i--;
                    continue;
                }

                if (pairDeal.Spred > spredNow &&
                    pairDeal.Spred - spredNow > Profit)
                {
                    NeedToClose(pairDeal.NumberPositions[0]);
                    NeedToClose(pairDeal.NumberPositions[1]);
                    _positionNumbers.Remove(pairDeal);
                    i--;
                }
            }
        }

        // Close position
        private void NeedToClose(int positionNum)
        {
            Position pos;

            pos = _tab1.PositionsOpenAll.Find(position => position.Number == positionNum);

            if (pos != null)
            {
                decimal price;

                if (pos.Direction == Side.Buy)
                {
                    price = _tab1.CandlesAll[_tab1.CandlesAll.Count - 1].Close - _tab1.Security.PriceStep * 10;
                }
                else
                {
                    price = _tab1.CandlesAll[_tab1.CandlesAll.Count - 1].Close + _tab1.Security.PriceStep * 10;
                }

                _tab1.CloseAtLimit(pos, price, pos.OpenVolume);

                return;
            }

            pos = _tab2.PositionsOpenAll.Find(position => position.Number == positionNum);

            if (pos != null)
            {
                decimal price;

                if (pos.Direction == Side.Buy)
                {
                    price = _tab2.CandlesAll[_tab2.CandlesAll.Count - 1].Close - _tab2.Security.PriceStep * 10;
                }
                else
                {
                    price = _tab2.CandlesAll[_tab2.CandlesAll.Count - 1].Close + _tab2.Security.PriceStep * 10;
                }

                _tab2.CloseAtLimit(pos, price, pos.OpenVolume);
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab, decimal Volume, string VolumeType, string TradeAssetInPortfolio)
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

    public class PairDealStausSaver
    {
        // num position
        public List<int> NumberPositions = new List<int>();

        // spread in time inter
        public decimal Spred;
    }
}