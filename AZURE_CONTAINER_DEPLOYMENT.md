# Azure Container Apps Deployment Guide

## Overview

This guide covers deploying DDClipBot to **Azure Container Apps** using a multi-container approach. Container Apps is ideal for:
- Running multiple containers (backend + frontend) in one environment
- Built-in scaling and load balancing
- Lower cost than App Service for dual-service apps
- Native Docker support with automatic builds

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│         Azure Container Apps Environment                │
│                                                          │
│  ┌──────────────────────┐    ┌────────────────────────┐│
│  │   Frontend Container │    │   Backend Container    ││
│  │   (Next.js:3000)     │───▶│   (.NET API:5000)     ││
│  │   Port: 443 (public) │    │   Port: 5000 (internal)││
│  └──────────────────────┘    └────────────────────────┘│
│           │                                              │
│           └──────────────────────────────────────────────┤
│                    Azure Container Apps Ingress          │
└─────────────────────────────────────────────────────────┘
                        │
                        ▼
              Public URL: https://ddclipbot.region.azurecontainerapps.io
```

**Benefits:**
- Frontend and backend deployed as separate containers
- Internal networking between containers (no CORS needed for internal calls)
- Frontend publicly accessible, backend internal-only (more secure)
- Automatic HTTPS with managed certificates
- Built-in secrets management

---

## Prerequisites

### 1. Azure CLI & Tools

```powershell
# Install Azure CLI
winget install Microsoft.AzureCLI

# Install Docker Desktop
winget install Docker.DockerDesktop

# Verify installations
az --version
docker --version
```

### 2. Azure Login

```powershell
# Login to Azure
az login

# Set your subscription (if you have multiple)
az account list --output table
az account set --subscription "YOUR_SUBSCRIPTION_ID"
```

### 3. Register Container Apps Provider

```powershell
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.OperationalInsights
```

---

## Phase 1: Create Dockerfiles

### Backend Dockerfile

Create `src/DDClipBot.Host/Dockerfile`:

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["DDClipBot.Host.csproj", "./"]
RUN dotnet restore "DDClipBot.Host.csproj"

# Copy source code and build
COPY . .
RUN dotnet build "DDClipBot.Host.csproj" -c Release -o /app/build
RUN dotnet publish "DDClipBot.Host.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Create temp directory for uploads
RUN mkdir -p /tmp/ddclipbot-uploads && chmod 777 /tmp/ddclipbot-uploads

# Copy published app
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5000

ENTRYPOINT ["dotnet", "DDClipBot.Host.dll"]
```

### Frontend Dockerfile

Create `src/ddclipbot-frontend/Dockerfile`:

```dockerfile
# Build stage
FROM node:20-alpine AS build
WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies
RUN npm ci --only=production

# Copy source code
COPY . .

# Build Next.js app
RUN npm run build

# Runtime stage
FROM node:20-alpine AS final
WORKDIR /app

# Copy built app and dependencies
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/public ./public

# Set environment variables
ENV NODE_ENV=production
ENV PORT=3000

EXPOSE 3000

CMD ["node", "server.js"]
```

### Update next.config.ts for Standalone Build

```typescript
import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: 'standalone', // Enable standalone build for Docker
  async rewrites() {
    const backendUrl = process.env.BACKEND_URL || 'http://backend:5000';
    return [
      {
        source: '/api/:path*',
        destination: `${backendUrl}/api/:path*`,
      },
    ];
  },
  serverActions: {
    bodySizeLimit: '2gb'
  }
};

export default nextConfig;
```

### .dockerignore Files

Create `src/DDClipBot.Host/.dockerignore`:
```
bin/
obj/
*.user
.vs/
.vscode/
*.log
```

Create `src/ddclipbot-frontend/.dockerignore`:
```
node_modules/
.next/
.git/
*.log
.env*.local
```

---

## Phase 2: Test Docker Builds Locally

### Build Backend

