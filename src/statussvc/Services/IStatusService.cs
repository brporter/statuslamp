namespace StatusService.Services
{
    using System;

    public interface IStatusService
    {
        string GetStatus(Guid deviceIdentifier);
        void SetStatus(Guid deviceIdentifier, string status);
    }
}