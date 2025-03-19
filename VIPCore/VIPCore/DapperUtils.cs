using System.ComponentModel.DataAnnotations.Schema;
using Dapper;

namespace VIPCore;

public interface IDapperObject
{
}

public static class DapperUtils
{
    public static void MapProperties<T>()
    {
        foreach (var type in typeof(T).Assembly.GetTypes().Where(t => t.IsAssignableTo(typeof(IDapperObject))))
        {
            Map(type);
        }
    }

    private static void Map(Type t)
    {
        SqlMapper.SetTypeMap(t, new CustomPropertyTypeMap(t, (type, columnName) =>
            type.GetProperties()
                .FirstOrDefault(prop => prop.GetCustomAttributes(false)
                    .OfType<ColumnAttribute>()
                    .Any(attr => attr.Name == columnName))!));
    }
}