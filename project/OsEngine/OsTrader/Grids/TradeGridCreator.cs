/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.OsTrader.Grids
{
    public class TradeGridCreator
    {
        #region Service

        public Side GridSide = Side.Buy;

        public decimal FirstPrice;

        public int LineCountStart;

        public TradeGridValueType TypeStep;

        public decimal LineStep;

        public decimal StepMultiplicator = 1;

        public TradeGridValueType TypeProfit;

        public decimal ProfitStep;

        public decimal ProfitMultiplicator = 1;

        public TradeGridVolumeType TypeVolume;

        public decimal StartVolume = 1;

        public string TradeAssetInPortfolio = "Prime";

        public decimal MartingaleMultiplicator = 1;

        public string GetSaveString()
        {
            string result = "";

            result += GridSide + "@";
            result += FirstPrice + "@";
            result += LineCountStart + "@";
            result += TypeStep + "@";
            result += LineStep + "@";
            result += StepMultiplicator + "@";
            result += TypeProfit + "@";
            result += ProfitStep + "@";
            result += ProfitMultiplicator + "@";
            result += TypeVolume + "@";
            result += StartVolume + "@";
            result += MartingaleMultiplicator + "@";
            result += TradeAssetInPortfolio + "@";
            result += GetSaveLinesString() + "@";
            result += "@";
            result += "@";
            result += "@";
            result += "@"; // пять пустых полей в резерв

            return result;
        }

        public void LoadFromString(string value)
        {
            try
            {
                string[] values = value.Split('@');

                Enum.TryParse(values[0], out GridSide);
                FirstPrice = values[1].ToDecimal();
                LineCountStart = Convert.ToInt32(values[2]);
                Enum.TryParse(values[3], out TypeStep);
                LineStep = values[4].ToDecimal();
                StepMultiplicator = values[5].ToDecimal();
                Enum.TryParse(values[6], out TypeProfit);
                ProfitStep = values[7].ToDecimal();
                ProfitMultiplicator = values[8].ToDecimal();
                Enum.TryParse(values[9], out TypeVolume);
                StartVolume = values[10].ToDecimal();
                MartingaleMultiplicator = values[11].ToDecimal();
                TradeAssetInPortfolio = values[12];
                LoadLines(values[13]);

            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(),LogMessageType.Error);
            }
        }

        #endregion

        #region Grid lines creation and storage

        public List<TradeGridLine> Lines = new List<TradeGridLine>();

        public void CreateNewGrid(BotTabSimple tab, TradeGridPrimeType gridType)
        {
            CreateMarketMakingGrid(tab);
        }

        public void DeleteGrid()
        {
            if(Lines.Count > 0)
            {
                Lines.Clear();
            }
        }

        public void CreateNewLine()
        {
            TradeGridLine newLine = new TradeGridLine();
            newLine.PriceEnter = 0;
            newLine.Side = GridSide;
            newLine.Volume = 0;
            Lines.Add(newLine);

        }

        public void RemoveSelected(List<int> numbers)
        {
            for(int i = numbers.Count-1; i > -1; i--)
            {
                int curNumber = numbers[i];

                if(curNumber >= Lines.Count)
                {
                    continue;
                }

                TradeGridLine line = Lines[curNumber];

                if(line.Position != null)
                {
                    SendNewLogMessage("User remove line with Position!!! \n !!!!! \n !!!!!! \n Grid is broken!!!", LogMessageType.Error);
                }

                Lines.RemoveAt(curNumber);
            }
        }

        private void CreateMarketMakingGrid(BotTabSimple tab)
        {
            Lines.Clear();

            decimal priceCurrent = FirstPrice;

            decimal volumeCurrent = StartVolume;

            decimal curStep = LineStep;

            decimal profitStep = ProfitStep;

            if (TypeStep == TradeGridValueType.Percent)
            {
                curStep = priceCurrent * (curStep / 100);

                if (tab.Security != null)
                {
                    curStep = Math.Round(curStep, tab.Security.Decimals);
                }
            }
            else if (TypeStep == TradeGridValueType.Absolute)
            {
                curStep = LineStep;

                if (tab.Security != null)
                {
                    curStep = Math.Round(curStep, tab.Security.Decimals);
                }
            }

            for (int i = 0; i < LineCountStart; i++)
            {
                /*if (FirstPrice > 0 
                    && curStep > FirstPrice*10)
                {
                    break;
                }*/

                /*if (priceCurrent <= 0)
                {
                    break;
                }*/

                /*if (priceCurrent / FirstPrice > 3)
                {
                    break;
                }*/

                TradeGridLine newLine = new TradeGridLine();
                newLine.PriceEnter = priceCurrent;

                if (tab.Security != null)
                {
                    newLine.PriceEnter = tab.RoundPrice(newLine.PriceEnter, tab.Security, GridSide);
                }

                newLine.Side = GridSide;
                newLine.Volume = volumeCurrent;

                if (tab.Security != null
                   && tab.Security.DecimalsVolume >= 0
                   && TypeVolume == TradeGridVolumeType.Contracts)
                {
                    newLine.Volume = Math.Round(volumeCurrent, tab.Security.DecimalsVolume);
                }
                else
                {
                    newLine.Volume = Math.Round(volumeCurrent, 4);
                }

                if (newLine.Volume <= 0)
                {
                    break;
                }

                Lines.Add(newLine);

                if (GridSide == Side.Buy)
                {
                    if (TypeProfit == TradeGridValueType.Percent)
                    {
                        newLine.PriceExit = newLine.PriceEnter + Math.Abs(newLine.PriceEnter * profitStep / 100);
                    }
                    else if (TypeProfit == TradeGridValueType.Absolute)
                    {
                        newLine.PriceExit = newLine.PriceEnter + profitStep;
                    }

                    if (tab.Security != null)
                    {
                        newLine.PriceExit = tab.RoundPrice(newLine.PriceExit, tab.Security, Side.Sell);
                    }

                    priceCurrent -= curStep;

                }
                else if (GridSide == Side.Sell)
                {
                    if (TypeProfit == TradeGridValueType.Percent)
                    {
                        newLine.PriceExit = newLine.PriceEnter - Math.Abs(newLine.PriceEnter * profitStep / 100);
                    }
                    else if (TypeProfit == TradeGridValueType.Absolute)
                    {
                        newLine.PriceExit = newLine.PriceEnter - profitStep;
                    }

                    if (tab.Security != null)
                    {
                        newLine.PriceExit = tab.RoundPrice(newLine.PriceExit, tab.Security, Side.Buy);
                    }

                    priceCurrent += curStep;
                }

                if (StepMultiplicator != 1
                    && StepMultiplicator != 0)
                {
                    curStep = curStep * StepMultiplicator;
                }

                if (ProfitMultiplicator != 1
                    && ProfitMultiplicator != 0)
                {
                    profitStep = profitStep * ProfitMultiplicator;
                }

                if (MartingaleMultiplicator != 0
                    && MartingaleMultiplicator != 1)
                {
                    volumeCurrent = volumeCurrent * MartingaleMultiplicator;
                }
            }
        }

        public string GetSaveLinesString()
        {
            try
            {
                string lines = "";

                for (int i = 0; i < Lines.Count; i++)
                {
                    lines += Lines[i].GetSaveStr() + "^";
                }
                 
                return lines;
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(), LogMessageType.Error);
                return "";
            }
        }

        public void LoadLines(string str)
        {
            if(string.IsNullOrEmpty(str))
            {
                return;
            }

            try
            {
                string[] linesInStr = str.Split('^');

                for(int i = 0;i < linesInStr.Length;i++)
                {
                    string line = linesInStr[i];

                    if(string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    TradeGridLine newLine = new TradeGridLine();
                    newLine.SetFromStr(line);
                    Lines.Add(newLine);
                }
            }
            catch (Exception e)
            {
               SendNewLogMessage(e.ToString(),LogMessageType.Error);
            }
        }

        public decimal GetVolume(TradeGridLine line, BotTabSimple tab)
        {
            decimal volume = 0;
            decimal volumeFromLine = line.Volume;
            decimal priceEnterForLine = line.PriceEnter;

            if (TypeVolume == TradeGridVolumeType.ContractCurrency) // "Валюта контракта"
            {
                decimal contractPrice = priceEnterForLine;

                if(tab.StartProgram == StartProgram.IsOsTrader)
                {
                    if(tab.Security.Lot != 0)
                    {
                        volume = Math.Round(volumeFromLine / contractPrice / tab.Security.Lot, tab.Security.DecimalsVolume);
                    }
                    else
                    {
                        volume = Math.Round(volumeFromLine / contractPrice, tab.Security.DecimalsVolume);
                    }
                }
                else
                {
                    if (tab.Security.Lot != 0)
                    {
                        volume = Math.Round(volumeFromLine / contractPrice / tab.Security.Lot, 7);
                    }
                    else
                    {
                        volume = Math.Round(volumeFromLine / contractPrice, 7);
                    }
                }

                return volume;
            }
            else if (TypeVolume == TradeGridVolumeType.Contracts) // кол-во контрактов
            {
                return line.Volume;
            }
            else // if (TypeVolume == Type_Volume.DepoPercent) // процент депозита
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

                if (portfolioPrimeAsset == 0
                    || portfolioPrimeAsset == 1)
                {
                    SendNewLogMessage("Can`t found portfolio in Deposit Percent volume mode " + TradeAssetInPortfolio, OsEngine.Logging.LogMessageType.Error);
                    return 0;
                }
                decimal moneyOnPosition = portfolioPrimeAsset * (volumeFromLine / 100);
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
        }

        #endregion

        #region Log

        public void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                ServerMaster.SendNewLogMessage(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    public class TradeGridLine
    {
        public decimal PriceEnter;

        public decimal PriceExit;

        public bool CanReplaceExitOrder;

        public decimal Volume;

        public Side Side;

        public int PositionNum = -1;

        public Position Position;

        public string GetSaveStr()
        {
            string result = "";

            result += PriceEnter + "|";
            result += Volume + "|";
            result += Side + "|";
            result += PriceExit + "|";
            result += PositionNum + "|";

            return result;
        }

        public void SetFromStr(string str)
        {
            string[] saveArray = str.Split('|');

            PriceEnter = saveArray[0].ToDecimal();
            Volume = saveArray[1].ToDecimal();
            Enum.TryParse(saveArray[2], out Side);
            PriceExit = saveArray[3].ToDecimal();
            PositionNum = Convert.ToInt32(saveArray[4]);
        }

    }
}
