using System;
using System.Collections.Generic;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels;
using OsEngine.Robots;
using OsEngine.OsOptimizer.OptimizerEntity;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using OsEngine.Alerts;
using OsEngine.Charts.CandleChart;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Indicators;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels.Tab.Internal;

using Color = System.Drawing.Color;

namespace OsEngine.OsOptimizer.OptEntity
{
    /// <summary>
    /// С - скромность
    /// </summary>
    public class KlasterizationFactory
    {
        private List<OptimazerFazeReport> _reports = new List<OptimazerFazeReport>();

        private List<FazeToChart> _fazesToChart = new List<FazeToChart>();

        public FazeToChart RebuildMapByFaze(int fazeIndex, List<OptimazerFazeReport> reports)
        {
            _reports = reports;
            _fazesToChart.Clear();

            for (int i = 0; i < reports.Count; i++)
            {
                FazeToChart faze = new FazeToChart();
                faze.Faze = reports[i];
            }

            FazeToChart myFaze = _fazesToChart[fazeIndex];
            myFaze.RebuildPoints();

            return myFaze;
        }
    }

    public class FazeToChart
    {
        public OptimazerFazeReport Faze;

        public List<PointOnChart> Points = new List<PointOnChart>();

        public void RebuildPoints()
        {
            Points.Add(ConvertToPointFirst(Faze.Reports[0]));

            Points.Add(ConvertToPointSecond(Faze.Reports[1]));

            Points.Add(ConvertToPointFird(Faze.Reports[2]));

            for (int i = 3;i < Faze.Reports.Count;i++)
            {
                ConvertToPoint(Faze.Reports[i]);
            }
        }

        public PointOnChart ConvertToPoint(OptimizerReport report)
        {
            PointOnChart pointOne = Points[0];
            PointOnChart pointTwo = Points[1];
            PointOnChart pointThree = Points[2];

            PointOnChart newPoint = new PointOnChart();
            newPoint.Report = report;


            decimal xA = pointOne.X;
            decimal xB = pointTwo.X;
            decimal xC = pointThree.X;

            decimal yA = pointOne.Y;
            decimal yB = pointTwo.Y;
            decimal yC = pointThree.Y;

            decimal h; // высота точки h = (3*V)/S
            decimal p; // полупериметр p = (a + b + c) : 2
            decimal s; // площадь S = (p − a) * (p − b), 
            decimal v; // объём

            decimal l1; // длинна катета 1
            decimal l2; // длинна катета 2
            decimal l3; // длинна катета 3

            decimal l4; // длинна катета 4
            decimal l5; // длинна катета 5
            decimal l6;  // длинна катета 6

            decimal x;
            decimal y;

            l1 = GetEuclidLength(pointOne, pointTwo);
            l2 = GetEuclidLength(pointTwo, pointThree);
            l3 = GetEuclidLength(pointThree, pointOne);
            l4 = GetEuclidLength(newPoint, pointOne);
            l5 = GetEuclidLength(newPoint, pointTwo);
            l6 = GetEuclidLength(newPoint, pointThree);

            decimal l1_2 = (decimal)Math.Pow(Convert.ToDouble(l1),2);
            decimal l2_2 = (decimal)Math.Pow(Convert.ToDouble(l2), 2);
            decimal l3_2 = (decimal)Math.Pow(Convert.ToDouble(l3), 2);
            decimal l4_2 = (decimal)Math.Pow(Convert.ToDouble(l4), 2);
            decimal l5_2 = (decimal)Math.Pow(Convert.ToDouble(l5), 2);
            decimal l6_2 = (decimal)Math.Pow(Convert.ToDouble(l6), 2);

            p = (l1 + l2 + l3) / 2;

            s = (p - l1) * (p - l2);

            v = 1 / 144*
                ((l1_2 * l5_2 * (l2_2 + l3_2 + l4_2 + l6_2 - l1_2 - l5_2)) 
                +
                (l2_2 * l6_2 * (l1_2 + l3_2 + l4_2 + l5_2 - l2_2 - l6_2))
                +
                (l3_2 * l4_2 * (l1_2 + l2_2 + l5_2 + l6_2 - l3_2 - l4_2))
                -
                (l1_2 * l2_2 * l4_2)
                -
                (l2_2 * l3_2 * l5_2)
                -
                (l1_2 * l3_2 * l6_2)
                -
                (l4_2 * l5_2 * l6_2) 
                );

            v = (decimal)Math.Sqrt((double)v);

            h = (3 * v) / s;
            x = (xA + xB + xC) / 3;
            y = (yA + yB + yC) / 3;

            newPoint.X = x;
            newPoint.Y = y;
            newPoint.Z = h;

            return newPoint;
        }

