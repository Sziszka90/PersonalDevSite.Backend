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

## Project Structure

- `PersonalDevSite.Functions/` — Azure Functions project
- `Clients/` — Service clients (e.g., ChatGptClient)
- `Middleware/` — Custom middleware (e.g., CORS)
- `Dtos/` — Data transfer objects

## License

MIT
