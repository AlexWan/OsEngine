/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;

namespace OsEngine.UpdateModule
{
    public class UpdateResponse
    {
        public bool Success { get; set; }
        public int MissedCommitsCount { get; set; }
        public List<GithubFileInfo> Files { get; set; }
        public List<CommitDisplay> Commits { get; set; }
        public DateTime ServerTime { get; set; }
        public string Error { get; set; }
    }

    public class GithubFileInfo
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public DateTime LastUpdate { get; set; }
        public string Url { get; set; }
    }

    public class CommitDisplay
    {
        public string Name { get; set; }
        public string Number { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
