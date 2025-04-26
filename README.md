BiblioAPI - API RESTful para SiGeBi
API RESTful desarrollada en ASP.NET Core Web API para el sistema de gestión de biblioteca SiGeBi. Utiliza ADO.NET y Stored Procedures para la interacción con la base de datos SQL Server.
Tecnologías Utilizadas
•	.NET 7.0 (o la versión que usaste)
•	ASP.NET Core Web API
•	C#
•	ADO.NET (System.Data.SqlClient)
•	SQL Server
•	Swagger (OpenAPI) para documentación de API
•	Microsoft.AspNetCore.Identity.Core (para Hashing de contraseñas)
Configuración del Proyecto
1.	Base de Datos:
o	Ejecutar el script SQL proporcionado (ScriptCompletoBD.sql - asegúrate de incluir este archivo en el repositorio) en una instancia de SQL Server. Esto creará la base de datos BibliotecaDB, las tablas (Libros, Usuarios, Prestamos) y los Stored Procedures necesarios.
2.	Cadena de Conexión:
o	Abrir el archivo appsettings.json.
o	Modificar la cadena de conexión dentro de ConnectionStrings:MiConexion para que apunte a tu instancia local de SQL Server, utilizando las credenciales adecuadas (ya sea Autenticación de Windows o Autenticación de SQL Server).
o	Ejemplo (Aut. Windows): "Server=TU_SERVIDOR;Database=BibliotecaDB;Trusted_Connection=True;TrustServerCertificate=True;"
o	Ejemplo (Aut. SQL): "Server=TU_SERVIDOR;Database=BibliotecaDB;User ID=TU_USUARIO;Password=TU_CLAVE;TrustServerCertificate=True;"
3.	Prerrequisitos:
o	Tener instalado el SDK de .NET correspondiente a la versión del proyecto.
o	Tener acceso a una instancia de SQL Server.
Ejecución
•	Opción 1 (Visual Studio): Abrir el archivo de solución (.sln) en Visual Studio y presionar F5 o el botón de inicio.
•	Opción 2 (CLI): Navegar a la carpeta raíz del proyecto BiblioAPI en una terminal y ejecutar el comando dotnet run.
La API estará disponible en las URLs indicadas en la consola (usualmente https://localhost:XXXX y http://localhost:YYYY).
Documentación de Endpoints (Swagger)
Una vez que la API esté en ejecución, la documentación interactiva de Swagger estará disponible, por defecto, en la ruta /swagger. Desde allí se pueden probar todos los endpoints.
Endpoints Principales:
•	Libros (/api/libro)
o	GET /: Obtiene todos los libros (acepta ?tituloFilter=... y ?autorFilter=...).
o	GET /{id}: Obtiene un libro por ID.
o	POST /: Crea un nuevo libro.
o	PUT /{id}: Actualiza un libro existente.
o	DELETE /{id}: Elimina un libro.
•	Usuarios (/api/usuario)
o	GET /: Obtiene todos los usuarios (sin hash de contraseña).
o	GET /{id}: Obtiene un usuario por ID (sin hash de contraseña).
o	POST /: Crea un nuevo usuario. Si se envía Clave en el body, la hashea; si no, genera y hashea una contraseña por defecto.
o	POST /validar: Valida las credenciales del usuario (espera JSON con Correo y Clave en el body). Devuelve los datos del usuario (sin hash) si es válido.
o	PUT /{id}: Actualiza los datos de un usuario (no actualiza la contraseña desde este endpoint).
o	DELETE /{id}: Elimina un usuario.
•	Préstamos (/api/prestamo)
o	GET /: Obtiene todos los préstamos.
o	POST /: Registra un nuevo préstamo.
o	PUT /{id}: Actualiza un préstamo existente (usado para marcar devolución).
o	DELETE /{id}: Elimina un préstamo.
Notas de Seguridad
•	La creación de usuarios desde la API genera una contraseña por defecto si no se proporciona una. Esta contraseña debe ser considerada temporal.
•	La validación de contraseñas se realiza comparando hashes seguros (IPasswordHasher).
•	Todas las interacciones con la base de datos utilizan Stored Procedures parametrizados para prevenir inyección SQL.
