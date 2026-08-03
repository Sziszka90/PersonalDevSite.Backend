# PersonalDevSite.Backend

This project is an Azure Functions (.NET isolated worker) backend for a personal developer site.
It provides API endpoints for AI-powered chat (ChatGPT), user summary, and other backend services.

## Features

- Azure Functions HTTP endpoints
- ChatGPT integration via OpenAI API
- Custom CORS middleware for frontend compatibility
- Dependency injection for services and clients
- Structured error handling and logging

## Getting Started

1. Install [.NET 8 SDK](https://dotnet.microsoft.com/download) and [Azure Functions Core Tools](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local).
2. Clone the repository.
3. Run the backend locally:
   ```sh
   cd PersonalDevSite.Functions
   func start --dotnet-isolated-debug
   ```
4. Configure environment variables in `local.settings.json` (e.g., OpenAI API key).

## Azure AI Search indexing

`PersonalDevSite.RAGIndexer` reads `PersonalDevSite.RAGIndexer/user_summary.txt`, creates embeddings with the configured Azure OpenAI embedding deployment, and uploads the chunks to an existing Azure AI Search index.

The shared non-secret settings are stored in `PersonalDevSite.Functions/appsettings.json` and are copied to the indexer automatically. Local secrets belong in the ignored `PersonalDevSite.RAGIndexer/appsettings.Development.json` file or environment variables.

```sh
export AZURE_OPENAI_ENDPOINT="https://<resource-name>.openai.azure.com/"
export AZURE_OPENAI_EMBEDDING_DEPLOYMENT="text-embedding-3-small"
export AZURE_SEARCH_ENDPOINT="https://<service-name>.search.windows.net"
export AZURE_SEARCH_INDEX_NAME="personaldevsite-chunks"
```

The indexer loads `appsettings.json`, then applies values from `appsettings.Development.json`, and finally lets environment variables override both. Local Functions runs read `AZURE_OPENAI_API_KEY` and `AZURE_SEARCH_API_KEY` from the Functions environment or `local.settings.json`. The deployed Functions app reads the Key Vault secrets `openai-api-key` and `search-api-key` when `AZURE_KEY_VAULT_URI` is configured. Production does not fall back to the local `AZURE_*_API_KEY` settings, and the Functions app fails fast when either required secret is missing. The signed-in or managed identity needs permission to read the Key Vault secrets and use the Azure OpenAI and Search services.

Run the indexer from the repository root:

```sh
dotnet run --project PersonalDevSite.RAGIndexer/PersonalDevSite.RAGIndexer.csproj
```

The indexer expects the configured index to already contain compatible `id`, `content`, and `embedding` fields. Functions uses the same index for hybrid retrieval before sending context to ChatGPT.

The model project endpoint is configured as `AZURE_OPENAI_MODEL_ENDPOINT` in `appsettings.json`, with model name `AZURE_OPENAI_MODEL_NAME`. Model requests use the required `AZURE_OPENAI_API_KEY` through the OpenAI Responses API, sourced locally from `local.settings.json` and in production from the `openai-api-key` Key Vault secret.

## Project Structure

- `PersonalDevSite.Functions/` — Azure Functions project
- `Clients/` — Service clients (e.g., ChatGptClient)
- `Middleware/` — Custom middleware (e.g., CORS)
- `Dtos/` — Data transfer objects
- `Clients/` — Service clients (e.g., OpenAIClient)

## License

MIT
