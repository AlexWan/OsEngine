/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

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
using System.Drawing;
using System.IO;

/*
pair trading robot building spread and trading based on the intersection of MA on the spread chart

SmaLong crossed SmaShort from top to bottom - the first tab sells, the second one buys.

SmaLong crossed SmaShort from the bottom up - the first one is buying, the second one is selling.

Exit: on the opposite signal
*/

namespace OsEngine.Robots.MarketMaker
{
    [Bot("PairTraderSpreadSma")] // We create an attribute so that we don't write anything to the BotFactory
    public class PairTraderSpreadSma : BotPanel
    {   
        private BotTabSimple _tab1;
        private BotTabSimple _tab2;
        private BotTabIndex _tabSpread;

        // Basic settings
        public BotTradeRegime Regime;
        public decimal Slippage1;
        public decimal Slippage2;

        // GetVolume settings
        public decimal Volume1;
        public decimal Volume2;
        public string TradeAssetInPortfolio1;
        public string TradeAssetInPortfolio2;
        public string VolumeType1;
        public string VolumeType2;

        // List candles
        private List<Candle> _candles1;
        private List<Candle> _candles2;
        private List<Candle> _candlesSpread;

        // Indicator
        private Aindicator _smaLong;
        private Aindicator _smaShort;

        public PairTraderSpreadSma(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab1 = TabsSimple[0];
            _tab1.CandleFinishedEvent += _tab1_CandleFinishedEvent;

            TabCreate(BotTabType.Simple);
            _tab2 = TabsSimple[1];
            _tab2.CandleFinishedEvent += _tab2_CandleFinishedEvent;

            TabCreate(BotTabType.Index);
            _tabSpread = TabsIndex[0];
            _tabSpread.SpreadChangeEvent += _tabSpread_SpreadChangeEvent;

            // Create indicator SmaLong
            _smaLong = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaLong", false);
            _smaLong = (Aindicator)_tabSpread.CreateCandleIndicator(_smaLong, "Prime");
            ((IndicatorParameterInt)_smaLong.Parameters[0]).ValueInt = 22;
            _smaLong.DataSeries[0].Color = Color.DodgerBlue;
            _smaLong.Save();

            // Create indicator SmaShort
            _smaShort = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaShort", false);
            _smaShort = (Aindicator)_tabSpread.CreateCandleIndicator(_smaShort, "Prime");
            _smaShort.DataSeries[0].Color = System.Drawing.Color.Yellow;
            ((IndicatorParameterInt)_smaShort.Parameters[0]).ValueInt = 3;
            _smaLong.DataSeries[0].Color = Color.DarkRed;
            _smaShort.Save();

            Volume1 = 1;
            Volume2 = 1;

            VolumeType1 = "Deposit percent";
            VolumeType2 = "Deposit percent";

            Slippage1 = 0;
            Slippage2 = 0;

            Load();

            DeleteEvent += Strategy_DeleteEvent;

            Description = OsLocalization.Description.DescriptionLabel50;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "PairTraderSpreadSma";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
            PairTraderSpreadSmaUi ui = new PairTraderSpreadSmaUi(this);
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
                    writer.WriteLine(VolumeType1);
                    writer.WriteLine(TradeAssetInPortfolio1);

                    writer.WriteLine(VolumeType2);
                    writer.WriteLine(TradeAssetInPortfolio2);

                    writer.WriteLine(Regime);
                    writer.WriteLine(Volume1);
                    writer.WriteLine(Volume2);

                    writer.WriteLine(Slippage1);
                    writer.WriteLine(Slippage2);
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
                    VolumeType1 = Convert.ToString(reader.ReadLine());
                    TradeAssetInPortfolio1 = Convert.ToString(reader.ReadLine());

                    VolumeType2 = Convert.ToString(reader.ReadLine());
                    TradeAssetInPortfolio2 = Convert.ToString(reader.ReadLine());

                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Volume1 = Convert.ToDecimal(reader.ReadLine());
                    Volume2 = Convert.ToDecimal(reader.ReadLine());

                    Slippage1 = Convert.ToDecimal(reader.ReadLine());
                    Slippage2 = Convert.ToDecimal(reader.ReadLine());

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

        // Logic tab 1
        void _tab1_CandleFinishedEvent(List<Candle> candles)
        {
            _candles1 = candles;

            if (_candles2 == null || _candlesSpread == null ||
                _candles2[_candles2.Count - 1].TimeStart != _candles1[_candles1.Count - 1].TimeStart ||
                _candles1[_candles1.Count - 1].TimeStart != _candlesSpread[_candlesSpread.Count - 1].TimeStart)
            {
                return;
            }

            CheckExit();
            Trade();
        }

        // Logic tab 2
        void _tab2_CandleFinishedEvent(List<Candle> candles)
        {
            _candles2 = candles;

            if (_candles1 == null || _candlesSpread == null ||
                _candles2[_candles2.Count - 1].TimeStart != _candles1[_candles1.Count - 1].TimeStart ||
                _candles2[_candles2.Count - 1].TimeStart != _candlesSpread[_candlesSpread.Count - 1].TimeStart)
            {
                return;
            }

            Trade();
            CheckExit();
        }

        // Tab index new candles
        void _tabSpread_SpreadChangeEvent(List<Candle> candles)
        {
            _candlesSpread = candles;

            if (_candles2 == null || _candles1 == null ||
                _candlesSpread[_candlesSpread.Count - 1].TimeStart != _candles1[_candles1.Count - 1].TimeStart ||
                _candlesSpread[_candlesSpread.Count - 1].TimeStart != _candles2[_candles2.Count - 1].TimeStart)
            {
                return;
            }

            CheckExit();
            Trade();
        }

        // Open position logic
        private void Trade()
        {
            //1 if the short MA on the spread crossed the long MA
            if (_candles1.Count < 10)
            {
                return;
            }

            if (StartProgram == StartProgram.IsOsTrader && DateTime.Now.Hour < 10)
            {
                return;
            }

            List<Position> positions = _tab1.PositionsOpenAll;

            if (positions != null && positions.Count != 0)
            {
                return;
            }

            if (_smaShort.DataSeries[0].Values == null)
            {
                return;
            }

            decimal smaShortNow = _smaShort.DataSeries[0].Last;
            decimal smaShortLast = _smaShort.DataSeries[0].Values[_smaShort.DataSeries[0].Values.Count - 2];
            decimal smaLongNow = _smaLong.DataSeries[0].Last;
            decimal smaLongLast = _smaLong.DataSeries[0].Values[_smaLong.DataSeries[0].Values.Count - 2];

            if (smaShortNow == 0 || smaLongNow == 0
                || smaShortLast == 0 || smaLongLast == 0)
            {
                return;
            }

            if (smaShortLast < smaLongLast &&
                smaShortNow > smaLongNow)
            {
                // crossed up
                _tab1.SellAtLimit(GetVolume(_tab1, Volume1, VolumeType1, TradeAssetInPortfolio1), _candles1[_candles1.Count - 1].Close - Slippage1);
                _tab2.BuyAtLimit(GetVolume(_tab2, Volume2, VolumeType2, TradeAssetInPortfolio2), _candles2[_candles2.Count - 1].Close + Slippage2);
            }

            if (smaShortLast > smaLongLast &&
                smaShortNow < smaLongNow)
            {
                //crossed down
                _tab2.SellAtLimit(GetVolume(_tab2, Volume2, VolumeType2, TradeAssetInPortfolio2), _candles2[_candles2.Count - 1].Close - Slippage2);
                _tab1.BuyAtLimit(GetVolume(_tab1, Volume1, VolumeType1, TradeAssetInPortfolio1), _candles1[_candles1.Count - 1].Close + Slippage1);
            }
        }

        // Check exit from position
        private void CheckExit()
        {
            List<Position> positions = _tab1.PositionsOpenAll;

            if (positions == null || positions.Count == 0)
            {
                return;
            }

            decimal smaShortNow = _smaShort.DataSeries[0].Last;
            decimal smaShortLast = _smaShort.DataSeries[0].Values[_smaShort.DataSeries[0].Values.Count - 2];
            decimal smaLongNow = _smaLong.DataSeries[0].Last;
            decimal smaLongLast = _smaLong.DataSeries[0].Values[_smaLong.DataSeries[0].Values.Count - 2];

            if (smaShortNow == 0 || smaLongNow == 0
                || smaShortLast == 0 || smaLongLast == 0)
            {
                return;
            }

            if (smaShortLast < smaLongLast &&
                smaShortNow > smaLongNow)
            {
                List<Position> positions1 = _tab1.PositionOpenLong;
                List<Position> positions2 = _tab2.PositionOpenShort;

                if (positions1 != null && positions1.Count != 0)
                {
                    Position pos1 = positions1[0];
                    _tab1.CloseAtLimit(pos1, _tab1.PriceBestBid - Slippage1, pos1.OpenVolume);
                }

                if (positions2 != null && positions2.Count != 0)
                {
                    Position pos2 = positions2[0];
                    _tab2.CloseAtLimit(pos2, _tab2.PriceBestAsk + Slippage1, pos2.OpenVolume);
                }
            }

            if (smaShortLast > smaLongLast &&
                smaShortNow < smaLongNow)
            {
                List<Position> positions1 = _tab1.PositionOpenShort;
                List<Position> positions2 = _tab2.PositionOpenLong;

                if (positions1 != null && positions1.Count != 0)
                {
                    Position pos1 = positions1[0];

                    _tab1.CloseAtLimit(pos1, _tab1.PriceBestAsk + Slippage1, pos1.OpenVolume);
                }

                if (positions2 != null && positions2.Count != 0)
                {
                    Position pos2 = positions2[0];

                    _tab2.CloseAtLimit(pos2, _tab2.PriceBestBid - Slippage1, pos2.OpenVolume);
                }
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
}