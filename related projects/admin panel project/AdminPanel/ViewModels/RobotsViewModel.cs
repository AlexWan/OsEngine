using AdminPanel.Entity;
using AdminPanel.Language;
using AdminPanel.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace AdminPanel.ViewModels
{
    public class RobotsViewModel : NotificationObject, ILocalization
    {
        public PositionsViewModel PositionsVm;
        public RobotsViewModel(PositionsViewModel positionsVm)
        {
            PositionsVm = positionsVm;
        }
        private ObservableCollection<Robot> _robots = new ObservableCollection<Robot>();

        public ObservableCollection<Robot> Robots
        {
            get { return _robots; }
            set { SetProperty(ref _robots, value, () => Robots); }
        }

        public void UpdateBotList(JArray jArray)
        {
            string[] bots = new string[jArray.Count];

            for (int i = 0; i < jArray.Count; i++)
            {
                var name = jArray[i]["NameStrategyUniq"].Value<string>();

                var needBot = Robots.FirstOrDefault(p => p.Name == name);

                if (needBot == null)
                {
                    needBot = new Robot(name, this);

                    AddElement(needBot, Robots);
                }

                bots[i] = name;
            }

            for (int i = 0; i < Robots.Count; i++)
            {
                if (!bots.Contains(Robots[i].Name))
                {
                    Robots[i].DeleteSettingsFile();
                    RemoveElement(Robots[i], Robots);
                    i--;
                }
            }
        }

        public void UpdateTable(JToken jt)
        {
            var name = jt["BotName"].Value<string>();

            var needBot = Robots.FirstOrDefault(r => r.Name == name);
            if (needBot == null)
            {
                needBot = new Robot(name, this);
                AddElement(needBot, Robots);
            }

            needBot.CurrentPositions = jt["PositionsCount"].Value<int>();

            needBot.CurrentLots = jt["NettoCount"].Value<int>();
            var candleTime = DateTime.Parse(jt["LastTimeUpdate"].Value<string>(), CultureInfo.InvariantCulture);
            var eventTime = DateTime.Parse(jt["EventSendTime"].Value<string>(), CultureInfo.InvariantCulture);

            needBot.SetCandleUpdateTime(candleTime, eventTime);
            needBot.CheckOtherStates();
            needBot.CheckBadPositions();
            needBot.CheckBotState();
        }

        public void UpdateBotLog(JToken jt)
        {
            var name = jt["BotName"].Value<string>();

            var needBot = Robots.FirstOrDefault(r => r.Name == name);
            if (needBot == null)
            {
                needBot = new Robot(name, this);
                AddElement(needBot, Robots);
            }

            Clear(needBot.Log);
            var logList = jt["Log"].Value<JArray>();

            foreach (var log in logList)
            {
                var logTime = log.Value<string>("Time");
                var logMessage = log.Value<string>("Message");
                var logMessageType = log.Value<string>("Type");

                var newLog = new LogMessage();
                newLog.Time = DateTime.Parse(logTime, CultureInfo.InvariantCulture);
                newLog.Type = Enum.Parse<LogMessageType>(logMessageType);
                newLog.Message = logMessage;

                AddElement(newLog, needBot.Log);
            }
        }

        public void UpdateBotParams(JToken jt)
        {
            var name = jt["BotName"].Value<string>();

            var needBot = Robots.FirstOrDefault(r => r.Name == name);
            if (needBot == null)
            {
                needBot = new Robot(name, this);
                AddElement(needBot, Robots);
            }

            var paramsList = jt["Params"].Value<JArray>();

            foreach (var parameter in paramsList)
            {
                var paramName = parameter.Value<string>("Name");
                var value = parameter.Value<string>("Value");

                var needParameter = needBot.Params.Keys.FirstOrDefault(k => k == paramName);
                if (needParameter == null)
                {
                    AddDictionaryElement(paramName, value, needBot.Params);
                }
                else if (needBot.Params[needParameter] != value)
                {
                    needBot.Params[needParameter] = value;
                }
            }
        }

        #region Local

        private string _nameHeader;
        public string NameHeader
        {
            get { return _nameHeader; }
            set
            {
                SetProperty(ref _nameHeader, value, () => NameHeader);
            }
        }

        private string _systemHeader;
        public string SystemHeader
        {
            get { return _systemHeader; }
            set
            {
                SetProperty(ref _systemHeader, value, () => SystemHeader);
            }
        }

        private string _statusHeader;
        public string StatusHeader
        {
            get { return _statusHeader; }
            set
            {
                SetProperty(ref _statusHeader, value, () => StatusHeader);
            }
        }

        private string _commentHeader;
        public string CommentHeader
        {
            get { return _commentHeader; }
            set
            {
                SetProperty(ref _commentHeader, value, () => CommentHeader);
            }
        }

        public void ChangeLocal()
        {
            NameHeader = OsLocalization.Entity.RobotNameHeader;
            SystemHeader = OsLocalization.Entity.SystemHeader;
            StatusHeader = OsLocalization.Entity.StatusHeader;
            CommentHeader = OsLocalization.Entity.CommentHeader;
        }

        #endregion
    }
}
