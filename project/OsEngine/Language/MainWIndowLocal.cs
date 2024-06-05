/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Language
{
    public class MainWindowLocal
    {

        public string Title => OsLocalization.ConvertToLocString(
            "Eng:Main_"+
            "Ru:Главное меню_");

        public string BlockDataLabel => OsLocalization.ConvertToLocString(
            "Eng:Work with data_"+
            "Ru:Данные_");

        public string BlockTestingLabel => OsLocalization.ConvertToLocString(
            "Eng:Testing_"+
            "Ru:Тестирование");

        public string BlockTradingLabel => OsLocalization.ConvertToLocString(
            "Eng:Trading_"+
            "Ru:Торговля");

        public string OsDataName => OsLocalization.ConvertToLocString(
            "Eng:Data_"+
            "Ru:Дата");

        public string OsConverter => OsLocalization.ConvertToLocString(
            "Eng:Converter_"+
            "Ru:Конвертер");

        public string OsCandleConverter => OsLocalization.ConvertToLocString(
            "Eng:Candle Converter_" +
            "Ru:Конвертер свечей");

        public string OsTesterName => OsLocalization.ConvertToLocString(
            "Eng:Tester_"+
            "Ru:Тестер");

        public string OsTesterLightName => OsLocalization.ConvertToLocString(
            "Eng:Tester Light_" +
            "Ru:Тестер. Light");

        public string OsOptimizerName => OsLocalization.ConvertToLocString(
            "Eng:Optimizer_"+
            "Ru:Оптимизатор");

        public string OsMinerName => OsLocalization.ConvertToLocString(
            "Eng:Miner_"+
            "Ru:Майнер");

        public string OsBotStationName => OsLocalization.ConvertToLocString(
            "Eng:Bot Station_"+
            "Ru:Роботы");

        public string OsBotStationLightName => OsLocalization.ConvertToLocString(
            "Eng:Bot Station Light_" +
            "Ru:Роботы. Light");

        public string Message1 => OsLocalization.ConvertToLocString(
            "Eng:Your operating system does not match the operating parameters of the terminal. Need to use a minimum of Windows 7_" +
            "Ru:Ваша оперативная система не соответствуют рабочим параметрам терминала. Нужно использовать минимум Windows 7_");

        public string Message2 => OsLocalization.ConvertToLocString(
            "Eng:Your operating system does not allow the program to save data. Restart it from admin._" +
            "Ru:Ваша оперативная система не даёт программе сохранять данные. Перезапустите её из под администратора._");

        public string Message3 => OsLocalization.ConvertToLocString(
            "Eng:Error trying to check Windows version. The program is closed. Describe the system in which you are trying to run the program and write to the develope_" +
            "Ru:Ошибка при попытке проверить версию Windows.Программа закрыта.Опишите систему в которой вы пытаетесь запустить программу и напишите разработчику_");

        public string Message4 => OsLocalization.ConvertToLocString(
            "Eng:Your version of .Net 4.5 or later. The robot will not work on your system. See chapter for instructions: Windows and .Net requirements_" +
            "Ru:Ваша версия .Net 4.5 or later. Робот не будет работать в Вашей системе. С.м. в инструкции главу: Требования к Windows и .Net_");

        public string Message5 => OsLocalization.ConvertToLocString(
            "Eng:Something went wrong !!! An unhandled exception occurred. " +
            "Ru:ВСЁ ПРОПАЛО!!! Произошло не обработанное исключение. " );

        public string Message6 => OsLocalization.ConvertToLocString(
            "Eng:It looks like you didn't run the program from its folder. Not by creating a shortcut, but by moving the program. You can't do that!_" +
            "Ru:Похоже Вы запустили программу не из папки с ней. Не создав ярлык, а обычным перемещением. Так нельзя!_");

        public string Message7 => OsLocalization.ConvertToLocString(
           "Eng:Os Engine is already running from this directory. You cannot run a second one!_" +
           "Ru:Os Engine уже запущен из данной директории. Второй запускать нельзя!_");

    }
}
