import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { pathToFileURL } from 'node:url'
const { chromium } = await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE).href)
const browser = await chromium.launch({ channel: 'msedge', headless: true })
const base = process.env.UI_TEST_URL || 'https://127.0.0.1:5900'
const out = 'tmp/pdf-qa/field-validation'
fs.mkdirSync(out, { recursive: true })
const pdf = fs.readFileSync('backend/Resources/Documents/templates/ss-report.pdf')
const profile = JSON.parse(fs.readFileSync('tmp/pdf-qa/ui-fixtures/profile.json'))
const detail = JSON.parse(fs.readFileSync('tmp/pdf-qa/ui-fixtures/social-service.json'))
const pp = JSON.parse(fs.readFileSync('tmp/pdf-qa/ui-fixtures/internship.json'))
let role = 'department_head', scenario = '', saves = 0
const exceptions = []
const problem = errors => ({ detail: 'Uno o más campos presentan errores de validación.', code: 'validation.failed', errors })
try {
 const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1440, height: 1000 } })
 await context.addCookies([{ name: 'session_exp', value: String(Math.floor(Date.now()/1000)+3600), url: base, secure: true }])
 const page = await context.newPage()
 page.on('pageerror', error => exceptions.push(error.message))
 await page.route('**/api/**', async route => {
  const request = route.request(), u = new URL(request.url()), endpoint = u.pathname
  if (!endpoint.startsWith('/api/')) return route.continue()
  let data = []
  if (endpoint === '/api/auth/me') data = { id: 1, username: 'qa', name: 'Prueba', roles: [role] }
  else if (endpoint === '/api/students' && request.method() === 'POST') {
   saves++
   if (scenario === 'network') return route.abort('failed')
   const status = scenario === 'duplicate' ? 409 : scenario === 'credits' ? 422 : 400
   const errors = scenario === 'duplicate' ? { controlNumber: ['La matrícula ya existe.'] } : scenario === 'credits' ? { completedCredits: ['No puedes superar 220 créditos.'] } : { Curp: ['CURP incorrecta del servidor'], FirstName: ['Nombre rechazado del servidor'], unknownField: ['Dato no editable: contacta a administración.'] }
   return route.fulfill({ status, json: problem(errors) })
  } else if (endpoint === '/api/students') data = { items: [], totalItems: 0, totalPages: 0 }
  else if (endpoint === '/api/students/me') data = { ...profile, hasValidImss: true, imssStatus: 'approved' }
  else if (endpoint === '/api/students/me/eligibility') data = { socialService: { eligible: true, reasons: [] }, internship: { eligible: true, reasons: [] } }
  else if (endpoint === '/api/students/me/contact' && request.method() === 'PUT') { saves++; return route.fulfill({ status: 400, json: problem({ phoneNumber: ['Teléfono rechazado por el servidor'] }) }) }
  else if (endpoint === '/api/social-services/me') data = scenario === 'report' ? [{ id: detail.id }] : []
  else if (endpoint === `/api/social-services/${detail.id}`) data = { ...detail, status: 'approved', documentRequirements: detail.documentRequirements.map(d => ({ ...d, canUpload: false })) }
  else if (endpoint.startsWith(`/api/social-services/${detail.id}/reports/`)) { saves++; return route.fulfill({ status: 422, json: problem({ activitiesSummary: ['Actividades rechazadas por el servidor'] }) }) }
  else if (endpoint === '/api/social-services/submissions') { saves++; return route.fulfill({ status: 400, json: problem({ '$.Application.StudentAddress': ['Domicilio rechazado por el servidor'], '$.Application.StudentPhone': ['Teléfono rechazado por el servidor'] }) }) }
  else if (endpoint === '/api/internships/me') data = scenario === 'evaluation' ? [{ id: pp.id }] : []
  else if (endpoint === `/api/internships/${pp.id}`) data = { ...pp, status: 'approved' }
  else if (endpoint.endsWith('/evaluation-format') && request.method() === 'PUT') { saves++; return route.fulfill({ status: 400, json: problem({ evaluationEndDate: ['Periodo rechazado por el servidor'] }) }) }
  else if (endpoint === '/api/internships' && request.method() === 'POST') { saves++; return route.fulfill({ status: 400, json: problem({ institutionalEmail: ['Correo rechazado por el servidor'] }) }) }
  else if (endpoint === '/api/companies' && request.method() === 'POST') { saves++; return route.fulfill({ status: 400, json: problem({ rfc: ['RFC rechazado por el servidor'] }) }) }
  else if (endpoint === '/api/companies') data = { items: [{ id: 1, name: 'Empresa QA', companyType: 'public', sector: 'public', companySize: 'not_applicable', city: 'Monclova', state: 'Coahuila', isActive: true }], totalItems: 1, totalPages: 1 }
  else if (endpoint.endsWith('/supervisors') && request.method() === 'POST') { saves++; return route.fulfill({ status: 400, json: problem({ fullName: ['Supervisor rechazado por el servidor'] }) }) }
  else if (endpoint === '/api/agreements' && request.method() === 'POST') { saves++; return route.fulfill({ status: 400, json: problem({ agreementNumber: ['Convenio rechazado por el servidor'] }) }) }
  else if (endpoint === '/api/agreements') data = { items: [], totalPages: 0 }
  else if (endpoint === '/api/careers' && request.method() === 'POST') { saves++; return route.fulfill({ status: 409, json: problem({ code: ['Clave de carrera ya registrada'] }) }) }
  else if (endpoint === '/api/careers') data = { items: [], totalItems: 0, totalPages: 0, page: 1 }
  else if (endpoint.endsWith('/options')) data = [{ id: 1, label: 'Informática', meta: { isActive: true, totalCredits: 220 } }]
  else if (endpoint.endsWith('/file') || endpoint.includes('/pdf/')) return route.fulfill({ contentType: 'application/pdf', body: pdf })
  await route.fulfill({ json: data })
 })
 function field(root, label) { return root.getByLabel(label, { exact: true }) }
 async function invalid(root, label) {
  const control = field(root, label)
  await control.waitFor()
  assert.equal(await control.getAttribute('aria-invalid'), 'true', label)
  const ids = (await control.getAttribute('aria-describedby') || '').split(' ')
  assert.ok(ids.some(id => id.endsWith('-error')), `${label}: missing description`)
 }
 // Manual registration: all local errors, live correction, optional CURP and server details.
 await page.goto(base + '/students')
 await page.getByRole('button', { name: 'Agregar alumno manualmente' }).click()
 let dialog = page.getByRole('dialog', { name: 'Agregar Alumno (Caso Específico)' })
 await dialog.getByRole('button', { name: 'Registrar Alumno', exact: true }).click()
 assert.equal(saves, 0)
 for (const label of ['No. de Control *', 'Nombre(s) *', 'Primer Apellido *']) await invalid(dialog, label)
 assert.equal(await field(dialog, 'No. de Control *').evaluate(el => el === document.activeElement), true)
 await field(dialog, 'No. de Control *').fill('I23050001')
 await field(dialog, 'Nombre(s) *').fill('María José')
 await field(dialog, 'Primer Apellido *').fill('Muñoz')
 await field(dialog, 'CURP').fill('MALA')
 await field(dialog, 'CURP').blur()
 await invalid(dialog, 'CURP')
 await field(dialog, 'CURP').fill('')
 await field(dialog, 'Semestre Actual (1-15) *').fill('16')
 await field(dialog, 'Créditos Aprobados *').fill('221')
 await dialog.getByRole('button', { name: 'Registrar Alumno', exact: true }).click()
 await invalid(dialog, 'Semestre Actual (1-15) *'); await invalid(dialog, 'Créditos Aprobados *')
 await field(dialog, 'Semestre Actual (1-15) *').fill('7')
 await field(dialog, 'Créditos Aprobados *').fill('190')
 await dialog.getByRole('button', { name: 'Registrar Alumno', exact: true }).click()
 await dialog.getByText('CURP incorrecta del servidor', { exact: true }).last().waitFor()
 await invalid(dialog, 'CURP'); await invalid(dialog, 'Nombre(s) *')
 await dialog.getByText('Dato no editable: contacta a administración.', { exact: false }).last().waitFor()
 assert.equal(await field(dialog, 'Nombre(s) *').inputValue(), 'María José')
 for (const width of [1440, 390]) {
  await page.setViewportSize({ width, height: 950 })
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1))
  await page.screenshot({ path: `${out}/manual-${width}.png` })
 }
 await page.evaluate(() => document.documentElement.dataset.theme = 'dark')
 await page.screenshot({ path: `${out}/manual-dark.png` })
 await page.evaluate(() => document.documentElement.dataset.theme = 'light')
 await dialog.getByRole('button', { name: 'Cancelar', exact: true }).click()
 await page.getByRole('button', { name: 'Agregar alumno manualmente' }).click()
 assert.equal(await dialog.locator('[aria-invalid=true]').count(), 0)
 await field(dialog, 'No. de Control *').fill('I23050002'); await field(dialog, 'Nombre(s) *').fill('Ana'); await field(dialog, 'Primer Apellido *').fill('Peña')
 scenario = 'duplicate'
 await dialog.getByRole('button', { name: 'Registrar Alumno', exact: true }).click()
 await dialog.getByText('La matrícula ya existe.', { exact: true }).last().waitFor()
 await invalid(dialog, 'No. de Control *')
 scenario = 'credits'
 await dialog.getByRole('button', { name: 'Registrar Alumno', exact: true }).click()
 await dialog.getByText('No puedes superar 220 créditos.', { exact: true }).last().waitFor()
 await invalid(dialog, 'Créditos Aprobados *')
 scenario = 'network'
 await dialog.getByRole('button', { name: 'Registrar Alumno', exact: true }).click()
 await dialog.getByText('No se pudo guardar el formulario.', { exact: true }).waitFor()
 await dialog.getByRole('button', { name: 'Cancelar', exact: true }).click()
 // Companies, supervisor and agreement forms share the same component and isolated state.
 scenario = ''
 await page.setViewportSize({ width: 1440, height: 1000 })
 await page.goto(base + '/companies')
 await page.getByRole('button', { name: 'Registrar empresa', exact: true }).click()
 dialog = page.getByRole('dialog', { name: 'Registrar empresa', exact: true })
 await dialog.getByRole('button', { name: 'Guardar empresa' }).click()
 await invalid(dialog, 'Nombre o razón social')
 await field(dialog, 'Nombre o razón social').fill('Empresa nueva')
 await field(dialog, 'Tipo de empresa').selectOption('public')
 await dialog.getByRole('button', { name: 'Guardar empresa' }).click()
 await dialog.getByText('RFC rechazado por el servidor', { exact: true }).last().waitFor()
 await invalid(dialog, 'RFC')
 await dialog.getByRole('button', { name: 'Cerrar modal' }).click()
 await page.getByRole('button', { name: 'Supervisores y convenios' }).click()
 await page.getByRole('button', { name: 'Registrar supervisor' }).click()
 dialog = page.getByRole('dialog', { name: 'Datos del supervisor' })
 await dialog.getByRole('button', { name: 'Guardar', exact: true }).click()
 await invalid(dialog, 'Nombre completo'); await invalid(dialog, 'Puesto')
 await field(dialog, 'Nombre completo').fill('Supervisor QA'); await field(dialog, 'Puesto').fill('Coordinador')
 await dialog.getByRole('button', { name: 'Guardar', exact: true }).click()
 await dialog.getByText('Supervisor rechazado por el servidor', { exact: true }).last().waitFor()
 await dialog.getByRole('button', { name: 'Cerrar modal' }).click()
 await page.getByRole('button', { name: 'Registrar convenio' }).click()
 dialog = page.getByRole('dialog', { name: 'Datos del convenio' })
 assert.equal(await dialog.locator('[aria-invalid=true]').count(), 0)
 await dialog.getByRole('button', { name: 'Guardar', exact: true }).click()
 await invalid(dialog, 'Número de convenio')
 await field(dialog, 'Número de convenio').fill('CONV-1')
 await field(dialog, 'Fecha de firma').fill('2026-11-01'); await field(dialog, 'Vencimiento').fill('2026-10-01')
 await dialog.getByRole('button', { name: 'Guardar', exact: true }).click()
 await invalid(dialog, 'Vencimiento')
 await field(dialog, 'Vencimiento').fill('2027-11-01')
 await dialog.getByRole('button', { name: 'Guardar', exact: true }).click()
 await dialog.getByText('Convenio rechazado por el servidor', { exact: true }).last().waitFor()
 // Career capture uses the same inline errors and summary links.
 await page.goto(base + '/catalogs/careers')
 await page.getByRole('button', { name: 'Nueva Carrera', exact: true }).click()
 dialog = page.getByRole('dialog', { name: 'Registrar Nueva Carrera' })
 await dialog.getByRole('button', { name: 'Registrar Carrera', exact: true }).click()
 await invalid(dialog, 'Clave Oficial (Siglas)'); await invalid(dialog, 'Nombre Oficial de la Carrera')
 await field(dialog, 'Clave Oficial (Siglas)').fill('QA')
 await field(dialog, 'Nombre Oficial de la Carrera').fill('Ingeniería de Prueba')
 await dialog.getByRole('button', { name: 'Registrar Carrera', exact: true }).click()
 await dialog.getByText('Clave de carrera ya registrada', { exact: true }).last().waitFor()
 await invalid(dialog, 'Clave Oficial (Siglas)')
 // Student SS submission: nested errors remain visible after the upload confirmation dialog closes.
 role = 'student'; scenario = ''
 await page.goto(base + '/social-service/my'); await page.reload()
 const send = page.getByRole('button', { name: 'Enviar Solicitud e IMSS a revisión' })
 await send.waitFor()
 await send.click()
 await invalid(page, 'Domicilio Completo del Estudiante *')
 await field(page, 'Domicilio Completo del Estudiante *').fill('Calle Hidalgo 120')
 await field(page, 'Actividades a Desarrollar en el Programa (Anexo XVIII) *').fill('Apoyo a la comunidad')
 await page.locator('input[type=file]').setInputFiles({ name: 'imss.pdf', mimeType: 'application/pdf', buffer: pdf })
 await send.click()
 const preview = page.getByRole('dialog', { name: 'Confirma tu Solicitud y constancia IMSS' })
 await preview.getByRole('button', { name: 'Confirmar y enviar a revisión' }).click()
 await preview.waitFor({ state: 'hidden' })
 await invalid(page, 'Domicilio Completo del Estudiante *')
 await page.getByText('Domicilio rechazado por el servidor', { exact: true }).last().waitFor()
 await page.screenshot({ path: `${out}/ss-nested-errors.png` })
 // PP request and profile: details are shown near their controls too.
 await page.goto(base + '/internships/my')
 await page.getByRole('button', { name: /Enviar.*Solicitud|Solicitar.*Carta/ }).first().waitFor()
 const ppSend = page.getByRole('button', { name: /Enviar.*Solicitud|Solicitar.*Carta/ }).first()

 await ppSend.click()
 await page.getByText('Correo rechazado por el servidor', { exact: true }).last().waitFor()
 await invalid(page, 'Correo Electrónico INSTITUCIONAL *')
 await page.goto(base + '/students/me')
 await page.getByRole('button', { name: /Guardar.*[Cc]ontacto|Guardar.*[Cc]ambios/ }).first().click()
 await page.getByText('Teléfono rechazado por el servidor', { exact: true }).last().waitFor()
 await invalid(page, 'Teléfono de Contacto')
 // Report and evaluation dialogs retain server errors within their modal.
 scenario = 'report'
 await page.goto(base + '/social-service/my')
 const reportRow = page.locator('article').filter({ hasText: '4.2 Reporte Bimestral 2' })
 await reportRow.getByRole('button', { name: 'Editar datos del formato' }).click()
 dialog = page.getByRole('dialog', { name: '4.2 Reporte Bimestral 2' })
 await field(dialog, 'Resumen de Actividades Realizadas *').fill('Apoyo comunitario')
 await dialog.getByRole('button', { name: 'Guardar e Imprimir Formato Lleno' }).click()
 await dialog.getByText('Actividades rechazadas por el servidor', { exact: true }).last().waitFor()
 await invalid(dialog, 'Resumen de Actividades Realizadas *')
 scenario = 'evaluation'
 await page.goto(base + '/internships/my')
 await page.getByRole('button', { name: 'Editar datos del formato' }).click()
 dialog = page.getByRole('dialog', { name: 'Prellenar e Imprimir Anexo XVII — Evaluación de Productividad Laboral' })
 await field(dialog, 'Fecha Inicio Evaluación *').fill('2026-08-01')
 await field(dialog, 'Fecha Fin Evaluación *').fill('2026-10-01')
 await dialog.getByRole('button', { name: 'Guardar e Imprimir Anexo XVII' }).click()
 await dialog.getByText('Periodo rechazado por el servidor', { exact: true }).last().waitFor()
 await invalid(dialog, 'Fecha Fin Evaluación *')
 assert.deepEqual(exceptions, [])
 console.log('Field-level validation: manual creation, multiple errors, live correction, 400/409/422/network, reset, nested SS, PP, profile, companies, supervisors, agreements, accessibility and responsive layout passed.')
} finally { await browser.close() }
