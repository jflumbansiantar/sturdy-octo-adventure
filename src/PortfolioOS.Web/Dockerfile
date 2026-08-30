# syntax=docker/dockerfile:1

# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/PortfolioOS.Web/PortfolioOS.Web.csproj", "src/PortfolioOS.Web/"]
COPY ["src/PortfolioOS.Shared/PortfolioOS.Shared.csproj", "src/PortfolioOS.Shared/"]
RUN dotnet restore "src/PortfolioOS.Web/PortfolioOS.Web.csproj"

COPY src/PortfolioOS.Web/ src/PortfolioOS.Web/
COPY src/PortfolioOS.Shared/ src/PortfolioOS.Shared/

WORKDIR /src/src/PortfolioOS.Web
RUN dotnet publish -c Release -o /app/publish --no-restore

# --- Runtime stage: static files served by nginx ---
FROM nginx:alpine AS final
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY src/PortfolioOS.Web/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
