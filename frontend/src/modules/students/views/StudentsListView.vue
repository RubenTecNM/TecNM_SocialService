<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch as watchValidation } from 'vue'
import { useFormValidation } from '../../../shared/composables/useFormValidation'
import { validateFields } from '../../../shared/utils/validationRules'
import UiFormErrors from '../../../shared/components/UiFormErrors.vue'
import DocumentReviewDialog from '../../expedients/components/DocumentReviewDialog.vue'
import { getStudentDocuments } from '../../expedients/api/expedientsApi'
import type { ExpedientDocument } from '../../expedients/types/expedients'
import { problemMessage } from '../../../shared/api/problemDetails'
import { hasRole } from '../../../shared/composables/useSession'
import { useRealtimeRefresh } from '../../../shared/composables/useRealtime'
import { getStudentsCsv } from '../utils/exportStudents'
import {
  UiAlert,
  UiBadge,
  UiBasePage,
  UiButton,
  UiCard,
  UiDialog,
  UiField,
  UiTable,
} from '../../../shared/components'
import { getCareerOptions, getPeriodOptions } from '../../catalogs/api/careersApi'
import type { OptionItem } from '../../catalogs/types/careers'
import {
  createStudent,
  createStudentAccount,
  downloadImportTemplate,
  getStudentById,
  getStudentImssBlob,
  getStudents,
  resetStudentPassword,
  updateStudent,
  uploadStudentsExcel,
} from '../api/studentsApi'
import type {
  CreateStudentAccountResponse,
  SocialServiceFollowUp,
  StudentDetail,
  StudentImportResult,
  StudentListItem,
  StudentRequest,
  StudentShift,
} from '../types/students'

const canFollowUp = computed(() => hasRole('admin', 'social_service_officer', 'department_head'))
const followUpStatus = ref('')
const followUpProgress = ref('')
const followUpLabels: Record<string, string> = {
  first_notice: 'Primer aviso',
  second_notice: 'Segundo aviso',
  third_notice: 'Tercer aviso',
  non_compliant: 'Producto no conforme',
  in_progress: 'En proceso de realización del servicio',
  released: 'Liberado',
  pending_setup: 'Periodo de aptitud pendiente',
}
const assessmentLabels: Record<string, string> = {
  consistent: 'Constante',
  low_progress: 'Con poco avance',
  pending: 'Pendiente de valoración',
}
const canCreate = computed(() =>
  hasRole('admin', 'social_service_officer', 'internship_coordinator', 'department_head'),
)
const canImport = computed(() => hasRole('admin', 'social_service_officer', 'department_head'))
const isExporting = ref(false)
const isDownloadingTemplate = ref(false)
const canRegister = computed(() => hasRole('admin'))
const canReview = computed(() =>
  hasRole('admin', 'social_service_officer', 'internship_coordinator'),
)

interface StudentRow extends StudentListItem {
  actions?: string
}

const students = ref<StudentRow[]>([])
const careerOptions = ref<OptionItem[]>([])
const periodOptions = ref<OptionItem[]>([])

const isLoading = ref(true)
const errorMessage = ref('')
const successMessage = ref('')

// Filtros y Paginación estandarizada (10 por página)
const searchQuery = ref('')
const selectedCareerId = ref<number | string>('')
const selectedShift = ref<string>('')
const selectedImssFilter = ref<string>('')
const currentPage = ref(1)
const pageSize = 10
const totalItems = ref(0)
const totalPages = computed(() => Math.max(1, Math.ceil(totalItems.value / pageSize)))

// Autocomplete con Debounce
const autocompleteSuggestions = ref<StudentListItem[]>([])
const isAutocompleteOpen = ref(false)
let debounceTimer: ReturnType<typeof setTimeout> | null = null

const activeTrackingPeriodLabel = computed(
  () =>
    students.value[0]?.socialServiceFollowUp?.trackingPeriodCode ||
    periodOptions.value.find((p) => p.meta?.isActive)?.label ||
    'Sin periodo activo',
)

const columns: { key: keyof StudentRow & string; label: string }[] = [
  { key: 'fullName', label: 'ALUMNO' },
  { key: 'controlNumber', label: 'MATRÍCULA' },
  { key: 'careerCode', label: 'CARRERA / SEMESTRE' },
  ...(hasRole('admin', 'social_service_officer', 'department_head')
    ? [{ key: 'socialServiceFollowUp' as const, label: 'SEGUIMIENTO SS' }]
    : []),
  { key: 'actions', label: 'DETALLE' },
]

// Modal de Alta / Edición de Alumno (Caso Específico)
const isStudentModalOpen = ref(false)
const isEditing = ref(false)
const editingId = ref<number | null>(null)
const isSubmittingStudent = ref(false)
const selectedDetail = ref<StudentDetail | null>(null)
const imssError = ref('')
const reviewOpen = ref(false)
const reviewDocument = ref<ExpedientDocument | null>(null)
const reviewStudentName = ref('')
const resetCandidate = ref<StudentListItem | null>(null)
const resetOpen = ref(false)
const resetting = ref(false)
const resetError = ref('')
const generatedControlNumber = ref('')

function openDocumentReview(document: ExpedientDocument, name: string) {
  reviewDocument.value = document
  reviewStudentName.value = name
  isStudentModalOpen.value = false
  detailOpen.value = false
  reviewOpen.value = true
}

async function reviewStudentImss() {
  if (!selectedDetail.value) return
  imssError.value = ''
  try {
    const docs = await getStudentDocuments(selectedDetail.value.id, { tramiteType: 'general' })
    const document = docs.find((d) => d.documentCode === 'IMSS' && d.status !== 'superseded')
    if (!document) {
      imssError.value =
        'El alumno debe cargar una constancia actual en su portal. Un archivo histórico no acredita revisión documental.'
      return
    }
    openDocumentReview(
      document,
      `${selectedDetail.value.firstName} ${selectedDetail.value.firstSurname}`,
    )
  } catch (error) {
    imssError.value = problemMessage(error, 'No se pudo consultar la constancia IMSS.')
  }
}

async function afterReview() {
  await loadData()
}

useRealtimeRefresh(
  (evt) => evt.documentCode === 'IMSS' || evt.eventType === 'tramite.updated',
  () => loadData(),
)

async function confirmReset() {
  if (!resetCandidate.value || resetting.value) return
  resetting.value = true
  resetError.value = ''
  try {
    await resetStudentPassword(resetCandidate.value.id)
    successMessage.value =
      'Contraseña restablecida a la matrícula. Las sesiones anteriores fueron invalidadas.'
    resetOpen.value = false
  } catch (error) {
    resetError.value = problemMessage(error, 'No se pudo restablecer la contraseña.')
  } finally {
    resetting.value = false
  }
}

const detailOpen = ref(false)
const detailRow = ref<StudentListItem | null>(null)
const detailLoading = ref(false)
const detailError = ref('')
let detailRequest = 0

async function openDetail(row: StudentListItem) {
  const request = ++detailRequest
  detailRow.value = row
  selectedDetail.value = null
  detailError.value = ''
  imssError.value = ''
  detailOpen.value = true
  detailLoading.value = row.canReadProfile !== false
  if (row.canReadProfile === false) return
  try {
    const result = await getStudentById(row.id)
    if (request === detailRequest) selectedDetail.value = result
  } catch (error) {
    if (request === detailRequest) {
      detailError.value = problemMessage(error, 'No se pudo cargar el detalle del alumno.')
    }
  } finally {
    if (request === detailRequest) detailLoading.value = false
  }
}

function displayDate(value: string | null | undefined) {
  return value
    ? new Date(value).toLocaleString('es-MX', { timeZone: 'America/Mexico_City' })
    : 'Sin registro'
}

function isFollowUpInProgress(followUp: SocialServiceFollowUp): boolean {
  return followUp.status === 'in_progress'
}

function followUpPrimaryStatus(followUp: SocialServiceFollowUp): string {
  if (followUp.status !== 'in_progress') return followUp.status
  if (followUp.elapsedSemesters == null) return 'pending_setup'
  if (followUp.elapsedSemesters <= 0) return 'first_notice'
  if (followUp.elapsedSemesters === 1) return 'second_notice'
  if (followUp.elapsedSemesters === 2) return 'third_notice'
  return 'non_compliant'
}

function followUpTone(row: StudentListItem): 'success' | 'warning' | 'danger' | 'info' {
  const followUp = row.socialServiceFollowUp
  if (!followUp) return 'info'
  const status = followUpPrimaryStatus(followUp)
  if (status === 'non_compliant' || status === 'third_notice') return 'danger'
  if (status === 'released') return 'success'
  if (status === 'second_notice' || followUp.progressAssessment === 'low_progress') return 'warning'
  return followUp.progressAssessment === 'consistent' ? 'success' : 'info'
}

