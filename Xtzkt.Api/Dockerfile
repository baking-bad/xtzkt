# Xtzkt.Api container image.
#
# The build context MUST be the repository root, not this directory:
#     docker build -f Xtzkt.Api/Dockerfile -t xtzkt-api .
#
# or, for all four images at once:
#     docker compose build
#
# Visual Studio builds this file too (see https://aka.ms/customizecontainer). Its
# "Container (Dockerfile)" launch profile runs the `base` stage in fast mode, so `base`
# must stay the first runtime stage.

# ------------------------------------------------------------------------------------------
# base — runtime image, shared by `final` and VS fast-mode debugging
# ------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

# curl for health checks
RUN apt-get update \
	&& apt-get install -y --no-install-recommends curl \
	&& rm -rf /var/lib/apt/lists/*

# non-root uid defined by the .NET base images
USER $APP_UID
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# every service maps GET /health, which answers 200 while the host is serving
HEALTHCHECK --interval=15s --start-interval=2s --timeout=6s --start-period=300s --retries=5 \
	CMD curl -fsS --max-time 5 "http://localhost:${ASPNETCORE_HTTP_PORTS:-8080}/health" || exit 1

# ------------------------------------------------------------------------------------------
# restore — NuGet layer, invalidated only by changes to the project files
# ------------------------------------------------------------------------------------------
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS restore

# .NET accepts Docker's arch names as-is
ARG TARGETARCH
WORKDIR /src

# this stage is byte-identical in all four Dockerfiles, so Docker builds it once and reuses
# the cached layers for the other images
COPY ["Xtzkt.slnx", "./"]
COPY ["Xtzkt.Api/Xtzkt.Api.csproj", "Xtzkt.Api/"]
COPY ["Xtzkt.Data/Xtzkt.Data.csproj", "Xtzkt.Data/"]
COPY ["Xtzkt.Indexers.Common/Xtzkt.Indexers.Common.csproj", "Xtzkt.Indexers.Common/"]
COPY ["Xtzkt.Indexers.L1/Xtzkt.Indexers.L1.csproj", "Xtzkt.Indexers.L1/"]
COPY ["Xtzkt.Indexers.TezosX/Xtzkt.Indexers.TezosX.csproj", "Xtzkt.Indexers.TezosX/"]
COPY ["Xtzkt.Services.Metadata/Xtzkt.Services.Metadata.csproj", "Xtzkt.Services.Metadata/"]
COPY ["Xtzkt.Utils/Xtzkt.Utils.csproj", "Xtzkt.Utils/"]
RUN dotnet restore "Xtzkt.slnx" --arch $TARGETARCH

# ------------------------------------------------------------------------------------------
# publish — compiles once
# ------------------------------------------------------------------------------------------
FROM restore AS publish

# .NET accepts Docker's arch names as-is
ARG TARGETARCH
ARG BUILD_CONFIGURATION=Release
COPY . .

# --arch must match the restore above
RUN dotnet publish "Xtzkt.Api/Xtzkt.Api.csproj" \
	--configuration $BUILD_CONFIGURATION \
	--arch $TARGETARCH \
	--no-restore \
	--output /app/publish \
	-p:UseAppHost=false \
	-p:ContinuousIntegrationBuild=true

# ------------------------------------------------------------------------------------------
# final
# ------------------------------------------------------------------------------------------
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Xtzkt.Api.dll"]
