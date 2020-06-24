﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using Microsoft.CSharp;
using OsEngine.Entity;

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
            if (Directory.Exists(@"Custom\Indicators\Scripts") == false)
            {
                Directory.CreateDirectory(@"Custom\Indicators\Scripts");
            }

            List<string> resultOne = GetFullNamesFromFolder(@"Custom\Indicators\Scripts");

            for (int i = 0; i < resultOne.Count; i++)
            {
                resultOne[i] = resultOne[i].Split('\\')[resultOne[i].Split('\\').Length - 1];
                resultOne[i] = resultOne[i].Split('.')[0];
            }

            //resultOne.Add("Template");

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

        public static List<string> GetFullNamesFromFolder(string directory)
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

           /* if (nameClass == "FBD")
            {
                Indicator = new FBD();
            }*/

            try
            {
                if (Indicator == null)
                {
                    List<string> fullPaths = GetFullNamesFromFolder(@"Custom\Indicators\Scripts");

                    string longNameClass = nameClass + ".txt";
                    string longNameClass2 = nameClass + ".cs";

                    string myPath = "";

                    for (int i = 0; i < fullPaths.Count; i++)
                    {
                        string nameInFile = fullPaths[i].Split('\\')[fullPaths[i].Split('\\').Length - 1];

                        if (nameInFile == longNameClass ||
                            nameInFile == longNameClass2)
                        {
                            myPath = fullPaths[i];
                            break;
                        }
                    }

                    if (myPath == "")
                    {
                        MessageBox.Show("Error! Indicator with name " + nameClass + "not found");
                        return null;
                    }

                    Indicator = Serialize(myPath, nameClass, name, canDelete);
                }

                Indicator.Init(name);
                Indicator.CanDelete = canDelete;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }

            return Indicator;
        }

        private static Aindicator Serialize(string path, string nameClass, string name, bool canDelete)
        {
            try
            {
                Aindicator result = null;

                string fileStr = ReadFile(path);

                CSharpCodeProvider prov = new CSharpCodeProvider();

                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                var res = Array.ConvertAll<Assembly, string>(assemblies, (x) =>
                {
                    if (!x.IsDynamic)
                    {
                        return x.Location;
                    }

                    return null;
                });

                CompilerParameters cp = new CompilerParameters(res);
                cp.IncludeDebugInformation = true;
                cp.GenerateInMemory = true;
                cp.TempFiles.KeepFiles = false;
                

               /* string folderCur = AppDomain.CurrentDomain.BaseDirectory + "Engine\\Temp";

                if (Directory.Exists(folderCur) == false)
                {
                    Directory.CreateDirectory(folderCur);
                }

                cp.OutputAssembly = folderCur + "\\tempInd" + nameClass +
                                    NumberGen.GetNumberDeal(StartProgram.IsOsTrader);*/

                CompilerResults results = prov.CompileAssemblyFromSource(cp, fileStr);

                if (results.Errors != null && results.Errors.Count != 0)
                {
                    string errorString = "Error! Indicator script runTime compilation problem! \n";
                    errorString += "Path to indicator: " + path + " \n";

                    int errorNum = 1;

                    foreach (var error in results.Errors)
                    {
                        errorString += "Error Number: " + errorNum + " \n";
                        errorString += error.ToString() + "\n";
                        errorNum++;
                    }

                    throw new Exception(errorString);
                }

                result = (Aindicator)results.CompiledAssembly.CreateInstance(results.CompiledAssembly.DefinedTypes.ElementAt(0).FullName);

                cp.TempFiles.Delete();

                return result;
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
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
