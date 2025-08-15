#!/bin/bash

# Run Azure Functions isolated worker (.NET 8) from project root
echo "🚀 Starting Azure Function..."
cd PersonalDevSite.Functions || exit 1
func start
