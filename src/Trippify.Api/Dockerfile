# syntax=docker/dockerfile:1.6

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY global.json ./
COPY src/Trippify.Domain/Trippify.Domain.csproj src/Trippify.Domain/
COPY src/Trippify.Application/Trippify.Application.csproj src/Trippify.Application/
COPY src/Trippify.Infrastructure/Trippify.Infrastructure.csproj src/Trippify.Infrastructure/
COPY src/Trippify.Api/Trippify.Api.csproj src/Trippify.Api/
COPY Directory.Build.props ./
RUN dotnet restore src/Trippify.Api/Trippify.Api.csproj
COPY src/ src/
RUN dotnet publish src/Trippify.Api/Trippify.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__Postgres= \
    SelfHosted__RunMigrationsOnStartup=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "Trippify.Api.dll"]
