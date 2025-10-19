# Azure Container Deployment Guide

## Prerequisites

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
- [Docker](https://docs.docker.com/get-docker/)
- Azure subscription with:
  - Azure Container Registry
  - Azure Web App (Linux, Container)

## Quick Deployment

```bash
# Build and tag
docker build -t derpresearch:latest .
docker tag derpresearch:latest websitesregistry.azurecr.io/derpresearch:latest

# Push to ACR
az acr login --name websitesregistry
docker push websitesregistry.azurecr.io/derpresearch:latest

# Restart Azure Web App
az webapp restart --name derpresearch --resource-group Websites

# Monitor logs
az webapp log tail --name derpresearch --resource-group Websites
```

## Detailed Steps

### 1. Build Docker Image

```bash
cd DerpResearch
docker build -t derpresearch:latest .
```

The Dockerfile uses multi-stage build:

- **Build stage**: .NET 9.0 SDK
- **Runtime stage**: Lightweight ASP.NET runtime

### 2. Test Locally (Recommended)

```bash
docker run -d -p 8080:8080 \
  -e AzureOpenAI__Endpoint="https://YOUR-INSTANCE.openai.azure.com/" \
  -e AzureOpenAI__ApiKey="YOUR-API-KEY" \
  -e AzureOpenAI__Deployments__Chat="gpt-4o" \
  -e AzureOpenAI__Deployments__ChatMini="gpt-4o-mini" \
  -e AzureOpenAI__Deployments__Embedding="text-embedding-3-large" \
  --name derpresearch-test derpresearch:latest

# Test at http://localhost:8080

# Cleanup
docker stop derpresearch-test
docker rm derpresearch-test
```

### 3. Tag for Azure Container Registry

```bash
docker tag derpresearch:latest websitesregistry.azurecr.io/derpresearch:latest
docker tag derpresearch:latest websitesregistry.azurecr.io/derpresearch:v1.0.0
```

### 4. Authenticate with ACR

```bash
az login
az acr login --name websitesregistry
```

**Alternative with admin credentials:**

```bash
az acr update --name websitesregistry --admin-enabled true
az acr credential show --name websitesregistry
docker login websitesregistry.azurecr.io -u websitesregistry -p <password>
```

### 5. Push to ACR

```bash
docker push websitesregistry.azurecr.io/derpresearch:latest
docker push websitesregistry.azurecr.io/derpresearch:v1.0.0
```

### 6. Configure Azure Web App

```bash
az webapp config container set \
  --name derpresearch \
  --resource-group Websites \
  --docker-custom-image-name websitesregistry.azurecr.io/derpresearch:latest \
  --docker-registry-server-url https://websitesregistry.azurecr.io

# Enable continuous deployment (optional)
az webapp deployment container config \
  --name derpresearch \
  --resource-group Websites \
  --enable-cd true
```

### 7. Configure Environment Variables

```bash
az webapp config appsettings set \
  --name derpresearch \
  --resource-group Websites \
  --settings \
    AzureOpenAI__Endpoint="https://YOUR-INSTANCE.openai.azure.com/" \
    AzureOpenAI__ApiKey="YOUR-API-KEY" \
    AzureOpenAI__Deployments__Chat="gpt-4o" \
    AzureOpenAI__Deployments__ChatMini="gpt-4o-mini" \
    AzureOpenAI__Deployments__Embedding="text-embedding-3-large" \
    ASPNETCORE_ENVIRONMENT="Production"
```

**Via Azure Portal:**

1. Navigate to App Services → `derpresearch`
2. Configuration → Application settings
3. Add settings as key-value pairs
4. Save

### 8. Restart and Verify

```bash
az webapp restart --name derpresearch --resource-group Websites
az webapp show --name derpresearch --resource-group Websites --query defaultHostName -o tsv
```

Visit: `https://derpresearch.azurewebsites.net`

### 9. Monitor Logs

```bash
az webapp log config \
  --name derpresearch \
  --resource-group Websites \
  --docker-container-logging filesystem

az webapp log tail --name derpresearch --resource-group Websites
```

## Deployment Script

Save as `deploy.sh`:

```bash
#!/bin/bash
ACR_NAME="websitesregistry"
ACR_LOGIN_SERVER="websitesregistry.azurecr.io"
IMAGE_NAME="derpresearch"
VERSION="latest"
RESOURCE_GROUP="Websites"
WEBAPP_NAME="derpresearch"

echo "Building Docker image..."
docker build -t ${IMAGE_NAME}:${VERSION} .

echo "Tagging image..."
docker tag ${IMAGE_NAME}:${VERSION} ${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${VERSION}

echo "Logging in to ACR..."
az acr login --name ${ACR_NAME}

echo "Pushing image..."
docker push ${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${VERSION}

echo "Configuring Web App..."
az webapp config container set \
  --name ${WEBAPP_NAME} \
  --resource-group ${RESOURCE_GROUP} \
  --docker-custom-image-name ${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${VERSION} \
  --docker-registry-server-url https://${ACR_LOGIN_SERVER}

echo "Restarting web app..."
az webapp restart --name ${WEBAPP_NAME} --resource-group ${RESOURCE_GROUP}

echo "Deployment complete!"
echo "URL: https://${WEBAPP_NAME}.azurewebsites.net"
```

## Database Persistence

**SQLite Configuration:**

- Database path: `/home/Data/deepresearch.db`
- Persists across container restarts
- Limited persistence across deployments

**Production Recommendations:**

- Azure SQL Database
- Azure Database for PostgreSQL
- Cosmos DB

## Important Notes

### Port Configuration

Container listens on port 8080 (Azure standard)

### HTTPS

Automatically handled by Azure Web Apps

### Health Checks

Azure pings `/` by default

## Troubleshooting

### Container Won't Start

```bash
az webapp log tail --name derpresearch --resource-group Websites
az webapp deployment list --name derpresearch --resource-group Websites
```

### 502 Bad Gateway

- Verify port 8080
- Check environment variables
- Review startup logs

### Memory Issues

- Upgrade App Service Plan
- Optimize vector index size

### Database Locked

- Expected with SQLite multi-instance
- Consider Azure SQL for production

## CI/CD Integration

### GitHub Actions

```yaml
name: Deploy to Azure

on:
  push:
    branches: [ main ]

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    
    - name: Login to ACR
      uses: azure/docker-login@v1
      with:
        login-server: websitesregistry.azurecr.io
        username: ${{ secrets.ACR_USERNAME }}
        password: ${{ secrets.ACR_PASSWORD }}
    
    - name: Build and push
      run: |
        docker build -t websitesregistry.azurecr.io/derpresearch:${{ github.sha }} .
        docker push websitesregistry.azurecr.io/derpresearch:${{ github.sha }}
    
    - name: Deploy to Azure
      uses: azure/webapps-deploy@v2
      with:
        app-name: derpresearch
        images: websitesregistry.azurecr.io/derpresearch:${{ github.sha }}
```

## Production Checklist

- [ ] Use Azure Key Vault for secrets
- [ ] Configure CORS for specific origins
- [ ] Add rate limiting
- [ ] Monitor costs (API usage)
- [ ] Migrate to Azure SQL/PostgreSQL
- [ ] Enable Application Insights
- [ ] Set up health checks
- [ ] Configure auto-scaling
- [ ] Enable registry webhooks
- [ ] Set up monitoring alerts
- [ ] Implement backup strategy
