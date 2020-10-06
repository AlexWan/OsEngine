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
            "Ru:Работа с данными_");

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

        public string OsOptimizerName => OsLocalization.ConvertToLocString(
            "Eng:Optimizer_"+
            "Ru:Оптимизатор");

        public string OsMinerName => OsLocalization.ConvertToLocString(
            "Eng:Miner_"+
            "Ru:Майнер");

        public string OsBotStationName => OsLocalization.ConvertToLocString(
            "Eng:BotStation_"+
            "Ru:Роботы");

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
            "Eng:Something went wrong !!! An unhandled exception occurred. Now we need to do the following: \r "
            + "1) Save the image of this window by pressing PrintScrin on the keyboard and paste the image into Paint \r"
            + "2) Write the details of the incident. Under what circumstances the program crashed \r"
            + "3) Send an image of this window and a description of the situation to the address: alexey@o-s-a.net \r"
            + "4) If the situation recurs, it will probably be necessary to clear the Engine and Data folder next to the exу file \r"
            + "5) You may have to delete the Os.Engine process from the task manager with your hands. \r"
            + "6) Error: _" +

            "Ru:ВСЁ ПРОПАЛО!!!Произошло не обработанное исключение.Сейчас нужно сделать следущее: \r"
            + "1) Сохранить изображение этого окна, нажав PrintScrin на клавиатуре и вставить изображение в Paint  \r "
            + "2) Написать подробности произошедшего инцидента. При каких обстоятельствах программа упала  \r "
            + "3) Выслать изображение этого окна и описание ситуации на адрес: alexey@o-s-a.net \r "
            + "4) Если ситуация повториться, вероятно будет нужно очистить папку Engine и Data что рядом с роботом  \r "
            + "5) Возможно придётся удалить процесс Os.Engine из диспетчера задач руками.  \r "
            + "6) Ошибка:  _"
            );
    }
}
