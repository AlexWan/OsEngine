/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.OsTrader.Grids;
using System;
using System.Collections.Generic;
using System.IO;


namespace OsEngine.OsTrader.ClientManagement
{
    public class TradeClient
    {
        public TradeClient()
        {


        }

        public int Number;

        public string Name;

        public string Status
        {
            get
            {
                return "Ok";
            }
        }

        public void Save()
        {
            try
            {
                string dir = Directory.GetCurrentDirectory();
                dir += "\\Engine\\ClientManagement\\";

                if (Directory.Exists(dir) == false)
                {
                    Directory.CreateDirectory(dir);
                }

                using (StreamWriter writer = new StreamWriter(@"Engine\ClientManagement\" + Number + @"TradeClient.txt", false))
                {
                    writer.WriteLine(Number);
                    writer.WriteLine(Name);
                    writer.WriteLine("");
                    writer.WriteLine("");
                    writer.WriteLine("");
                    writer.WriteLine("");
                    writer.WriteLine("");





                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }

        }

        public void LoadFromFile(string fileAddress)
        {
            if (!File.Exists(fileAddress))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(fileAddress))
                {
                    Number = Convert.ToInt32(reader.ReadLine());
                    Name = reader.ReadLine();

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }

        }

        public void Delete()
        {
            try
            {
                if (File.Exists(@"Engine\ClientManagement\" + Number + @"TradeClient.txt") == true)
                {
                    File.Delete(@"Engine\ClientManagement\" + Number + @"TradeClient.txt");
                }
            }
            catch
            {
                // ignore
            }
        }

        public List<TradeClientConnector> ClientConnectorsSettings = new List<TradeClientConnector>();

        public List<TradeClientRobots> ClientRobotsSettings = new List<TradeClientRobots>();



    }

    public class TradeClientRobots
    {
        public int Number;

        public bool IsOn;

        public string GetSaveString()
        {
            return "";
        }

        public List<TradeClientRobotsParameter> Parameters;

        public List<TradeClientSourceSettings> SourceSettings;




    }

    public class TradeClientRobotsParameter
    {
        public string GetSaveString()
        {
            return "";
        }


    }

    public class TradeClientSourceSettings
    {




    }

    public class TradeClientConnector
    {


    }
}
