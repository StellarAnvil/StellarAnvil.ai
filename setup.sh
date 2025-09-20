#!/bin/bash

# StellarAnvil Setup Script
# This script sets up the development environment for StellarAnvil

set -e

echo "🚀 Setting up StellarAnvil development environment..."

# Check prerequisites
echo "📋 Checking prerequisites..."

# Check .NET 9
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK not found. Please install .NET 9 SDK from https://dotnet.microsoft.com/download/dotnet/9.0"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo "✅ .NET SDK found: $DOTNET_VERSION"

# Check Docker
if ! command -v docker &> /dev/null; then
    echo "❌ Docker not found. Please install Docker Desktop from https://www.docker.com/products/docker-desktop"
    exit 1
fi

echo "✅ Docker found: $(docker --version)"

# Check if Docker is running
if ! docker info &> /dev/null; then
    echo "❌ Docker is not running. Please start Docker Desktop"
    exit 1
fi

echo "✅ Docker is running"

# Start infrastructure services
echo "🐳 Starting infrastructure services..."
docker-compose up -d

# Wait for PostgreSQL to be ready
echo "⏳ Waiting for PostgreSQL to be ready..."
sleep 10

# Check if PostgreSQL is ready
until docker exec stellaranvil-postgres pg_isready -U stellaranvil -d stellaranvil; do
    echo "Waiting for PostgreSQL..."
    sleep 2
done

echo "✅ PostgreSQL is ready"

# Restore NuGet packages
echo "📦 Restoring NuGet packages..."
dotnet restore

# Build the solution
echo "🔨 Building the solution..."
dotnet build

# Run database migrations
echo "🗄️ Running database migrations..."
cd src/StellarAnvil.Api
dotnet ef database update
cd ../..

echo "🎉 Setup complete!"
echo ""
echo "🚀 To start the application:"
echo "   dotnet run --project src/StellarAnvil.AppHost"
echo ""
echo "🌐 Access points:"
echo "   - API: https://localhost:5001"
echo "   - Swagger: https://localhost:5001"
echo "   - Grafana: http://localhost:3000 (admin/admin)"
echo "   - Prometheus: http://localhost:9090"
echo "   - Jaeger: http://localhost:16686"
echo ""
echo "📚 Check the README.md for detailed usage instructions"