const detailSections = computed(() => {
  const d = selectedDetail.value
  if (!d) return []
  const accountLabels = {
    missing: 'Sin cuenta',
    active: 'Activa',
    inactive: 'Inactiva',
    inconsistent: 'Revisar cuenta',
  }
  return [
    {
      title: 'Datos personales',
      fields: [
        ['Nombre', d.firstName],
        ['Primer apellido', d.firstSurname],
        ['Segundo apellido', d.secondSurname],
        ['CURP', d.curp],
        ['Sexo', d.gender === 'M' ? 'Hombre' : d.gender === 'F' ? 'Mujer' : null],
        ['Discapacidad', d.hasDisability ? d.disabilityDescription || 'Sí' : 'No'],
      ],
    },
    {
      title: 'Datos académicos',
      fields: [
        ['Carrera', d.careerName],
        ['Semestre', String(d.currentSemester)],
        ['Turno', formatShift(d.shift)],
        ['Créditos aprobados', String(d.completedCredits)],
        ['Avance reticular', d.curriculumProgressPercentage + '%'],
        ['Inscripción', d.isEnrolled ? 'Inscrito' : 'No inscrito'],
      ],
    },
    {
      title: 'Contacto y cuenta',
      fields: [
        ['Correo institucional', d.institutionalEmail],
        ['Correo personal', d.personalEmail],
        ['Teléfono', d.phoneNumber],
        ['Cuenta de acceso', accountLabels[d.accountStatus]],
        ['Usuario', d.accountUsername],
      ],
    },
    {
      title: 'Registro',
      fields: [
        ['Alta', displayDate(d.createdAt)],
        ['Última actualización', displayDate(d.updatedAt)],
        ['Lote de importación', d.importBatchId ? String(d.importBatchId) : 'Alta individual'],
      ],
    },
  ]
})

function applyFilters() {
  isAutocompleteOpen.value = false
  currentPage.value = 1
  void loadData()
}

const studentForm = reactive<StudentRequest>({
  careerId: 0,
  controlNumber: '',
  curp: '',
  firstName: '',
  firstSurname: '',
  secondSurname: '',
  gender: 'M',
  shift: 'morning',
  hasDisability: false,
  disabilityDescription: '',
  institutionalEmail: '',
  personalEmail: '',
  phoneNumber: '',
  currentSemester: 6,
  completedCredits: 190,
  isEnrolled: true,
})

// Modal de Importación por Excel
const isImportModalOpen = ref(false)
const importPeriodId = ref<number>(0)
const importFile = ref<File | null>(null)
const isImporting = ref(false)
const importResult = ref<StudentImportResult | null>(null)
const importErrorMessage = ref('')

// Modal de Credenciales Generadas
const isAccountModalOpen = ref(false)
const generatedAccount = ref<CreateStudentAccountResponse | null>(null)

const hasActiveFilters = computed(() => {
  return (
    searchQuery.value.trim() !== '' ||
    Boolean(selectedCareerId.value) ||
    selectedShift.value !== '' ||
    selectedImssFilter.value !== '' ||
    followUpStatus.value !== '' ||
    followUpProgress.value !== ''
  )
})

function formatShift(shift: StudentShift | string | null): string {
  if (!shift) return '—'
  if (shift === 'morning') return 'Matutino'
  if (shift === 'afternoon') return 'Vespertino'
  if (shift === 'night') return 'Nocturno'
  return shift
}

async function loadCatalogs(): Promise<void> {
  try {
    const [careers, periods] = await Promise.all([
      getCareerOptions().catch(() => []),
      getPeriodOptions().catch(() => []),
    ])
    careerOptions.value = careers
    periodOptions.value = periods
    if (careers.length > 0 && !studentForm.careerId) {
      studentForm.careerId = careers[0].id
    }
    if (periods.length > 0 && !importPeriodId.value) {
      importPeriodId.value = periods[0].id
    }
  } catch {
    // silencioso
  }
}

let listRequest = 0
async function loadData(): Promise<void> {
  const request = ++listRequest
  isLoading.value = true
  errorMessage.value = ''
  try {
    const hasValidImss =
      selectedImssFilter.value === 'true'
        ? true
        : selectedImssFilter.value === 'false'
          ? false
          : undefined

    const res = await getStudents({
      page: currentPage.value,
      pageSize,
      search: searchQuery.value.trim() || undefined,
      careerId: Number(selectedCareerId.value) || undefined,
      shift: selectedShift.value || undefined,
      hasValidImss,
      followUpStatus: followUpStatus.value || undefined,
      followUpProgress: followUpProgress.value || undefined,
    })
    if (request !== listRequest) return
    students.value = res.items || []
    totalItems.value = res.totalItems || students.value.length
  } catch (err: any) {
    if (request !== listRequest) return
    students.value = []
    totalItems.value = 0
    errorMessage.value = problemMessage(err, 'Error al cargar padrón de alumnos.')
  } finally {
    if (request === listRequest) isLoading.value = false
  }
}

async function fetchSuggestions(query: string): Promise<void> {
  const q = query.trim()
  if (!q) {
    autocompleteSuggestions.value = []
    isAutocompleteOpen.value = false
    return
  }
  try {
    const res = await getStudents({
      page: 1,
      pageSize: 6,
      search: q,
      careerId: Number(selectedCareerId.value) || undefined,
    })
    autocompleteSuggestions.value = (res.items || []).slice(0, 6)
    isAutocompleteOpen.value = autocompleteSuggestions.value.length > 0
  } catch {
    autocompleteSuggestions.value = []
    isAutocompleteOpen.value = false
  }
}

function handleSearchInput(): void {
  if (debounceTimer) clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    currentPage.value = 1
    void fetchSuggestions(searchQuery.value)
    void loadData()
  }, 280)
}

function selectSuggestion(item: StudentListItem): void {
  searchQuery.value = item.controlNumber
  isAutocompleteOpen.value = false
  currentPage.value = 1
  void loadData()
}

function clearSearch(): void {
  searchQuery.value = ''
  autocompleteSuggestions.value = []
  isAutocompleteOpen.value = false
  currentPage.value = 1
  void loadData()
}

function handleSearchFocus(): void {
  if (searchQuery.value.trim().length > 0 && autocompleteSuggestions.value.length > 0) {
    isAutocompleteOpen.value = true
  }
}

function handleSearchBlur(): void {
  setTimeout(() => {
    isAutocompleteOpen.value = false
  }, 180)
}

function resetAllFilters(): void {
  followUpStatus.value = ''
  followUpProgress.value = ''
  currentPage.value = 1
  searchQuery.value = ''
  autocompleteSuggestions.value = []
  isAutocompleteOpen.value = false
  selectedCareerId.value = ''
  selectedShift.value = ''
  selectedImssFilter.value = ''
  void loadData()
}

function openCreateModal(): void {
  errorMessage.value = ''
  isEditing.value = false
  editingId.value = null
  selectedDetail.value = null
  Object.assign(studentForm, {
    socialServiceEligiblePeriodId: undefined,
    careerId: careerOptions.value[0]?.id ?? 1,
    controlNumber: '',
    curp: '',
    firstName: '',
    firstSurname: '',
    secondSurname: '',
    gender: 'M',
    shift: 'morning',
    hasDisability: false,
    disabilityDescription: '',
    institutionalEmail: '',
    personalEmail: '',
    phoneNumber: '',
    currentSemester: 6,
    completedCredits: 190,
    isEnrolled: true,
  })
  isStudentModalOpen.value = true
}

async function openEditModal(row: StudentListItem): Promise<void> {
  errorMessage.value = ''
  imssError.value = ''
  try {
    const detail = await getStudentById(row.id)
    isEditing.value = true
    editingId.value = detail.id
    selectedDetail.value = detail
    Object.assign(studentForm, {
      socialServiceEligiblePeriodId: detail.socialServiceEligiblePeriodId,
      careerId: detail.careerId,
      controlNumber: detail.controlNumber,
      curp: detail.curp || '',
      firstName: detail.firstName,
      firstSurname: detail.firstSurname,
      secondSurname: detail.secondSurname || '',
      gender: detail.gender || 'M',
      shift: detail.shift,
      hasDisability: detail.hasDisability,
      disabilityDescription: detail.disabilityDescription || '',
      institutionalEmail: detail.institutionalEmail || '',
      personalEmail: detail.personalEmail || '',
      phoneNumber: detail.phoneNumber || '',
      currentSemester: detail.currentSemester,
      completedCredits: detail.completedCredits,
      isEnrolled: detail.isEnrolled,
    })
    isStudentModalOpen.value = true
  } catch (err: any) {
    errorMessage.value = problemMessage(err, 'No se pudo cargar el detalle del estudiante.')
  }
}

