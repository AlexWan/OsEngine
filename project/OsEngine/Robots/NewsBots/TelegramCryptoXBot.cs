/*Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
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
using System.Text.RegularExpressions;


/* Description
Trading robot for OsEngine

Works with a news source and a screener in which receives trading signals
from the CryptoX|Protruding Telegram channel, finds a security,
makes a deal, sets take profit and stop loss.
The signal has three take profit levels. 
After the second take level is triggered, the stop-loss level is moved to no loss.
Use 1 min timeframe
Link: https://t.me/+byORdjvgSFQ2ODQ0
*/

namespace OsEngine.Robots.NewsBots
{
    [Bot("TelegramCryptoXBot")]
    public class TelegramCryptoXBot : BotPanel
    {
        private BotTabScreener _tabScreener;

        // Basic Settings
        private StrategyParameterString _regime;

        public StrategyParameterDecimal _stopPercent;

        // GetVolume Settings
        private StrategyParameterString _volumeType;

        private StrategyParameterDecimal _volume;

        private StrategyParameterString _tradeAssetInPortfolio;

        // Manual manage position
        private StrategyParameterString _securityForManagement;

        private List<TradeSignal> _signalsWithPositions = new List<TradeSignal>();

        public TelegramCryptoXBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.News);
            TabsNews[0].NewsEvent += CryptoX_NewsEvent;

            TabCreate(BotTabType.Screener);

