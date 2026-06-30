# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0.0-bookworm-slim-amd64 AS build
WORKDIR /src

# Copy csproj and restore (caching layer)
COPY src/backend/OpenStickyMemos.Api/*.csproj ./OpenStickyMemos.Api/
RUN dotnet restore OpenStickyMemos.Api/OpenStickyMemos.Api.csproj

# Copy everything and publish
COPY . .
RUN dotnet publish OpenStickyMemos.Api/OpenStickyMemos.Api.csproj -c Release -o /app --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0.0-bookworm-slim-amd64 AS runtime
WORKDIR /app
EXPOSE 5000
COPY --from=build /app .
ENTRYPOINT ["dotnet", "OpenStickyMemos.Api.dll"]
