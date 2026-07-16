FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY MissaoIsrael.sln ./
COPY src/MissaoIsrael.Domain/MissaoIsrael.Domain.csproj src/MissaoIsrael.Domain/
COPY src/MissaoIsrael.Application/MissaoIsrael.Application.csproj src/MissaoIsrael.Application/
COPY src/MissaoIsrael.Infrastructure/MissaoIsrael.Infrastructure.csproj src/MissaoIsrael.Infrastructure/
COPY src/MissaoIsrael.Api/MissaoIsrael.Api.csproj src/MissaoIsrael.Api/
RUN dotnet restore MissaoIsrael.sln

COPY . .
RUN dotnet publish src/MissaoIsrael.Api/MissaoIsrael.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MissaoIsrael.Api.dll"]