function studentRules(): Record<string, string[]> {
  const errors: Record<string, string[]> = {}
  const check = (key: string, invalid: boolean, message: string) => {
    if (invalid) errors[key] = [message]
  }
  check(
    'controlNumber',
    !/^[A-Z0-9_-]{1,32}$/.test(studentForm.controlNumber.trim().toUpperCase()),
    'Indica una matrícula de 1 a 32 caracteres: letras, números, guion o guion bajo.',
  )
  check(
    'curp',
    !!studentForm.curp?.trim() &&
      !/^[A-Z]{4}\d{6}[HM][A-Z]{5}[A-Z0-9]\d$/.test(studentForm.curp.trim().toUpperCase()),
    'La CURP debe tener 18 caracteres con el formato correcto.',
  )
  for (const [key, max] of [
    ['firstName', 100],
    ['firstSurname', 70],
  ] as const) {
    check(
      key,
      !studentForm[key].trim() || studentForm[key].trim().length > max,
      `Este campo es obligatorio (máximo ${max} caracteres).`,
    )
  }
  check(
    'secondSurname',
    (studentForm.secondSurname?.trim().length || 0) > 70,
    'Máximo 70 caracteres.',
  )
  check('careerId', !studentForm.careerId, 'Selecciona una carrera.')
  check(
    'currentSemester',
    !Number.isInteger(studentForm.currentSemester) ||
      studentForm.currentSemester < 1 ||
      studentForm.currentSemester > 15,
    'Indica un semestre entero entre 1 y 15.',
  )
  const total = careerOptions.value.find((c) => c.id === studentForm.careerId)?.meta?.totalCredits
  check(
    'completedCredits',
    !Number.isInteger(studentForm.completedCredits) ||
      studentForm.completedCredits < 0 ||
      (total !== undefined && studentForm.completedCredits > total),
    'Los créditos deben ser un entero entre cero y el total de la carrera.',
  )
  for (const key of ['institutionalEmail', 'personalEmail'] as const) {
    check(
      key,
      !!studentForm[key]?.trim() &&
        (studentForm[key]!.trim().length > 255 || !studentForm[key]!.includes('@')),
      'Indica un correo con @ y máximo 255 caracteres.',
    )
  }
  check(
    'phoneNumber',
    (studentForm.phoneNumber?.trim().length || 0) > 20,
    'El teléfono admite máximo 20 caracteres.',
  )
  check(
    'gender',
    !!studentForm.gender && !['M', 'F'].includes(studentForm.gender.trim().toUpperCase()),
    'Selecciona una opción válida.',
  )
  check(
    'shift',
    !['morning', 'afternoon', 'night'].includes(studentForm.shift.trim().toLowerCase()),
    'Selecciona un turno válido.',
  )
  check(
    'disabilityDescription',
    (studentForm.hasDisability && !studentForm.disabilityDescription?.trim()) ||
      (studentForm.disabilityDescription?.trim().length || 0) > 150,
    'Describe la discapacidad (máximo 150 caracteres).',
  )
  return errors
}

async function handleSaveStudent(): Promise<void> {
  if (isSubmittingStudent.value || !studentValidation.validate()) return
  if (isSubmittingStudent.value || (isEditing.value ? !canRegister.value : !canCreate.value)) return
  isSubmittingStudent.value = true
  errorMessage.value = ''
  successMessage.value = ''
  try {
    const payload: StudentRequest = {
      ...studentForm,
      controlNumber: studentForm.controlNumber.trim().toUpperCase(),
      curp: studentForm.curp?.trim().toUpperCase() || null,
      secondSurname: studentForm.secondSurname?.trim() || null,
      institutionalEmail: studentForm.institutionalEmail?.trim() || null,
      personalEmail: studentForm.personalEmail?.trim() || null,
      phoneNumber: studentForm.phoneNumber?.trim() || null,
      disabilityDescription: studentForm.hasDisability
        ? studentForm.disabilityDescription?.trim() || null
        : null,
    }

    if (isEditing.value && editingId.value) {
      await updateStudent(editingId.value, payload)
      successMessage.value = `Expediente de ${payload.controlNumber} actualizado correctamente.`
    } else {
      await createStudent(payload)
      successMessage.value = `Alumno ${payload.controlNumber} agregado al padrón exitosamente.`
    }
    isStudentModalOpen.value = false
    await loadData()
  } catch (err: any) {
    studentValidation.apply(err, 'No se pudo guardar el formulario.')
    errorMessage.value = problemMessage(err, 'Error al guardar los datos del alumno.')
  } finally {
    isSubmittingStudent.value = false
  }
}

async function handleDownloadStudentImss(id: number, controlNumber: string): Promise<void> {
  try {
    const blob = await getStudentImssBlob(id)
    const url = window.URL.createObjectURL(blob)
    window.open(url, '_blank')
  } catch {
    errorMessage.value = `El alumno ${controlNumber} aún no tiene un archivo PDF de vigencia IMSS almacenado.`
  }
}

async function handleCreateAccount(row: StudentListItem): Promise<void> {
  errorMessage.value = ''
  try {
    const res = await createStudentAccount(row.id)
    generatedAccount.value = res
    generatedControlNumber.value = row.controlNumber
    isAccountModalOpen.value = true
    await loadData()
  } catch (err: any) {
    errorMessage.value = problemMessage(
      err,
      'No se pudo generar la cuenta de usuario para el alumno.',
    )
  }
}

async function handleExportStudents(): Promise<void> {
  if (isExporting.value || isLoading.value) return
  isExporting.value = true
  errorMessage.value = ''
  try {
    const csv = await getStudentsCsv({
      search: searchQuery.value.trim() || undefined,
      careerId: Number(selectedCareerId.value) || undefined,
      shift: selectedShift.value || undefined,
      hasValidImss:
        selectedImssFilter.value === '' ? undefined : selectedImssFilter.value === 'true',
      followUpStatus: followUpStatus.value || undefined,
      followUpProgress: followUpProgress.value || undefined,
    })
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8;' }))
    const link = document.createElement('a')
    link.href = url
    link.download = 'Alumnos_TecNM.csv'
    link.click()
    setTimeout(() => URL.revokeObjectURL(url), 1000)
  } catch (error) {
    errorMessage.value = problemMessage(
      error,
      'No se pudo exportar el listado de alumnos. Vuelve a intentar.',
    )
  } finally {
    isExporting.value = false
  }
}

async function handleDownloadTemplate(): Promise<void> {
  if (isDownloadingTemplate.value || !canCreate.value) return
  isDownloadingTemplate.value = true
  try {
    const blob = await downloadImportTemplate()
    const url = window.URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'Plantilla_Importacion_Estudiantes_TecNM.xlsx'
    a.click()
    window.URL.revokeObjectURL(url)
  } catch {
    errorMessage.value = 'No se pudo descargar la plantilla oficial de Excel.'
  } finally {
    isDownloadingTemplate.value = false
  }
}

function handleExcelFileChange(event: Event): void {
  const input = event.target as HTMLInputElement
  importFile.value = input.files?.[0] ?? null
}

function openImportModal(): void {
  importResult.value = null
  importFile.value = null
  importErrorMessage.value = ''
  errorMessage.value = ''
  isImportModalOpen.value = true
}

async function handleRunImport(): Promise<void> {
  if (isImporting.value || !importValidation.validate()) return
  if (!importFile.value || !importPeriodId.value) return
  isImporting.value = true
  errorMessage.value = ''
  importErrorMessage.value = ''
  try {
    const result = await uploadStudentsExcel(importPeriodId.value, importFile.value)
    importResult.value = result
    await loadData()

    if (result.failedRecords > 0 || (result.rowErrors && result.rowErrors.length > 0)) {
      importErrorMessage.value = `Se importaron ${result.importedRecords} alumnos, pero ${result.failedRecords} filas presentaron errores. Revisa el detalle abajo.`
    } else {
      successMessage.value = `Importación finalizada: ${result.importedRecords} alumnos; ${result.createdAccounts} cuentas creadas; ${result.existingAccounts} existentes; ${result.failedRecords} filas rechazadas.`
      isImportModalOpen.value = false
      importResult.value = null
      importFile.value = null
    }
  } catch (err: any) {
    importValidation.apply(err, 'No se pudo importar el archivo.')
    importErrorMessage.value = problemMessage(err, 'Error al procesar el archivo Excel (.xlsx).')
    errorMessage.value = importErrorMessage.value
  } finally {
    isImporting.value = false
  }
}

onBeforeUnmount(() => {
  if (debounceTimer) clearTimeout(debounceTimer)
  listRequest++
  detailRequest++
})

onMounted(async () => {
  await Promise.all([loadCatalogs(), loadData()])
})

const studentTotalCredits = computed(
  () => careerOptions.value.find((c) => c.id === studentForm.careerId)?.meta?.totalCredits,
)
const importValidation = useFormValidation(() =>
  validateFields(
    { periodId: importPeriodId.value, file: importFile.value },
    { periodId: { required: true, min: 1 }, file: { required: true } },
  ),
)
watchValidation(isImportModalOpen, () => {
  importValidation.reset()
  importErrorMessage.value = ''
})
const studentValidation = useFormValidation(studentRules)
watchValidation(isStudentModalOpen, () => {
  studentValidation.reset()
  errorMessage.value = ''
})
</script>

