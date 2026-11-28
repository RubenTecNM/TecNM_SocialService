using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Storage;
using TecNM.Api.Data;
using TecNM.Api.Modules.Expedients;

internal static class DatabaseChecks
{
    public static async Task RunAsync()
    {
        var connection = Environment.GetEnvironmentVariable("DOCUMENT_TEST_CONNECTION");
        if (string.IsNullOrEmpty(connection)) { Console.WriteLine("PostgreSQL integration checks skipped: set DOCUMENT_TEST_CONNECTION to isolated document_checks."); return; }
        if (new NpgsqlConnectionStringBuilder(connection).Database != "document_checks") throw new Exception("Tests require isolated database document_checks.");
        var options = new DbContextOptionsBuilder<TecNMDbContext>().UseNpgsql(connection).Options;
        await using var db = new TecNMDbContext(options);
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero));
        var storage = new LocalFileStorage(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:RootPath"] = Path.GetFullPath("tmp/pdf-qa/db-storage") }).Build());
        var service = new DocumentsService(db, new TestUser(["department_head"], 5), storage, clock);
        var studentService = new DocumentsService(db, new TestUser(["student"], 2), storage, clock);
        var bytes = File.ReadAllBytes("backend/Resources/Documents/templates/pp-evaluation.pdf");
        IFormFile Upload(byte[] value) => new FormFile(new MemoryStream(value), 0, value.Length, "file", "imss.pdf");
        var uploaded = await studentService.UploadDocumentAsync(1, Upload(bytes), "general", null, "IMSS");
        Ensure(uploaded.Status == "pending", "IMSS upload stays pending");
        Ensure((await studentService.UploadDocumentAsync(1, Upload(bytes), "general", null, "IMSS")).Id == uploaded.Id, "Identical upload is idempotent");
        var eligibility = new ImssEligibilityService(db, clock);
        try { await eligibility.RequireApprovedAsync(1); throw new Exception("Pending IMSS admitted"); }
        catch (BusinessRuleException) { }
        var approved = await service.ReviewDocumentAsync(uploaded.Id, new("approved", null, new DateOnly(2026, 10, 1)));
        Ensure(approved.Status == "approved" && await eligibility.RequireApprovedAsync(1) == uploaded.Id, "Confirmed current IMSS admits");
        Ensure((await service.ReviewDocumentAsync(uploaded.Id, new("approved", null, new DateOnly(2026, 10, 1)))).Id == uploaded.Id, "Repeated review is idempotent");
        var replacement = await studentService.UploadDocumentAsync(1, Upload([.. bytes, 10]), "general", null, "IMSS");
        Ensure(replacement.Id != uploaded.Id && replacement.Status == "pending", "Annual replacement needs new review");
        Ensure(await db.ExpedientDocuments.AsNoTracking().AnyAsync(d => d.Id == uploaded.Id && d.Status == "superseded"), "Old IMSS retained");
        try { await service.ReviewDocumentAsync(replacement.Id, new("rejected", " ")); throw new Exception("Empty rejection accepted"); }
        catch (ValidationException) { }
        await service.ReviewDocumentAsync(replacement.Id, new("rejected", "Escaneo ilegible"));
        Ensure(!(await db.Students.AsNoTracking().SingleAsync(s => s.Id == 1)).HasValidImss, "Rejection clears projection");
        Ensure(await db.ExpedientDocuments.CountAsync(d => d.StudentId == 1 && d.TramiteType == "general" && d.DocumentCode == "IMSS" && d.Status != "superseded") == 1, "One current general document with null tramite ID");

        // Two independently loaded xmin values must not overwrite a committed review.
        await using var other = new TecNMDbContext(options);
        var stale = await other.ExpedientDocuments.SingleAsync(d => d.Id == replacement.Id);
        var current = await db.ExpedientDocuments.SingleAsync(d => d.Id == replacement.Id);
        current.RejectionReason = "Observación actualizada";
        await db.SaveChangesAsync();
        stale.RejectionReason = "Observación obsoleta";
        try { await other.SaveChangesAsync(); throw new Exception("Lost update accepted"); }
        catch (DbUpdateConcurrencyException) { }
        Console.WriteLine("PostgreSQL migration, IMSS lifecycle, null-key uniqueness and xmin concurrency checks passed.");
    }

    private static void Ensure(bool condition, string message) { if (!condition) throw new Exception(message); }
}
