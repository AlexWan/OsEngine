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

Trend strategy based on 2 indicators Sma and Stohastic. 

Buy:
If lastClose > lastSma + Step and secondLastStoh <= Downline and firstLastStoh >= Downline - Enter Long. 

Sell: 
If lastClose < lastSma - Step and secondLastStoh >= Upline and firstLastStoh <= Upline - Enter Short. 

Exit Long: lastClose < lastSma - Step.
Exit Short: lastClose > lastSma + Step.
 */

namespace OsEngine.Robots.Trend
{
    [Bot("SmaStochastic")] // We create an attribute so that we don't write anything to the BotFactory
    public class SmaStochastic : BotPanel
    {
        private BotTabSimple _tab;

        // Basic sattings
        public BotTradeRegime Regime;
        public decimal Step;
        public decimal Slippage;

        // GetVolume settings
        public decimal Volume;
        public string VolumeType;
        public string TradeAssetInPortfolio;

        // Indicator stoh line
        public decimal Upline;
        public decimal Downline;
        
        // Indicators
        private Aindicator _smaFast;
        private Aindicator _stochastic;

        public SmaStochastic(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            
            // Basic settings
            Slippage = 0;
            VolumeType = "Deposit percent";
            Volume = 1;
            Step = 500;
            Upline = 70;
            Downline = 30;

            // Create indicator Sma
            _smaFast = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaFast", false);
            _smaFast = (Aindicator)_tab.CreateCandleIndicator(_smaFast, "Prime");
            _smaFast.Save();

            // Create indicator Stochastic
            _stochastic = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "Stochastic", false);
            _stochastic = (Aindicator)_tab.CreateCandleIndicator(_stochastic, "NewArea0");
            _stochastic.Save();

            Load();

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            // Subscribe to the strategy delete event
            DeleteEvent += Strategy_DeleteEvent;

            Description = OsLocalization.Description.DescriptionLabel118;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "SmaStochastic";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
            SmaStochasticUi ui = new SmaStochasticUi(this);
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
                    writer.WriteLine(Slippage);
                    writer.WriteLine(Volume);
                    writer.WriteLine(Regime);
                    writer.WriteLine(Step);
                    writer.WriteLine(Upline);
                    writer.WriteLine(Downline);

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
                    Slippage = Convert.ToDecimal(reader.ReadLine());
                    Volume = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out Regime);
                    Step = Convert.ToDecimal(reader.ReadLine());
                    Upline = Convert.ToDecimal(reader.ReadLine());
                    Downline = Convert.ToDecimal(reader.ReadLine());

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

        private decimal _lastClose;
        private decimal _firstLastStoh;
        private decimal _secondLastStoh;
        private decimal _lastSma;

        // Logic
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_smaFast.DataSeries[0].Values == null || _stochastic.DataSeries[0].Values == null)
            {
                return;
            }

            _lastClose = candles[candles.Count - 1].Close;
            _firstLastStoh = _stochastic.DataSeries[0].Last;
            _secondLastStoh = _stochastic.DataSeries[0].Values[_stochastic.DataSeries[0].Values.Count - 2];
            _lastSma = _smaFast.DataSeries[0].Last;

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
            if (_lastClose > _lastSma + Step && _secondLastStoh <= Downline && _firstLastStoh >= Downline &&
                Regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(GetVolume(_tab), _lastClose + Slippage);
            }

            if (_lastClose < _lastSma - Step && _secondLastStoh >= Upline && _firstLastStoh <= Upline &&
                Regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(GetVolume(_tab), _lastClose - Slippage);
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.Direction == Side.Buy)
            {
                if (_lastClose < _lastSma - Step)
                {
                    _tab.CloseAtLimit(position, _lastClose - Slippage, position.OpenVolume);
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastClose > _lastSma + Step)
                {
                    _tab.CloseAtLimit(position, _lastClose - Slippage, position.OpenVolume);
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