<template>
  <UiBasePage :fluid="true" class="fullscreen-page">
    <!-- Encabezado Ejecutivo Compacto -->
    <header class="exec-header">
      <div class="exec-header__left">
        <div class="exec-eyebrow">
          <span class="exec-badge">DEPARTAMENTO DE GESTIÓN TECNOLÓGICA Y VINCULACIÓN</span>
          <span v-if="canFollowUp" class="exec-period">
            · Periodo de seguimiento: <strong>{{ activeTrackingPeriodLabel }}</strong>
          </span>
        </div>
        <div class="title-row">
          <h1 class="exec-title">Padrón de Estudiantes Elegibles</h1>
          <span class="count-badge">{{ totalItems }} candidatos</span>
        </div>
        <p class="exec-subtitle">
          Carga de alumnos candidatos por archivo Excel o registro individual para casos específicos
        </p>
      </div>

      <div class="actions-group" role="group" aria-label="Acciones del padrón">
        <UiButton
          variant="secondary"
          size="sm"
          class="action-btn"
          :loading="isExporting"
          :disabled="isLoading || !totalItems"
          @click="handleExportStudents"
        >
          {{ isExporting ? 'Exportando…' : 'Exportar alumnos (.csv)' }}
        </UiButton>
        <UiButton
          v-if="canCreate"
          variant="secondary"
          size="sm"
          class="action-btn"
          :loading="isDownloadingTemplate"
          @click="handleDownloadTemplate"
        >
          <svg viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2" fill="none" aria-hidden="true">
            <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
            <polyline points="7 10 12 15 17 10" />
            <line x1="12" y1="15" x2="12" y2="3" />
          </svg>
          Descargar Plantilla Excel (.xlsx)
        </UiButton>
        <UiButton
          v-if="canImport"
          variant="secondary"
          size="sm"
          class="action-btn"
          @click="openImportModal"
        >
          <svg viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2" fill="none" aria-hidden="true">
            <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
            <polyline points="17 8 12 3 7 8" />
            <line x1="12" y1="3" x2="12" y2="15" />
          </svg>
          Importar Padrón Excel
        </UiButton>
        <UiButton
          v-if="canCreate"
          variant="primary"
          size="sm"
          class="action-btn primary"
          @click="openCreateModal"
        >
          <svg viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2.2" fill="none" aria-hidden="true">
            <line x1="12" y1="5" x2="12" y2="19" />
            <line x1="5" y1="12" x2="19" y2="12" />
          </svg>
          Agregar alumno manualmente
        </UiButton>
      </div>
    </header>

    <!-- Alertas de Retroalimentación -->
    <UiAlert
      v-if="errorMessage && !(studentValidation.hasErrors.value || importValidation.hasErrors.value)"
      tone="danger"
      class="compact-alert"
    >
      {{ errorMessage }}
    </UiAlert>
    <UiAlert v-if="successMessage" tone="success" class="compact-alert">
      {{ successMessage }}
    </UiAlert>

    <!-- Tarjeta Principal a Pantalla Completa -->
    <UiCard class="workspace-card">
      <div class="filters-bar">
        <div class="roster-filters">
          <div class="search-box">
            <label class="filter-label" for="roster-search">Buscar alumno</label>
            <div class="autocomplete-container">
              <div class="input-wrapper">
                <svg
                  class="search-icon"
                  viewBox="0 0 24 24"
                  width="15"
                  height="15"
                  stroke="currentColor"
                  stroke-width="2"
                  fill="none"
                  aria-hidden="true"
                >
                  <circle cx="11" cy="11" r="8" />
                  <line x1="21" y1="21" x2="16.65" y2="16.65" />
                </svg>
                <input
                  id="roster-search"
                  v-model="searchQuery"
                  type="search"
                  class="modern-input"
                  placeholder="Nombre, apellidos o matrícula (ej. I23050363)..."
                  autocomplete="off"
                  @input="handleSearchInput"
                  @focus="handleSearchFocus"
                  @blur="handleSearchBlur"
                  @keyup.enter="applyFilters"
                />
                <button
                  v-if="searchQuery"
                  type="button"
                  class="clear-search-btn"
                  aria-label="Limpiar búsqueda"
                  @click="clearSearch"
                >
                  ✕
                </button>
              </div>

              <!-- Sugerencias Dinámicas Autocomplete -->
              <div
                v-if="isAutocompleteOpen && autocompleteSuggestions.length > 0"
                class="suggestions-dropdown"
                role="listbox"
              >
                <div
                  v-for="item in autocompleteSuggestions"
                  :key="item.id"
                  class="suggestion-item"
                  role="option"
                  @mousedown.prevent="selectSuggestion(item)"
                >
                  <div class="suggestion-left">
                    <span class="suggestion-control">{{ item.controlNumber }}</span>
                    <span class="suggestion-name">{{ item.fullName }}</span>
                  </div>
                  <div class="suggestion-right">
                    <span class="suggestion-career">{{ item.careerCode }}</span>
                    <span v-if="item.currentSemester != null" class="suggestion-progress">
                      Sem. {{ item.currentSemester }}
                    </span>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div class="select-box">
            <label class="filter-label" for="roster-career">Carrera</label>
            <div class="select-wrapper">
              <select
                id="roster-career"
                v-model="selectedCareerId"
                class="modern-select"
                @change="applyFilters"
              >
                <option value="">Todas las carreras</option>
                <option v-for="c in careerOptions" :key="c.id" :value="c.id">{{ c.label }}</option>
              </select>
              <svg
                class="select-chevron"
                viewBox="0 0 24 24"
                width="14"
                height="14"
                stroke="currentColor"
                stroke-width="2"
                fill="none"
                aria-hidden="true"
              >
                <polyline points="6 9 12 15 18 9" />
              </svg>
            </div>
          </div>

          <div v-if="canFollowUp" class="select-box">
            <label class="filter-label" for="follow-up-status">Seguimiento SS</label>
            <div class="select-wrapper">
              <select
                id="follow-up-status"
                v-model="followUpStatus"
                class="modern-select"
                @change="applyFilters"
              >
                <option value="">Todos</option>
                <option v-for="(label, value) in followUpLabels" :key="value" :value="value">
                  {{ label }}
                </option>
              </select>
              <svg
                class="select-chevron"
                viewBox="0 0 24 24"
                width="14"
                height="14"
                stroke="currentColor"
                stroke-width="2"
                fill="none"
                aria-hidden="true"
              >
                <polyline points="6 9 12 15 18 9" />
              </svg>
            </div>
          </div>
        </div>

        <div class="roster-filter-actions">
          <details class="extra-filters">
            <summary>Más filtros</summary>
            <div class="extra-filter-fields">
              <div class="select-box">
                <label class="filter-label" for="roster-shift">Turno</label>
                <div class="select-wrapper">
                  <select
                    id="roster-shift"
                    v-model="selectedShift"
                    class="modern-select"
                    @change="applyFilters"
                  >
                    <option value="">Todos los turnos</option>
                    <option value="morning">Matutino</option>
                    <option value="afternoon">Vespertino</option>
                    <option value="night">Nocturno</option>
                  </select>
                  <svg class="select-chevron" viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2" fill="none" aria-hidden="true">
                    <polyline points="6 9 12 15 18 9" />
                  </svg>
                </div>
              </div>

              <div class="select-box">
                <label class="filter-label" for="roster-imss">Vigencia IMSS</label>
                <div class="select-wrapper">
                  <select
                    id="roster-imss"
                    v-model="selectedImssFilter"
                    class="modern-select"
                    @change="applyFilters"
                  >
                    <option value="">Todos los estados</option>
                    <option value="true">Vigente</option>
                    <option value="false">Pendiente</option>
                  </select>
                  <svg class="select-chevron" viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2" fill="none" aria-hidden="true">
                    <polyline points="6 9 12 15 18 9" />
                  </svg>
                </div>
              </div>

              <div v-if="canFollowUp" class="select-box">
                <label class="filter-label" for="follow-up-progress">Constancia documental</label>
                <div class="select-wrapper">
                  <select
                    id="follow-up-progress"
                    v-model="followUpProgress"
                    class="modern-select"
                    @change="applyFilters"
                  >
                    <option value="">Todas</option>
                    <option v-for="(label, value) in assessmentLabels" :key="value" :value="value">
                      {{ label }}
                    </option>
                  </select>
                  <svg class="select-chevron" viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2" fill="none" aria-hidden="true">
                    <polyline points="6 9 12 15 18 9" />
                  </svg>
                </div>
              </div>
            </div>
          </details>

          <UiButton
            v-if="hasActiveFilters"
            variant="secondary"
            size="sm"
            class="reset-btn"
            @click="resetAllFilters"
          >
            Limpiar filtros
          </UiButton>
        </div>
      </div>

      <p v-if="isLoading" class="roster-feedback" role="status">Cargando alumnos…</p>

      <div class="roster-table" :aria-busy="isLoading">
        <UiTable class="compact-table" label="Listado del Padrón de Alumnos" :columns="columns" :rows="students">
          <template #cell-fullName="{ row }">
            <span class="roster-name">{{ row.fullName }}</span>
          </template>
          <template #cell-controlNumber="{ row }">
            <span class="roster-control">{{ row.controlNumber }}</span>
          </template>
          <template #cell-careerCode="{ row }">
            <div class="roster-career">
              <span class="career-pill">{{ row.careerCode }}</span>
              <small v-if="row.currentSemester != null">Semestre {{ row.currentSemester }}</small>
            </div>
          </template>
          <template #cell-socialServiceFollowUp="{ row }">
            <div v-if="row.socialServiceFollowUp" class="roster-status">
              <UiBadge :tone="followUpTone(row)">
                {{ followUpLabels[followUpPrimaryStatus(row.socialServiceFollowUp)] }}
              </UiBadge>
              <small v-if="isFollowUpInProgress(row.socialServiceFollowUp)" class="roster-status-process">
                En proceso de realización del servicio
              </small>
              <small v-if="isFollowUpInProgress(row.socialServiceFollowUp)">
                {{ assessmentLabels[row.socialServiceFollowUp.progressAssessment || 'pending'] }}
              </small>
            </div>
          </template>
          <template #cell-actions="{ row }">
            <UiButton
              variant="secondary"
              size="sm"
              :aria-label="'Ver detalle de ' + row.fullName"
              @click="openDetail(row)"
            >
              Ver detalle
            </UiButton>
          </template>
        </UiTable>
      </div>

      <p v-if="!isLoading && !students.length && !errorMessage" class="roster-feedback" role="status">
        No hay alumnos que coincidan con los filtros.
      </p>

      <div class="pagination-bar">
        <span class="pagination-info">
          Mostrando <strong>{{ students.length }}</strong> de <strong>{{ totalItems }}</strong> alumnos · Página <strong>{{ currentPage }}</strong> de <strong>{{ totalPages }}</strong>
        </span>
        <div class="pagination-controls">
          <UiButton
            variant="secondary"
            size="sm"
            :disabled="isLoading || currentPage <= 1"
            @click="currentPage--; loadData()"
          >
            Anterior
          </UiButton>
          <UiButton
            variant="secondary"
            size="sm"
            :disabled="isLoading || currentPage >= totalPages"
            @click="currentPage++; loadData()"
          >
            Siguiente
          </UiButton>
        </div>
      </div>
    </UiCard>

    <!-- ================================================================== -->
    <!-- MODAL DETALLE DEL ALUMNO                                           -->
    <!-- ================================================================== -->
    <UiDialog v-model="detailOpen" size="lg" title="Detalle del alumno">
      <div v-if="detailRow" class="student-detail">
        <header class="student-detail-heading">
          <h2>{{ detailRow.fullName }}</h2>
          <p>{{ detailRow.controlNumber }} · {{ detailRow.careerCode }}</p>
        </header>
        <p v-if="detailLoading" role="status">Cargando expediente…</p>
        <UiAlert v-if="detailError" tone="danger">
          <p>{{ detailError }}</p>
          <UiButton variant="secondary" size="sm" @click="openDetail(detailRow)">Reintentar</UiButton>
        </UiAlert>
        <section v-if="detailRow.socialServiceFollowUp" class="detail-section">
          <h3>Seguimiento de Servicio Social</h3>
          <div class="detail-status-header">
            <UiBadge :tone="followUpTone(detailRow)">
              {{ followUpLabels[followUpPrimaryStatus(detailRow.socialServiceFollowUp)] }}
            </UiBadge>
            <span v-if="isFollowUpInProgress(detailRow.socialServiceFollowUp)" class="detail-status-secondary">
              En proceso de realización del servicio
            </span>
          </div>
          <dl class="detail-fields">
            <div v-if="isFollowUpInProgress(detailRow.socialServiceFollowUp)">
              <dt>Constancia documental</dt>
              <dd>{{ assessmentLabels[detailRow.socialServiceFollowUp.progressAssessment || 'pending'] }}</dd>
            </div>
            <div v-if="isFollowUpInProgress(detailRow.socialServiceFollowUp) && detailRow.socialServiceFollowUp.autoReason">
              <dt>Criterio</dt>
              <dd>{{ detailRow.socialServiceFollowUp.autoReason }}</dd>
            </div>
            <div>
              <dt>Primer periodo de aptitud</dt>
              <dd>{{ detailRow.socialServiceFollowUp.eligiblePeriodCode || 'Pendiente de confirmar' }}</dd>
            </div>
            <div>
              <dt>Periodo de seguimiento</dt>
              <dd>{{ detailRow.socialServiceFollowUp.trackingPeriodCode || 'Sin periodo activo' }}</dd>
            </div>
            <div>
              <dt>Semestres desde la aptitud</dt>
              <dd>{{ detailRow.socialServiceFollowUp.elapsedSemesters ?? 'Pendiente' }}</dd>
            </div>
            <div>
              <dt>Evidencias aprobadas</dt>
              <dd>{{ detailRow.socialServiceFollowUp.approvedDocumentsCount }}</dd>
            </div>
            <div>
              <dt>Última evidencia aprobada</dt>
              <dd>{{ displayDate(detailRow.socialServiceFollowUp.lastApprovedAt) }}</dd>
            </div>
          </dl>
        </section>
        <UiAlert v-if="detailRow.canReadProfile === false" tone="info">
          Este candidato dispone de un resumen de seguimiento. Los datos personales y académicos completos requieren acceso a su expediente.
        </UiAlert>
        <section v-for="section in detailSections" :key="section.title" class="detail-section">
          <h3>{{ section.title }}</h3>
          <dl class="detail-fields">
            <div v-for="field in section.fields" :key="field[0] ?? ''">
              <dt>{{ field[0] }}</dt>
              <dd>{{ field[1] || 'Sin registro' }}</dd>
            </div>
          </dl>
        </section>
        <section v-if="selectedDetail" class="detail-section">
          <h3>Vigencia IMSS</h3>
          <dl class="detail-fields">
            <div>
              <dt>Estado documental</dt>
              <dd>
                {{
                  ({
                    missing: 'Sin documento',
                    pending: 'Pendiente de revisión',
                    approved: 'Aprobado',
                    rejected: 'Rechazado',
                  })[selectedDetail.imssStatus] || selectedDetail.imssStatus
                }}
              </dd>
            </div>
            <div>
              <dt>Validó</dt>
              <dd>{{ selectedDetail.imssValidatedByName || 'Sin registro' }}</dd>
            </div>
            <div>
              <dt>Fecha de validación</dt>
              <dd>{{ displayDate(selectedDetail.imssValidatedAt) }}</dd>
            </div>
            <div v-if="selectedDetail.imssRejectionReason">
              <dt>Motivo de rechazo</dt>
              <dd>{{ selectedDetail.imssRejectionReason }}</dd>
            </div>
          </dl>
          <UiAlert v-if="imssError" tone="danger">{{ imssError }}</UiAlert>
          <div class="detail-actions">
            <UiButton
              v-if="selectedDetail.hasImssFile"
              variant="secondary"
              size="sm"
              @click="handleDownloadStudentImss(selectedDetail.id, selectedDetail.controlNumber)"
            >
              Ver PDF IMSS
            </UiButton>
            <UiButton
              v-if="canReview"
              variant="secondary"
              size="sm"
              :disabled="!selectedDetail.imssDocumentId"
              @click="reviewStudentImss"
            >
              {{ selectedDetail.imssStatus === 'pending' ? 'Revisar IMSS' : 'Ver dictamen IMSS' }}
            </UiButton>
          </div>
        </section>
        <section v-if="canRegister && selectedDetail" class="detail-section">
          <h3>Administración</h3>
          <div class="detail-actions">
            <UiButton variant="secondary" size="sm" @click="detailOpen = false; openEditModal(detailRow)">
              Editar datos
            </UiButton>
            <UiButton
              v-if="!detailRow.hasUserAccount"
              variant="secondary"
              size="sm"
              @click="detailOpen = false; handleCreateAccount(detailRow)"
            >
              Crear cuenta
            </UiButton>
            <UiButton
              v-else
              variant="secondary"
              size="sm"
              @click="detailOpen = false; resetCandidate = detailRow; resetError = ''; resetOpen = true"
            >
              Restablecer contraseña
            </UiButton>
          </div>
        </section>
      </div>
      <template #footer>
        <UiButton variant="secondary" size="sm" @click="detailOpen = false">Cerrar detalle</UiButton>
      </template>
    </UiDialog>

    <!-- ================================================================== -->
    <!-- MODAL 1: ALTA / EDICIÓN INDIVIDUAL DE ALUMNO (CASO ESPECÍFICO)      -->
    <!-- ================================================================== -->
    <UiDialog
      v-model="isStudentModalOpen"
      size="lg"
      :title="isEditing ? `Expediente · ${studentForm.controlNumber}` : 'Agregar Alumno (Caso Específico)'"
    >
      <UiAlert
        v-if="errorMessage && !(studentValidation.hasErrors.value || importValidation.hasErrors.value)"
        tone="danger"
      >
        {{ errorMessage }}
      </UiAlert>
      <fieldset
        class="modal-form-grid"
        :disabled="isEditing ? !canRegister : !canCreate"
        style="border: 0; padding: 0; margin: 0"
      >
        <UiFormErrors :validation="studentValidation" />
        <UiField
          label="Primer periodo de aptitud SS"
          hint="Primer semestre como candidato"
          v-bind="studentValidation.field('socialServiceEligiblePeriodId', 'Primer periodo de aptitud SS')"
          v-slot="{ controlAttrs }"
        >
          <select v-bind="controlAttrs" v-model="studentForm.socialServiceEligiblePeriodId" class="modal-select">
            <option :value="undefined">Activo al crear / conservar al editar</option>
            <option v-for="p in periodOptions" :key="p.id" :value="p.id">{{ p.label }}</option>
          </select>
        </UiField>
        <UiField
          label="No. de Control *"
          hint="Ej. I23050363"
          v-bind="studentValidation.field('controlNumber', 'No. de Control *')"
          v-slot="{ controlAttrs }"
        >
          <input
            v-bind="controlAttrs"
            v-model="studentForm.controlNumber"
            type="text"
            class="modal-input"
            placeholder="I23050999"
          />
        </UiField>

        <UiField
          label="Carrera *"
          v-bind="studentValidation.field('careerId', 'Carrera *')"
          v-slot="{ controlAttrs }"
        >
          <select v-bind="controlAttrs" v-model.number="studentForm.careerId" class="modal-select">
            <option v-for="c in careerOptions" :key="c.id" :value="c.id">{{ c.label }}</option>
          </select>
        </UiField>

        <UiField
          label="Nombre(s) *"
          v-bind="studentValidation.field('firstName', 'Nombre(s) *')"
          v-slot="{ controlAttrs }"
        >
          <input
            v-bind="controlAttrs"
            v-model="studentForm.firstName"
            type="text"
            class="modal-input"
            placeholder="Nombre del alumno"
          />
        </UiField>

        <UiField
          label="Primer Apellido *"
          v-bind="studentValidation.field('firstSurname', 'Primer Apellido *')"
          v-slot="{ controlAttrs }"
        >
          <input
            v-bind="controlAttrs"
            v-model="studentForm.firstSurname"
            type="text"
            class="modal-input"
            placeholder="Apellido paterno"
          />
        </UiField>

        <UiField
          label="Segundo Apellido"
          v-bind="studentValidation.field('secondSurname', 'Segundo Apellido')"
          v-slot="{ controlAttrs }"
        >
          <input
            v-bind="controlAttrs"
            v-model="studentForm.secondSurname"
            type="text"
            class="modal-input"
            placeholder="Apellido materno"
          />
        </UiField>

        <UiField
          label="CURP"
          v-bind="studentValidation.field('curp', 'CURP')"
          v-slot="{ controlAttrs }"
        >
          <input
            v-bind="controlAttrs"
            v-model="studentForm.curp"
            type="text"
            class="modal-input"
            maxlength="18"
            placeholder="18 caracteres"
          />
        </UiField>

        <UiField
          label="Semestre Actual (1-15) *"
          v-bind="studentValidation.field('currentSemester', 'Semestre Actual (1-15) *')"
          v-slot="{ controlAttrs }"
        >
          <input
            v-bind="controlAttrs"
            v-model.number="studentForm.currentSemester"
            type="number"
            class="modal-input"
            min="1"
            max="15"
          />
        </UiField>

        <UiField
          label="Créditos Aprobados *"
          :hint="`Total retícula: ${studentTotalCredits ?? 'Consulta la carrera'}`"
          v-bind="studentValidation.field('completedCredits', 'Créditos aprobados')"
          v-slot="{ controlAttrs }"
        >
          <input
            v-bind="controlAttrs"
            v-model.number="studentForm.completedCredits"
            type="number"
            class="modal-input"
            min="0"
            :max="careerOptions.find((c) => c.id === studentForm.careerId)?.meta?.totalCredits"
          />
        </UiField>

        <UiField
          label="Turno *"
          v-bind="studentValidation.field('shift', 'Turno *')"
          v-slot="{ controlAttrs }"
        >
          <select v-bind="controlAttrs" v-model="studentForm.shift" class="modal-select">
            <option value="morning">Matutino</option>
            <option value="afternoon">Vespertino</option>
            <option value="night">Nocturno</option>
          </select>
        </UiField>

        <UiField
          label="Sexo (Estadístico 911)"
          v-bind="studentValidation.field('gender', 'Sexo (Estadístico 911)')"
          v-slot="{ controlAttrs }"
        >
          <select v-bind="controlAttrs" v-model="studentForm.gender" class="modal-select">
            <option value="M">Hombre (M)</option>
            <option value="F">Mujer (F)</option>
          </select>
        </UiField>

        <UiField
          label="Correo Institucional"
          v-bind="studentValidation.field('institutionalEmail', 'Correo Institucional')"
          v-slot="{ controlAttrs }"
        >
          <input
            v-bind="controlAttrs"
            v-model="studentForm.institutionalEmail"
            type="email"
            class="modal-input"
            placeholder="L23050999@monclova.tecnm.mx"
          />
        </UiField>

        <UiField
          label="Teléfono de Contacto"
          v-bind="studentValidation.field('phoneNumber', 'Teléfono de Contacto')"
          v-slot="{ controlAttrs }"
        >
          <input
            v-bind="controlAttrs"
            v-model="studentForm.phoneNumber"
            type="text"
            class="modal-input"
            placeholder="8661234567"
          />
        </UiField>
      </fieldset>

      <!-- Sección de Constancia de Vigencia IMSS en edición o alta -->
      <div class="imss-box">
        <div class="imss-box-header">
          <strong>Constancia de Vigencia de Derechos del IMSS (PDF)</strong>
          <span
            v-if="selectedDetail"
            class="imss-status-tag"
            :class="{ valid: selectedDetail.hasValidImss }"
          >
            {{ selectedDetail.hasValidImss ? 'Vigente y Validado' : 'Pendiente de Validación' }}
          </span>
        </div>
        <p class="imss-box-desc">
          Revisa que el PDF corresponda al alumno y muestre estado VIGENTE antes de aprobarlo.
        </p>
        <p>El alumno debe cargar su constancia desde su portal.</p>

        <UiAlert v-if="imssError" tone="danger" role="alert">{{ imssError }}</UiAlert>
        <p v-if="selectedDetail?.imssStatus === 'missing'">
          {{
            selectedDetail.hasImssFile
              ? 'Archivo histórico: requiere nueva carga documental.'
              : 'Sin constancia documental: el alumno debe cargarla desde su perfil.'
          }}
        </p>
        <p v-if="selectedDetail?.imssRejectionReason">{{ selectedDetail.imssRejectionReason }}</p>
        <div v-if="isEditing && selectedDetail" class="imss-actions-row">
          <UiButton
            v-if="selectedDetail.hasImssFile"
            variant="secondary"
            size="sm"
            @click="handleDownloadStudentImss(selectedDetail.id, selectedDetail.controlNumber)"
          >
            Ver PDF IMSS Almacenado
          </UiButton>
          <UiButton
            v-if="canReview"
            variant="secondary"
            size="sm"
            :disabled="!selectedDetail.imssDocumentId"
            @click="reviewStudentImss"
          >
            {{ selectedDetail.imssStatus === 'pending' ? 'Revisar IMSS' : 'Ver dictamen IMSS' }}
          </UiButton>
        </div>
      </div>

      <template #footer>
        <UiButton variant="secondary" size="sm" @click="isStudentModalOpen = false">Cancelar</UiButton>
        <UiButton
          v-if="isEditing ? canRegister : canCreate"
          variant="primary"
          size="sm"
          :disabled="isSubmittingStudent"
          @click="handleSaveStudent"
        >
          {{ isSubmittingStudent ? 'Guardando...' : isEditing ? 'Guardar Cambios' : 'Registrar Alumno' }}
        </UiButton>
      </template>
    </UiDialog>

    <!-- ================================================================== -->
    <!-- MODAL 2: IMPORTACIÓN MASIVA DESDE EXCEL (.XLSX)                     -->
    <!-- ================================================================== -->
    <UiDialog
      v-model="isImportModalOpen"
      size="md"
      title="Importar Padrón de Alumnos desde Excel (.xlsx)"
    >
      <div class="import-modal-content">
        <p>
          Las cuentas nuevas usarán el correo institucional y su matrícula como contraseña. Las cuentas existentes conservan su contraseña.
        </p>
        <p class="import-intro-text">
          Carga el archivo Excel con los alumnos que están en condiciones de realizar su Servicio Social o Prácticas Profesionales en el periodo seleccionado.
        </p>

        <UiAlert v-if="importErrorMessage && !importValidation.hasErrors.value" tone="danger">
          {{ importErrorMessage }}
        </UiAlert>
        <UiFormErrors :validation="importValidation" />

        <UiField
          v-bind="importValidation.field('periodId', 'Periodo escolar destino')"
          label="Periodo Escolar Destino *"
          v-slot="{ controlAttrs }"
        >
          <select v-bind="controlAttrs" v-model.number="importPeriodId" class="modal-select">
            <option v-for="p in periodOptions" :key="p.id" :value="p.id">{{ p.label }}</option>
          </select>
        </UiField>

        <UiField
          v-bind="importValidation.field('file', 'Archivo Excel')"
          label="Archivo Excel (.xlsx) *"
          v-slot="{ controlAttrs }"
        >
          <input
            v-bind="controlAttrs"
            type="file"
            accept=".xlsx"
            class="file-input"
            @change="handleExcelFileChange"
          />
        </UiField>

        <div v-if="importResult" class="import-summary-box">
          <strong>Resultado de la Importación ({{ importResult.fileName }}):</strong>
          <ul>
            <li>Total de filas leídas: <strong>{{ importResult.totalRecords }}</strong></li>
            <li>Alumnos importados/actualizados: <strong>{{ importResult.importedRecords }}</strong></li>
            <li>Cuentas creadas: <strong>{{ importResult.createdAccounts }}</strong></li>
            <li>Cuentas existentes: <strong>{{ importResult.existingAccounts }}</strong></li>
            <li>Filas con error: <strong>{{ importResult.failedRecords }}</strong></li>
          </ul>
          <div v-if="importResult.rowErrors?.length" class="import-errors-list">
            <div v-for="(err, idx) in importResult.rowErrors.slice(0, 5)" :key="idx">
              Fila {{ err.row }} ({{ err.controlNumber || 'S/N' }}): {{ err.message }}
            </div>
          </div>
        </div>
      </div>

      <template #footer>
        <div class="import-modal-footer">
          <UiButton variant="secondary" size="sm" @click="handleDownloadTemplate">
            Descargar Plantilla Oficial
          </UiButton>
          <div class="footer-actions">
            <UiButton variant="secondary" size="sm" @click="isImportModalOpen = false">Cerrar</UiButton>
            <UiButton
              variant="primary"
              size="sm"
              :disabled="!importFile || isImporting"
              @click="handleRunImport"
            >
              {{ isImporting ? 'Importando...' : 'Subir y Procesar Excel' }}
            </UiButton>
          </div>
        </div>
      </template>
    </UiDialog>

    <!-- ================================================================== -->
    <!-- MODAL 3: CREDENCIALES DE ACCESO GENERADAS PARA ALUMNO               -->
    <!-- ================================================================== -->
    <UiDialog v-model="isAccountModalOpen" size="sm" title="Cuenta de Acceso Creada">
      <div v-if="generatedAccount" class="account-modal-body">
        <UiAlert tone="success">
          Se ha creado la cuenta de acceso institucional para el estudiante. Comparte estas credenciales:
        </UiAlert>
        <div class="credential-item">
          <span class="cred-label">Usuario:</span>
          <strong class="cred-value">{{ generatedAccount.username }}</strong>
        </div>
        <div class="credential-item">
          <span class="cred-label">Contraseña habitual:</span>
          <code class="cred-pass">{{ generatedControlNumber }}</code>
        </div>
      </div>
      <template #footer>
        <UiButton variant="primary" size="sm" @click="isAccountModalOpen = false">Entendido</UiButton>
      </template>
    </UiDialog>

    <DocumentReviewDialog
      v-model="reviewOpen"
      :document="reviewDocument"
      :student-name="reviewStudentName"
      @reviewed="afterReview"
    />

    <UiDialog v-model="resetOpen" title="Restablecer contraseña">
      <UiAlert v-if="resetError" tone="danger" role="alert">{{ resetError }}</UiAlert>
      <p>
        La contraseña de {{ resetCandidate?.accountUsername }} será su matrícula
        {{ resetCandidate?.controlNumber }}. Las sesiones actuales se cerrarán.
      </p>
      <template #footer>
        <UiButton variant="secondary" size="sm" :disabled="resetting" @click="resetOpen = false">
          Cancelar
        </UiButton>
        <UiButton size="sm" :loading="resetting" :disabled="resetting" @click="confirmReset">
          Restablecer contraseña
        </UiButton>
      </template>
    </UiDialog>
  </UiBasePage>
