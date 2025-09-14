# ------------------------
# Stage 1: Build
# ------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# (Optional) faster restores if you use a private NuGet feed:
# COPY NuGet.config ./

# Copy only solution & project files first for restore-layer caching
COPY ["CTCare.sln", "."]
COPY ["Src/CTCare.Api/CTCare.Api.csproj", "Src/CTCare.Api/"]
COPY ["Src/CTCare.Application/CTCare.Application.csproj", "Src/CTCare.Application/"]
COPY ["Src/CTCare.Domain/CTCare.Domain.csproj", "Src/CTCare.Domain/"]
COPY ["Src/CTCare.Infrastructure/CTCare.Infrastructure.csproj", "Src/CTCare.Infrastructure/"]
COPY ["Src/CTCare.Reporting/CTCare.Reporting.csproj", "Src/CTCare.Reporting/"]
COPY ["Src/CTCare.Shared/CTCare.Shared.csproj", "Src/CTCare.Shared/"]
COPY ["Tests/CTCare.Application/CTCare.Application.csproj", "Tests/CTCare.Application/"]
COPY ["Tests/CTCare.Domain/CTCare.Domain.csproj", "Tests/CTCare.Domain/"]

RUN dotnet restore "CTCare.sln"

# Now copy the source and publish
COPY . .
# Tip: make the build configuration switchable
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Src/CTCare.Api/CTCare.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore

# ------------------------
# Stage 2: Runtime
# ------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# --- Globalization (safer for non-en-US locales) ---
# If you need full ICU; comment this out if not needed.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libicu-dev curl \
    && rm -rf /var/lib/apt/lists/*

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV DOTNET_ENVIRONMENT=Production

# --- Networking/Ports ---
# Render will inject $PORT; we'll still expose 8080 for generic Docker runs
EXPOSE 8080
# Default; Render blueprint will override with "http://0.0.0.0:${PORT}"
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

# --- Security: run as non-root ---
# Create an unprivileged user and switch to it
RUN useradd -m appuser
USER appuser

# App files
COPY --from=build /app/publish ./

# --- Healthcheck (optional but nice) ---
# Assumes you map /health in your API
HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
  CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "CTCare.Api.dll"]
