using System.Diagnostics.Contracts;
using Kraken.WebSockets.Messages;

namespace Kraken.WebSockets.Events
{
    public class KrakenMessageEventArgs<TMessage> where TMessage : IKrakenMessage, new()
    {
        public TMessage Message { get; }

        public KrakenMessageEventArgs(TMessage message)
        {
            Contract.Requires( message != null);

            Message = message;
        }
    }
}
