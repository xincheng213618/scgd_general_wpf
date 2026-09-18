// Pure rendering / interaction state tests. No browser or layout engine is used.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';

// Standalone by default; set CODE_HISTORY_ARTIFACT to also exercise a real report.
const files = [
  {path:'UI/App/Views/Page.cs',project:'project:UI/App',area:'UI',language:'C#',files:1,code:100,comments:10,blank:5,lines:115},
  {path:'UI/App/App.csproj',project:'project:UI/App',area:'UI',language:'MSBuild',files:1,code:10,comments:0,blank:0,lines:10},
  {path:'Engine/Core/Core.cs',project:'project:Engine/Core',area:'Engine',language:'C#',files:1,code:50,comments:5,blank:2,lines:57},
];
const projects = [
  {id:'project:UI/App',name:'App',directory:'UI/App',kind:'工程目录',definitions:['App.csproj'],area:'UI',files:2,code:110,comments:10,blank:5,lines:125},
  {id:'project:Engine/Core',name:'Core',directory:'Engine/Core',kind:'工程目录',definitions:['Core.csproj'],area:'Engine',files:1,code:50,comments:5,blank:2,lines:57},
];
const commits = ['2026-09-01','2026-09-07','2026-09-13','2026-09-18'].map((date,i)=>({
  date,timestamp:date+'T12:00:00+08:00',hour:12,weekday:[1,0,6,4][i],commit:String(i).repeat(40),author:'Test',
  subject:'fix: fixture',category:'缺陷修复',area:'UI',areas:{UI:14},files:1,added:10,deleted:4,net:6,churn:14,
}));
const weeks = ['2026-08-31','2026-09-07','2026-09-14'].map(week_start=>({week_start,code_lines:160,
  added_lines:10,deleted_lines:4,net_growth:6,commits:1,week_status:'完整周'}));
const fixture = process.env.CODE_HISTORY_ARTIFACT
  ? JSON.parse(fs.readFileSync(process.env.CODE_HISTORY_ARTIFACT,'utf8')).analysis
  : {worktree:{files,projects,languages:[{language:'C#',code:150},{language:'MSBuild',code:10}],
      total:{files:3,code:160,comments:15,blank:7,lines:182},project_count:2,project_definition_count:2,skipped:0,
      root:'fixture',scannedAt:'2026-09-18T12:00:00Z'},
    history:{commits,weeks,ref:'HEAD',head:'abc12345',branch:'test',first:'2026-09-01',last:'2026-09-18',
      generatedAt:'2026-09-18T12:00:00Z',recent_week:{start:'2026-09-07',end:'2026-09-13',available:true,churn:28,net:12,added:20,deleted:8,commits:2,active_days:2},
      previous_week:{start:'2026-08-31',end:'2026-09-06',available:false,churn:14,net:6,added:10,deleted:4,commits:1,active_days:1}},
    summary:{latest_weekly_churn:21,latest_complete_weeks:2,commits:4}};
const script = fs.readFileSync(new URL('../code_history_dashboard/dashboard.js', import.meta.url), 'utf8');
function app() {
  const nodes = new Map();
  let csvBlob;
  const element = id => {
    if (!nodes.has(id)) nodes.set(id, {textContent:'', innerHTML:'', classList:{add(){},toggle(){}},setAttribute(){},focus(){},setSelectionRange(){}});
    return nodes.get(id);
  };
  element('dashboard-data').textContent = JSON.stringify(fixture);
  const events = {};
  const context = vm.createContext({console,Date,Intl,Map,Set,JSON,Math,Number,String,Array,Object,Blob,
    URL:{createObjectURL(blob){csvBlob=blob;return 'blob:test';},revokeObjectURL(){}},setTimeout(){},
    localStorage:{getItem(){return null;}},
    window:{scrollTo(){},scrollY:0},
    document:{getElementById:element,querySelectorAll(){return[];},createElement(){return{click(){}};},addEventListener(name,fn){events[name]=fn;},body:{classList:{add(){},toggle(){}}}},
  });
  vm.runInContext(script,context);
  return {run:code=>vm.runInContext(code,context),element,events,exportText:()=>csvBlob.text()};
}

test('all four views render real data without exceptions or non-finite values',()=>{
  const a=app();
  for(const view of ['overview','projects','activity','methods']) {
    const html=a.run(`S.view='${view}';render();document.getElementById('content').innerHTML`);
    assert.ok(html.length>500);
    assert.doesNotMatch(html,/NaN|Infinity|undefined/);
  }
});
test('each project and its subdirectories can be opened, and file totals reconcile',()=>{
  const a=app();
  assert.equal(a.run(`W.projects.every(p=>W.files.filter(f=>f.project===p.id).reduce((n,f)=>n+f.code,0)===p.code)`),true);
  assert.equal(a.run(`W.projects.every(p=>{S.project=p.id;S.directory=null;S.page=0;return projectDetail().includes('目录与文件')})`),true);
  const nested=a.run(`const f=W.files.find(f=>f.path.split('/').length>3);S.project=f.project;S.directory=f.path.slice(0,f.path.lastIndexOf('/')+1);projectDetail()`);
  assert.match(nested,/目录与文件/);
});
test('project filters, empty results and size sort use the same selected population',()=>{
  const a=app();
  assert.ok(a.run(`S.area='UI';projectRows().every(p=>p.area==='UI')`));
  assert.equal(a.run(`S.query='not-a-real-project-xyz';projectRows().length`),0);
  assert.match(a.run(`S.project='';projects()`),/没有匹配的项目/);
  assert.equal(a.run(`S.query='';S.area='';S.sort='files';const r=projectRows();r.every((v,i)=>!i||r[i-1].files>=v.files)`),true);
});
test('day/week/month aggregations reconcile to selected commits and diff totals',()=>{
  const a=app();
  for(const grain of ['日','周','月','半年','年']){
    assert.equal(a.run(`S.grain='${grain}';sum(activityPeriods(activityRows()),'commits')===activityRows().length`),true);
    assert.equal(a.run(`sum(activityPeriods(activityRows()),'net')===sum(activityRows(),'net')`),true);
  }
});
test('empty interval, search and one-day range remain valid',()=>{
  const a=app();
  assert.doesNotMatch(a.run(`S.start=H.last;S.end=H.last;activity()`),/NaN|Infinity|undefined/);
  assert.equal(a.run(`S.commitQuery='not-a-real-subject-xyz';commitRows().length`),0);
  assert.match(a.run(`activity()`),/没有匹配的提交/);
  assert.equal(a.run(`longestStreak([{date:'2026-01-01'},{date:'2026-01-02'},{date:'2026-01-02'},{date:'2026-01-04'}])`),2);
  assert.equal(a.run(`compare(12,0)`),'上周为 0，不计算增幅');
});
test('untrusted text is escaped in rendered tables',()=>{
  const a=app();
  assert.equal(a.run(`esc('<img src=x onerror=alert(1)>')`),'&lt;img src=x onerror=alert(1)&gt;');
});
test('CSV export follows filters and escapes spreadsheet formula text',async()=>{
  const a=app();
  a.run(`S.view='projects';S.area='UI';projects();exportCsv()`);
  const csv=await a.exportText();
  assert.match(csv,/UI/);
  assert.doesNotMatch(csv,/Engine\/Core/);
  a.run(`exported=[{name:'=SUM(1,2)',net:-12,note:'hello,"world"'}];exportCsv()`);
  assert.equal(await a.exportText(),`"name","net","note"\r\n"'=SUM(1,2)","-12","hello,""world"""`);
});
