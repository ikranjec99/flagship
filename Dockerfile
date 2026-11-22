FROM node:24-alpine AS web-build
WORKDIR /source
COPY frontend/package.json frontend/package.json
WORKDIR /source/frontend
RUN npm install --no-audit --no-fund
WORKDIR /source
COPY frontend frontend
RUN mkdir -p Flagship/Flagship.API/wwwroot && cd frontend && npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS api-build
WORKDIR /source
COPY Directory.Build.props ./
COPY Flagship/Flagship.API/Flagship.API.csproj Flagship/Flagship.API/
COPY Flagship/Flagship.Core/Flagship.Core.csproj Flagship/Flagship.Core/
RUN dotnet restore Flagship/Flagship.API/Flagship.API.csproj
COPY Flagship/Flagship.API Flagship/Flagship.API
COPY Flagship/Flagship.Core Flagship/Flagship.Core
COPY --from=web-build /source/Flagship/Flagship.API/wwwroot Flagship/Flagship.API/wwwroot
RUN dotnet publish Flagship/Flagship.API/Flagship.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
COPY --from=api-build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Flagship.API.dll"]
