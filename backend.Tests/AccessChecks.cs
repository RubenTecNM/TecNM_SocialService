using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TecNM.Api.Core.Entities;
using TecNM.Api.Data;

internal static class AccessChecks
{
    public static async Task RunAsync(WebApplicationFactory<TecNMDbContext> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TecNMDbContext>();
        using var ss = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var pp = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var viewer = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var student = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var admin = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        foreach (var (client, name) in new[] { (ss,"encargado.ss"), (pp,"coord.practicas"), (viewer,"jefa.vinculacion"), (student,"l23050363@monclova.tecnm.mx"), (admin,"admin") })
            await Expect(await client.PostAsJsonAsync("/api/auth/login", new { username = name, password = "TecNM-Demo-2026!" }), HttpStatusCode.OK);

        var period = new AcademicPeriod { Code = "QA-RBAC", Name = "Pruebas de alcance", SchoolYear = "2025-2026", StartDate = new(2026,1,1), EndDate = new(2026,12,31) };
        var a = new Company { Name = "QA Empresa A", CompanyType = "micro", Sector = "private", CompanySize = "micro" };
        var b = new Company { Name = "QA Empresa B", CompanyType = "public", Sector = "public", IsActive = false };
        var supervisor = new CompanySupervisor { Company = a, FullName = "Responsable QA", Position = "Dirección" };
        var career = await db.Careers.Select(c => c.Id).FirstAsync();
        var program = await db.SocialServicePrograms.Select(p => p.Id).FirstAsync();
        var students = Enumerable.Range(0,10).Select(i => new Student { ControlNumber = $"QA{i:000}", FirstName = "Alumno QA", FirstSurname = i.ToString(), CareerId = career, CurrentSemester = 7 }).ToArray();
        db.AddRange(period,a,b,supervisor); db.AddRange(students); await db.SaveChangesAsync();
        SocialService Social(int n, string status, Company company) => new() { StudentId = students[n].Id, PeriodId = period.Id, ProgramId = program, CompanyId = company.Id, SupervisorId = supervisor.Id, SemesterEnrolled = 7, StartDate = period.StartDate, EndDate = period.EndDate, Status = status };
        Internship Internship(int n,string status,Company? company) => new() { StudentId = students[n].Id, PeriodId = period.Id, CompanyId = company?.Id, SemesterEnrolled = 7, Status = status };
        var ownSs = Social(0,"active",a); var ownPp = Internship(3,"active",b);
        db.AddRange(ownSs, Social(1,"approved",a), Social(2,"cancelled",b), ownPp, Internship(4,"accepted",a), Internship(5,"released",b), Internship(0,"active",b), Internship(7,"active",null));
        var otherPeriod = Social(6,"active",b); otherPeriod.PeriodId = await db.AcademicPeriods.Where(p => p.Id != period.Id).Select(p => p.Id).FirstAsync(); db.Add(otherPeriod);
        await db.SaveChangesAsync();
        var docSs = new ExpedientDocument { StudentId = students[0].Id, TramiteType = "social_service", TramiteId = ownSs.Id, DocumentCode = "CA", StoragePath = "qa/not-opened.pdf", OriginalFileName = "ca.pdf", FileMimeType = "application/pdf", FileSizeBytes = 5 };
        var docPp = new ExpedientDocument { StudentId = students[3].Id, TramiteType = "internship", TramiteId = ownPp.Id, DocumentCode = "CA", StoragePath = "qa/not-opened.pdf", OriginalFileName = "ca.pdf", FileMimeType = "application/pdf", FileSizeBytes = 5 };
        var general = new ExpedientDocument { StudentId = students[9].Id, TramiteType = "general", DocumentCode = "IMSS", StoragePath = "qa/not-opened.pdf", OriginalFileName = "imss.pdf", FileMimeType = "application/pdf", FileSizeBytes = 5 };
        db.AddRange(docSs,docPp,general); await db.SaveChangesAsync();

        var storageRoot = Environment.GetEnvironmentVariable("Storage__RootPath")!;
        var filesBefore = Directory.GetFiles(storageRoot, "*.pdf", SearchOption.AllDirectories).Length;
        foreach (var (client, id, type, tramiteId, code, expected) in new[] {
            (ss, students[0].Id, "social_service", ownSs.Id, "CP", HttpStatusCode.Forbidden),
            (pp, students[3].Id, "internship", ownPp.Id, "EV", HttpStatusCode.Forbidden),
            (admin, students[0].Id, "social_service", ownSs.Id, "CA", HttpStatusCode.Forbidden),
            (student, students[0].Id, "social_service", ownSs.Id, "CP", HttpStatusCode.NotFound) })
        {
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(File.ReadAllBytes("backend/Resources/Documents/templates/pp-evaluation.pdf")), "file", "evidence.pdf");
            form.Add(new StringContent(type), "tramiteType");
            form.Add(new StringContent(tramiteId.ToString()), "tramiteId");
            form.Add(new StringContent(code), "documentCode");
            await Expect(await client.PostAsync($"/api/students/{id}/documents", form), expected);
        }
        if (Directory.GetFiles(storageRoot, "*.pdf", SearchOption.AllDirectories).Length != filesBefore)
            throw new Exception("Forbidden upload wrote a file.");

