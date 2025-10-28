# Backend Configuration Guide

## Required User Secrets

The backend requires several secrets to be configured using `dotnet user-secrets`. Navigate to `src/DDClipBot.Host/` and run:

### Discord Configuration

```powershell
# Discord Bot Token
dotnet user-secrets set "Discord:Token" "YOUR_DISCORD_BOT_TOKEN"

# Discord OAuth Client Secret
dotnet user-secrets set "Discord:ClientSecret" "YOUR_DISCORD_CLIENT_SECRET"
```

### YouTube API Configuration

To upload videos to YouTube, you need OAuth credentials:

```powershell
# YouTube OAuth Client ID
dotnet user-secrets set "YouTube:ClientId" "YOUR_YOUTUBE_CLIENT_ID"

# YouTube OAuth Client Secret
dotnet user-secrets set "YouTube:ClientSecret" "YOUR_YOUTUBE_CLIENT_SECRET"

# YouTube Refresh Token (from initial OAuth flow)
dotnet user-secrets set "YouTube:RefreshToken" "YOUR_YOUTUBE_REFRESH_TOKEN"
```

#### How to Get YouTube OAuth Credentials

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select existing
3. Enable the **YouTube Data API v3**
4. Go to **Credentials** → **Create Credentials** → **OAuth 2.0 Client ID**
5. Set application type to **Desktop App** or **Web Application**
6. Download the client secret JSON file
7. Extract `client_id` and `client_secret`

#### How to Get YouTube Refresh Token

You need to perform an initial OAuth flow to get a refresh token:

1. Use the OAuth 2.0 Playground: https://developers.google.com/oauthplayground/
2. In settings (gear icon), check "Use your own OAuth credentials"
3. Enter your Client ID and Client Secret
4. In Step 1, select **YouTube Data API v3** → `https://www.googleapis.com/auth/youtube.upload`
5. Click "Authorize APIs" and sign in with the Google account that owns the YouTube channel
6. In Step 2, click "Exchange authorization code for tokens"
7. Copy the `refresh_token` value

### Discord Channel Configuration (Optional)

~~Map friendly channel names to Discord channel IDs in `appsettings.json`:~~ 

**UPDATE**: Channels are now fetched dynamically from the Discord API. You just need to configure your Discord Server (Guild) ID in `appsettings.json`:

```json
{
  "Discord": {
    "GuildId": "YOUR_DISCORD_SERVER_ID_HERE"
  }
}
```

To get your Discord Server ID:
1. Enable Developer Mode in Discord (User Settings → Advanced → Developer Mode)
2. Right-click your server name and select "Copy Server ID"

The backend will automatically fetch all text channels that the bot has access to via the `/api/discord/channels` endpoint.

### Upload Configuration (Optional)

Configure where temporary video files are stored in `appsettings.json`:

```json
{
  "Upload": {
    "TempPath": "C:\\temp\\ddclipbot-uploads"
  }
}
```

If not specified, defaults to system temp folder.

## Running the Backend

```powershell
cd src\DDClipBot.Host
dotnet run
```

The API will be available at `http://localhost:5000`

## API Endpoints

### Authentication
- `GET /api/auth/session` - Check if user is authenticated
- `POST /api/auth/discord/callback` - Discord OAuth callback

### Discord
- `GET /api/discord/channels` - Get list of available Discord text channels

### Video Upload
- `POST /api/videos/upload` - Upload a video (multipart/form-data)
  - **Authentication**: Required (session cookie)
  - **Fields**:
    - `title` (required) - Video title
    - `description` (optional) - Video description
    - `publishMessage` (optional) - Message to post with video in Discord
    - `targetChannel` (required) - Discord channel ID
    - `pingChannel` (boolean) - Whether to @here ping the channel
    - `video` (required) - Video file (max 500MB)
  - **Returns**: `{ jobId, status, message }`

## Background Worker

The `VideoUploadWorker` runs as a background service and:
1. Monitors for queued upload jobs
2. Uploads videos to YouTube using the configured OAuth credentials
3. Posts status updates to Discord channels
4. Cleans up temporary files after processing

## Architecture

- **`/Endpoints`** - API endpoint definitions
  - `AuthEndpoints.cs` - Authentication endpoints
  - `DiscordEndpoints.cs` - Discord data endpoints (channels list)
  - `UploadEndpoints.cs` - Video upload endpoints

- **`/Services`** - Business logic services
  - `SessionStore.cs` - In-memory user session management
  - `JobStore.cs` - In-memory upload job tracking
  - `YouTubeApiService.cs` - YouTube API integration
  - `VideoUploadWorker.cs` - Background job processor

- **`/Models`** - Data models
  - `UserSession.cs` - User session data
  - `VideoUploadJob.cs` - Upload job data and status

- **`/Configuration`** - Configuration models
  - `YouTubeOptions.cs` - YouTube API settings

## Notes

- Session store and job store are currently in-memory. For production, these should be backed by a database or Redis.
- File size limit is 500MB (configurable in `Program.cs`)
- Videos are uploaded as "unlisted" to YouTube by default
- Worker checks for jobs every 5 seconds
- Temp files are deleted after successful upload or failure
