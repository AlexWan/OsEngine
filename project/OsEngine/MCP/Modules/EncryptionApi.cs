/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Text.Json;
using OsEngine.Logging;
using OsEngine.Market.ServerEncryption;
using OsEngine.MCP.Json;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API handlers for the global server passwords encryption (master password).
    /// Disable requires the master password and is verified the same way as unlock.
    /// </summary>
    public class EncryptionApi : IMcpToolProvider
    {
        #region Fields

        private static readonly object _unlockRateLocker = new object();

        private static int _unlockFailures;

        private static DateTime _unlockBannedUntil = DateTime.MinValue;

        private const int UnlockMaxFailures = 5;

        private const int UnlockBanMinutes = 5;

        #endregion

        #region Events

        public event Action<string, LogMessageType> NewLogMessageEvent;

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
                    case "encryption_get_status":
                        response.Result = GetStatus();
                        break;

                    case "encryption_unlock":
                        response.Result = Unlock(request.Params);
                        break;

                    case "encryption_enable":
                        response.Result = Enable(request.Params);
                        break;

                    case "encryption_disable":
                        response.Result = Disable(request.Params);
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in encryption API"
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

        public List<McpTool> GetTools()
        {
            return new List<McpTool>
            {
                new McpTool { Name = "encryption_get_status", Description = "Get global encryption status (Plain/Encrypted/Declined and unlocked flag)", InputSchema = new { type = "object", properties = new { }, required = new string[0] } },
                new McpTool { Name = "encryption_unlock", Description = "Unlock the encryptor with the master password for this session", InputSchema = new { type = "object", properties = new { password = new { type = "string", description = "Master password" } }, required = new[] { "password" } } },
                new McpTool { Name = "encryption_enable", Description = "Enable encryption with a new master password (min 8 chars). Works only when encryption is not enabled", InputSchema = new { type = "object", properties = new { password = new { type = "string", description = "New master password, min 8 characters" } }, required = new[] { "password" } } },
                new McpTool { Name = "encryption_disable", Description = "Disable encryption and decrypt all server passwords to plain text (destructive). Requires the current master password", InputSchema = new { type = "object", properties = new { password = new { type = "string", description = "Current master password" } }, required = new[] { "password" } } }
            };
        }

        #endregion

        #region Private methods

        private object GetStatus()
        {
            ServerEncryptionStatus status = ServerEncryptionMaster.GetStatus();

            string statusStr = "Plain";

            if (status == ServerEncryptionStatus.Encrypted)
            {
                statusStr = "Encrypted";
            }
            else if (status == ServerEncryptionStatus.Declined)
            {
                statusStr = "Declined";
            }

            return new
            {
                Status = statusStr,
                Unlocked = ServerEncryptionMaster.IsUnlocked
            };
        }

        private object Unlock(JsonElement parameters)
        {
            string password = ParsePassword(parameters);

            lock (_unlockRateLocker)
            {
                if (DateTime.Now < _unlockBannedUntil)
                {
                    throw new ArgumentException("Too many failed unlock attempts. Try again after " + _unlockBannedUntil.ToString("HH:mm:ss"));
                }
            }

            if (ServerEncryptionMaster.IsUnlocked)
            {
                return new
                {
                    Success = true,
                    AlreadyUnlocked = true
                };
            }

            if (ServerEncryptionMaster.GetStatus() != ServerEncryptionStatus.Encrypted)
            {
                throw new ArgumentException("Encryption is not enabled");
            }

            if (ServerEncryptionMaster.TryUnlock(password) == false)
            {
                lock (_unlockRateLocker)
                {
                    _unlockFailures++;

                    if (_unlockFailures >= UnlockMaxFailures)
                    {
                        _unlockBannedUntil = DateTime.Now.AddMinutes(UnlockBanMinutes);
                        _unlockFailures = 0;
                    }
                }

                throw new ArgumentException("Wrong password");
            }

            lock (_unlockRateLocker)
            {
                _unlockFailures = 0;
                _unlockBannedUntil = DateTime.MinValue;
            }

            SendLog("Encryptor unlocked via MCP API", LogMessageType.System);

            return new
            {
                Success = true,
                AlreadyUnlocked = false
            };
        }

        private object Enable(JsonElement parameters)
        {
            string password = ParsePassword(parameters);

            if (password.Length < 8)
            {
                throw new ArgumentException("Password must be at least 8 characters");
            }

            if (ServerEncryptionMaster.GetStatus() == ServerEncryptionStatus.Encrypted)
            {
                throw new ArgumentException("Encryption is already enabled. Changing the password via API is not allowed - use the local encryption window");
            }

            if (ServerEncryptionMaster.Enable(password) == false)
            {
                throw new ArgumentException("Failed to enable encryption. See the log for details");
            }

            SendLog("Encryption enabled via MCP API", LogMessageType.System);

            return new
            {
                Success = true
            };
        }

        private object Disable(JsonElement parameters)
        {
            string password = ParsePassword(parameters);

            if (ServerEncryptionMaster.GetStatus() != ServerEncryptionStatus.Encrypted)
            {
                throw new ArgumentException("Encryption is not enabled");
            }

            if (ServerEncryptionMaster.Disable(password) == false)
            {
                throw new ArgumentException("Failed to disable encryption. Wrong password or file error - see the log for details");
            }

            SendLog("Encryption disabled via MCP API", LogMessageType.System);

            return new
            {
                Success = true
            };
        }

        private string ParsePassword(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object
                || parameters.TryGetProperty("password", out JsonElement passwordElement) == false
                || passwordElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("Parameter 'password' is required and must be a string");
            }

            string password = passwordElement.GetString();

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Parameter 'password' must not be empty");
            }

            return password;
        }

        private void SendLog(string message, LogMessageType type)
        {
            NewLogMessageEvent?.Invoke(message, type);
        }

        #endregion
    }
}