        await Expect(await student.GetAsync("/api/internships"), HttpStatusCode.Forbidden);
        await Expect(await student.GetAsync("/api/social-services"), HttpStatusCode.Forbidden);
        await Expect(await pp.DeleteAsync($"/api/companies/{b.Id}"), HttpStatusCode.Conflict);
        await Expect(await ss.GetAsync("/api/internships"), HttpStatusCode.Forbidden);
        await Expect(await pp.GetAsync("/api/social-services"), HttpStatusCode.Forbidden);
        await Expect(await ss.GetAsync($"/api/internships/{ownPp.Id}/pdf/evaluation"), HttpStatusCode.NotFound);
        await Expect(await pp.GetAsync($"/api/social-services/{ownSs.Id}/pdf/application"), HttpStatusCode.NotFound);
        await Expect(await ss.GetAsync($"/api/students/{students[3].Id}"), HttpStatusCode.NotFound);
        await Expect(await pp.GetAsync($"/api/students/{students[1].Id}"), HttpStatusCode.NotFound);
        await Expect(await ss.GetAsync($"/api/students/{students[9].Id}"), HttpStatusCode.NotFound);
        await Expect(await pp.GetAsync($"/api/students/{students[9].Id}"), HttpStatusCode.NotFound);
        foreach (var (client, foreign) in new[] { (ss,docPp), (pp,docSs) })
        {
            await Expect(await client.GetAsync($"/api/documents/{foreign.Id}/file"), HttpStatusCode.NotFound);
            await Expect(await client.PostAsJsonAsync($"/api/documents/{foreign.Id}/review",new {status="approved"}), HttpStatusCode.NotFound);
            await Expect(await client.DeleteAsync($"/api/documents/{foreign.Id}"), HttpStatusCode.NotFound);
        }
        await Expect(await ss.GetAsync($"/api/reports/tracking?tramiteType=internship&periodId={period.Id}"), HttpStatusCode.Forbidden);
        await Expect(await pp.GetAsync($"/api/reports/tracking?tramiteType=social_service&periodId={period.Id}"), HttpStatusCode.Forbidden);
        await Expect(await student.GetAsync($"/api/reports/linkage?tramiteType=internship&periodId={period.Id}"), HttpStatusCode.Forbidden);
        var visible = await ss.GetFromJsonAsync<JsonElement>($"/api/students/{students[0].Id}/documents");
        if (visible.EnumerateArray().Any(d => d.GetProperty("tramiteType").GetString() == "internship")) throw new Exception("Document listing leaked opposite process.");
        foreach (var client in new[] {ss,pp})
        {
            var preAdmission = await client.GetFromJsonAsync<JsonElement>("/api/documents/review-queue?tramiteType=general&pageSize=100");
            if (!preAdmission.GetProperty("items").EnumerateArray().Any(d => d.GetProperty("id").GetInt64() == general.Id)) throw new Exception("Shared pre-admission missing.");
        }
        await Expect(await ss.PostAsJsonAsync($"/api/documents/{general.Id}/review", new {status="approved",documentDate=new DateOnly(2026,1,1)}), HttpStatusCode.OK);
        await Expect(await ss.PostAsJsonAsync($"/api/documents/{docSs.Id}/review", new {status="approved"}), HttpStatusCode.OK);
        await Expect(await pp.PostAsJsonAsync($"/api/documents/{docPp.Id}/review", new {status="approved"}), HttpStatusCode.OK);

