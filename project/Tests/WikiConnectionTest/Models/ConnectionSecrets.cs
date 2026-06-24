/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple.pdf
*/

namespace WikiConnectionTest.Models
{
    /// <summary>
    /// Connector credentials stored as name/value parameters per connector type.
    /// Format matches the test stand secrets: each connector has a dictionary of parameters.
    /// </summary>
    public class ConnectionSecrets
    {
        /// <summary>
        /// Connector type -> parameter name -> parameter value.
        /// Example:
        /// {
        ///   "TInvest": { "Token": "t.xxx" },
        ///   "Alor": { "Token": "..." }
        /// }
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> Connectors { get; set; } = new Dictionary<string, Dictionary<string, string>>();
    }
}
