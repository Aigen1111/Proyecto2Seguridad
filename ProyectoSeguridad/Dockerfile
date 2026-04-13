# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copiar archivos de proyecto
COPY *.csproj ./
RUN dotnet restore

# Copiar código fuente
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Instalar herramientas necesarias
RUN apt-get update && apt-get install -y ca-certificates && rm -rf /var/lib/apt/lists/*

# Copiar archivos publicados
COPY --from=build /app/publish .

# Crear directorio para certificados (opcional para HTTPS)
RUN mkdir -p /app/https

# Exponer puertos
EXPOSE 80 443

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=40s --retries=3 \
  CMD curl -f http://localhost/health || exit 1

# Ejecutar aplicación
ENTRYPOINT ["dotnet", "ProyectoSeguridad.dll"]
