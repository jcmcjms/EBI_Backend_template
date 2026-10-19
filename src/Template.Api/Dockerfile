# Multi-stage build for Template.Api. Context is the repository root:
#   docker build -f src/Template.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props global.json ./
COPY src/Template.Api/Template.Api.csproj src/Template.Api/
RUN dotnet restore src/Template.Api/Template.Api.csproj

COPY src/Template.Api/ src/Template.Api/
RUN dotnet publish src/Template.Api/Template.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
# The base image runs as a non-root user by default.
ENTRYPOINT ["dotnet", "Template.Api.dll"]