        foreach (var client in new[] { ss, pp, viewer })
        foreach (var (type, assigned, active, companyCount) in new[] { ("social_service",2,1,1), ("internship",3,2,2) })
        {
            var response = await client.GetAsync($"/api/reports/linkage?tramiteType={type}&periodId={period.Id}&pageSize=1");
            await Expect(response,HttpStatusCode.OK);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (json.GetProperty("tramiteType").GetString() != type || json.GetProperty("totals").GetProperty("assignedStudents").GetInt64()!=assigned || json.GetProperty("totals").GetProperty("activeStudents").GetInt64()!=active || json.GetProperty("totals").GetProperty("projects").GetInt64()!=assigned) throw new Exception("Linkage counts mixed domains/statuses/periods or excluded inactive companies.");
            if (json.GetProperty("byCompanyType").GetArrayLength()!=6 || json.GetProperty("companies").GetProperty("totalItems").GetInt32()!=companyCount || json.GetProperty("companies").GetProperty("items").GetArrayLength()!=1) throw new Exception("Linkage zero categories/pagination wrong.");
            var text=await response.Content.ReadAsStringAsync();
            if (text.Contains("\"studentId\"") || text.Contains("\"tramiteId\"") || text.Contains("\"documentId\"")) throw new Exception("Aggregate contains personal resource IDs.");
            var statsResp = await client.GetAsync($"/api/reports/statistics?tramiteType={type}&periodId={period.Id}");
            await Expect(statsResp,HttpStatusCode.OK);
            var analyticsResp = await client.GetAsync($"/api/reports/analytics?tramiteType={type}&timeScope=period&periodId={period.Id}&statusGroup=in_progress");
            await Expect(analyticsResp, HttpStatusCode.OK);
            var analyticsText = await analyticsResp.Content.ReadAsStringAsync();
            if (analyticsText.Contains("\"studentId\"") || analyticsText.Contains("\"tramiteId\"") || analyticsText.Contains("\"documentId\"") || analyticsText.Contains("\"controlNumber\""))
                throw new Exception("Analytics response leaked personal resource IDs.");
            var analyticsJson = JsonDocument.Parse(analyticsText).RootElement;
            if (analyticsJson.GetProperty("summary").GetProperty("uniqueStudents").GetInt32() < 1)
                throw new Exception($"Cross-process analytics returned zero for {type}.");
        }
        await Expect(await ss.GetAsync("/api/reports/linkage?tramiteType=other"),HttpStatusCode.BadRequest);
        await Expect(await ss.GetAsync("/api/reports/linkage"),HttpStatusCode.BadRequest);
        await Expect(await ss.GetAsync("/api/reports/linkage?tramiteType=internship&periodId=9999999"),HttpStatusCode.NotFound);

        var payload = new { name="QA catálogo compartido", companyType="small", sector="private", companySize="small", city="Monclova",state="Coahuila" };
        var created=await ss.PostAsJsonAsync("/api/companies",payload);await Expect(created,HttpStatusCode.Created);
        var companyId=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        await Expect(await pp.PutAsJsonAsync($"/api/companies/{companyId}",payload),HttpStatusCode.OK);
        await Expect(await viewer.PostAsJsonAsync("/api/companies",payload),HttpStatusCode.Forbidden);
        await Expect(await ss.PostAsJsonAsync("/api/companies",new {name="Invalid",companyType="other",sector="educational",companySize="not_applicable"}),HttpStatusCode.BadRequest);
        var roles = await db.UserRoles.Where(ur => ur.User!.Username=="encargado.ss").ToListAsync();
        // Role removal and restoration happen only inside the disposable test database.
        db.UserRoles.RemoveRange(roles);await db.SaveChangesAsync();
        await Expect(await ss.GetAsync("/api/companies"),HttpStatusCode.Forbidden);
        foreach (var role in roles) { role.DeletedAt = null; db.UserRoles.Update(role); }
        await db.SaveChangesAsync();
        await Expect(await ss.GetAsync("/api/companies"),HttpStatusCode.OK);
        var account=await db.Users.SingleAsync(u=>u.Username=="encargado.ss");account.IsActive=false;await db.SaveChangesAsync();
        await Expect(await ss.GetAsync("/api/companies"),HttpStatusCode.Unauthorized);
        account.IsActive=true;await db.SaveChangesAsync();
        Console.WriteLine("HTTP process isolation, shared pre-admission, companies, SQL linkage totals/pagination and immediate role revocation passed.");
    }

    private static async Task Expect(HttpResponseMessage response,HttpStatusCode status)
    {
        if(response.StatusCode!=status) throw new Exception($"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: expected {(int)status}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }
}
