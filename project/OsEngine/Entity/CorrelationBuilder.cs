/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;


namespace OsEngine.Entity
{
    public class CorrelationBuilder
    {
        public List<PairIndicatorValue> ReloadCorrelation(List<Candle> candles1, List<Candle> candles2, int correlationLookBack)
        {
            _correlationList = new List<PairIndicatorValue>();

            if (candles1.Count < correlationLookBack
                || candles2.Count < correlationLookBack)
            {
                return _correlationList;
            }

            for (int i = 0; i < correlationLookBack; i++)
            {
                ReloadCorrelationByIndex(
                    candles1,
                    candles2,
                    candles1.Count - 1 - correlationLookBack + i,
                    candles2.Count - 1 - correlationLookBack + i,
                    correlationLookBack);
            }

            return _correlationList;
        }

        private List<PairIndicatorValue> _correlationList = new List<PairIndicatorValue>();

        private void ReloadCorrelationByIndex(List<Candle> candles1, List<Candle> candles2, int index1, int index2, int correlationLookBack)
        {
            List<double> movesOne = new List<double>();
            List<double> movesTwo = new List<double>();


            for (int indFirstSec = index1, indSecondSec = index2;
                indFirstSec >= 0 && indSecondSec >= 0
                && indFirstSec > index1 - correlationLookBack
                && indSecondSec > index2 - correlationLookBack
                ;
                indFirstSec--, indSecondSec--)
            {
                Candle first = candles1[indFirstSec];
                Candle second = candles2[indSecondSec];

                if (first.TimeStart != second.TimeStart)
                { // в случае если время не равно
                    break;
                }

                movesOne.Insert(0, Convert.ToDouble(first.Close));
                movesTwo.Insert(0, Convert.ToDouble(second.Close));
            }

            if (movesOne.Count == 0
                || movesTwo.Count == 0
                || movesOne.Count != movesTwo.Count)
            {
                return;
            }

            double lastCorrelation = Correlation(movesOne.ToArray(), movesTwo.ToArray());

            if (lastCorrelation == 0)
            {
                return;
            }

            lastCorrelation = Math.Round(lastCorrelation, 4);

            PairIndicatorValue value = new PairIndicatorValue();
            value.Time = candles1[index1].TimeStart;
            value.Value = Convert.ToDecimal(lastCorrelation);

            _correlationList.Add(value);
        }

        private double Correlation(Double[] Xs, Double[] Ys)
        {
            Double sumX = 0;
            Double sumX2 = 0;
            Double sumY = 0;
            Double sumY2 = 0;
            Double sumXY = 0;

            int n = Xs.Length < Ys.Length ? Xs.Length : Ys.Length;

            for (int i = 0; i < n; ++i)
            {
                Double x = Xs[i];
                Double y = Ys[i];

                sumX += x;
                sumX2 += x * x;
                sumY += y;
                sumY2 += y * y;
                sumXY += x * y;
            }

            Double stdX = Math.Sqrt(sumX2 / n - sumX * sumX / n / n);
            Double stdY = Math.Sqrt(sumY2 / n - sumY * sumY / n / n);
            Double covariance = (sumXY / n - sumX * sumY / n / n);

            if (stdX == 0 && stdY == 0)
            {
                return 0;
            }

            return covariance / stdX / stdY;
        }

    }

    public class PairIndicatorValue
    {
        public DateTime Time;

        public decimal Value;

    }
}