using Fixora.Models;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Data;

public static class SyncExtensions
{
    public static void SyncServices(this AppDbContext db, Technician technician, IEnumerable<int> serviceIds)
    {
        var ids = serviceIds.Distinct().ToList();
        db.Entry(technician).Collection(t => t.Services).Load();
        foreach (var existing in technician.Services.Where(s => !ids.Contains(s.Service_ID)).ToList())
        {
            technician.Services.Remove(existing);
        }
        var have = technician.Services.Select(s => s.Service_ID).ToHashSet();
        foreach (var service in db.Services.Where(s => ids.Contains(s.Service_ID) && !have.Contains(s.Service_ID)).ToList())
        {
            technician.Services.Add(service);
        }
        db.SaveChanges();
    }
}