</template>

<style scoped>
/* Estructura a pantalla completa sin scroll externo */
.fullscreen-page {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  overflow: hidden;
  padding: 12px 22px 10px !important;
}

.exec-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.85rem;
  margin-bottom: 0.65rem;
  flex-wrap: wrap;
  flex-shrink: 0;
}

.exec-header__left {
  flex: 1;
  min-width: 260px;
}

.exec-eyebrow {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  margin-bottom: 0.15rem;
  flex-wrap: wrap;
}

.exec-badge {
  font-size: 0.65rem;
  font-weight: 800;
  letter-spacing: 0.06em;
  padding: 0.14rem 0.5rem;
  border-radius: 5px;
  background: var(--color-accent-light);
  color: var(--color-accent);
  border: 1px solid color-mix(in srgb, var(--color-accent) 40%, transparent);
}

.exec-period {
  font-size: 0.74rem;
  font-weight: 500;
  color: var(--color-text-secondary);
}

.exec-period strong {
  color: var(--color-text-primary);
  font-weight: 700;
}

.title-row {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  flex-wrap: wrap;
}

.exec-title {
  font-size: 1.22rem;
  font-weight: 800;
  margin: 0;
  color: var(--color-text-primary);
  letter-spacing: -0.015em;
  line-height: 1.2;
}

