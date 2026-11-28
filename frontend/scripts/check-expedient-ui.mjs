import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { pathToFileURL } from 'node:url'

// Uses API-produced demo fixtures from the isolated HTTP suite; never mocks production traffic.
const playwrightModule = process.env.PLAYWRIGHT_MODULE || 'playwright'
const { chromium } = await import(path.isAbsolute(playwrightModule) ? pathToFileURL(playwrightModule).href : playwrightModule)
const root = path.resolve('tmp/pdf-qa')
const detail = JSON.parse(fs.readFileSync(path.join(root, 'ui-fixtures/social-service.json')))
const profile = JSON.parse(fs.readFileSync(path.join(root, 'ui-fixtures/profile.json')))
const documents = JSON.parse(fs.readFileSync(path.join(root, 'ui-fixtures/documents.json')))
const pp = JSON.parse(fs.readFileSync(path.join(root, 'ui-fixtures/internship.json')))
const ppAdmin = JSON.parse(fs.readFileSync(path.join(root, 'ui-fixtures/internship-admin.json')))
const pdf = fs.readFileSync('backend/Resources/Documents/templates/ss-supervisor-evaluation.pdf')
const base = process.env.UI_TEST_URL || 'https://127.0.0.1:5900'
let jointOpening = false, jointSent = false
let closed = false
let presentationMissing = false
let admin = false
let uploadedAcceptance = false
let uploadedImss = false
const browser = await chromium.launch({ channel: 'msedge', headless: true })
try {
  const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1440, height: 1000 } })
  await context.addCookies([{ name: 'session_exp', value: String(Math.floor(Date.now()/1000)+3600), url: base, secure: true }])
  const page = await context.newPage()
  const errors = []
  const pdfRequests = []
  page.on('pageerror', error => errors.push(error.message))
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url()), endpoint = url.pathname
    if (endpoint.includes('/pdf/')) pdfRequests.push(endpoint)
    if (!endpoint.startsWith('/api/')) return route.continue()
    let body = []
    if (endpoint === '/api/auth/me') body = { id: admin ? 5 : 2, username: admin ? 'jefa.vinculacion' : 'usuario', name: 'Usuario de prueba', email: 'test@monclova.tecnm.mx', roles: [admin ? 'department_head' : 'student'] }
    else if (endpoint === '/api/students/me') body = jointOpening ? { ...profile, hasValidImss: false, imssStatus: null } : profile
    else if (endpoint === '/api/students/me/eligibility') body = { socialService: { eligible: false }, internship: { eligible: false } }
    else if (endpoint === '/api/students/me/imss' && route.request().method() === 'PUT') {
      uploadedImss = true
      body = { ...profile, hasValidImss: false, imssDocumentDate: '2026-11-03' }
    }
    else if (endpoint === '/api/social-services/me') body = jointOpening && !jointSent ? [] : [{ id: detail.id }]
    else if (endpoint.endsWith('/options')) body = [{ id: 1, label: 'Programa de prueba', metadata: { isActive: true } }]
    else if (endpoint === '/api/social-services/submissions') {
      assert.ok(route.request().postDataBuffer().includes(Buffer.from('name="application"')))
      assert.ok(route.request().postDataBuffer().includes(Buffer.from('name="imssFile"')))
      jointSent = true; body = detail
    }
    else if (endpoint === `/api/social-services/${detail.id}`) body = { ...detail, status: closed ? 'released' : 'approved', documentRequirements: detail.documentRequirements.map(d => ({ ...d, canUpload: d.canUpload && !closed })) }
    else if (endpoint === '/api/internships/me') body = [{ id: pp.id }]
    else if (endpoint === `/api/internships/${pp.id}`) body = admin ? ppAdmin : pp
    else if (endpoint === '/api/internships') body = { items: [{ id: pp.id, studentId: pp.student.id, studentControlNumber: pp.student.controlNumber, studentFullName: pp.student.fullName, careerCode: 'INF', periodCode: pp.period.code, status: pp.status, companyName: null }], totalItems: 1, totalPages: 1 }
    else if (endpoint === `/api/students/${pp.student.id}/documents` && route.request().method() === 'POST') {
      assert.ok(route.request().postDataBuffer().includes(Buffer.from('name="documentCode"\r\n\r\nCA')))
      uploadedAcceptance = true
      body = { id: 1001, documentCode: 'CA', status: 'pending' }
    }
    else if (endpoint === '/api/students/me/documents') body = url.searchParams.get('tramiteType') === 'internship' ? [] : documents.filter(d => !presentationMissing || d.documentCode !== 'CP')
    else if (endpoint.endsWith('/file') || endpoint.includes('/pdf/')) return route.fulfill({ contentType: 'application/pdf', body: pdf })
    await route.fulfill({ contentType: 'application/json', body: JSON.stringify(body) })
  })
  await page.goto(base + '/social-service/my')
  await page.getByText('Expediente documental', { exact: true }).waitFor({timeout:10000})
  assert.equal(await page.getByText('Calificación por Criterio', { exact: false }).count(), 0)
  assert.ok(await page.locator('input[type=file]').count() > 0)
  const commitment = page.locator('article').filter({ hasText: '2. Carta Compromiso' })
  assert.equal(await commitment.getByRole('button', { name: 'Editar datos del formato' }).count(), 0)
  const row = page.locator('article').filter({ hasText: 'Formato de Evaluación Cualitativa' }).first()
  await row.getByRole('button', { name: 'Descargar prellenado' }).click()
  assert.ok(pdfRequests.includes(`/api/social-services/${detail.id}/pdf/evaluation_1`))
  const dialog = page.getByRole('dialog')
  await dialog.waitFor({ state: 'visible' })
  assert.match(await dialog.locator('iframe').getAttribute('src'), /^blob:/)
  await dialog.getByRole('link', { name: 'Descargar PDF' }).waitFor({ state: 'visible' })
  await page.screenshot({ path: path.join(root, 'ui-desktop.png'), fullPage: false })
  await page.keyboard.press('Escape')
  await dialog.waitFor({ state: 'hidden' })
  assert.equal(await page.locator('iframe').count(), 0)
  const termination = page.locator('article').filter({ hasText: '8. Carta de Terminación' })
  await termination.getByRole('button', { name: 'Descargar prellenado' }).click()
  await dialog.waitFor({ state: 'visible' })
  assert.ok(pdfRequests.includes(`/api/social-services/${detail.id}/pdf/termination_letter`))
  await page.keyboard.press('Escape')
  await dialog.waitFor({ state: 'hidden' })
  await page.setViewportSize({ width: 390, height: 844 })
  await page.screenshot({ path: path.join(root, 'ui-mobile.png'), fullPage: false })
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1), 'Mobile horizontal overflow')
  presentationMissing = true
  await page.reload()
  const presentation = page.locator('article').filter({ hasText: '0. Carta de Presentación' })
  await presentation.waitFor()
  assert.equal(await presentation.locator('input[type=file]').count(), 1)
  assert.equal(await presentation.getByRole('button', { name: 'Descargar prellenado' }).count(), 0)
  await presentation.scrollIntoViewIfNeeded()
  await page.screenshot({ path: path.join(root, 'ui-cp-upload-mobile.png') })
  closed = true
  await page.reload()
  await page.getByText('Expediente documental', { exact: true }).waitFor()
  assert.equal(await page.locator('input[type=file]').count(), 0)
  assert.equal(await page.getByRole('button', { name: 'Editar datos del formato' }).count(), 0)
  await page.setViewportSize({ width: 1440, height: 1000 })
  await page.goto(base + '/internships/my')
  await page.getByText('Expediente documental', { exact: true }).waitFor()
  const acceptance = page.locator('article').filter({ has: page.locator('strong').filter({ hasText: /^Carta de Aceptación$/ }) })
  assert.equal(await acceptance.locator('input[type=file]').count(), 0)
  const evaluation = page.locator('article').filter({ hasText: 'Anexo XVII · Evaluación de desempeño' })
  assert.equal(await evaluation.locator('input[type=file]').count(), 1)
  await evaluation.getByRole('button', { name: 'Editar datos del formato' }).click()
  await page.getByRole('dialog').waitFor()
  assert.equal(await page.getByRole('dialog').locator('input[type=date]').count(), 2)
  assert.equal(await page.getByRole('dialog').locator('select').count(), 0)
  await page.keyboard.press('Escape')
  await page.screenshot({ path: path.join(root, 'ui-pp-desktop.png') })
  await page.setViewportSize({ width: 390, height: 844 })
  await evaluation.scrollIntoViewIfNeeded()
  await page.screenshot({ path: path.join(root, 'ui-pp-mobile.png') })
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1), 'PP mobile overflow')
  admin = true
  await page.setViewportSize({ width: 1440, height: 1000 })
  await page.goto(base + '/internships')
  await page.getByRole('button', { name: 'Gestionar', exact: false }).click()
  await page.getByText('Expediente documental', { exact: true }).waitFor()
  await acceptance.locator('input[type=file]').setInputFiles({ name: 'aceptacion.pdf', mimeType: 'application/pdf', buffer: pdf })
  const preview = page.getByRole('dialog', { name: 'Verifica el archivo:', exact: false })
  await preview.waitFor({ state: 'visible' })
  assert.equal(uploadedAcceptance, false)
  assert.match(await preview.locator('iframe').getAttribute('src'), /^blob:/)
  await preview.getByRole('button', { name: 'Cancelar' }).click()
  assert.equal(uploadedAcceptance, false)
  await acceptance.locator('input[type=file]').setInputFiles({ name: 'aceptacion.pdf', mimeType: 'application/pdf', buffer: pdf })
  await preview.getByRole('button', { name: 'Confirmar y enviar a revisión' }).click()
  await preview.waitFor({ state: 'hidden' })
  assert.ok(uploadedAcceptance)
  await page.screenshot({ path: path.join(root, 'ui-pp-admin.png') })
  admin = false
  await page.goto(base + '/students/me')
  const imssUpload = page.getByRole('button', { name: 'Subir Constancia IMSS (PDF)', exact: true })
  await imssUpload.waitFor()
  await page.locator('input[type=file]').setInputFiles({ name: 'imss.pdf', mimeType: 'application/pdf', buffer: pdf })
  assert.ok(await imssUpload.isEnabled(), 'IMSS date is optional on upload')
  await imssUpload.click()
  assert.equal(uploadedImss, false)
  await page.getByRole('dialog', { name: 'Verifica tu constancia IMSS' }).getByRole('button', { name: 'Confirmar y enviar a revisión' }).click()
  await page.getByText('Constancia IMSS cargada. Vinculación debe revisarla y aprobarla.', { exact: true }).waitFor()
  assert.ok(uploadedImss)
  await page.setViewportSize({ width: 390, height: 844 })
  await page.screenshot({ path: path.join(root, 'ui-imss-profile-mobile.png') })
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1), 'IMSS profile mobile overflow')
  jointOpening = true; closed = false
  await page.goto(base + '/social-service/my')
  const sendJoint = page.getByRole('button', { name: 'Enviar Solicitud e IMSS a revisión' })
  await sendJoint.waitFor()
  assert.equal(await page.getByRole('button', { name: 'Actualizar estado IMSS' }).count(), 0)
  assert.equal(await page.getByRole('button', { name: 'Subir constancia para revisión' }).count(), 0)
  await page.locator('input[type=file]').setInputFiles({ name: 'imss.pdf', mimeType: 'application/pdf', buffer: pdf })
  await page.getByLabel('Domicilio Completo del Estudiante *', { exact: true }).fill('Calle de prueba 10')
  await page.getByLabel('Actividades a Desarrollar en el Programa (Anexo XVIII) *', { exact: true }).fill('Apoyo comunitario')
  await sendJoint.click()
  const jointPreview = page.getByRole('dialog', { name: 'Confirma tu Solicitud y constancia IMSS' })
  await jointPreview.waitFor()
  assert.equal(jointSent, false)
  await jointPreview.getByRole('button', { name: 'Cancelar' }).click()
  assert.equal(jointSent, false)
  await sendJoint.click()
  await jointPreview.getByRole('button', { name: 'Confirmar y enviar a revisión' }).click()
  await jointPreview.waitFor({ state: 'hidden' })
  assert.ok(jointSent)
  assert.deepEqual(errors, [])
  console.log('SS/PP desktop/mobile, viewer Escape, terminal actions, PP dates, staff-only acceptance and declared IMSS date checks passed.')
} finally { await browser.close() }
