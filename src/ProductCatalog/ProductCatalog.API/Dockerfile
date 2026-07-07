FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/ProductCatalog/ProductCatalog.API/ProductCatalog.API.csproj src/ProductCatalog/ProductCatalog.API/
COPY src/ProductCatalog/ProductCatalog.Application/ProductCatalog.Application.csproj src/ProductCatalog/ProductCatalog.Application/
COPY src/ProductCatalog/ProductCatalog.Infrastructure/ProductCatalog.Infrastructure.csproj src/ProductCatalog/ProductCatalog.Infrastructure/
COPY src/ProductCatalog/ProductCatalog.Domain/ProductCatalog.Domain.csproj src/ProductCatalog/ProductCatalog.Domain/
COPY src/Shared/Shared.Kernel/Shared.Kernel.csproj src/Shared/Shared.Kernel/
RUN dotnet restore src/ProductCatalog/ProductCatalog.API/ProductCatalog.API.csproj

COPY src/ProductCatalog src/ProductCatalog
COPY src/Shared src/Shared
RUN dotnet publish src/ProductCatalog/ProductCatalog.API/ProductCatalog.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ProductCatalog.API.dll"]