```powershell
cd src\DDClipBot.Host
docker build -t ddclipbot-backend:latest .
```

### Build Frontend

```powershell
cd src\ddclipbot-frontend
docker build -t ddclipbot-frontend:latest .
```

### Test Locally with Docker Compose (Optional)

Create `docker-compose.yml` in repo root:

```yaml
version: '3.8'

services:
  backend:
    build:
      context: ./src/DDClipBot.Host
      dockerfile: Dockerfile
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - Discord__Token=${DISCORD_TOKEN}
      - Discord__ClientSecret=${DISCORD_CLIENT_SECRET}
      - YouTube__ClientId=${YOUTUBE_CLIENT_ID}
      - YouTube__ClientSecret=${YOUTUBE_CLIENT_SECRET}
      - YouTube__RefreshToken=${YOUTUBE_REFRESH_TOKEN}

  frontend:
    build:
      context: ./src/ddclipbot-frontend
      dockerfile: Dockerfile
    ports:
      - "3000:3000"
    environment:
      - BACKEND_URL=http://backend:5000
      - NEXT_PUBLIC_API_URL=http://localhost:5000
    depends_on:
      - backend
```

Test:
```powershell
docker-compose up
```

---

## Phase 3: Create Azure Container Registry (ACR)

```powershell
# Set variables
$RESOURCE_GROUP = "ddclipbot-rg"
$LOCATION = "eastus"
$ACR_NAME = "ddclipbotacr"  # Must be globally unique, lowercase, no hyphens

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create Container Registry
az acr create `
  --resource-group $RESOURCE_GROUP `
  --name $ACR_NAME `
  --sku Basic `
  --admin-enabled true

# Get ACR login credentials
az acr credential show --name $ACR_NAME --resource-group $RESOURCE_GROUP
```

**Save the output:** You'll need the username and password.

---

## Phase 4: Push Images to ACR

### Login to ACR

```powershell
az acr login --name $ACR_NAME
```

### Tag and Push Backend

```powershell
cd src\DDClipBot.Host

# Build for ACR
docker build -t "$ACR_NAME.azurecr.io/ddclipbot-backend:latest" .

# Push to ACR
docker push "$ACR_NAME.azurecr.io/ddclipbot-backend:latest"
```

### Tag and Push Frontend

```powershell
cd src\ddclipbot-frontend

# Build for ACR
docker build -t "$ACR_NAME.azurecr.io/ddclipbot-frontend:latest" .

# Push to ACR
docker push "$ACR_NAME.azurecr.io/ddclipbot-frontend:latest"
```

---

## Phase 5: Create Container Apps Environment

```powershell
$CONTAINERAPPS_ENV = "ddclipbot-env"

# Create Log Analytics workspace (required for Container Apps)
$LOG_ANALYTICS = "ddclipbot-logs"
az monitor log-analytics workspace create `
  --resource-group $RESOURCE_GROUP `
  --workspace-name $LOG_ANALYTICS `
  --location $LOCATION

# Get workspace ID and key
$LOG_ANALYTICS_ID = az monitor log-analytics workspace show `
  --resource-group $RESOURCE_GROUP `
  --workspace-name $LOG_ANALYTICS `
  --query customerId `
  --output tsv

$LOG_ANALYTICS_KEY = az monitor log-analytics workspace get-shared-keys `
  --resource-group $RESOURCE_GROUP `
  --workspace-name $LOG_ANALYTICS `
  --query primarySharedKey `
  --output tsv

# Create Container Apps environment
az containerapp env create `
  --name $CONTAINERAPPS_ENV `
  --resource-group $RESOURCE_GROUP `
  --location $LOCATION `
  --logs-workspace-id $LOG_ANALYTICS_ID `
  --logs-workspace-key $LOG_ANALYTICS_KEY
```

---

## Phase 6: Deploy Backend Container App

