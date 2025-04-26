-- Script SQL Completo para la Base de Datos BibliotecaDB
-- Versión con Restricciones y Lógica Adicional

-- 1. Creación de la Base de Datos
-- Asegúrate de que no exista antes de crearla o ejecuta esta parte manualmente.
-- Si ya existe, comenta o elimina las siguientes dos líneas.
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'BibliotecaDB')
BEGIN
    CREATE DATABASE BibliotecaDB;
END
GO

-- 2. Usar la Base de Datos Creada
USE BibliotecaDB;
GO

-- 3. Creación de Tablas con Restricciones Adicionales

-- Crear la tabla de Libros
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Libros]') AND type in (N'U'))
BEGIN
    CREATE TABLE Libros (
        Id INT PRIMARY KEY IDENTITY(1,1),
        Titulo NVARCHAR(100) NOT NULL,
        Autor NVARCHAR(100) NOT NULL,
        Editorial NVARCHAR(100) NOT NULL,
        ISBN NVARCHAR(20) NOT NULL UNIQUE, -- ISBN debe ser único
        Anio INT NOT NULL,
        Categoria NVARCHAR(50) NOT NULL,
        Existencias INT NOT NULL DEFAULT 0,
        -- Restricciones CHECK
        CONSTRAINT CHK_Libro_Anio CHECK (Anio > 1000 AND Anio <= YEAR(GETDATE())), -- Año razonable
        CONSTRAINT CHK_Libro_Existencias CHECK (Existencias >= 0) -- Existencias no negativas
    );
    PRINT 'Tabla Libros creada con restricciones.';
END
ELSE
BEGIN
    PRINT 'Tabla Libros ya existe.';
END
GO

-- Crear la tabla de Usuarios
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Usuarios]') AND type in (N'U'))
BEGIN
    CREATE TABLE Usuarios (
        Id INT PRIMARY KEY IDENTITY(1,1),
        Nombre NVARCHAR(50) NOT NULL,
        Apellido NVARCHAR(50) NOT NULL,
        Correo NVARCHAR(100) NOT NULL UNIQUE, -- Correo debe ser único
        Telefono NVARCHAR(20) NULL,
        TipoUsuario NVARCHAR(20) NOT NULL,
        Clave NVARCHAR(MAX) NOT NULL, -- Cambiado a MAX para Hashes largos. IMPORTANTE: Almacenar HASH, no texto plano.
        -- Restricciones CHECK
        CONSTRAINT CHK_Usuario_Tipo CHECK (TipoUsuario IN ('Estudiante', 'Docente', 'Administrador', 'Otro')) -- Limitar tipos de usuario
    );
    PRINT 'Tabla Usuarios creada con restricciones.';
END
ELSE
BEGIN
    PRINT 'Tabla Usuarios ya existe.';
END
GO

-- Crear la tabla de Préstamos
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Prestamos]') AND type in (N'U'))
BEGIN
    CREATE TABLE Prestamos (
        Id INT PRIMARY KEY IDENTITY(1,1),
        IdUsuario INT NOT NULL,
        IdLibro INT NOT NULL,
        FechaPrestamo DATETIME NOT NULL DEFAULT GETDATE(),
        FechaDevolucionEsperada DATETIME NOT NULL,
        FechaDevolucionReal DATETIME NULL,
        Estado NVARCHAR(20) NOT NULL, -- Ej: 'Pendiente', 'Devuelto', 'Atrasado'
        -- Restricciones CHECK
        CONSTRAINT CHK_Prestamo_Estado CHECK (Estado IN ('Pendiente', 'Devuelto', 'Atrasado')), -- Limitar estados
        CONSTRAINT CHK_Prestamo_Fechas CHECK (FechaDevolucionEsperada > FechaPrestamo), -- Devolución esperada después del préstamo
        CONSTRAINT CHK_Prestamo_FechaReal CHECK (FechaDevolucionReal IS NULL OR FechaDevolucionReal >= FechaPrestamo), -- Devolución real después o igual al préstamo
        -- Foreign Keys
        -- ON DELETE CASCADE puede ser peligroso. Si se borra un usuario/libro, se borran sus préstamos.
        -- Considerar ON DELETE NO ACTION y manejar la lógica en la aplicación o procedimientos.
        CONSTRAINT FK_Prestamos_Usuarios FOREIGN KEY (IdUsuario) REFERENCES Usuarios(Id) ON DELETE CASCADE,
        CONSTRAINT FK_Prestamos_Libros FOREIGN KEY (IdLibro) REFERENCES Libros(Id) ON DELETE CASCADE
    );
    PRINT 'Tabla Prestamos creada con restricciones.';
