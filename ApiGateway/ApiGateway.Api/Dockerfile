# =========================
# Build image
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY ApiGateway/ApiGateway.Api/ApiGateway.Api.csproj ApiGateway/ApiGateway.Api/
RUN dotnet restore ApiGateway/ApiGateway.Api/ApiGateway.Api.csproj

COPY ApiGateway/ ApiGateway/

RUN dotnet publish ApiGateway/ApiGateway.Api/ApiGateway.Api.csproj -c Release -o /app/publish

# =========================
# Runtime image
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

RUN mkdir -p /data/config /home/app/.aspnet/DataProtection-Keys && chown -R $APP_UID:$APP_UID /app /data/config /home/app
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8888
EXPOSE 8888

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8888; printf "GET /health HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3; head -n 1 <&3 | grep -q " 200 "'

ENTRYPOINT ["dotnet", "ApiGateway.Api.dll"]
