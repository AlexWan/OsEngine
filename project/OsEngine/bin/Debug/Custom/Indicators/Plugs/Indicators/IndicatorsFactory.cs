using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OsEngine.Indicators
{
    public class IndicatorsFactory
    {
        public static List<string> GetIndicatorsNames()
        {
            if (Directory.Exists(@"Custom") == false)
            {
                Directory.CreateDirectory(@"Custom");
            }

            if (Directory.Exists(@"Custom\Indicators") == false)
            {
                Directory.CreateDirectory(@"Custom\Indicators");
            }

            List<string> resultOne = GetFullNamesFromFolder(@"Custom\Indicators");

            for (int i = 0; i < resultOne.Count; i++)
            {
                resultOne[i] = resultOne[i].Split('\\')[resultOne[i].Split('\\').Length - 1];
                resultOne[i] = resultOne[i].Split('.')[0];
            }

            // resultOne.Add("Ssma");

            List<string> resultTrue = new List<string>();

            for (int i = 0; i < resultOne.Count; i++)
            {
                bool isInArray = false;

                for (int i2 = 0; i2 < resultTrue.Count; i2++)
                {
                    if (resultTrue[i2][0] > resultOne[i][0])
                    {
                        resultTrue.Insert(i2, resultOne[i]);
                        isInArray = true;
                        break;
                    }
                }

                if (isInArray == false)
                {
                    resultTrue.Add(resultOne[i]);
                }
            }

            return resultTrue;
        }

        private static List<string> GetFullNamesFromFolder(string directory)
        {
            List<string> results = new List<string>();

            string[] subDirectories = Directory.GetDirectories(directory);

            for (int i = 0; i < subDirectories.Length; i++)
            {
                results.AddRange(GetFullNamesFromFolder(subDirectories[i]));
            }

            string[] files = Directory.GetFiles(directory);

            results.AddRange(files.ToList());

            return results;
        }

        public static Aindicator CreateIndicatorByName(string nameClass, string name, bool canDelete)
        {
            Aindicator Indicator = null;

            /* if (nameClass == "Ssma")
             {
                 Indicator = new Ssma();
             }*/

            if (Indicator == null)
            {
                List<string> fullPaths = GetFullNamesFromFolder(@"Custom\Indicators");

                string longNameClass = nameClass + ".txt";
                string longNameClass2 = nameClass + ".cs";

                string myPath = "";

                for (int i = 0; i < fullPaths.Count; i++)
                {
                    if (fullPaths[i].EndsWith(longNameClass) ||
                        fullPaths[i].EndsWith(longNameClass2))
                    {
                        myPath = fullPaths[i];
                        break;
                    }
                }

                Indicator = Serialize(myPath, nameClass, name, canDelete);
            }

            Indicator.CanDelete = canDelete;
            Indicator.Init(name);

            return Indicator;
        }

        private static Aindicator Serialize(string path, string nameClass, string name, bool canDelete)
        {
            try
            {
                return null;
            }
            catch (Exception e)
            {
                string errorString = "Error! Indicator script runTime compilation problem! \n";
                errorString += "Path to indicator: " + path + " \n";
                errorString += e.ToString() + " \n";
                throw new Exception(errorString);
            }
        }

        private static string ReadFile(string path)
        {
            String result = "";

            using (StreamReader reader = new StreamReader(path))
            {
                result = reader.ReadToEnd();
                reader.Close();
            }

            return result;
        }
    }
}
