using AdminPanel.Utils;
using AdminPanel.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AdminPanel.Entity
{
    public class Robot : NotificationObject
    {
        private RobotsViewModel _model;
        public Robot(string name, RobotsViewModel model)
        {
            Name = name;
            _model = model;
            Load();
        }
        public string Name { get; set; }

        private int _maxPositions;
        public int MaxPositions
        {
            get { return _maxPositions; }
            set { SetProperty(ref _maxPositions, value, () => MaxPositions); }
        }

        private int _currentPositions;
        public int CurrentPositions
        {
            get { return _currentPositions; }
            set
            {
                SetProperty(ref _currentPositions, value, () => CurrentPositions);
                if (value > MaxPositions)
                {
                    MaxPositionsStatus = Status.Danger;
                }
                else
                {
                    MaxPositionsStatus = Status.Ok;
                }
            }

        }
        
        private int _maxLots;
        public int MaxLots
        {
            get { return _maxLots; }
            set
            { SetProperty(ref _maxLots, value, () => MaxLots); }

        }
        
        private int _currentLots;
        public int CurrentLots
        {
            get { return _currentLots; }
            set
            {
                SetProperty(ref _currentLots, value, () => CurrentLots);
                if (value > MaxLots)
                {
                    MaxLotStatus = Status.Danger;
                }
                else
                {
                    MaxLotStatus = Status.Ok;
                }
            }

        }

        private Status _status;
        public Status Status
        {
            get { return _status; }
            set { SetProperty(ref _status, value, () => Status); }
        }
        
        private Status _candleUpdateStatus;
        public Status CandleUpdateStatus
        {
            get { return _candleUpdateStatus; }
            set { SetProperty(ref _candleUpdateStatus, value, () => CandleUpdateStatus); }
        }

        private Status _logStatus;
        public Status LogStatus
        {
            get { return _logStatus; }
            set { SetProperty(ref _logStatus, value, () => LogStatus); }
        }

        private Status _positionsStatus;
        public Status PositionsStatus
        {
            get { return _positionsStatus; }
            set { SetProperty(ref _positionsStatus, value, () => PositionsStatus); }
        }

        private Status _maxPositionsStatus;
        public Status MaxPositionsStatus
        {
            get { return _maxPositionsStatus; }
            set { SetProperty(ref _maxPositionsStatus, value, () => MaxPositionsStatus); }
        }

        private Status _maxLotStatus;
        public Status MaxLotStatus
        {
            get { return _maxLotStatus; }
            set { SetProperty(ref _maxLotStatus, value, () => MaxLotStatus); }
        }

        private string _lastTimeCandleUpdate;
        public string LastTimeCandleUpdate
        {
            get { return _lastTimeCandleUpdate; }
            set { SetProperty(ref _lastTimeCandleUpdate, value, () => LastTimeCandleUpdate); }
        }

        public void SetCandleUpdateTime(DateTime candleTime, DateTime eventTime)
        {
            if (candleTime == DateTime.MinValue)
            {
                LastTimeCandleUpdate = "No data";
                CandleUpdateStatus = Status.Danger;
                return;
            }
            var diff = eventTime - candleTime;
            var minutes = diff.TotalMinutes;
            var seconds = diff.TotalSeconds - minutes * 60;
            LastTimeCandleUpdate = $"Last update: {Math.Round(minutes)} min. {Math.Round(seconds)} sec ago";

            if (minutes >= 1)
            {
                CandleUpdateStatus = Status.Danger;
            }
            else
            {
                CandleUpdateStatus = Status.Ok;
            }
        }

        public void CheckOtherStates()
        {
            var errors = Log.FindAll(l => l.Type == LogMessageType.Error);
            if (errors.Count != 0)
            {
                LogStatus = Status.Danger;
            }
            else
            {
                LogStatus = Status.Ok;
            }
        }

        private DateTime _lastTimeBadPosition = DateTime.MinValue;
        public void CheckBadPositions()
        {
            var badPositions = _model.PositionsVm.Positions.ToList().FindAll(p => p.Bot == Name &&
                (p.State == "Opening" || p.State == "ClosingFail" || p.State == "ClosingSurplus" || p.State == "Closing"));

            if (badPositions.Count != 0)
            {
                if (_lastTimeBadPosition == DateTime.MinValue)
                {
                    _lastTimeBadPosition = DateTime.Now;
                }
            }
            else
            {
                PositionsStatus = Status.Ok;
                PositionsComment = Comments.NoError;
                _lastTimeBadPosition = DateTime.MinValue;
            }

            if (_lastTimeBadPosition == DateTime.MinValue)
            {
                return;
            }

            if (_lastTimeBadPosition.AddMinutes(1) < DateTime.Now)
            {
                PositionsStatus = Status.Error;
                PositionsComment = Comments.BadPositions;
            }
            else
            {
                PositionsStatus = Status.Ok;
                PositionsComment = Comments.NoError;
            }
        }

        public void CheckBotState()
        {
            var sate = Status.Ok;

            if (CandleUpdateStatus == Status.Danger)
            {
                sate = Status.Danger;
            }
            if (LogStatus == Status.Danger)
            {
                sate = Status.Danger;
            }
            if (PositionsStatus == Status.Danger)
            {
                sate = Status.Danger;
            }
            if (MaxPositionsStatus == Status.Danger)
            {
                sate = Status.Danger;
            }
            if (MaxLotStatus == Status.Danger)
            {
                sate = Status.Danger;
            }

            if (CandleUpdateStatus == Status.Error)
            {
                sate = Status.Error;
            }
            if (LogStatus == Status.Error)
            {
                sate = Status.Error;
            }
            if (PositionsStatus == Status.Error)
            {
                sate = Status.Error;
            }
            if (MaxPositionsStatus == Status.Error)
            {
                sate = Status.Error;
            }
            if (MaxLotStatus == Status.Error)
            {
                sate = Status.Error;
            }

            Status = sate;
        }

        private string _logComment = Comments.NoError;
        public string LogComment
        {
            get { return _logComment; }
            set { SetProperty(ref _logComment, value, () => LogComment); }
        }

        private string _positionsComment = Comments.NoError;
        public string PositionsComment
        {
            get { return _positionsComment; }
            set { SetProperty(ref _positionsComment, value, () => PositionsComment); }
        }

        public Dictionary<string, string> Params { get; set; } = new Dictionary<string, string>();
       
        public List<LogMessage> Log { get; set; } = new List<LogMessage>();

        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter($"Engine\\{Name}.txt", false))
                {
                    writer.WriteLine(MaxPositions);
                    writer.WriteLine(MaxLots);
                    writer.Close();
                }
            }
            catch
            {
                // ignored
            }
        }

        public void Load()
        {
            var path = $"Engine\\{Name}.txt";
            if (!File.Exists(path))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    MaxPositions = Convert.ToInt32(reader.ReadLine());
                    MaxLots = Convert.ToInt32(reader.ReadLine());
                    reader.Close();
                }
            }
            catch (Exception e)
            {
                // ignore
            }
        }

        public void DeleteSettingsFile()
        {
            var path = $"Engine\\{Name}.txt";
            if (!File.Exists(path))
            {
                return;
            }
            File.Delete(path);
        }
    }

    public static class Comments
    {
        public const string NoError = "No error messages";
        public const string BadPositions = "Bad pos detected";

    }
}
