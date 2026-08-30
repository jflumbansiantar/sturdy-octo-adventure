# syntax=docker/dockerfile:1

# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only project files first for better layer caching on restore
COPY ["src/PortfolioOS.API/PortfolioOS.API.csproj", "src/PortfolioOS.API/"]
COPY ["src/PortfolioOS.Application/PortfolioOS.Application.csproj", "src/PortfolioOS.Application/"]
COPY ["src/PortfolioOS.Domain/PortfolioOS.Domain.csproj", "src/PortfolioOS.Domain/"]
COPY ["src/PortfolioOS.Infrastructure/PortfolioOS.Infrastructure.csproj", "src/PortfolioOS.Infrastructure/"]
COPY ["src/PortfolioOS.Shared/PortfolioOS.Shared.csproj", "src/PortfolioOS.Shared/"]
RUN dotnet restore "src/PortfolioOS.API/PortfolioOS.API.csproj"

# Now copy the rest of the source and publish
COPY src/PortfolioOS.API/ src/PortfolioOS.API/
COPY src/PortfolioOS.Application/ src/PortfolioOS.Application/
COPY src/PortfolioOS.Domain/ src/PortfolioOS.Domain/
COPY src/PortfolioOS.Infrastructure/ src/PortfolioOS.Infrastructure/
COPY src/PortfolioOS.Shared/ src/PortfolioOS.Shared/

WORKDIR /src/src/PortfolioOS.API
RUN dotnet publish -c Release -o /app/publish --no-restore

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PortfolioOS.API.dll"]
