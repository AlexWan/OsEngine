namespace AdminSlave.Model
{
    public class OsEngine: NotificationObject
    {
        public int ProcessId { get; set; } = 0;
        
        public string Path { get; private set; }
        
        private State _state;

        public OsEngine(string path)
        {
            Path = path;
        }

        public State State
        {
            get { return _state; }
            set { SetProperty(ref _state, value, () => State); }
        }
    }

    public enum State
    {
        Active,
        NotAsk,
        Off
    }
}
