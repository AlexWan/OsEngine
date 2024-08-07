using System;

namespace OsEngine.Helpers
{
    /// <summary>
    /// Вспомогательные функции для парсинга даты-времени.
    /// </summary>
    public static class DateTimeParseHelper
    {
        /// <summary>
        /// Парсит дату-время из двух строк, строки даты и строки времени.
        /// </summary>
        /// <param name="dateString">Строка даты в формате "YYYYMMDD".</param>
        /// <param name="timeString">Строка времени в формате "HHmmSS".</param>
        public static DateTime ParseFromTwoStrings(string dateString, string timeString)
        {
            ParseDateOrTimeString(dateString, out int year, out int month, out int day);
            ParseDateOrTimeString(timeString, out int hour, out int minute, out int second);
            return new DateTime(year, month, day, hour, minute, second);
        }

        /// <summary>
        /// Парсит строку даты или времени в выходные переменные год-месяц-день (если строка даты) либо час-минута-секунда (если строка времени).
        /// </summary>
        /// <remarks>
        /// Хоть строки и представляют собой разные сущности, логика парсинга одинакова.
        /// </remarks>
        public static void ParseDateOrTimeString(string dateOrTimeString, out int yearHour, out int monthMinute, out int daySecond)
        {
            // по-хорошему сделать бы проверок на длину хотя бы, но, с другой стороны, это будет медленнее, да и все равно, как падать, если все равно падать
            int dateOrTimeInt = Convert.ToInt32(dateOrTimeString);
            yearHour = dateOrTimeInt / 10000;
            monthMinute = dateOrTimeInt / 100 % 100;
            daySecond = dateOrTimeInt % 100;
        }
    }
}