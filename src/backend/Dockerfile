# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy csproj first for layer caching
COPY src/backend/OpenStickyMemos.Api/OpenStickyMemos.Api.csproj src/backend/OpenStickyMemos.Api/
RUN dotnet restore src/backend/OpenStickyMemos.Api/OpenStickyMemos.Api.csproj

# Copy remaining source files and publish
COPY src/backend/OpenStickyMemos.Api/ src/backend/OpenStickyMemos.Api/
RUN dotnet publish src/backend/OpenStickyMemos.Api/OpenStickyMemos.Api.csproj -c Release -o /app --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
EXPOSE 5000
COPY --from=build /app .
ENTRYPOINT ["dotnet", "OpenStickyMemos.Api.dll"]