        public PointOnChart ConvertToPointFirst(OptimizerReport report)
        {
            PointOnChart newPoint = new PointOnChart();
            newPoint.Report = report;

            return newPoint;
        }

        public PointOnChart ConvertToPointSecond(OptimizerReport report)
        {
            PointOnChart newPoint = new PointOnChart();
            newPoint.Report = report;

            newPoint.X = GetEuclidLength(newPoint, Points[0]);

            return newPoint;
        }

        public PointOnChart ConvertToPointFird(OptimizerReport report)
        {
            PointOnChart newPoint = new PointOnChart();
            newPoint.Report = report;

            PointOnChart pointOne = Points[0];
            PointOnChart pointTwo = Points[1];

            decimal d = GetLengthBetweenPointsOnChart(pointOne.X, pointOne.Y, pointTwo.X, pointTwo.Y);
            decimal c = GetEuclidLength(newPoint, pointOne);
            decimal b = GetEuclidLength(newPoint, pointTwo);

            decimal p = (d + c + b) / 2;
            decimal Y = (decimal)Math.Sqrt((double)(p*(p - d)*(p - b)*(p - c)));

            decimal X = (decimal)Math.Sqrt(Math.Pow((double)c, 2) - Math.Pow((double)Y, 2));

            newPoint.X = X;
            newPoint.Y = Y;

            return newPoint;
        }

        private decimal GetLengthBetweenPointsOnChart(decimal x1, decimal y1, decimal x2, decimal y2)
        {
            double value = Math.Sqrt(Math.Pow(Convert.ToDouble(x2 - x1), 2) + Math.Pow(Convert.ToDouble(y2 - y1), 2));

            return Convert.ToDecimal(value);
        }

        public decimal GetEuclidLength(PointOnChart pointOne, PointOnChart pointTwo)
        {
            List<decimal> valuesOne = GetDecimalsArray(pointOne.Report.GetParameters());
            List<decimal> valuesTwo = GetDecimalsArray(pointTwo.Report.GetParameters());

            double summ = 0;

            for(int i = 0;i < valuesOne.Count;i++)
            {
                summ += Math.Pow(Convert.ToDouble(
                    Math.Abs(valuesOne[i] - valuesTwo[i])
                    ),2);
            }

            decimal result = Convert.ToDecimal(Math.Sqrt(summ));


            return result;
        }

        private List<decimal> GetDecimalsArray(List<IIStrategyParameter> param)
        {
            List<decimal> values = new List<decimal>();

            List<IIStrategyParameter> paramsOne = param;

            for (int i = 0; i < paramsOne.Count; i++)
            {
                if (paramsOne[i].Type == StrategyParameterType.Decimal)
                {
                    values.Add(((StrategyParameterDecimal)paramsOne[i]).ValueDecimal);
                }
                if (paramsOne[i].Type == StrategyParameterType.Int)
                {
                    values.Add(((StrategyParameterInt)paramsOne[i]).ValueInt);
                }
            }

            return values;
        }
    }

    public class PointOnChart
    {
        public decimal X;

        public decimal Y;

        public decimal Z;

        public OptimizerReport Report;

        public Color PointColor;
    }

    public class Point
    {
        public decimal Y;

        public decimal X;
    }
}