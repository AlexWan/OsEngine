/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Text.Json;
using OsEngine.Logging;
using OsEngine.MCP.Json;
using OsEngine.PrimeSettings;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API handlers for terminal prime settings.
    /// </summary>
    public class SettingsApi : IMcpToolProvider
    {
        #region Fields

        private readonly Action<string, object> _publishEvent;

        #endregion

        #region Events

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion

        #region Constructors

        public SettingsApi(Action<string, object> publishEvent)
        {
            _publishEvent = publishEvent;
            PrimeSettingsMaster.SettingsChanged += PrimeSettingsMaster_SettingsChanged;
        }

        #endregion

        #region Public methods

        public McpJsonRpcResponse Handle(McpJsonRpcRequest request)
        {
            McpJsonRpcResponse response = new McpJsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = request.Id
            };

            try
            {
                switch (request.Method)
                {
                    case "prime_settings_get":
                        response.Result = GetPrimeSettings();
                        break;

                    case "prime_settings_set":
                        response.Result = SetPrimeSettings(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in settings API"
                        };
                        break;
                }
            }
            catch (Exception error)
            {
                response.Error = new McpJsonRpcError
                {
                    Code = -32603,
                    Message = error.Message
                };
            }

            return response;
        }

        #endregion

        #region Event handlers

        private void PrimeSettingsMaster_SettingsChanged()
        {
            try
            {
                _publishEvent("prime_settings.changed", McpPrimeSettings.FromCurrent());
            }
            catch (Exception error)
            {
                SendLog($"Send prime_settings.changed failed: {error}", LogMessageType.Error);
            }
        }

        #endregion

        #region Private methods

        private McpPrimeSettings GetPrimeSettings()
        {
            return McpPrimeSettings.FromCurrent();
        }

        private object SetPrimeSettings(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Parameters must be an object");
            }

            if (parameters.TryGetProperty("errorLogMessageBoxIsActive", out JsonElement errorLogMessageBoxIsActive)
                && (errorLogMessageBoxIsActive.ValueKind == JsonValueKind.True || errorLogMessageBoxIsActive.ValueKind == JsonValueKind.False))
            {
                PrimeSettingsMaster.ErrorLogMessageBoxIsActive = errorLogMessageBoxIsActive.GetBoolean();
            }

            if (parameters.TryGetProperty("errorLogBeepIsActive", out JsonElement errorLogBeepIsActive)
                && (errorLogBeepIsActive.ValueKind == JsonValueKind.True || errorLogBeepIsActive.ValueKind == JsonValueKind.False))
            {
                PrimeSettingsMaster.ErrorLogBeepIsActive = errorLogBeepIsActive.GetBoolean();
            }

            if (parameters.TryGetProperty("transactionBeepIsActive", out JsonElement transactionBeepIsActive)
                && (transactionBeepIsActive.ValueKind == JsonValueKind.True || transactionBeepIsActive.ValueKind == JsonValueKind.False))
            {
                PrimeSettingsMaster.TransactionBeepIsActive = transactionBeepIsActive.GetBoolean();
            }

            if (parameters.TryGetProperty("rebootTradeUiLight", out JsonElement rebootTradeUiLight)
                && (rebootTradeUiLight.ValueKind == JsonValueKind.True || rebootTradeUiLight.ValueKind == JsonValueKind.False))
            {
                PrimeSettingsMaster.RebootTradeUiLight = rebootTradeUiLight.GetBoolean();
            }

            if (parameters.TryGetProperty("reportCriticalErrors", out JsonElement reportCriticalErrors)
                && (reportCriticalErrors.ValueKind == JsonValueKind.True || reportCriticalErrors.ValueKind == JsonValueKind.False))
            {
                PrimeSettingsMaster.ReportCriticalErrors = reportCriticalErrors.GetBoolean();
            }

            if (parameters.TryGetProperty("labelInHeaderBotStation", out JsonElement labelInHeaderBotStation)
                && labelInHeaderBotStation.ValueKind == JsonValueKind.String)
            {
                PrimeSettingsMaster.LabelInHeaderBotStation = labelInHeaderBotStation.GetString();
            }

            if (parameters.TryGetProperty("memoryCleanerRegime", out JsonElement memoryCleanerRegime)
                && memoryCleanerRegime.ValueKind == JsonValueKind.String
                && Enum.TryParse<MemoryCleanerRegime>(memoryCleanerRegime.GetString(), out MemoryCleanerRegime regime))
            {
                PrimeSettingsMaster.MemoryCleanerRegime = regime;
            }

            return new
            {
                Success = true,
                Settings = McpPrimeSettings.FromCurrent()
            };
        }

        public List<McpTool> GetTools()
        {
            return new List<McpTool>
            {
                new McpTool { Name = "prime_settings_get", Description = "Get terminal prime settings", InputSchema = new { type = "object", properties = new { }, required = new string[0] } },
                new McpTool { Name = "prime_settings_set", Description = "Set terminal prime settings", InputSchema = new { type = "object", properties = new { }, required = new string[0] } }
            };
        }

        private void SendLog(string message, LogMessageType type)
        {
            NewLogMessageEvent?.Invoke(message, type);
        }

        #endregion
    }
}
