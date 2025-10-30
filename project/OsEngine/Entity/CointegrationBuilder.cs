/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;

namespace OsEngine.Entity
{
    /// <summary>
    /// Object responsible for the calculation of the cointegration
    /// </summary>
    public class CointegrationBuilder
    {
        /// <summary>
        /// Length of cointegration calculation
        /// </summary>
        public int CointegrationLookBack = 50;

        /// <summary>
        /// Deviation for calculating lines on cointegration
        /// </summary>
        public decimal CointegrationDeviation = 1;

        /// <summary>
        /// An array with cointegration values. The actual value is the last
        /// </summary>
        public List<PairIndicatorValue> Cointegration = new List<PairIndicatorValue>();

        /// <summary>
        /// Multiplier for multiplication of the second instrument to obtain minimal deviations on the Cointegration graph
        /// </summary>
        public decimal CointegrationMult
        {
            get { return _cointegrationMult; }
        }
        private decimal _cointegrationMult;

        /// <summary>
        /// Standard deviation on the cointegration deviation graph
        /// </summary>
        public decimal CointegrationStandartDeviation
        {
            get { return _cointegrationStandartDeviation; }
        }
        private decimal _cointegrationStandartDeviation;

        /// <summary>
        /// The side in which the current value of the deviation between the instruments is located, 
        /// relative to the lines on the cointegration graph. 
        /// No - on the middle. Up - above the top line. Down - below the bottom line. 
        /// </summary>
        public CointegrationLineSide SideCointegrationValue
        {
            get
            {
                return _sideCointegrationValue;
            }
        }
        private CointegrationLineSide _sideCointegrationValue;

        /// <summary>
        /// Value of the upper line on the deviation graph
        /// </summary>
        public decimal LineUpCointegration
        {
            get { return _lineUpCointegration; }
        }
        private decimal _lineUpCointegration;

        /// <summary>
        /// The value of the bottom line on the deviation graph 
        /// </summary>
        public decimal LineDownCointegration
        {
            get { return _lineDownCointegration; }
        }
        private decimal _lineDownCointegration;

