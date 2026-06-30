# --- build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /App

# Copy .csproj files and restore as a distinct layer for better caching
COPY SchoolMgmt.WebApi/SchoolMgmt.WebApi.csproj ./SchoolMgmt.WebApi/
COPY SchoolMgmt.Domain/SchoolMgmt.Domain.csproj ./SchoolMgmt.Domain/
COPY SchoolMgmt.Application/SchoolMgmt.Application.csproj ./SchoolMgmt.Application/
COPY SchoolMgmt.Infrastructure/SchoolMgmt.Infrastructure.csproj ./SchoolMgmt.Infrastructure/

# Restore dependencies — scoped to WebApi.csproj (pulls in Domain/Application/
# Infrastructure via ProjectReference). Test projects are excluded via
# .dockerignore and never restored/built for this image.
RUN dotnet restore ./SchoolMgmt.WebApi/SchoolMgmt.WebApi.csproj

# Copy the rest of the source and publish
COPY . ./
RUN dotnet publish -c Release -o published ./SchoolMgmt.WebApi/SchoolMgmt.WebApi.csproj

# --- migrator stage
FROM build AS migrator
WORKDIR /App
RUN dotnet tool install --global dotnet-ef --version 8.*
ENV PATH="${PATH}:/root/.dotnet/tools"
ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "./SchoolMgmt.Infrastructure/SchoolMgmt.Infrastructure.csproj", "--startup-project", "./SchoolMgmt.Infrastructure/SchoolMgmt.Infrastructure.csproj"]

# --- api runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /App
COPY --from=build /App/published .
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "SchoolMgmt.WebApi.dll"]
