# ETAPA 1: Compilación
# Descarga una imagen oficial de .NET 8 SDK.
# Crea y se posiciona en la carpeta /app dentro del contenedor.
# ---- 
# ¿Qué significa AS build-env? Le estás poniendo un nombre a la etapa.
# Ese nombre luego se usa para copiar archivos en la etapa 2:
#    - COPY --from=build-env /app/out .
# Una vez que termina el docker build, normalmente solo se conserva la última etapa como imagen final.
# Cada FROM inicia un nuevo entorno (nuevo sistema de archivos basado en una imagen distinta).

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS compilation-stage
WORKDIR /app

# Copiar los archivos .csproj y restaurar las dependencias
# Copia el archivo .csproj desde tu máquina al contenedor.
# ejemplo AuthService.Api.csproj -> se copia en /app/AuthService.Api.csproj
# RUN dotnet restore: Descarga todos los paquetes NuGet definidos en el .csproj.
COPY ./src .
RUN dotnet restore

# Copiar el resto de los archivos del código fuente y compilar
# Controllers Services Program.cs appsettings.json etc. -> al contenedor
# publish -> Genera una versión lista para producción en la ruta /app/out -> aqui iran las DLL.
COPY . .
RUN dotnet publish ./src -c Release -o out

# ETAPA 2: Runtime (Imagen final ligera)
# Ahora comienza una nueva imagen.
# contiene el Runtime de .NET ASP.NET Core
# No tiene: SDK Compilador Herramientas de desarrollo Por eso es más pequeña.
# crea /app y se posiciona allí.
# Copiar la aplicación compilada COPY --from=build-env /app/out .
# Aquí ocurre la magia del multi-stage build.  
# Toma los archivos publicados desde la primera etapa: build-env:/app/out -> y los copia runtime:/app
# Quedaria: /app
#           ├── AuthService.Api.dll
#           ├── appsettings.json
#           ├── ...
# EN EL COPY -> En ese momento Docker tiene acceso a ambas etapas:
# COPY --from=[nombre_etapa]... -> Estás copiando archivos desde una etapa anterior mientras Docker aún está construyendo la imagen.
#   - Ruild-env /app/out al runtime /app (segunda etapa)
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=compilation-stage /app/out .

# Exponer el puerto en el que correrá la API dentro del contenedor
EXPOSE 8080

# Comando para arrancar la aplicación
ENTRYPOINT ["dotnet", "AuthService.Api.dll"]
