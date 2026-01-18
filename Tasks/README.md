# Introduction
- Plantilla .Net core 6 para WebApi

# Getting Started
1. Se debe cambiar nombres de proyectos.
2. Cambiar namespace.
3. Eliminar lo que no se use.
4. Mantener la arquitectura interfaces.
5. Mantener inyección de dependencias.
6. Documentación de apis con swagger para lo cual se debe colocar comentarios respectivos.
7. Se usa nueva dll de autorización, funcional solo para .Net core

# Build and Test
- Existe proyecto de ejemplo para implementar pruebas unitarias de la capa de Business.

# Contribute
- Se pueden realizar mejoras, analizadas y revisadas en conjunto.

# Instalar Template
1. En una consola navegar al directorio donde se encuentra la solución del Template <Academikus.NetCore6WebApiTemplate.Solution>
2. Ejecutar el siguiente comando: 
    dotnet new install .\
	
# Desinstalar Template
1. En una consola navegar al directorio donde se encuentra la solución del Template <Academikus.NetCore6WebApiTemplate.Solution>
2. Ejecutar el siguiente comando: 
    dotnet new uninstall .\

# Generar una nueva solución a partir del template
1. Navegue hacia la carpeta donde va a crear la solución
2. Ejecute el siguiente comando. Reemplace <NombreDeLaSolution> por el nombre de su solución.
	- <NombreDeLaSolution> Sin puntos, ni prefijos. 
	- Ejemplo: D2LWebApi. No debería ser D2LWebApi.Solution.
	- Ejecutar: dotnet new AcademikusWebApi -S NombreDeLaSolution
3. Ingresar al directorio de la nueva solución y borrar la carpeta git, vs.

# Uso del token transversal
1. En el archivo Program.cs de cada API (Microservicio), se debe setear en false la opción ValidateIssuer. (ValidateIssuer = false).
2. En el archivo appsettings.json, se debe agregar en la lista de audiencias el dominio de las APIS que se van a compartir el token ("ValidAudiences": [ "https://localhost:7135/", "https://localhost:7136/" ]) 


NOTA: Si hay una nueva versión del template, se debe desintalar y volver a instalar el nuevo template.
SOURCE: https://learn.microsoft.com/en-us/dotnet/core/tutorials/cli-templates-create-item-template