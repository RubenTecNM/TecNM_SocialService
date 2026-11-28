using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TecNM.Api.Core.Entities;
using TecNM.Api.Data;
using TecNM.Api.Modules.Students;

internal static class AccountImportChecks
{
    public static async Task RunAsync(WebApplicationFactory<TecNMDbContext> factory,byte[] samplePdf)
    {
        using var scope=factory.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<TecNMDbContext>();
        var imports=scope.ServiceProvider.GetRequiredService<StudentImportsService>();
        var period=await db.AcademicPeriods.Select(p=>p.Id).FirstAsync();
        using var head=factory.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
        using var ss=factory.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
        using var pp=factory.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
        await Expect(await head.PostAsJsonAsync("/api/auth/login",new{username="jefa.vinculacion",password="TecNM-Demo-2026!"}),HttpStatusCode.OK);
        await Expect(await ss.PostAsJsonAsync("/api/auth/login",new{username="encargado.ss",password="TecNM-Demo-2026!"}),HttpStatusCode.OK);
        await Expect(await pp.PostAsJsonAsync("/api/auth/login",new{username="coord.practicas",password="TecNM-Demo-2026!"}),HttpStatusCode.OK);
        var headHash=await db.Users.Where(u=>u.Username=="jefa.vinculacion").Select(u=>u.PasswordHash).SingleAsync();
        byte[] Excel(params (string control,string email,string name)[] rows)
        {
            using var stream=new MemoryStream();stream.Write(imports.GenerateTemplateXlsx());
            using(var zip=new ZipArchive(stream,ZipArchiveMode.Update,true))
            {
                zip.GetEntry("xl/worksheets/sheet1.xml")!.Delete();
                XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                string[] headers=["control_number","first_surname","second_surname","first_name","career_code","institutional_email"];
                var values=new[]{headers}.Concat(rows.Select(r=>new[]{r.control,"Prueba","",r.name,"INF",r.email}));
                var data=new XElement(ns+"sheetData",values.Select((row,index)=>new XElement(ns+"row",new XAttribute("r",index+1),row.Select((text,column)=>new XElement(ns+"c",new XAttribute("r",$"{(char)('A'+column)}{index+1}"),new XAttribute("t","inlineStr"),new XElement(ns+"is",new XElement(ns+"t",text)))))));
                using var output=zip.CreateEntry("xl/worksheets/sheet1.xml").Open();new XDocument(new XElement(ns+"worksheet",data)).Save(output);
            }
            return stream.ToArray();
        }
        async Task<HttpResponseMessage> PostImport(HttpClient client,byte[] bytes)
        {
            using var form=new MultipartFormDataContent();form.Add(new ByteArrayContent(bytes),"file","students.xlsx");form.Add(new StringContent(period.ToString()),"periodId");
            return await client.PostAsync("/api/student-imports",form);
        }
        async Task<JsonElement> Import(byte[] bytes)
        {
            using var form=new MultipartFormDataContent();form.Add(new ByteArrayContent(bytes),"file","students.xlsx");form.Add(new StringContent(period.ToString()),"periodId");
            var response=await head.PostAsync("/api/student-imports",form);await Expect(response,HttpStatusCode.Created);
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }
        var headEmail=await db.Users.Where(u=>u.Username=="jefa.vinculacion").Select(u=>u.Email).SingleAsync();
        await Expect(await PostImport(pp,Excel(("QAIMPPP","qa.imppp@monclova.tecnm.mx","PP"))),HttpStatusCode.Forbidden);
        var ssImport=await PostImport(ss,Excel(("QAIMPSS","qa.impss@monclova.tecnm.mx","SS")));await Expect(ssImport,HttpStatusCode.Created);
        var ssBatchId=(await ssImport.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        await Expect(await ss.GetAsync($"/api/student-imports/{ssBatchId}"),HttpStatusCode.OK);
        await Expect(await PostImport(head,Excel(("QAIMPHD","qa.imphd@monclova.tecnm.mx","Jefa"))),HttpStatusCode.Created);
        await Expect(await head.GetAsync("/api/student-imports"),HttpStatusCode.OK);
        var result=await Import(Excel(("QAIMP01","qa.import1@monclova.tecnm.mx","Uno"),("QAIMP02","qa.import2@monclova.tecnm.mx","Dos"),
            ("QAIMP03","","Sin correo"),("QAIMP04","invalid","Inválido"),("QAIMP01","duplicate@monclova.tecnm.mx","Repetido"),
            ("QAIMP05","qa.import1@monclova.tecnm.mx","Correo repetido"),("QAIMP06",headEmail,"Colisión"),("QAIMP07","long@monclova.tecnm.mx",new string('x',101))));
        if(result.GetProperty("importedRecords").GetInt32()!=2 || result.GetProperty("createdAccounts").GetInt32()!=2 || result.GetProperty("failedRecords").GetInt32()!=6) throw new Exception($"Import counters invalid: {result}");
        db.ChangeTracker.Clear();
        var one=await db.Students.Include(s=>s.User).SingleAsync(s=>s.ControlNumber=="QAIMP01");
        var two=await db.Students.Include(s=>s.User).SingleAsync(s=>s.ControlNumber=="QAIMP02");
        if(one.SocialServiceEligiblePeriodId!=period || one.SocialServiceEligiblePeriodUpdatedAt==null || one.SocialServiceEligiblePeriodUpdatedBy==null) throw new Exception("Import must preserve eligible period provenance.");
        using var student=factory.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
        await Expect(await student.PostAsJsonAsync("/api/auth/login",new{username=" QA.IMPORT2@MONCLOVA.TECNM.MX ",password="QAIMP02"}),HttpStatusCode.OK);
        await Expect(await student.PostAsJsonAsync("/api/auth/login",new{username="QAIMP02",password="QAIMP02"}),HttpStatusCode.Unauthorized);
        var custom=new PasswordHasher<User>().HashPassword(one.User!,"Changed-Password-2026!");
        one.User!.PasswordHash=custom;one.User.IsActive=false;await db.SaveChangesAsync();
        result=await Import(Excel(("QAIMP01","qa.import1@monclova.tecnm.mx","Actualizado")));
        if(result.GetProperty("existingAccounts").GetInt32()!=1 || result.GetProperty("createdAccounts").GetInt32()!=0) throw new Exception("Reimport created a duplicate account.");
        db.ChangeTracker.Clear();
        if((await db.Students.AsNoTracking().SingleAsync(s=>s.Id==one.Id)).SocialServiceEligiblePeriodId!=period) throw new Exception("Reimport must not reset eligibility origin.");
        var preserved=await db.Users.SingleAsync(u=>u.Id==one.UserId);
        if(preserved.PasswordHash!=custom || preserved.IsActive || await db.Users.Where(u=>u.Username=="jefa.vinculacion").Select(u=>u.PasswordHash).SingleAsync()!=headHash) throw new Exception("Import modified account credentials/activity.");
        await Expect(await student.PutAsJsonAsync("/api/students/me/contact",new{institutionalEmail="changed@monclova.tecnm.mx"}),HttpStatusCode.BadRequest);
        await Expect(await ss.PostAsync($"/api/students/{two.Id}/account/reset-password",null),HttpStatusCode.Forbidden);
        await Expect(await head.PostAsync($"/api/students/{two.Id}/account/reset-password",null),HttpStatusCode.NoContent);
        await Expect(await student.GetAsync("/api/auth/me"),HttpStatusCode.Unauthorized);
        await Expect(await student.PostAsJsonAsync("/api/auth/login",new{username="qa.import2@monclova.tecnm.mx",password="QAIMP02"}),HttpStatusCode.OK);
        await Expect(await ss.GetAsync($"/api/students/{two.Id}"),HttpStatusCode.NotFound);
        using(var form=new MultipartFormDataContent())
        {
            form.Add(new ByteArrayContent(samplePdf),"file","imss.pdf");
            await Expect(await student.PutAsync("/api/students/me/imss",form),HttpStatusCode.OK);
        }
        await Expect(await ss.PostAsJsonAsync($"/api/students/{two.Id}/imss/validation",new{isValid=true}),HttpStatusCode.OK);
        var pending=await db.ExpedientDocuments.AsNoTracking().SingleAsync(d=>d.StudentId==two.Id && d.DocumentCode=="IMSS");
        await Expect(await student.GetAsync($"/api/documents/{pending.Id}/imss-date-suggestion"),HttpStatusCode.Forbidden);
        await Expect(await ss.GetAsync($"/api/documents/{pending.Id}/imss-date-suggestion"),HttpStatusCode.OK);
        var approved=await ss.PostAsJsonAsync($"/api/students/{two.Id}/imss/validation",new{isValid=true,documentDate=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,"America/Mexico_City"))});
        await Expect(approved,HttpStatusCode.OK);
        var json=await approved.Content.ReadFromJsonAsync<JsonElement>();
        if(!json.TryGetProperty("documentCode",out _) || json.TryGetProperty("curp",out _)) throw new Exception("Pre-admission adapter exposed a full profile.");

