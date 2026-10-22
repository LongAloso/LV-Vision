FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY IdentityService/IdentityService.Api/IdentityService.Api.csproj IdentityService/IdentityService.Api/
COPY IdentityService/IdentityService.Application/IdentityService.Application.csproj IdentityService/IdentityService.Application/
COPY IdentityService/IdentityService.Domain/IdentityService.Domain.csproj IdentityService/IdentityService.Domain/
COPY IdentityService/IdentityService.Infrastructure/IdentityService.Infrastructure.csproj IdentityService/IdentityService.Infrastructure/

RUN dotnet restore IdentityService/IdentityService.Api/IdentityService.Api.csproj

COPY IdentityService/ IdentityService/

RUN dotnet publish IdentityService/IdentityService.Api/IdentityService.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

RUN mkdir -p /data/audit-outbox /data/config /home/app/.aspnet/DataProtection-Keys && chown -R $APP_UID:$APP_UID /app /data /home/app
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/5000; printf "GET /health/ready HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3; head -n 1 <&3 | grep -q " 200 "'

ENTRYPOINT ["dotnet", "IdentityService.Api.dll"]
