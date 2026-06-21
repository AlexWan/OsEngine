/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.MCP.Json;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// Provides descriptors for MCP tools implemented by a module.
    /// </summary>
    public interface IMcpToolProvider
    {
        List<McpTool> GetTools();
    }
}
