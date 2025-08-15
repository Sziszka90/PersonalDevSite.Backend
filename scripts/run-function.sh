#!/bin/bash

# Run Azure Functions isolated worker (.NET 8) from project root
PROJECT_PATH="PersonalDevSite.Functions/PersonalDevSite.Functions.csproj"

# Run the Azure Function
echo "🚀 Starting Azure Function..."
dotnet run --project "$PROJECT_PATH"
