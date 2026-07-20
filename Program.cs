using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace CosmosTest
{
    class Program
    {
        private static readonly string EndpointUri = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT") ?? "https://impactx-db-west-final.documents.azure.com:443/";
        private static readonly string PrimaryKey = Environment.GetEnvironmentVariable("COSMOS_KEY") ?? "";

        private static CosmosClient cosmosClient;
        private static Database database;
        private static Container container;

        private static string databaseId = "TestDatabase";
        private static string containerId = "TestContainer";

        static async Task Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==================================================");
            Console.WriteLine("  Cosmos DB NoSQL Connection Test (C# / .NET SDK)");
            Console.WriteLine("==================================================");
            Console.ResetColor();

            if (string.IsNullOrEmpty(PrimaryKey))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[ERROR] La variable de entorno 'COSMOS_KEY' no está configurada.");
                Console.WriteLine("Para ejecutar esta prueba localmente, ejecuta en tu terminal:");
                Console.WriteLine("  $env:COSMOS_KEY=\"tu_primary_key_aqui\"");
                Console.WriteLine("y luego vuelve a ejecutar: dotnet run");
                Console.ResetColor();
                return;
            }

            try
            {
                // 1. Initialize CosmosClient
                Console.WriteLine("\nConnecting to Cosmos DB...");
                cosmosClient = new CosmosClient(EndpointUri, PrimaryKey, new CosmosClientOptions() 
                { 
                    SerializerOptions = new CosmosSerializationOptions() { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase } 
                });
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Connection object initialized.");
                Console.ResetColor();

                // 2. Create Database if not exists
                Console.WriteLine($"\nEnsuring database '{databaseId}' exists...");
                database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Database '{databaseId}' is ready.");
                Console.ResetColor();

                // 3. Create Container if not exists (using \"/partitionKey\" as the partition key)
                Console.WriteLine($"Ensuring container '{containerId}' exists with partition key '/partitionKey'...");
                container = await database.CreateContainerIfNotExistsAsync(containerId, "/partitionKey");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Container '{containerId}' is ready.");
                Console.ResetColor();

                // 4. Create and Insert a test document
                string uniqueId = Guid.NewGuid().ToString();
                var testItem = new TestItem
                {
                    id = uniqueId,
                    partitionKey = "test-partition",
                    Name = "ImpactX Connection Test",
                    Message = "hola buenas noches haciendo pruebas",
                    Timestamp = DateTime.UtcNow
                };

                Console.WriteLine($"\nInserting a new document (ID: {testItem.id})...");
                ItemResponse<TestItem> testItemResponse = await container.CreateItemAsync<TestItem>(testItem, new PartitionKey(testItem.partitionKey));
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Document inserted successfully!");
                Console.ResetColor();
                Console.WriteLine($"  - Request Charge: {testItemResponse.Headers.RequestCharge} RUs");
                Console.WriteLine($"  - HTTP Status Code: {testItemResponse.StatusCode}");
                Console.WriteLine($"  - Inserted Item ID: {testItemResponse.Resource.id}");
                Console.WriteLine($"  - Timestamp (UTC): {testItemResponse.Resource.Timestamp}");

                // 5. Read back the document to verify read capability
                Console.WriteLine($"\nReading back the document (ID: {uniqueId}) to verify read access...");
                ItemResponse<TestItem> readResponse = await container.ReadItemAsync<TestItem>(uniqueId, new PartitionKey("test-partition"));
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Document read successfully!");
                Console.ResetColor();
                Console.WriteLine($"  - Name: {readResponse.Resource.Name}");
                Console.WriteLine($"  - Message: {readResponse.Resource.Message}");
            }
            catch (CosmosException de)
            {
                Exception baseException = de.GetBaseException();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nCosmos DB error: {de.StatusCode} - {de.Message} (Base: {baseException.Message})");
                Console.ResetColor();
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nError: {e.Message}");
                Console.ResetColor();
            }
            finally
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n==================================================");
                Console.WriteLine("  End of Cosmos DB Connection Test");
                Console.WriteLine("==================================================");
                Console.ResetColor();
            }
        }
    }

    public class TestItem
    {
        public string id { get; set; }
        public string partitionKey { get; set; }
        public string Name { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