.count-badge {
  display: inline-flex;
  align-items: center;
  padding: 0.15rem 0.55rem;
  border-radius: 6px;
  background: var(--color-info-badge);
  color: var(--color-info-text);
  border: 1px solid var(--color-info-border);
  font-size: 0.73rem;
  font-weight: 700;
}

.exec-subtitle {
  color: var(--color-text-secondary);
  margin: 0.12rem 0 0;
  font-size: 0.79rem;
  line-height: 1.3;
}

.actions-group {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  flex-wrap: wrap;
}

.action-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.38rem;
}

.compact-alert {
  margin-bottom: 0.5rem;
  padding: 0.55rem 0.85rem;
  flex-shrink: 0;
}

/* Tarjeta principal ajustada para pantalla completa */
.workspace-card {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  padding: 12px 16px !important;
  border: 1px solid var(--color-border);
  background: var(--color-surface-white);
}

/* Barra de filtros con superficie institucional contrastada */
.filters-bar {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-bottom: 0.6rem;
  padding: 0.55rem 0.75rem;
  border-radius: 8px;
  background: var(--surface-toolbar);
  border: 1px solid var(--color-border);
  flex-shrink: 0;
}

.roster-filters {
  display: grid;
  grid-template-columns: minmax(240px, 1.6fr) minmax(180px, 1fr) minmax(180px, 1.1fr);
  gap: 0.75rem;
  align-items: flex-end;
}

