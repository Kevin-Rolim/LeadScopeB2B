import { chromium } from '@playwright/test';
import { mkdir, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
const url=process.argv[2];
if(!url || !process.env.LEADSCOPE_UI_PASSWORD) throw new Error('Execute o runner SQL com --visual; somente banco temporário.');
await mkdir('artifacts/ui',{recursive:true});
const browser=await chromium.launch({executablePath:process.env.CHROME_PATH||'/usr/bin/google-chrome',headless:true,args:['--no-sandbox']});
const errors=[],failed=[],screens=[];
let assertions=0;
const check=(ok,name)=>{assert.ok(ok,name);assertions++;console.log('UI OK: '+name);};
async function setup(width=1440,height=1000){const c=await browser.newContext({viewport:{width,height},locale:'pt-BR',reducedMotion:'reduce'});const p=await c.newPage();p.on('pageerror',e=>errors.push(e.message));p.on('console',m=>{if(m.type()==='error'&&!m.text().startsWith('Failed to load resource:'))errors.push(m.text())});p.on('requestfailed',r=>{if(!r.failure()?.errorText.includes('ERR_ABORTED'))failed.push(r.url())});p.on('response',r=>{if(r.status()>=400&&/\.(css|js|woff2|svg)(\?|$)/.test(r.url()))failed.push(r.url()+':'+r.status())});return {c,p};}
async function shot(p,name){await p.evaluate(()=>document.fonts.ready);await p.screenshot({path:`artifacts/ui/${name}.png`,fullPage:true});screens.push(name);}
async function login(p,who='admin'){await p.goto(url+'/Conta/Login');await p.locator('#Email').fill(who+'@example.test');await p.locator('#Senha').fill(process.env.LEADSCOPE_UI_PASSWORD);await Promise.all([p.waitForURL(/\/(Usuarios|Empresas|Leads)(\?|$)/),p.getByRole('button',{name:'Entrar',exact:true}).click()]);}
async function noOverflow(p,name){check(await p.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'sem overflow global: '+name);}
try{
 const {c,p}=await setup();
 await p.goto(url+'/Conta/Login');await shot(p,'login-desktop');
 check(await p.locator('.auth-story').isVisible(),'layout dividido do login');
 check(await p.locator('input[type=password]').count()===1,'senha protegida inicialmente');
 await p.getByRole('button',{name:'Mostrar senha'}).click();check(await p.locator('#Senha').getAttribute('type')==='text','alternar visibilidade da senha');
 await p.getByRole('button',{name:'Ocultar senha'}).click();
 await p.locator('button[type=submit]').click();check(await p.locator('#Email').evaluate(e=>!e.validity.valid),'validação de campo obrigatório');
 for(const path of ['Cadastro','RecuperarAcesso','RedefinirSenha?token=invalido']){await p.goto(url+'/Conta/'+path);await shot(p,'conta-'+path.split('?')[0]);check((await p.locator('main h1').textContent()).length>0,'tela de conta '+path);}
 await login(p);
 check(await p.locator('.app-sidebar').evaluate(e=>Math.round(e.getBoundingClientRect().width))===240,'sidebar 240px como referência');
 for(const module of ['Home','Empresas','Pessoas','Leads','Importacao','Usuarios','Auditoria']){await p.goto(url+'/'+module);check((await p.locator('main h1').textContent()).length>0,'módulo '+module);await noOverflow(p,module);await shot(p,module.toLowerCase()+'-desktop');}
 for(const module of ['Empresas','Pessoas','Leads','Usuarios']) {
   await p.goto(url+'/'+module);
   for(const action of ['Detalhes','Editar']) {
     const href=await p.locator('tbody tr').first().getByRole('link',{name:action,exact:true}).getAttribute('href');
     await p.goto(url+href); check((await p.locator('main h1').textContent()).length>0,module+' '+action+' direto');
     await shot(p,module.toLowerCase()+'-'+action.toLowerCase());
     if(module==='Pessoas'&&action==='Detalhes') { const remove=await p.getByRole('link',{name:'Remover',exact:true}).getAttribute('href');await p.goto(url+remove);check(await p.getByRole('button',{name:'Confirmar remoção'}).isVisible(),'tela de remoção de pessoa');await shot(p,'pessoa-remover'); }
     await p.goto(url+'/'+module);
   }
 }
 const pages=['/Empresas/Create','/Pessoas/Create','/Leads/Create','/Usuarios/Create','/Conta/Perfil','/Conta/AlterarSenha'];
 for(const path of pages){await p.goto(url+path);check(await p.locator('form[method=post] input[name=__RequestVerificationToken]').count()>0,'CSRF '+path);await shot(p,path.slice(1).replaceAll('/','-'));}
 await p.goto(url+'/Empresas');await p.getByRole('link',{name:'Cadastrar empresa',exact:true}).click();await p.locator('#side-panel').waitFor({state:'visible'});await p.locator('#panel-content #RazaoSocial').waitFor();
 check(await p.locator('#side-panel').getAttribute('open')!==null,'cadastro em painel Razor');
 await p.locator('#RazaoSocial').fill('Empresa visual temporária');await shot(p,'empresa-painel');
 await p.getByRole('button',{name:'Fechar painel'}).click();await p.locator('#confirm-dialog').waitFor({state:'visible'});check(await p.getByRole('heading',{name:'Descartar alterações?'}).isVisible(),'confirmação de descarte');await p.getByRole('button',{name:'Cancelar',exact:true}).click();check(await p.locator('#side-panel').isVisible(),'cancelar preserva formulário');
 await p.getByRole('button',{name:'Salvar',exact:true}).click();await p.waitForURL(/\/Empresas$/);check(await p.getByRole('status').filter({hasText:'Empresa cadastrada'}).isVisible(),'cadastro real exibe notificação');
 await p.locator('#busca').fill('Empresa visual temporária');await p.getByRole('button',{name:'Filtrar',exact:true}).click();await p.waitForLoadState('load');check(await p.locator('tbody tr').count()===1,'pesquisa real servidor');
 await p.getByRole('link',{name:'Editar',exact:true}).click();await p.locator('#panel-content #RazaoSocial').waitFor();await p.locator('#RazaoSocial').fill('Empresa visual alterada');await p.getByRole('button',{name:'Salvar alterações'}).click();await p.waitForURL(/\/Empresas$/);check(await p.getByRole('status').filter({hasText:'atualizada'}).isVisible(),'edição real no painel');
 await p.goto(url+'/Pessoas');await p.getByRole('link',{name:'Cadastrar pessoa',exact:true}).click();await p.locator('#panel-content #Nome').waitFor();await p.locator('#Nome').fill('Contato visual temporário');await p.locator('#Email').fill('visual-temporario@example.test');await p.getByRole('button',{name:'Salvar',exact:true}).click();await p.waitForURL(/\/Pessoas$/);check(await p.getByText('Contato visual temporário',{exact:true}).isVisible(),'cadastro real pessoa');
 await p.locator('tr').filter({hasText:'Contato visual temporário'}).getByRole('link',{name:'Detalhes',exact:true}).click();await p.locator('#panel-content .detail-list').waitFor();await p.getByRole('button',{name:'Bloquear',exact:true}).click();await p.waitForURL(/\/Pessoas\/Details/);check(await p.getByRole('button',{name:'Desbloquear',exact:true}).isVisible(),'bloqueio real');
 await p.getByRole('button',{name:'Desbloquear',exact:true}).click();await p.waitForLoadState('load');check(await p.getByRole('button',{name:'Bloquear',exact:true}).isVisible(),'desbloqueio real');
 await p.goto(url+'/Leads');await p.locator('tbody tr').first().getByRole('link',{name:'Revisar',exact:true}).click();await p.locator('#panel-content #revisao').waitFor();await shot(p,'lead-revisao-painel');await p.locator('input[name=decisao][value=QUALIFICADO]').check();await p.locator('#comentario').fill('Conferido no teste de interface.');await p.getByRole('button',{name:'Registrar revisão',exact:true}).click();await p.locator('#confirm-dialog').waitFor({state:'visible'});await p.getByRole('button',{name:'Confirmar',exact:true}).click();await p.waitForURL(/\/Leads\/Details/);check(await p.getByRole('status').filter({hasText:'Revisão registrada'}).isVisible(),'revisão real auditada');
 await p.goto(url+'/Leads');await p.locator('input[name=selecionados]').first().check();const downloaded=p.waitForEvent('download');await p.getByRole('button',{name:'Exportar selecionados (CSV)'}).click();const download=await downloaded;check(download.suggestedFilename()==='leads.csv','download CSV real');
 await p.goto(url+'/Importacao');await p.locator('#tipo').selectOption('pessoas');await p.locator('#arquivo').setInputFiles({name:'teste.csv',mimeType:'text/csv',buffer:Buffer.from('Nome;Email\nContato importado visual;visual-import@example.test')});await p.getByRole('button',{name:'Importar',exact:true}).click();await p.waitForLoadState('load');check(await p.getByText('Cadastrado.',{exact:true}).isVisible(),'upload CSV real');
 await p.goto(url+'/Empresas');await p.locator('#busca').fill('Empresa visual alterada');await p.getByRole('button',{name:'Filtrar',exact:true}).click();await p.waitForLoadState('load');await p.getByRole('link',{name:'Editar',exact:true}).click();await p.locator('#panel-content #RazaoSocial').waitFor();await p.getByRole('button',{name:'Remover empresa',exact:true}).click();await p.locator('#confirm-dialog').waitFor({state:'visible'});await p.getByRole('button',{name:'Cancelar',exact:true}).click();check(await p.locator('#side-panel').isVisible(),'remoção cancelada preserva registro');await p.getByRole('button',{name:'Remover empresa',exact:true}).click();await p.getByRole('button',{name:'Confirmar',exact:true}).click();await p.waitForURL(/\/Empresas$/);check(await p.getByRole('status').filter({hasText:'removido'}).isVisible(),'remoção lógica real');
 await p.goto(url+'/rota-inexistente');check(await p.getByRole('heading',{name:'Recurso não encontrado'}).isVisible(),'404 visual com status correto');await shot(p,'erro-404');
 await c.close();
 const {c:mobile,p:m}=await setup(390,844);await m.goto(url+'/Conta/Login');await shot(m,'login-mobile');await noOverflow(m,'login mobile');await login(m,'usuario');
 await m.getByRole('button',{name:'Abrir menu'}).click();check(await m.locator('.app-sidebar').isVisible(),'menu mobile abre');await shot(m,'menu-mobile');await m.keyboard.press('Escape');check(!await m.locator('.app-sidebar').isVisible(),'Escape fecha menu mobile');
 for(const module of ['Home','Empresas','Pessoas','Leads','Importacao']){await m.goto(url+'/'+module);await noOverflow(m,module+' mobile');await shot(m,module.toLowerCase()+'-mobile');}
 await m.goto(url+'/Empresas');await m.getByRole('link',{name:'Cadastrar empresa',exact:true}).click();await m.locator('#panel-content #RazaoSocial').waitFor();await noOverflow(m,'painel mobile');await shot(m,'empresa-painel-mobile');await m.getByRole('button',{name:'Fechar painel'}).click();
 await m.goto(url+'/Leads');check(await m.getByRole('link',{name:'Revisar',exact:true}).count()===0,'usuário sem ação de revisão');await m.goto(url+'/Usuarios');check(await m.getByRole('heading',{name:'Acesso negado'}).isVisible(),'autorização administrativa preservada');await shot(m,'acesso-negado-mobile');await mobile.close();
 const {c:reviewer,p:r}=await setup();await login(r,'revisor');await r.goto(url+'/Leads');check(await r.getByRole('link',{name:'Revisar',exact:true}).count()>0,'revisor tem revisão');check(await r.getByRole('link',{name:'Cadastrar lead',exact:true}).count()===0,'revisor sem cadastro');await reviewer.close();
 check(errors.length===0,'sem erros JavaScript: '+errors.join('; '));check(failed.length===0,'recursos locais carregados: '+failed.join('; '));
 await writeFile('artifacts/ui/results.json',JSON.stringify({assertions,screens,errors,failed},null,2));console.log(`UI SUCESSO: ${assertions} verificações; ${screens.length} capturas.`);
}finally{await browser.close();}
