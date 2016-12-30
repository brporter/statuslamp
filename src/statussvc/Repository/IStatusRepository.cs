namespace StatusService.Repository
{
    using System;

    public interface IStatusRepository
    {
        string GetStatus(Guid deviceIdentifier);
        void SetStatus(Guid deviceIdentifier, string status);
    }
}