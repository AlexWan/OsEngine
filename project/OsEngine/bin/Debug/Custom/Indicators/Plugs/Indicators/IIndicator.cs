/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{

    /// <summary>
    /// indicator interface
    /// </summary>
    public interface IIndicator
    {

        /// <summary>
        /// sequence type
        /// </summary>
        IndicatorChartPaintType TypeIndicator { get; set; }

        /// <summary>
        /// indicator colors
        /// </summary>
        List<Color> Colors { get; }

        /// <summary>
        /// all values in one array
        /// </summary>
        List<List<decimal>> ValuesToChart { get; }

        /// <summary>
        /// whether the indicator can be removed from the chart. Or it is reserved for the robot. True - you can remove it
        /// </summary>
        bool CanDelete { get; set; }

        /// <summary>
        /// name of data series on which indicator will be drawn
        /// </summary>
        string NameSeries { get; set; }

        /// <summary>
        /// name of data area where indicator will be drawn
        /// </summary>
        string NameArea { get; set; }

        /// <summary>
        /// unique indicator name
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// whether indicator drawing is enabled
        /// </summary>
        bool PaintOn { get; set; }

        /// <summary>
        /// save settings to file
        /// </summary>
        void Save();

        /// <summary>
        /// upload settings from file
        /// </summary>
        void Load();

        /// <summary>
        /// delete file with settings
        /// </summary>
        void Delete();

        /// <summary>
        /// clear data
        /// </summary>
        void Clear();

        /// <summary>
        /// display settings window
        /// </summary>
        void ShowDialog();

        /// <summary>
        /// indicator needs to be redrawn
        /// </summary>
        event Action<IIndicator> NeedToReloadEvent;

        /// <summary>
        /// update indicator values
        /// </summary>
        void Process(List<Candle> candles);

    }

    /// <summary>
    /// Indicator type
    /// </summary>
    public enum IndicatorChartPaintType
    {
        /// <summary>
        /// Line
        /// </summary>
        Line,

        /// <summary>
        /// Column
        /// </summary>
        Column,

        /// <summary>
        /// Point
        /// </summary>
        Point,

        /// <summary>
        /// Candle
        /// </summary>
        Candle

    }
}