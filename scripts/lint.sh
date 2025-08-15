#!/bin/bash

# Simple code analysis script for PersonalDevSite Backend
# Uses the linting setup from Directory.Build.props and .editorconfig

set -e


PROJECT_PATH="PersonalDevSite.Functions/PersonalDevSite.Functions.csproj"

echo "🔍 Running code analysis for PersonalDevSite.Functions project..."

# Check if project exists
if [[ ! -f "$PROJECT_PATH" ]]; then
    echo "❌ Project file not found: $PROJECT_PATH"
    exit 1
fi

# 1. Restore packages
echo "📦 Restoring packages..."
dotnet restore "$PROJECT_PATH"

# 2. Build with analysis enabled
echo "🔨 Building with code analysis..."
dotnet build "$PROJECT_PATH" --no-restore --verbosity normal

# 3. Format check (fail if issues found)
echo "🎨 Checking code formatting..."
dotnet format "$PROJECT_PATH" --verify-no-changes --verbosity normal
if [[ $? -ne 0 ]]; then
    echo "❌ Code formatting issues found. Run 'dotnet format' to fix them."
    exit 1
else
    echo "✅ Code formatting is good!"
fi

echo "✨ Code analysis complete!"