.search-box,
.select-box {
  display: flex;
  flex-direction: column;
  gap: 0.22rem;
  min-width: 0;
}

.filter-label {
  font-size: 0.73rem;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.input-wrapper,
.select-wrapper {
  position: relative;
  display: flex;
  align-items: center;
  width: 100%;
}

.search-icon {
  position: absolute;
  left: 0.75rem;
  color: var(--color-text-secondary);
  pointer-events: none;
  z-index: 2;
}

input.modern-input {
  width: 100%;
  min-height: 34px;
  height: 34px;
  border-radius: 7px;
  border: 1px solid var(--input-border) !important;
  background: var(--input-bg) !important;
  padding: 0 2rem 0 2.35rem !important;
  font-size: 0.81rem;
  color: var(--color-text-primary) !important;
  outline: none;
  box-sizing: border-box;
  transition: border-color 160ms ease, background-color 160ms ease, box-shadow 160ms ease;
}

input.modern-input::-webkit-search-cancel-button,
input.modern-input::-webkit-search-decoration {
  -webkit-appearance: none;
  appearance: none;
  display: none;
}

input.modern-input:hover,
select.modern-select:hover {
  border-color: var(--color-gray-400) !important;
}

input.modern-input:focus {
  border-color: var(--color-border-focus) !important;
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-border-focus) 22%, transparent);
}

select.modern-select {
  width: 100%;
  min-height: 34px;
  height: 34px;
  border-radius: 7px;
  border: 1px solid var(--input-border) !important;
  background: var(--input-bg) !important;
  padding: 0 2rem 0 0.75rem !important;
  font-size: 0.81rem;
  color: var(--color-text-primary) !important;
  outline: none;
  appearance: none;
  cursor: pointer;
  box-sizing: border-box;
  transition: border-color 160ms ease, background-color 160ms ease, box-shadow 160ms ease;
}

select.modern-select:focus {
  border-color: var(--color-border-focus) !important;
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-border-focus) 22%, transparent);
}

.select-chevron {
  position: absolute;
  right: 0.75rem;
  color: var(--color-text-secondary);
  pointer-events: none;
}

.autocomplete-container {
  position: relative;
  width: 100%;
}

.clear-search-btn {
  position: absolute;
  right: 0.55rem;
  background: transparent;
  border: none;
  color: var(--color-primary);
  cursor: pointer;
  font-size: 0.85rem;
  font-weight: 800;
  padding: 0.2rem 0.3rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: background-color 140ms ease, color 140ms ease, transform 140ms ease;
}

.clear-search-btn:hover {
  color: var(--color-primary-hover);
  background: var(--color-primary-subtle);
  transform: scale(1.08);
}

.suggestions-dropdown {
  position: absolute;
  top: calc(100% + 4px);
  left: 0;
  right: 0;
  background: var(--floating-bg);
  border: 1px solid var(--floating-border);
  border-radius: 8px;
  box-shadow: var(--floating-shadow);
  z-index: 999;
  max-height: 220px;
  overflow-y: auto;
  box-sizing: border-box;
}

.suggestion-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.45rem 0.75rem;
  cursor: pointer;
  border-bottom: 1px solid var(--color-border-light);
  transition: background-color 140ms ease;
}

.suggestion-item:last-child {
  border-bottom: none;
}

.suggestion-item:hover {
  background: var(--color-primary-subtle);
}

.suggestion-left {
  display: flex;
  flex-direction: column;
  gap: 0.08rem;
}

.suggestion-control {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-weight: 700;
  font-size: 0.78rem;
  color: var(--color-info-text);
}

.suggestion-name {
  font-size: 0.8rem;
  color: var(--color-text-primary);
}

.suggestion-right {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}

.suggestion-career {
  font-size: 0.7rem;
  font-weight: 700;
  padding: 0.1rem 0.4rem;
  background: var(--color-primary-subtle);
  border-radius: 4px;
  color: var(--color-info-text);
}

