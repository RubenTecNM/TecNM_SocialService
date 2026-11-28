using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TecNM.Api.Core.Entities;
using TecNM.Api.Data;

internal static class FollowUpChecks
{
    public static async Task RunAsync(WebApplicationFactory<TecNMDbContext> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TecNMDbContext>();
        using var admin = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var ss = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var pp = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        foreach (var (client, name) in new[] {(admin,"admin"),(ss,"encargado.ss"),(pp,"coord.practicas")})
            (await client.PostAsJsonAsync("/api/auth/login",new {username=name,password="TecNM-Demo-2026!"})).EnsureSuccessStatusCode();
        var previous = await db.AcademicPeriods.Where(p=>p.IsActive).ToListAsync();
        var periods = Enumerable.Range(0,5).Select(i=>new AcademicPeriod {Code=$"FOLLOW{i}",Name="Seguimiento QA",SchoolYear=$"{2009+i/2+i%2}-{2010+i/2+i%2}",StartDate=new DateOnly(2010+i/2, i%2==0?2:8,1),EndDate=new DateOnly(2010+i/2,i%2==0?7:12,28)}).ToArray();
        foreach(var p in previous)p.IsActive=false;
        periods[0].IsActive=true; db.AddRange(periods);
        var student=new Student {ControlNumber="FOLLOWQA",FirstName="Seguimiento",FirstSurname="Prueba",CareerId=await db.Careers.Select(c=>c.Id).FirstAsync()};
        db.Add(student); await db.SaveChangesAsync();
        var service=new SocialService {SemesterEnrolled=7,StudentId=student.Id,PeriodId=periods[0].Id,ProgramId=await db.SocialServicePrograms.Select(x=>x.Id).FirstAsync(),CompanyId=await db.Companies.Select(x=>x.Id).FirstAsync(),SupervisorId=await db.CompanySupervisors.Select(x=>x.Id).FirstAsync(),StartDate=periods[0].StartDate,EndDate=periods[1].EndDate};
        async Task<StudentSocialServiceFollowUp> Current()=>await db.StudentSocialServiceFollowUps.SingleAsync(f=>f.StudentId==student.Id);
        void Check(bool ok,string message){if(!ok)throw new Exception("Follow up: "+message);}
        async Task<JsonElement> List(HttpClient client,string filter="") {
            var response=await client.GetAsync("/api/students?search=FOLLOWQA"+filter); response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }
        Check((await Current()).Status=="pending_setup","unknown origin cannot create notices");
        student.SocialServiceEligiblePeriodId=periods[0].Id; await db.SaveChangesAsync();
        var labels=new[]{"first_notice","second_notice","third_notice","non_compliant","non_compliant"};
        for(var i=0;i<5;i++) {
            foreach(var p in periods)p.IsActive=false;periods[i].IsActive=true;await db.SaveChangesAsync();
            Check((await Current()).Status==labels[i],"semester progression "+i);
        }
        var summary=(await List(ss)).GetProperty("items")[0];
        Check(summary.GetProperty("currentSemester").ValueKind==JsonValueKind.Null&&!summary.GetProperty("canReadProfile").GetBoolean(),"candidate summary redacted");
        Check((await ss.GetAsync($"/api/students/{student.Id}")).StatusCode==HttpStatusCode.NotFound,"candidate summary does not grant profile access");
        Check((await pp.GetAsync("/api/students?followUpStatus=first_notice")).StatusCode==HttpStatusCode.Forbidden,"PP cannot query SS filters");
        db.Add(service);await db.SaveChangesAsync();Check((await Current()).Status=="non_compliant","request does not exempt notices");
        var doc=new ExpedientDocument {StudentId=student.Id,TramiteType="social_service",TramiteId=service.Id,DocumentCode="SOL",Origin="uploaded",Status="pending",StoragePath="qa",OriginalFileName="qa.pdf",FileSizeBytes=10};
        db.Add(doc);await db.SaveChangesAsync();Check((await Current()).Status=="non_compliant","pending PDF does not exempt notices");
        doc.Status="rejected";await db.SaveChangesAsync();Check((await Current()).Status=="non_compliant","rejected PDF does not exempt notices");
        doc.Status="approved";doc.Origin="system";await db.SaveChangesAsync();Check((await Current()).Status=="non_compliant","automatic document is not student progress");
        doc.Origin="uploaded";await db.SaveChangesAsync();Check((await Current()).Status=="in_progress","first approved PDF exempts before activation");
        var autoLow=(await List(admin)).GetProperty("items")[0].GetProperty("socialServiceFollowUp");
        Check(autoLow.GetProperty("progressAssessment").GetString()=="low_progress"&&!string.IsNullOrEmpty(autoLow.GetProperty("autoReason").GetString()),"auto low progress without recent evidence");
        Check((await List(admin,"&followUpStatus=in_progress&followUpProgress=low_progress&pageSize=1")).GetProperty("totalItems").GetInt32()==1,"auto low progress filter");
        doc.ReviewedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync();
        var autoConsistent=(await List(admin)).GetProperty("items")[0].GetProperty("socialServiceFollowUp");
        Check(autoConsistent.GetProperty("progressAssessment").GetString()=="consistent"&&!string.IsNullOrEmpty(autoConsistent.GetProperty("autoReason").GetString()),"auto consistent with recent approved evidence");
        Check((await List(admin,"&followUpStatus=in_progress&followUpProgress=consistent&pageSize=1")).GetProperty("totalItems").GetInt32()==1,"auto consistent filter");
        Check((await List(admin,"&followUpStatus=in_progress&followUpProgress=low_progress&pageSize=1")).GetProperty("totalItems").GetInt32()==0,"consistent excludes low progress filter");
        var manualPut=await admin.PutAsJsonAsync($"/api/students/{student.Id}/social-service-follow-up/review",new {periodId=periods[4].Id,assessment="low_progress",notes="No permitido"});
        Check(manualPut.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,"manual review endpoint removed");
        foreach(var p in periods)p.IsActive=false;periods[3].IsActive=true;await db.SaveChangesAsync();
        var newPeriodDto=(await List(admin)).GetProperty("items")[0].GetProperty("socialServiceFollowUp");
        Check(newPeriodDto.GetProperty("progressAssessment").GetString()=="consistent","new period preserves auto assessment");
        service.Status="cancelled";await db.SaveChangesAsync();Check((await Current()).Status=="non_compliant","cancelled evidence ignored");
        service.Status="released";await db.SaveChangesAsync();Check((await Current()).Status=="released","released takes priority");
        service.Status="non_compliant";await db.SaveChangesAsync();Check((await Current()).Status=="non_compliant","formal PNC preserved");
        foreach(var p in periods)p.IsActive=false;foreach(var p in previous)p.IsActive=true;await db.SaveChangesAsync();
        Console.WriteLine("Follow-up semester progression, evidence, redaction, roles, automatic assessment and filters passed.");
    }
}

