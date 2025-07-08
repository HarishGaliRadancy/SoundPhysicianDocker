# 1. Base runtime for final image (small)
FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

# Create output directory to avoid runtime errors
RUN mkdir -p /app/output

# 2. Build environment (SDK image)
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["SoundPhysicianDocker.csproj", "."]
RUN dotnet restore "./SoundPhysicianDocker.csproj"

# 3. Copy and build your app
COPY . .
WORKDIR "/src/."
RUN dotnet build "SoundPhysicianDocker.csproj" -c Release -o /app/build

# 4. Publish the app (optimized build)
FROM build AS publish
RUN dotnet publish "SoundPhysicianDocker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 5. Final image to run the app
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SoundPhysicianDocker.dll"]
