/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OsEngine.McpApi.TestStand
{
    /// <summary>
    /// Stores connector credentials for the test stand.
    /// Loaded from environment variables, a local JSON file, or a console prompt.
    /// The local file must NOT be committed to the repository.
    /// </summary>
    public class TestSecrets
    {
        public string ConnectorType { get; set; } = string.Empty;

        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

        private const string FileName = "test-secrets.json";
        private const string TypeEnvVar = "OSENGINE_TEST_CONNECTOR_TYPE";
        private const string ParametersEnvVar = "OSENGINE_TEST_CONNECTOR_PARAMETERS";

        /// <summary>
        /// Load secrets using the following priority:
        /// 1. Environment variables.
        /// 2. Local test-secrets.json next to the executable.
        /// 3. Interactive console prompt (and save to test-secrets.json).
        /// </summary>
        public static TestSecrets Load(string baseDirectory)
        {
            string filePath = Path.Combine(baseDirectory, FileName);

            TestSecrets? fromEnv = TryLoadFromEnvironment();
            if (fromEnv != null)
            {
                Console.WriteLine($"[Secrets] Loaded from environment variables ({TypeEnvVar}, {ParametersEnvVar}).");
                return fromEnv;
            }

            if (File.Exists(filePath))
            {
                TestSecrets? fromFile = TryLoadFromFile(filePath);
                if (fromFile != null)
                {
                    Console.WriteLine($"[Secrets] Loaded from {filePath}");
                    return fromFile;
                }
            }

            Console.WriteLine("[Secrets] Test secrets not found.");
            Console.WriteLine($"[Secrets] Environment variables {TypeEnvVar} and {ParametersEnvVar} are not set, and {filePath} does not exist.");
            Console.WriteLine("[Secrets] Please enter connector credentials. They will be saved to a local file (ignored by git).");

            return PromptAndSave(filePath);
        }

        private static TestSecrets? TryLoadFromEnvironment()
        {
            string? type = Environment.GetEnvironmentVariable(TypeEnvVar);
            string? parametersJson = Environment.GetEnvironmentVariable(ParametersEnvVar);

            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(parametersJson))
            {
                return null;
            }

            try
            {
                Dictionary<string, string>? parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(parametersJson);
                if (parameters == null)
                {
                    return null;
                }

                return new TestSecrets
                {
                    ConnectorType = type,
                    Parameters = parameters
                };
            }
            catch
            {
                return null;
            }
        }

        private static TestSecrets? TryLoadFromFile(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);

                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("connector", out JsonElement connectorElement)
                        || connectorElement.ValueKind != JsonValueKind.Object)
                    {
                        return null;
                    }

                    string type = string.Empty;

                    if (connectorElement.TryGetProperty("type", out JsonElement typeElement)
                        && typeElement.ValueKind == JsonValueKind.String)
                    {
                        type = typeElement.GetString() ?? string.Empty;
                    }

                    var parameters = new Dictionary<string, string>();

                    if (connectorElement.TryGetProperty("parameters", out JsonElement parametersElement)
                        && parametersElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (JsonProperty property in parametersElement.EnumerateObject())
                        {
                            parameters[property.Name] = property.Value.ValueKind == JsonValueKind.String
                                ? property.Value.GetString() ?? string.Empty
                                : property.Value.ToString();
                        }
                    }

                    return new TestSecrets
                    {
                        ConnectorType = type,
                        Parameters = parameters
                    };
                }
            }
            catch
            {
                return null;
            }
        }

        private static TestSecrets PromptAndSave(string filePath)
        {
            Console.Write("Connector type (e.g. Binance): ");
            string type = Console.ReadLine()?.Trim() ?? string.Empty;

            var parameters = new Dictionary<string, string>();

            while (true)
            {
                Console.Write("Parameter name (empty to finish): ");
                string name = Console.ReadLine()?.Trim() ?? string.Empty;

                if (string.IsNullOrEmpty(name))
                {
                    break;
                }

                Console.Write($"Value for '{name}': ");
                string value = Console.ReadLine()?.Trim() ?? string.Empty;

                parameters[name] = value;
            }

            var secrets = new TestSecrets
            {
                ConnectorType = type,
                Parameters = parameters
            };

            Save(filePath, secrets);

            Console.WriteLine($"[Secrets] Saved to {filePath}");
            Console.WriteLine("[Secrets] This file is ignored by git. Do not commit it.");

            return secrets;
        }

        private static void Save(string filePath, TestSecrets secrets)
        {
            string? directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var data = new
            {
                connector = new
                {
                    type = secrets.ConnectorType,
                    parameters = secrets.Parameters
                }
            };

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(filePath, json);
        }
    }
}
