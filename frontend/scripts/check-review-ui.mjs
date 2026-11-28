import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { pathToFileURL } from 'node:url'

// Synthetic queue states exercise UI guards; HTTP authorization is tested separately.
const module = process.env.PLAYWRIGHT_MODULE || 'playwright'
const { chromium } = await import(path.isAbsolute(module) ? pathToFileURL(module).href : module)
const base = process.env.UI_TEST_URL || 'https://127.0.0.1:5900'
const root = path.resolve('tmp/pdf-qa')
const sample = JSON.parse(fs.readFileSync(path.join(root, 'ui-fixtures/documents.json')))[0]
const item = { ...sample, id: 999, tramiteType: 'general', tramiteId: null, documentCode: 'IMSS',
  originalFileName: 'vigencia-imss.pdf',
  status: 'pending', documentDate: null, studentFullName: 'Estudiante de prueba',
  studentControlNumber: 'TEST001', careerCode: 'ISC', careerName: 'Ingeniería en Sistemas Computacionales' }
const pdf = fs.readFileSync('backend/Resources/Documents/templates/ss-supervisor-evaluation.pdf')
let fail = false
const reviews = [], queries = []
const browser = await chromium.launch({ channel: 'msedge', headless: true })
try {
  const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1440, height: 1000 } })
  await context.addCookies([{ name: 'session_exp', value: String(Math.floor(Date.now()/1000)+3600), url: base, secure: true }])
  const page = await context.newPage()
  const errors = []
  page.on('pageerror', error => errors.push(error.message))
  await page.addInitScript(() => {
    window.revokedPdfUrls = []
    const original = URL.revokeObjectURL.bind(URL)
    URL.revokeObjectURL = url => { window.revokedPdfUrls.push(url); original(url) }
  })
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url()), endpoint = url.pathname
    if (!endpoint.startsWith('/api/')) return route.continue()
    let body = []
    if (endpoint === '/api/auth/me') body = { id: 5, username: 'jefa.vinculacion', name: 'Jefatura de prueba', roles: ['department_head'] }
    else if (endpoint === '/api/documents/review-queue') {
      queries.push(Object.fromEntries(url.searchParams))
      if (fail) return route.fulfill({ status: 500, contentType: 'application/json', body: '{}' })
      const items = !url.searchParams.get('search') || item.studentFullName.includes(url.searchParams.get('search')) ? [item] : []
      body = { items, totalItems: items.length, totalPages: 1 }
    } else if (endpoint.endsWith('/imss-date-suggestion')) body = { status: 'not_found', suggestedDate: null, pageNumber: null }
    else if (endpoint.endsWith('/review')) {
      const payload = route.request().postDataJSON()
      reviews.push(payload); item.status = payload.status
      body = item
    } else if (endpoint.endsWith('/file')) return route.fulfill({ contentType: 'application/pdf', body: pdf })
    await route.fulfill({ contentType: 'application/json', body: JSON.stringify(body) })
  })
  await page.goto(base + '/expedients/review')
  const row = page.locator('tbody tr').filter({ hasText: 'TEST001' })
  await row.waitFor()
  assert.ok(await row.getByText('General / IMSS', { exact: false }).count())
  assert.equal(await page.getByText('Sello Digital', { exact: false }).count(), 0)
  await row.getByRole('button', { name: 'Ver PDF', exact: true }).click()
  const dialog = page.getByRole('dialog')
  await dialog.locator('iframe').waitFor()
  const blob = await dialog.locator('iframe').getAttribute('src')
  await page.keyboard.press('Escape')
  await dialog.waitFor({ state: 'hidden' })
  assert.ok(await page.evaluate(url => window.revokedPdfUrls.includes(url), blob))
  await row.getByRole('button', { name: 'Dictaminar', exact: true }).click()
  await dialog.getByRole('button', { name: 'Rechazar documento' }).click()
  await dialog.getByText('Indica el motivo de rechazo.', { exact: true }).waitFor()
  await dialog.getByRole('button', { name: 'Aprobar documento' }).click()
  await dialog.getByText('Confirma que el PDF corresponde', { exact: false }).last().waitFor()
  assert.equal(reviews.length, 0)
  await dialog.getByRole('checkbox').check()
  await page.screenshot({ path: path.join(root, 'ui-review-desktop.png') })
  await dialog.getByRole('button', { name: 'Aprobar documento' }).click()
  await dialog.waitFor({ state: 'hidden' })
  assert.deepEqual(reviews[0], { status: 'approved' })
  await page.getByLabel('Buscar documento en cola de revisión').fill('Sin coincidencias')
  await row.waitFor({ state: 'hidden' })
  assert.equal(queries.at(-1).search, 'Sin coincidencias')
  await page.getByLabel('Buscar documento en cola de revisión').fill('')
  await row.waitFor()
  await page.getByLabel('Tipo', { exact: true }).selectOption('general')
  await page.waitForResponse(response => response.url().includes('tramiteType=general'))
  assert.equal(queries.at(-1).tramiteType, 'general')
  await page.setViewportSize({ width: 390, height: 844 })
  item.status = 'pending'
  await page.getByRole('button', { name: 'Actualizar bandeja', exact: true }).click()
  await row.getByRole('button', { name: 'Dictaminar', exact: true }).click()
  await dialog.getByLabel('Motivo de rechazo', { exact: false }).fill('Texto ilegible; subir PDF completo.')
  await page.screenshot({ path: path.join(root, 'ui-review-mobile.png') })
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1), 'Review mobile horizontal overflow')
  await dialog.getByRole('button', { name: 'Rechazar documento' }).click()
  await dialog.waitFor({ state: 'hidden' })
  assert.deepEqual(reviews[1], { status: 'rejected', rejectionReason: 'Texto ilegible; subir PDF completo.' })
  fail = true
  await page.getByRole('button', { name: 'Actualizar bandeja', exact: true }).click()
  await page.getByText('Error al cargar. Actualiza la bandeja para reintentar.').waitFor()
  await page.locator('tbody tr').waitFor({ state: 'detached' })
  fail = false
  await page.getByRole('button', { name: 'Actualizar bandeja', exact: true }).click()
  await row.waitFor()
  assert.deepEqual(errors, [])
  console.log('Review guards, IMSS payload, server search, PDF cleanup, mobile and API failure checks passed.')
} finally { await browser.close() }
