# build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ./src/services/PriceIngestor/ ./src/services/PriceIngestor/
WORKDIR /src/src/services/PriceIngestor
RUN dotnet restore
RUN dotnet publish -c Release -o /app

# runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PriceIngestor.dll"]
