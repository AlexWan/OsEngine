namespace OsEngine.Market.Servers.Entity
{
    /// <summary>
    /// optional interface of a server realization that can tell margin mode and leverage of a security.
    /// Read only: it does not change anything on the exchange
    /// необязательный интерфейс реализации сервера, который знает режим маржи и плечо инструмента.
    /// Только чтение: на бирже ничего не меняет
    /// </summary>
    public interface IServerMarginInfo
    {
        /// <summary>
        /// last margin settings received from the exchange, null if unknown
        /// последние настройки маржи, полученные от биржи, null если неизвестны
        /// </summary>
        /// <param name="securityNameCode">security name as in Security.Name</param>
        SecurityMarginInfo GetMarginInfo(string securityNameCode);
    }
}
