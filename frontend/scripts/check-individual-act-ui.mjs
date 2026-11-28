import fs from 'node:fs';
import {pathToFileURL} from 'node:url';
const {chromium}=await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE).href);
fs.mkdirSync('tmp/individual-act',{recursive:true});
const detail=JSON.parse(fs.readFileSync('tmp/pdf-qa/ui-fixtures/social-service.json'));
const b=await chromium.launch({channel:'msedge',headless:true});
try {
const c=await b.newContext({ignoreHTTPSErrors:true});const base=process.env.UI_TEST_URL || 'https://127.0.0.1:5900';
await c.addCookies([{name:'session_exp',value:String(Math.floor(Date.now()/1000)+3600),url:base,secure:true}]);
const p=await c.newPage();let requests=0;
await p.route('**/api/**',async r=>{const u=new URL(r.request().url());let data=[];
if(u.pathname==='/api/auth/me')data={id:5,name:'Jefatura',username:'jefa.vinculacion',roles:['department_head']};
else if(u.pathname==='/api/social-services')data={items:[{...detail,studentFullName:detail.student.fullName,studentControlNumber:detail.student.controlNumber,careerCode:'INF',periodCode:detail.period.code,status:'released'}],totalItems:1,totalPages:1};
else if(u.pathname===`/api/social-services/${detail.id}`)data={...detail,status:'released'};
else if(u.pathname.endsWith('/pdf/individual_act')){requests++;return r.fulfill({status:422,json:{detail:'Datos incompletos',errors:{companyName:['Falta la dependencia registrada.']}}});}
await r.fulfill({json:data});});
await p.goto(base+'/social-service');await p.getByRole('button',{name:'Gestionar Expediente'}).click();
for(const width of [1440,390]){await p.setViewportSize({width,height:950});const btn=p.getByRole('button',{name:'Descargar Acta Individual',exact:true});await btn.scrollIntoViewIfNeeded();await btn.click();await p.getByText('Falta la dependencia registrada.',{exact:false}).first().waitFor();await p.screenshot({path:`tmp/individual-act/ui-${width}.png`});}
if(requests!==2)throw Error('Unexpected requests');console.log('Act button and blob errors passed desktop/mobile');
}finally{await b.close();}


