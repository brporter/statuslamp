namespace StatusService.Repository
{
    using System;
    using System.Collections.Concurrent;

    public class InMemoryStatusRepository 
        : IStatusRepository 
    {
        readonly ConcurrentDictionary<Guid, string> _status;

        public InMemoryStatusRepository()
        {
            _status = new ConcurrentDictionary<Guid, string>();
        }

        public void SetStatus(Guid deviceIdentifier, string status)
        {
            _status.AddOrUpdate(deviceIdentifier, status, (g, s) => status);
        }

        public string GetStatus(Guid deviceIdentifier)
        {
            if (_status.ContainsKey(deviceIdentifier))
                return _status[deviceIdentifier];

            return null;
        }
    }
}