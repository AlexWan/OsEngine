/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Language
{
    public class ChartsLocal
    {

        public string LabelPaintIntdicatorIsVisible => OsLocalization.ConvertToLocString(
            "Eng:Is Visible_" +
            "Ru:Прорисовывать_");
        public string LabelIndicatorExponential => OsLocalization.ConvertToLocString(
            "Eng:Exponentially weighted_" +
            "Ru:Экспоненциальное взвешивание_");
        public string LabelIndicatorCorrectionCoeff => OsLocalization.ConvertToLocString(
            "Eng:Correction Coefficient_" +
            "Ru:Коэффциент коррекции_");

        public string LabelIndicatorMultiplier => OsLocalization.ConvertToLocString(
            "Eng:Multiplier_" +
            "Ru:Множитель_");
        public string LabelButtonIndicatorColor => OsLocalization.ConvertToLocString(
            "Eng:Color_" +
            "Ru:Цвет_");

        public string LabelButtonIndicatorColorUp => OsLocalization.ConvertToLocString(
            "Eng:Color Up_" +
            "Ru:Цвет верхней_");

        public string LabelButtonIndicatorColorDown => OsLocalization.ConvertToLocString(
            "Eng:Color Down_" +
            "Ru:Цвет нижней_");

        public string LabelButtonIndicatorAccept => OsLocalization.ConvertToLocString(
            "Eng:Accept_" +
            "Ru:Принять_");

        public string LabelIndicatorLongPeriod => OsLocalization.ConvertToLocString(
            "Eng:Long Period_" +
            "Ru:Длинный период_");

        public string LabelIndicatorShortPeriod => OsLocalization.ConvertToLocString(
            "Eng:Short Period_" +
            "Ru:Короткий период_");

        public string LabelIndicatorPeriod => OsLocalization.ConvertToLocString(
            "Eng:Period_" +
            "Ru:Длина_");

        public string LabelIndicatorMethod => OsLocalization.ConvertToLocString(
            "Eng:Calculation method_" +
            "Ru:Метод расчёта_");

        public string LabelIndicatorShift => OsLocalization.ConvertToLocString(
            "Eng:Shift_" +
            "Ru:Сдвиг_");

        public string LabelIndicatorDeviation => OsLocalization.ConvertToLocString(
            "Eng:Deviation_" +
            "Ru:Отклонений_");

        public string LabelIndicatorSmoothing => OsLocalization.ConvertToLocString(
            "Eng:Smoothing_" +
            "Ru:Сглаживание_");

        public string LabelIndicatorStep => OsLocalization.ConvertToLocString(
            "Eng:Step_" +
            "Ru:Шаг_");

        public string LabelIndicatorMaxStep => OsLocalization.ConvertToLocString(
            "Eng:Max step_" +
            "Ru:Максимальный шаг_");

        public string LabelIndicatorAlligator1 => OsLocalization.ConvertToLocString(
            "Eng:Alligator Lips. Fast_" +
            "Ru:Губы Аллигатора. Быстрая_");

        public string LabelIndicatorAlligator2 => OsLocalization.ConvertToLocString(
            "Eng:Alligator's Jaw. Slow_" +
            "Ru:Челюсть Аллигатора. Медленная_");

        public string LabelIndicatorAlligator3 => OsLocalization.ConvertToLocString(
            "Eng:Alligator's Teeth. Middle_" +
            "Ru:Зубы Аллигатора. Средняя_");

        public string LabelIndicatorSettingsSma => OsLocalization.ConvertToLocString(
            "Eng:Sma settings_" +
            "Ru:Настройки средней_");

        public string LabelIndicatorCandleType => OsLocalization.ConvertToLocString(
            "Eng:Candle type_" +
            "Ru:Тип свечи_");

        public string LabelIndicatorCandlePriceType => OsLocalization.ConvertToLocString(
            "Eng:Candle price type_" +
            "Ru:Тип цены свечи_");

        public string LabelIndicatorSmaType => OsLocalization.ConvertToLocString(
            "Eng:Sma type_" +
            "Ru:Тип средней_");

        public string LabelIndicatorType => OsLocalization.ConvertToLocString(
            "Eng:Indicator type_" +
            "Ru:Тип индикатора_");

        public string LabelIndicatorAreasOnChart => OsLocalization.ConvertToLocString(
            "Eng:Area on chart_" +
            "Ru:Окна на графике_");


        public string TitleIndicatorCreateUi => OsLocalization.ConvertToLocString(
            "Eng:Create indicator_" +
            "Ru:Добавить индикатор_");

        public string ChartMenuItem1 => OsLocalization.ConvertToLocString(
            "Eng:Drawing a chart_" +
            "Ru:Отрисовка чарта_");

        public string ChartMenuItem2 => OsLocalization.ConvertToLocString(
            "Eng:Color scheme_" +
            "Ru:Цветовая схема_");

        public string ChartMenuItem3 => OsLocalization.ConvertToLocString(
            "Eng:Dark_" +
            "Ru:Тёмная_");

        public string ChartMenuItem4 => OsLocalization.ConvertToLocString(
            "Eng:White_" +
            "Ru:Светлая_");

        public string ChartMenuItem5 => OsLocalization.ConvertToLocString(
            "Eng:Deal figure_" +
            "Ru:Фигура сделки_");

        public string ChartMenuItem6 => OsLocalization.ConvertToLocString(
            "Eng:Cross_" +
            "Ru:Перекрестие_");

        public string ChartMenuItem7 => OsLocalization.ConvertToLocString(
            "Eng:Rhomb_" +
            "Ru:Ромб_");

        public string ChartMenuItem8 => OsLocalization.ConvertToLocString(
            "Eng:Point_" +
            "Ru:Точка_");

        public string ChartMenuItem9 => OsLocalization.ConvertToLocString(
            "Eng:Triangle (dont use in debug regime)_" +
            "Ru:Треугольник (тормозит при дебаггинге)_");

        public string ChartMenuItem10 => OsLocalization.ConvertToLocString(
            "Eng:Hide areas_" +
            "Ru:Скрыть области_");


        public string ChartMenuItem11 => OsLocalization.ConvertToLocString(
            "Eng:Show areas_" +
            "Ru:Показать области_");

        public string ChartMenuItem12 => OsLocalization.ConvertToLocString(
            "Eng:Edit indicator_" +
            "Ru:Редактировать индикатор_");

        public string ChartMenuItem13 => OsLocalization.ConvertToLocString(
            "Eng:Delete indicator_" +
            "Ru:Удалить индикатор_");

        public string ChartMenuItem14 => OsLocalization.ConvertToLocString(
            "Eng:Create indicator_" +
            "Ru:Добавить индикатор_");

        public string Label1 => OsLocalization.ConvertToLocString(
            "Eng: Settings _" +
            "Ru: Настройки _");

        public string Label2 => OsLocalization.ConvertToLocString(
            "Eng: Parameters _" +
            "Ru: Параметры _");

        public string Label3 => OsLocalization.ConvertToLocString(
            "Eng: Visual _" +
            "Ru: Визуал _");

        public string Label4 => OsLocalization.ConvertToLocString(
            "Eng: Include indicators _" +
            "Ru: Встроенные индикаторы _");

        public string Label5 => OsLocalization.ConvertToLocString(
            "Eng:Default_" +
            "Ru:Сбросить_");

        public string Label6 => OsLocalization.ConvertToLocString(
            "Eng: Include _" +
            "Ru: Встроенные _");

        public string Label7 => OsLocalization.ConvertToLocString(
            "Eng: Scripts _" +
            "Ru:Скрипты_");
    }
}
