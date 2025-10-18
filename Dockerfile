# Multi-stage Dockerfile for DeepResearch.WebApp
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies (layer caching)
COPY DeepResearch.WebApp.csproj .
RUN dotnet restore

# Copy remaining source files
COPY . .

# Build and publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Create Data directory in /home for persistence (Azure App Service standard)
RUN mkdir -p /home/Data && chown -R appuser:appuser /home/Data

# Create /app directory and set ownership
RUN mkdir -p /app && chown -R appuser:appuser /app

# Copy published files from build stage
COPY --from=build /app/publish .

# Set ownership
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose port 8080 (Azure Web Apps default)
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the application
ENTRYPOINT ["dotnet", "DeepResearch.WebApp.dll"]
