using System;

namespace Kraken.WebSockets.Events
{
    public sealed class KrakenPrivateEventArgs<TPrivate> : EventArgs
    {
        public TPrivate PrivateMessage { get; }

        public KrakenPrivateEventArgs(TPrivate privateMessage)
        {
            PrivateMessage = privateMessage;
        }
    }
}
