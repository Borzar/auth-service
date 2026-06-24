
# ==========================================
# ETAPA 1: Base de compilación y restauración
# DESCARGA UNA IMAGEN OFICIAL DE .NET 8 SDK.
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS compilation-stage
WORKDIR /app
# Copiar el código fuente y restaurar dependencias
COPY ./src .
RUN dotnet restore

# ==========================================
# ETAPA 2: Ejecución de Tests 🧪
# ==========================================
FROM compilation-stage AS test-stage
# Mantiene la ubicacion anterior /app
# Ejecuta los tests. Si uno solo falla, devuelve un código de salida diferente de 0 
# y Docker rompe el build aquí mismo. (Realease: optimiza el codigo para prod. Mas liviano)
RUN dotnet test --no-restore -c Release

# ==========================================
# ETAPA 3: Publicación (Solo llega aquí si los tests pasaron)
# ==========================================
FROM test-stage AS publish-stage
RUN dotnet publish -c Release -o out

# ==========================================
# ETAPA 4: Runtime (Imagen final ligera)
# AHORA COMIENZA UNA NUEVA IMAGEN.
# CONTIENE EL RUNTIME DE .NET ASP.NET CORE
# NO TIENE: SDK COMPILADOR HERRAMIENTAS DE DESARROLLO POR ESO ES MÁS PEQUEÑA.
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
# Ojo: Ahora copiamos desde "publish-stage"
COPY --from=publish-stage /app/out .

EXPOSE 8080
ENTRYPOINT ["dotnet", "AuthService.Api.dll"]