// --- BiblioAPI/Program.cs ---

// Usings necesarios para los servicios, hashing y configuraci�n
using BiblioAPI.Services;
using BiblioAPI.Models; // Necesario para IPasswordHasher<UsuarioModel>
using Microsoft.AspNetCore.Identity; // Necesario para IPasswordHasher

var builder = WebApplication.CreateBuilder(args);

// 1. --- Configuraci�n de Servicios ---

// Habilitar controladores de API
builder.Services.AddControllers();

// Configuraci�n para Swagger/OpenAPI (documentaci�n y prueba de API)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Opcional: Configuraci�n adicional de Swagger (ej. informaci�n de la API)
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Version = "v1",
        Title = "BiblioAPI",
        Description = "API para el Sistema de Gesti�n de Biblioteca (SiGeBi)"
        // Puedes a�adir m�s informaci�n como Contacto, Licencia, etc.
    });
});

// *** Registro del Hasher de Contrase�as ***
// Se registra como Singleton, lo cual es eficiente para este servicio sin estado.
// Se especifica <UsuarioModel> para asociarlo con tu modelo de usuario.
builder.Services.AddSingleton<IPasswordHasher<UsuarioModel>, PasswordHasher<UsuarioModel>>();

// *** Registro de tus Servicios de L�gica de Negocio/Acceso a Datos ***
// Scoped: Se crea una nueva instancia de estos servicios por cada petici�n HTTP.
// Esto es bueno para servicios que usan recursos como DbContext o HttpClient.
builder.Services.AddScoped<UsuarioService>();
builder.Services.AddScoped<LibroService>();
builder.Services.AddScoped<PrestamoService>();
// Nota: IConfiguration ya est� disponible por defecto, no necesitas registrarlo manualmente.
// builder.Services.AddSingleton<IConfiguration>(builder.Configuration); //<- Esta l�nea es redundante

// *** Configuraci�n de CORS (Cross-Origin Resource Sharing) ***
// Esencial para permitir que tu aplicaci�n Frontend (BiblioApp, ejecut�ndose
// en un origen diferente - localhost con otro puerto) pueda llamar a esta API.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBiblioApp", // Nombre descriptivo de la pol�tica
        policyBuilder =>
        {
            // Lee la URL de la app frontend desde la configuraci�n (si la tienes)
            // string? frontendAppUrl = builder.Configuration["AllowedOrigins:BiblioApp"];

            // O especifica los or�genes permitidos directamente.
            // �IMPORTANTE! En producci�n, s� lo m�s espec�fico posible.
            // Para desarrollo, puedes permitir el origen de tu BiblioApp (ej. https://localhost:7060)
            policyBuilder
                //.WithOrigins(frontendAppUrl ?? "https://localhost:7060") // Permitir origen espec�fico (ajusta el puerto)
                .AllowAnyOrigin() // M�S PERMISIVO: Permite cualquier origen (�til para desarrollo, pero menos seguro para producci�n)
                .AllowAnyHeader() // Permitir cualquier cabecera (Authorization, Content-Type, etc.)
                .AllowAnyMethod(); // Permitir m�todos HTTP (GET, POST, PUT, DELETE, etc.)
        });
});


// 2. --- Construcci�n de la Aplicaci�n ---
var app = builder.Build();


// 3. --- Configuraci�n del Pipeline de Peticiones HTTP ---
// El orden del middleware es importante.

// Configuraci�n espec�fica para el entorno de Desarrollo
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage(); // Muestra p�ginas de error detalladas
    app.UseSwagger(); // Habilita el middleware de Swagger (genera el JSON)
    app.UseSwaggerUI(options => // Habilita la interfaz de usuario de Swagger
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "BiblioAPI v1");
        // Opcional: Ruta donde estar� disponible la UI de Swagger
        // options.RoutePrefix = string.Empty; // Para acceder desde la ra�z (/)
    });
}
else
{
    // En producci�n, usar un manejador de excepciones m�s gen�rico
    // app.UseExceptionHandler("/Error"); // Necesitar�as crear un endpoint /Error
    // Considera usar middleware de ProblemDetails para errores est�ndar:
    app.UseStatusCodePages(); // Devuelve respuestas simples para c�digos de error sin cuerpo
}

// Redirigir peticiones HTTP a HTTPS
app.UseHttpsRedirection();

// Aplicar la pol�tica CORS definida anteriormente
app.UseCors("AllowBiblioApp");

// Habilitar enrutamiento para que las peticiones lleguen a los controladores
app.UseRouting();

// Middleware de Autenticaci�n (si lo implementas m�s adelante)
// app.UseAuthentication();

// Middleware de Autorizaci�n (verifica permisos)
// Aseg�rate de tenerlo si usas atributos [Authorize] en los controladores/acciones.
app.UseAuthorization();

// Mapear las rutas definidas en los atributos [Route] de los controladores
app.MapControllers();

// 4. --- Ejecutar la Aplicaci�n ---
app.Run();
