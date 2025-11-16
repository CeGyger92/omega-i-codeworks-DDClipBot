# DDClipBot - Azure Deployment Reference

**Deployment Date**: October 28, 2025  
**Status**: ✅ Successfully Deployed (Phase 8 Complete)

---

## 🔑 Critical Information

### Azure Resources Created

| Resource | Name | Type | Purpose |
|----------|------|------|---------|
| Resource Group | `ddclipbot-rg` | Resource Group | Contains all resources |
| Container Registry | `ddclipbotacr` | Azure Container Registry | Stores Docker images |
| Log Analytics | `ddclipbot-logs` | Log Analytics Workspace | Centralized logging |
| Container Environment | `ddclipbot-env` | Container Apps Environment | Hosts both apps |
| Backend App | `ddclipbot-backend` | Container App | .NET API (Internal) |
| Frontend App | `ddclipbot-frontend` | Container App | Next.js UI (Public) |

### 🌐 URLs and Endpoints

```
Frontend (Public):
https://ddclipbot-frontend.agreeableriver-6efb45ef.eastus.azurecontainerapps.io

Backend (Internal Only):
https://ddclipbot-backend.internal.agreeableriver-6efb45ef.eastus.azurecontainerapps.io

Container Registry:
ddclipbotacr.azurecr.io

Environment Domain:
agreeableriver-6efb45ef.eastus.azurecontainerapps.io
```

### 📊 Resource Configuration

#### Backend Container App
- **CPU**: 1.0 cores
- **Memory**: 2 GB
- **Replicas**: 1-3 (auto-scale)
- **Port**: 5000
- **Ingress**: Internal only (not publicly accessible)
- **Image**: `ddclipbotacr.azurecr.io/ddclipbot-backend:latest`

#### Frontend Container App
- **CPU**: 0.5 cores
- **Memory**: 1 GB
- **Replicas**: 1-2 (auto-scale)
- **Port**: 3000
- **Ingress**: External (public HTTPS)
- **Image**: `ddclipbotacr.azurecr.io/ddclipbot-frontend:latest`

### 🔐 Secrets Stored in Azure

The following secrets are stored in Azure Container Apps (encrypted):

- `discord-token` - Discord bot token for Gateway API
- `discord-client-secret` - Discord OAuth client secret
- `youtube-client-id` - YouTube API OAuth client ID
- `youtube-client-secret` - YouTube API OAuth client secret
- `youtube-refresh-token` - YouTube API refresh token
- `ddclipbotacrazurecrio-ddclipbotacr` - ACR registry password (auto-generated)

### 🔧 Environment Variables

#### Backend
```
ASPNETCORE_ENVIRONMENT=Production
Discord__ClientId=1430631789776601199
Discord__RedirectUri=https://ddclipbot-frontend.agreeableriver-6efb45ef.eastus.azurecontainerapps.io/login
Discord__GuildId=277523569708105729
Discord__Token=secretref:discord-token
Discord__ClientSecret=secretref:discord-client-secret
YouTube__ClientId=secretref:youtube-client-id
YouTube__ClientSecret=secretref:youtube-client-secret
YouTube__RefreshToken=secretref:youtube-refresh-token
YouTube__ApplicationName=DDClipBot
Upload__MaxFileSizeBytes=2147483648
```

#### Frontend
```
NODE_ENV=production
BACKEND_URL=https://ddclipbot-backend.internal.agreeableriver-6efb45ef.eastus.azurecontainerapps.io
NEXT_PUBLIC_API_URL=https://ddclipbot-backend.internal.agreeableriver-6efb45ef.eastus.azurecontainerapps.io
NEXT_PUBLIC_DISCORD_CLIENT_ID=1430631789776601199
```

**Note**: The Discord Client ID is exposed to the browser (NEXT_PUBLIC_) because it's used for OAuth redirect URLs. This is safe - it's a public identifier, not a secret.

---

## 🚀 Quick Deployment Commands

### PowerShell Variables (Set these first)
```powershell
$RESOURCE_GROUP = "ddclipbot-rg"
$LOCATION = "eastus"
$ACR_NAME = "ddclipbotacr"
$CONTAINERAPPS_ENV = "ddclipbot-env"
$BACKEND_APP = "ddclipbot-backend"
$FRONTEND_APP = "ddclipbot-frontend"
$ACR_SERVER = "$ACR_NAME.azurecr.io"
```

### 🚀 Easy Update Script (Recommended)

Use the automated update script to rebuild and redeploy:

```powershell
# Update both frontend and backend
.\update-azure.ps1

# Update only backend
.\update-azure.ps1 -BackendOnly

# Update only frontend
.\update-azure.ps1 -FrontendOnly

# Skip build and just update running containers (if images already pushed)
.\update-azure.ps1 -SkipBuild
```

