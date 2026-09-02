# syntax=docker/dockerfile:1

# --- Embedding model stage ---
# The chat assistant's embedding model (~490MB), fetched at build time rather than committed.
# It lives in its own stage so the download is cached independently of the source tree:
# editing code does not re-fetch it.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS model
WORKDIR /model
ARG MODEL_BASE=https://huggingface.co/intfloat/multilingual-e5-small/resolve/main
RUN curl -fSL --retry 3 -o model.onnx              "$MODEL_BASE/onnx/model.onnx" \
 && curl -fSL --retry 3 -o sentencepiece.bpe.model "$MODEL_BASE/sentencepiece.bpe.model" \
 && curl -fSL --retry 3 -o tokenizer.json          "$MODEL_BASE/tokenizer.json"

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

# Embedding:ModelPath defaults to models/e5-small, relative to this WORKDIR.
# Without this the API still starts and every feature except /api/chat works.
COPY --from=model /model/ ./models/e5-small/

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PortfolioOS.API.dll"]
