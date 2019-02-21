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
    public class ConverterLocal
    {
        public string Message1 => OsLocalization.ConvertToLocString(
            "Eng:The conversion procedure cannot be started. the thread is already running_" +
            "Ru:Процедура конвертации не может быть запущена т.к. поток уже запущен_");

        public string Message2 => OsLocalization.ConvertToLocString(
            "Eng:The conversion procedure cannot be started. No source file specified_" +
            "Ru:Процедура конвертации не может быть запущеана. Не указан файл с исходными данными_");

        public string Message3 => OsLocalization.ConvertToLocString(
            "Eng:The conversion procedure cannot be started. No output file specified_" +
            "Ru:Процедура конвертации не может быть запущеана. Не указан файл с выходными данными_");

        public string Message4 => OsLocalization.ConvertToLocString(
            "Eng:Conversion procedure started_" +
            "Ru:Процедура конвертации начата_");

        public string Message5 => OsLocalization.ConvertToLocString(
            "Eng:Load trades from file_" +
            "Ru:Загружаем тики из файла_");

        public string Message6 => OsLocalization.ConvertToLocString(
            "Eng:Load week _" +
            "Ru:Грузим неделю _");

        public string Message7 => OsLocalization.ConvertToLocString(
            "Eng:Month _" +
            "Ru:Месяц _");

        public string Message8 => OsLocalization.ConvertToLocString(
            "Eng:loaded. Create a series of candles_" +
            "Ru: подгружен. Создаём серии свечек_");

        public string Message9 => OsLocalization.ConvertToLocString(
            "Eng:Save ended_" +
            "Ru:Сохранение завершено_");

        public string Message10 => OsLocalization.ConvertToLocString(
            "Eng:Download aborted. Wrong format in data file_" +
            "Ru:Скачивание прервано. В файле данных не верный формат_");

        public string Label1 => OsLocalization.ConvertToLocString(
            "Eng:Initial data_" +
            "Ru:Исходные данные_");

        public string Label2 => OsLocalization.ConvertToLocString(
            "Eng:Outgoing data_" +
            "Ru:Исходящие данные_");

        public string Label3 => OsLocalization.ConvertToLocString(
            "Eng:Specify_" +
            "Ru:Указать_");

        public string Label4 => OsLocalization.ConvertToLocString(
            "Eng:Logging_" +
            "Ru:Лог_");

        public string Label5 => OsLocalization.ConvertToLocString(
            "Eng:Start conversion_" +
            "Ru:Начать конвертацию_");
    }
}
