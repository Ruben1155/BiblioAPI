# SiGeBi Library System

Este repositorio contiene dos proyectos:

- **BiblioAPI** – API RESTful desarrollada en ASP.NET Core Web API para el sistema de gestión de biblioteca SiGeBi.  
- **BiblioApp** – Interfaz web MVC en ASP.NET Core MVC para consumir BiblioAPI y gestionar la biblioteca SiGeBi.

---

## 📚 BiblioAPI

API RESTful para SiGeBi.  
Desarrollada en ASP.NET Core Web API con ADO.NET y Stored Procedures para SQL Server.

### Tecnologías utilizadas

- .NET 8.0
- ASP.NET Core Web API  
- C#  
- ADO.NET (System.Data.SqlClient)  
- SQL Server  
- Swagger (OpenAPI) para documentación de API  
- Microsoft.AspNetCore.Identity.Core (para hashing de contraseñas)

### 🛠 Configuración

1. **Base de Datos**  
   - Ejecutar `ScriptCompletoBD.sql` en tu instancia de SQL Server.  
   - Se crea la base de datos `BibliotecaDB`, tablas (`Libros`, `Usuarios`, `Prestamos`) y los Stored Procedures.

2. **Cadena de conexión**  
   - Edita `appsettings.json` → `ConnectionStrings:MiConexion`.  
   - Ejemplo (Autenticación Windows):  
     ```json
     "Server=TU_SERVIDOR;Database=BibliotecaDB;Trusted_Connection=True;TrustServerCertificate=True;"
     ```
   - Ejemplo (Autenticación SQL Server):  
     ```json
     "Server=TU_SERVIDOR;Database=BibliotecaDB;User ID=TU_USUARIO;Password=TU_CLAVE;TrustServerCertificate=True;"
     ```

3. **Prerrequisitos**  
   - SDK de .NET correspondiente.  
   - Instancia de SQL Server accesible.

### ▶️ Ejecución

- **Visual Studio**  
  1. Abrir `.sln`  
  2. Presionar F5 o botón de inicio.

- **CLI**  
  ```bash
  cd BiblioAPI
  dotnet run
La API estará disponible en la(s) URL(s) que devuelva la consola (ej. https://localhost:5001, http://localhost:5000).

📖 Documentación (Swagger)
Una vez en ejecución, accede a:

bash
Copiar
Editar
/swagger
para probar todos los endpoints de forma interactiva.

Endpoints principales
Libros (/api/libro)

GET / – Obtiene todos los libros (filtros: ?tituloFilter=…, ?autorFilter=…)

GET /{id} – Obtiene libro por ID

POST / – Crea un libro

PUT /{id} – Actualiza un libro existente

DELETE /{id} – Elimina un libro

Usuarios (/api/usuario)

GET / – Lista todos los usuarios (sin hash de contraseña)

GET /{id} – Obtiene usuario por ID (sin hash)

POST / – Crea usuario (hashea contraseña enviada o genera una por defecto)

POST /validar – Valida credenciales (envía JSON con Correo y Clave)

PUT /{id} – Actualiza datos de usuario (no cambia contraseña)

DELETE /{id} – Elimina usuario

Préstamos (/api/prestamo)

GET / – Lista todos los préstamos

POST / – Registra nuevo préstamo

PUT /{id} – Actualiza un préstamo (p.ej. marcar devolución)

DELETE /{id} – Elimina préstamo

🔒 Notas de seguridad
Las contraseñas por defecto generadas deben considerarse temporales.

El hashing y la validación de contraseñas usan IPasswordHasher.

Todos los Stored Procedures son parametrizados para prevenir inyección SQL.

💻 BiblioApp
Interfaz web MVC para SiGeBi, consumiendo BiblioAPI.

Tecnologías utilizadas
.NET 8.0

ASP.NET Core MVC & Razor Views

C#

Bootstrap 5

Sesiones de ASP.NET Core (login y roles)

iTextSharp.LGPLv2.Core (exportación a PDF)

EPPlus (exportación a Excel)

Newtonsoft.Json (comunicación con la API)

Microsoft.AspNetCore.Http.Abstractions (IHttpContextAccessor)

🛠 Configuración
Dependencia de API

Asegúrate de que BiblioAPI esté corriendo.

URL de la API

Edita appsettings.json → ApiSettings:BaseUrl.

Ejemplo:

json
Copiar
Editar
"BaseUrl": "https://localhost:5001/api"
Prerrequisitos

SDK de .NET correspondiente.

BiblioAPI en ejecución.

▶️ Ejecución
Visual Studio

Abrir .sln

Presionar F5 (con BiblioAPI corriendo)

CLI

bash
Copiar
Editar
cd BiblioApp
dotnet run
Accede a la URL que indique la consola (ej. https://localhost:5002).

🚀 Funcionalidades
Registro & Login

Registro público (Estudiante, Docente, Otros)

Validación de credenciales contra la API

Roles & Navegación

“Administrador” vs. otros

Dashboard de admin (contadores de Libros, Usuarios, Préstamos)

Menú adaptado al rol

Rutas protegidas (p.ej. gestión de usuarios solo para admin)

Gestión de Libros

Listar, buscar, crear, editar, eliminar

Exportar a PDF y Excel

Gestión de Usuarios (Admin)

Listar, crear (con contraseña por defecto), ver, editar, eliminar

Gestión de Préstamos

Listar todos los préstamos

Registrar nuevos y devoluciones

Eliminar

Exportar a PDF y Excel
