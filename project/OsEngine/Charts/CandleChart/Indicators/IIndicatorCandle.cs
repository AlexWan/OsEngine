/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Charts.CandleChart.Indicators
{

    /// <summary>
    /// интерфейс для индикаторов на свечках
    /// </summary>
    public interface IIndicatorCandle
    {

        /// <summary>
        ///  тип последовательности
        /// </summary>
        IndicatorOneCandleChartType TypeIndicator { get; set; }

        /// <summary>
        /// цвета индикатора
        /// </summary>
        List<Color> Colors { get;}

        /// <summary>
        /// все значения в одном массиве
        /// </summary>
        List<List<decimal>> ValuesToChart { get;}

        /// <summary>
        /// можно ли удалить индикатор с графика. Это нужно для того чтобы у роботов нельзя было удалить 
        /// индикаторы которые ему нужны в торговле
        /// </summary>
        bool CanDelete { get; set; }

        /// <summary>
        /// название серии данных на которой прорисовывается индикатор
        /// </summary>
        string NameSeries { get; set; }

        /// <summary>
        /// название области на которой прорисовывается индикатор
        /// </summary>
        string NameArea { get; set; }

        /// <summary>
        /// Имя
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Надо ли прорисовывать индикатор на чарте
        /// </summary>
        bool PaintOn { get; set; }

        /// <summary>
        /// Сохранить настройки
        /// </summary>
        void Save();

        /// <summary>
        /// Загрузить настройки
        /// </summary>
        void Load();

        /// <summary>
        /// Удалить файл с настройками
        /// </summary>
        void Delete();

        /// <summary>
        /// очистить данные
        /// </summary>
        void Clear();

        /// <summary>
        /// Показать меню настройки
        /// </summary>
        void ShowDialog();

        /// <summary>
        /// требуется перерисовать индикатор
        /// </summary>
        event Action<IIndicatorCandle> NeadToReloadEvent;

        /// <summary>
        /// Обновить значения индикатора
        /// </summary>
        void Process(List<Candle> candles);

    }

    /// <summary>
    /// Тип индикатора
    /// </summary>
    public enum IndicatorOneCandleChartType
    {
        /// <summary>
        /// Линия
        /// </summary>
        Line,
        /// <summary>
        /// Столбец
        /// </summary>
        Column,
        /// <summary>
        /// Точка
        /// </summary>
        Point
    }
}