END
ELSE
BEGIN
    PRINT 'Tabla Prestamos ya existe.';
END
GO

-- 4. Creación de Procedimientos Almacenados (con lógica mejorada)

-- Procedimientos para Libros
PRINT 'Creando/Actualizando Procedimientos Almacenados para Libros...';
GO

IF OBJECT_ID('InsertarLibro', 'P') IS NOT NULL DROP PROCEDURE InsertarLibro;
GO
CREATE PROCEDURE InsertarLibro
    @Titulo NVARCHAR(100),
    @Autor NVARCHAR(100),
    @Editorial NVARCHAR(100),
    @ISBN NVARCHAR(20),
    @Anio INT,
    @Categoria NVARCHAR(50),
    @Existencias INT
AS
BEGIN
    -- Validaciones adicionales podrían ir aquí (ej. formato ISBN)
    INSERT INTO Libros (Titulo, Autor, Editorial, ISBN, Anio, Categoria, Existencias)
    VALUES (@Titulo, @Autor, @Editorial, @ISBN, @Anio, @Categoria, @Existencias);
END;
GO

IF OBJECT_ID('ObtenerLibros', 'P') IS NOT NULL DROP PROCEDURE ObtenerLibros;
GO
CREATE PROCEDURE ObtenerLibros
AS
BEGIN
    SELECT Id, Titulo, Autor, Editorial, ISBN, Anio, Categoria, Existencias FROM Libros;
END;
GO

IF OBJECT_ID('ActualizarLibro', 'P') IS NOT NULL DROP PROCEDURE ActualizarLibro;
GO
CREATE PROCEDURE ActualizarLibro
    @Id INT,
    @Titulo NVARCHAR(100),
    @Autor NVARCHAR(100),
    @Editorial NVARCHAR(100),
    @ISBN NVARCHAR(20),
    @Anio INT,
    @Categoria NVARCHAR(50),
    @Existencias INT
AS
BEGIN
    -- Validaciones adicionales podrían ir aquí
    UPDATE Libros
    SET Titulo = @Titulo,
        Autor = @Autor,
        Editorial = @Editorial,
        ISBN = @ISBN,
        Anio = @Anio,
        Categoria = @Categoria,
        Existencias = @Existencias
    WHERE Id = @Id;
END;
GO

IF OBJECT_ID('EliminarLibro', 'P') IS NOT NULL DROP PROCEDURE EliminarLibro;
GO
CREATE PROCEDURE EliminarLibro
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    -- Verificar si el libro tiene préstamos pendientes antes de eliminar
    IF EXISTS (SELECT 1 FROM Prestamos WHERE IdLibro = @Id AND Estado = 'Pendiente')
    BEGIN
        -- Lanzar un error indicando que no se puede eliminar
        THROW 50003, 'No se puede eliminar el libro porque tiene préstamos pendientes.', 1;
        RETURN; -- Detiene la ejecución
    END

    -- Si no hay préstamos pendientes, proceder a eliminar
    BEGIN TRY
        DELETE FROM Libros WHERE Id = @Id;
        PRINT 'Libro eliminado correctamente.';
    END TRY
    BEGIN CATCH
        PRINT 'Error al intentar eliminar el libro.';
        THROW; -- Re-lanzar el error original
    END CATCH
END;
GO

-- Procedimientos para Usuarios
PRINT 'Creando/Actualizando Procedimientos Almacenados para Usuarios...';
GO

IF OBJECT_ID('InsertarUsuario', 'P') IS NOT NULL DROP PROCEDURE InsertarUsuario;
GO
CREATE PROCEDURE InsertarUsuario
    @Nombre NVARCHAR(50),
    @Apellido NVARCHAR(50),
    @Correo NVARCHAR(100),
    @Telefono NVARCHAR(20),
    @TipoUsuario NVARCHAR(20),
    @ClaveHash NVARCHAR(MAX) -- Recibe el HASH de la clave
AS
BEGIN
    -- Validaciones (ej. formato correo)
    IF NOT @Correo LIKE '%_@__%.__%'
    BEGIN
         THROW 50004, 'Formato de correo electrónico inválido.', 1;
         RETURN;
    END

    INSERT INTO Usuarios (Nombre, Apellido, Correo, Telefono, TipoUsuario, Clave)
    VALUES (@Nombre, @Apellido, @Correo, @Telefono, @TipoUsuario, @ClaveHash); -- Insertar Hash
