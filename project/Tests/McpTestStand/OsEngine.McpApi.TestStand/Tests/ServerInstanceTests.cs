/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for operations on a specific server instance.
    /// </summary>
    public class ServerInstanceTests
    {
        private const string Module = "SERVER_INSTANCE";
        private readonly TestContext _context;
        private string _serverType = "Binance";

        public ServerInstanceTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            _serverType = !string.IsNullOrWhiteSpace(_context.Secrets.ConnectorType)
                ? _context.Secrets.ConnectorType
                : "Binance";

            ActivateConnector(_serverType);

            TestGetParams();
            TestSetParams();
            TestCreateAndDeleteInstance();
            TestDeleteInstanceZeroFails();
            TestGetDataAfterConnect();
        }

        private void ActivateConnector(string serverType)
        {
            const string method = "server_management_activate";
            object request = new { type = serverType };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestGetParams()
        {
            const string method = "server_instance_get_params";
            string serverType = _serverType;
            object request = new { type = serverType };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement root = document.RootElement;

                    if (!TryExtractToolResult(root, out JsonElement resultElement, out string error))
                    {
                        _context.RecordFail(Module, method, error);
                        return;
                    }

                    if (resultElement.ValueKind != JsonValueKind.Array)
                    {
                        _context.RecordFail(Module, method, "response result is not an array");
                        return;
                    }

                    foreach (JsonElement parameterElement in resultElement.EnumerateArray())
                    {
                        if (parameterElement.ValueKind != JsonValueKind.Object)
                        {
                            _context.RecordFail(Module, method, "parameter entry is not an object");
                            return;
                        }

                        if (!parameterElement.TryGetProperty("name", out _)
                            || !parameterElement.TryGetProperty("type", out _)
                            || !parameterElement.TryGetProperty("value", out _))
                        {
                            _context.RecordFail(Module, method, "parameter entry is missing required fields (name, type, value)");
                            return;
                        }
                    }

                    int count = resultElement.GetArrayLength();
                    _context.RecordPass(Module, method, $"returned {count} parameter(s) for {serverType}");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestSetParams()
        {
            const string getMethod = "server_instance_get_params";
            const string setMethod = "server_instance_set_params";
            string serverType = _serverType;
            object getRequest = new { type = serverType };

            try
            {
                _context.PrintRequest(Module, getMethod, getRequest);
                string getResponse = _context.Client.ToolsCall(getMethod, getRequest);
                _context.PrintResponse(getResponse);

                using (var document = JsonDocument.Parse(getResponse))
                {
                    JsonElement root = document.RootElement;

                    if (!TryExtractToolResult(root, out JsonElement resultElement, out string error))
                    {
                        _context.RecordFail(Module, setMethod, error);
                        return;
                    }

                    if (resultElement.ValueKind != JsonValueKind.Array)
                    {
                        _context.RecordFail(Module, setMethod, "get_params response result is not an array");
                        return;
                    }

                    string? targetName = null;
                    bool originalValue = false;

                    foreach (JsonElement parameterElement in resultElement.EnumerateArray())
                    {
                        if (parameterElement.TryGetProperty("name", out JsonElement nameElement)
                            && nameElement.ValueKind == JsonValueKind.String
                            && parameterElement.TryGetProperty("type", out JsonElement typeElement)
                            && typeElement.ValueKind == JsonValueKind.String
                            && typeElement.GetString() == "Bool"
                            && parameterElement.TryGetProperty("value", out JsonElement valueElement)
                            && (valueElement.ValueKind == JsonValueKind.True || valueElement.ValueKind == JsonValueKind.False))
                        {
                            targetName = nameElement.GetString();
                            originalValue = valueElement.GetBoolean();
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(targetName))
                    {
                        _context.RecordFail(Module, setMethod, "no bool parameter found to test set_params");
                        return;
                    }

                    bool newValue = !originalValue;

                    object setRequest = new
                    {
                        type = serverType,
                        parameters = new[]
                        {
                            new { name = targetName, value = newValue }
                        }
                    };

                    _context.PrintRequest(Module, setMethod, setRequest);
                    string setResponse = _context.Client.ToolsCall(setMethod, setRequest);
                    _context.PrintResponse(setResponse);

                    using (var setDocument = JsonDocument.Parse(setResponse))
                    {
                        JsonElement setRoot = setDocument.RootElement;

                        if (!TryExtractToolResult(setRoot, out JsonElement setResultElement, out error))
                        {
                            _context.RecordFail(Module, setMethod, error);
                            return;
                        }

                        if (!setResultElement.TryGetProperty("success", out JsonElement successElement)
                            || successElement.ValueKind != JsonValueKind.True)
                        {
                            _context.RecordFail(Module, setMethod, "set_params did not return success");
                            return;
                        }

                        if (!setResultElement.TryGetProperty("updated", out JsonElement updatedElement)
                            || updatedElement.ValueKind != JsonValueKind.Array
                            || updatedElement.GetArrayLength() != 1)
                        {
                            _context.RecordFail(Module, setMethod, "set_params did not return exactly one updated parameter");
                            return;
                        }
                    }

                    _context.PrintRequest(Module, getMethod, getRequest);
                    string verifyResponse = _context.Client.ToolsCall(getMethod, getRequest);
                    _context.PrintResponse(verifyResponse);

                    using (var verifyDocument = JsonDocument.Parse(verifyResponse))
                    {
                        JsonElement verifyRoot = verifyDocument.RootElement;

                        if (!TryExtractToolResult(verifyRoot, out JsonElement verifyResultElement, out error))
                        {
                            _context.RecordFail(Module, setMethod, error);
                            return;
                        }

                        bool verified = false;

                        foreach (JsonElement parameterElement in verifyResultElement.EnumerateArray())
                        {
                            if (parameterElement.TryGetProperty("name", out JsonElement nameElement)
                                && nameElement.ValueKind == JsonValueKind.String
                                && nameElement.GetString() == targetName
                                && parameterElement.TryGetProperty("value", out JsonElement valueElement)
                                && (valueElement.ValueKind == JsonValueKind.True || valueElement.ValueKind == JsonValueKind.False)
                                && valueElement.GetBoolean() == newValue)
                            {
                                verified = true;
                                break;
                            }
                        }

                        if (!verified)
                        {
                            _context.RecordFail(Module, setMethod, $"parameter '{targetName}' was not updated to {newValue}");
                            return;
                        }
                    }

                    object restoreRequest = new
                    {
                        type = serverType,
                        parameters = new[]
                        {
                            new { name = targetName, value = originalValue }
                        }
                    };

                    _context.Client.ToolsCall(setMethod, restoreRequest);

                    _context.RecordPass(Module, setMethod, $"updated '{targetName}' from {originalValue} to {newValue} and restored");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, setMethod, error.Message);
            }
        }

        private void TestCreateAndDeleteInstance()
        {
            const string createMethod = "server_instance_create";
            const string deleteMethod = "server_instance_delete";
            string serverType = _serverType;
            object createRequest = new { type = serverType };

            try
            {
                _context.PrintRequest(Module, createMethod, createRequest);
                string createResponse = _context.Client.ToolsCall(createMethod, createRequest);
                _context.PrintResponse(createResponse);

                int createdNumber;

                using (var createDocument = JsonDocument.Parse(createResponse))
                {
                    JsonElement createRoot = createDocument.RootElement;

                    if (!TryExtractToolResult(createRoot, out JsonElement createResultElement, out string error))
                    {
                        _context.RecordFail(Module, deleteMethod, error);
                        return;
                    }

                    if (!createResultElement.TryGetProperty("number", out JsonElement numberElement)
                        || numberElement.ValueKind != JsonValueKind.Number
                        || !numberElement.TryGetInt32(out createdNumber)
                        || createdNumber < 1)
                    {
                        _context.RecordFail(Module, deleteMethod, "failed to create a valid instance to delete");
                        return;
                    }
                }

                object deleteRequest = new { type = serverType, number = createdNumber };

                _context.PrintRequest(Module, deleteMethod, deleteRequest);
                string deleteResponse = _context.Client.ToolsCall(deleteMethod, deleteRequest);
                _context.PrintResponse(deleteResponse);

                using (var deleteDocument = JsonDocument.Parse(deleteResponse))
                {
                    JsonElement deleteRoot = deleteDocument.RootElement;

                    if (!TryExtractToolResult(deleteRoot, out JsonElement deleteResultElement, out string error))
                    {
                        _context.RecordFail(Module, deleteMethod, error);
                        return;
                    }

                    if (!deleteResultElement.TryGetProperty("type", out JsonElement typeElement)
                        || typeElement.ValueKind != JsonValueKind.String
                        || typeElement.GetString() != serverType)
                    {
                        _context.RecordFail(Module, deleteMethod, "response result is missing valid 'type'");
                        return;
                    }

                    if (!deleteResultElement.TryGetProperty("number", out JsonElement numberElement)
                        || numberElement.ValueKind != JsonValueKind.Number
                        || !numberElement.TryGetInt32(out int deletedNumber)
                        || deletedNumber != createdNumber)
                    {
                        _context.RecordFail(Module, deleteMethod, "response result is missing valid 'number' matching created instance");
                        return;
                    }

                    if (!deleteResultElement.TryGetProperty("deleted", out JsonElement deletedElement)
                        || deletedElement.ValueKind != JsonValueKind.True)
                    {
                        _context.RecordFail(Module, deleteMethod, "response result is missing 'deleted': true");
                        return;
                    }
                }

                _context.RecordPass(Module, deleteMethod, $"deleted {serverType} instance #{createdNumber}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, deleteMethod, error.Message);
            }
        }

        private void TestDeleteInstanceZeroFails()
        {
            const string method = "server_instance_delete";
            string serverType = _serverType;
            object request = new { type = serverType, number = 0 };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement root = document.RootElement;

                    if (root.TryGetProperty("IsError", out JsonElement isErrorElement)
                        && isErrorElement.ValueKind == JsonValueKind.True)
                    {
                        _context.RecordPass(Module, method, "deleting instance 0 correctly returned an error");
                        return;
                    }

                    if (TryExtractToolResult(root, out JsonElement resultElement, out _)
                        && resultElement.TryGetProperty("deleted", out JsonElement deletedElement)
                        && deletedElement.ValueKind == JsonValueKind.True)
                    {
                        _context.RecordFail(Module, method, "instance 0 was deleted but should be protected");
                        return;
                    }
                }

                _context.RecordFail(Module, method, "expected an error when deleting instance 0, but none was returned");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordPass(Module, method, $"deleting instance 0 correctly returned an error: {error.Message}");
            }
        }

        private void TestGetDataAfterConnect()
        {
            const string method = "server_instance_get_data_after_connect";
            const string createMethod = "server_instance_create";
            const string deleteMethod = "server_instance_delete";
            const string setParamsMethod = "server_instance_set_params";
            const string connectMethod = "server_instance_connect";
            const string disconnectMethod = "server_instance_disconnect";
            const string getStatusMethod = "server_instance_get_status";
            const string getSecuritiesMethod = "server_instance_get_securities";
            const string getPortfoliosMethod = "server_instance_get_portfolios";
            const string getLogMethod = "server_instance_get_log";

            string serverType = _context.Secrets.ConnectorType;

            if (string.IsNullOrWhiteSpace(serverType))
            {
                _context.RecordFail(Module, method, "no connector type configured in test secrets");
                return;
            }

            HttpResponseMessage? sseResponse = null;
            Stream? sseStream = null;
            StreamReader? sseReader = null;
            CancellationTokenSource? sseCts = null;
            Task? sseTask = null;
            var receivedEvents = new ConcurrentQueue<string>();
            int createdNumber = -1;

            try
            {
                _context.PrintRequest(Module, method + " (SSE subscribe)", new { });
                sseResponse = _context.Client.GetSseResponse();
                sseStream = sseResponse.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                sseReader = new StreamReader(sseStream, Encoding.UTF8);
                sseCts = new CancellationTokenSource();

                sseTask = Task.Run(() =>
                {
                    string eventName = string.Empty;
                    string data = string.Empty;

                    while (!sseCts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            string? line = sseReader.ReadLine();

                            if (line == null)
                            {
                                Thread.Sleep(50);
                                continue;
                            }

                            if (line.StartsWith("event: "))
                            {
                                eventName = line.Substring("event: ".Length).Trim();
                            }
                            else if (line.StartsWith("data: "))
                            {
                                data = line.Substring("data: ".Length).Trim();
                            }
                            else if (string.IsNullOrEmpty(line))
                            {
                                if (!string.IsNullOrEmpty(eventName) && !string.IsNullOrEmpty(data))
                                {
                                    receivedEvents.Enqueue(data);
                                }

                                eventName = string.Empty;
                                data = string.Empty;
                            }
                        }
                        catch
                        {
                            break;
                        }
                    }
                }, sseCts.Token);

                object createRequest = new { type = serverType };
                _context.PrintRequest(Module, createMethod, createRequest);
                string createResponse = _context.Client.ToolsCall(createMethod, createRequest);
                _context.PrintResponse(createResponse);

                using (JsonDocument createDocument = JsonDocument.Parse(createResponse))
                {
                    if (!TryExtractToolResult(createDocument.RootElement, out JsonElement createResult, out string error))
                    {
                        _context.RecordFail(Module, method, error);
                        return;
                    }

                    if (!createResult.TryGetProperty("number", out JsonElement numberElement)
                        || numberElement.ValueKind != JsonValueKind.Number
                        || !numberElement.TryGetInt32(out createdNumber)
                        || createdNumber < 1)
                    {
                        _context.RecordFail(Module, method, "failed to create a valid instance");
                        return;
                    }
                }

                if (_context.Secrets.Parameters.Count > 0)
                {
                    var parametersToSet = _context.Secrets.Parameters
                        .Select(p => new { name = p.Key, value = (object)p.Value })
                        .ToArray();

                    object setRequest = new
                    {
                        type = serverType,
                        number = createdNumber,
                        parameters = parametersToSet
                    };

                    _context.PrintRequest(Module, setParamsMethod, setRequest);
                    string setResponse = _context.Client.ToolsCall(setParamsMethod, setRequest);
                    _context.PrintResponse(setResponse);
                }

                object connectRequest = new { type = serverType, number = createdNumber };
                _context.PrintRequest(Module, connectMethod, connectRequest);
                string connectResponse = _context.Client.ToolsCall(connectMethod, connectRequest);
                _context.PrintResponse(connectResponse);

                if (!WaitForServerStatus(receivedEvents, "Connect", TimeSpan.FromSeconds(30)))
                {
                    _context.RecordFail(Module, method, "server_instance.status_changed with status Connect not received within 30 seconds");
                    return;
                }

                if (!WaitForEvent(receivedEvents, "server_instance.security.updated", TimeSpan.FromSeconds(30)))
                {
                    _context.RecordFail(Module, method, "server_instance.security.updated not received within 30 seconds");
                    return;
                }

                if (!WaitForEvent(receivedEvents, "server_instance.portfolio.updated", TimeSpan.FromSeconds(30)))
                {
                    _context.RecordFail(Module, method, "server_instance.portfolio.updated not received within 30 seconds");
                    return;
                }

                object getStatusRequest = new { type = serverType, number = createdNumber };
                _context.PrintRequest(Module, getStatusMethod, getStatusRequest);
                string getStatusResponse = _context.Client.ToolsCall(getStatusMethod, getStatusRequest);
                _context.PrintResponse(getStatusResponse);

                using (JsonDocument statusDocument = JsonDocument.Parse(getStatusResponse))
                {
                    if (!TryExtractToolResult(statusDocument.RootElement, out JsonElement statusResult, out string statusError))
                    {
                        _context.RecordFail(Module, method, statusError);
                        return;
                    }

                    if (!statusResult.TryGetProperty("status", out JsonElement statusElement)
                        || statusElement.ValueKind != JsonValueKind.String
                        || statusElement.GetString() != "Connect")
                    {
                        _context.RecordFail(Module, method, "get_status did not return Connect");
                        return;
                    }
                }

                object getSecuritiesRequest = new { type = serverType, number = createdNumber };
                _context.PrintRequest(Module, getSecuritiesMethod, getSecuritiesRequest);
                string getSecuritiesResponse = _context.Client.ToolsCall(getSecuritiesMethod, getSecuritiesRequest);
                _context.PrintResponse(getSecuritiesResponse);

                using (JsonDocument securitiesDocument = JsonDocument.Parse(getSecuritiesResponse))
                {
                    if (!TryExtractToolResult(securitiesDocument.RootElement, out JsonElement securitiesResult, out string securitiesError))
                    {
                        _context.RecordFail(Module, method, securitiesError);
                        return;
                    }

                    if (!securitiesResult.TryGetProperty("count", out JsonElement securitiesCountElement)
                        || securitiesCountElement.ValueKind != JsonValueKind.Number
                        || !securitiesCountElement.TryGetInt32(out int securitiesCount)
                        || securitiesCount < 1)
                    {
                        _context.RecordFail(Module, method, "get_securities did not return any securities");
                        return;
                    }
                }

                object getPortfoliosRequest = new { type = serverType, number = createdNumber };
                _context.PrintRequest(Module, getPortfoliosMethod, getPortfoliosRequest);
                string getPortfoliosResponse = _context.Client.ToolsCall(getPortfoliosMethod, getPortfoliosRequest);
                _context.PrintResponse(getPortfoliosResponse);

                using (JsonDocument portfoliosDocument = JsonDocument.Parse(getPortfoliosResponse))
                {
                    if (!TryExtractToolResult(portfoliosDocument.RootElement, out JsonElement portfoliosResult, out string portfoliosError))
                    {
                        _context.RecordFail(Module, method, portfoliosError);
                        return;
                    }

                    if (!portfoliosResult.TryGetProperty("count", out JsonElement portfoliosCountElement)
                        || portfoliosCountElement.ValueKind != JsonValueKind.Number
                        || !portfoliosCountElement.TryGetInt32(out int portfoliosCount)
                        || portfoliosCount < 0)
                    {
                        _context.RecordFail(Module, method, "get_portfolios did not return valid count");
                        return;
                    }
                }

                object getLogRequest = new { type = serverType, number = createdNumber, count = 50 };
                _context.PrintRequest(Module, getLogMethod, getLogRequest);
                string getLogResponse = _context.Client.ToolsCall(getLogMethod, getLogRequest);
                _context.PrintResponse(getLogResponse);

                using (JsonDocument logDocument = JsonDocument.Parse(getLogResponse))
                {
                    if (!TryExtractToolResult(logDocument.RootElement, out JsonElement logResult, out string logError))
                    {
                        _context.RecordFail(Module, method, logError);
                        return;
                    }

                    if (!logResult.TryGetProperty("messages", out JsonElement messagesElement)
                        || messagesElement.ValueKind != JsonValueKind.Array)
                    {
                        _context.RecordFail(Module, method, "get_log did not return messages array");
                        return;
                    }
                }

                object disconnectRequest = new { type = serverType, number = createdNumber };
                _context.PrintRequest(Module, disconnectMethod, disconnectRequest);
                string disconnectResponse = _context.Client.ToolsCall(disconnectMethod, disconnectRequest);
                _context.PrintResponse(disconnectResponse);

                _context.RecordPass(Module, method, $"got securities, portfolios, status and log from connected {serverType} instance #{createdNumber}");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
            finally
            {
                try
                {
                    sseCts?.Cancel();
                    sseReader?.Dispose();
                    sseStream?.Dispose();
                    sseResponse?.Dispose();
                }
                catch { }

                try
                {
                    if (createdNumber >= 1)
                    {
                        object deleteRequest = new { type = serverType, number = createdNumber };
                        _context.Client.ToolsCall(deleteMethod, deleteRequest);
                    }
                }
                catch { }
            }
        }

        private static bool WaitForEvent(ConcurrentQueue<string> events, string expectedEventName, TimeSpan timeout)
        {
            DateTime deadline = DateTime.Now.Add(timeout);

            while (DateTime.Now < deadline)
            {
                while (events.TryDequeue(out string? data))
                {
                    if (string.IsNullOrEmpty(data))
                    {
                        continue;
                    }

                    try
                    {
                        using (JsonDocument document = JsonDocument.Parse(data))
                        {
                            JsonElement root = document.RootElement;

                            if (root.TryGetProperty("event", out JsonElement eventElement)
                                && eventElement.ValueKind == JsonValueKind.String
                                && eventElement.GetString() == expectedEventName)
                            {
                                return true;
                            }
                        }
                    }
                    catch { }
                }

                Thread.Sleep(100);
            }

            return false;
        }

        private static bool WaitForServerStatus(ConcurrentQueue<string> events, string expectedStatus, TimeSpan timeout)
        {
            DateTime deadline = DateTime.Now.Add(timeout);

            while (DateTime.Now < deadline)
            {
                while (events.TryDequeue(out string? data))
                {
                    if (string.IsNullOrEmpty(data))
                    {
                        continue;
                    }

                    try
                    {
                        using (JsonDocument document = JsonDocument.Parse(data))
                        {
                            JsonElement root = document.RootElement;

                            if (root.TryGetProperty("payload", out JsonElement payload)
                                && payload.TryGetProperty("status", out JsonElement statusElement)
                                && statusElement.ValueKind == JsonValueKind.String
                                && statusElement.GetString() == expectedStatus)
                            {
                                return true;
                            }
                        }
                    }
                    catch { }
                }

                Thread.Sleep(100);
            }

            return false;
        }

        private static bool TryExtractToolResult(JsonElement root, out JsonElement resultElement, out string error)
        {
            error = string.Empty;
            resultElement = default;

            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "response is not an object";
                return false;
            }

            if (!root.TryGetProperty("Content", out JsonElement contentElement)
                || contentElement.ValueKind != JsonValueKind.Array
                || contentElement.GetArrayLength() == 0)
            {
                error = "response Content is missing or empty";
                return false;
            }

            JsonElement textElement = contentElement[0];

            if (!textElement.TryGetProperty("Text", out JsonElement textProperty)
                || textProperty.ValueKind != JsonValueKind.String)
            {
                error = "response Content[0].Text is missing or not a string";
                return false;
            }

            string resultJson = textProperty.GetString() ?? string.Empty;

            try
            {
                using (var resultDocument = JsonDocument.Parse(resultJson))
                {
                    resultElement = resultDocument.RootElement.Clone();
                }
            }
            catch (Exception parseError)
            {
                error = $"failed to parse Content[0].Text as JSON: {parseError.Message}";
                return false;
            }

            return true;
        }
    }
}
