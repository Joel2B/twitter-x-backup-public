FROM mcr.microsoft.com/dotnet/sdk:10.0.101 AS build
WORKDIR /src

ARG RUN_LIVE_X_API_TESTS=0

COPY Backup.sln ./
COPY Backup.csproj ./
COPY Backup.Tests/Backup.Tests.csproj Backup.Tests/
COPY Backup.IntegrationTests/Backup.IntegrationTests.csproj Backup.IntegrationTests/

RUN dotnet restore Backup.sln

COPY . .
RUN dotnet build Backup.sln -c Release --no-restore

FROM build AS test
ARG RUN_LIVE_X_API_TESTS=0
ENV RUN_LIVE_X_API_TESTS=${RUN_LIVE_X_API_TESTS}

RUN dotnet test Backup.Tests/Backup.Tests.csproj -c Release --no-build
RUN dotnet test Backup.IntegrationTests/Backup.IntegrationTests.csproj -c Release --no-build

FROM test AS publish
RUN dotnet publish Backup.csproj -c Release --no-build -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0.1 AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata \
    && rm -rf /var/lib/apt/lists/*

ENV TZ=America/Mazatlan

ENTRYPOINT ["dotnet", "Backup.dll"]