The script automatically:
- Loads environment variables from `.env`
- Gets current frontend URL for backend configuration
- Builds Docker images with correct build arguments
- Pushes to ACR
- Updates Container Apps with proper environment variables
- Displays the Discord OAuth redirect URI to configure

### Manual Build and Push Commands
### Manual Build and Push Commands
```powershell
# Login to ACR
az acr login --name $ACR_NAME

# Get backend URL for frontend build
$BACKEND_FQDN = az containerapp show `
  --name $BACKEND_APP `
  --resource-group $RESOURCE_GROUP `
  --query properties.configuration.ingress.fqdn `
  --output tsv

# Build and push backend
cd src\DDClipBot.Host
docker build -t "$ACR_NAME.azurecr.io/ddclipbot-backend:latest" .
docker push "$ACR_NAME.azurecr.io/ddclipbot-backend:latest"

# Build and push frontend (with proper build args)
cd ..\ddclipbot-frontend
docker build `
  -t "$ACR_NAME.azurecr.io/ddclipbot-frontend:latest" `
  --build-arg BACKEND_URL="https://$BACKEND_FQDN" `
  --build-arg NEXT_PUBLIC_DISCORD_CLIENT_ID="1430631789776601199" `
  .
docker push "$ACR_NAME.azurecr.io/ddclipbot-frontend:latest"
```

### Manual Update Commands
### Manual Update Commands
```powershell
# Get frontend URL for backend redirect URI
$FRONTEND_URL = az containerapp show `
  --name $FRONTEND_APP `
  --resource-group $RESOURCE_GROUP `
  --query properties.configuration.ingress.fqdn `
  --output tsv

# Update backend with new image and redirect URI
az containerapp update `
  --name $BACKEND_APP `
  --resource-group $RESOURCE_GROUP `
  --image "$ACR_SERVER/ddclipbot-backend:latest" `
  --set-env-vars "Discord__RedirectUri=https://$FRONTEND_URL/login"

# Get backend URL for frontend
$BACKEND_FQDN = az containerapp show `
  --name $BACKEND_APP `
  --resource-group $RESOURCE_GROUP `
  --query properties.configuration.ingress.fqdn `
  --output tsv

# Update frontend with new image and environment variables
az containerapp update `
  --name $FRONTEND_APP `
  --resource-group $RESOURCE_GROUP `
  --image "$ACR_SERVER/ddclipbot-frontend:latest" `
  --set-env-vars `
    "BACKEND_URL=https://$BACKEND_FQDN" `
    "NEXT_PUBLIC_API_URL=https://$BACKEND_FQDN" `
    "NEXT_PUBLIC_DISCORD_CLIENT_ID=1430631789776601199"
```

---

## 📝 Regular Maintenance Tasks

### 1. Update Application Code

After making code changes:

```powershell
# 1. Build new images locally (test with docker-compose first!)
docker-compose up --build

# 2. Push to ACR
az acr login --name ddclipbotacr
cd src\DDClipBot.Host
docker build -t ddclipbotacr.azurecr.io/ddclipbot-backend:latest .
docker push ddclipbotacr.azurecr.io/ddclipbot-backend:latest

cd ..\ddclipbot-frontend
docker build -t ddclipbotacr.azurecr.io/ddclipbot-frontend:latest .
docker push ddclipbotacr.azurecr.io/ddclipbot-frontend:latest

# 3. Update Container Apps
az containerapp update --name ddclipbot-backend --resource-group ddclipbot-rg --image ddclipbotacr.azurecr.io/ddclipbot-backend:latest
az containerapp update --name ddclipbot-frontend --resource-group ddclipbot-rg --image ddclipbotacr.azurecr.io/ddclipbot-frontend:latest
```

### 2. Update Secrets

```powershell
# Update a specific secret
az containerapp update `
  --name ddclipbot-backend `
  --resource-group ddclipbot-rg `
  --secrets youtube-refresh-token="NEW_TOKEN_HERE"

# Update multiple secrets
az containerapp update `
  --name ddclipbot-backend `
  --resource-group ddclipbot-rg `
  --secrets `
    discord-token="NEW_DISCORD_TOKEN" `
    youtube-refresh-token="NEW_YOUTUBE_TOKEN"
```

### 3. View Logs

```powershell
# Backend logs (live stream)
az containerapp logs show `
  --name ddclipbot-backend `
  --resource-group ddclipbot-rg `
  --follow

# Frontend logs (live stream)
az containerapp logs show `
  --name ddclipbot-frontend `
  --resource-group ddclipbot-rg `
  --follow

# Get recent logs (no streaming)
az containerapp logs show `
  --name ddclipbot-backend `
  --resource-group ddclipbot-rg `
  --tail 100
```

