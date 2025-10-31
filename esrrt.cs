using System;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

public interface IEntity { /* tus miembros, p.ej. Id */ }

public static class EfTableResolver
{
    /// <summary>
    /// Devuelve el CLR type de la entidad que mapea a la tabla dada (y esquema opcional).
    /// Si la tabla está compartida por varias entidades, intenta devolver el tipo raíz.
    /// </summary>
    public static Type? ResolveEntityClrType(DbContext context, string tableName, string? schema = null)
    {
        // En EF Core 8 es más fiable pasar por los TableMappings (soporta TPT/TPC/splits)
        var matches = context.Model
            .GetEntityTypes()
            .SelectMany(et => et.GetTableMappings()
                                .Select(tm => new { et.ClrType, Table = tm.Table }))
            .Where(x =>
                string.Equals(x.Table.Name, tableName, StringComparison.OrdinalIgnoreCase) &&
                (schema == null || string.Equals(x.Table.Schema ?? "dbo", schema, StringComparison.OrdinalIgnoreCase)))
            .Select(x => x.ClrType)
            .Distinct()
            .ToList();

        if (matches.Count == 0) return null;
        if (matches.Count == 1) return matches[0];

        // Si varias entidades comparten la misma tabla, prioriza la raíz de la jerarquía
        var roots = matches.Where(t => t.BaseType == null || !matches.Contains(t.BaseType)).ToList();
        if (roots.Count == 1) return roots[0];

        throw new InvalidOperationException(
            $"La tabla '{schema ?? "dbo"}.{tableName}' está compartida por múltiples entidades y no es posible resolver un tipo único.");
    }

    /// <summary>
    /// Devuelve un IQueryable (no genérico) para la tabla dada.
    /// </summary>
    public static IQueryable GetQueryable(DbContext context, string tableName, string? schema = null)
    {
        var clr = ResolveEntityClrType(context, tableName, schema)
                  ?? throw new InvalidOperationException($"No hay entidad mapeada a '{schema ?? "dbo"}.{tableName}'.");
        // DbContext.Set(Type) está disponible en EF Core y devuelve un DbSet no genérico
        return context.Set(clr);
    }

    /// <summary>
    /// Devuelve un IQueryable<IEntity> para operar tipado por la interfaz.
    /// </summary>
    public static IQueryable<IEntity> GetQueryableAsIEntity(DbContext context, string tableName, string? schema = null)
    {
        var query = GetQueryable(context, tableName, schema);
        return query.Cast<IEntity>();
    }
}


// 1) Obtener el CLR type de la entidad por nombre de tabla
var clrType = EfTableResolver.ResolveEntityClrType(db, "Users"); // typeof(User)

// 2) Obtener un IQueryable no genérico y filtrar vía dinámico/reflection
var q = EfTableResolver.GetQueryable(db, "Users"); // IQueryable
// si tu interfaz IEntity es implementada por User:
var typed = q.Cast<IEntity>(); // IQueryable<IEntity>

// 3) Hacer una ordenación por un campo conocido
var ordered = typed.OrderBy(e => /* e.Id, o lo que exponga IEntity */);

// 4) Si necesitas un DbSet genérico (por reflección):
var method = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(clrType!);
var genericSet = (IQueryable)method.Invoke(db, null)!;
