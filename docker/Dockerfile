# syntax=docker/dockerfile:1

# ---- build stage: SDK image, compiles and publishes ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project file first so the restore layer stays cached
# until dependencies change (not on every source edit).
COPY src/Durnius.Web/Durnius.Web.csproj src/Durnius.Web/
RUN dotnet restore src/Durnius.Web/Durnius.Web.csproj

# Now copy the source and publish without re-restoring.
COPY src/Durnius.Web/ src/Durnius.Web/
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
