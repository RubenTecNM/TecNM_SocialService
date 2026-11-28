import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { pathToFileURL } from 'node:url'

const moduleName = process.env.PLAYWRIGHT_MODULE || 'playwright'
const { chromium } = await import(path.isAbsolute(moduleName) ? pathToFileURL(moduleName).href : moduleName)
const base = process.env.UI_TEST_URL || 'https://127.0.0.1:5900'
const pdf = fs.readFileSync('backend/Resources/Documents/templates/ss-report.pdf')
const manifest = JSON.parse(fs.readFileSync('backend/Resources/Documents/manifest.json', 'utf8'))
const key = 'ss-report'
const active = { key, area: 'social_service', mode: 'prefill', activeVersionId: 1, draftId: null, revision: 125 }
const layout = structuredClone(manifest[key])
const brand = JSON.parse(fs.readFileSync('backend/Resources/Documents/branding/manifest.json', 'utf8'))[key]
const png = fs.readFileSync('backend/Resources/Documents/branding/' + brand.zones[0].png)
layout.logoZones = brand.zones
layout.fields = layout.fields.map(field => ({ page: 0, alignment: 'left', fontSize: null, minFontSize: null, rows: null, ...field }))
const draft = { id: 2, revision: 126, publishedAt: null, slots: structuredClone(layout.fields), layout,
  logos: brand.zones.map((zone, slot) => ({ slot, logoId: 1, x: zone.x, y: zone.y, width: zone.width, height: zone.height })) }
