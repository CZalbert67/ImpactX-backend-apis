# Guía de Conexión a Cosmos DB - ImpactX Backend APIs

Esta guía contiene la configuración de acceso y snippets de código listos para conectar el Backend a **Azure Cosmos DB NoSQL**.

## Credenciales de Conexión (Desarrollo / Pruebas)

* **Account Endpoint:** `https://impactx-db-west-final.documents.azure.com:443/`
* **Account Key (Lectura y Escritura):** `<REPLACE_WITH_YOUR_COSMOS_KEY>`
* **Base de Datos Principal:** `ImpactX-Data`
* **Base de Datos Temporal/Test:** `TestDatabase`

---

## 🔒 Buenas Prácticas de Configuración

No escribas de forma fija (hardcodeada) las credenciales de base de datos en tu código fuente. Configúralas como variables de entorno o archivos de configuración local ignorados por Git:

* **En C# (.NET):** Guárdalas en `appsettings.Development.json` o usa Secret Manager de User Secrets.
* **En Node.js:** Guárdalas en un archivo `.env` localmente (`COSMOS_ENDPOINT`, `COSMOS_KEY`).

---

## Snippets de Código de Conexión

### 1. Ejemplo en C# / .NET SDK (Microsoft.Azure.Cosmos)
Instalar paquetes: `dotnet add package Microsoft.Azure.Cosmos` y `dotnet add package Newtonsoft.Json`.

```csharp
using Microsoft.Azure.Cosmos;

string endpoint = "https://impactx-db-west-final.documents.azure.com:443/";
string key = "<REPLACE_WITH_YOUR_COSMOS_KEY>";

CosmosClient cosmosClient = new CosmosClient(endpoint, key, new CosmosClientOptions() 
{ 
    SerializerOptions = new CosmosSerializationOptions() { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase } 
});

// Obtener base de datos y contenedor
Database database = cosmosClient.GetDatabase("ImpactX-Data");
Container container = database.GetContainer("MiContenedor");

// Insertar Item
MyItem item = new MyItem { id = "123", partitionKey = "grupo-1", name = "Test" };
await container.CreateItemAsync(item, new PartitionKey(item.partitionKey));
```

### 2. Ejemplo en Node.js (SDK @azure/cosmos)
Instalar paquete: `npm install @azure/cosmos`

```javascript
import { CosmosClient } from "@azure/cosmos";

const endpoint = "https://impactx-db-west-final.documents.azure.com:443/";
const key = "<REPLACE_WITH_YOUR_COSMOS_KEY>";

const client = new CosmosClient({ endpoint, key });

// Obtener base de datos y contenedor
const database = client.database("ImpactX-Data");
const container = database.container("MiContenedor");

// Insertar Item
const item = { id: "123", partitionKey: "grupo-1", name: "Test" };
const { resource } = await container.items.create(item);
console.log(`Documento creado con ID: ${resource.id}`);
```

---

## Cómo usar Cosmos DB Studio para Validaciones Locales

Usa Cosmos DB Studio para validar que los datos enviados por la API lleguen correctamente:

1. Descarga e instala **Cosmos DB Studio**.
2. Crea una nueva conexión ingresando los siguientes datos:
   * **Name:** `ImpactX`
   * **Endpoint:** `https://impactx-db-west-final.documents.azure.com:443/`
   * **Key:** `<REPLACE_WITH_YOUR_COSMOS_KEY>`
   * **Serverless:** Desmarcado
   * **Folder:** En blanco
3. Haz clic en **OK**.
4. Haz doble clic en el contenedor bajo `ImpactX-Data`.
5. En la ventana central escribe:
   ```sql
   SELECT * FROM c
   ```
6. Haz clic en el botón de **Play (Triángulo Negro)**.
