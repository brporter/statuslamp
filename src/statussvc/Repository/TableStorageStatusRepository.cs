namespace StatusService.Repository {
    using System;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using Microsoft.Extensions.Options;

    public class TableStorageStatusRepository : IStatusRepository
    {
        const int TableCreationTimeout = 60000; // milliseconds
        const string Unknown = "Unknown";

        readonly CloudTableClient _tableClient;
        readonly CloudTable _table;

        public TableStorageStatusRepository(IOptions<AzureOptions> azureOptions)
        {
            var account = CloudStorageAccount.Parse(azureOptions.Value.StorageConnectionString);
            _tableClient = account.CreateCloudTableClient();
            _table = _tableClient.GetTableReference("DeviceStatus");

            var resultTask = _table.CreateIfNotExistsAsync();

            if (!resultTask.Wait(TableCreationTimeout))
                throw new InvalidOperationException("Failed to ensure table storage exists.");
        }

        public string GetStatus(Guid deviceIdentifier)
        {
            var key = deviceIdentifier.ToString();

            var operation = TableOperation.Retrieve<StatusEntity>(key, key);
            var resultTask = _table.ExecuteAsync(operation);

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
            var resultTask = _table.ExecuteAsync(operation);

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