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
    /// candles indicator interface
    /// интерфейс для индикаторов на свечках
    /// </summary>
    public interface IIndicator
    {

        /// <summary>
        /// sequence type
        ///  тип последовательности
        /// </summary>
        IndicatorChartPaintType TypeIndicator { get; set; }

        /// <summary>
        /// indicator colors
        /// цвета индикатора
        /// </summary>
        List<Color> Colors { get;}

        /// <summary>
        /// all values in one array
        /// все значения в одном массиве
        /// </summary>
        List<List<decimal>> ValuesToChart { get;}

        /// <summary>
        /// whether indicator can be removed from chart. This is necessary so that robots can't be removed /можно ли удалить индикатор с графика. Это нужно для того чтобы у роботов нельзя было удалить 
        /// indicators he needs in trading/индикаторы которые ему нужны в торговле
        /// </summary>
        bool CanDelete { get; set; }

        /// <summary>
        /// name of data series on which indicator will be drawn
        /// название серии данных на которой прорисовывается индикатор
        /// </summary>
        string NameSeries { get; set; }

        /// <summary>
        /// name of data area where indicator will be drawn
        /// название области на которой прорисовывается индикатор
        /// </summary>
        string NameArea { get; set; }

        /// <summary>
        /// unique indicator name
        /// Имя
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// is indicator tracing enabled
        /// Надо ли прорисовывать индикатор на чарте
        /// </summary>
        bool PaintOn { get; set; }

        /// <summary>
        /// save settings to file
        /// Сохранить настройки
        /// </summary>
        void Save();

        /// <summary>
        /// upload settings from file
        /// Загрузить настройки
        /// </summary>
        void Load();

        /// <summary>
        /// delete file with settings
        /// Удалить файл с настройками
        /// </summary>
        void Delete();

        /// <summary>
        /// clear data
        /// очистить данные
        /// </summary>
        void Clear();

        /// <summary>
        /// display settings window
        /// Показать меню настройки
        /// </summary>
        void ShowDialog();

        /// <summary>
        /// indicator needs to be redrawn
        /// требуется перерисовать индикатор
        /// </summary>
        event Action<IIndicator> NeadToReloadEvent;

        /// <summary>
        /// Update indicator values
        /// Обновить значения индикатора
        /// </summary>
        void Process(List<Candle> candles);

    }

    /// <summary>
    /// Indicator type
    /// Тип индикатора
    /// </summary>
    public enum IndicatorChartPaintType
    {
        /// <summary>
        /// Line
        /// Линия
        /// </summary>
        Line,
        /// <summary>
        /// Column
        /// Столбец
        /// </summary>
        Column,
        /// <summary>
        /// Point
        /// Точка
        /// </summary>
        Point
    }
}
