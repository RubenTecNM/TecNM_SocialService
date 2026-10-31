using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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
    PasswordHasher<User> passwordHasher,
    IConfiguration configuration)
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
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Students
            .Include(s => s.Career)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(s =>
                EF.Functions.ILike(s.ControlNumber, pattern) ||
                EF.Functions.ILike(s.FirstName, pattern) ||
                EF.Functions.ILike(s.FirstSurname, pattern) ||
                (s.SecondSurname != null && EF.Functions.ILike(s.SecondSurname, pattern)) ||
                (s.Curp != null && EF.Functions.ILike(s.Curp, pattern)));
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
            query = query.Where(s => s.HasValidImss == hasValidImss.Value);
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

        var totalItems = await query.CountAsync(cancellationToken);
        var currentPage = Math.Max(1, page);
        var validPageSize = Math.Clamp(pageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(totalItems / (double)validPageSize);

        var items = await query
            .Skip((currentPage - 1) * validPageSize)
            .Take(validPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<StudentListItem>(
            items.Select(ToListItem).ToList(),
            currentPage,
            validPageSize,
            totalItems,
            totalPages);
    }

    public async Task<IReadOnlyList<OptionItem>> GetOptionsAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Students
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
        var student = await dbContext.Students
            .Include(s => s.Career)
            .Include(s => s.ImssValidator)
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
        {
            throw new NotFoundException("students.not_found", $"Estudiante con id {id} no encontrado.");
        }

        return ToDetail(student);
    }

    public async Task<StudentDetail> GetMyProfileAsync(CancellationToken cancellationToken = default)
    {
        var student = await GetCurrentStudentEntityAsync(asNoTracking: true, cancellationToken);
        return ToDetail(student);
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
                $"Ya existe un estudiante registrado con la matrícula '{normControlNumber}'.");
        }

        var credits = request.CompletedCredits > 0 ? request.CompletedCredits : 156;
        if (credits > career.TotalCredits)
        {
            credits = career.TotalCredits;
        }

        var student = new Student
        {
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

        return await GetByIdAsync(student.Id, cancellationToken);
    }

    public async Task<StudentDetail> UpdateAsync(long id, StudentRequest request, CancellationToken cancellationToken = default)
    {
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
                $"Ya existe otro estudiante registrado con la matrícula '{normControlNumber}'.");
        }

        var credits = request.CompletedCredits >= 0 ? request.CompletedCredits : student.CompletedCredits;

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
        if (request.InstitutionalEmail is not null)
        {
            student.InstitutionalEmail = NormalizeOptionalLower(request.InstitutionalEmail);
        }

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

        var email = student.InstitutionalEmail ?? student.PersonalEmail;
        if (string.IsNullOrWhiteSpace(email))
        {
            email = $"{student.ControlNumber.ToLowerInvariant()}@monclova.tecnm.mx";
            student.InstitutionalEmail = email;
        }

        var username = student.ControlNumber.ToUpperInvariant();
        var existingUser = await dbContext.Users
            .SingleOrDefaultAsync(u => u.Username == username || u.Email == email, cancellationToken);

        var tempPassword = $"TecNM-{student.ControlNumber}-{RandomNumberGenerator.GetInt32(100, 999)}!";

        var studentRole = await dbContext.Roles
            .SingleOrDefaultAsync(r => r.Name == "student", cancellationToken)
            ?? throw new NotFoundException("roles.not_found", "El rol 'student' no existe en la base de datos.");

        if (existingUser is null)
        {
            existingUser = new User
            {
                Username = username,
                Email = email,
                PasswordHash = string.Empty,
                FirstName = student.FirstName,
                FirstSurname = student.FirstSurname,
                SecondSurname = student.SecondSurname,
                IsActive = true
            };
            existingUser.PasswordHash = passwordHasher.HashPassword(existingUser, tempPassword);

            dbContext.Users.Add(existingUser);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.UserRoles.Add(new UserRole
            {
                UserId = existingUser.Id,
                RoleId = studentRole.Id
            });
        }
        else
        {
            existingUser.PasswordHash = passwordHasher.HashPassword(existingUser, tempPassword);
            existingUser.IsActive = true;

            var hasRole = await dbContext.UserRoles
                .AnyAsync(ur => ur.UserId == existingUser.Id && ur.RoleId == studentRole.Id, cancellationToken);
            if (!hasRole)
            {
                dbContext.UserRoles.Add(new UserRole
                {
                    UserId = existingUser.Id,
                    RoleId = studentRole.Id
                });
            }
        }

        student.UserId = existingUser.Id;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateStudentAccountResponse(existingUser.Id, existingUser.Username, tempPassword);
    }

    public async Task<StudentDetail> UploadImssAsync(
        long id,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var student = await dbContext.Students
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
        {
            throw new NotFoundException("students.not_found", $"Estudiante con id {id} no encontrado.");
        }

        return await SaveImssFileAsync(student, file, cancellationToken);
    }

    public async Task<StudentDetail> UploadMyImssAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var student = await GetCurrentStudentEntityAsync(asNoTracking: false, cancellationToken);
        return await SaveImssFileAsync(student, file, cancellationToken);
    }

    public async Task<(Stream stream, string contentType, string fileName)> GetImssStreamAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var student = await dbContext.Students
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
        {
            throw new NotFoundException("students.not_found", $"Estudiante con id {id} no encontrado.");
        }

        return OpenImssStream(student);
    }

    public async Task<(Stream stream, string contentType, string fileName)> GetMyImssStreamAsync(
        CancellationToken cancellationToken = default)
    {
        var student = await GetCurrentStudentEntityAsync(asNoTracking: true, cancellationToken);
        return OpenImssStream(student);
    }

    public async Task<StudentDetail> ValidateImssAsync(
        long id,
        bool isValid,
        CancellationToken cancellationToken = default)
    {
        var student = await dbContext.Students
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
        {
            throw new NotFoundException("students.not_found", $"Estudiante con id {id} no encontrado.");
        }

        if (string.IsNullOrWhiteSpace(student.ImssValidityFileUrl))
        {
            throw new BusinessRuleException(
                "students.imss_file_missing",
                "El estudiante no ha subido su Constancia de Vigencia de Derechos del IMSS.");
        }

        student.HasValidImss = isValid;
        student.ImssValidatedAt = DateTimeOffset.UtcNow;
        student.ImssValidatedBy = currentUser.Id;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(student.Id, cancellationToken);
    }

    private async Task<StudentDetail> SaveImssFileAsync(
        Student student,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["file"] = ["El archivo de constancia IMSS está vacío."]
            });
        }

        const long maxSizeBytes = 5 * 1024 * 1024;
        if (file.Length > maxSizeBytes)
        {
            throw new AppException(413, "files.too_large", "El archivo supera el límite permitido de 5 MB.", "Archivo demasiado grande");
        }

        string extension;
        using (var stream = file.OpenReadStream())
        {
            var header = new byte[4];
            var read = await stream.ReadAsync(header.AsMemory(0, 4), cancellationToken);
            if (read >= 4 && header.SequenceEqual(PdfMagicBytes))
            {
                extension = ".pdf";
            }
            else if (read >= 4 && header.SequenceEqual(PngMagicBytes))
            {
                extension = ".png";
            }
            else if (read >= 3 && header.Take(3).SequenceEqual(JpgMagicBytes))
            {
                extension = ".jpg";
            }
            else
            {
                throw new AppException(415, "files.unsupported_type", "La Constancia de Vigencia IMSS debe ser un archivo PDF, JPG o PNG válido.", "Formato no compatible");
            }
        }

        var dirPath = Path.Combine(StorageRootPath, "students", student.Id.ToString(), "imss");
        Directory.CreateDirectory(dirPath);

        if (!string.IsNullOrWhiteSpace(student.ImssValidityFileUrl))
        {
            var oldPath = Path.Combine(StorageRootPath, student.ImssValidityFileUrl.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(oldPath))
            {
                try { File.Delete(oldPath); } catch { /* ignore */ }
            }
        }

        var fileName = $"imss_{student.ControlNumber}_{Guid.NewGuid():N}{extension}";
        var relativePath = $"students/{student.Id}/imss/{fileName}";
        var fullPath = Path.Combine(dirPath, fileName);

        using (var target = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
        {
            await file.CopyToAsync(target, cancellationToken);
        }

        student.ImssValidityFileUrl = relativePath;
        student.HasValidImss = false;
        student.ImssValidatedAt = null;
        student.ImssValidatedBy = null;

        await dbContext.SaveChangesAsync(cancellationToken);

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

        var query = dbContext.Students
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

        return new StudentEligibility(
            new EligibilityResult(ssReasons.Count == 0, ssReasons),
            new EligibilityResult(ppReasons.Count == 0, ppReasons));
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

    private static void ValidateRequest(StudentRequest request, int totalCredits)
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
                $"Los créditos acumulados ({request.CompletedCredits}) no pueden exceder el total de la carrera ({totalCredits}).");
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
            s.UserId.HasValue);

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
            s.UpdatedAt);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeOptionalUpper(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? NormalizeOptionalLower(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
