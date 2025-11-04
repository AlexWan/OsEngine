/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Language
{
    public class HintMessage
    {
        public string HintMessageLabel0 => OsLocalization.ConvertToLocString(
            "Eng:Price step. If it is incorrect, orders will be rounded improperly_" +
            "Ru:Шаг цены. Если он не верный, ордера будут округляться неправильно_");

        public string HintMessageLabel1 => OsLocalization.ConvertToLocString(
            "Eng:The cost of the price step. If it is incorrect, the profit from positions will be calculated incorrectly_" +
            "Ru:Стоимость шага цены. Если он не верный, прибыль с позиций будет считаться неправильно_");

        public string HintMessageLabel2 => OsLocalization.ConvertToLocString(
            "Eng:This is the amount of funds that a trader must deposit as collateral to open and maintain positions in the market_" +
            "Ru:Это сумма средств, которую трейдер должен внести в качестве залога для открытия и поддержания позиций на рынке_");

        public string HintMessageLabel3 => OsLocalization.ConvertToLocString(
            "Eng:This is the minimum number of units of the instrument that can be bought or sold in a single transaction._" +
            "Ru:Это минимальное количество единиц инструмента, которое можно купить или продать за одну сделку_");

        public string HintMessageLabel4 => OsLocalization.ConvertToLocString(
            "Eng:Number of decimal places in volume_" +
            "Ru:Кол-во знаков после запятой в объёме_");

        public string HintMessageLabel5 => OsLocalization.ConvertToLocString(
            "Eng:Add indicator to the main chart area (candlesticks)_" +
            "Ru:Добавить индикатор в основную область графика (к свечам)_");

        public string HintMessageLabel6 => OsLocalization.ConvertToLocString(
            "Eng:Add indicator to a new area below the chart_" +
            "Ru:Добавить индикатор в новую область под графиком_");

        public string HintMessageLabel7 => OsLocalization.ConvertToLocString(
            "Eng:Add indicator to an existing area below the chart_" +
            "Ru:Добавить индикатор в уже созданную область под графиком_");

        public string HintMessageLabel8 => OsLocalization.ConvertToLocString(
            "Eng:Futures expiration. By default, the date of the last candle._" +
            "Ru:Экспирация фьючерса. По умолчанию дата последней свечи_");

        public string HintMessageError0 => OsLocalization.ConvertToLocString(
            "Eng:The value of Price Step must be a positive number._" +
            "Ru:Значение Шага цены должно быть положительным числом_");

        public string HintMessageError1 => OsLocalization.ConvertToLocString(
            "Eng:The value of Price Step Price must be a positive number._" +
            "Ru:Значение Цена шага цены должно быть положительным числом_");

        public string HintMessageError2 => OsLocalization.ConvertToLocString(
            "Eng:The value of Collateral must be a positive number._" +
            "Ru:Значение ГО должно быть положительным числом_");

        public string HintMessageError3 => OsLocalization.ConvertToLocString(
            "Eng:The value of Lot must be a positive number._" +
            "Ru:Значение Лот должно быть положительным числом_");

        public string HintMessageError4 => OsLocalization.ConvertToLocString(
            "Eng:The value of VolumeDecimal must be a positive number._" +
            "Ru:Значение Точность объема должно быть положительным целым числом_");

        public string HintMessageError5 => OsLocalization.ConvertToLocString(
            "Eng:The saving process was interrupted. One of the fields contains an invalid value: _" +
            "Ru:Процесс сохранения прерван. В одном из полей не допустимое значение: _");
    }
}