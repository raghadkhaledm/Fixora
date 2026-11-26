using System.Collections;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Data;

public static class EntityLoader
{
    public static T Load<T>(this AppDbContext db, T entity, params string[] paths) where T : class
    {
        foreach (var path in paths)
        {
            LoadPath(db, entity, path.Split('.'), 0, false);
        }

        return entity;
    }

    public static T LoadMissing<T>(this AppDbContext db, T entity, params string[] paths) where T : class
    {
        foreach (var path in paths)
        {
            LoadPath(db, entity, path.Split('.'), 0, true);
        }

        return entity;
    }

    private static void LoadPath(AppDbContext db, object? entity, string[] segments, int index, bool onlyMissing)
    {
        if (entity == null || index >= segments.Length)
        {
            return;
        }

        var name = Capitalize(segments[index]);
        var entry = db.Entry(entity);
        var nav = entry.Metadata.FindNavigation(name);
        if (nav == null)
        {
            return;
        }

        var navEntry = entry.Navigation(name);
        if (!onlyMissing || !navEntry.IsLoaded)
        {
            navEntry.Load();
        }

        var value = entry.Metadata.ClrType.GetProperty(name)?.GetValue(entity);
        if (value is IEnumerable items && value is not string)
        {
            foreach (var item in items)
            {
                LoadPath(db, item, segments, index + 1, onlyMissing);
            }
        }
        else
        {
            LoadPath(db, value, segments, index + 1, onlyMissing);
        }
    }

    private static string Capitalize(string s)
    {
        return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
