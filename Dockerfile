FROM mcr.microsoft.com/dotnet/sdk:10.0.101 AS build
WORKDIR /src

COPY Backup.sln ./
COPY Backup.Domain/Backup.Domain.csproj Backup.Domain/
COPY Backup.Application/Backup.Application.csproj Backup.Application/
COPY Backup.Infrastructure/Backup.Infrastructure.csproj Backup.Infrastructure/
COPY Backup.Cli/Backup.Cli.csproj Backup.Cli/
COPY Backup.Api/Backup.Api.csproj Backup.Api/
COPY Backup.Tests/Backup.Tests.csproj Backup.Tests/
COPY Backup.IntegrationTests/Backup.IntegrationTests.csproj Backup.IntegrationTests/

RUN dotnet restore Backup.sln

COPY . .
RUN dotnet build Backup.sln -c Release --no-restore

FROM build AS test
ARG RUN_LIVE_X_API_TESTS=0
ENV RUN_LIVE_X_API_TESTS=${RUN_LIVE_X_API_TESTS}

RUN dotnet test Backup.Tests/Backup.Tests.csproj -c Release --no-build --no-restore
RUN dotnet test Backup.IntegrationTests/Backup.IntegrationTests.csproj -c Release --no-build --no-restore

FROM test AS cli-publish
RUN dotnet publish Backup.Cli/Backup.Cli.csproj -c Release --no-build --no-restore -o /app/publish

FROM test AS api-publish
RUN dotnet publish Backup.Api/Backup.Api.csproj -c Release --no-build --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0.1 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

ENV TZ=America/Mazatlan

FROM runtime AS cli
COPY --from=cli-publish /app/publish .
ENTRYPOINT ["dotnet", "Backup.Cli.dll"]

FROM runtime AS api
COPY --from=api-publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8081
EXPOSE 8081
ENTRYPOINT ["dotnet", "Backup.Api.dll"]
