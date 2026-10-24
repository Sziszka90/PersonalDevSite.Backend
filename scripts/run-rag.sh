#!/bin/bash

set -e

# Resolve the project path from this script so it works from any directory.
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname -- "$SCRIPT_DIR")"

echo "Starting RAG indexer..."
dotnet run --project "$REPO_ROOT/PersonalDevSite.RAGIndexer/PersonalDevSite.RAGIndexer.csproj"
