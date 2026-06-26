/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple.pdf
*/

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OsEngine.MCP
{
    /// <summary>
    /// Allowed remote IP address and optional port for MCP API.
    /// Empty or "any" port means any remote port is allowed.
    /// </summary>
    public class McpAllowedIp : INotifyPropertyChanged
    {
        private string _ip;
        private string _port;

        public string Ip
        {
            get => _ip;
            set
            {
                if (_ip == value)
                {
                    return;
                }
                _ip = value;
                OnPropertyChanged();
            }
        }

        public string Port
        {
            get => _port;
            set
            {
                if (_port == value)
                {
                    return;
                }
                _port = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public McpAllowedIp Clone()
        {
            return new McpAllowedIp { Ip = Ip, Port = Port };
        }

        public override bool Equals(object obj)
        {
            if (obj is McpAllowedIp other)
            {
                return string.Equals(Ip, other.Ip, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Port, other.Port, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (Ip != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Ip) : 0);
                hash = hash * 23 + (Port != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Port) : 0);
                return hash;
            }
        }
    }
}
