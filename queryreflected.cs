using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

public static class DynamicEfQuery
{
    /// <summary>
    /// Ejecuta una consulta dinámica: resuelve la entidad por nombre de tabla o entidad,
    /// ordena por una propiedad y proyecta otra propiedad como object.
    /// </summary>
    public static IQueryable<object> Query(
        DbContext ctx,
        string tableOrEntityName,   // p.ej. "Customers" (tabla) o "Customer" (entidad)
        string orderByProperty,     // p.ej. "LastName"
        string selectProperty,      // p.ej. "Email"
        bool ascending = true)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (string.IsNullOrWhiteSpace(tableOrEntityName)) throw new ArgumentException("Required", nameof(tableOrEntityName));
        if (string.IsNullOrWhiteSpace(orderByProperty)) throw new ArgumentException("Required", nameof(orderByProperty));
        if (string.IsNullOrWhiteSpace(selectProperty)) throw new ArgumentException("Required", nameof(selectProperty));

        // 1) Resolver IEntityType por nombre de tabla o por nombre de entidad
        var entityType = ResolveEntityType(ctx, tableOrEntityName)
                         ?? throw new InvalidOperationException($"No se encontró una entidad mapeada a '{tableOrEntityName}'.");

        // 2) IQueryable no genérico
        var set = ctx.Set(entityType).AsQueryable();

        // 3) Validar que ambas propiedades existan en el modelo (escalares, no navegación)
        var orderProp = entityType.FindProperty(orderByProperty)
                       ?? throw new InvalidOperationException($"La propiedad de orden '{orderByProperty}' no existe en {entityType.DisplayName()}.");
        var selectProp = entityType.FindProperty(selectProperty)
                        ?? throw new InvalidOperationException($"La propiedad de salida '{selectProperty}' no existe en {entityType.DisplayName()}.");

        // 4) x => x.Prop (lambda tipada con el tipo real de la propiedad) para OrderBy
        var param = Expression.Parameter(entityType.ClrType, "x");
        var orderBody = Expression.Property(param, orderProp.PropertyInfo ?? throw new InvalidOperationException("Propiedad sin PropertyInfo."));
        var orderLambda = Expression.Lambda(orderBody, param); // tipo: Func<TEntity, TKey>

        // Llamada a Queryable.OrderBy/OrderByDescending vía reflexión y MakeGenericMethod
        var queryExpr = set.Expression;
        var orderMethodName = ascending ? nameof(Queryable.OrderBy) : nameof(Queryable.OrderByDescending);
        var orderedExpr = Expression.Call(
            typeof(Queryable),
            orderMethodName,
            new Type[] { entityType.ClrType, orderProp.ClrType },
            queryExpr,
            Expression.Quote(orderLambda)
        );

        var orderedQuery = set.Provider.CreateQuery(orderedExpr);

        // 5) Proyección: x => (object)x.SelectProp
        //    Usamos Expression.Convert para boxear valores por valor (int, DateTime, etc.)
        var selectBodyRaw = Expression.Property(param, selectProp.PropertyInfo!);
        var selectBodyBoxed = Expression.Convert(selectBodyRaw, typeof(object));
        var selectLambda = Expression.Lambda(selectBodyBoxed, param); // Func<TEntity, object>

        var selectExpr = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Select),
            new Type[] { entityType.ClrType, typeof(object) },
            orderedQuery.Expression,
            Expression.Quote(selectLambda)
        );

        var projected = orderedQuery.Provider.CreateQuery<object>(selectExpr);

        // Consejo opcional: AsNoTracking para consultas de solo lectura
        // (hay que volver a aplicar porque AsNoTracking es extensión genérica)
        // Aquí una alternativa segura usando EF.Property para forzar no tracking:
        return projected; // si quieres no-tracking, aplica sobre 'set' antes de construir las expresiones.
    }

    private static IEntityType? ResolveEntityType(DbContext ctx, string tableOrEntityName)
    {
        // 1) Por nombre de tabla
        var byTable = ctx.Model
            .GetEntityTypes()
            .FirstOrDefault(et => string.Equals(et.GetSchema() + "." + et.GetTableName(),
                                                tableOrEntityName, StringComparison.OrdinalIgnoreCase)
                               || string.Equals(et.GetTableName(), tableOrEntityName, StringComparison.OrdinalIgnoreCase));
        if (byTable != null) return byTable;

        // 2) Por DisplayName (nombre de entidad/clase)
        var byEntity = ctx.Model
            .GetEntityTypes()
            .FirstOrDefault(et => string.Equals(et.DisplayName(), tableOrEntityName, StringComparison.OrdinalIgnoreCase)
                               || string.Equals(et.ClrType.Name, tableOrEntityName, StringComparison.OrdinalIgnoreCase));
        return byEntity;
    }
}