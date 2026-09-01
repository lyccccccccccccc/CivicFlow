using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CivicFlow.Infrastructure.Persistence;

public sealed record SchemaValidationResult(bool IsValid, IReadOnlyList<string> Differences);

public sealed class SchemaBaselineValidator(ApplicationDbContext db)
{
    public async Task<SchemaValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        if (!db.Database.IsSqlServer())
            return new(false, ["Legacy baseline registration is supported only for SQL Server."]);

        var expected = ExpectedSchema();
        var actual = await ActualSchemaAsync(cancellationToken);
        var differences = expected.Except(actual, StringComparer.OrdinalIgnoreCase).Select(x => $"Missing: {x}")
            .Concat(actual.Except(expected, StringComparer.OrdinalIgnoreCase).Select(x => $"Unexpected: {x}"))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        return new(differences.Count == 0, differences);
    }

    private HashSet<string> ExpectedSchema()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in db.Model.GetRelationalModel().Tables)
        {
            var tableName = Name(table.Schema, table.Name);
            result.Add($"table|{tableName}");
            foreach (var column in table.Columns)
                result.Add($"column|{tableName}|{column.Name}|{NormalizeType(column.StoreType)}|{column.IsNullable}");

            if (table.PrimaryKey is { } primaryKey)
                result.Add($"pk|{tableName}|{string.Join(',', primaryKey.Columns.Select(x => x.Name))}");

            foreach (var index in table.Indexes)
                result.Add($"index|{tableName}|{index.IsUnique}|{string.Join(',', index.Columns.Select(x => x.Name))}|{NormalizeFilter(index.Filter)}");

            foreach (var foreignKey in table.ForeignKeyConstraints)
                result.Add($"fk|{tableName}|{string.Join(',', foreignKey.Columns.Select(x => x.Name))}|{Name(foreignKey.PrincipalTable.Schema, foreignKey.PrincipalTable.Name)}|{string.Join(',', foreignKey.PrincipalColumns.Select(x => x.Name))}|{DeleteAction(foreignKey.OnDeleteAction)}");
        }
        return result;
    }

    private async Task<HashSet<string>> ActualSchemaAsync(CancellationToken cancellationToken)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);

        await ReadAsync(connection, """
            SELECT s.name, t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
            WHERE t.name <> '__EFMigrationsHistory'
            """, reader => result.Add($"table|{Name(reader.GetString(0), reader.GetString(1))}"), cancellationToken);

        await ReadAsync(connection, """
            SELECT s.name, t.name, c.name,
              CASE WHEN ty.name IN ('nvarchar','nchar') THEN ty.name+'('+(CASE WHEN c.max_length=-1 THEN 'max' ELSE CONVERT(varchar(10),c.max_length/2) END)+')'
                   WHEN ty.name IN ('varchar','char','varbinary','binary') THEN ty.name+'('+(CASE WHEN c.max_length=-1 THEN 'max' ELSE CONVERT(varchar(10),c.max_length) END)+')'
                   WHEN ty.name IN ('decimal','numeric') THEN ty.name+'('+CONVERT(varchar(10),c.precision)+','+CONVERT(varchar(10),c.scale)+')'
                   ELSE ty.name END, c.is_nullable
            FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
            JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
            WHERE t.name <> '__EFMigrationsHistory'
            """, reader => result.Add($"column|{Name(reader.GetString(0), reader.GetString(1))}|{reader.GetString(2)}|{NormalizeType(reader.GetString(3))}|{reader.GetBoolean(4)}"), cancellationToken);

        await ReadAsync(connection, """
            SELECT s.name,t.name,STRING_AGG(c.name,',') WITHIN GROUP (ORDER BY ic.key_ordinal)
            FROM sys.key_constraints kc JOIN sys.tables t ON t.object_id=kc.parent_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
            JOIN sys.index_columns ic ON ic.object_id=t.object_id AND ic.index_id=kc.unique_index_id
            JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=ic.column_id
            WHERE kc.type='PK' GROUP BY s.name,t.name
            """, reader => result.Add($"pk|{Name(reader.GetString(0), reader.GetString(1))}|{reader.GetString(2)}"), cancellationToken);

        await ReadAsync(connection, """
            SELECT s.name,t.name,i.is_unique,STRING_AGG(c.name,',') WITHIN GROUP (ORDER BY ic.key_ordinal),COALESCE(i.filter_definition,'')
            FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
            JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.is_included_column=0
            JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=ic.column_id
            WHERE i.is_primary_key=0 AND i.is_unique_constraint=0 AND i.is_hypothetical=0 AND t.name <> '__EFMigrationsHistory'
            GROUP BY s.name,t.name,i.index_id,i.is_unique,i.filter_definition
            """, reader => result.Add($"index|{Name(reader.GetString(0), reader.GetString(1))}|{reader.GetBoolean(2)}|{reader.GetString(3)}|{NormalizeFilter(reader.GetString(4))}"), cancellationToken);

        await ReadAsync(connection, """
            SELECT ps.name,pt.name,
              STRING_AGG(pc.name,',') WITHIN GROUP (ORDER BY fkc.constraint_column_id),
              rs.name,rt.name,
              STRING_AGG(rc.name,',') WITHIN GROUP (ORDER BY fkc.constraint_column_id),fk.delete_referential_action_desc
            FROM sys.foreign_keys fk JOIN sys.tables pt ON pt.object_id=fk.parent_object_id JOIN sys.schemas ps ON ps.schema_id=pt.schema_id
            JOIN sys.tables rt ON rt.object_id=fk.referenced_object_id JOIN sys.schemas rs ON rs.schema_id=rt.schema_id
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id
            JOIN sys.columns pc ON pc.object_id=pt.object_id AND pc.column_id=fkc.parent_column_id
            JOIN sys.columns rc ON rc.object_id=rt.object_id AND rc.column_id=fkc.referenced_column_id
            GROUP BY ps.name,pt.name,rs.name,rt.name,fk.delete_referential_action_desc
            """, reader => result.Add($"fk|{Name(reader.GetString(0), reader.GetString(1))}|{reader.GetString(2)}|{Name(reader.GetString(3), reader.GetString(4))}|{reader.GetString(5)}|{reader.GetString(6)}"), cancellationToken);
        return result;
    }

    private static async Task ReadAsync(DbConnection connection, string sql, Action<DbDataReader> add, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) add(reader);
    }

    private static string Name(string? schema, string table) => $"{schema ?? "dbo"}.{table}";
    private static string NormalizeType(string value) => value.Replace(" ", string.Empty).ToLowerInvariant();
    private static string NormalizeFilter(string? value) => (value ?? string.Empty).Replace("[", string.Empty).Replace("]", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
    private static string DeleteAction(ReferentialAction action) => action switch
    {
        ReferentialAction.Cascade => "CASCADE", ReferentialAction.SetNull => "SET_NULL",
        ReferentialAction.SetDefault => "SET_DEFAULT", _ => "NO_ACTION"
    };
}