```powershell
$BACKEND_APP = "ddclipbot-backend"
$ACR_SERVER = "$ACR_NAME.azurecr.io"

# Get ACR credentials
$ACR_USERNAME = az acr credential show --name $ACR_NAME --query username --output tsv
$ACR_PASSWORD = az acr credential show --name $ACR_NAME --query "passwords[0].value" --output tsv

# Deploy backend (INTERNAL ingress - not publicly accessible)
az containerapp create `
  --name $BACKEND_APP `
  --resource-group $RESOURCE_GROUP `
  --environment $CONTAINERAPPS_ENV `
  --image "$ACR_SERVER/ddclipbot-backend:latest" `
  --target-port 5000 `
  --ingress internal `
  --registry-server $ACR_SERVER `
  --registry-username $ACR_USERNAME `
  --registry-password $ACR_PASSWORD `
  --cpu 1.0 `
  --memory 2.0Gi `
  --min-replicas 1 `
  --max-replicas 3 `
  --secrets `
    discord-token="YOUR_DISCORD_TOKEN" `
    discord-client-secret="YOUR_DISCORD_CLIENT_SECRET" `
    youtube-client-id="YOUR_YOUTUBE_CLIENT_ID" `
    youtube-client-secret="YOUR_YOUTUBE_CLIENT_SECRET" `
    youtube-refresh-token="YOUR_YOUTUBE_REFRESH_TOKEN" `
  --env-vars `
    "ASPNETCORE_ENVIRONMENT=Production" `
    "Discord__ClientId=YOUR_DISCORD_CLIENT_ID" `
    "Discord__RedirectUri=https://YOUR-FRONTEND-URL/login" `
    "Discord__GuildId=277523569708105729" `
    "Discord__Token=secretref:discord-token" `
    "Discord__ClientSecret=secretref:discord-client-secret" `
    "YouTube__ClientId=secretref:youtube-client-id" `
    "YouTube__ClientSecret=secretref:youtube-client-secret" `
    "YouTube__RefreshToken=secretref:youtube-refresh-token" `
    "YouTube__ApplicationName=DDClipBot" `
    "Upload__MaxFileSizeBytes=2147483648"

# Get backend internal FQDN
$BACKEND_FQDN = az containerapp show `
  --name $BACKEND_APP `
  --resource-group $RESOURCE_GROUP `
  --query properties.configuration.ingress.fqdn `
  --output tsv

Write-Host "Backend FQDN: $BACKEND_FQDN"
```

---

## Phase 7: Deploy Frontend Container App

```powershell
$FRONTEND_APP = "ddclipbot-frontend"

# Deploy frontend (EXTERNAL ingress - publicly accessible)
az containerapp create `
  --name $FRONTEND_APP `
  --resource-group $RESOURCE_GROUP `
  --environment $CONTAINERAPPS_ENV `
  --image "$ACR_SERVER/ddclipbot-frontend:latest" `
  --target-port 3000 `
  --ingress external `
  --registry-server $ACR_SERVER `
  --registry-username $ACR_USERNAME `
  --registry-password $ACR_PASSWORD `
  --cpu 0.5 `
  --memory 1.0Gi `
  --min-replicas 1 `
  --max-replicas 2 `
  --env-vars `
    "NODE_ENV=production" `
    "BACKEND_URL=https://$BACKEND_FQDN" `
    "NEXT_PUBLIC_API_URL=https://$BACKEND_FQDN"

# Get frontend public URL
$FRONTEND_URL = az containerapp show `
  --name $FRONTEND_APP `
  --resource-group $RESOURCE_GROUP `
  --query properties.configuration.ingress.fqdn `
  --output tsv

Write-Host "Frontend URL: https://$FRONTEND_URL"
```

---

## Phase 8: Update Backend with Frontend URL

Now that we know the frontend URL, update the backend:

```powershell
# Update backend with correct Discord redirect URI
az containerapp update `
  --name $BACKEND_APP `
  --resource-group $RESOURCE_GROUP `
  --set-env-vars "Discord__RedirectUri=https://$FRONTEND_URL/login"
