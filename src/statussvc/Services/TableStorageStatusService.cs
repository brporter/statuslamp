namespace StatusService.Services {
    using System;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using Microsoft.Extensions.Options;

    public class TableStorageStatusService 
        : IStatusService
    {
        const int TableCreationTimeout = 60000; // milliseconds
        const string DeviceIdentifierRowKey = "DeviceIdentifier";
        const string Unknown = "Unknown";

        readonly CloudTableClient _tableClient;
        readonly CloudTable _deviceStatusTable;
        readonly CloudTable _deviceIdentifierTable;

        public TableStorageStatusService(IOptions<AzureOptions> azureOptions)
        {
            var account = CloudStorageAccount.Parse(azureOptions.Value.StorageConnectionString);
            _tableClient = account.CreateCloudTableClient();
            _deviceStatusTable = _tableClient.GetTableReference("DeviceStatus");
            _deviceIdentifierTable = _tableClient.GetTableReference("DeviceIdentifiers");

            var statusTableCreationTask = _deviceStatusTable.CreateIfNotExistsAsync();
            var deviceIdTableCreationTask = _deviceIdentifierTable.CreateIfNotExistsAsync();            

            if (!statusTableCreationTask.Wait(TableCreationTimeout))
                throw new InvalidOperationException("Failed to ensure status table storage exists.");

            if (!deviceIdTableCreationTask.Wait(TableCreationTimeout))
                throw new InvalidOperationException("Failed to ensure device identifier table storage exists.");
        }

        public string GetStatus(Guid deviceIdentifier)
        {
            var key = deviceIdentifier.ToString();

            var operation = TableOperation.Retrieve<StatusEntity>(key, key);
            var resultTask = _deviceStatusTable.ExecuteAsync(operation);

            resultTask.Wait();

            var result = resultTask.Result.Result as StatusEntity;
            
            return result?.Status ?? Unknown;
        }

        public void SetStatus(Guid deviceIdentifier, string status)
        {
            var key = deviceIdentifier.ToString();

            var entity = new StatusEntity() {
                DeviceIdentifier = deviceIdentifier,
                Status = status,
                PartitionKey = key,
                RowKey = key
            };

            var operation = TableOperation.InsertOrReplace(entity);
            var resultTask = _deviceStatusTable.ExecuteAsync(operation);

            resultTask.Wait();
        }

        private class StatusEntity 
            : TableEntity 
        {
            public Guid DeviceIdentifier {get;set;}
            public string Status { get;set; }
        }
    }
}