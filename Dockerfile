FROM mcr.microsoft.com/dotnet/sdk:10.0.101 AS build
WORKDIR /src

COPY *.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0.1 AS final
WORKDIR /app
COPY --from=build /app/publish .

RUN apt-get update \
 && apt-get install -y --no-install-recommends tzdata \
 && rm -rf /var/lib/apt/lists/*

ENV TZ=America/Mazatlan

ENTRYPOINT ["dotnet", "Backup.dll"]
