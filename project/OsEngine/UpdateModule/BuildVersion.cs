/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;

namespace OsEngine.UpdateModule
{
    public class BuildVersion
    {
        public DateTime VersionTime { get; set; }
        public string Path { get; set; }
        public string Open {  get; set; }
        public string RollBack { get; set; }
        public string OpenButtonText { get; set; }
        public string RollbackButtonText { get; set; }
    }
}
