/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.CandleChart.Elements;
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
using System.Drawing;
using System.IO;

/* Description
Counter Trend Strategy Based on Willams% R Indicator
Buy:
1. Williams Range is smaller than DownLine.
sell:
1. Williams Range is larger than UpLine.
exit:
1. On the return signal
*/

namespace OsEngine.Robots.CounterTrend
{
    [Bot("WilliamsRangeTrade")] // We create an attribute so that we don't write anything to the BotFactory
    public class WilliamsRangeTrade : BotPanel
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
        private Aindicator _williamsRange;

        // Up and Down line on Rsi
        public LineHorisontal _upline;
        public LineHorisontal _downline;
        
        // The last value of the indicator and price
        private decimal _lastPrice;
        private decimal _lastWr;

        public WilliamsRangeTrade(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Creating an indicator WilliamsRange
            _williamsRange = IndicatorsFactory.CreateIndicatorByName("WilliamsRange", name + "WilliamsRange Fast", false);
            _williamsRange = (Aindicator)_tab.CreateCandleIndicator(_williamsRange, "WilliamsArea");
            _williamsRange.Save();

            _upline = new LineHorisontal("upline", "WilliamsArea", false)
            {
                Color = Color.Green,
                Value = 0,


            };
            _tab.SetChartElement(_upline);
            _upline.TimeEnd = DateTime.Now;

            _downline = new LineHorisontal("downline", "WilliamsArea", false)
            {
                Color = Color.Yellow,
                Value = 0

            };
            _tab.SetChartElement(_downline);
            _downline.TimeEnd = DateTime.Now;

            _volumeType = "Deposit percent";
            _slippage = 0;
            _volume = 1;
            _upline.Value = -20;
            _downline.Value = -80;

            Load();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            DeleteEvent += Strategy_DeleteEvent;

            Description = OsLocalization.Description.DescriptionLabel25;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "WilliamsRangeTrade";
        }
        
        // settings GUI
        public override void ShowIndividualSettingsDialog()
        {
            WilliamsRangeTradeUi ui = new WilliamsRangeTradeUi(this);
            ui.ShowDialog();
        }

        // save settings in .txt file
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false))
                {
                    writer.WriteLine(_volumeType);
                    writer.WriteLine(_tradeAssetInPortfolio);
                    writer.WriteLine(_slippage);
                    writer.WriteLine(_volume);
                    writer.WriteLine(_regime);
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
                    _volumeType = Convert.ToString(reader.ReadLine());
                    _tradeAssetInPortfolio = Convert.ToString(reader.ReadLine());
                    _slippage = Convert.ToDecimal(reader.ReadLine());
                    _volume = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out _regime);
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

        // candle finished event
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime == BotTradeRegime.Off)
            {
                return;
            }

            if (_williamsRange.DataSeries[0].Values == null || _williamsRange.DataSeries[0].Values.Count < ((IndicatorParameterInt)_williamsRange.Parameters[0]).ValueInt + 2)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _lastWr = _williamsRange.DataSeries[0].Last;

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
            if (_lastWr < _downline.Value && _regime != BotTradeRegime.OnlyShort)
            {
                _tab.BuyAtLimit(_volume, _lastPrice + _slippage);
            }

            if (_lastWr > _upline.Value && _regime != BotTradeRegime.OnlyLong)
            {
                _tab.SellAtLimit(_volume, _lastPrice - _slippage);
            }
        }

        // logic close position
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.Direction == Side.Buy)
            {
                if (_lastWr > _upline.Value)
                {
                    _tab.CloseAtLimit(position, _lastPrice - _slippage, position.OpenVolume);

                    if (_regime != BotTradeRegime.OnlyLong && _regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.SellAtLimit(_volume, _lastPrice - _slippage);
                    }
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastWr < _downline.Value)
                {
                    _tab.CloseAtLimit(position, _lastPrice + _slippage, position.OpenVolume);

                    if (_regime != BotTradeRegime.OnlyShort && _regime != BotTradeRegime.OnlyClosePosition)
                    {
                        _tab.BuyAtLimit(_volume, _lastPrice + _slippage);
                    }
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
