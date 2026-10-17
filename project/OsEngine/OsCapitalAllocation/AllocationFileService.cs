/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsCapitalAllocation.Algorithms.HierarchicalRiskParity;
using OsEngine.OsTrader.Panels;

namespace OsEngine.OsCapitalAllocation
{
    /// <summary>
    /// Файл аллокации: сохранение результата (формат 100%..600%), чтение и применение
    /// объёмов к запущенным ботам. Общий для UI и MCP.
    /// </summary>
    public class AllocationFileService
    {
        public static void Save(string filePath, List<List<int>> clusters, string[] botNames, decimal[] weights)
        {
            File.WriteAllText(filePath, BuildContent(clusters, botNames, weights));
        }

        public static string BuildContent(List<List<int>> clusters, string[] botNames, decimal[] weights)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("Num cluster;Num in cluster;Name robot;100%;200%;300%;400%;500%;600%;Journal;");
            sb.Append("\r\n");

            for (int c = 0; c < clusters.Count; c++)
            {
                List<int> cluster = clusters[c];

                sb.Append(c + 1);
                sb.Append(new string(';', 10));
                sb.Append("\r\n");

                for (int j = 0; j < cluster.Count; j++)
                {
                    int botIndex = cluster[j];
                    decimal weight = weights[botIndex];

                    sb.Append(';');
                    sb.Append(j + 1);
                    sb.Append(';');
                    sb.Append(botNames[botIndex]);
                    sb.Append(';');

                    for (int m = 1; m <= 6; m++)
                    {
                        sb.Append(FormatWeight(weight * (100 * m)));
                        sb.Append(';');
                    }

                    sb.Append(';');
                    sb.Append("\r\n");
                }
            }

            return sb.ToString();
        }

        public static List<ClusterInFile> Load(string filePath)
        {
            List<string> stringsInFile = new List<string>();

            using (StreamReader sr = new StreamReader(filePath))
            {
                while (sr.EndOfStream == false)
                {
                    stringsInFile.Add(sr.ReadLine());
                }
            }

            List<ClusterInFile> botInFiles = new List<ClusterInFile>();

            for (int i = 1; i < stringsInFile.Count; i++)
            {
                string currentString = stringsInFile[i];

                if (currentString.EndsWith(";;;;;;;;;"))
                {
                    ClusterInFile newCluster = new ClusterInFile();
                    newCluster.ClusterNum = Convert.ToInt32(currentString.Split(';')[0]);
                    botInFiles.Add(newCluster);

                    for (int j = i + 1; ; j++)
                    {
                        if (j >= stringsInFile.Count)
                        {
                            break;
                        }

                        string thisString = stringsInFile[j];

                        if (thisString.EndsWith(";;;;;;;;;"))
                        {
                            i = j - 1;
                            break;
                        }

                        string[] array = thisString.Split(';');

                        BotInFile newBot = new BotInFile();
                        newBot.Name = array[2];
                        newBot.Shoulder1Value = array[3].ToDecimal();
                        newBot.Shoulder2Value = array[4].ToDecimal();
                        newBot.Shoulder3Value = array[5].ToDecimal();
                        newBot.Shoulder4Value = array[6].ToDecimal();
                        newBot.Shoulder5Value = array[7].ToDecimal();
                        newBot.Shoulder6Value = array[8].ToDecimal();

                        newCluster.Bots.Add(newBot);
                    }
                }
            }

            return botInFiles;
        }

        public static AllocationApplyResult Compute(List<ClusterInFile> botInFiles, string shoulder, List<BotPanel> robots)
        {
            AllocationApplyResult result = new AllocationApplyResult();

            for (int i = 0; i < robots.Count; i++)
            {
                BotPanel robot = robots[i];

                StrategyParameterInt maxPosesParam = null;
                StrategyParameterDecimal volumeParam = null;

                for (int j = 0; j < robot.Parameters.Count; j++)
                {
                    if (robot.Parameters[j].Name.Contains("Max positions")
                        || robot.Parameters[j].Name.Contains("Positions count"))
                    {
                        maxPosesParam = (StrategyParameterInt)robot.Parameters[j];
                    }

                    if (robot.Parameters[j].Name == "Volume")
                    {
                        volumeParam = (StrategyParameterDecimal)robot.Parameters[j];
                    }
                }

                decimal volumeDevider = 1;

                if (maxPosesParam != null)
                {
                    volumeDevider = maxPosesParam.ValueInt;
                }

                if (volumeParam == null)
                {
                    result.Skipped.Add(new SkippedBotInfo
                    {
                        BotName = robot.NameStrategyUniq,
                        Reason = OsLocalization.CapitalAllocation.ReasonNoVolume
                    });
                    continue;
                }

                BotInFile myBotInCluster = null;

                for (int j = 0; j < botInFiles.Count; j++)
                {
                    BotInFile found = botInFiles[j].Bots.Find(b => b.Name == robot.NameStrategyUniq.Split('_')[0]);

                    if (found != null)
                    {
                        myBotInCluster = found;
                        break;
                    }
                }

                if (myBotInCluster == null)
                {
                    result.Skipped.Add(new SkippedBotInfo
                    {
                        BotName = robot.NameStrategyUniq,
                        Reason = OsLocalization.CapitalAllocation.ReasonNotFound
                    });
                    continue;
                }

                decimal volumeBase = 0;

                if (shoulder == "1")
                {
                    volumeBase = myBotInCluster.Shoulder1Value;
                }
                else if (shoulder == "2")
                {
                    volumeBase = myBotInCluster.Shoulder2Value;
                }
                else if (shoulder == "3")
                {
                    volumeBase = myBotInCluster.Shoulder3Value;
                }
                else if (shoulder == "4")
                {
                    volumeBase = myBotInCluster.Shoulder4Value;
                }
                else if (shoulder == "5")
                {
                    volumeBase = myBotInCluster.Shoulder5Value;
                }
                else if (shoulder == "6")
                {
                    volumeBase = myBotInCluster.Shoulder6Value;
                }

                decimal currentVolumeOnPosition = Math.Round(volumeBase / volumeDevider, 3);

                result.Applied.Add(new BotAllocation
                {
                    Bot = robot,
                    VolumeParam = volumeParam,
                    Volume = currentVolumeOnPosition
                });
            }

            return result;
        }

