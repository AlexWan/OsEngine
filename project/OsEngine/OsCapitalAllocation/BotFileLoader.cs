/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.IO;
using OsEngine.Entity;

namespace OsEngine.OsCapitalAllocation
{
    /// <summary>
    /// Загрузка файлов ботов с позициями (формат Position.GetStringForSave()).
    /// </summary>
    public static class BotFileLoader
    {
        /// <summary>
        /// Загрузить один файл. Возвращает null, если файл не найден.
        /// </summary>
        public static BotFile Load(string filePath)
        {
            if (File.Exists(filePath) == false)
            {
                return null;
            }

            BotFile botFile = new BotFile();

            botFile.BotName = Path.GetFileName(filePath).Split('_')[0];
            botFile.Path = filePath;

            string[] lines = File.ReadAllLines(filePath);

            botFile.Positions = new List<Position>(lines.Length);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                if (line.Split('#').Length < 25)
                {
                    continue;
                }

                Position position = new Position();

                try
                {
                    position.SetDealFromString(line);
                }
                catch (Exception)
                {
                    continue;
                }

                botFile.Positions.Add(position);
            }

            return botFile;
        }

        /// <summary>
        /// Загрузить все .txt файлы из папки.
        /// </summary>
        public static List<BotFile> LoadFolder(string folderPath)
        {
            List<BotFile> result = new List<BotFile>();

            if (Directory.Exists(folderPath) == false)
            {
                return result;
            }

            string[] files = Directory.GetFiles(folderPath, "*.txt");

            for (int i = 0; i < files.Length; i++)
            {
                BotFile botFile = Load(files[i]);

                if (botFile != null && botFile.Positions.Count > 0)
                {
                    result.Add(botFile);
                }
            }

            return result;
        }
    }
}