### 4. Scale Applications

```powershell
# Manually scale backend
az containerapp update `
  --name ddclipbot-backend `
  --resource-group ddclipbot-rg `
  --min-replicas 2 `
  --max-replicas 5

# Manually scale frontend
az containerapp update `
  --name ddclipbot-frontend `
  --resource-group ddclipbot-rg `
  --min-replicas 1 `
  --max-replicas 3
```

### 5. Restart Containers

```powershell
# Restart backend
az containerapp revision restart `
  --name ddclipbot-backend `
  --resource-group ddclipbot-rg

# Restart frontend
az containerapp revision restart `
  --name ddclipbot-frontend `
  --resource-group ddclipbot-rg
```

### 6. Check Container Status

```powershell
# Get backend status
az containerapp show `
  --name ddclipbot-backend `
  --resource-group ddclipbot-rg `
  --query properties.runningStatus

# Get frontend status
az containerapp show `
  --name ddclipbot-frontend `
  --resource-group ddclipbot-rg `
  --query properties.runningStatus

# List all revisions
az containerapp revision list `
  --name ddclipbot-backend `
  --resource-group ddclipbot-rg `
  --output table
```

---

## ⚠️ Important Notes for Future

### YouTube Token Refresh
- The `YouTubeTokenRefreshService` runs every 24 hours to refresh access tokens
- If Google returns a new refresh token, check logs at `/tmp/youtube-refresh-token.txt` in the container
- Update the secret with: `az containerapp update --name ddclipbot-backend --resource-group ddclipbot-rg --secrets youtube-refresh-token="NEW_TOKEN"`
- **Remember**: You need to **publish your Google OAuth app** to production (not testing mode) to prevent 7-day token expiration

### Discord OAuth Configuration
- **Redirect URI** is currently set to: `https://ddclipbot-frontend.agreeableriver-6efb45ef.eastus.azurecontainerapps.io/login`
- If you change this URL (custom domain, etc.), update:
  1. Discord Developer Portal OAuth2 settings
  2. Backend environment variable: `Discord__RedirectUri`

### File Storage
- **Current**: Using ephemeral storage (files deleted on container restart)
- **Recommendation**: Set up Azure Blob Storage for persistent uploads (see Phase 10 in deployment guide)
- **Note**: 2GB file upload limit configured in `Upload__MaxFileSizeBytes`

### Session/Job Storage
- **Current**: In-memory storage (lost on container restart)
- **Recommendation**: Implement Azure Redis Cache or Azure SQL Database (see Phase 11 in deployment guide)
- **Impact**: Users will need to re-login after container restarts

### CORS Configuration
- **Current**: No CORS configured (same origin - frontend calls backend internally)
- **If needed**: Add CORS in `Program.cs` for direct API access from other domains

### Cost Monitoring
Current configuration costs approximately:
- Container Apps: ~$25-35/month (consumption-based)
- Container Registry: ~$5/month (Basic tier)
- Log Analytics: Free tier (5GB/month included)
- **Total**: ~$30-40/month

Monitor actual costs in Azure Portal → Cost Management

---

## 🔍 Troubleshooting

### Container Won't Start
```powershell
# Check logs for errors
az containerapp logs show --name ddclipbot-backend --resource-group ddclipbot-rg --tail 200

# Check if secrets are properly set
az containerapp show --name ddclipbot-backend --resource-group ddclipbot-rg --query properties.configuration.secrets

# Verify image exists in ACR
az acr repository show --name ddclipbotacr --image ddclipbot-backend:latest
```

### Frontend Can't Reach Backend
```powershell
# Verify backend FQDN is correct in frontend env vars
az containerapp show --name ddclipbot-frontend --resource-group ddclipbot-rg --query properties.template.containers[0].env

# Check backend is running
az containerapp show --name ddclipbot-backend --resource-group ddclipbot-rg --query properties.runningStatus

# Verify backend is internal ingress
az containerapp show --name ddclipbot-backend --resource-group ddclipbot-rg --query properties.configuration.ingress.external
# Should return: false
```

### Discord OAuth Failing
```powershell
# Verify redirect URI matches Discord Developer Portal
az containerapp show --name ddclipbot-backend --resource-group ddclipbot-rg --query "properties.template.containers[0].env[?name=='Discord__RedirectUri']"

# Check Discord secrets are set
az containerapp show --name ddclipbot-backend --resource-group ddclipbot-rg --query "properties.template.containers[0].env[?name=='Discord__Token']"
```

### YouTube Upload Failing
```powershell
# Check YouTube secrets
az containerapp show --name ddclipbot-backend --resource-group ddclipbot-rg --query "properties.template.containers[0].env[?name=='YouTube__RefreshToken']"

# View backend logs for YouTube errors
az containerapp logs show --name ddclipbot-backend --resource-group ddclipbot-rg --follow | Select-String "YouTube"
```