            _tabScreener = TabsScreener[0];
            _tabScreener.CandleFinishedEvent += CandleFinishedEvent;
            _tabScreener.PositionClosingSuccesEvent += PositionClosingSuccesEvent;

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, " Base ");

            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, " Base ");

            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4, " Base ");

            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", " Base ");

            _stopPercent = CreateParameter("Stop percent", 3.0m, 0, 20, 1m, " Base ");

            // Manual manage position
            StrategyParameterButton stopMoveButton = CreateParameterButton("  Move stop to no loss", " Manual manage position ");

            _securityForManagement = CreateParameter("Security name", "", " Manual manage position ");

            stopMoveButton.UserClickOnButtonEvent += StopButton_UserClickOnButtonEvent;

            Description = OsLocalization.Description.DescriptionLabel53;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "TelegramCryptoXBot";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // We received the news
        private void CryptoX_NewsEvent(News news)
        {
            if (_regime.ValueString == "Off" || _regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            // signal message
            if (news.Value.StartsWith("🚀"))
            {
                TradeSignal signal = ParseToSignal(news.Value);

                if (signal != null)
                {
                    FindAndMakeDeal(signal);
                }
                else
                {
                    SendNewLogMessage("The signal could not be recognized", Logging.LogMessageType.Error);
                }
            }
        }

        private void CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (_signalsWithPositions.Count == 0 || tab.PositionsOpenAll == null || tab.PositionsOpenAll.Count == 0)
            {
                return;
            }

            // set stop and profit
            TradeSignal signalClean = _signalsWithPositions.Find(p => p.SecName == tab.Security.Name && p.isCompleted == false);

            if (signalClean != null)
            {
                SetProfitAndStop(tab, signalClean);
            }

            // stop to a no loss
            TradeSignal signalRisk = _signalsWithPositions.Find(p => p.SecName == tab.Security.Name && p.isCompleted == true);

            if (signalRisk != null)
            {
                MoveStopLevel(tab, signalRisk, candles[^1]);
            }
        }

        /// <summary>
        /// Check the breakdown of the second take-profit level
        /// </summary>
        private void MoveStopLevel(BotTabSimple tab, TradeSignal signal, Candle lastCandle)
        {
            Position pos = tab.PositionsLast;

            if (pos == null)
            {
                _signalsWithPositions.Remove(signal);
            }

            if (pos.Direction == Side.Buy)
            {
                if (lastCandle.Close > signal.TakeProfits[1])
                {
                    tab.CloseAtStopMarket(pos, pos.EntryPrice, "stop without loss");

                    SendNewLogMessage("Stop moved", Logging.LogMessageType.Error);

                    _signalsWithPositions.Remove(signal);
                }
            }
            else
            {
                if (lastCandle.Close < signal.TakeProfits[1])
                {
                    tab.CloseAtStopMarket(pos, pos.EntryPrice, "stop without loss");

                    SendNewLogMessage("Stop moved", Logging.LogMessageType.Error);

                    _signalsWithPositions.Remove(signal);
                }
            }
        }

        /// <summary>
        /// Search for a security in the screener and enter into a transaction
        /// </summary>
        /// <param name="signal">transaction parameters from the message</param>
        private void FindAndMakeDeal(TradeSignal signal)
        {
            BotTabSimple tab = _tabScreener.Tabs.Find(p => p.Security.Name.Contains(signal.SecName));

            if (tab != null)
            {
                decimal volume = GetVolume(tab);

                decimal entryPrice = ContainsDigit(signal.Entry) ? signal.Entry.ToDecimal() : 0;

                if (signal.Direction == Side.Buy)
                {
                    if (_regime.ValueString != "OnlyShort")
                    {
                        signal.SecName = tab.Security.Name;
                        _signalsWithPositions.Add(signal);

                        tab.ManualPositionSupport.DisableManualSupport();

                        if (entryPrice == 0)
                            tab.BuyAtMarket(volume);
                        else
                            tab.BuyAtLimit(volume, entryPrice);
                    }
                }
                else if (signal.Direction == Side.Sell)
                {
                    if (_regime.ValueString != "OnlyLong")
                    {
                        signal.SecName = tab.Security.Name;
                        _signalsWithPositions.Add(signal);

                        tab.ManualPositionSupport.DisableManualSupport();

                        if (entryPrice == 0)
                            tab.SellAtMarket(volume);
                        else
                            tab.SellAtLimit(volume, entryPrice);
                    }
                }
                else
                {
                    SendNewLogMessage("The direction is not known", Logging.LogMessageType.Error);
                }
            }
            else
            {
                SendNewLogMessage("Security not found", Logging.LogMessageType.Error);
            }
        }

        public TradeSignal ParseToSignal(string message)
        {
            try
            {
                string firstLinePattern = @"🚀\s*#([A-Z0-9]+)\s+(LONG|SHORT)";
                string entryPattern = @"Диапазон входа:s*([^⏺️]+)";
                string takeProfitPattern = @"Тейки:s*([^⏺️]+)";
                string stopPattern = @"[СCсc][тТ][оО][пП]:\s*([^\r\n]*)";

                Match firstLineMatch = Regex.Match(message, firstLinePattern, RegexOptions.IgnoreCase);
                Match entryMatch = Regex.Match(message, entryPattern);
                Match takeProfitMatch = Regex.Match(message, takeProfitPattern);
                Match stopMatch = Regex.Match(message, stopPattern);

                if (!firstLineMatch.Success)
                {
                    return null;
                }

                TradeSignal tradeSignal = new TradeSignal();

                tradeSignal.SecName = firstLineMatch.Groups[1].Value;
                tradeSignal.Direction = firstLineMatch.Groups[2].Value.ToUpper() == "LONG" ? Side.Buy : Side.Sell;
                tradeSignal.Entry = entryMatch.Success ? entryMatch.Groups[1].Value.Trim() : null;
                tradeSignal.Stop = stopMatch.Success ? stopMatch.Groups[1].Value.Trim() : null;

                tradeSignal.TakeProfits = ParseTakeProfits(takeProfitMatch);

                if (tradeSignal.TakeProfits.Count == 0)
                    return null;

                return tradeSignal;
            }
            catch
            {
                SendNewLogMessage($"Parse Error", Logging.LogMessageType.Error);
                return null;
            }
        }

        private List<decimal> ParseTakeProfits(Match takeProfitMatch)
        {
            List<decimal> takeProfits = new List<decimal>();

            if (takeProfitMatch.Success)
            {
                string[] takeProfitValues = takeProfitMatch.Groups[1].Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < takeProfitValues.Length; i++)
                {
                    if (ContainsDigit(takeProfitValues[i]))
                    {
                        takeProfits.Add(takeProfitValues[i].Trim().ToDecimal());
                    }
                }
            }

            return takeProfits;
        }

        private bool ContainsDigit(string input)
        {
            return !string.IsNullOrWhiteSpace(input) && Regex.IsMatch(input, @"\d");
        }

        private void SetProfitAndStop(BotTabSimple tab, TradeSignal signal)
        {
            Position pos = tab.PositionsLast;

            decimal stopPrice = 0;

            if (!ContainsDigit(signal.Stop))
            {
                if (pos.Direction == Side.Buy)
                    stopPrice = pos.EntryPrice - pos.EntryPrice * (_stopPercent.ValueDecimal / 100);
                else
                    stopPrice = pos.EntryPrice + pos.EntryPrice * (_stopPercent.ValueDecimal / 100);
            }
            else
            {
                stopPrice = Math.Round(signal.Stop.ToDecimal(), tab.Security.Decimals);
            }

            tab.CloseAtStopMarket(pos, stopPrice, "stop-loss");

            if (signal.TakeProfits.Count > 0)
            {
                decimal vol = Math.Round(pos.OpenVolume / signal.TakeProfits.Count, tab.Security.DecimalsVolume);

                for (int i = 0; i < signal.TakeProfits.Count; i++)
                {
                    decimal priceProfit = Math.Round(signal.TakeProfits[i], tab.Security.Decimals);

                    tab.CloseAtLimitUnsafe(pos, priceProfit, vol);
                }
            }

            signal.isCompleted = true;
        }

        // after stop delete signal
        private void PositionClosingSuccesEvent(Position pos, BotTabSimple tab)
        {
            TradeSignal signal = _signalsWithPositions.Find(p => p.SecName == tab.Security.Name);

            if (signal != null)
            {
                _signalsWithPositions.Remove(signal);
            }
        }

        private void StopButton_UserClickOnButtonEvent()
        {
            if (_securityForManagement.ValueString == "")
            {
                SendNewLogMessage("Enter security name", Logging.LogMessageType.Error);
                return;
            }

            for (int i = 0; i < _tabScreener.Tabs.Count; i++)
            {
                BotTabSimple tab = _tabScreener.Tabs[i];

                if (tab.Security.Name.Contains(_securityForManagement.ValueString))
                {
                    Position pos = tab.PositionsLast;

                    if (pos != null)
                    {
                        tab.CloseAtStopMarket(pos, pos.EntryPrice, "stop without loss");

                        SendNewLogMessage("Stop moved", Logging.LogMessageType.User);

                        TradeSignal signal = _signalsWithPositions.Find(p => p.SecName == tab.Security.Name);

                        if (signal != null)
                        {
                            _signalsWithPositions.Remove(signal);
                        }
                    }
                    else
                    {
                        SendNewLogMessage($"Position {tab.Security.Name} not found", Logging.LogMessageType.Error);
                    }
                }
            }
        }

        // Method for calculating the volume of entry into a position
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

    public class TradeSignal
    {
        public string SecName { get; set; }

        public Side Direction { get; set; }

        public string Entry { get; set; }

        public List<decimal> TakeProfits { get; set; }

        public string Stop { get; set; }

        public bool isCompleted { get; set; }
    }
}