END;
GO

IF OBJECT_ID('ObtenerUsuarios', 'P') IS NOT NULL DROP PROCEDURE ObtenerUsuarios;
GO
CREATE PROCEDURE ObtenerUsuarios
AS
BEGIN
    SELECT Id, Nombre, Apellido, Correo, Telefono, TipoUsuario -- No devolver la clave/hash
    FROM Usuarios;
END;
GO

IF OBJECT_ID('ActualizarUsuario', 'P') IS NOT NULL DROP PROCEDURE ActualizarUsuario;
GO
CREATE PROCEDURE ActualizarUsuario
    @Id INT,
    @Nombre NVARCHAR(50),
    @Apellido NVARCHAR(50),
    @Correo NVARCHAR(100),
    @Telefono NVARCHAR(20),
    @TipoUsuario NVARCHAR(20),
    @ClaveHash NVARCHAR(MAX) = NULL -- Opcional: Recibe nuevo Hash si se quiere cambiar clave
AS
BEGIN
    -- Validaciones
    IF @Correo IS NOT NULL AND NOT @Correo LIKE '%_@__%.__%'
    BEGIN
         THROW 50004, 'Formato de correo electrónico inválido.', 1;
         RETURN;
    END

    UPDATE Usuarios
    SET Nombre = ISNULL(@Nombre, Nombre), -- Actualiza solo si no es NULL
        Apellido = ISNULL(@Apellido, Apellido),
        Correo = ISNULL(@Correo, Correo),
        Telefono = ISNULL(@Telefono, Telefono),
        TipoUsuario = ISNULL(@TipoUsuario, TipoUsuario),
        -- Actualiza la clave solo si se proporciona un nuevo hash
        Clave = CASE WHEN @ClaveHash IS NOT NULL THEN @ClaveHash ELSE Clave END
    WHERE Id = @Id;
END;
GO

IF OBJECT_ID('EliminarUsuario', 'P') IS NOT NULL DROP PROCEDURE EliminarUsuario;
GO
CREATE PROCEDURE EliminarUsuario
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    -- Verificar si el usuario tiene préstamos pendientes
    IF EXISTS (SELECT 1 FROM Prestamos WHERE IdUsuario = @Id AND Estado = 'Pendiente')
    BEGIN
        -- Lanzar un error
        THROW 50005, 'No se puede eliminar el usuario porque tiene préstamos pendientes.', 1;
        RETURN;
    END

    -- Proceder a eliminar si no hay préstamos pendientes
    BEGIN TRY
        DELETE FROM Usuarios WHERE Id = @Id;
        PRINT 'Usuario eliminado correctamente.';
    END TRY
    BEGIN CATCH
        PRINT 'Error al intentar eliminar el usuario.';
        THROW;
    END CATCH
END;
GO

IF OBJECT_ID('ValidarUsuario', 'P') IS NOT NULL DROP PROCEDURE ValidarUsuario;
GO
CREATE PROCEDURE ValidarUsuario
    @Correo NVARCHAR(100),
    @ClaveProporcionada NVARCHAR(100) -- Recibe la clave en texto plano para comparar con el Hash
