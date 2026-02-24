/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;

namespace OsEngine.UpdateModule
{
    public class FileState : GithubFileInfo
    {
        public DateTime CurrVersionTime { get; set; }

        public State State { get; set; }
    }

    public enum State
    {
        Removed,
        Obsolete,
        New,
        Actual
    }
}