.suggestion-progress {
  font-size: 0.74rem;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.roster-filter-actions {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.75rem;
}

.extra-filters {
  flex: 1;
  margin: 0 !important;
  padding: 0 !important;
  border: 0 !important;
}

.extra-filters summary {
  cursor: pointer;
  min-height: 26px !important;
  padding: 2px 8px !important;
  display: inline-flex;
  align-items: center;
  color: var(--color-text-secondary);
  font-size: 0.75rem;
  font-weight: 700;
  border-radius: 6px;
  border: 1px solid transparent;
  transition: background-color 150ms ease, color 150ms ease, border-color 150ms ease;
}

.extra-filters summary:hover {
  background: var(--secondary-hover);
  color: var(--color-text-primary);
  border-color: var(--color-border-medium);
}

.extra-filters summary::before {
  content: '+';
  margin-right: 6px;
  color: var(--color-accent);
  font-weight: 800;
}

.extra-filters[open] summary::before {
  content: '−';
}

.extra-filter-fields {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 0.75rem;
  padding-top: 0.45rem;
}

/* Tabla compacta institucional que se ajusta a la vista completa */
.roster-table {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.compact-table {
  flex: 1;
  min-height: 0;
  margin: 0 -16px !important;
  border-top: 1px solid var(--color-border);
  border-radius: 0 !important;
}

.compact-table :deep(table) {
  width: 100%;
  table-layout: fixed;
  white-space: normal;
}

.compact-table :deep(th) {
  padding: 6px 12px;
  font-size: 0.68rem;
  font-weight: 700;
  letter-spacing: 0.05em;
  background: var(--surface-table-head);
  color: var(--color-text-secondary);
  border-bottom: 1px solid var(--color-border-medium);
  position: sticky;
  top: 0;
  z-index: 1;
}

.compact-table :deep(td) {
  padding: 5px 12px;
  font-size: 0.8rem;
  overflow-wrap: anywhere;
  vertical-align: middle;
  border-bottom: 1px solid var(--color-border-light);
}

.compact-table :deep(th:nth-child(1)) {
  width: 28%;
}
.compact-table :deep(th:nth-child(2)) {
  width: 14%;
}
.compact-table :deep(th:nth-child(3)) {
  width: 17%;
}
.compact-table :deep(th:nth-child(4)) {
  width: 28%;
}
.compact-table :deep(th:last-child) {
  width: 13%;
}

.compact-table :deep(tbody tr) {
  transition: background-color 150ms ease;
}

.compact-table :deep(tbody tr:hover) {
  background: var(--color-primary-subtle);
}

.roster-name {
  font-weight: 600;
  color: var(--color-text-primary);
  line-height: 1.2;
}

.roster-control {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-weight: 700;
  font-size: 0.79rem;
  font-variant-numeric: tabular-nums;
  color: var(--color-text-primary);
}

.roster-career {
  display: inline-flex;
  flex-direction: row;
  align-items: center;
  gap: 0.4rem;
  flex-wrap: wrap;
}

.roster-status {
  display: inline-flex;
  flex-direction: row;
  align-items: center;
  gap: 0.4rem;
  flex-wrap: wrap;
}

.career-pill {
  display: inline-block;
  padding: 0.1rem 0.45rem;
  border-radius: 5px;
  background: var(--color-primary-subtle);
  border: 1px solid var(--color-border-medium);
  font-size: 0.72rem;
  font-weight: 700;
  color: var(--color-text-primary);
}

.roster-career small,
.roster-status small {
  color: var(--color-text-secondary);
  font-size: 0.74rem;
}

.roster-status .roster-status-process {
  color: var(--color-text-primary);
  font-weight: 500;
}

.roster-status :deep(.badge) {
  max-width: 100%;
  white-space: normal;
  line-height: 1.35;
}

.roster-feedback {
  margin: 0;
  padding: 1rem;
  color: var(--color-text-secondary);
  font-size: 0.84rem;
}

.pagination-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  padding-top: 0.6rem;
  margin-top: auto;
  border-top: 1px solid var(--color-border);
  flex-wrap: wrap;
  flex-shrink: 0;
}

.pagination-info {
  font-size: 0.78rem;
  color: var(--color-text-secondary);
}

.pagination-controls {
  display: flex;
  gap: 0.4rem;
}

/* Modales e Información de Detalle */
.detail-status-header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px 12px;
}

.detail-status-secondary {
  font-size: 0.84rem;
  font-weight: 500;
  color: var(--color-text-secondary);
}

.student-detail {
  min-width: 0;
}

.student-detail-heading h2 {
  font-size: 1.25rem;
  margin: 0 0 0.25rem;
  overflow-wrap: anywhere;
  color: var(--color-text-primary);
}

.student-detail-heading p {
  margin: 0;
  color: var(--color-text-secondary);
}

.detail-section {
  padding-top: 1rem;
  margin-top: 1rem;
  border-top: 1px solid var(--color-border);
}

.detail-section h3 {
  font-size: 0.95rem;
  margin: 0 0 0.65rem;
  color: var(--color-text-primary);
}

.detail-fields {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.75rem 1.25rem;
  margin: 0.75rem 0;
}

.detail-fields > div {
  min-width: 0;
}

.detail-fields dt {
  font-size: 0.76rem;
  color: var(--color-text-secondary);
  margin-bottom: 0.15rem;
}

.detail-fields dd {
  margin: 0;
  font-size: 0.85rem;
  overflow-wrap: anywhere;
  white-space: pre-wrap;
  color: var(--color-text-primary);
}

.detail-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.45rem;
}

.modal-form-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 0.75rem 1rem;
}

.modal-input,
.modal-select {
  width: 100%;
  min-height: 36px;
  border-radius: 7px;
  border: 1px solid var(--input-border);
  background: var(--input-bg);
  padding: 0.4rem 0.75rem;
  font-size: 0.84rem;
  color: var(--color-text-primary);
  outline: none;
  box-sizing: border-box;
  transition: border-color 160ms ease, box-shadow 160ms ease;
}

.modal-input:focus,
.modal-select:focus {
  border-color: var(--color-border-focus);
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-border-focus) 22%, transparent);
}

.imss-box {
  margin-top: 1rem;
  padding: 0.85rem 1rem;
  border-radius: 8px;
  background: var(--color-bg-light);
  border: 1px solid var(--color-border);
}

.imss-box-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.imss-status-tag {
  font-size: 0.74rem;
  font-weight: 700;
  padding: 0.15rem 0.55rem;
  border-radius: 6px;
  background: var(--color-warning-badge);
  color: var(--color-warning-text);
  border: 1px solid var(--color-warning-border);
}

.imss-status-tag.valid {
  background: var(--color-success-badge);
  color: var(--color-success-text);
  border-color: var(--color-success-border);
}

.imss-box-desc {
  margin: 0.25rem 0 0.5rem;
  font-size: 0.8rem;
  color: var(--color-text-secondary);
}

.file-input {
  font-size: 0.82rem;
  color: var(--color-text-secondary);
}

.imss-actions-row {
  display: flex;
  gap: 0.5rem;
  margin-top: 0.65rem;
  flex-wrap: wrap;
}

.import-modal-content {
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
}

.import-intro-text {
  margin: 0;
  font-size: 0.84rem;
  color: var(--color-text-secondary);
  line-height: 1.4;
}

.import-summary-box {
  padding: 0.85rem;
  border-radius: 8px;
  background: var(--color-bg-light);
  border: 1px solid var(--color-border);
  font-size: 0.84rem;
  color: var(--color-text-primary);
}

.import-summary-box ul {
  margin: 0.4rem 0;
  padding-left: 1.2rem;
}

.import-errors-list {
  margin-top: 0.5rem;
  font-size: 0.78rem;
  color: var(--color-danger-text);
}

.import-modal-footer {
  display: flex;
  justify-content: space-between;
  width: 100%;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.footer-actions {
  display: flex;
  gap: 0.5rem;
}

.account-modal-body {
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
}

.credential-item {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.cred-label {
  font-size: 0.78rem;
  color: var(--color-text-secondary);
}

.cred-value {
  font-size: 1rem;
  color: var(--color-text-primary);
}

.cred-pass {
  font-size: 1.05rem;
  font-weight: 700;
  color: var(--color-info-text);
  background: var(--color-info-badge);
  border: 1px solid var(--color-info-border);
  padding: 0.22rem 0.55rem;
  border-radius: 6px;
  display: inline-block;
  width: fit-content;
}

@media (max-width: 900px) {
  .fullscreen-page {
    height: auto !important;
    overflow: visible !important;
    padding: 4px 0 12px !important;
    min-width: 0;
    max-width: 100%;
  }

  .workspace-card {
    min-width: 0;
    max-width: 100%;
    overflow: hidden;
    padding: 10px 12px !important;
  }

  .roster-filters,
  .extra-filter-fields {
    grid-template-columns: minmax(0, 1fr);
  }

  .compact-table {
    margin: 0 -12px !important;
    max-width: calc(100% + 24px);
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
  }

  .compact-table :deep(table) {
    table-layout: auto;
    min-width: 680px;
  }
}

@media (max-width: 640px) {
  .exec-header {
    flex-direction: column;
    align-items: stretch;
    gap: 0.6rem;
  }

  .exec-header__left {
    min-width: 0;
  }

  .exec-title {
    font-size: 1.05rem;
  }

  .actions-group {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    width: 100%;
    gap: 0.4rem;
  }

  .actions-group :deep(.btn) {
    width: 100%;
    min-height: 38px;
    white-space: normal;
    text-align: center;
    padding: 4px 8px;
    font-size: 0.75rem;
  }

  input.modern-input,
  select.modern-select,
  .modal-input,
  .modal-select {
    min-height: 40px;
    height: 40px;
    font-size: 16px;
  }

  .roster-filter-actions {
    flex-direction: column;
    align-items: stretch;
    gap: 0.5rem;
  }

  .pagination-bar {
    flex-direction: column;
    align-items: stretch;
    gap: 0.5rem;
  }

  .pagination-info {
    text-align: center;
  }

  .pagination-controls {
    width: 100%;
  }

  .pagination-controls :deep(.btn) {
    flex: 1;
  }

  .modal-form-grid,
  .detail-fields {
    grid-template-columns: 1fr;
  }

  .detail-actions :deep(.btn) {
    flex: 1 1 auto;
  }

  .imss-box-header {
    flex-direction: column;
    align-items: flex-start;
  }

  .file-input {
    max-width: 100%;
  }

  .import-modal-footer {
    flex-direction: column;
    align-items: stretch;
  }

  .footer-actions {
    width: 100%;
  }

  .footer-actions :deep(.btn) {
    flex: 1;
  }
}
</style>