```

---

## Phase 9: Configure Production Settings

### Update Discord OAuth

1. Go to [Discord Developer Portal](https://discord.com/developers/applications)
2. Select your application
3. Go to **OAuth2** → **Redirects**
4. Add: `https://$FRONTEND_URL/login`
5. Save changes

### Update Google OAuth Consent Screen

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Navigate to **APIs & Services** → **OAuth consent screen**
3. Add authorized domain: `azurecontainerapps.io`
4. Go to **Credentials** → Select your OAuth 2.0 Client ID
5. Add redirect URI: `https://developers.google.com/oauthplayground` (for token refresh)
6. **Publish the app** (move from Testing to Production) to prevent 7-day token expiration

### Update CORS in Backend (if needed)

Edit `src/DDClipBot.Host/Program.cs`:

```csharp
if (app.Environment.IsProduction())
{
    // In production, frontend calls backend internally via Container Apps networking
    // No CORS needed for internal calls, but allow public for direct API testing
    app.UseCors(policy => policy
        .WithOrigins($"https://{Environment.GetEnvironmentVariable("FRONTEND_FQDN")}")
        .AllowCredentials()
        .AllowAnyMethod()
        .AllowAnyHeader());
}
else
{
    app.UseCors(policy => policy
        .WithOrigins("http://localhost:3000")
        .AllowCredentials()
        .AllowAnyMethod()
        .AllowAnyHeader());
}
```

### Update Secure Cookie Settings

Edit `src/DDClipBot.Host/Endpoints/AuthEndpoints.cs` around line 117:

```csharp
options.Secure = app.Environment.IsProduction(); // Change from false
```

---

## Phase 10: Set Up Persistent Storage

Container Apps have ephemeral storage by default. For production:

### Option A: Azure Files (Simple, Good for Temp Uploads)

```powershell
# Create storage account
$STORAGE_ACCOUNT = "ddclipbotstorage"  # Lowercase, no hyphens
az storage account create `
  --name $STORAGE_ACCOUNT `
  --resource-group $RESOURCE_GROUP `
  --location $LOCATION `
  --sku Standard_LRS

# Create file share
az storage share create `
  --name uploads `
  --account-name $STORAGE_ACCOUNT

# Get storage key
$STORAGE_KEY = az storage account keys list `
  --account-name $STORAGE_ACCOUNT `
  --resource-group $RESOURCE_GROUP `
  --query "[0].value" `
  --output tsv

# Add storage to Container Apps environment
az containerapp env storage set `
  --name $CONTAINERAPPS_ENV `
  --resource-group $RESOURCE_GROUP `
  --storage-name uploads `
  --azure-file-account-name $STORAGE_ACCOUNT `
  --azure-file-account-key $STORAGE_KEY `
  --azure-file-share-name uploads `
  --access-mode ReadWrite

# Update backend to mount storage
az containerapp update `
  --name $BACKEND_APP `
  --resource-group $RESOURCE_GROUP `
  --set-env-vars "Upload__TempPath=/mnt/uploads"
```

### Option B: Azure Blob Storage (Better for Large Files)

Add to backend secrets:
```powershell
az containerapp update `
  --name $BACKEND_APP `
  --resource-group $RESOURCE_GROUP `
  --secrets storage-connection-string="YOUR_BLOB_CONNECTION_STRING" `
  --set-env-vars "Upload__StorageConnectionString=secretref:storage-connection-string"
```

Then update `VideoUploadWorker.cs` to use Azure Blob SDK.

---

## Phase 11: Set Up Database for Sessions/Jobs

### Option A: Azure SQL Database

