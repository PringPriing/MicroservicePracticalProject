FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/ApiGateway/ApiGateway.csproj src/ApiGateway/
RUN dotnet restore src/ApiGateway/ApiGateway.csproj

COPY src/ApiGateway src/ApiGateway
RUN dotnet publish src/ApiGateway/ApiGateway.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
