FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/UserManagement/UserManagement.API/UserManagement.API.csproj src/UserManagement/UserManagement.API/
COPY src/UserManagement/UserManagement.Application/UserManagement.Application.csproj src/UserManagement/UserManagement.Application/
COPY src/UserManagement/UserManagement.Infrastructure/UserManagement.Infrastructure.csproj src/UserManagement/UserManagement.Infrastructure/
COPY src/UserManagement/UserManagement.Domain/UserManagement.Domain.csproj src/UserManagement/UserManagement.Domain/
COPY src/Shared/Shared.Kernel/Shared.Kernel.csproj src/Shared/Shared.Kernel/
RUN dotnet restore src/UserManagement/UserManagement.API/UserManagement.API.csproj

COPY src/UserManagement src/UserManagement
COPY src/Shared src/Shared
RUN dotnet publish src/UserManagement/UserManagement.API/UserManagement.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "UserManagement.API.dll"]
