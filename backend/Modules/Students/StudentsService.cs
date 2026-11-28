using TecNM.Api.Modules.Expedients;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Http;
using TecNM.Api.Core.Security;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.Students;

public partial class StudentsService(
    TecNMDbContext dbContext,
    ICurrentUser currentUser,
    StudentAccountsService accounts,
    IConfiguration configuration,
    DocumentsService documents,
    ImssEligibilityService imss,
    TimeProvider clock)
{
    private static readonly HashSet<string> ValidShifts = ["morning", "afternoon", "night"];
    private static readonly byte[] PdfMagicBytes = [0x25, 0x50, 0x44, 0x46]; // %PDF
    private static readonly byte[] PngMagicBytes = [0x89, 0x50, 0x4E, 0x47]; // PNG
    private static readonly byte[] JpgMagicBytes = [0xFF, 0xD8, 0xFF];       // JPEG

    [GeneratedRegex(@"^[A-Z0-9_-]{1,32}$")]
    private static partial Regex ControlNumberRegex();

    [GeneratedRegex(@"^[A-Z]{4}\d{6}[HM][A-Z]{5}[A-Z0-9]\d$")]
    private static partial Regex CurpRegex();

    private string StorageRootPath =>
        configuration["Storage:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "storage");

    public async Task<PagedResponse<StudentListItem>> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        long? careerId,
        string? shift,
        bool? isEnrolled,
        bool? hasValidImss,
        bool? hasDisability,
        string? sort,
        CancellationToken cancellationToken = default, string? followUpStatus = null, string? followUpProgress = null)
    {
        var canFollowUp=currentUser.CanReadProcess("social_service");
        if((followUpStatus!=null||followUpProgress!=null)&&!canFollowUp) currentUser.RequireProcess("social_service");
        if(followUpStatus!=null&&!new[]{"first_notice","second_notice","third_notice","non_compliant","in_progress","released","pending_setup"}.Contains(followUpStatus)) throw new ValidationException(new Dictionary<string,string[]>{{"followUpStatus",["Estatus inválido."]}});
        if(followUpProgress!=null&&!new[]{"consistent","low_progress","pending"}.Contains(followUpProgress)) throw new ValidationException(new Dictionary<string,string[]>{{"followUpProgress",["Valoración inválida."]}});
        var fullAccessIds=currentUser.VisibleStudents(dbContext).Select(s=>s.Id);
        var query = (canFollowUp ? dbContext.Students : currentUser.VisibleStudents(dbContext))
            .Include(s => s.User).ThenInclude(u => u!.UserRoles).ThenInclude(r => r.Role)
            .Include(s => s.Career)
            .AsNoTracking();

        var nowUtc=clock.GetUtcNow();
        var cutoffUtc=nowUtc.AddDays(-75);
        var graceStartDate=DateOnly.FromDateTime(nowUtc.UtcDateTime).AddDays(-60);
        DateTimeOffset? activePeriodStartUtc=null;
        if(canFollowUp||followUpProgress!=null)
        {
            var activeStart=await dbContext.AcademicPeriods.AsNoTracking().Where(p=>p.IsActive).OrderByDescending(p=>p.StartDate).ThenByDescending(p=>p.Id).Select(p=>(DateOnly?)p.StartDate).FirstOrDefaultAsync(cancellationToken);
            if(activeStart.HasValue) activePeriodStartUtc=new DateTimeOffset(activeStart.Value.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero);
        }
        var hasActivePeriod=activePeriodStartUtc.HasValue;
        var activeStartUtc=activePeriodStartUtc??DateTimeOffset.MinValue;

        if(followUpStatus!=null) query=query.Where(s=>dbContext.StudentSocialServiceFollowUps.Any(f=>f.StudentId==s.Id&&f.Status==followUpStatus));
        if(followUpProgress!=null)
        {
            query=followUpProgress switch
            {
                "pending"=>query.Where(s=>dbContext.StudentSocialServiceFollowUps.Any(f=>f.StudentId==s.Id&&f.Status=="in_progress"&&(f.TrackingPeriodId==null||f.SocialServiceId==null))),
                "consistent"=>query.Where(s=>dbContext.StudentSocialServiceFollowUps.Any(f=>f.StudentId==s.Id&&f.Status=="in_progress"&&f.TrackingPeriodId!=null&&f.SocialServiceId!=null&&dbContext.SocialServices.Any(ss=>ss.Id==f.SocialServiceId&&(ss.Status=="completed_pending_review"||ss.StartDate>=graceStartDate||(f.LastApprovedAt!=null&&((hasActivePeriod&&f.LastApprovedAt>=activeStartUtc)||f.LastApprovedAt>=cutoffUtc)))))),
                "low_progress"=>query.Where(s=>dbContext.StudentSocialServiceFollowUps.Any(f=>f.StudentId==s.Id&&f.Status=="in_progress"&&f.TrackingPeriodId!=null&&f.SocialServiceId!=null&&dbContext.SocialServices.Any(ss=>ss.Id==f.SocialServiceId&&!(ss.Status=="completed_pending_review"||ss.StartDate>=graceStartDate||(f.LastApprovedAt!=null&&((hasActivePeriod&&f.LastApprovedAt>=activeStartUtc)||f.LastApprovedAt>=cutoffUtc)))))),
                _=>query.Where(_=>false)
            };
        }
        // Protected candidate values must not be inferred through filters.
        if(shift!=null||isEnrolled!=null||hasValidImss!=null||hasDisability!=null) query=query.Where(s=>fullAccessIds.Contains(s.Id));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(s =>
                EF.Functions.ILike(s.ControlNumber, pattern) ||
                EF.Functions.ILike(s.FirstName, pattern) ||
                EF.Functions.ILike(s.FirstSurname, pattern) ||
                (s.SecondSurname != null && EF.Functions.ILike(s.SecondSurname, pattern)) ||
                (fullAccessIds.Contains(s.Id) && s.Curp != null && EF.Functions.ILike(s.Curp, pattern)));
        }

        if (careerId.HasValue)
        {
            query = query.Where(s => s.CareerId == careerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(shift))
        {
            var normShift = shift.Trim().ToLowerInvariant();
            query = query.Where(s => s.Shift == normShift);
        }

        if (isEnrolled.HasValue)
        {
            query = query.Where(s => s.IsEnrolled == isEnrolled.Value);
        }

        if (hasValidImss.HasValue)
        {
            query = query.Where(s => dbContext.ExpedientDocuments.Any(d => d.StudentId == s.Id
                && d.TramiteType == "general" && d.TramiteId == null && d.DocumentCode == "IMSS"
                && d.Status == "approved") == hasValidImss.Value);
        }

        if (hasDisability.HasValue)
        {
            query = query.Where(s => s.HasDisability == hasDisability.Value);
        }

        query = sort?.ToLowerInvariant() switch
        {
            "controlnumber" => query.OrderBy(s => s.ControlNumber),
            "-controlnumber" => query.OrderByDescending(s => s.ControlNumber),
            "firstsurname" => query.OrderBy(s => s.FirstSurname).ThenBy(s => s.SecondSurname).ThenBy(s => s.FirstName),
            "-firstsurname" => query.OrderByDescending(s => s.FirstSurname).ThenByDescending(s => s.SecondSurname),
            "curriculumprogresspercentage" => query.OrderBy(s => s.CurriculumProgressPercentage),
            "-curriculumprogresspercentage" => query.OrderByDescending(s => s.CurriculumProgressPercentage),
            _ => query.OrderBy(s => s.FirstSurname).ThenBy(s => s.SecondSurname).ThenBy(s => s.FirstName)
        };

        if(!currentUser.ReadsAll() && sort?.Contains("curriculum", StringComparison.OrdinalIgnoreCase)==true) query=query.OrderBy(s=>s.FirstSurname).ThenBy(s=>s.Id);
        var totalItems = await query.CountAsync(cancellationToken);
        var currentPage = Math.Max(1, page);
        var validPageSize = Math.Clamp(pageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(totalItems / (double)validPageSize);

        var items = await query
            .Skip((currentPage - 1) * validPageSize)
            .Take(validPageSize)
            .ToListAsync(cancellationToken);

        await ProjectImssAsync(items, cancellationToken);
        var ids=items.Select(s=>s.Id).ToList();
        var allowed=await currentUser.VisibleStudents(dbContext).Where(s=>ids.Contains(s.Id)).Select(s=>s.Id).ToListAsync(cancellationToken);
        var followUps=canFollowUp ? await dbContext.StudentSocialServiceFollowUps.Where(f=>ids.Contains(f.StudentId)).ToDictionaryAsync(f=>f.StudentId,cancellationToken) : new Dictionary<long,StudentSocialServiceFollowUp>();
        var serviceIds=followUps.Values.Where(f=>f.Status=="in_progress"&&f.TrackingPeriodId!=null&&f.SocialServiceId.HasValue).Select(f=>f.SocialServiceId!.Value).Distinct().ToList();
        var serviceSnapshots=serviceIds.Count>0
            ? await dbContext.SocialServices.AsNoTracking().Where(ss=>serviceIds.Contains(ss.Id)).ToDictionaryAsync(ss=>ss.Id,ss=>new SocialServiceFollowUpSnapshot(ss.Status,ss.StartDate),cancellationToken)
            : new Dictionary<long,SocialServiceFollowUpSnapshot>();
        return new PagedResponse<StudentListItem>(
            items.Select(s=> { var item=ToListItem(s); if(!allowed.Contains(s.Id)) item=item with {CurrentSemester=null,CurriculumProgressPercentage=null,Shift=null,IsEnrolled=null,HasValidImss=null,HasUserAccount=null,AccountUsername=null,AccountStatus=null,CanReadProfile=false}; return item with {SocialServiceFollowUp=followUps.TryGetValue(s.Id,out var f)?FollowUpDto(f,f.SocialServiceId.HasValue&&serviceSnapshots.TryGetValue(f.SocialServiceId.Value,out var snapshot)?snapshot:null,activePeriodStartUtc,graceStartDate,cutoffUtc):null}; }).ToList(),
            currentPage,
            validPageSize,
            totalItems,
            totalPages);
    }

    public async Task<IReadOnlyList<OptionItem>> GetOptionsAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = currentUser.VisibleStudents(dbContext)
            .Include(s => s.User).ThenInclude(u => u!.UserRoles).ThenInclude(r => r.Role)
            .Include(s => s.Career)
            .AsNoTracking()
            .Where(s => s.IsEnrolled);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(s =>
                EF.Functions.ILike(s.ControlNumber, pattern) ||
                EF.Functions.ILike(s.FirstName, pattern) ||
                EF.Functions.ILike(s.FirstSurname, pattern) ||
                (s.SecondSurname != null && EF.Functions.ILike(s.SecondSurname, pattern)));
        }

        var items = await query
            .OrderBy(s => s.ControlNumber)
            .Take(20)
            .ToListAsync(cancellationToken);

        await ProjectImssAsync(items, cancellationToken);
        return items
            .Select(s => new OptionItem(
                s.Id,
                $"{s.ControlNumber} · {FormatFullName(s)}",
                new Dictionary<string, object?>
                {
                    ["controlNumber"] = s.ControlNumber,
                    ["careerId"] = s.CareerId,
                    ["careerCode"] = s.Career?.Code,
                    ["currentSemester"] = s.CurrentSemester,
                    ["hasValidImss"] = s.HasValidImss
                }))
            .ToList();
    }

    public async Task<StudentDetail> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await currentUser.RequireStudentAsync(dbContext, id, cancellationToken);
        var student = await dbContext.Students
            .Include(s => s.User).ThenInclude(u => u!.UserRoles).ThenInclude(r => r.Role)
            .Include(s => s.Career)
            .Include(s => s.ImssValidator)
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
        {
            throw new NotFoundException("students.not_found", $"Estudiante con id {id} no encontrado.");
        }

        var latest = await imss.GetLatestAsync(student.Id, cancellationToken);
        return ToDetail(student) with { HasValidImss = ImssEligibilityService.IsCurrent(latest, ImssEligibilityService.Today(clock)),
            ImssStatus = latest?.Status ?? "missing", ImssDocumentId = latest?.Id,
            ImssDocumentDate = latest?.DocumentDate, ImssRejectionReason = latest?.RejectionReason };
    }

    public async Task<StudentDetail> GetMyProfileAsync(CancellationToken cancellationToken = default)
    {
        var student = await GetCurrentStudentEntityAsync(asNoTracking: true, cancellationToken);
        var latest = await imss.GetLatestAsync(student.Id, cancellationToken);
        return ToDetail(student) with { HasValidImss = ImssEligibilityService.IsCurrent(latest, ImssEligibilityService.Today(clock)),
            ImssStatus = latest?.Status ?? "missing", ImssDocumentId = latest?.Id,
            ImssDocumentDate = latest?.DocumentDate, ImssRejectionReason = latest?.RejectionReason };
    }

    public async Task<StudentDetail> CreateAsync(StudentRequest request, CancellationToken cancellationToken = default)
    {
        var career = await ValidateAndGetCareerAsync(request.CareerId, cancellationToken);
        ValidateRequest(request, career.TotalCredits);

        var normControlNumber = request.ControlNumber.Trim().ToUpperInvariant();
        var exists = await dbContext.Students
            .AnyAsync(s => s.ControlNumber == normControlNumber, cancellationToken);

        if (exists)
        {
            throw new ConflictException(
                "students.duplicated_control_number",
                $"Ya existe un estudiante registrado con la matrícula '{normControlNumber}'.", new Dictionary<string, string[]> { ["controlNumber"] = ["Esta matrícula ya está registrada. Verifica el número de control."] });
        }

        var credits = request.CompletedCredits > 0 ? request.CompletedCredits : 156;
        if (credits > career.TotalCredits)
        {
            credits = career.TotalCredits;
        }

        var student = new Student
        {
            SocialServiceEligiblePeriodId = await ResolveEligiblePeriodAsync(request.SocialServiceEligiblePeriodId, cancellationToken),
            CareerId = career.Id,
            ControlNumber = normControlNumber,
            Curp = NormalizeOptionalUpper(request.Curp),
            FirstName = request.FirstName.Trim(),
            FirstSurname = request.FirstSurname.Trim(),
            SecondSurname = NormalizeOptional(request.SecondSurname),
            Gender = NormalizeOptionalUpper(request.Gender),
            Shift = request.Shift.Trim().ToLowerInvariant(),
            HasDisability = request.HasDisability,
            DisabilityDescription = request.HasDisability ? NormalizeOptional(request.DisabilityDescription) : null,
            InstitutionalEmail = NormalizeOptionalLower(request.InstitutionalEmail),
            PersonalEmail = NormalizeOptionalLower(request.PersonalEmail),
            PhoneNumber = NormalizeOptional(request.PhoneNumber),
            CurrentSemester = request.CurrentSemester,
            CompletedCredits = credits,
            CurriculumProgressPercentage = CalculateProgress(credits, career.TotalCredits),
            IsEnrolled = request.IsEnrolled
        };

        dbContext.Students.Add(student);
        await dbContext.SaveChangesAsync(cancellationToken);

        return currentUser.IsInRole("department_head")
            ? await GetByIdAsync(student.Id, cancellationToken)
            : ToDetail(student) with { CareerName = career.Name };
    }

    public async Task<StudentDetail> UpdateAsync(long id, StudentRequest request, CancellationToken cancellationToken = default)
    {
        await currentUser.RequireStudentAsync(dbContext, id, cancellationToken);
        var career = await ValidateAndGetCareerAsync(request.CareerId, cancellationToken);
        ValidateRequest(request, career.TotalCredits);

        var student = await dbContext.Students
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
        {
            throw new NotFoundException("students.not_found", $"Estudiante con id {id} no encontrado.");
        }

        var normControlNumber = request.ControlNumber.Trim().ToUpperInvariant();
        var exists = await dbContext.Students
            .AnyAsync(s => s.Id != id && s.ControlNumber == normControlNumber, cancellationToken);

        if (exists)
        {
            throw new ConflictException(
                "students.duplicated_control_number",
                $"Ya existe otro estudiante registrado con la matrícula '{normControlNumber}'.", new Dictionary<string, string[]> { ["controlNumber"] = ["Esta matrícula ya pertenece a otro alumno."] });
        }

        var credits = request.CompletedCredits >= 0 ? request.CompletedCredits : student.CompletedCredits;

        if(request.SocialServiceEligiblePeriodId.HasValue) student.SocialServiceEligiblePeriodId=await ResolveEligiblePeriodAsync(request.SocialServiceEligiblePeriodId,cancellationToken);
        student.CareerId = career.Id;
        student.ControlNumber = normControlNumber;
        student.Curp = NormalizeOptionalUpper(request.Curp);
        student.FirstName = request.FirstName.Trim();
        student.FirstSurname = request.FirstSurname.Trim();
        student.SecondSurname = NormalizeOptional(request.SecondSurname);
        student.Gender = NormalizeOptionalUpper(request.Gender);
        student.Shift = request.Shift.Trim().ToLowerInvariant();
        student.HasDisability = request.HasDisability;
        student.DisabilityDescription = request.HasDisability ? NormalizeOptional(request.DisabilityDescription) : null;
        student.InstitutionalEmail = NormalizeOptionalLower(request.InstitutionalEmail);
        student.PersonalEmail = NormalizeOptionalLower(request.PersonalEmail);
        student.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        student.CurrentSemester = request.CurrentSemester;
        student.CompletedCredits = credits;
        student.CurriculumProgressPercentage = CalculateProgress(credits, career.TotalCredits);
        student.IsEnrolled = request.IsEnrolled;

        if (student.UserId.HasValue) await accounts.EnsureAsync(student, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(student.Id, cancellationToken);
    }

    public async Task<StudentDetail> UpdateMyContactAsync(
        StudentContactRequest request,
        CancellationToken cancellationToken = default)
    {
        var student = await GetCurrentStudentEntityAsync(asNoTracking: false, cancellationToken);
        var errors = new Dictionary<string, string[]>();

        if (!string.IsNullOrWhiteSpace(request.PersonalEmail) &&
            (request.PersonalEmail.Trim().Length > 255 || !request.PersonalEmail.Contains('@')))
        {
            errors["personalEmail"] = ["El correo personal debe tener un formato válido y máximo 255 caracteres."];
        }

        if (!string.IsNullOrWhiteSpace(request.InstitutionalEmail) &&
            (request.InstitutionalEmail.Trim().Length > 255 || !request.InstitutionalEmail.Contains('@')))
        {
            errors["institutionalEmail"] = ["El correo institucional debe tener un formato válido y máximo 255 caracteres."];
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && request.PhoneNumber.Trim().Length > 20)
        {
            errors["phoneNumber"] = ["El teléfono de contacto no puede exceder 20 caracteres."];
        }

        if (request.CurrentSemester.HasValue && (request.CurrentSemester.Value < 1 || request.CurrentSemester.Value > 15))
        {
            errors["currentSemester"] = ["El semestre debe estar comprendido entre 1 y 15."];
        }

        if (!string.IsNullOrWhiteSpace(request.Shift) && !ValidShifts.Contains(request.Shift.Trim().ToLowerInvariant()))
        {
            errors["shift"] = ["El turno debe ser morning, afternoon o night."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        student.PersonalEmail = NormalizeOptionalLower(request.PersonalEmail) ?? student.PersonalEmail;
        if (request.InstitutionalEmail is not null && NormalizeOptionalLower(request.InstitutionalEmail) != NormalizeOptionalLower(student.InstitutionalEmail))
            throw new ValidationException(new Dictionary<string, string[]> { ["institutionalEmail"] = ["Solo administración puede cambiar el correo institucional."] });

        student.PhoneNumber = NormalizeOptional(request.PhoneNumber) ?? student.PhoneNumber;
        if (request.CurrentSemester.HasValue)
        {
            student.CurrentSemester = request.CurrentSemester.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Shift))
        {
            student.Shift = request.Shift.Trim().ToLowerInvariant();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(student.Id, cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await currentUser.RequireStudentAsync(dbContext, id, cancellationToken);
        var student = await dbContext.Students
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
        {
            throw new NotFoundException("students.not_found", $"Estudiante con id {id} no encontrado.");
        }

        var hasTramites = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::int AS "Value"
                FROM (
                    SELECT id FROM social_services WHERE student_id = {0} AND deleted_at IS NULL
                    UNION ALL
                    SELECT id FROM internships WHERE student_id = {0} AND deleted_at IS NULL
                ) t
                """,
                id)
            .SingleAsync(cancellationToken);

        if (hasTramites > 0)
        {
            throw new ConflictException("students.in_use", "No se puede eliminar un estudiante que cuenta con trámites registrados.");
        }

        dbContext.Students.Remove(student);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<StudentEligibility> GetEligibilityAsync(long id, CancellationToken cancellationToken = default)
    {
        await currentUser.RequireStudentAsync(dbContext, id, cancellationToken);
        var student = await dbContext.Students
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
        {
            throw new NotFoundException("students.not_found", $"Estudiante con id {id} no encontrado.");
        }

        return await BuildEligibilityAsync(student, cancellationToken);
    }

    public async Task<StudentEligibility> GetMyEligibilityAsync(CancellationToken cancellationToken = default)
    {
        var student = await GetCurrentStudentEntityAsync(asNoTracking: true, cancellationToken);
        return await BuildEligibilityAsync(student, cancellationToken);
    }

    public async Task<CreateStudentAccountResponse> CreateAccountAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        await currentUser.RequireStudentAsync(dbContext, id, cancellationToken);
        var student = await dbContext.Students
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
        {
            throw new NotFoundException("students.not_found", $"Estudiante con id {id} no encontrado.");
        }

        if (student.UserId.HasValue)
        {
            throw new ConflictException("students.account_exists", "El estudiante ya cuenta con un usuario vinculado.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await accounts.EnsureAsync(student, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CreateStudentAccountResponse(student.UserId!.Value, student.User!.Username, StudentAccountsService.Status(student));
    }

    public async Task<StudentDetail> UploadImssAsync(
        long id,
        IFormFile file,
        CancellationToken cancellationToken = default,
        DateOnly? documentDate = null)
    {
        await currentUser.RequireStudentAsync(dbContext, id, cancellationToken);
        var student = await dbContext.Students
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
        {
            throw new NotFoundException("students.not_found", $"Estudiante con id {id} no encontrado.");
        }

        return await SaveImssFileAsync(student, file, cancellationToken, documentDate);
    }

    public async Task<StudentDetail> UploadMyImssAsync(
        IFormFile file,
        CancellationToken cancellationToken = default,
        DateOnly? documentDate = null)
    {
        var student = await GetCurrentStudentEntityAsync(asNoTracking: false, cancellationToken);
        return await SaveImssFileAsync(student, file, cancellationToken, documentDate);
    }

    public async Task<(Stream stream, string contentType, string fileName)> GetImssStreamAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        await currentUser.RequireStudentAsync(dbContext, id, cancellationToken);
        var student = await dbContext.Students
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
        {
            throw new NotFoundException("students.not_found", $"Estudiante con id {id} no encontrado.");
        }

        var latest = await imss.GetLatestAsync(student.Id, cancellationToken);
        if (latest is not null)
        {
            var (stream, name) = await documents.GetDocumentStreamAsync(latest.Id, cancellationToken);
            return (stream, "application/pdf", name);
        }
        return OpenImssStream(student);
    }

    public async Task<(Stream stream, string contentType, string fileName)> GetMyImssStreamAsync(
        CancellationToken cancellationToken = default)
    {
        var student = await GetCurrentStudentEntityAsync(asNoTracking: true, cancellationToken);
        var latest = await imss.GetLatestAsync(student.Id, cancellationToken);
        if (latest is not null)
        {
            var (stream, name) = await documents.GetDocumentStreamAsync(latest.Id, cancellationToken);
            return (stream, "application/pdf", name);
        }
        return OpenImssStream(student);
    }

    public async Task<ExpedientDocumentResponse> ValidateImssAsync(
        long id,
        bool isValid,
        string? rejectionReason,
        DateOnly? documentDate,
        CancellationToken cancellationToken = default)
    {
        await currentUser.RequireStudentAsync(dbContext, id, cancellationToken, true);
        var doc = await imss.GetLatestAsync(id, cancellationToken)
            ?? throw new BusinessRuleException("students.imss_file_missing", "Carga primero un PDF IMSS en el expediente.");
        return await documents.ReviewDocumentAsync(doc.Id, new ReviewDocumentRequest(isValid ? "approved" : "rejected", rejectionReason, documentDate), cancellationToken);
    }

    private async Task<StudentDetail> SaveImssFileAsync(Student student, IFormFile file, CancellationToken cancellationToken, DateOnly? documentDate)
    {
        await documents.UploadDocumentAsync(student.Id, file, "general", null, "IMSS", cancellationToken, documentDate);
        return await GetByIdAsync(student.Id, cancellationToken);
    }

    private (Stream stream, string contentType, string fileName) OpenImssStream(Student student)
    {
        if (string.IsNullOrWhiteSpace(student.ImssValidityFileUrl))
        {
            throw new NotFoundException("students.imss_not_found", "El estudiante no cuenta con archivo de vigencia IMSS cargado.");
        }

        var fullPath = Path.Combine(StorageRootPath, student.ImssValidityFileUrl.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            throw new NotFoundException("students.imss_file_missing", "El archivo físico de vigencia IMSS no fue encontrado.");
        }

        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        var contentType = ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/pdf"
        };

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (stream, contentType, $"Vigencia_IMSS_{student.ControlNumber}{ext}");
    }

    private async Task<Student> GetCurrentStudentEntityAsync(bool asNoTracking, CancellationToken cancellationToken)
    {
        if (!currentUser.Id.HasValue)
        {
            throw new NotFoundException("students.profile_not_linked", "No hay una sesión de estudiante activa.");
        }

        var query = currentUser.VisibleStudents(dbContext)
            .Include(s => s.User).ThenInclude(u => u!.UserRoles).ThenInclude(r => r.Role)
            .Include(s => s.Career)
            .Include(s => s.ImssValidator)
            .AsQueryable();

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        var student = await query.SingleOrDefaultAsync(s => s.UserId == currentUser.Id.Value, cancellationToken);
        if (student is null)
        {
            throw new NotFoundException(
                "students.profile_not_linked",
                "La cuenta actual no tiene un expediente de estudiante vinculado.");
        }

        return student;
    }

    private async Task<StudentEligibility> BuildEligibilityAsync(Student student, CancellationToken cancellationToken)
    {
        var ssReasons = new List<EligibilityReason>();
        var ppReasons = new List<EligibilityReason>();

        var latest = await imss.GetLatestAsync(student.Id, cancellationToken);
        if (!ImssEligibilityService.IsCurrent(latest, ImssEligibilityService.Today(clock)))
        {
            var reason = new EligibilityReason("students.imss_approval_required", "Requiere una constancia IMSS con estado VIGENTE aprobada por Vinculación.");
            ssReasons.Add(reason);
            ppReasons.Add(reason);
        }

        if (!student.IsEnrolled)
        {
            ssReasons.Add(new EligibilityReason("eligibility.not_enrolled", "El estudiante debe encontrarse inscrito en el periodo actual."));
            ppReasons.Add(new EligibilityReason("eligibility.not_enrolled", "El estudiante debe encontrarse inscrito en el periodo actual."));
        }

        var activeOrReleasedSs = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::int AS "Value"
                FROM social_services
                WHERE student_id = {0}
                  AND deleted_at IS NULL
                  AND status NOT IN ('cancelled', 'non_compliant')
                """,
                student.Id)
            .SingleAsync(cancellationToken);

        if (activeOrReleasedSs > 0)
        {
            ssReasons.Add(new EligibilityReason("eligibility.social_service_exists", "El estudiante ya cuenta con un Servicio Social en curso o liberado."));
        }

        var activeOrReleasedPp = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::int AS "Value"
                FROM internships
                WHERE student_id = {0}
                  AND deleted_at IS NULL
                  AND status NOT IN ('cancelled', 'non_compliant')
                """,
                student.Id)
            .SingleAsync(cancellationToken);

        if (activeOrReleasedPp > 0)
        {
            ppReasons.Add(new EligibilityReason("eligibility.internship_exists", "El estudiante ya cuenta con Prácticas Profesionales en curso o liberadas."));
        }

        var owner = currentUser.IsInRole("student") && student.UserId == currentUser.Id;
        return new StudentEligibility(
            owner || currentUser.CanReadProcess("social_service") ? new EligibilityResult(ssReasons.Count == 0, ssReasons) : null,
            owner || currentUser.CanReadProcess("internship") ? new EligibilityResult(ppReasons.Count == 0, ppReasons) : null);
    }

    private async Task<Career> ValidateAndGetCareerAsync(long careerId, CancellationToken cancellationToken)
    {
        var career = await dbContext.Careers
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == careerId && c.IsActive, cancellationToken);

        if (career is null)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["careerId"] = ["La carrera seleccionada no existe o se encuentra inactiva."]
            });
        }

        return career;
    }

    internal static void ValidateRequest(StudentRequest request, int totalCredits)
    {
        var errors = new Dictionary<string, string[]>();

        var controlNumber = request.ControlNumber?.Trim().ToUpperInvariant() ?? "";
        if (string.IsNullOrWhiteSpace(controlNumber) || !ControlNumberRegex().IsMatch(controlNumber))
        {
            errors["controlNumber"] = ["La matrícula es obligatoria (1 a 32 caracteres alfanuméricos)."];
        }

        if (!string.IsNullOrWhiteSpace(request.Curp))
        {
            var curp = request.Curp.Trim().ToUpperInvariant();
            if (!CurpRegex().IsMatch(curp))
            {
                errors["curp"] = ["La CURP no cumple con el formato oficial de 18 caracteres."];
            }
        }

        if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Trim().Length > 100)
        {
            errors["firstName"] = ["El nombre es obligatorio y debe tener máximo 100 caracteres."];
        }

        if (string.IsNullOrWhiteSpace(request.FirstSurname) || request.FirstSurname.Trim().Length > 70)
        {
            errors["firstSurname"] = ["El primer apellido es obligatorio y debe tener máximo 70 caracteres."];
        }

        if (!string.IsNullOrWhiteSpace(request.SecondSurname) && request.SecondSurname.Trim().Length > 70)
        {
            errors["secondSurname"] = ["El segundo apellido no puede exceder 70 caracteres."];
        }

        if (!string.IsNullOrWhiteSpace(request.Gender))
        {
            var g = request.Gender.Trim().ToUpperInvariant();
            if (g != "M" && g != "F")
            {
                errors["gender"] = ["El género debe ser 'M', 'F' o nulo."];
            }
        }

        var shift = request.Shift?.Trim().ToLowerInvariant() ?? "";
        if (!ValidShifts.Contains(shift))
        {
            errors["shift"] = ["El turno debe ser 'morning', 'afternoon' o 'night'."];
        }

        if (request.HasDisability && string.IsNullOrWhiteSpace(request.DisabilityDescription))
        {
            errors["disabilityDescription"] = ["Debe especificar la descripción cuando se indica discapacidad."];
        }
        else if (!string.IsNullOrWhiteSpace(request.DisabilityDescription) && request.DisabilityDescription.Trim().Length > 150)
        {
            errors["disabilityDescription"] = ["La descripción de discapacidad no puede exceder 150 caracteres."];
        }

        if (!string.IsNullOrWhiteSpace(request.InstitutionalEmail) &&
            (request.InstitutionalEmail.Trim().Length > 255 || !request.InstitutionalEmail.Contains('@')))
        {
            errors["institutionalEmail"] = ["El correo institucional no tiene un formato válido."];
        }

        if (!string.IsNullOrWhiteSpace(request.PersonalEmail) &&
            (request.PersonalEmail.Trim().Length > 255 || !request.PersonalEmail.Contains('@')))
        {
            errors["personalEmail"] = ["El correo personal no tiene un formato válido."];
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && request.PhoneNumber.Trim().Length > 20)
        {
            errors["phoneNumber"] = ["El teléfono no puede exceder 20 caracteres."];
        }

        if (request.CurrentSemester < 1 || request.CurrentSemester > 15)
        {
            errors["currentSemester"] = ["El semestre actual debe estar entre 1 y 15."];
        }

        if (request.CompletedCredits < 0)
        {
            errors["completedCredits"] = ["Los créditos aprobados no pueden ser negativos."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        if (request.CompletedCredits > totalCredits)
        {
            throw new BusinessRuleException(
                "students.credits_exceed_total",
                $"Los créditos acumulados ({request.CompletedCredits}) no pueden exceder el total de la carrera ({totalCredits}).", new Dictionary<string, string[]> { ["completedCredits"] = [$"Indica como máximo {totalCredits} créditos para esta carrera."] });
        }
    }

    public static decimal CalculateProgress(int completedCredits, int totalCredits)
    {
        if (totalCredits <= 0)
        {
            return 0.00m;
        }

        var pct = Math.Round((decimal)completedCredits / totalCredits * 100m, 2);
        return Math.Min(100.00m, Math.Max(0.00m, pct));
    }

    public static string FormatFullName(Student s) =>
        string.Join(" ", new[] { s.FirstName, s.FirstSurname, s.SecondSurname }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    private async Task ProjectImssAsync(IReadOnlyCollection<Student> students, CancellationToken ct)
    {
        var ids = students.Select(s => s.Id).ToArray();
        var approved = await dbContext.ExpedientDocuments.AsNoTracking().Where(d => ids.Contains(d.StudentId)
            && d.TramiteType == "general" && d.TramiteId == null && d.DocumentCode == "IMSS"
            && d.Status == "approved")
            .Select(d => d.StudentId).ToListAsync(ct);
        var valid = approved.ToHashSet();
        foreach (var student in students) student.HasValidImss = valid.Contains(student.Id);
    }

    private static StudentListItem ToListItem(Student s) =>
        new(
            s.Id,
            s.ControlNumber,
            FormatFullName(s),
            s.CareerId,
            s.Career?.Code ?? "",
            s.CurrentSemester,
            s.CurriculumProgressPercentage,
            s.Shift,
            s.IsEnrolled,
            s.HasValidImss,
            s.UserId.HasValue) { AccountUsername = s.User?.Username, AccountStatus = StudentAccountsService.Status(s) };

    private static StudentDetail ToDetail(Student s)
    {
        string? validatorName = null;
        if (s.ImssValidator is not null)
        {
            validatorName = string.Join(" ", new[]
            {
                s.ImssValidator.FirstName,
                s.ImssValidator.FirstSurname,
                s.ImssValidator.SecondSurname
            }.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        return new StudentDetail(
            s.Id,
            s.UserId,
            s.CareerId,
            s.Career?.Name ?? "",
            s.ImportBatchId,
            s.ControlNumber,
            s.Curp,
            s.FirstName,
            s.FirstSurname,
            s.SecondSurname,
            s.Gender,
            s.Shift,
            s.HasDisability,
            s.DisabilityDescription,
            s.InstitutionalEmail,
            s.PersonalEmail,
            s.PhoneNumber,
            s.CurrentSemester,
            s.CompletedCredits,
            s.CurriculumProgressPercentage,
            s.IsEnrolled,
            s.HasValidImss,
            !string.IsNullOrWhiteSpace(s.ImssValidityFileUrl),
            s.ImssValidatedAt,
            validatorName,
            s.CreatedAt,
            s.UpdatedAt) { SocialServiceEligiblePeriodId=s.SocialServiceEligiblePeriodId, AccountUsername = s.User?.Username, AccountStatus = StudentAccountsService.Status(s) };
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeOptionalUpper(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? NormalizeOptionalLower(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