```powershell
$SQL_SERVER = "ddclipbot-sql"
$SQL_DB = "ddclipbot"
$SQL_ADMIN = "sqladmin"
$SQL_PASSWORD = "YOUR_STRONG_PASSWORD_HERE"  # Change this!

# Create SQL Server
az sql server create `
  --name $SQL_SERVER `
  --resource-group $RESOURCE_GROUP `
  --location $LOCATION `
  --admin-user $SQL_ADMIN `
  --admin-password $SQL_PASSWORD

# Allow Azure services to access SQL Server
az sql server firewall-rule create `
  --resource-group $RESOURCE_GROUP `
  --server $SQL_SERVER `
  --name AllowAzureServices `
  --start-ip-address 0.0.0.0 `
  --end-ip-address 0.0.0.0

# Create database
az sql db create `
  --resource-group $RESOURCE_GROUP `
  --server $SQL_SERVER `
  --name $SQL_DB `
  --service-objective S0

# Get connection string
$SQL_CONNECTION_STRING = "Server=tcp:$SQL_SERVER.database.windows.net,1433;Initial Catalog=$SQL_DB;Persist Security Info=False;User ID=$SQL_ADMIN;Password=$SQL_PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

# Add to backend
az containerapp update `
  --name $BACKEND_APP `
  --resource-group $RESOURCE_GROUP `
  --secrets sql-connection-string="$SQL_CONNECTION_STRING" `
  --set-env-vars "ConnectionStrings__DefaultConnection=secretref:sql-connection-string"
```

### Option B: Azure Redis Cache (Faster, Simpler)

```powershell
$REDIS_NAME = "ddclipbot-redis"

# Create Redis Cache (Basic tier)
az redis create `
  --name $REDIS_NAME `
  --resource-group $RESOURCE_GROUP `
  --location $LOCATION `
  --sku Basic `
  --vm-size c0

# Get Redis connection string
$REDIS_KEY = az redis list-keys `
  --name $REDIS_NAME `
  --resource-group $RESOURCE_GROUP `
  --query primaryKey `
  --output tsv

$REDIS_CONNECTION_STRING = "$REDIS_NAME.redis.cache.windows.net:6380,password=$REDIS_KEY,ssl=True,abortConnect=False"

# Add to backend
az containerapp update `
  --name $BACKEND_APP `
  --resource-group $RESOURCE_GROUP `
  --secrets redis-connection-string="$REDIS_CONNECTION_STRING" `
  --set-env-vars "Redis__ConnectionString=secretref:redis-connection-string"
```

Then install `StackExchange.Redis` NuGet package and update `SessionStore.cs` and `JobStore.cs`.

---

## Phase 12: Enable Application Insights

```powershell
$APPINSIGHTS_NAME = "ddclipbot-insights"

# Create Application Insights
az monitor app-insights component create `
  --app $APPINSIGHTS_NAME `
  --location $LOCATION `
  --resource-group $RESOURCE_GROUP `
  --application-type web

# Get instrumentation key
$APPINSIGHTS_KEY = az monitor app-insights component show `
  --app $APPINSIGHTS_NAME `
  --resource-group $RESOURCE_GROUP `
  --query instrumentationKey `
  --output tsv

# Add to backend
az containerapp update `
  --name $BACKEND_APP `
  --resource-group $RESOURCE_GROUP `
  --secrets appinsights-key="$APPINSIGHTS_KEY" `
  --set-env-vars "ApplicationInsights__InstrumentationKey=secretref:appinsights-key"
```

Add to backend `DDClipBot.Host.csproj`:
```xml
<PackageReference Include="Microsoft.ApplicationInsights.AspNetCore" Version="2.22.0" />
```

Add to `Program.cs`:
```csharp
builder.Services.AddApplicationInsightsTelemetry();
```

---

## Phase 13: Set Up Custom Domain (Optional)

```powershell
# Add custom domain to frontend
az containerapp hostname add `
  --hostname "ddclipbot.yourdomain.com" `
  --resource-group $RESOURCE_GROUP `
  --name $FRONTEND_APP

# Bind certificate (managed certificate)
az containerapp hostname bind `
  --hostname "ddclipbot.yourdomain.com" `
  --resource-group $RESOURCE_GROUP `
  --name $FRONTEND_APP `
  --environment $CONTAINERAPPS_ENV `
  --validation-method CNAME
