import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { pathToFileURL } from 'node:url'

// Read-only integration smoke: only authentication writes; never uploads or dictates evidence.
const module = process.env.PLAYWRIGHT_MODULE || 'playwright'
const { chromium } = await import(path.isAbsolute(module) ? pathToFileURL(module).href : module)
const base = process.env.UI_TEST_URL || 'http://localhost:3000'
const password = process.env.UI_TEST_PASSWORD || 'TecNM-Demo-2026!'
const studentUsername = process.env.UI_TEST_STUDENT_USERNAME || 'l23050363@monclova.tecnm.mx'
const output = path.resolve('tmp/pdf-qa/live-process-access')
fs.mkdirSync(output, { recursive: true })
const snapshotPath = process.env.LINKAGE_SQL_SNAPSHOT
const expected = snapshotPath ? JSON.parse(fs.readFileSync(path.resolve(snapshotPath), 'utf8').replace(/^\uFEFF/, '')) : []
const browser = await chromium.launch({ channel: 'msedge', headless: true })
const processPaths = { social_service: 'social-services', internship: 'internships' }
const headings = { social_service: 'Hoja ESTADÍSTICO (Servicio Social)', internship: 'Hoja ESTADÍSTICO (Prácticas Profesionales)' }
const buttons = { social_service: 'Estadístico Servicio Social', internship: 'Estadístico Prácticas Profesionales' }
const catalogTypes = new Set(['public','micro','small','medium','large','social'])
const references = {}
async function json(request, endpoint) {
  const response = await request.get(base + endpoint)
  assert.equal(response.status(), 200, `${endpoint}: HTTP ${response.status()}`)
  return response.json()
}
try {
  for (const [username, role, own] of [
    ['jefa.vinculacion','department_head','social_service'],
    ['encargado.ss','social_service_officer','social_service'],
    ['coord.practicas','internship_coordinator','internship'],
    [studentUsername,'student',null],
  ]) {
    const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width:1440,height:1000 } })
    const page = await context.newPage()
    page.setDefaultTimeout(15000)
    const errors = []
    page.on('pageerror', error => errors.push(error.message))
    await page.goto(base + '/login')
    await page.getByLabel('Correo institucional o usuario de personal', { exact:true }).fill(username)
    await page.getByLabel('Contraseña', { exact:true }).fill(password)
    const [login] = await Promise.all([
      page.waitForResponse(response => response.url().endsWith('/api/auth/login')),
      page.getByRole('button', { name:'Ingresar',exact:true }).click(),
    ])
    assert.equal(login.status(),200, `${role}: login`)
    await page.waitForURL(url => url.pathname === '/')
    const me = await json(context.request, '/api/auth/me')
    assert.ok(me.roles.includes(role))
    if (role === 'student') {
      for (const endpoint of ['/api/companies','/api/social-services','/api/internships','/api/reports/linkage?tramiteType=internship'])
        assert.equal((await context.request.get(base + endpoint)).status(),403, `${role}: ${endpoint}`)
      await page.goto(base + '/reports/stats')
      await page.getByRole('heading',{name:'Acceso no disponible'}).waitFor()
      await context.close()
      continue
    }
    const companies = await json(context.request,'/api/companies?pageSize=100')
    assert.ok(companies.items.length > 0)
    assert.ok(companies.items.every(company => catalogTypes.has(company.companyType)))
    if (role === 'department_head') {
      for (const [type,endpoint] of Object.entries(processPaths)) {
        const list = await json(context.request,`/api/${endpoint}?pageSize=100`)
        references[type] = list.items
      }
    }
    if (role === 'social_service_officer' || role === 'internship_coordinator') {
      const foreign = own === 'social_service' ? 'internship' : 'social_service'
      assert.equal((await context.request.get(`${base}/api/${processPaths[foreign]}`)).status(),403)
      assert.equal((await context.request.get(`${base}/api/reports/tracking?tramiteType=${foreign}`)).status(),403)
      const ownIds = new Set(references[own].map(row => row.studentId))
      const students = await json(context.request,'/api/students?pageSize=100')
      assert.ok(students.items.every(row => ownIds.has(row.id)), `${role}: student list leaked opposite process`)
      if (references[foreign].length) {
        const item = references[foreign][0]
        assert.equal((await context.request.get(`${base}/api/${processPaths[foreign]}/${item.id}`)).status(),403)
        assert.ok([403,404].includes((await context.request.get(`${base}/api/${processPaths[foreign]}/${item.id}/pdf/${foreign==='internship'?'evaluation':'application'}`)).status()))
      }
    }
    for (const type of Object.keys(processPaths)) {
      const linkage = await json(context.request,`/api/reports/linkage?tramiteType=${type}`)
      assert.equal(linkage.tramiteType,type)
      assert.equal(linkage.byCompanyType.length,6)
      assert.equal(linkage.byCompanyType.reduce((sum,row)=>sum+row.assignedStudents,0),linkage.totals.assignedStudents)
      assert.equal(linkage.byCompanyType.reduce((sum,row)=>sum+row.activeStudents,0),linkage.totals.activeStudents)
      assert.ok(linkage.companies.items.every(row=>!('studentId' in row) && !('tramiteId' in row)))
    }
    for (const row of expected) {
      const linkage = await json(context.request,`/api/reports/linkage?tramiteType=${row.tramiteType}&periodId=${row.periodId}&pageSize=1`)
      assert.deepEqual(linkage.totals,{assignedStudents:row.assignedStudents,activeStudents:row.activeStudents,projects:row.projects},`${role}: SQL/HTTP aggregate mismatch`)
    }
    const first = page.waitForResponse(response => response.url().includes('/api/reports/linkage?') && new URL(response.url()).searchParams.get('tramiteType')===own)
    await page.goto(base + '/reports/stats')
    assert.equal((await first).status(),200)
    await page.getByText(headings[own],{exact:true}).waitFor()
    const foreign = own === 'social_service' ? 'internship' : 'social_service'
    const switched = page.waitForResponse(response => response.url().includes('/api/reports/linkage?') && new URL(response.url()).searchParams.get('tramiteType')===foreign)
    await page.getByRole('button',{name:buttons[foreign],exact:true}).click()
    assert.equal((await switched).status(),200)
    await page.getByText(headings[foreign],{exact:true}).waitFor()
    assert.equal(await page.getByText(headings[own],{exact:true}).count(),0)
    await page.screenshot({path:path.join(output,`${role}-statistics.png`),fullPage:true})
    await page.goto(base + '/companies')
    await page.getByRole('table').waitFor()
    await page.getByRole('button',{name:'Registrar empresa',exact:true}).click()
    await page.getByRole('dialog').getByLabel('Tipo de empresa',{exact:true}).waitFor()
    await page.keyboard.press('Escape')
    await page.screenshot({path:path.join(output,`${role}-companies.png`),fullPage:true})
    if (role === 'social_service_officer' || role === 'internship_coordinator') {
      await page.setViewportSize({width:390,height:844})
      assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Company layout overflow')
      await page.screenshot({path:path.join(output,`${role}-companies-mobile.png`),fullPage:true})
    }
    assert.deepEqual(errors,[],`${role}: browser errors`)
    await context.close()
  }
  console.log('Live five-role login, company classification, SS/PP scope, area defaults/switching and SQL/HTTP metrics passed. No business data modified.')
} finally { await browser.close() }
