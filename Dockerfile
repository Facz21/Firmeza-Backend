# Stage 1: Build & Publish .NET 10 Web API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for optimal Docker layer caching
COPY ["src/Core/EnterpriseSystem.Domain/EnterpriseSystem.Domain.csproj", "src/Core/EnterpriseSystem.Domain/"]
COPY ["src/Core/EnterpriseSystem.Application/EnterpriseSystem.Application.csproj", "src/Core/EnterpriseSystem.Application/"]
COPY ["src/Infrastructure/EnterpriseSystem.Infrastructure/EnterpriseSystem.Infrastructure.csproj", "src/Infrastructure/EnterpriseSystem.Infrastructure/"]
COPY ["src/Presentation/EnterpriseSystem.WebApi/EnterpriseSystem.WebApi.csproj", "src/Presentation/EnterpriseSystem.WebApi/"]

RUN dotnet restore "src/Presentation/EnterpriseSystem.WebApi/EnterpriseSystem.WebApi.csproj"

# Copy the rest of the backend source code
COPY . .

WORKDIR "/src/src/Presentation/EnterpriseSystem.WebApi"
RUN dotnet publish "EnterpriseSystem.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Production Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Required by QuestPDF / SkiaSharp and Npgsql on Linux containers
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 libfreetype6 libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5193
ENV ASPNETCORE_ENVIRONMENT=Development
EXPOSE 5193

ENTRYPOINT ["dotnet", "EnterpriseSystem.WebApi.dll"]
