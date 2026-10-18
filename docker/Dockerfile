# syntax=docker/dockerfile:1

# ---- client stage: Node builds the React app into the Web wwwroot ----
FROM node:24-alpine AS client
WORKDIR /src

COPY src/Durnius.Client/package.json src/Durnius.Client/package-lock.json src/Durnius.Client/
RUN --mount=type=cache,target=/root/.npm \
    npm --prefix src/Durnius.Client ci

COPY src/Durnius.Client/ src/Durnius.Client/

# Same-origin API: an empty base URL makes the SPA issue relative /api requests.
# Override with --build-arg VITE_API_URL=http://host:port to point elsewhere.
ARG VITE_API_URL=""
ENV VITE_API_URL=$VITE_API_URL

RUN npm --prefix src/Durnius.Client run build

# ---- build stage: SDK image, compiles and publishes ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project files first so the restore layer stays cached
# until dependencies change (not on every source edit).
COPY src/Durnius.Web/Durnius.Web.csproj src/Durnius.Web/Directory.Build.props src/Durnius.Web/
RUN dotnet restore src/Durnius.Web/Durnius.Web.csproj

# Now copy the source and publish without re-restoring.
COPY src/Durnius.Web/ src/Durnius.Web/

COPY --from=client /src/src/Durnius.Web/wwwroot/ src/Durnius.Web/wwwroot/

RUN dotnet publish src/Durnius.Web/Durnius.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---- final stage: ASP.NET runtime only, no SDK or source ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Run as the non-root "app" user built into the official images.
USER $APP_UID

# Official .NET images listen on 8080 by default.
EXPOSE 8080
ENTRYPOINT ["dotnet", "Durnius.Web.dll"]
