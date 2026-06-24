/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple.pdf
*/

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using WikiConnectionTest.Models;

namespace WikiConnectionTest.Services
{
    /// <summary>
    /// Saves collected securities to Wiki .md files with JSON Lines.
    /// </summary>
    public class WikiFileService
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public string EnsureWikiFolder(string osEnginePath)
        {
            string? directory = Path.GetDirectoryName(osEnginePath);

            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("Cannot determine OsEngine directory");
            }

            string wikiFolder = Path.Combine(directory, "Wiki");

            if (!Directory.Exists(wikiFolder))
            {
                Directory.CreateDirectory(wikiFolder);
                Console.WriteLine($"[FileService] Created Wiki folder: {wikiFolder}");
            }

            return wikiFolder;
        }

        public void SaveSecurities(string filePath, ConnectorMetadata metadata, IEnumerable<WikiSecurity> securities)
        {
            string? directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var builder = new StringBuilder();
            builder.AppendLine($"# {metadata.Connector} Securities");
            builder.AppendLine();
            builder.AppendLine("## Metadata");
            builder.AppendLine();
            builder.AppendLine("```json");
            builder.AppendLine(JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            builder.AppendLine("```");
            builder.AppendLine();
            builder.AppendLine("## Securities");
            builder.AppendLine();
            builder.AppendLine("```jsonl");

            foreach (WikiSecurity security in securities)
            {
                builder.AppendLine(JsonSerializer.Serialize(security, JsonOptions));
            }

            builder.AppendLine("```");

            File.WriteAllText(filePath, builder.ToString(), Encoding.UTF8);
            Console.WriteLine($"[FileService] Saved {filePath}");
        }
    }
}
