# StellarAnvil Setup Script for Windows
# This script sets up the development environment for StellarAnvil

Write-Host "🚀 Setting up StellarAnvil development environment..." -ForegroundColor Green

# Check prerequisites
Write-Host "📋 Checking prerequisites..." -ForegroundColor Yellow

# Check .NET 9
try {
    $dotnetVersion = dotnet --version
    Write-Host "✅ .NET SDK found: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ .NET SDK not found. Please install .NET 9 SDK from https://dotnet.microsoft.com/download/dotnet/9.0" -ForegroundColor Red
    exit 1
}

# Check Docker
try {
    $dockerVersion = docker --version
    Write-Host "✅ Docker found: $dockerVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ Docker not found. Please install Docker Desktop from https://www.docker.com/products/docker-desktop" -ForegroundColor Red
    exit 1
}

# Check if Docker is running
try {
    docker info | Out-Null
    Write-Host "✅ Docker is running" -ForegroundColor Green
} catch {
    Write-Host "❌ Docker is not running. Please start Docker Desktop" -ForegroundColor Red
    exit 1
}

# Start infrastructure services
Write-Host "🐳 Starting infrastructure services..." -ForegroundColor Yellow
docker-compose up -d

# Wait for PostgreSQL to be ready
Write-Host "⏳ Waiting for PostgreSQL to be ready..." -ForegroundColor Yellow
Start-Sleep -Seconds 10

# Check if PostgreSQL is ready
$retries = 0
$maxRetries = 30
do {
    try {
        docker exec stellaranvil-postgres pg_isready -U stellaranvil -d stellaranvil | Out-Null
        Write-Host "✅ PostgreSQL is ready" -ForegroundColor Green
        break
    } catch {
        Write-Host "Waiting for PostgreSQL..." -ForegroundColor Yellow
        Start-Sleep -Seconds 2
        $retries++
    }
} while ($retries -lt $maxRetries)

if ($retries -eq $maxRetries) {
    Write-Host "❌ PostgreSQL failed to start" -ForegroundColor Red
    exit 1
}

# Restore NuGet packages
Write-Host "📦 Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore

# Build the solution
Write-Host "🔨 Building the solution..." -ForegroundColor Yellow
dotnet build

# Run database migrations
Write-Host "🗄️ Running database migrations..." -ForegroundColor Yellow
Set-Location "src/StellarAnvil.Api"
dotnet ef database update
Set-Location "../.."

Write-Host "🎉 Setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "🚀 To start the application:" -ForegroundColor Cyan
Write-Host "   dotnet run --project src/StellarAnvil.AppHost" -ForegroundColor White
Write-Host ""
Write-Host "🌐 Access points:" -ForegroundColor Cyan
Write-Host "   - API: https://localhost:5001" -ForegroundColor White
Write-Host "   - Swagger: https://localhost:5001" -ForegroundColor White
Write-Host "   - Grafana: http://localhost:3000 (admin/admin)" -ForegroundColor White
Write-Host "   - Prometheus: http://localhost:9090" -ForegroundColor White
Write-Host "   - Jaeger: http://localhost:16686" -ForegroundColor White
Write-Host ""
Write-Host "📚 Check the README.md for detailed usage instructions" -ForegroundColor Cyan
