import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { pathToFileURL } from 'node:url'

// Synthetic UI responses; authorization, persistence and extraction use the isolated HTTP suite.
const module = process.env.PLAYWRIGHT_MODULE || 'playwright'
const { chromium } = await import(path.isAbsolute(module) ? pathToFileURL(module).href : module)
const base = process.env.UI_TEST_URL || 'https://127.0.0.1:5900'
const output = path.resolve('tmp/pdf-qa/accounts-imss')
fs.mkdirSync(output, { recursive: true })
const pdf = fs.readFileSync('backend/Resources/Documents/templates/pp-evaluation.pdf')
const student = { id: 11, userId: 12, controlNumber: 'I23050348', firstName: 'Alumno', firstSurname: 'Prueba', secondSurname: null,
  fullName: 'Alumno Prueba', careerId: 1, careerCode: 'INF', careerName: 'Ingeniería Informática', currentSemester: 7, completedCredits: 156,
  curriculumProgressPercentage: 60, shift: 'morning', isEnrolled: true, hasUserAccount: true, accountStatus: 'active',
  accountUsername: 'alumno@monclova.tecnm.mx', institutionalEmail: 'alumno@monclova.tecnm.mx', personalEmail: '', phoneNumber: '',
  hasValidImss: false, hasImssFile: true, imssStatus: 'pending', imssDocumentId: 999, imssDocumentDate: null }
const documents = [999,1000].map((id, i) => ({ id, studentId: 11+i, studentControlNumber: i ? 'TEST002' : student.controlNumber,
  studentFullName: i ? 'Segunda Alumna' : student.fullName, careerCode: 'INF', careerName: student.careerName,
  tramiteType: 'general', tramiteId: null, documentCode: 'IMSS', originalFileName: 'imss.pdf', fileMimeType: 'application/pdf',
  fileSizeBytes: pdf.length, status: 'pending', documentDate: i ? '2026-10-01' : null, createdAt: '2026-11-07T12:00:00Z' }))
