namespace OsEngine.Language
{
    public class MainWindowLocal
    {

        public string Title => OsLocalization.ConvertToLocString(
            "Eng:Main_"+
            "Ru:Главное меню_");

        public string BlockDataLabel => OsLocalization.ConvertToLocString(
            "Eng:   Work \n with data_"+
            "Ru:    Работа \nс данными_");

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
    }
}
