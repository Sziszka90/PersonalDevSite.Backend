#!/bin/bash

set -e

# Run the RAG indexer from the project root
echo "Starting RAG indexer..."
dotnet run --project PersonalDevSite.RAGIndexer/PersonalDevSite.RAGIndexer.csproj
