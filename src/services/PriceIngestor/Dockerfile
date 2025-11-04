# build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# copy only project files first for better layer caching
COPY ./src/*.sln ./src/
COPY ./src/common/Common.Data/Common.Data.csproj ./src/common/Common.Data/
COPY ./src/common/Common.Rest/Common.Rest.csproj ./src/common/Common.Rest/
COPY ./src/services/PriceIngestor/PriceIngestor.csproj ./src/services/PriceIngestor/
RUN dotnet restore ./src/services/PriceIngestor/PriceIngestor.csproj

# now copy sources
COPY ./src/ ./src/
RUN dotnet publish ./src/services/PriceIngestor/PriceIngestor.csproj -c Release -o /app

# runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app ./ 
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PriceIngestor.dll"]
