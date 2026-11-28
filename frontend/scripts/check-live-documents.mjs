import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { pathToFileURL } from 'node:url'

// Read-only smoke check against the running local environment; never uploads or dictates evidence.
const module = process.env.PLAYWRIGHT_MODULE || 'playwright'
const { chromium } = await import(path.isAbsolute(module) ? pathToFileURL(module).href : module)
const base = process.env.UI_TEST_URL || 'http://localhost:3000'
const password = process.env.UI_TEST_PASSWORD || 'TecNM-Demo-2026!'
const studentUsername = process.env.UI_TEST_STUDENT_USERNAME || 'l23050363@monclova.tecnm.mx'
const root = path.resolve('tmp/pdf-qa')
const browser = await chromium.launch({ channel: 'msedge', headless: true })
try {
  for (const username of ['jefa.vinculacion', studentUsername]) {
    const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1440, height: 1000 } })
    const page = await context.newPage()
    const errors = []
    page.on('pageerror', error => errors.push(error.message))
    await page.goto(base + '/login')
    await page.getByLabel('Correo institucional o usuario de personal', { exact: true }).fill(username)
    await page.getByLabel('Contraseña', { exact: true }).fill(password)
    const [login] = await Promise.all([
      page.waitForResponse(response => response.url().endsWith('/api/auth/login')),
      page.getByRole('button', { name: 'Ingresar', exact: true }).click(),
    ])
    assert.equal(login.status(), 200, `${username}: login response`)
    await page.waitForURL(url => !url.pathname.includes('login'), { timeout: 10000 })
    const me = await context.request.get(base + '/api/auth/me')
    assert.equal(me.status(), 200, `${username}: authenticated cookie`)
    if (username === 'jefa.vinculacion') {
      await page.goto(base + '/expedients/review')
      await page.getByRole('button', { name: 'Actualizar bandeja', exact: true }).waitFor()
      const queue = await context.request.get(base + '/api/documents/review-queue?pageSize=1')
      assert.equal(queue.status(), 200)
      assert.ok(Array.isArray((await queue.json()).items))
      for (const [type, endpoint, file] of [['internships', 'evaluation', 'pp'], ['social-services', 'application', 'ss'], ['social-services', 'commitment', 'ss-commitment']]) {
        const list = await context.request.get(`${base}/api/${type}?pageSize=50`)
        assert.equal(list.status(), 200)
        let generated = false
        for (const item of (await list.json()).items) {
          const pdf = await context.request.get(`${base}/api/${type}/${item.id}/pdf/${endpoint}`)
          if (pdf.status() === 422) continue // An existing record may lack required printable data.
          assert.equal(pdf.status(), 200, `${type}: official PDF generation`)
          const bytes = await pdf.body()
          assert.equal(bytes.subarray(0, 4).toString(), '%PDF')
          fs.writeFileSync(path.join(root, `live-${file}.pdf`), bytes)
          generated = true; break
        }
        assert.ok(generated, `${type}: at least one existing demo record can generate PDF`)
      }
      await page.screenshot({ path: path.join(root, 'live-review.png') })
    } else {
      await page.goto(base + '/students/me')
      await page.getByRole('button', { name: 'Subir Constancia IMSS (PDF)', exact: true }).waitFor()
      assert.equal((await context.request.get(base + '/api/students/me')).status(), 200)
      await page.screenshot({ path: path.join(root, 'live-student-profile.png') })
    }
    assert.deepEqual(errors, [])
    await context.close()
  }
  console.log('Live local login student/admin, authenticated queue and official SS/PP PDFs passed; no evidence modified.')
} finally { await browser.close() }
