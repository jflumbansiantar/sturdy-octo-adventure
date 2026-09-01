# syntax=docker/dockerfile:1

# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/PortfolioOS.Admin/PortfolioOS.Admin.csproj", "src/PortfolioOS.Admin/"]
RUN dotnet restore "src/PortfolioOS.Admin/PortfolioOS.Admin.csproj"

COPY src/PortfolioOS.Admin/ src/PortfolioOS.Admin/

WORKDIR /src/src/PortfolioOS.Admin
RUN dotnet publish -c Release -o /app/publish --no-restore

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# curl dipakai healthcheck docker-compose; image aspnet tidak menyertakannya.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PortfolioOS.Admin.dll"]
