# Use official .NET image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set work directory
WORKDIR /app

# Copy project files
COPY . ./

# Restore dependencies
RUN dotnet restore

# Build and publish
RUN dotnet publish -c Release -o out

# Use runtime-only image for smaller size
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out ./

# Run the bot
CMD ["dotnet", "TgBotGregory.dll"]
