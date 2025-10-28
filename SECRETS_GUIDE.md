# Secrets Configuration Guide

## How Secrets Work in Docker vs Azure

### Local Development (Docker/Docker Compose)

Secrets are passed as **environment variables** to containers.

#### Method 1: .env File (Recommended)

1. Create `.env` in your repo root
2. Add to `.gitignore` (already exists in most projects)
3. Docker Compose reads it automatically

**Create `.env`:**
```bash
# Discord Secrets
DISCORD_TOKEN=MTMxMjU5NzYxOTczNjc5NzI4NA.GqK3gH.your_actual_token_here
DISCORD_CLIENT_ID=1312597619736797284
DISCORD_CLIENT_SECRET=your_client_secret_here

# YouTube Secrets
YOUTUBE_CLIENT_ID=123456789-abcdefghijklmnop.apps.googleusercontent.com
YOUTUBE_CLIENT_SECRET=GOCSPX-your_client_secret
YOUTUBE_REFRESH_TOKEN=1//your_refresh_token
```

**Run Docker Compose:**
```powershell
# Automatically reads .env file
docker-compose up
```

#### Method 2: PowerShell Environment Variables

```powershell
# Set in current PowerShell session
$env:DISCORD_TOKEN = "your_token"
$env:DISCORD_CLIENT_SECRET = "your_secret"
$env:YOUTUBE_CLIENT_ID = "your_id"
$env:YOUTUBE_CLIENT_SECRET = "your_secret"
$env:YOUTUBE_REFRESH_TOKEN = "your_refresh_token"

# Then run docker-compose
docker-compose up
```

#### Method 3: Direct Command Line

```powershell
docker run `
  -e "Discord__Token=your_token" `
  -e "Discord__ClientId=your_client_id" `
  -e "Discord__ClientSecret=your_secret" `
  -e "YouTube__ClientId=your_youtube_id" `
  -e "YouTube__ClientSecret=your_youtube_secret" `
  -e "YouTube__RefreshToken=your_refresh_token" `
  -p 5000:5000 `
  ddclipbot-backend:latest
```

---

### Azure Container Apps (Production)

Secrets are stored **encrypted** in Azure and injected as environment variables at runtime.

#### Initial Setup (Phase 6)

```powershell
# Get your secrets ready (see below for where to find them)
$DISCORD_TOKEN = "MTMxMjU5NzYxOTczNjc5NzI4NA.GqK3gH.your_actual_token"
$DISCORD_CLIENT_ID = "1312597619736797284"
$DISCORD_CLIENT_SECRET = "your_client_secret"
$YOUTUBE_CLIENT_ID = "123456789-abcdefg.apps.googleusercontent.com"
$YOUTUBE_CLIENT_SECRET = "GOCSPX-your_secret"
$YOUTUBE_REFRESH_TOKEN = "1//04M2rLfEOqSa8..."

# Create container app with secrets
az containerapp create `
  --name ddclipbot-backend `
  --resource-group ddclipbot-rg `
  --environment ddclipbot-env `
  --image ddclipbotacr.azurecr.io/ddclipbot-backend:latest `
  --secrets `
    discord-token="$DISCORD_TOKEN" `
    discord-client-secret="$DISCORD_CLIENT_SECRET" `
    youtube-client-id="$YOUTUBE_CLIENT_ID" `
    youtube-client-secret="$YOUTUBE_CLIENT_SECRET" `
    youtube-refresh-token="$YOUTUBE_REFRESH_TOKEN" `
  --env-vars `
    "Discord__Token=secretref:discord-token" `
    "Discord__ClientId=$DISCORD_CLIENT_ID" `
    "Discord__ClientSecret=secretref:discord-client-secret" `
    "YouTube__ClientId=secretref:youtube-client-id" `
    "YouTube__ClientSecret=secretref:youtube-client-secret" `
    "YouTube__RefreshToken=secretref:youtube-refresh-token" `
    "YouTube__ApplicationName=DDClipBot" `
  --target-port 5000 `
  --ingress internal
```

