/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System.Threading;
using OsEngine.Language;

/* Description
Robot example from the lecture course "C# for algotreader".

Buy:
If best bid volume bigger on value of _percentInFirstBid, than volume in the summary bid below. Buy at limit price

Exit:
Close At Stop Market and Close At Profit Market;
*/

namespace OsEngine.Robots.BotsFromStartLessons
{
    // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    // Вместо того, чтобы добавлять вручную через BotFactory, мы используем атрибут для упрощения процесса.
    [Bot("Lesson8Bot1")]
    public class Lesson8Bot1 : BotPanel
    {
        // Reference to the main trading tab
        // Ссылка на главную вкладку
        private BotTabSimple _tabToTrade;

        // Basic settings
        // Базовые настройки
        private StrategyParameterString _regime;

        // GetVolume settings
        // настройки метода GetVolume
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Thread setting
        // Настройка потока
        private StrategyParameterInt _millisecondsToSleepWorker;

        // Depth of Market settings
        // Настройки стакана заявок
        private StrategyParameterInt _countBidsToCheck;
        private StrategyParameterDecimal _percentInFirstBid;

        // Price settings
        // Настройки для цены
        private StrategyParameterInt _slippagePriceStep;
        private StrategyParameterInt _stopPriceStep;
        private StrategyParameterInt _profitPriceStep;

        // Логика
        // 1 Мы смотрим лучший бид. Объём в нём. 
        // 2 Мы складываем объёмы в бидах ниже. Идём на глубину N.
        // 3 Если в лучшем биде объёмы на M % > чем в суммарных бидах ниже. Покупаем. Ура.

        public Lesson8Bot1(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            // Создаём главную вкладку для торговли
            TabCreate(BotTabType.Simple);
            _tabToTrade = TabsSimple[0];

            // Basic settings
            // Базовые настройки
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });

            // GetVolume settings
            // настройки метода GetVolume
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Thread setting
            // Настройка потока
            _millisecondsToSleepWorker = CreateParameter("Worker milliseconds to sleep", 1000, 1, 50, 5);

            // Depth of Market settings
            // Настройки стакана заявок
            _countBidsToCheck = CreateParameter("Count bids to check", 3, 1, 50, 5);
            _percentInFirstBid = CreateParameter("Percent in first bid", 100m, 10, 50, 5);

            // Price settings
            // Настройки для цены
            _slippagePriceStep = CreateParameter("Slippage to entry. Price step", -2, 1, 10, 1);
            _stopPriceStep = CreateParameter("Stop. Price step", 15, 10, 50, 1);
            _profitPriceStep = CreateParameter("Profit. Price step", 5, 10, 50, 1);

            //Create a new thread that works in WorkerPlace()
            Thread worker = new Thread(WorkerPlace);
            worker.Start();

            Description = OsLocalization.Description.DescriptionLabel16;
        }

        private void WorkerPlace()
        {
            // Loop with condition in brackets. If true, the loop continues
            // Цикл с условием в скобках. Если в скобках true, то цикл - продолжается
            while (true)
            {
                try
                {
                    Thread.Sleep(_millisecondsToSleepWorker.ValueInt);

                    if (_regime.ValueString == "Off")
                    {
                        continue;
                    }

                    if (_tabToTrade.IsConnected == false)
                    {   
                        // if the source is not ready yet. And not connected to data
                        // если источник ещё не готов. И не подключен к данным

                        continue;
                    }

                    if (_tabToTrade.IsReadyToTrade == false)
                    {
                        // if the source is not willing to trade
                        // если источник не готов торговать

                        continue;
                    }

                    List<Position> positions = _tabToTrade.PositionsOpenAll;

                    if (positions.Count == 0)
                    {
                        // opening logic
                        // логика открытия

                        MarketDepth md = _tabToTrade.MarketDepth;

                        if (md == null)
                        {
                            continue;
                        }

                        md = md.GetCopy();

                        if (md.Bids == null
                            || md.Bids.Count < _countBidsToCheck.ValueInt + 2)
                        {
                            continue;
                        }

                        // take volume best bid from market depth
                        // берём объём в лучшем уровне покупки стакана
                        decimal firstBidVolume = md.Bids[0].Bid.ToDecimal(); 

                        decimal checkBidsVolume = 0;

                        for (int i = 1; i < _countBidsToCheck.ValueInt; i++)
                        {
                            // total volume in bids under best, to depth countBidsToCheck
                            // считаем суммарный объём в бидах под лучшим, на глубину countBidsToCheck
                            checkBidsVolume += md.Bids[i].Bid.ToDecimal();
                        }

                        // If the volume of the first bid is X% or more of all bids, and this X is greater than value of parameter _percentInFirstBid, we enter the position.
                        // Если объём первой ставки равен X% или больше всех ставок, и эта X больше значения параметра _percentInFirstBid, мы входим позицию.
                        if (firstBidVolume / (checkBidsVolume / 100) >= _percentInFirstBid.ValueDecimal)
                        {
                            decimal volume = GetVolume(_tabToTrade);
                            decimal price = md.Bids[0].Price.ToDecimal();
                            price += _tabToTrade.Security.PriceStep * _slippagePriceStep.ValueInt;

                            _tabToTrade.BuyAtLimit(volume, price);
                        }
                    }
                    else
                    {
                        // Exit
                        // Выход

                        Position pos = positions[0];

                        if (pos.OpenVolume == 0)
                        {
                            continue;
                        }

                        if (pos.State != PositionStateType.Open)
                        {
                            continue;
                        }

                        if (pos.StopOrderPrice != 0)
                        {
                            continue;
                        }

                        decimal stopPrice =
                            pos.EntryPrice - _tabToTrade.Security.PriceStep * _stopPriceStep.ValueInt;

                        decimal profitPrice =
                            pos.EntryPrice + _tabToTrade.Security.PriceStep * _profitPriceStep.ValueInt;

                        _tabToTrade.CloseAtStopMarket(pos, stopPrice);
                        _tabToTrade.CloseAtProfitMarket(pos, profitPrice);
                    }
                }
                catch (Exception e)
                {
                    Thread.Sleep(10000);
                    SendNewLogMessage("Worker thread error " + e.ToString(), Logging.LogMessageType.Error);
                }
            }
        }

        public override string GetNameStrategyType()
        {
            return "Lesson8Bot1";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

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
}