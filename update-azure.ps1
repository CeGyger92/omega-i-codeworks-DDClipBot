# Azure Container Apps Update Script
# This script rebuilds and redeploys your containers to Azure

param(
    [switch]$SkipBuild,
    [switch]$BackendOnly,
    [switch]$FrontendOnly
)

# Configuration Variables
$RESOURCE_GROUP = "ddclipbot-rg"
$ACR_NAME = "ddclipbotacr"
$BACKEND_APP = "ddclipbot-backend"
$FRONTEND_APP = "ddclipbot-frontend"
$ACR_SERVER = "$ACR_NAME.azurecr.io"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  DDClipBot Azure Update Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get Frontend URL for backend configuration
Write-Host "Getting frontend URL..." -ForegroundColor Yellow
$FRONTEND_URL = az containerapp show `
  --name $FRONTEND_APP `
  --resource-group $RESOURCE_GROUP `
  --query properties.configuration.ingress.fqdn `
  --output tsv

if (-not $FRONTEND_URL) {
    Write-Host "ERROR: Could not get frontend URL" -ForegroundColor Red
    exit 1
}

Write-Host "Frontend URL: https://$FRONTEND_URL" -ForegroundColor Green
Write-Host ""

# Load environment variables from .env
if (Test-Path ".env") {
    Write-Host "Loading environment variables from .env..." -ForegroundColor Yellow
    Get-Content .env | ForEach-Object {
        if ($_ -match '^\s*([^#][^=]+)=(.*)$') {
            $name = $matches[1].Trim()
            $value = $matches[2].Trim()
            Set-Item -Path "env:$name" -Value $value
        }
    }
    Write-Host "Environment variables loaded" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "WARNING: .env file not found. Using existing values." -ForegroundColor Yellow
    Write-Host ""
}

# Login to ACR
if (-not $SkipBuild) {
    Write-Host "Logging in to Azure Container Registry..." -ForegroundColor Yellow
    az acr login --name $ACR_NAME
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to login to ACR" -ForegroundColor Red
        exit 1
    }
    Write-Host ""
}

# Build and Push Backend
if (-not $FrontendOnly) {
    if (-not $SkipBuild) {
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "Building Backend Image..." -ForegroundColor Cyan
        Write-Host "========================================" -ForegroundColor Cyan
        
        Set-Location src\DDClipBot.Host
        docker build -t "$ACR_SERVER/ddclipbot-backend:latest" .
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: Backend build failed" -ForegroundColor Red
            Set-Location ..\..
            exit 1
        }
        
        Write-Host ""
        Write-Host "Pushing Backend Image to ACR..." -ForegroundColor Yellow
        docker push "$ACR_SERVER/ddclipbot-backend:latest"
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: Backend push failed" -ForegroundColor Red
            Set-Location ..\..
            exit 1
        }
        
        Set-Location ..\..
        Write-Host "Backend image pushed successfully!" -ForegroundColor Green
        Write-Host ""
    }
    
    Write-Host "Updating Backend Container App..." -ForegroundColor Yellow
    az containerapp update `
      --name $BACKEND_APP `
      --resource-group $RESOURCE_GROUP `
      --image "$ACR_SERVER/ddclipbot-backend:latest" `
      --set-env-vars "Discord__RedirectUri=https://$FRONTEND_URL/login"
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Backend update failed" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Backend deployed successfully!" -ForegroundColor Green
    Write-Host ""
}

# Build and Push Frontend
if (-not $BackendOnly) {
    if (-not $SkipBuild) {
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "Building Frontend Image..." -ForegroundColor Cyan
        Write-Host "========================================" -ForegroundColor Cyan
        
        Set-Location src\ddclipbot-frontend
        
        # Get backend internal URL
        $BACKEND_FQDN = az containerapp show `
          --name $BACKEND_APP `
          --resource-group $RESOURCE_GROUP `
          --query properties.configuration.ingress.fqdn `
          --output tsv
        
        Write-Host "Backend FQDN: $BACKEND_FQDN" -ForegroundColor Cyan
        
        docker build `
          -t "$ACR_SERVER/ddclipbot-frontend:latest" `
          --build-arg BACKEND_URL="https://$BACKEND_FQDN" `
          --build-arg NEXT_PUBLIC_DISCORD_CLIENT_ID="$env:NEXT_PUBLIC_DISCORD_CLIENT_ID" `
          .
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: Frontend build failed" -ForegroundColor Red
            Set-Location ..\..
            exit 1
        }
        
        Write-Host ""
        Write-Host "Pushing Frontend Image to ACR..." -ForegroundColor Yellow
        docker push "$ACR_SERVER/ddclipbot-frontend:latest"
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: Frontend push failed" -ForegroundColor Red
            Set-Location ..\..
            exit 1
        }
        
        Set-Location ..\..
        Write-Host "Frontend image pushed successfully!" -ForegroundColor Green
        Write-Host ""
    }
    
    Write-Host "Updating Frontend Container App..." -ForegroundColor Yellow
    
    # Get backend internal URL
    $BACKEND_FQDN = az containerapp show `
      --name $BACKEND_APP `
      --resource-group $RESOURCE_GROUP `
      --query properties.configuration.ingress.fqdn `
      --output tsv
    
    az containerapp update `
      --name $FRONTEND_APP `
      --resource-group $RESOURCE_GROUP `
      --image "$ACR_SERVER/ddclipbot-frontend:latest" `
      --set-env-vars `
        "BACKEND_URL=https://$BACKEND_FQDN" `
        "NEXT_PUBLIC_API_URL=https://$BACKEND_FQDN" `
        "NEXT_PUBLIC_DISCORD_CLIENT_ID=$env:NEXT_PUBLIC_DISCORD_CLIENT_ID"
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Frontend update failed" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Frontend deployed successfully!" -ForegroundColor Green
    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Green
Write-Host "  Deployment Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Frontend URL: https://$FRONTEND_URL" -ForegroundColor Cyan
Write-Host ""
Write-Host "IMPORTANT: Update Discord Developer Portal OAuth2 Redirect URI:" -ForegroundColor Yellow
Write-Host "  https://$FRONTEND_URL/login" -ForegroundColor White
Write-Host ""