### High Memory/CPU Usage
```powershell
# Check current resource usage
az monitor metrics list `
  --resource /subscriptions/7a559bd8-5d43-40e3-995b-37325f6f51fb/resourceGroups/ddclipbot-rg/providers/Microsoft.App/containerApps/ddclipbot-backend `
  --metric "CpuUsage" `
  --start-time (Get-Date).AddHours(-1) `
  --end-time (Get-Date) `
  --interval PT1M

# Increase resources if needed
az containerapp update `
  --name ddclipbot-backend `
  --resource-group ddclipbot-rg `
  --cpu 2.0 `
  --memory 4.0Gi
```

---

## 🎯 Next Steps (Phase 9+)

### Recommended Improvements

1. **Discord OAuth Configuration** (Phase 9)
   - Update Discord Developer Portal with production redirect URI
   - Verify: `https://ddclipbot-frontend.agreeableriver-6efb45ef.eastus.azurecontainerapps.io/login`

2. **Google OAuth Configuration** (Phase 9)
   - Add `azurecontainerapps.io` to authorized domains
   - **Publish app to production** (prevents 7-day token expiration)
   - Add production redirect URI if needed

3. **Persistent Storage** (Phase 10)
   - Set up Azure Blob Storage for video uploads
   - Configure connection string in secrets
   - Update `VideoUploadWorker.cs` to use Blob SDK

4. **Database Setup** (Phase 11)
   - Choose: Azure Redis (faster) or Azure SQL (more features)
   - Update `SessionStore.cs` and `JobStore.cs`
   - Add connection string to secrets

5. **Application Insights** (Phase 12)
   - Create Application Insights resource
   - Add instrumentation key to backend
   - Monitor performance, errors, and usage

6. **Health Checks** (Phase 16)
   - Add `/health` endpoint to backend
   - Configure Container Apps health probes
   - Monitor Discord/YouTube connectivity

7. **Rate Limiting**
   - Install `AspNetCoreRateLimit` package
   - Configure limits (e.g., 10 uploads/hour per user)
   - Protect against abuse

8. **Custom Domain** (Phase 13)
   - Register a domain (e.g., `ddclipbot.yourdomain.com`)
   - Configure DNS CNAME to Container Apps
   - Enable managed SSL certificate

---

## 📚 Useful Azure CLI Commands

```powershell
# List all Container Apps
az containerapp list --resource-group ddclipbot-rg --output table

# Get all environment variables for backend
az containerapp show --name ddclipbot-backend --resource-group ddclipbot-rg --query properties.template.containers[0].env

# List all secrets (names only, not values)
az containerapp secret list --name ddclipbot-backend --resource-group ddclipbot-rg --output table

# Get ACR repositories
az acr repository list --name ddclipbotacr --output table

# View all resources in resource group
az resource list --resource-group ddclipbot-rg --output table

# Delete everything (CAUTION!)
# az group delete --name ddclipbot-rg --yes
```

---

## 📞 Support Resources

- **Azure Container Apps Docs**: https://learn.microsoft.com/en-us/azure/container-apps/
- **Azure CLI Reference**: https://learn.microsoft.com/en-us/cli/azure/containerapp
- **Discord Developer Portal**: https://discord.com/developers/applications/1430631789776601199
- **Google Cloud Console**: https://console.cloud.google.com/
- **YouTube API Quotas**: https://console.cloud.google.com/apis/api/youtube.googleapis.com/quotas

---

## ✅ Deployment Checklist

- [x] Phase 1: Dockerfiles created
- [x] Phase 2: Local Docker testing complete
- [x] Phase 3: Azure Container Registry created
- [x] Phase 4: Images pushed to ACR
- [x] Phase 5: Container Apps Environment created
- [x] Phase 6: Backend Container App deployed
- [x] Phase 7: Frontend Container App deployed
- [x] Phase 8: Backend updated with frontend URL
- [ ] Phase 9: Discord & Google OAuth configured for production
- [ ] Phase 10: Persistent storage configured
- [ ] Phase 11: Database setup
- [ ] Phase 12: Application Insights enabled
- [ ] Phase 13: Custom domain (optional)
- [ ] Phase 14: Automated deployment script
- [ ] Phase 15: CI/CD pipeline (optional)
- [ ] Phase 16: Health checks and monitoring

**Current Status**: ✅ **Phase 8 Complete - Application Deployed and Running!**

---

**Last Updated**: October 28, 2025  
**Deployed By**: charles.gyger@marcumllp.com  
**Subscription**: 7a559bd8-5d43-40e3-995b-37325f6f51fb