```

---

## Phase 14: Automated Deployment Script

Create `deploy.ps1` in repo root:

```powershell
#!/usr/bin/env pwsh

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup,
    
    [Parameter(Mandatory=$true)]
    [string]$AcrName,
    
    [string]$BackendApp = "ddclipbot-backend",
    [string]$FrontendApp = "ddclipbot-frontend"
)

Write-Host "=== DDClipBot Deployment Script ===" -ForegroundColor Cyan

# Login to ACR
Write-Host "Logging into Azure Container Registry..." -ForegroundColor Yellow
az acr login --name $AcrName

# Build and push backend
Write-Host "Building backend image..." -ForegroundColor Yellow
Set-Location src\DDClipBot.Host
docker build -t "$AcrName.azurecr.io/ddclipbot-backend:latest" .
docker push "$AcrName.azurecr.io/ddclipbot-backend:latest"

# Build and push frontend
Write-Host "Building frontend image..." -ForegroundColor Yellow
Set-Location ..\ddclipbot-frontend
docker build -t "$AcrName.azurecr.io/ddclipbot-frontend:latest" .
docker push "$AcrName.azurecr.io/ddclipbot-frontend:latest"

Set-Location ..\..

# Update backend container app
Write-Host "Updating backend container app..." -ForegroundColor Yellow
az containerapp update `
  --name $BackendApp `
  --resource-group $ResourceGroup `
  --image "$AcrName.azurecr.io/ddclipbot-backend:latest"

# Update frontend container app
Write-Host "Updating frontend container app..." -ForegroundColor Yellow
az containerapp update `
  --name $FrontendApp `
  --resource-group $ResourceGroup `
  --image "$AcrName.azurecr.io/ddclipbot-frontend:latest"

Write-Host "=== Deployment Complete! ===" -ForegroundColor Green

# Get URLs
$FrontendUrl = az containerapp show `
  --name $FrontendApp `
  --resource-group $ResourceGroup `
  --query properties.configuration.ingress.fqdn `
  --output tsv

Write-Host "Frontend URL: https://$FrontendUrl" -ForegroundColor Cyan
```

Usage:
```powershell
.\deploy.ps1 -ResourceGroup "ddclipbot-rg" -AcrName "ddclipbotacr"
```

---

## Phase 15: GitHub Actions CI/CD (Optional)

Create `.github/workflows/deploy.yml`:

```yaml
name: Deploy to Azure Container Apps

on:
  push:
    branches: [ main ]
  workflow_dispatch:

env:
  RESOURCE_GROUP: ddclipbot-rg
  ACR_NAME: ddclipbotacr
  BACKEND_APP: ddclipbot-backend
  FRONTEND_APP: ddclipbot-frontend

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Azure Login
      uses: azure/login@v1
      with:
        creds: ${{ secrets.AZURE_CREDENTIALS }}
    
    - name: Login to ACR
      run: az acr login --name ${{ env.ACR_NAME }}
    
    - name: Build and Push Backend
      run: |
        cd src/DDClipBot.Host
        docker build -t ${{ env.ACR_NAME }}.azurecr.io/ddclipbot-backend:${{ github.sha }} .
        docker tag ${{ env.ACR_NAME }}.azurecr.io/ddclipbot-backend:${{ github.sha }} ${{ env.ACR_NAME }}.azurecr.io/ddclipbot-backend:latest
        docker push ${{ env.ACR_NAME }}.azurecr.io/ddclipbot-backend:${{ github.sha }}
        docker push ${{ env.ACR_NAME }}.azurecr.io/ddclipbot-backend:latest
    
    - name: Build and Push Frontend
      run: |
        cd src/ddclipbot-frontend
        docker build -t ${{ env.ACR_NAME }}.azurecr.io/ddclipbot-frontend:${{ github.sha }} .
        docker tag ${{ env.ACR_NAME }}.azurecr.io/ddclipbot-frontend:${{ github.sha }} ${{ env.ACR_NAME }}.azurecr.io/ddclipbot-frontend:latest
        docker push ${{ env.ACR_NAME }}.azurecr.io/ddclipbot-frontend:${{ github.sha }}
        docker push ${{ env.ACR_NAME }}.azurecr.io/ddclipbot-frontend:latest
    
    - name: Deploy Backend
      run: |
        az containerapp update \
          --name ${{ env.BACKEND_APP }} \
          --resource-group ${{ env.RESOURCE_GROUP }} \
          --image ${{ env.ACR_NAME }}.azurecr.io/ddclipbot-backend:${{ github.sha }}
    
    - name: Deploy Frontend
      run: |
        az containerapp update \
          --name ${{ env.FRONTEND_APP }} \
          --resource-group ${{ env.RESOURCE_GROUP }} \
          --image ${{ env.ACR_NAME }}.azurecr.io/ddclipbot-frontend:${{ github.sha }}
```

Set up Azure credentials secret:
```powershell
az ad sp create-for-rbac --name "ddclipbot-github" --sdk-auth --role contributor --scopes /subscriptions/YOUR_SUBSCRIPTION_ID/resourceGroups/ddclipbot-rg
```

Add the JSON output as a GitHub secret named `AZURE_CREDENTIALS`.

---

## Phase 16: Monitoring & Health Checks

### Add Health Check Endpoint

Add to `Program.cs`:

```csharp
app.MapGet("/health", async (IJobStore jobStore, GatewayClient discord) =>
{
    var health = new
    {
        Status = "Healthy",
        Timestamp = DateTime.UtcNow,
        Discord = discord.IsReady ? "Connected" : "Disconnected",
        Jobs = new
        {
            Pending = jobStore.GetPendingJobs().Count()
        }
    };
    
    return Results.Ok(health);
});
```

Configure in Container Apps:
```powershell
az containerapp update `
  --name $BACKEND_APP `
  --resource-group $RESOURCE_GROUP `
  --health-probe-type liveness `
  --health-probe-path /health `
  --health-probe-interval 30 `
  --health-probe-timeout 3 `
  --health-probe-threshold 3
```

### View Logs

```powershell
# Backend logs
az containerapp logs show `
  --name $BACKEND_APP `
  --resource-group $RESOURCE_GROUP `
  --follow

# Frontend logs
az containerapp logs show `
  --name $FRONTEND_APP `
  --resource-group $RESOURCE_GROUP `
  --follow
```

---

## Cost Estimation

### Minimal Setup (Development/Small Scale)
- **Container Apps Environment**: Free (included in resource costs)
- **Backend Container**: ~$15-25/month (1 vCPU, 2GB RAM, 1 replica)
- **Frontend Container**: ~$10-15/month (0.5 vCPU, 1GB RAM, 1 replica)
- **Container Registry (Basic)**: ~$5/month (10GB storage)
- **Log Analytics**: Free tier (5GB/month)
- **Azure Files/Blob**: ~$1-5/month (depends on usage)
- **Total**: **~$30-50/month**

### With Database
- Add Azure SQL (S0): +$15/month
- Or Azure Redis (Basic C0): +$17/month

### With Application Insights
- Free tier: 5GB data/month (usually sufficient)
- Overage: $2.30 per GB

---

## Scaling Configuration

```powershell
# Auto-scale based on HTTP requests
az containerapp update `
  --name $BACKEND_APP `
  --resource-group $RESOURCE_GROUP `
  --min-replicas 1 `
  --max-replicas 5 `
  --scale-rule-name http-scale `
  --scale-rule-type http `
  --scale-rule-http-concurrency 100