        db.ChangeTracker.Clear();
        var beforeEmailChange=await db.Users.AsNoTracking().SingleAsync(u=>u.Id==two.UserId);
        var updated=await head.PutAsJsonAsync($"/api/students/{two.Id}",new StudentRequest(two.CareerId,two.ControlNumber,null,"Dos","Prueba",null,null,
            InstitutionalEmail:"qa.changed@monclova.tecnm.mx",CompletedCredits:156));
        await Expect(updated,HttpStatusCode.OK);
        db.ChangeTracker.Clear();
        var changed=await db.Users.AsNoTracking().SingleAsync(u=>u.Id==two.UserId);
        if(changed.Username!="qa.changed@monclova.tecnm.mx" || changed.Email!=changed.Username || changed.PasswordHash!=beforeEmailChange.PasswordHash
            || changed.SessionVersion!=beforeEmailChange.SessionVersion+1) throw new Exception("Administrative email change lost credentials or failed session revocation.");
        await Expect(await student.GetAsync("/api/auth/me"),HttpStatusCode.Unauthorized);
        await Expect(await student.PostAsJsonAsync("/api/auth/login",new{username=changed.Username,password="QAIMP02"}),HttpStatusCode.OK);
        var manual=await head.PostAsJsonAsync("/api/students",new StudentRequest(two.CareerId,"QAMANUAL01",null,"Manual","Prueba",null,null,InstitutionalEmail:"qa.manual@monclova.tecnm.mx"));
        await Expect(manual,HttpStatusCode.Created);
        var manualId=(await manual.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        var manualAccount=await head.PostAsync($"/api/students/{manualId}/account",null);await Expect(manualAccount,HttpStatusCode.Created);
        var accountJson=await manualAccount.Content.ReadFromJsonAsync<JsonElement>();
        if(accountJson.GetProperty("username").GetString()!="qa.manual@monclova.tecnm.mx" || accountJson.TryGetProperty("temporaryPassword",out _)) throw new Exception("Manual account contract is obsolete.");
        await Expect(await head.PostAsync($"/api/students/{manualId}/account",null),HttpStatusCode.Conflict);

        var connection=Environment.GetEnvironmentVariable("DOCUMENT_TEST_CONNECTION")!;
        await using(var failing=new TecNMDbContext(new DbContextOptionsBuilder<TecNMDbContext>().UseNpgsql(connection).AddInterceptors(new FailAccountSave()).Options))
        {
            var service=new StudentImportsService(failing,new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Storage:RootPath",Path.GetFullPath("tmp/pdf-qa/import-failure")}}).Build(),new StudentAccountsService(failing,new PasswordHasher<User>()));
            var bytes=Excel(("QAFAIL01","qa.failure@monclova.tecnm.mx","Rollback"));
            var failed=await service.ImportExcelAsync(period,new FormFile(new MemoryStream(bytes),0,bytes.Length,"file","failure.xlsx"));
            if(failed.ImportedRecords!=0 || failed.FailedRecords!=1 || failed.Status!="failed") throw new Exception("Failed import row was reported as committed.");
        }
        if(await db.Students.AnyAsync(s=>s.ControlNumber=="QAFAIL01") || await db.Users.AnyAsync(u=>u.Username=="qa.failure@monclova.tecnm.mx")) throw new Exception("Import failure left orphan data.");
        Console.WriteLine("HTTP Excel accounts, credential preservation, collision isolation, reset revocation and SS pre-admission checks passed.");
    }

    private static async Task Expect(HttpResponseMessage response,HttpStatusCode status)
    {
        if(response.StatusCode!=status) throw new Exception($"{response.RequestMessage?.RequestUri}: expected {(int)status}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }
    private sealed class FailAccountSave:SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(data.Context!.ChangeTracker.Entries<User>().Any(e=>e.State==EntityState.Added)) throw new DbUpdateException("Simulated account persistence failure.");
            return base.SavingChangesAsync(data,result,ct);
        }
    }
}
