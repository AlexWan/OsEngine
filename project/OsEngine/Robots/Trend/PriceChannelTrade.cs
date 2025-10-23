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

/* Description
Trading robot for osengine.

Trend strategy on interseption PriceChannel indicator. 

Buy:
If PriceHigh > _lastPriceChUp - close position and open Long.

Sell: 
If PriceLow < _lastPriceChDown - close position and open Short.
 */

namespace OsEngine.Robots.Trend
{
    [Bot("PriceChannelTrade")] // We create an attribute so that we don't write anything to the BotFactory
    public class PriceChannelTrade : BotPanel
    {
        private BotTabSimple _tab;

        // Basic settings
        public BotTradeRegime Regime;
        public decimal Slippage;
        
        // GetVolume settings
        public decimal Volume;
        public string VolumeType;
        public string TradeAssetInPortfolio;

        // Indicator
        private Aindicator _priceChannel;

        // The last value of the indicator and price
        private decimal _lastPriceC;
        private decimal _lastPriceH;
        private decimal _lastPriceL;
        private decimal _lastPriceChUp;
        private decimal _lastPriceChDown;

        public PriceChannelTrade(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            Slippage = 0;
            VolumeType = "Deposit percent";
            Volume = 1;

            // Create indicator PriceChannel
            _priceChannel = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PC", false);
            _priceChannel = (Aindicator)_tab.CreateCandleIndicator(_priceChannel, "Prime");
            _priceChannel.Save();

            Load();

            // Subscribe to the strategy delete event
            DeleteEvent += Strategy_DeleteEvent;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel117;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "PriceChannelTrade";
        }
        
        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
            PriceChannelTradeUi ui = new PriceChannelTradeUi(this);
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
                    writer.WriteLine(VolumeType);
                    writer.WriteLine(TradeAssetInPortfolio);
                    writer.WriteLine(Volume);
                    writer.WriteLine(Slippage);
                    writer.WriteLine(Regime);

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
                    VolumeType = reader.ReadLine();
                    TradeAssetInPortfolio = reader.ReadLine();
                    Volume = reader.ReadLine().ToDecimal();
                    Slippage = reader.ReadLine().ToDecimal();
                    Enum.TryParse(reader.ReadLine(), true, out Regime);

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

        // Logic
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_priceChannel.DataSeries[0].Values == null)
            {
                return;
            }

            _lastPriceC = candles[candles.Count - 1].Close;
            _lastPriceH = candles[candles.Count - 1].High;
            _lastPriceL = candles[candles.Count - 1].Low;
            _lastPriceChUp = _priceChannel.DataSeries[0].Values[_priceChannel.DataSeries[0].Values.Count - 2];
            _lastPriceChDown = _priceChannel.DataSeries[1].Values[_priceChannel.DataSeries[1].Values.Count - 2];

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
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_lastPriceH > _lastPriceChUp &&
                _lastPriceL < _lastPriceChDown)
            {
                return;
            }

            if (_lastPriceH > _lastPriceChUp && Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(GetVolume(_tab), _lastPriceC + Slippage);
            }

            if (_lastPriceL < _lastPriceChDown && Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(GetVolume(_tab), _lastPriceC - Slippage);
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if(position.State != PositionStateType.Open)
            {
                return;
            }

            if (position.Direction == Side.Buy)
            {
                if (_lastPriceL < _lastPriceChDown)
                {
                    _tab.CloseAtLimit(position, _lastPriceC - Slippage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyLong 
                        && Regime != BotTradeRegime.OnlyClosePosition
                        && _tab.PositionsOpenAll.Count < 3)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _lastPriceC - Slippage);
                    }
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastPriceH > _lastPriceChUp)
                {
                    _tab.CloseAtLimit(position, _lastPriceC + Slippage, position.OpenVolume);

                    if (Regime != BotTradeRegime.OnlyShort && Regime 
                        != BotTradeRegime.OnlyClosePosition
                        && _tab.PositionsOpenAll.Count < 3)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _lastPriceC + Slippage);
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