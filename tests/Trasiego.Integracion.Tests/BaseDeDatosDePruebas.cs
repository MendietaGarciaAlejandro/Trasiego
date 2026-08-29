using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Trasiego.Infraestructura.Persistencia;

namespace Trasiego.Integracion.Tests;

/// <summary>
/// Crea una base de datos propia en LocalDB para la ejecucion y la tira al terminar.
/// </summary>
/// <remarks>
/// LocalDB y no un contenedor: aqui hace falta SQL Server de verdad (indices unicos,
/// precision de los decimal, el aislamiento de las transacciones), pero no hace falta que
/// sea el mismo binario que en produccion, y asi las pruebas corren sin tener que arrancar
/// Docker. La instancia de desarrollo tampoco se toca, que ahi vive la base de datos con
/// la que se prueba a mano.
/// </remarks>
public sealed class BaseDeDatosDePruebas : IAsyncLifetime
{
    private const string Instancia = @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;";

    private readonly string _nombre = "trasiego_pruebas_" + Guid.NewGuid().ToString("N")[..12];

    public string CadenaDeConexion => $"{Instancia}Database={_nombre};";

    public ContextoDeTrasiego Contexto() =>
        new(new DbContextOptionsBuilder<ContextoDeTrasiego>()
            .UseSqlServer(CadenaDeConexion)
            .Options);

    public async Task InitializeAsync()
    {
        await using var contexto = Contexto();
        await contexto.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        // Sin vaciar el pool, SQL Server no deja borrar la base de datos: quedan
        // conexiones abiertas contra ella aunque los DbContext ya esten cerrados.
        SqlConnection.ClearAllPools();

        await using var contexto = Contexto();
        await contexto.Database.EnsureDeletedAsync();
    }
}

[CollectionDefinition(nameof(ColeccionConBaseDeDatos))]
public class ColeccionConBaseDeDatos : ICollectionFixture<BaseDeDatosDePruebas>;
