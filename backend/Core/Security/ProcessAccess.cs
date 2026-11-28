using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Data;

namespace TecNM.Api.Core.Security;

public static class ProcessAccess
{
    public static bool ReadsAll(this ICurrentUser user) => user.IsInRole("department_head");
    public static bool CanReadProcess(this ICurrentUser user, string type) => user.ReadsAll()
        || type == "social_service" && user.IsInRole("social_service_officer")
        || type == "internship" && user.IsInRole("internship_coordinator");

    public static void RequireProcess(this ICurrentUser user, string type)
    {
        if (!user.CanReadProcess(type))
            throw new AppException(403, "auth.forbidden", "No tienes acceso a este proceso.");
    }

    public static IQueryable<Student> VisibleStudents(this ICurrentUser user, TecNMDbContext db, bool preAdmission = false)
    {
        if (user.ReadsAll()) return db.Students;
        var ss = user.IsInRole("social_service_officer");
        var pp = user.IsInRole("internship_coordinator");
        var student = user.IsInRole("student");
        return db.Students.Where(s => student && s.UserId == user.Id
            || ss && db.SocialServices.Any(t => t.StudentId == s.Id)
            || pp && db.Internships.Any(t => t.StudentId == s.Id)
            || preAdmission && (ss || pp)
                && !db.SocialServices.Any(t => t.StudentId == s.Id)
                && !db.Internships.Any(t => t.StudentId == s.Id));
    }

    public static async Task RequireStudentAsync(this ICurrentUser user, TecNMDbContext db, long id, CancellationToken ct, bool preAdmission = false)
    {
        if (!await user.VisibleStudents(db, preAdmission).AnyAsync(s => s.Id == id, ct))
            throw new NotFoundException("students.not_found", "El expediente solicitado no existe.");
    }

    public static IQueryable<ExpedientDocument> VisibleDocuments(this ICurrentUser user, TecNMDbContext db, IQueryable<ExpedientDocument> query)
    {
        if (user.ReadsAll()) return query;
        var ss = user.IsInRole("social_service_officer");
        var pp = user.IsInRole("internship_coordinator");
        var student = user.IsInRole("student");
        var generalIds = user.VisibleStudents(db, true).Select(s => s.Id);
        return query.Where(d => student && db.Students.Any(s => s.Id == d.StudentId && s.UserId == user.Id)
            || ss && d.TramiteType == "social_service"
            || pp && d.TramiteType == "internship"
            || d.TramiteType == "general" && generalIds.Contains(d.StudentId));
    }
}
