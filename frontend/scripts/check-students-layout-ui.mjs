import assert from 'node:assert/strict'
import fs from 'node:fs'
import {pathToFileURL} from 'node:url'
const {chromium}=await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE).href)
const browser=await chromium.launch({channel:'msedge',headless:true})
const base=process.env.UI_TEST_URL || 'https://127.0.0.1:5900'
const output='tmp/pdf-qa/students-layout';fs.mkdirSync(output,{recursive:true})
try {
 const context=await browser.newContext({ignoreHTTPSErrors:true,viewport:{width:1440,height:1000}})
 await context.addCookies([{name:'session_exp',value:String(Math.floor(Date.now()/1000)+3600),url:base,secure:true}])
 const page=await context.newPage(),errors=[],requests=[]
 page.on('pageerror',e=>errors.push(e.message))
 let role='department_head',failDetail=false,failList=false
 const followUp={status:'in_progress',progressAssessment:'low_progress',approvedDocumentsCount:2,eligiblePeriodCode:'FJ26',trackingPeriodCode:'AD26',trackingPeriodId:3,elapsedSemesters:1,lastApprovedAt:'2026-10-01T12:00:00Z',notes:'Revisar segundo reporte bimestral',reviewedAt:'2026-11-08T12:00:00Z'}
 const row={id:1,controlNumber:'I23050363',fullName:'Héctor Gabriel Torres Arzola',careerCode:'INF',careerId:1,currentSemester:7,canReadProfile:true,hasUserAccount:true,socialServiceFollowUp:followUp}
 const full={...row,firstName:'Héctor Gabriel',firstSurname:'Torres',secondSurname:'Arzola',curp:'TOAH050315HCLRRH01',careerName:'Ingeniería Informática',completedCredits:192,curriculumProgressPercentage:73.85,shift:'morning',isEnrolled:true,gender:'M',hasDisability:false,personalEmail:'personal@example.test',institutionalEmail:'alumno.institucional@example.test',phoneNumber:'8661234567',accountUsername:'alumno.institucional@example.test',accountStatus:'active',imssStatus:'approved',hasValidImss:true,hasImssFile:false,createdAt:'2026-02-09T12:00:00Z',updatedAt:'2026-11-08T12:00:00Z'}
 await page.route('**/api/**',async route=>{
  const u=new URL(route.request().url());if(!u.pathname.startsWith('/api/'))return route.continue()
  requests.push(u.pathname);let data=[]
  if(u.pathname==='/api/auth/me')data={id:1,name:'Prueba',username:'qa',roles:[role]}
  else if(u.pathname==='/api/students') {
   if(failList)return route.fulfill({status:500,json:{detail:'Error de padrón simulado'}})
   const items=u.searchParams.get('search')==='Sin coincidencias'?[]:[{...row,canReadProfile:role==='department_head'}]
   data={items,totalItems:items.length,totalPages:items.length,page:1,pageSize:50}
  }else if(u.pathname==='/api/students/1') {
   if(failDetail)return route.fulfill({status:500,json:{detail:'Detalle temporalmente no disponible'}})
   data=full
  }else if(u.pathname.endsWith('/options'))data=[{id:1,label:'Informática',meta:{isActive:true}}]
  await route.fulfill({json:data})
 })
 await page.goto(base+'/students')
 const table=page.getByRole('region',{name:'Listado del Padrón de Alumnos',exact:true})
 await table.getByText(row.fullName,{exact:true}).waitFor()
 assert.equal(await table.locator('thead th').count(),5)
 assert.equal(requests.filter(p=>p.includes('review-queue')).length,0)
 assert.equal(await page.getByText('IMSS de preingreso',{exact:true}).count(),0)
 assert.equal(await table.getByText('73.85%').count(),0)
 assert.equal(await table.getByText(full.accountUsername).count(),0)
 for(const width of [1440,1024,390]) {
  await page.setViewportSize({width,height:1000})
  assert.ok(await table.evaluate(el=>el.scrollWidth<=el.clientWidth+1),'table overflow at '+width)
  assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'page overflow at '+width)
  await page.screenshot({path:output+'/list-'+width+'.png',fullPage:true})
 }
 const button=table.getByRole('button',{name:'Ver detalle de '+row.fullName})
 failDetail=true;await button.click()
 const detail=page.getByRole('dialog',{name:'Detalle del alumno',exact:true})
 await detail.getByText('Detalle temporalmente no disponible').waitFor()
 failDetail=false;await detail.getByRole('button',{name:'Reintentar',exact:true}).click()
 await detail.getByText(full.curp,{exact:true}).waitFor()
 for(const value of [full.personalEmail,full.phoneNumber,'73.85%','Revisar segundo reporte bimestral'])await detail.getByText(value,{exact:true}).waitFor()
 assert.ok(await detail.evaluate(el=>el.scrollWidth<=el.clientWidth+1),'detail mobile overflow')
 await page.screenshot({path:output+'/detail-mobile.png',fullPage:true})
 await page.keyboard.press('Escape');await detail.waitFor({state:'hidden'});assert.ok(await button.evaluate(el=>el===document.activeElement))
 await page.evaluate(()=>localStorage.setItem('tecnm_sspp_theme','dark'));await page.reload();await table.getByText(row.fullName,{exact:true}).waitFor()
 await page.screenshot({path:output+'/dark-mobile.png',fullPage:true})
 const search=page.getByRole('searchbox',{name:'Buscar alumno',exact:true})
 await search.fill('Sin coincidencias');await page.getByText('No hay alumnos que coincidan con los filtros.',{exact:true}).waitFor()
 await search.fill('');await table.getByText(row.fullName,{exact:true}).waitFor()
 failList=true;await search.fill('Error');await page.getByText('Error de padrón simulado',{exact:true}).waitFor();assert.equal(await table.locator('tbody tr').count(),0)
 failList=false;role='social_service_officer';await page.reload();await table.getByText(row.fullName,{exact:true}).waitFor()
 const before=requests.filter(p=>p==='/api/students/1').length
 await button.click();await detail.getByText(/Este candidato dispone de un resumen/).waitFor()
 assert.equal(requests.filter(p=>p==='/api/students/1').length,before)
 assert.equal(await detail.getByText(full.curp,{exact:true}).count(),0)
 assert.deepEqual(errors,[])
 console.log('Students layout: one list, five columns, no overflow, full/restricted detail, retry, focus, empty/error and dark mode passed.')
}finally{await browser.close()}
