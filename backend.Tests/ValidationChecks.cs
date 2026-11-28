using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TecNM.Api.Data;

internal static class ValidationChecks
{
    public static async Task RunAsync(WebApplicationFactory<TecNMDbContext> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TecNMDbContext>();
        using var admin = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var login = await admin.PostAsJsonAsync("/api/auth/login", new { username = "jefa.vinculacion", password = "TecNM-Demo-2026!" });
        login.EnsureSuccessStatusCode();
        var career = await db.Careers.FirstAsync(c => c.IsActive);
        var invalid = await admin.PostAsJsonAsync("/api/students", new {
            careerId = career.Id, controlNumber = "INVALID SPACE", curp = "ABC", firstName = "", firstSurname = "",
            currentSemester = 16, completedCredits = -1, institutionalEmail = "sin-arroba", phoneNumber = new string('1', 21)
        });
        if (invalid.StatusCode != HttpStatusCode.BadRequest) throw new Exception("Expected field validation 400.");
        var errors = (await invalid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        foreach (var field in new[] { "controlNumber", "curp", "firstName", "firstSurname", "currentSemester", "completedCredits", "institutionalEmail", "phoneNumber" })
            if (!errors.TryGetProperty(field, out var messages) || messages.GetArrayLength() == 0) throw new Exception($"Missing field error: {field}");
        var control = "V" + Guid.NewGuid().ToString("N")[..20].ToUpperInvariant();
        object Payload(string number, int credits) => new { careerId = career.Id, controlNumber = number, firstName = "María José", firstSurname = "Muñoz", curp = "", institutionalEmail = "", phoneNumber = "", completedCredits = credits };
        var created = await admin.PostAsJsonAsync("/api/students", Payload(control, 10));
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        try
        {
            var duplicate = await admin.PostAsJsonAsync("/api/students", Payload(control, 10));
            if (duplicate.StatusCode != HttpStatusCode.Conflict || !(await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("controlNumber", out _))
                throw new Exception("Duplicate matrícula lost its 409 field detail.");
            var excess = await admin.PostAsJsonAsync("/api/students", Payload(control + "A", career.TotalCredits + 1));
            if (excess.StatusCode != HttpStatusCode.UnprocessableEntity || !(await excess.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("completedCredits", out _))
                throw new Exception("Excess credits lost its 422 field detail.");
            var options = await admin.GetFromJsonAsync<JsonElement>("/api/careers/options");
            var option = options.EnumerateArray().Single(o => o.GetProperty("id").GetInt64() == career.Id);
            if (option.GetProperty("meta").GetProperty("totalCredits").GetInt32() != career.TotalCredits) throw new Exception("Wrong career credit limit.");
        }
        finally { db.Students.Remove(await db.Students.SingleAsync(s => s.Id == id)); await db.SaveChangesAsync(); }
        Console.WriteLine("Manual student validation: multiple errors, optional fields, duplicate 409, credit 422 and real career limits passed.");
    }
}