**How it works:**
- `--secrets` → Stores encrypted in Azure (never visible in portal)
- `secretref:secret-name` → References the encrypted secret
- Plain values like `Discord__ClientId` → Not sensitive, stored as regular env vars

#### Updating Secrets

```powershell
# Update YouTube refresh token (e.g., after token refresh service logs a new one)
az containerapp update `
  --name ddclipbot-backend `
  --resource-group ddclipbot-rg `
  --secrets youtube-refresh-token="NEW_TOKEN_HERE"

# Update multiple secrets at once
az containerapp update `
  --name ddclipbot-backend `
  --resource-group ddclipbot-rg `
  --secrets `
    discord-token="new_token" `
    youtube-refresh-token="new_token"
```

#### Viewing Secrets (Azure Portal)

**You CANNOT view secret values** (security feature), but you can:
1. Go to Azure Portal → Container App
2. Click **Secrets** in left menu
3. See secret **names** (not values)
4. Click "Add" to add new secrets
5. Click secret name to update (must provide new value)

---

## Where to Get Each Secret

### Discord Secrets

#### 1. DISCORD_TOKEN (Bot Token)
1. Go to [Discord Developer Portal](https://discord.com/developers/applications)
2. Select your application
3. Go to **Bot** tab
4. Click **Reset Token** (if needed) or **Copy**
5. Format: `MTMxMjU5NzYxOTczNjc5NzI4NA.GqK3gH.long_random_string`

#### 2. DISCORD_CLIENT_ID
1. Discord Developer Portal → Your application
2. Go to **General Information** tab
3. Copy **Application ID**
4. Format: `1312597619736797284` (numeric)

#### 3. DISCORD_CLIENT_SECRET
1. Discord Developer Portal → Your application
2. Go to **OAuth2** tab
3. Under **Client Secret**, click **Reset Secret** (if needed) or **Copy**
4. Format: `random_string_32_chars`

**Current values from your project:**
- Client ID: `1312597619736797284` (from appsettings.json)
- Redirect URI: `http://localhost:3000/login` (update for production)
- Guild ID: `277523569708105729` (your Discord server)

---

### YouTube Secrets

#### 1. YOUTUBE_CLIENT_ID
1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Select your project
3. Go to **APIs & Services** → **Credentials**
4. Find your OAuth 2.0 Client ID
5. Copy the **Client ID**
6. Format: `123456789-abc123def456ghi789jkl.apps.googleusercontent.com`

#### 2. YOUTUBE_CLIENT_SECRET
1. Same location as Client ID
2. Click on the OAuth 2.0 Client ID name
3. Copy **Client secret**
4. Format: `GOCSPX-random_string`

#### 3. YOUTUBE_REFRESH_TOKEN
**You already have this!** It's currently in your user secrets.

To retrieve it:
```powershell
cd src\DDClipBot.Host
dotnet user-secrets list
```

Output will show:
```
YouTube:RefreshToken = 1//your_refresh_token
```

**Or from terminal history** (I can see you set it):
```
1//your_refresh_token
```

**Important:** Make sure your Google OAuth app is **published to production** (not Testing mode) to prevent the 7-day expiration!

---

## Security Best Practices

### For .env File (Local)

Add to `.gitignore`:
```gitignore
# Secrets
.env
.env.local
.env.*.local
```

**NEVER commit .env to Git!**

### For Azure

- ✅ Use `--secrets` with `secretref:` for sensitive data
- ✅ Use plain `--env-vars` for non-sensitive config (URLs, IDs, etc.)
- ✅ Secrets are encrypted at rest in Azure
- ✅ Secrets never appear in logs or portal UI
- ❌ Don't use plain env vars for passwords/tokens

### Example Separation

**Secrets (use secretref):**
- Discord Bot Token
- Discord Client Secret  
- YouTube Client Secret
- YouTube Refresh Token
- Database passwords
- API keys

**Regular Env Vars (plain values):**
- Discord Client ID (public in OAuth flow anyway)
- Discord Guild ID (server ID)
- Frontend URLs
- Feature flags
- Port numbers

---

## Testing Secret Injection

### Local Docker Test

```powershell
# Build backend
cd src\DDClipBot.Host
docker build -t ddclipbot-backend:latest .

# Run with secrets from .env file in parent directory
cd ..\..
docker-compose up backend

# Or run manually with explicit secrets
docker run `
  -e "Discord__Token=test_token" `
  -e "ASPNETCORE_ENVIRONMENT=Development" `
  -p 5000:5000 `
  ddclipbot-backend:latest

# Check logs to verify secrets are loaded
# Should see: "Discord Gateway is ready" (means token worked)
```

### Verify Azure Secrets

```powershell
# List all secrets (names only, not values)
az containerapp secret list `
  --name ddclipbot-backend `
  --resource-group ddclipbot-rg `
  --output table

# Check environment variables (shows secretref, not actual values)
az containerapp show `
  --name ddclipbot-backend `
  --resource-group ddclipbot-rg `
  --query properties.template.containers[0].env

# View logs to verify app can use secrets
az containerapp logs show `
  --name ddclipbot-backend `
  --resource-group ddclipbot-rg `
  --follow
```

---

## Quick Reference Commands

### Get All Your Current Secrets

```powershell
# From user secrets
cd src\DDClipBot.Host
dotnet user-secrets list

# Discord values from appsettings.json
cat appsettings.json | Select-String "Discord"

# From PowerShell environment (if set)
Get-ChildItem Env: | Where-Object { $_.Name -like "*DISCORD*" -or $_.Name -like "*YOUTUBE*" }
```

### Set Up .env File for Docker

```powershell
# Create .env from user secrets
cd src\DDClipBot.Host
$secrets = dotnet user-secrets list

# Manually create .env in repo root with format:
# DISCORD_TOKEN=value
# YOUTUBE_CLIENT_ID=value
# etc.
```

### Prepare for Azure Deployment

```powershell
# Store secrets in PowerShell variables (easier to reference in az commands)
$DISCORD_TOKEN = "your_token"
$DISCORD_CLIENT_SECRET = "your_secret"
$YOUTUBE_CLIENT_ID = "your_id"
$YOUTUBE_CLIENT_SECRET = "your_secret"  
$YOUTUBE_REFRESH_TOKEN = "your_refresh_token"

# Use in az containerapp create (see Phase 6)
```

---

## Common Issues

**1. Docker container can't connect to Discord/YouTube**
- Check: Are secrets actually being passed? Look at docker-compose.yml
- Check: .env file exists and has correct format (`KEY=value`, no quotes)
- Test: Add `echo $Discord__Token` in Dockerfile CMD to verify (remove after testing!)

**2. Azure container failing with auth errors**
- Check: Secrets reference correct names (`secretref:discord-token` not `secretref:Discord__Token`)
- Check: Secret names use lowercase with hyphens, env vars use PascalCase with `__`
- Verify: `az containerapp secret list` shows all required secrets

**3. .env file not being read**
- Check: File is named exactly `.env` (not `.env.txt`)
- Check: File is in same directory as `docker-compose.yml`
- Check: No quotes around values unless value contains spaces

---

## Next Steps

Before running Phase 2, you need to:

1. ✅ Create `.env` file in repo root with all your secrets
2. ✅ Verify you can retrieve all secrets from `dotnet user-secrets list`
3. ✅ Optionally set PowerShell variables for Azure deployment later

Would you like me to help you:
- Create a `.env.example` template file?
- Retrieve your current secrets from user-secrets?
- Test the Docker build with secrets?