        /// <summary>
        /// A method that calculates between two arrays of candlesticks an array of differences with multiplication of the second by a multiplier. 
        /// In this case, the resulting array contains the results with the minimum deviation of differences from zero.
        /// </summary>
        /// <param name="candles1">Candles security 1</param>
        /// <param name="candles2">Candles security 1</param>
        /// <param name="needToRoundValues">Whether values need to be rounded. false - no need. 
        /// true - we round up by discarding the irrelevant values and everything becomes prettier, 
        /// but we waste a lot of resources on it.</param>
        public void ReloadCointegration(List<Candle> candles1, List<Candle> candles2, bool needToRoundValues)
        {
            Cointegration = new List<PairIndicatorValue>();
            _lineUpCointegration = 0;
            _lineDownCointegration = 0;
            _cointegrationStandartDeviation = 0;

            List<double> movesOne = new List<double>();
            List<double> movesTwo = new List<double>();

            for (int indFirstSec = candles1.Count - 1, indSecondSec = candles2.Count - 1;
                indFirstSec >= 0 && indSecondSec >= 0;
                indFirstSec--, indSecondSec--)
            {
                if (movesOne.Count == CointegrationLookBack && movesTwo.Count == CointegrationLookBack)
                    break;

                Candle first = candles1[indFirstSec];
                Candle second = candles2[indSecondSec];

                if (first.TimeStart > second.TimeStart)
                { // в случае если время не равно
                    indSecondSec++;
                    continue;
                }
                else if (second.TimeStart > first.TimeStart)
                { // в случае если время не равно
                    indFirstSec++;
                    continue;
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

            double startMult = GetStartMult(movesOne, movesTwo);

            double optimalMult = GetOptimalMultToArrays(movesOne, movesTwo, startMult);

            List<double> curDeviation = GetDevArray(movesOne, movesTwo, optimalMult);


            int rounder = 6;

            if (needToRoundValues)
            {
                rounder = GetRounder(curDeviation);
            }

            List<PairIndicatorValue> deviationDecimals = new List<PairIndicatorValue>();

            for (int i = curDeviation.Count - 1, candleInd = candles1.Count - 1; i >= 0; i--, candleInd--)
            {
                PairIndicatorValue value = new PairIndicatorValue();

                if (needToRoundValues == true)
                {
                    value.Value = Convert.ToDecimal(Math.Round(curDeviation[i], rounder));
                }
                else
                {
                    value.Value = Convert.ToDecimal(curDeviation[i]);
                }

                value.Time = candles1[candleInd].TimeStart;
                deviationDecimals.Insert(0, value);
            }

            _cointegrationMult = Convert.ToDecimal(optimalMult);
            Cointegration = deviationDecimals;

            decimal CointegrationLast = Cointegration[Cointegration.Count - 1].Value;

            _cointegrationStandartDeviation = StdDev(curDeviation).ToStringWithNoEndZero().ToDecimal();

            _lineUpCointegration = _cointegrationStandartDeviation * CointegrationDeviation;
            _lineDownCointegration = -(_cointegrationStandartDeviation * CointegrationDeviation);

            if (CointegrationLast > _lineUpCointegration)
            {
                _sideCointegrationValue = CointegrationLineSide.Up;
            }
            else if (CointegrationLast < _lineDownCointegration)
            {
                _sideCointegrationValue = CointegrationLineSide.Down;
            }
            else
            {
                _sideCointegrationValue = CointegrationLineSide.No;
            }
        }

        private int GetRounder(List<double> curDeviation)
        {
            int rounder = 0;


            for (int i = curDeviation.Count - 1; i >= 0 && i > curDeviation.Count - 1 - 5; i--)
            {
                int rounderOnIndex = GetRounderToIndex(curDeviation, i);

                if (rounderOnIndex > rounder)
                {
                    rounder = rounderOnIndex;
                }
            }

            return rounder;
        }

        private int GetRounderToIndex(List<double> curDeviation, int index)
        {
            int rounder = 6;


            if (curDeviation[index] != 0)
            {
                string valueLastInString = Math.Abs(Convert.ToDecimal(curDeviation[index])).ToStringWithNoEndZero();

                if (valueLastInString.StartsWith("0,"))
                {
                    valueLastInString = valueLastInString.Replace("0,", "");

                    int countZeros = 0;

                    while (valueLastInString.Length > 0 &&
                        valueLastInString.StartsWith("0"))
                    {
                        valueLastInString = valueLastInString.Substring(1);
                        countZeros++;
                    }
                    rounder = countZeros + 4;
                }
                else
                {
                    rounder = 3;
                }
            }



            return rounder;
        }

        private double GetStartMult(List<double> movesOne, List<double> movesTwo)
        { // здесь мы выравниваем первые значения по значимой части хотябы. Чтобы было одинаковой длины

            double startMult = 1;

            double valueOne = movesOne[0];
            double valueTwo = movesTwo[0];

            if (valueTwo < valueOne)
            {
                while (valueOne > valueTwo * startMult)
                {
                    startMult = startMult * 10;
                }
            }
            else if (valueTwo > valueOne)
            {
                while (valueOne < valueTwo * startMult)
                {
                    startMult = startMult / 10;
                }
            }

            return startMult;
        }

        private double GetOptimalMultToArrays(List<double> movesOne, List<double> movesTwo, double mult)
        {
            string lastSide = "";

            double stdDeviationStart = StdDev(GetDevArray(movesOne, movesTwo, mult));

            for (int i = 0; i < 50; i++)
            {// движение для мультипликатора в 50 %
                double stdDeviationPlus = StdDev(GetDevArray(movesOne, movesTwo, mult * 2));
                double stdDeviationMinus = StdDev(GetDevArray(movesOne, movesTwo, mult * 0.5));

                if (i == 0
                    && stdDeviationStart < stdDeviationPlus
                    && stdDeviationStart < stdDeviationMinus)
                {
                    break;
                }
                else if (stdDeviationPlus < stdDeviationMinus)
                {
                    if (lastSide == "-")
                    {
                        break;
                    }

                    mult = mult * 2;

                    lastSide = "+";

                }
                else
                {
                    if (lastSide == "+")
                    {
                        break;
                    }

                    mult = mult * 0.5;

                    lastSide = "-";
                }
            }

            lastSide = "";

            for (int i = 0; i < 50; i++)
            {// движение для мультипликатора в 10 %
                double stdDeviationPlus = StdDev(GetDevArray(movesOne, movesTwo, mult * 1.1));
                double stdDeviationMinus = StdDev(GetDevArray(movesOne, movesTwo, mult * 0.9));

                if (stdDeviationPlus < stdDeviationMinus)
                {
                    if (lastSide == "-")
                    {
                        break;
                    }

                    mult = mult * 1.1;

                    lastSide = "+";

                }
                else
                {
                    if (lastSide == "+")
                    {
                        break;
                    }

                    mult = mult * 0.9;

                    lastSide = "-";
                }
            }

            lastSide = "";

            for (int i = 0; i < 50; i++)
            {// движение для мультипликатора в 1 %
                double stdDeviationPlus = StdDev(GetDevArray(movesOne, movesTwo, mult * 1.01));
                double stdDeviationMinus = StdDev(GetDevArray(movesOne, movesTwo, mult * 0.99));

                if (stdDeviationPlus < stdDeviationMinus)
                {
                    if (lastSide == "-")
                    {
                        break;
                    }

                    mult = mult * 1.01;

                    lastSide = "+";

                }
                else
                {
                    if (lastSide == "+")
                    {
                        break;
                    }

                    mult = mult * 0.99;

                    lastSide = "-";
                }
            }

            lastSide = "";

            for (int i = 0; i < 50; i++)
            {// движение для мультипликатора в 0.1 %
                double stdDeviationPlus = StdDev(GetDevArray(movesOne, movesTwo, mult * 1.001));
                double stdDeviationMinus = StdDev(GetDevArray(movesOne, movesTwo, mult * 0.999));

                if (stdDeviationPlus < stdDeviationMinus)
                {
                    if (lastSide == "-")
                    {
                        break;
                    }

                    mult = mult * 1.001;

                    lastSide = "+";

                }
                else
                {
                    if (lastSide == "+")
                    {
                        break;
                    }

                    mult = mult * 0.999;

                    lastSide = "-";
                }
            }

            lastSide = "";

            for (int i = 0; i < 50; i++)
            {// движение для мультипликатора в 0.01 %
                double stdDeviationPlus = StdDev(GetDevArray(movesOne, movesTwo, mult * 1.0001));
                double stdDeviationMinus = StdDev(GetDevArray(movesOne, movesTwo, mult * 0.9999));

                if (stdDeviationPlus < stdDeviationMinus)
                {
                    if (lastSide == "-")
                    {
                        break;
                    }

                    mult = mult * 1.0001;

                    lastSide = "+";

                }
                else
                {
                    if (lastSide == "+")
                    {
                        break;
                    }

                    mult = mult * 0.9999;

                    lastSide = "-";
                }
            }

            return mult;
        }

        private double StdDev(List<double> deviations)
        {
            double result = 0;

            for (int i = 0; i < deviations.Count; i++)
            {
                result += Math.Abs(deviations[i]);
            }

            result = result / deviations.Count;

            return result;
        }

        private List<double> GetDevArray(List<double> movesOne, List<double> movesTwo, double mult)
        {
            List<double> std = new List<double>();

            for (int i = 0; i < movesOne.Count; i++)
            {
                std.Add(movesOne[i] - movesTwo[i] * mult);
            }

            return std;
        }

    }

    /// <summary>
    /// The side in which the current value of the deviation between the instruments is located, 
    /// relative to the lines on the cointegration graph. 
    /// </summary>
    public enum CointegrationLineSide
    {
        /// <summary>
        /// No - on the middle
        /// </summary>
        No,

        /// <summary>
        /// Up - above the top line.
        /// </summary>
        Up,

        /// <summary>
        /// Down - below the bottom line.
        /// </summary>
        Down
    }
}