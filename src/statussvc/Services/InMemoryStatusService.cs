namespace StatusService.Services
{
    using System;
    using System.Collections.Concurrent;

    public class InMemoryStatusService
        : IStatusService 
    {
        readonly ConcurrentDictionary<Guid, string> _status;

        public InMemoryStatusService()
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