        public static void Apply(AllocationApplyResult result)
        {
            if (result == null || result.Applied == null)
            {
                return;
            }

            for (int i = 0; i < result.Applied.Count; i++)
            {
                result.Applied[i].VolumeParam.ValueDecimal = result.Applied[i].Volume;
            }
        }

        public static List<List<int>> ComputeHrpClusters(HierarchicalRiskParityAlgorithm hrp, int botsCount, double minCorrelation)
        {
            List<DendrogramNode> merges = hrp.Merges;
            List<int> sorted = hrp.SortedIndices;

            if (merges.Count == 0 || sorted.Count == 0)
            {
                return null;
            }

            double threshold = Math.Sqrt(0.5 * (1 - minCorrelation));

            int[] labels = BuildClusterLabels(merges, botsCount, threshold);

            Dictionary<int, List<int>> groups = new Dictionary<int, List<int>>();

            for (int i = 0; i < labels.Length; i++)
            {
                if (groups.ContainsKey(labels[i]) == false)
                {
                    groups[labels[i]] = new List<int>();
                }

                groups[labels[i]].Add(i);
            }

            List<List<int>> clusters = new List<List<int>>();
            HashSet<int> used = new HashSet<int>();

            for (int i = 0; i < sorted.Count; i++)
            {
                int label = labels[sorted[i]];

                if (used.Contains(label) == false)
                {
                    used.Add(label);
                    clusters.Add(groups[label]);
                }
            }

            return clusters;
        }

        private static int[] BuildClusterLabels(List<DendrogramNode> merges, int botsCount, double threshold)
        {
            int[] parent = new int[botsCount];

            for (int i = 0; i < botsCount; i++)
            {
                parent[i] = i;
            }

            int[] representative = new int[botsCount + merges.Count];

            for (int i = 0; i < botsCount; i++)
            {
                representative[i] = i;
            }

            for (int k = 0; k < merges.Count; k++)
            {
                DendrogramNode node = merges[k];
                int id = botsCount + k;
                int leftRep = representative[node.Left];
                int rightRep = representative[node.Right];

                representative[id] = leftRep;

                if (node.Distance < threshold)
                {
                    UnionCluster(parent, leftRep, rightRep);
                }
            }

            Dictionary<int, int> clusterIds = new Dictionary<int, int>();
            int[] clusterOfBot = new int[botsCount];

            for (int i = 0; i < botsCount; i++)
            {
                int root = FindCluster(parent, i);

                if (clusterIds.ContainsKey(root) == false)
                {
                    clusterIds[root] = clusterIds.Count;
                }

                clusterOfBot[i] = clusterIds[root];
            }

            return clusterOfBot;
        }

        private static int FindCluster(int[] parent, int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }

            return x;
        }

        private static void UnionCluster(int[] parent, int a, int b)
        {
            int rootA = FindCluster(parent, a);
            int rootB = FindCluster(parent, b);

            if (rootA != rootB)
            {
                parent[rootB] = rootA;
            }
        }

        private static string FormatWeight(decimal value)
        {
            return Math.Round(value, 2).ToString().Replace(",", ".");
        }
    }

    public class ClusterInFile
    {
        public int ClusterNum;

        public List<BotInFile> Bots = new List<BotInFile>();
    }

    public class BotInFile
    {
        public string Name;

        public decimal Shoulder1Value;

        public decimal Shoulder2Value;

        public decimal Shoulder3Value;

        public decimal Shoulder4Value;

        public decimal Shoulder5Value;

        public decimal Shoulder6Value;
    }

    public class BotAllocation
    {
        public BotPanel Bot;

        public StrategyParameterDecimal VolumeParam;

        public decimal Volume;
    }

    public class SkippedBotInfo
    {
        public string BotName;

        public string Reason;
    }

    public class AllocationApplyResult
    {
        public List<BotAllocation> Applied = new List<BotAllocation>();

        public List<SkippedBotInfo> Skipped = new List<SkippedBotInfo>();
    }
}