let saved, savedLogos, publish, authority
let adminView = false
let uploaded = false
const browser = await chromium.launch({ channel: 'msedge', headless: true })
try {
  const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1440, height: 1080 } })
  await context.addCookies([{ name: 'session_exp', value: String(Math.floor(Date.now() / 1000) + 3600), url: base, secure: true }])
  const page = await context.newPage()
  page.setDefaultTimeout(12_000)
  page.on('pageerror', error => console.error(error.message))
  await page.route('**/api/**', async route => {
    const request = route.request()
    const url = new URL(request.url())
    const endpoint = url.pathname
    if (!endpoint.startsWith('/api/')) return route.continue()
    const method = request.method()
    let json = []
    if (endpoint === '/api/auth/me') json = { id: 2, username: 'ss', name: 'Usuario de prueba', roles: [adminView ? 'admin' : 'social_service_officer'] }
    else if (endpoint === '/api/template-management') json = [active]
    else if (endpoint === '/api/template-management/logos') json = [{ id: 1, name: 'Marca inicial', width: 800, height: 200 }, { id: 2, name: 'Marca nueva', width: 800, height: 200 }]
    else if (endpoint.endsWith('/image')) return route.fulfill({ contentType: 'image/png', body: png })
    else if (endpoint === '/api/institutional-authorities/active') json = {}
    else if (endpoint === '/api/institutional-authorities' && method === 'GET') json = { items: [] }
    else if (endpoint === '/api/institutional-authorities' && method === 'POST') {
      assert.equal(request.headers()['x-csrf-token'], 'template-csrf')
      authority = request.postDataJSON()
      json = { id: 1, isActive: true, ...authority }
    }
    else if (endpoint.endsWith('/csrf')) json = { token: 'template-csrf' }
    else if (endpoint.endsWith('/draft') && method === 'POST') {
      assert.equal(request.headers()['x-csrf-token'], 'template-csrf')
      active.draftId = draft.id
      json = draft
    } else if (endpoint.endsWith('/draft/file')) {
      assert.match(request.headers()['content-type'], /multipart\/form-data/)
      assert.equal(url.searchParams.get('revision'), String(draft.revision))
      draft.revision++
      uploaded = true
      json = draft
    } else if (endpoint.endsWith('/draft/layout')) {
      saved = request.postDataJSON()
      assert.equal(saved.revision, draft.revision)
      draft.layout.fields = saved.fields
      draft.revision++
      json = draft
    } else if (endpoint.endsWith('/draft/logos')) {
      savedLogos = request.postDataJSON()
      assert.equal(savedLogos.revision, draft.revision)
      draft.logos = savedLogos.logos
      draft.revision++
      json = draft
    } else if (endpoint.endsWith('/draft/publish')) {
      publish = request.postDataJSON()
      active.activeVersionId = draft.id
      active.draftId = null
      return route.fulfill({ status: 204 })
    } else if (endpoint.endsWith('/versions')) json = [{ id: active.activeVersionId, publishedAt: '2026-11-26T14:00:00Z' }]
    else if (endpoint.endsWith('/file') || endpoint.endsWith('/draft/preview')) return route.fulfill({ contentType: 'application/pdf', body: pdf })
    return route.fulfill({ json })
  })
  await page.goto(base + '/catalogs/document-templates')
  await page.getByRole('heading', { name: 'Plantillas documentales' }).waitFor().catch(async error => {
    console.error(page.url(), await page.locator('body').innerText())
    throw error
  })
  await page.getByLabel('Formato institucional').selectOption(key)
  await page.getByRole('button', { name: 'Crear borrador' }).click()
  await page.getByLabel('PDF sustituto', { exact: true }).setInputFiles({ name: 'sustituto.pdf', mimeType: 'application/pdf', buffer: pdf })
  await page.getByRole('status').filter({ hasText: 'PDF cargado' }).waitFor()
  assert.ok(uploaded)
  await page.waitForFunction(() => {
    const canvas = document.querySelector('canvas')
    if (!canvas || canvas.width < 600 || canvas.closest('[aria-busy="true"]')) return false
    const pixels = canvas.getContext('2d').getImageData(0, 0, canvas.width, 125).data
    return pixels.some((value, index) => index % 4 === 0 && value < 120 && pixels[index + 3] === 255)
  })
  if (process.env.TEMPLATE_EDITOR_SCREENSHOT) await page.screenshot({ path: process.env.TEMPLATE_EDITOR_SCREENSHOT })
  await page.getByRole('button', { name: 'Acercar' }).click()
  const overlay = page.getByRole('button', { name: 'Seleccionar campo 1', exact: true })
  await overlay.scrollIntoViewIfNeeded()
  const box = await overlay.boundingBox()
  const x = box.x + box.width / 2, y = box.y + box.height / 2
  await page.mouse.move(x, y)
  await page.mouse.down()
  await page.mouse.move(x - 25, y + 12.5)
  await page.mouse.up()
  const first = manifest[key].fields[0]
  const inputs = page.getByRole('group', { name: 'Configuración' })
  const moved = await inputs.getByLabel('X (pt)', { exact: true }).first().inputValue()
  assert.ok(Math.abs(Number(moved) - (first.x - 20)) <= 0.1)
  const axis = inputs.getByLabel('Línea base (pt)').first()
  await axis.fill(String(first.rows[0].baseline + 11))
  await page.getByRole('button', { name: 'Guardar configuración' }).click()
  await page.getByRole('status').filter({ hasText: 'Configuración guardada' }).waitFor()
  assert.equal(saved.fields[0].rows[0].baseline, first.rows[0].baseline + 11)
  await page.getByRole('button', { name: 'Logotipos', exact: true }).click()
  const logos = page.getByRole('group', { name: 'Logotipos del formato' })
  await logos.getByLabel('Revisión del logotipo').selectOption('2')
  const width = brand.zones[0].width - 10
  await logos.getByLabel('Ancho (pt)').fill(String(width))
  await page.getByRole('button', { name: 'Guardar logotipos' }).click()
  await page.getByRole('status').filter({ hasText: 'Logotipos guardados' }).waitFor()
  assert.equal(savedLogos.logos[0].logoId, 2)
  assert.equal(savedLogos.logos[0].width, width)
  for (const type of ['normal', 'larga']) {
    await page.getByRole('button', { name: 'Ver muestra ' + type }).click()
    const modal = page.getByRole('dialog')
    await modal.getByTitle('Muestra ' + type).waitFor()
    await modal.getByRole('button', { name: 'Cerrar', exact: true }).last().click()
  }
  await page.getByLabel('Motivo de publicación').fill('Formato redistribuido')
  await page.getByRole('button', { name: 'Publicar para nuevas aperturas' }).click()
  await page.getByRole('status').filter({ hasText: 'Plantilla publicada' }).waitFor()
  assert.deepEqual(publish, { templateRevision: 125, draftRevision: draft.revision, reason: 'Formato redistribuido' })
  adminView = true
  await page.goto(base + '/catalogs/institutional-identity')
  await page.getByLabel('Cargo institucional').selectOption('outreach_division_head')
  await page.getByLabel('Nombre del nuevo titular').fill('Lucía Muñoz Valdés')
  await page.getByLabel('Cargo para el PDF').fill('Titular de Vinculación')
  await page.getByRole('button', { name: 'Activar titular' }).click()
  await page.getByRole('status').filter({ hasText: 'Titular activado' }).waitFor()
  assert.equal(authority.roleKey, 'outreach_division_head')
  assert.equal(authority.fullName, 'Lucía Muñoz Valdés')
  console.log('Template editor, logo revisions, publication and authority catalog passed.')
} finally {
  await browser.close()
}
