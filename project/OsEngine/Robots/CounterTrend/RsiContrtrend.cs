/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Indicators;

/* Description
Overbought / Oversold RSI Countertrend Strategy with Trend Filtering via MovingAverage
Buy:
1. Sma more price.
2. Rsi is higher than UpLine.
Sale:
1. Sma less price.
2. Rsi is less than DownLine.
Exit:
By return signal
*/

namespace OsEngine.Robots.CounterTrend
{
    [Bot("RsiContrtrend")] // We create an attribute so that we don't write anything to the BotFactory
    public class RsiContrtrend : BotPanel
    {
        // Tab to trade
        private BotTabSimple _tab;

        // Basic settings
        public decimal _slippage;
        public BotTradeRegime _regime;
        
        // GetVolume settings
        public decimal _volume;
        public string _volumeType;
        public string _tradeAssetInPortfolio;
        
        // Indicator
        private Aindicator _sma;
        private Aindicator _rsi;

        // Up and Down line on Rsi
        public LineHorisontal _upline;
        public LineHorisontal _downline;

        // The last value of the indicator and price
        private decimal _lastPrice;
        private decimal _lastSma;
        private decimal _lastRsi;

        public RsiContrtrend(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Create indicator Sma
            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma", false);
            _sma = (Aindicator)_tab.CreateCandleIndicator(_sma, "Prime");
            ((IndicatorParameterInt)_sma.Parameters[0]).ValueInt = 50;
            _sma.DataSeries[0].Color = Color.CornflowerBlue;
            _sma.Save();

            // Create indicator RSI
            _rsi = IndicatorsFactory.CreateIndicatorByName("RSI", name + "RSI", false);
            _rsi = (Aindicator)_tab.CreateCandleIndicator(_rsi, "RsiArea");
            ((IndicatorParameterInt)_rsi.Parameters[0]).ValueInt = 20;
            _rsi.DataSeries[0].Color = Color.Gold;
            _rsi.Save();

            _upline = new LineHorisontal("upline", "RsiArea", false)
            {
                Color = Color.Green,
                Value = 0,
            };
            _tab.SetChartElement(_upline);
            _upline.TimeEnd = DateTime.Now;

            _downline = new LineHorisontal("downline", "RsiArea", false)
            {
                Color = Color.Yellow,
                Value = 0

            };
            _tab.SetChartElement(_downline);
            _downline.TimeEnd = DateTime.Now;

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
            
            DeleteEvent += Strategy_DeleteEvent;

            _volumeType = "Deposit percent";
            _slippage = 0;
            _volume = 1;
            _upline.Value = 65;
            _downline.Value = 35;

            Load();

            Description = OsLocalization.Trader.Label299;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "RsiContrtrend";
        }

        // show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
            RsiContrtrendUi ui = new RsiContrtrendUi(this);
            ui.ShowDialog();
        }

        // save settings in .txt file
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false))
                {
                    writer.WriteLine(_regime);
                    writer.WriteLine(_volumeType);
                    writer.WriteLine(_tradeAssetInPortfolio);
                    writer.WriteLine(_slippage);
                    writer.WriteLine(_volume);
                    writer.WriteLine(_upline.Value);
                    writer.WriteLine(_downline.Value);

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
                    Enum.TryParse(reader.ReadLine(), true, out _regime);
                    _volumeType =Convert.ToString(reader.ReadLine());
                    _tradeAssetInPortfolio = Convert.ToString(reader.ReadLine());
                    _slippage = Convert.ToDecimal(reader.ReadLine());
                    _volume = Convert.ToDecimal(reader.ReadLine());
                    _upline.Value = Convert.ToDecimal(reader.ReadLine());
                    _downline.Value = Convert.ToDecimal(reader.ReadLine());
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

        // logic
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_sma.DataSeries[0].Values == null || _rsi.DataSeries[0].Values == null)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _lastSma = _sma.DataSeries[0].Last;
            _lastRsi = _rsi.DataSeries[0].Last;


            if (_sma.DataSeries[0].Values.Count < ((IndicatorParameterInt)_sma.Parameters[0]).ValueInt + 1 || 
                _rsi.DataSeries[0].Values == null || _rsi.DataSeries[0].Values.Count < ((IndicatorParameterInt)_rsi.Parameters[0]).ValueInt + 5)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);

                    _upline.Refresh();
                    _downline.Refresh();
                }
            }

            if (_regime == BotTradeRegime.OnlyClosePosition)
            {
                return;
            }

            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        // logic open position
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            decimal lastClose = candles[candles.Count - 1].Close;
            if (_lastSma > lastClose && _lastRsi > _upline.Value && _regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(GetVolume(_tab), _lastPrice - _slippage);
            }
            if (_lastSma < lastClose && _lastRsi < _downline.Value && _regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(GetVolume(_tab), _lastPrice + _slippage);
            }
        }

        // logic close position
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            decimal lastClose = candles[candles.Count - 1].Close;
            if (position.Direction == Side.Buy)
            {
                if (lastClose < _lastSma || _lastRsi > _upline.Value)
                {
                    _tab.CloseAtLimit(position, _lastPrice - _slippage, position.OpenVolume);

                }
            }
            if (position.Direction == Side.Sell)
            {
                if (lastClose > _lastSma || _lastRsi < _downline.Value)
                {
                    _tab.CloseAtLimit(position, _lastPrice + _slippage, position.OpenVolume);
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