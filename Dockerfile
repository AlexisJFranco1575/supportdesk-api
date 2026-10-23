# syntax=docker/dockerfile:1

# ---------- Stage 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY . .

# Only the API project (and what it references) is restored and published.
# global.json is deliberately not needed here: the image's SDK is the one used.
RUN rm -f global.json \
    && dotnet restore src/SupportDesk.Api/SupportDesk.Api.csproj \
    && dotnet publish src/SupportDesk.Api/SupportDesk.Api.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        /p:UseAppHost=false

# ---------- Stage 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Listen on 8080 (non-privileged port); TLS is terminated by the hosting platform.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

# The aspnet image ships a non-root "app" user; never run the API as root.
USER $APP_UID

ENTRYPOINT ["dotnet", "SupportDesk.Api.dll"]