# Auto-scale based on CPU
az containerapp update `
  --name $BACKEND_APP `
  --resource-group $RESOURCE_GROUP `
  --scale-rule-name cpu-scale `
  --scale-rule-type cpu `
  --scale-rule-metadata value=70  # Scale when CPU > 70%
```

---

## Troubleshooting

### View Container Logs
```powershell
az containerapp logs show --name $BACKEND_APP --resource-group $RESOURCE_GROUP --follow
```

### Restart Container
```powershell
az containerapp revision restart --name $BACKEND_APP --resource-group $RESOURCE_GROUP
```

### Check Revisions
```powershell
az containerapp revision list --name $BACKEND_APP --resource-group $RESOURCE_GROUP --output table
```

### Exec into Container (for debugging)
```powershell
az containerapp exec --name $BACKEND_APP --resource-group $RESOURCE_GROUP
```

### Common Issues

**1. "Failed to pull image" Error**
- Verify ACR credentials are correct
- Ensure image was pushed successfully: `az acr repository list --name $ACR_NAME`

**2. Backend can't connect to Discord/YouTube**
- Check secrets are set correctly: `az containerapp show --name $BACKEND_APP --resource-group $RESOURCE_GROUP --query properties.configuration.secrets`
- Verify environment variables reference secrets correctly

**3. Frontend can't reach backend**
- Verify BACKEND_URL points to internal FQDN
- Check backend ingress is set to `internal`
- Both apps must be in same Container Apps Environment

**4. File uploads failing**
- Check if storage mount is working: exec into container and verify /mnt/uploads exists
- Verify permissions on mounted storage
- Check container memory limits (2GB files need adequate RAM)

---

## Security Best Practices

1. **Use Managed Identity** (instead of ACR username/password):
   ```powershell
   az containerapp identity assign --name $BACKEND_APP --resource-group $RESOURCE_GROUP --system-assigned
   # Then configure ACR to allow managed identity access
   ```

2. **Restrict Backend Ingress**: Already set to `internal` (not publicly accessible)

3. **Use Key Vault for Secrets** (optional, for extra security):
   ```powershell
   # Create Key Vault
   az keyvault create --name ddclipbot-kv --resource-group $RESOURCE_GROUP
   
   # Store secrets
   az keyvault secret set --vault-name ddclipbot-kv --name discord-token --value "YOUR_TOKEN"
   
   # Reference in Container App
   az containerapp update --name $BACKEND_APP --resource-group $RESOURCE_GROUP \
     --secrets discord-token=keyvaultref:https://ddclipbot-kv.vault.azure.net/secrets/discord-token,identityref:/subscriptions/.../managedIdentities/...
   ```

4. **Enable HTTPS**: Automatic with Container Apps (managed certificates)

5. **Rate Limiting**: Add middleware in backend to prevent abuse

---

## Next Steps

1. ✅ Create Dockerfiles for backend and frontend
2. ✅ Test Docker builds locally
3. ✅ Create Azure resources (ACR, Container Apps Environment)
4. ✅ Push images to ACR
5. ✅ Deploy backend and frontend containers
6. ✅ Configure persistent storage
7. ✅ Set up database (Redis or SQL)
8. ✅ Enable Application Insights
9. ✅ Configure custom domain (optional)
10. ✅ Set up CI/CD pipeline

---

## Quick Reference Commands

```powershell
# Deploy latest changes
.\deploy.ps1 -ResourceGroup "ddclipbot-rg" -AcrName "ddclipbotacr"

# View logs
az containerapp logs show --name ddclipbot-backend --resource-group ddclipbot-rg --follow

# Scale up/down
az containerapp update --name ddclipbot-backend --resource-group ddclipbot-rg --min-replicas 2 --max-replicas 10

# Update secrets
az containerapp update --name ddclipbot-backend --resource-group ddclipbot-rg --secrets new-secret="value"

# Check health
curl https://YOUR-BACKEND-URL/health
```
