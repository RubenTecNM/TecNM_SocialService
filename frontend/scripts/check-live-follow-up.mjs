import assert from 'node:assert/strict'
import fs from 'node:fs'
import { pathToFileURL } from 'node:url'
const { chromium } = await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE).href)
const base = process.env.UI_TEST_URL || 'http://localhost:3000'
const output = 'tmp/pdf-qa/live-follow-up'
fs.mkdirSync(output, { recursive: true })
const browser = await chromium.launch({ channel: 'msedge', headless: true })
const expected = {first_notice:2, second_notice:1, third_notice:1, non_compliant:1, in_progress:4, released:1, pending_setup:1}
try {
 for (const [username, role] of [['jefa.vinculacion','department_head'], ['encargado.ss','social_service_officer'], ['coord.practicas','internship_coordinator']]) {
  const context = await browser.newContext({ignoreHTTPSErrors:true, viewport:{width:1440,height:1000}})
  const page = await context.newPage(), errors=[]
  page.on('pageerror', error=>errors.push(error.message))
  await page.goto(base+'/login')
  await page.getByLabel('Correo institucional o usuario de personal',{exact:true}).fill(username)
  await page.getByLabel('Contraseña',{exact:true}).fill(process.env.UI_TEST_PASSWORD || 'TecNM-Demo-2026!')
  const [login] = await Promise.all([page.waitForResponse(r=>r.url().endsWith('/api/auth/login')), page.getByRole('button',{name:'Ingresar',exact:true}).click()])
  assert.equal(login.status(),200,username+' login')
  await page.waitForURL(url=>url.pathname==='/')
  const response=await context.request.get(base+'/api/students?pageSize=100');assert.equal(response.status(),200)
  const students=(await response.json()).items
  if(role==='internship_coordinator') {
   assert.ok(students.every(s=>s.socialServiceFollowUp===null))
   assert.equal((await context.request.get(base+'/api/students?followUpStatus=first_notice')).status(),403)
  } else {
   assert.equal(students.length,11)
   for(const [status,count] of Object.entries(expected)) {
    assert.equal(students.filter(s=>s.socialServiceFollowUp.status===status).length,count,role+' '+status)
    const filtered=await context.request.get(base+'/api/students?pageSize=1&followUpStatus='+status)
    assert.equal(filtered.status(),200)
    const data=await filtered.json();assert.equal(data.totalItems,count);assert.equal(data.items.length,1)
   }
   const candidate=students.find(s=>s.controlNumber==='I22050915')
   if(role==='social_service_officer') {
    assert.equal(candidate.canReadProfile,false);assert.equal(candidate.currentSemester,null)
    assert.equal((await context.request.get(base+'/api/students/'+candidate.id)).status(),404)
   }
   const low=students.find(s=>s.socialServiceFollowUp.progressAssessment==='low_progress')
   assert.ok(low)
   assert.ok(low.socialServiceFollowUp.autoReason)
  }
  await page.goto(base+'/students')
  await page.getByRole('region',{name:'Listado del Padrón de Alumnos'}).waitFor()
  const roster=page.getByRole('region',{name:'Listado del Padrón de Alumnos'})
  assert.equal(await page.getByText('IMSS de preingreso',{exact:true}).count(),0)
  assert.equal(await roster.locator('thead th').count(),role==='internship_coordinator'?4:5)
  const readable=students.find(s=>s.canReadProfile!==false)
  if(readable) {
   await roster.getByRole('button',{name:'Ver detalle de '+readable.fullName,exact:true}).click()
   const detail=page.getByRole('dialog',{name:'Detalle del alumno',exact:true})
   await detail.getByRole('heading',{name:'Datos personales',exact:true}).waitFor()
   await detail.getByRole('heading',{name:'Contacto y cuenta',exact:true}).waitFor()
   await page.keyboard.press('Escape');await detail.waitFor({state:'hidden'})
  }
  if(role==='internship_coordinator')assert.equal(await page.getByRole('combobox',{name:'Seguimiento SS',exact:true}).count(),0)
  else {
   const table=page.getByRole('region',{name:'Listado del Padrón de Alumnos'})
   await table.getByText('Producto no conforme',{exact:true}).waitFor()
   assert.ok(await table.evaluate(el=>el.scrollWidth<=el.clientWidth+1),'desktop overflow')
   await page.screenshot({path:output+'/'+role+'-desktop.png',fullPage:true})
   await page.setViewportSize({width:390,height:844})
   assert.ok(await table.evaluate(el=>el.scrollWidth<=el.clientWidth+1),'mobile overflow')
   await page.screenshot({path:output+'/'+role+'-mobile.png',fullPage:true})
  }
  assert.deepEqual(errors,[])
  console.log(role+': real login, SQL scenarios, API filters, review, permissions and UI passed')
  await context.close()
 }
} finally { await browser.close() }

