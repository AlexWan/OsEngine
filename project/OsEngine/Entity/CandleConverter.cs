﻿using System;
using System.Collections.Generic;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Entity
{
    /// <summary>
    /// конвертер для свечек
    /// </summary>
    public static class CandleConverter
    {

        /// <summary>
        /// хранилища уже конвертированных свечек
        /// </summary>
        private static List<ValueSave> _valuesToFormula = new List<ValueSave>();

        /// <summary>
        /// слить свечи 
        /// </summary>
        /// <param name="candles">свечи</param>
        /// <param name="countMerge">количество складывания для начального ТФ</param>
        /// <returns></returns>
        public static List<Candle> Merge(List<Candle> candles, int countMerge)
        {
            if (countMerge <= 1)
            {
                return candles;
            }

            if (candles == null ||
                candles.Count == 0 ||
                candles.Count < countMerge)
            {
                return candles;
            }

  
            ValueSave saveVal = _valuesToFormula.Find(val => val.Name == candles[0].StringToSave + countMerge);

            List<Candle> mergeCandles = null;

            if (saveVal != null)
            {
                mergeCandles = saveVal.ValueCandles;
            }
            else
            {
                mergeCandles = new List<Candle>();
                saveVal = new ValueSave();
                saveVal.ValueCandles = mergeCandles;
                saveVal.Name = candles[0].StringToSave + countMerge;
                _valuesToFormula.Add(saveVal);
            }
            
// узнаём начальный индекс

            int firstIndex = 0;

            if (mergeCandles.Count != 0)
            {
                mergeCandles.RemoveAt(mergeCandles.Count-1);
            }

            if (mergeCandles.Count != 0)
            {
                for (int i = candles.Count - 1; i > -1; i--)
                {
                    if (mergeCandles[mergeCandles.Count - 1].TimeStart == candles[i].TimeStart)
                    {
                        firstIndex = i+countMerge;
                        break;
                    }
                }
            }

// собираем

            for (int i = firstIndex; i < candles.Count; )
            {
                int countReal = countMerge;

                if (countReal + i > candles.Count)
                {
                    countReal = candles.Count - i;
                }
                else if (i + countMerge < candles.Count &&
                    candles[i].TimeStart.Day != candles[i + countMerge].TimeStart.Day)
                {
                    countReal = 0;

                    for (int i2 = i; i2 < candles.Count; i2++)
                    {
                        if (candles[i].TimeStart.Day == candles[i2].TimeStart.Day)
                        {
                            countReal += 1;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                if (countReal == 0)
                {
                    break;
                }

                mergeCandles.Add(Concate(candles, i, countReal));
                i += countReal;

            }

            return mergeCandles;
        }

        /// <summary>
        /// соединить свечи
        /// </summary>
        /// <param name="candles">изначальные свечи</param>
        /// <param name="index">индекс начала</param>
        /// <param name="count">количество свечек для соединения</param>
        /// <returns></returns>
        private static Candle Concate(List<Candle> candles, int index, int count)
        {
            Candle candle = new Candle();

            candle.Open = candles[index].Open;
            candle.High = Decimal.MinValue;
            candle.Low = Decimal.MaxValue;
            candle.TimeStart = candles[index].TimeStart;

            for (int i = index; i < candles.Count && i < index + count; i++)
            {
                if (candles[i].Trades != null)
                {
                    candle.Trades.AddRange(candles[i].Trades);
                }
                
                candle.Volume += candles[i].Volume;

                if (candles[i].High > candle.High)
                {
                    candle.High = candles[i].High;
                }

                if (candles[i].Low < candle.Low)
                {
                    candle.Low = candles[i].Low;
                }

                candle.Close = candles[i].Close;
            }

            return candle;
        }

        /// <summary>
        /// очистить старые данные
        /// </summary>
        public static void Clear()
        {
            _valuesToFormula = new List<ValueSave>();
        }
    }
}