let role = 'department_head', failQueue = false, failReview = false, noDocument = false, uploaded = false
const reviews = [], queries = [], resets = []
let suggestionRequests = 0
let openings = 0
const browser = await chromium.launch({ channel: 'msedge', headless: true })
try {
  const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1440, height: 1000 } })
  await context.addCookies([{ name: 'session_exp', value: String(Math.floor(Date.now()/1000)+3600), url: base, secure: true }])
  const page = await context.newPage(), errors = []
  page.on('pageerror', error => errors.push(error.message))
  await page.addInitScript(() => {
    window.revokedPdfUrls = []
    const revoke = URL.revokeObjectURL.bind(URL)
    URL.revokeObjectURL = url => { window.revokedPdfUrls.push(url); revoke(url) }
  })
  await page.route('**/api/**', async route => {
    const request = route.request(), url = new URL(request.url()), endpoint = url.pathname
    if (!endpoint.startsWith('/api/')) return route.continue()
    let body = []
    if (endpoint === '/api/auth/me') body = { id: role === 'student' ? 12 : 5, username: role === 'student' ? student.accountUsername : 'jefa.vinculacion', name: 'Prueba', roles: [role] }
    else if (endpoint === '/api/documents/review-queue') {
      queries.push(Object.fromEntries(url.searchParams))
      if (failQueue) return route.fulfill({ status: 500, json: { detail: 'Error simulado' } })
      const search = url.searchParams.get('search') || ''
      const items = documents.filter(d => `${d.studentFullName} ${d.studentControlNumber}`.includes(search))
      body = { items, totalItems: items.length, totalPages: 1 }
    } else if (endpoint.endsWith('/imss-date-suggestion')) {
      suggestionRequests++
      body = endpoint.includes('/999/') ? { status: 'not_found', suggestedDate: null, pageNumber: null }
        : { status: 'found', suggestedDate: '2026-11-03', pageNumber: 1 }
    } else if (endpoint.endsWith('/review')) {
      if (failReview) return route.fulfill({ status: 400, json: { detail: 'La fecha requiere corrección.', code: 'validation.failed', errors: { documentDate: ['La fecha no puede ser futura.'] } } })
      const payload = request.postDataJSON(), id = Number(endpoint.split('/')[3])
      reviews.push({ id, ...payload }); Object.assign(documents.find(d => d.id === id), payload)
      body = documents.find(d => d.id === id)
    } else if (endpoint.endsWith('/file') || (endpoint.endsWith('/imss') && request.method() === 'GET')) return route.fulfill({ contentType: 'application/pdf', body: pdf })
    else if (endpoint.endsWith('/account/reset-password')) { resets.push(endpoint); return route.fulfill({ status: 204 }) }
    else if (endpoint === '/api/students') body = { items: [student], totalItems: 1, page: 1, pageSize: 20, totalPages: 1 }
    else if (endpoint.endsWith('/documents')) body = noDocument ? [] : [documents[0]]
    else if (endpoint === '/api/students/me/imss' && request.method() === 'PUT') { uploaded = true; body = student }
    else if (endpoint === '/api/students/me/eligibility') body = { socialService: { eligible: false, reasons: [] }, internship: { eligible: false, reasons: [] } }
    else if (endpoint === '/api/students/11' || endpoint === '/api/students/me') body = student
    else if (endpoint === '/api/social-services/me' || endpoint === '/api/internships/me') body = []
    else if ((endpoint === '/api/social-services' || endpoint === '/api/internships') && request.method() === 'POST') { openings++; body = {} }
    else if (endpoint.endsWith('/options')) body = [{ id: 1, label: 'Opción de prueba' }]
    await route.fulfill({ contentType: 'application/json', body: JSON.stringify(body) })
  })
  await page.goto(base + '/expedients/review')
  const pre = page.getByRole('region', { name: 'Cola de Documentos', exact: true })
  await pre.getByRole('button', { name: 'Dictaminar', exact: true }).first().waitFor()
  await pre.getByRole('button', { name: 'Dictaminar', exact: true }).first().click()
  let dialog = page.getByRole('dialog', { name: /Revisar IMSS/ })
  await dialog.locator('iframe').waitFor()
  const blob = await dialog.locator('iframe').getAttribute('src')
  await dialog.getByRole('button', { name: 'Aprobar documento' }).click()
  await dialog.getByText('Confirma que el PDF corresponde al alumno y muestra estado VIGENTE.', { exact: true }).waitFor()
  await dialog.getByRole('button', { name: 'Rechazar documento' }).click()
  await dialog.getByText('Indica el motivo de rechazo.', { exact: true }).waitFor()
  await page.keyboard.press('Escape')
  await dialog.waitFor({ state: 'hidden' })
  assert.ok(await page.evaluate(url => window.revokedPdfUrls.includes(url), blob))
  await pre.getByRole('button', { name: 'Dictaminar', exact: true }).nth(1).click()
  assert.equal(await dialog.locator('input[type=date]').count(), 0)
  await dialog.getByRole('checkbox').check()
  failReview = true
  await dialog.getByRole('button', { name: 'Aprobar documento' }).click()
  await dialog.getByText(/La fecha requiere corrección/).waitFor()
  assert.equal(reviews.length, 0)
  failReview = false
  await page.screenshot({ path: path.join(output, 'desktop-manual-date.png') })
  await dialog.getByRole('button', { name: 'Aprobar documento' }).click()
  await dialog.waitFor({ state: 'hidden' })
  assert.deepEqual(reviews[0], { id: 1000, status: 'approved' })
  documents[1].documentDate = '2025-11-03'
  await page.getByRole('button', { name: 'Actualizar Cola', exact: true }).click()
  await pre.getByRole('button', { name: 'Dictaminar', exact: true }).nth(1).click()
  await dialog.getByText('Dictamen: Aprobado', { exact: false }).waitFor()
  assert.equal(await dialog.getByRole('button', { name: 'Aprobar documento' }).count(), 0)
  await page.keyboard.press('Escape')
  await page.goto(base + '/students')
  await page.getByRole('button', { name: 'Ver detalle de ' + student.fullName, exact: true }).click()
  await page.getByRole('button', { name: 'Restablecer contraseña', exact: true }).click()
  dialog = page.getByRole('dialog', { name: 'Restablecer contraseña', exact: true })
  await dialog.getByRole('button', { name: 'Restablecer contraseña', exact: true }).click()
  await dialog.waitFor({ state: 'hidden' })
  assert.deepEqual(resets, ['/api/students/11/account/reset-password'])
  noDocument = true; student.hasImssFile = false; student.imssDocumentId = null; student.imssStatus = 'missing'
  await page.getByRole('button', { name: 'Ver detalle de ' + student.fullName, exact: true }).click()
  dialog = page.getByRole('dialog', { name: 'Detalle del alumno' })
  await dialog.getByText('Sin documento', { exact: true }).waitFor()
  assert.ok(await dialog.getByRole('button', { name: 'Ver dictamen IMSS', exact: true }).isDisabled())
  await page.keyboard.press('Escape')

  role = 'social_service_officer'
  await page.goto(base + '/expedients/review')
  const row = page.locator('tbody tr').filter({ hasText: student.controlNumber })
  await row.getByRole('button', { name: 'Dictaminar', exact: true }).click()
  dialog = page.getByRole('dialog', { name: /Revisar IMSS/ })
  await dialog.locator('iframe').waitFor()
  assert.equal(await dialog.locator('input[type=date]').count(), 0)
  await page.setViewportSize({ width: 390, height: 844 })
  await dialog.getByLabel('Motivo de rechazo', { exact: false }).fill('PDF ilegible; cargar constancia completa.')
  await page.screenshot({ path: path.join(output, 'mobile-rejection.png') })
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1))
  await dialog.getByRole('button', { name: 'Rechazar documento' }).click()
  await dialog.waitFor({ state: 'hidden' })
  assert.deepEqual(reviews[1], { id: 999, status: 'rejected', rejectionReason: 'PDF ilegible; cargar constancia completa.' })
  await page.getByLabel('Buscar documento en cola de revisión').fill('Sin coincidencias')
  await row.waitFor({ state: 'hidden' })
  assert.equal(queries.at(-1).search, 'Sin coincidencias')
  failQueue = true
  await page.getByRole('button', { name: 'Actualizar Cola', exact: true }).click()
  await page.getByText('No fue posible cargar la bandeja.', { exact: false }).waitFor()
  assert.equal(await page.locator('tbody tr').count(), 0)
  failQueue = false
  role = 'student'
  await page.goto(base + '/students/me')
  assert.ok(await page.getByLabel('Correo Institucional (@monclova.tecnm.mx)').getAttribute('readonly') !== null)
  await page.locator('input[type=file]').setInputFiles({ name: 'imss.pdf', mimeType: 'application/pdf', buffer: pdf })
  await page.getByRole('button', { name: 'Subir Constancia IMSS (PDF)' }).click()
  await page.getByText('Constancia IMSS cargada.', { exact: false }).waitFor()
  assert.ok(uploaded, 'Date must be optional on upload')
  for (const route of ['/social-service/my', '/internships/my']) {
    await page.goto(base + route)
    await page.getByRole('button', { name: 'Actualizar estado IMSS', exact: true }).waitFor()
    const opening = page.getByRole('button', { name: route.startsWith('/social-service') ? 'Enviar Solicitud a Vinculación y Generar Anexo XVIII' : 'Solicitar Carta de Presentación', exact: true })
    assert.ok(await opening.isDisabled(), 'Pending IMSS must disable opening')
    await page.locator('input[type=file]').first().setInputFiles({ name: 'imss.pdf', mimeType: 'application/pdf', buffer: pdf })
    await page.getByRole('button', { name: 'Subir constancia para revisión', exact: true }).click()
    await page.getByText('Constancia cargada. Pendiente de revisión;', { exact: false }).waitFor()
    assert.ok(await opening.isDisabled())
  }
  assert.equal(openings, 0, 'Uploading an IMSS must not open a process')
  assert.equal(suggestionRequests, 0)
  assert.deepEqual(errors, [])
  console.log('Shared IMSS review, dates, errors, reset, desktop/mobile and optional-date upload checks passed.')
} finally { await browser.close() }
