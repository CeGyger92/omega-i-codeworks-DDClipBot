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