AS
BEGIN
    -- ** LÓGICA DE VALIDACIÓN CON HASH DEBE IMPLEMENTARSE EN LA APLICACIÓN (C#) **
    -- Este SP solo recupera los datos necesarios para que la aplicación valide.
    -- La aplicación debe:
    -- 1. Obtener el hash almacenado para el @Correo.
    -- 2. Comparar el hash de @ClaveProporcionada con el hash almacenado usando la misma librería/algoritmo.
    -- 3. Si coinciden, la validación es exitosa.

    SELECT Id, Nombre, Apellido, Correo, Telefono, TipoUsuario, Clave AS ClaveHash -- Devolver el Hash para validar en C#
    FROM Usuarios
    WHERE Correo = @Correo;
END;
GO

-- Procedimientos para Préstamos
PRINT 'Creando/Actualizando Procedimientos Almacenados para Préstamos...';
GO

IF OBJECT_ID('RegistrarPrestamo', 'P') IS NOT NULL DROP PROCEDURE RegistrarPrestamo;
GO
CREATE PROCEDURE RegistrarPrestamo
    @IdUsuario INT,
    @IdLibro INT,
    @FechaDevolucionEsperada DATETIME,
    @Estado NVARCHAR(20) = 'Pendiente'
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ExistenciasActuales INT;
    DECLARE @UsuarioExiste INT;

    -- Verificar que el usuario exista
    SELECT @UsuarioExiste = COUNT(*) FROM Usuarios WHERE Id = @IdUsuario;
    IF @UsuarioExiste = 0
    BEGIN
        THROW 50006, 'El usuario especificado no existe.', 1;
        RETURN;
    END

    -- Verificar existencias del libro
    SELECT @ExistenciasActuales = Existencias FROM Libros WHERE Id = @IdLibro;
    IF @ExistenciasActuales IS NULL -- El libro no existe
    BEGIN
        THROW 50007, 'El libro especificado no existe.', 1;
        RETURN;
    END
    IF @ExistenciasActuales <= 0 -- No hay existencias
    BEGIN
        THROW 50001, 'No hay existencias disponibles para el libro solicitado.', 1;
        RETURN;
    END

    -- Verificar fecha de devolución esperada
    IF @FechaDevolucionEsperada <= GETDATE()
    BEGIN
        THROW 50008, 'La fecha de devolución esperada debe ser posterior a la fecha actual.', 1;
        RETURN;
    END

    -- Proceder con la transacción
    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO Prestamos (IdUsuario, IdLibro, FechaDevolucionEsperada, Estado)
        VALUES (@IdUsuario, @IdLibro, @FechaDevolucionEsperada, @Estado);

        UPDATE Libros
        SET Existencias = Existencias - 1
        WHERE Id = @IdLibro;

        COMMIT TRANSACTION;
        PRINT 'Préstamo registrado y existencias actualizadas.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        PRINT 'Error al registrar el préstamo. Se revirtió la transacción.';
        THROW;
    END CATCH
END;
GO

IF OBJECT_ID('ObtenerPrestamos', 'P') IS NOT NULL DROP PROCEDURE ObtenerPrestamos;
GO
CREATE PROCEDURE ObtenerPrestamos
AS
BEGIN
    -- Opcionalmente unir con Libros y Usuarios para obtener más detalles
    SELECT
        p.Id,
        p.IdUsuario,
        u.Nombre + ' ' + u.Apellido AS NombreUsuario,
        p.IdLibro,
        l.Titulo AS TituloLibro,
        p.FechaPrestamo,
        p.FechaDevolucionEsperada,
        p.FechaDevolucionReal,
        p.Estado
    FROM Prestamos p
    INNER JOIN Usuarios u ON p.IdUsuario = u.Id
    INNER JOIN Libros l ON p.IdLibro = l.Id;
END;
GO

IF OBJECT_ID('ActualizarPrestamo', 'P') IS NOT NULL DROP PROCEDURE ActualizarPrestamo;
GO
CREATE PROCEDURE ActualizarPrestamo -- Para registrar devolución o cambiar estado
    @Id INT,
    @FechaDevolucionReal DATETIME = NULL, -- Hacerla opcional
    @Estado NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdLibro INT;
    DECLARE @EstadoActual NVARCHAR(20);
    DECLARE @PrestamoExiste INT;

    -- Verificar que el préstamo exista
    SELECT @PrestamoExiste = COUNT(*) FROM Prestamos WHERE Id = @Id;
    IF @PrestamoExiste = 0
    BEGIN
        THROW 50009, 'El préstamo especificado no existe.', 1;
        RETURN;
    END

    -- Obtener el estado actual y el IdLibro
    SELECT @EstadoActual = Estado, @IdLibro = IdLibro
    FROM Prestamos
    WHERE Id = @Id;

    -- Validar nuevo estado
    IF @Estado NOT IN ('Pendiente', 'Devuelto', 'Atrasado')
    BEGIN
        THROW 50010, 'El estado proporcionado no es válido.', 1;
        RETURN;
    END

    -- Validar FechaDevolucionReal si el estado es 'Devuelto'
    IF @Estado = 'Devuelto' AND @FechaDevolucionReal IS NULL
    BEGIN
        THROW 50011, 'Se debe proporcionar una FechaDevolucionReal para marcar el préstamo como Devuelto.', 1;
        RETURN;
    END

    -- Evitar actualizar si ya está devuelto (a menos que se quiera cambiar a otro estado, lo cual sería raro)
    IF @EstadoActual = 'Devuelto' AND @Estado = 'Devuelto'
    BEGIN
         PRINT 'El préstamo ya está marcado como Devuelto.';
         RETURN;
    END

    -- Proceder con la transacción
    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE Prestamos
        SET FechaDevolucionReal = @FechaDevolucionReal, -- Actualizar fecha si se proporciona
            Estado = @Estado
        WHERE Id = @Id;

        -- Ajustar existencias solo si el estado cambia A 'Devuelto' desde un estado anterior NO devuelto
        IF @Estado = 'Devuelto' AND @EstadoActual <> 'Devuelto'
        BEGIN
            UPDATE Libros
            SET Existencias = Existencias + 1
            WHERE Id = @IdLibro;
            PRINT 'Existencias del libro incrementadas.';
        END
        -- Considerar si se debe decrementar existencias si se cambia de 'Devuelto' a 'Pendiente' (poco común)

        COMMIT TRANSACTION;
        PRINT 'Préstamo actualizado y existencias ajustadas (si aplica).';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        PRINT 'Error al actualizar el préstamo. Se revirtió la transacción.';
        THROW;
    END CATCH
END;
GO

IF OBJECT_ID('EliminarPrestamo', 'P') IS NOT NULL DROP PROCEDURE EliminarPrestamo;
GO
CREATE PROCEDURE EliminarPrestamo
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @IdLibro INT;
    DECLARE @EstadoActual NVARCHAR(20);

    -- Obtener datos antes de eliminar para posible lógica de revertir stock
    SELECT @EstadoActual = Estado, @IdLibro = IdLibro FROM Prestamos WHERE Id = @Id;

    IF @EstadoActual IS NULL
    BEGIN
        PRINT 'El préstamo no existe.';
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        DELETE FROM Prestamos WHERE Id = @Id;

        -- Opcional: Si se elimina un préstamo 'Pendiente', ¿debería devolverse el libro al stock?
        IF @EstadoActual = 'Pendiente'
        BEGIN
             UPDATE Libros SET Existencias = Existencias + 1 WHERE Id = @IdLibro;
             PRINT 'Existencias del libro incrementadas debido a eliminación de préstamo pendiente.';
        END

        COMMIT TRANSACTION;
        PRINT 'Préstamo eliminado correctamente.';
    END TRY
    BEGIN CATCH
         IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        PRINT 'Error al intentar eliminar el préstamo.';
        THROW;
    END CATCH
END;
GO

PRINT '*** Script de Base de Datos (con restricciones) completado exitosamente! ***';
GO
```

**Cambios Realizados:**

1.  **Restricciones `CHECK`:** Añadidas a las tablas `Libros`, `Usuarios` y `Prestamos` para validar años, existencias, tipos de usuario, estados de préstamo y coherencia de fechas.
2.  **Restricciones `UNIQUE`:** Añadidas a `Libros.ISBN` y `Usuarios.Correo`.
3.  **Tipo de Dato `Clave`:** Cambiado a `NVARCHAR(MAX)` en `Usuarios` para acomodar hashes largos.
4.  **Procedimiento `EliminarLibro`:** Ahora verifica si existen préstamos pendientes para ese libro antes de permitir la eliminación.
5.  **Procedimiento `EliminarUsuario`:** Ahora verifica si el usuario tiene préstamos pendientes antes de permitir la eliminación.
6.  **Procedimiento `InsertarUsuario`:** Añadida validación básica de formato de correo y renombrado el parámetro `@Clave` a `@ClaveHash` para mayor claridad (la lógica de hashing real debe estar en C#).
7.  **Procedimiento `ActualizarUsuario`:** Permite actualizar campos individualmente y la clave solo si se proporciona un nuevo hash.
8.  **Procedimiento `ValidarUsuario`:** Modificado para devolver el hash almacenado (`ClaveHash`) para que la lógica de comparación de hashes se realice en la aplicación C#.
9.  **Procedimiento `RegistrarPrestamo`:** Añadidas verificaciones para asegurar que el usuario y el libro existen, que hay existencias y que la fecha de devolución es futura.
10. **Procedimiento `ActualizarPrestamo`:** Mejorada la lógica para manejar la actualización de estado y la fecha de devolución real, ajustando las existencias solo cuando un préstamo pasa a estado 'Devuelto'.
11. **Procedimiento `EliminarPrestamo`:** Añadida lógica opcional para devolver el libro al stock si se elimina un préstamo que estaba 'Pendiente'.
12. **Procedimiento `ObtenerPrestamos`:** Modificado para unir con `Usuarios` y `Libros` y devolver información más descriptiva.
13. **Comentarios:** Añadidos comentarios adicionales explicando las restricciones y la lógica.

Este script es más robusto y ayuda a mantener la integridad de los datos en la base de datos. Recuerda que la lógica de hashing de contraseñas debe implementarse en tu código 


-- Script para crear el Stored Procedure ObtenerUsuarioPorId
-- Selecciona un usuario por su ID, excluyendo la clave/hash.

USE BibliotecaDB; -- Asegúrate de estar en la base de datos correcta
GO

PRINT 'Creando/Actualizando Stored Procedure ObtenerUsuarioPorId...';
GO

IF OBJECT_ID('ObtenerUsuarioPorId', 'P') IS NOT NULL
    DROP PROCEDURE ObtenerUsuarioPorId;
GO

CREATE PROCEDURE ObtenerUsuarioPorId
    @Id INT -- Parámetro de entrada: el ID del usuario a buscar
AS
BEGIN
    SET NOCOUNT ON; -- Evita mensajes de 'rows affected'

    -- Seleccionar todos los campos necesarios EXCEPTO la clave/hash
    SELECT
        Id,
        Nombre,
        Apellido,
        Correo,
        Telefono,
        TipoUsuario
    FROM
        Usuarios
    WHERE
        Id = @Id;

    -- El procedimiento devolverá una fila si el ID existe, o ninguna fila si no existe.
END;
GO

PRINT 'Stored Procedure ObtenerUsuarioPorId creado/actualizado exitosamente.';
GO

-- Script para crear el Stored Procedure ObtenerLibroPorId
-- Selecciona un libro por su ID.

USE BibliotecaDB; -- Asegúrate de estar en la base de datos correcta
GO

PRINT 'Creando/Actualizando Stored Procedure ObtenerLibroPorId...';
GO

IF OBJECT_ID('ObtenerLibroPorId', 'P') IS NOT NULL
    DROP PROCEDURE ObtenerLibroPorId;
GO

CREATE PROCEDURE ObtenerLibroPorId
    @Id INT -- Parámetro de entrada: el ID del libro a buscar
AS
BEGIN
    SET NOCOUNT ON; -- Evita mensajes de 'rows affected'

    -- Seleccionar todos los campos necesarios del libro
    SELECT
        Id,
        Titulo,
        Autor,
        Editorial,
        ISBN,
        Anio,
        Categoria,
        Existencias
    FROM
        Libros
    WHERE
        Id = @Id;

    -- Devolverá una fila si el ID existe, o ninguna si no existe.
END;
GO

PRINT 'Stored Procedure ObtenerLibroPorId creado/actualizado exitosamente.';
GO

-- Script para ACTUALIZAR el Stored Procedure ObtenerLibros
-- Añade parámetros opcionales para filtrar por Título y/o Autor.

USE BibliotecaDB; -- Asegúrate de estar en la base de datos correcta
GO

PRINT 'Actualizando Stored Procedure ObtenerLibros para incluir filtros...';
GO

-- Eliminar el procedimiento existente si existe
IF OBJECT_ID('ObtenerLibros', 'P') IS NOT NULL
    DROP PROCEDURE ObtenerLibros;
GO

-- Crear el nuevo procedimiento con parámetros de filtro
CREATE PROCEDURE ObtenerLibros
    -- Parámetros opcionales para los filtros. NULL o vacío significa no filtrar por ese campo.
    @TituloFilter NVARCHAR(100) = NULL,
    @AutorFilter NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        Id,
        Titulo,
        Autor,
        Editorial,
        ISBN,
        Anio,
        Categoria,
        Existencias
    FROM
        Libros
    WHERE
        -- Condición para filtrar por Título (si @TituloFilter no es NULL ni vacío)
        (@TituloFilter IS NULL OR @TituloFilter = '' OR Titulo LIKE '%' + @TituloFilter + '%')
        AND -- Condición para filtrar por Autor (si @AutorFilter no es NULL ni vacío)
        (@AutorFilter IS NULL OR @AutorFilter = '' OR Autor LIKE '%' + @AutorFilter + '%')
    ORDER BY
        Titulo; -- Opcional: ordenar los resultados

END;
GO

PRINT 'Stored Procedure ObtenerLibros actualizado exitosamente con filtros.';
GO
