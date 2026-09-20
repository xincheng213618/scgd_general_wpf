/* The generated page contains its data, styles and script. No network required. */
"use strict";
const D = JSON.parse(document.getElementById("dashboard-data").textContent);
const W = D.worktree, H = D.history;
const $ = id => document.getElementById(id);
const esc = value => String(value ?? "").replace(/[&<>"']/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c]));
const num = value => Number(value ?? 0).toLocaleString("zh-CN", {maximumFractionDigits: 0});
const compact = value => Math.abs(value) >= 10000 ? (value / 10000).toFixed(1) + "万" : num(value);
const signed = value => (value > 0 ? "+" : "") + num(value);
const pct = (a,b) => b ? (a/b*100).toFixed(1) + "%" : "—";
const sum = (rows, key) => rows.reduce((n,r) => n + Number(r[key] || 0), 0);
const parseDay = day => new Date(day + "T00:00:00Z");
const shiftDay = (day, delta) => new Date(parseDay(day).getTime() + delta*86400000).toISOString().slice(0,10);
const weekdays = ["周一","周二","周三","周四","周五","周六","周日"];
const palette = ["#6377e7","#42a995","#8f80cd","#dea259","#dc8293","#74a6cc","#abb3c7"];
const S = {view:"overview", project:"", directory:null, area:"", query:"", sort:"code", page:0,
  start:shiftDay(H.last,-89), end:H.last, preset:"90", grain:"周", day:"", commitQuery:"", commitPage:0};
if (S.start < H.first) S.start = H.first;
let exported = [];

function aggregate(rows, key) {
  const map = new Map();
  for (const r of rows) {
    const k = r[key];
    if (!map.has(k)) map.set(k,{name:k,files:0,code:0,comments:0,blank:0,lines:0,churn:0,commits:0});
    const a = map.get(k);
    for (const metric of ["files","code","comments","blank","lines","churn"]) a[metric] += r[metric] || 0;
    a.commits++;
  }
  return [...map.values()];
}
function metric(label, value, unit, note, badge="") {
  return `<article class="metric"><div class="metric-label">${label}${badge?`<span>${badge}</span>`:""}</div><div class="metric-value">${value}<span class="metric-unit">${unit}</span></div><div class="metric-note">${note}</div></article>`;
}
function panel(title, subtitle, content, extra="") {
  return `<section class="panel"><div class="panel-head"><div><h2>${title}</h2>${subtitle?`<p>${subtitle}</p>`:""}</div>${extra}</div>${content}</section>`;
}
function empty(text="此范围没有数据") { return `<div class="empty">${esc(text)}</div>`; }
function compare(current, previous, available=true) {
  if (!available) return "缺少完整上周，暂不比较";
  if (previous === 0) return current === 0 ? "与上周持平" : "上周为 0，不计算增幅";
  const change=(current-previous)/Math.abs(previous)*100;
  return `较上周 ${change>0?"+":""}${change.toFixed(1)}%`;
}
function bars(rows, key="code", limit=8) {
  const items=[...rows].sort((a,b)=>b[key]-a[key]).slice(0,limit);
  const max=Math.max(1,...items.map(r=>r[key]));
  return items.length ? `<div class="mini-rows">${items.map((r,i)=>`<div class="mini-row"><span class="label" title="${esc(r.name)}">${esc(r.name)}</span><div class="track"><div class="fill" style="width:${r[key]/max*100}%;background:${palette[i%palette.length]}"></div></div><span class="num" title="${num(r[key])}">${compact(r[key])}</span></div>`).join("")}</div>` : empty();
}
function legend(series) {
  return `<div class="legend">${series.map(s=>`<span><i style="background:${s.color}"></i>${esc(s.label)}</span>`).join("")}</div>`;
}
function lineChart(rows, xKey, series, {height=220, area=false}={}) {
  if (!rows.length) return empty();
  const width=840, left=62, right=20, top=15, bottom=34;
  const dates=rows.map(r=>parseDay(r[xKey]).getTime());
  const first=Math.min(...dates), last=Math.max(...dates);
  const vals=rows.flatMap(r=>series.map(s=>Number(r[s.key] || 0)));
  const low=Math.min(0,...vals), high=Math.max(1,...vals), span=high-low;
  const x=i=>left+(dates[i]-first)/(last-first || 1)*(width-left-right);
  const y=v=>top+(high-v)/span*(height-top-bottom);
  let svg=`<svg class="chart" viewBox="0 0 ${width} ${height}" role="img" aria-label="${esc(series.map(s=>s.label).join('、'))}趋势">`;
  for(let i=0;i<5;i++) {const v=low+span*i/4;svg+=`<line class="gridline" x1="${left}" x2="${width-right}" y1="${y(v)}" y2="${y(v)}"/><text x="${left-10}" y="${y(v)+4}" text-anchor="end">${compact(v)}</text>`;}
  const marks=[...new Set(Array.from({length:Math.min(5,rows.length)},(_,i)=>Math.round(i*(rows.length-1)/Math.max(1,Math.min(5,rows.length)-1))))];
  marks.forEach(i=>svg+=`<text x="${x(i)}" y="${height-7}" text-anchor="${i===0?'start':i===rows.length-1?'end':'middle'}">${esc(rows[i][xKey])}</text>`);
  series.forEach((s,si)=>{
    const points=rows.map((r,i)=>`${x(i)},${y(r[s.key]||0)}`).join(" ");
    if(area && si===0) svg+=`<polygon points="${x(0)},${y(0)} ${points} ${x(rows.length-1)},${y(0)}" fill="${s.color}" opacity=".09"/>`;
    svg+=`<polyline points="${points}" fill="none" stroke="${s.color}" stroke-width="2.4" stroke-linejoin="round"/>`;
    rows.forEach((r,i)=>svg+=`<circle cx="${x(i)}" cy="${y(r[s.key]||0)}" r="${rows.length>150?2:3}" fill="${s.color}" tabindex="0"><title>${esc(r[xKey])} · ${esc(s.label)} ${num(r[s.key])}</title></circle>`);
  });
  return legend(series)+'<div class="chart-scroll">'+svg+"</svg></div>";
}
function columnChart(values, labels, color=palette[0]) {
  const width=760,height=185,left=40,top=15,bottom=28,max=Math.max(1,...values),cw=(width-left-10)/values.length;
  let out=`<svg class="chart" viewBox="0 0 ${width} ${height}" role="img" aria-label="提交数量分布">`;
  for(let i=0;i<=3;i++){const v=max*i/3,y=height-bottom-v/max*(height-top-bottom);out+=`<line class="gridline" x1="${left}" x2="750" y1="${y}" y2="${y}"/><text x="${left-8}" y="${y+4}" text-anchor="end">${num(v)}</text>`;}
  values.forEach((v,i)=>{const h=v/max*(height-top-bottom),x=left+cw*i+cw*.18;out+=`<rect x="${x}" y="${height-bottom-h}" width="${cw*.64}" height="${h}" rx="3" fill="${color}" tabindex="0"><title>${esc(labels[i])} · ${num(v)} 次提交</title></rect><text x="${x+cw*.32}" y="${height-6}" text-anchor="middle">${labels.length>12&&i%3?'':esc(labels[i])}</text>`;});
  return '<div class="chart-scroll">'+out+"</svg></div>";
}
function overview() {
  const r=H.recent_week,p=H.previous_week, total=W.total;
  const recent=r.available, comparison=p.available && recent;
  const sizes=H.weeks, changes=sizes.slice(-16);
  const weekTitle=recent?`${r.start} — ${r.end}`:"历史不足一个完整自然周";
  const avg=D.summary.latest_weekly_churn;
  exported=H.weeks;
  let out=`<div class="metrics">${metric("工作区代码 / 内容",num(total.code),"行",`${num(total.files)} 个文件 · 含未提交修改`,"当前磁盘")}${metric("工程目录",num(W.project_count),"个",`${num(W.project_definition_count)} 个工程定义 · 可逐层查看`)}${metric("最近完整周净增长",recent?signed(r.net):"—","行",recent?compare(r.net,p.net,comparison):weekTitle)}${metric("最近完整周提交",recent?num(r.commits):"—","次",recent?`${r.active_days} 个活跃日 · ${compare(r.commits,p.commits,comparison)}`:weekTitle)}</div>`;
  const scale=lineChart(sizes,"week_start",[{key:"code_lines",label:"代码 / 内容行",color:palette[0]}],{area:true});
  const max=Math.max(1,r.churn,p.churn,avg);
  const comparisonRows=[{name:"最近完整周",value:r.churn,available:recent,color:palette[0]},{name:"上一完整周",value:p.churn,available:p.available,color:palette[2]},{name:`近 ${D.summary.latest_complete_weeks} 周均值`,value:avg,available:D.summary.latest_complete_weeks>0,color:palette[5]}];
  const weekly=`<div class="comparison">${comparisonRows.map(v=>`<div class="compare-row"><span class="compare-label">${v.name}</span><div class="track"><div class="fill" style="width:${v.available?v.value/max*100:0}%;background:${v.color}"></div></div><span class="compare-value">${v.available?compact(v.value):"—"}</span></div>`).join("")}</div><div class="stat-list"><div><span>新增行</span><b class="positive">${recent?num(r.added):"—"}</b></div><div><span>删除行</span><b class="negative">${recent?num(r.deleted):"—"}</b></div><div><span>净增长</span><b>${recent?signed(r.net):"—"}</b></div><div><span>自然日均变更</span><b>${recent?num(r.churn/7):"—"}</b></div></div>${recent&&avg?`<div class="callout">最近完整周总变更为近 ${D.summary.latest_complete_weeks} 周均值的 <strong>${pct(r.churn,avg)}</strong>。总变更衡量文本改动，净增长衡量规模变化。</div>`:""}`;
  out+=`<div class="grid">${panel("代码规模的演进","Git 精确周快照 · 不含未提交修改",scale,`<span class="tag">${num(D.summary.commits)} 个提交节点</span>`)}${panel("最近一周，变化有多大？",weekTitle,weekly)}</div>`;
  out+=`<div class="grid equal">${panel("近期新增与删除","最近 16 个自然周 · 最后一期可能尚未结束",lineChart(changes,"week_start",[{key:"added_lines",label:"新增",color:palette[1]},{key:"deleted_lines",label:"删除",color:palette[4]}]))}${panel("近期净增长","新增 − 删除 · 负值表示文本规模收缩",lineChart(changes,"week_start",[{key:"net_growth",label:"净增长",color:palette[0]}]))}</div>`;
  out+=`<div class="grid equal">${panel("当前目录规模","工作区代码 / 内容行 · 前 8 个目录",bars(aggregate(W.files,"area")))}${panel("当前语言构成","工作区代码 / 内容行 · 前 8 种语言",bars(W.languages.map(x=>({...x,name:x.language}))))}</div>`;
  out+=panel("历史周期明细","导出按钮可导出全部周快照",`<details><summary>展开最近 16 周的精确数据</summary><div class="table-wrap"><table><thead><tr><th>自然周</th><th>状态</th><th class="num">代码 / 内容行</th><th class="num">新增</th><th class="num">删除</th><th class="num">净增长</th><th class="num">提交</th></tr></thead><tbody>${[...changes].reverse().map(w=>`<tr><td>${esc(w.week_start)}</td><td>${esc(w.week_status)}</td><td class="num">${num(w.code_lines)}</td><td class="num positive">${num(w.added_lines)}</td><td class="num negative">${num(w.deleted_lines)}</td><td class="num">${signed(w.net_growth)}</td><td class="num">${num(w.commits)}</td></tr>`).join("")}</tbody></table></div></details>`);
  out+=panel("历史变化节点","保留长期变化线索，按需展开；同期版本记录不代表因果关系。",`<details><summary>规模跃变 · ${(D.scale_jumps||[]).length} 个节点</summary><div class="table-wrap"><table><thead><tr><th>周起始</th><th class="num">之前代码行</th><th class="num">之后代码行</th><th class="num">变化</th><th>版本背景</th></tr></thead><tbody>${(D.scale_jumps||[]).map(r=>`<tr><td>${esc(r.jump_date)}</td><td class="num">${num(r.before_code_lines)}</td><td class="num">${num(r.after_code_lines)}</td><td class="num">${signed(r.code_delta)}</td><td>${esc(r.release_context)}</td></tr>`).join('')}</tbody></table></div></details><details><summary>持续节奏变化 · ${(D.change_points||[]).length} 个节点</summary><p class="hint">前后各 8 个完整周的周变更中位样本值至少相差 1.6 倍，候选间隔至少 12 周，最多展示 5 个。</p><div class="table-wrap"><table><thead><tr><th>周起始</th><th>方向</th><th class="num">之前周变更</th><th class="num">之后周变更</th><th>版本背景</th></tr></thead><tbody>${(D.change_points||[]).map(r=>`<tr><td>${esc(r.change_date)}</td><td>${esc(r.direction)}</td><td class="num">${num(r.before_weekly_churn)}</td><td class="num">${num(r.after_weekly_churn)}</td><td>${esc(r.release_context)}</td></tr>`).join('')}</tbody></table></div></details>`);
  return out;
}

function projectRows() {
  return W.projects.filter(p=>(!S.area||p.area===S.area)&&(!S.query||`${p.name} ${p.directory}`.toLowerCase().includes(S.query.toLowerCase()))).sort((a,b)=>b[S.sort]-a[S.sort]||a.name.localeCompare(b.name));
}
function tableNumbers(row,total) {
  return `<td class="num">${num(row.files)}</td><td class="num"><strong>${num(row.code)}</strong></td><td class="num">${num(row.comments)}</td><td class="num">${num(row.blank)}</td><td class="num">${num(row.lines)}</td><td><div class="share-bar"><div class="track"><div class="fill" style="width:${total?row.code/total*100:0}%"></div></div><span>${pct(row.code,total)}</span></div></td>`;
}
function inventoryHead(label="项目 / 目录") {
  return `<thead><tr><th>${label}</th><th class="num">文件</th><th class="num">代码 / 内容</th><th class="num">注释</th><th class="num">空行</th><th class="num">物理行</th><th>代码占比</th></tr></thead>`;
}
function pagination(page,count,size,scope) {
  const pages=Math.max(1,Math.ceil(count/size));
  return `<div class="pagination"><span class="count">共 ${num(count)} 项</span><button class="button" data-page="${scope}" data-step="-1" ${page===0?"disabled":""}>上一页</button><span>${page+1} / ${pages}</span><button class="button" data-page="${scope}" data-step="1" ${page+1>=pages?"disabled":""}>下一页</button></div>`;
}
function composition(rows) {
  const total=sum(rows,"lines"),code=sum(rows,"code"),comments=sum(rows,"comments"),blank=sum(rows,"blank");
  const parts=[{label:"代码 / 内容",value:code,color:palette[0]},{label:"纯注释",value:comments,color:palette[1]},{label:"空行",value:blank,color:palette[6]}];
  return `<div class="distribution">${parts.map(p=>`<span title="${p.label} ${num(p.value)}" style="width:${total?p.value/total*100:0}%;background:${p.color}"></span>`).join("")}</div>${legend(parts.map(p=>({...p,label:`${p.label} ${pct(p.value,total)}`})))}<div class="inline-stats"><span>物理行<strong>${num(total)}</strong></span><span>平均每文件<strong>${rows.length?num(total/rows.length):"—"} 行</strong></span></div>`;
}
function projects() {
  if(S.project) return projectDetail();
  const rows=projectRows(),ids=new Set(rows.map(r=>r.id)),files=W.files.filter(f=>ids.has(f.project));
  exported=rows;
  let out=`<div class="toolbar"><input id="project-search" type="search" placeholder="搜索项目名或目录路径" aria-label="搜索项目" value="${esc(S.query)}"><label>目录<select id="area-filter"><option value="">所有目录</option>${[...new Set(W.projects.map(p=>p.area))].sort().map(a=>`<option ${a===S.area?"selected":""}>${esc(a)}</option>`).join("")}</select></label><label>排序<select id="project-sort"><option value="code" ${S.sort==='code'?'selected':''}>代码 / 内容行</option><option value="lines" ${S.sort==='lines'?'selected':''}>物理行</option><option value="files" ${S.sort==='files'?'selected':''}>文件数</option></select></label><span class="hint">工作区快照 · 不受历史日期筛选影响</span></div>`;
  out+=`<div class="metrics">${metric("筛选后的代码 / 内容",num(sum(rows,"code")),"行",`占整个工作区 ${pct(sum(rows,'code'),W.total.code)}`)}${metric("物理文本",num(sum(rows,"lines")),"行","代码 / 内容 + 注释 + 空行")}${metric("识别文件",num(files.length),"个",`${rows.length} 个项目 / 目录分组`)}${metric("千行以上文件",num(files.filter(f=>f.code>=1000).length),"个","按代码 / 内容行 ≥ 1,000 统计")}</div>`;
  out+=`<div class="grid equal">${panel("项目规模分布","当前筛选 · 前 8 个项目 / 目录",bars(rows))}${panel("文本结构","当前筛选下的物理行构成",composition(files))}</div>`;
  const page=rows.slice(S.page*20,S.page*20+20);
  out+=panel(`项目与目录<span class="badge-number">${rows.length}</span>`,"点击项目查看目录、文件和语言；按物理位置归属，不重复计算共享文件。",rows.length?`<div class="table-wrap"><table>${inventoryHead()}<tbody>${page.map(p=>`<tr><td class="project-name"><button class="table-button" data-project="${esc(p.id)}">${esc(p.name)}</button> <span class="pill">${p.kind}</span><span class="path" title="${esc(p.directory)}">${esc(p.directory)}</span></td>${tableNumbers(p,sum(rows,"code"))}</tr>`).join("")}</tbody></table></div>${pagination(S.page,rows.length,20,"projects")}`:empty("没有匹配的项目，试试其它名称或目录"));
  return out;
}
function projectFiles() {return W.files.filter(f=>f.project===S.project);}
function projectDetail() {
  const p=W.projects.find(p=>p.id===S.project),all=projectFiles();
  if(!p){S.project="";return projects();}
  const root=p.kind==="工程目录"?(p.directory==="."?"":p.directory+"/"):(p.directory==="根目录"?"":p.directory+"/");
  const prefix=S.directory===null?root:S.directory;
  const files=all.filter(f=>f.path.startsWith(prefix));
  exported=files;
  const direct=new Map();
  files.forEach(f=>{
    const rest=f.path.slice(prefix.length),part=rest.split("/")[0],folder=rest.includes("/");
    const key=part+(folder?"/":"");
    if(!direct.has(key)) direct.set(key,{name:key,path:prefix+key,folder,files:0,code:0,comments:0,blank:0,lines:0});
    const row=direct.get(key); for(const k of ["files","code","comments","blank","lines"]) row[k]+=f[k];
  });
  const items=[...direct.values()].sort((a,b)=>Number(b.folder)-Number(a.folder)||b.code-a.code);
  const chunks=prefix.slice(root.length).split("/").filter(Boolean);
  let trail=`<button data-back-projects>所有项目</button><span>/</span><button data-directory="${esc(root)}">${esc(p.name)}</button>`;
  chunks.forEach((part,i)=>trail+=`<span>/</span><button data-directory="${esc(root+chunks.slice(0,i+1).join('/')+'/')}">${esc(part)}</button>`);
  let out=`<div class="breadcrumbs">${trail}</div><div class="metrics">${metric("当前目录代码 / 内容",num(sum(files,"code")),"行",esc(prefix||"仓库根目录"))}${metric("物理文本",num(sum(files,"lines")),"行",`注释 ${num(sum(files,'comments'))} · 空行 ${num(sum(files,'blank'))}`)}${metric("当前目录文件",num(files.length),"个",`${new Set(files.map(f=>f.language)).size} 种语言 / 文本类型`)}${metric("占项目代码规模",pct(sum(files,"code"),p.code),"",`项目总量 ${num(p.code)} 行`)}</div>`;
  out+=`<div class="grid equal">${panel("语言构成","当前目录及其子目录 · 前 8 种语言",bars(aggregate(files,"language")))}${panel("文本结构","当前目录及其子目录",composition(files))}</div>`;
  out+=panel("目录与文件",`${esc(p.directory)} · ${p.definitions.length?esc(p.definitions.join('、')):'无工程定义，按独立目录分组'}`,`<div class="table-wrap"><table>${inventoryHead("名称")}<tbody>${items.slice(S.page*30,S.page*30+30).map(f=>`<tr><td class="project-name">${f.folder?`<button class="table-button" data-directory="${esc(f.path)}">${esc(f.name)}</button> <span class="pill">目录</span>`:`<span title="${esc(f.path)}">${esc(f.name)}</span>`}</td>${tableNumbers(f,sum(files,"code"))}</tr>`).join("")}</tbody></table></div>${pagination(S.page,items.length,30,"directories")}`);
  const largest=[...files].sort((a,b)=>b.code-a.code).slice(0,10);
  out+=panel("较大的文件","当前目录内代码 / 内容行最多的 10 个文件",`<div class="table-wrap"><table><thead><tr><th>文件路径</th><th>语言</th><th class="num">代码 / 内容</th><th class="num">物理行</th></tr></thead><tbody>${largest.map(f=>`<tr><td class="detail-title">${esc(f.path)}</td><td>${esc(f.language)}</td><td class="num">${num(f.code)}</td><td class="num">${num(f.lines)}</td></tr>`).join("")}</tbody></table></div>`);
  return out;
}

function activityRows() {return H.commits.filter(c=>c.date>=S.start&&c.date<=S.end);}
function longestStreak(rows) {
  const days=[...new Set(rows.map(c=>c.date))].sort();let longest=0,current=0,previous="";
  for(const d of days){current=previous&&shiftDay(previous,1)===d?current+1:1;longest=Math.max(longest,current);previous=d;}return longest;
}
function activityPeriods(rows) {
  const bucket=day=>{
    if(S.grain==='周')return shiftDay(day,-((parseDay(day).getUTCDay()+6)%7));
    if(S.grain==='月')return day.slice(0,7)+'-01';
    if(S.grain==='半年')return day.slice(0,4)+(Number(day.slice(5,7))<=6?'-01-01':'-07-01');
    if(S.grain==='年')return day.slice(0,4)+'-01-01';
    return day;
  };
  const map=new Map(),start=bucket(S.start);
  const next=day=>{
    const d=parseDay(day),months={'月':1,'半年':6,'年':12}[S.grain];
    return months?new Date(Date.UTC(d.getUTCFullYear(),d.getUTCMonth()+months,1)).toISOString().slice(0,10):shiftDay(day,S.grain==='周'?7:1);
  };
  for(let d=start;d<=S.end;d=next(d)) map.set(d,{date:d,commits:0,added:0,deleted:0,net:0});
  for(const row of rows){const p=map.get(bucket(row.date));if(p){p.commits++;p.added+=row.added;p.deleted+=row.deleted;p.net+=row.net;}}
  return [...map.values()];
}
function calendar(rows) {
  const counts=new Map();rows.forEach(c=>counts.set(c.date,(counts.get(c.date)||0)+1));
  let start=shiftDay(S.end,-364);if(start<S.start)start=S.start;
  const visibleStart=start;start=shiftDay(start,-((parseDay(start).getUTCDay()+6)%7));
  const max=Math.max(1,...counts.values());
  let cells="";
  for(let d=start;d<=S.end;d=shiftDay(d,1)){
    const n=counts.get(d)||0, outside=d<visibleStart;
    const bg=n?`background:color-mix(in srgb, var(--accent) ${20+80*Math.sqrt(n/max)}%, var(--panel))`:"";
    cells+=`<button class="day ${outside?'outside':''}" ${outside?'disabled':''} style="${bg}" data-day="${d}" data-tip="${d} · ${n} 次" title="${d} · ${n} 次提交" aria-label="查看 ${d} 的 ${n} 次提交"></button>`;
  }
  return `<div class="calendar-scroll"><div class="calendar">${cells}</div></div><div class="calendar-caption"><span>${visibleStart} — ${S.end} · 最多显示最近 365 天</span><span>浅 → 深：提交由少到多</span></div>`;
}
function heatmap(rows) {
  const values=Array.from({length:7},()=>Array(24).fill(0));rows.forEach(c=>values[c.weekday][c.hour]++);
  const max=Math.max(1,...values.flat());let html='<div class="calendar-scroll"><div class="heatmap"><span></span>';
  for(let h=0;h<24;h++)html+=`<span class="label">${h%3===0?String(h).padStart(2,'0'):''}</span>`;
  values.forEach((row,d)=>{html+=`<span class="label">${weekdays[d]}</span>`;row.forEach((n,h)=>{const label=`${weekdays[d]} ${h}:00–${h}:59 · ${n} 次提交`;html+=`<div class="cell" tabindex="0" role="img" aria-label="${label}" title="${label}" style="${n?`background:color-mix(in srgb, var(--accent) ${20+80*Math.sqrt(n/max)}%, var(--panel))`:''}"></div>`;});});
  return html+'</div></div><p class="hint">颜色按当前范围内次数缩放；小时保留每条提交记录自带的 UTC 偏移。</p>';
}
function commitRows() {
  return activityRows().filter(c=>(!S.day||c.date===S.day)&&(!S.commitQuery||`${c.subject} ${c.author} ${c.commit} ${c.area}`.toLowerCase().includes(S.commitQuery.toLowerCase()))).sort((a,b)=>Date.parse(b.timestamp)-Date.parse(a.timestamp));
}
function activity() {
  const rows=activityRows(),hours=Array(24).fill(0),days=Array(7).fill(0);rows.forEach(r=>{hours[r.hour]++;days[r.weekday]++;});
  const active=new Set(rows.map(c=>c.date)).size,span=Math.round((parseDay(S.end)-parseDay(S.start))/86400000)+1;
  const periods=activityPeriods(rows),night=rows.filter(c=>c.hour<6).length;
  let out=`<div class="toolbar"><div class="segment">${[['30','近 30 天'],['90','近 90 天'],['365','近一年'],['all','全部历史']].map(([k,v])=>`<button data-range="${k}" class="${S.preset===k?'active':''}">${v}</button>`).join('')}</div><label>从<input id="date-start" type="date" min="${H.first}" max="${S.end}" value="${S.start}"></label><label>至<input id="date-end" type="date" min="${S.start}" max="${H.last}" value="${S.end}"></label><label>趋势粒度<select id="grain">${['日','周','月','半年','年'].map(g=>`<option ${S.grain===g?'selected':''}>${g}</option>`).join('')}</select></label></div>`;
  out+=`<div class="metrics">${metric("区间提交",num(rows.length),"次",`${S.start} — ${S.end}`)}${metric("活跃天数",num(active),"天",`${span} 个自然日 · 活跃率 ${pct(active,span)}`)}${metric("最长连续提交",num(longestStreak(rows)),"天","按当前区间的自然日计算")}${metric("区间净增长",signed(sum(rows,'net')),"行",`新增 ${compact(sum(rows,'added'))} · 删除 ${compact(sum(rows,'deleted'))}`)}</div>`;
  out+=panel("提交活跃日历","每列一周，自上至下为周一到周日 · 点击日期筛选下方提交记录",rows.length?calendar(rows):empty());
  out+=`<div class="grid equal">${panel("提交次数趋势",`${S.grain}粒度 · 首尾周期可能不完整`,lineChart(periods,'date',[{key:'commits',label:'提交次数',color:palette[0]}],{area:true}))}${panel("新增与删除",`${S.grain}粒度 · 以实际文本 diff 行数计量`,lineChart(periods,'date',[{key:'added',label:'新增行',color:palette[1]},{key:'deleted',label:'删除行',color:palette[4]}]))}</div>`;
  out+=`<div class="grid equal">${panel("一天中的提交时间","提交者时间 · 0–23 时",columnChart(hours,Array.from({length:24},(_,i)=>String(i).padStart(2,'0'))))}${panel("一周中的提交分布","按提交次数计数",columnChart(days,weekdays,palette[2]))}</div>`;
  out+=panel("星期 × 小时","更细地观察提交出现的时段",heatmap(rows)+`<div class="inline-stats"><span>周末提交<strong>${pct(days[5]+days[6],rows.length)}</strong></span><span>00:00–05:59 提交<strong>${pct(night,rows.length)}</strong></span><span>活跃日均提交<strong>${active?(rows.length/active).toFixed(1):'—'}</strong></span></div>`);
  const areaMap=new Map();rows.forEach(c=>Object.entries(c.areas).forEach(([name,churn])=>areaMap.set(name,(areaMap.get(name)||0)+churn)));
  out+=`<div class="grid equal">${panel("目录变更分布","当前时间范围 · 新增 + 删除行",bars([...areaMap].map(([name,churn])=>({name,churn})),"churn"))}${panel("提交主题","标题关键词分类 · 用于回顾，不代表精确任务分类",bars(aggregate(rows,'category'),'commits'))}</div>`;
  const records=commitRows();exported=records;
  out+=panel("提交记录",S.day?`当前查看 ${S.day} <button class="quiet" id="clear-day">清除日期筛选</button>`:"当前区间的第一父链提交",`<div class="toolbar"><input type="search" id="commit-search" aria-label="搜索提交" placeholder="搜索标题、作者、目录或哈希" value="${esc(S.commitQuery)}"><span class="hint">导出当前筛选的提交记录</span></div>${records.length?`<div class="table-wrap"><table><thead><tr><th>提交者时间</th><th>提交 / 标题</th><th>作者</th><th>主要目录</th><th class="num">新增</th><th class="num">删除</th><th class="num">净增长</th></tr></thead><tbody>${records.slice(S.commitPage*20,S.commitPage*20+20).map(c=>`<tr><td class="streak">${esc(c.timestamp.slice(0,16).replace('T',' '))}<span class="path">UTC ${esc(c.timestamp.slice(-6))}</span></td><td><span class="pill">${esc(c.commit.slice(0,8))}</span> <span class="detail-title">${esc(c.subject)}</span></td><td>${esc(c.author)}</td><td>${esc(c.area)}</td><td class="num positive">${num(c.added)}</td><td class="num negative">${num(c.deleted)}</td><td class="num">${signed(c.net)}</td></tr>`).join('')}</tbody></table></div>${pagination(S.commitPage,records.length,20,'commits')}`:empty('没有匹配的提交')}`);
  return out;
}

function methods() {
  exported=[];
  return `<div class="methods">${panel("两种数据来源，各回答不同问题","",`<table><thead><tr><th>范围</th><th>来源</th><th>包含未提交修改</th></tr></thead><tbody><tr><td>当前项目与文件规模</td><td>本次扫描磁盘上的工作区文件</td><td>是，包括未被忽略的未跟踪文件</td></tr><tr><td>历史规模、变更与提交时间</td><td>${esc(H.ref)} 的 Git 第一父链，合并按第一父差异计一次</td><td>否</td></tr></tbody></table><h3>工作区统计</h3><p>工程定义识别 .csproj、.vcxproj、.fsproj、.vbproj、.shproj。同一目录的多个定义合并为一组；每个文件归属最近的祖先工程目录。没有工程定义的文件按顶层目录归组，嵌套工程归自己。此处统计物理文件，不执行 MSBuild，不展开链接文件，也不等同于某个配置实际编译的文件列表。</p><p>已识别 ${num(W.total.files)} 个文本文件，${num(W.project_count)} 个工程目录，${num(W.project_definition_count)} 个工程定义。未识别、二进制、缺失或越界文件跳过 ${num(W.skipped)} 个。bin、obj、node_modules 等固定目录排除；${W.exclude_generated?'已开启已知生成文件后缀排除':'未开启生成文件排除，Designer 等已跟踪内容可能计入'}。</p><h3>“代码 / 内容行”是什么</h3><p>非空且不只含注释的行。包含 Markdown、JSON、XAML、工程配置等文本内容，不全部是可执行代码。纯注释和空行另列；三者之和等于物理行。采用与 count_code_lines.py 相同的字符级分类器，不是语言编译器。</p><h3>时间和比较</h3><p>提交时间使用 Git 提交者时间（%cI），保留每条记录自带的日期和 UTC 偏移；可能不同于作者原始编写时间。小时分布反映提交出现的时段，不能反推工作时长。历史范围为 ${H.first} 至 ${H.last}；近 30 / 90 / 365 天均以所选历史最后一天为终点。</p><p>“最近完整周”是历史最后日期所在周之前的周一至周日，与再前一个完整周比较。历史没有覆盖整周时显示不可比较；基数为 0 时不计算百分比。日历最多显示所选范围尾部 365 天，图表与区间指标覆盖完整选择范围。</p><h3>变更与规模</h3><p>总变更＝新增＋删除；净增长＝新增−删除。重命名按删除加新增计量。注释和空行也在 Git diff 中；每周代码 / 内容规模则重新读取周末提交快照分类。文本变更不等同于功能质量或个人产出。工作区值和 ${esc(H.ref)} 历史值来自不同快照。</p><h3>刷新和使用</h3><p>在仓库根目录运行 <code>py Scripts/generate_code_history_dashboard.py</code> 重新生成此页面。页面数据内嵌，可离线查看；主题选择保存在当前浏览器。图表点可悬停或键盘聚焦查看数值，表格和导出提供精确数据。文件路径与提交标题仅作为文本显示。</p><h3>参考</h3><p><a href="https://docs.github.com/en/repositories/viewing-activity-and-data-for-your-repository/analyzing-changes-to-a-repositorys-content" target="_blank" rel="noreferrer">GitHub：提交频率与代码变更</a> · <a href="https://git-scm.com/docs/git-log" target="_blank" rel="noreferrer">Git：历史遍历与提交时间</a></p>`)}${panel("扫描信息","",`<p>工作区：<code>${esc(W.root)}</code></p><p>Git 提交：<code>${esc(H.head)}</code></p><p>历史生成时间：${esc(H.generatedAt)}</p><p>工作区扫描完成：${esc(W.scannedAt)}</p><p>若扫描期间其它任务正在改文件，这份报告可能跨越数个写入时点。需要严格一致快照时，请在相关修改完成后重新运行。</p>`)}</div>`;
}
function render() {
  const labels={overview:['代码概览','当前规模、历史演进与最近完整周的变化'],projects:['项目规模','从工程到目录，再到每一个文件'],activity:['提交节奏','观察提交时间、活跃程度与文本变更'],methods:['统计口径','数据来源、计数规则与比较边界']};
  $('view-title').textContent=labels[S.view][0];$('view-subtitle').textContent=labels[S.view][1];
  document.querySelectorAll('[data-view]').forEach(b=>{b.classList.toggle('active',b.dataset.view===S.view);b.setAttribute('aria-current',b.dataset.view===S.view?'page':'false');});
  $('content').innerHTML=({overview,projects,activity,methods})[S.view]();
  $('export').disabled=!exported.length;
  $('viz-tooltip').hidden=true;
}
// Native SVG title elements remain available; this tooltip also supports keyboard focus.
function showVizTooltip(event) {
  const target=event.target.closest('.chart circle, .chart rect, .heatmap .cell');
  if(!target)return;
  const title=target.querySelector('title')?.textContent || target.getAttribute('aria-label');
  if(!title)return;
  const tooltip=$('viz-tooltip'),box=target.getBoundingClientRect();
  tooltip.textContent=title;tooltip.hidden=false;
  tooltip.style.left=Math.max(8,Math.min(box.left,window.innerWidth-310))+'px';
  tooltip.style.top=Math.max(8,box.top-42)+'px';
}
document.addEventListener('pointerover',showVizTooltip);
document.addEventListener('focusin',showVizTooltip);
document.addEventListener('pointerout',()=>{$('viz-tooltip').hidden=true;});
document.addEventListener('focusout',()=>{$('viz-tooltip').hidden=true;});
function exportCsv() {
  if(!exported.length)return;
  const keys=Object.keys(exported[0]).filter(k=>typeof exported[0][k]!=='object');
  const cell=v=>{let text=String(v??'');if(typeof v==='string'&&/^[\s]*[=+\-@]/.test(text))text="'"+text;return '"'+text.replace(/"/g,'""')+'"';};
  const csv='\ufeff'+[keys,...exported.map(r=>keys.map(k=>r[k]))].map(row=>row.map(cell).join(',')).join('\r\n');
  const url=URL.createObjectURL(new Blob([csv],{type:'text/csv;charset=utf-8'}));const a=document.createElement('a');a.href=url;a.download=`colorvision-${S.view}-${H.last}.csv`;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
}
document.addEventListener('click',event=>{
  const b=event.target.closest('button');if(!b||b.disabled)return;
  if(b.dataset.view){S.view=b.dataset.view;render();window.scrollTo(0,0);}
  else if(b.dataset.project){S.project=b.dataset.project;S.directory=null;S.page=0;render();window.scrollTo(0,0);}
  else if(b.hasAttribute('data-back-projects')){S.project='';S.directory=null;S.page=0;render();}
  else if(b.hasAttribute('data-directory')){S.directory=b.dataset.directory;S.page=0;render();}
  else if(b.dataset.range){S.preset=b.dataset.range;S.end=H.last;S.start=S.preset==='all'?H.first:shiftDay(H.last,1-Number(S.preset));if(S.start<H.first)S.start=H.first;S.day='';S.commitPage=0;render();}
  else if(b.dataset.day){S.day=b.dataset.day;S.commitPage=0;render();$('commit-search').scrollIntoView({block:'center',behavior:'smooth'});}
  else if(b.id==='clear-day'){S.day='';S.commitPage=0;render();$('commit-search').scrollIntoView({block:'center'});}
  else if(b.dataset.page){if(b.dataset.page==='commits')S.commitPage+=Number(b.dataset.step);else S.page+=Number(b.dataset.step);const y=window.scrollY;render();window.scrollTo(0,y);}
  else if(b.id==='export')exportCsv();
  else if(b.id==='theme-toggle'){document.body.classList.toggle('dark');try{localStorage.setItem('colorvision-history-theme',document.body.classList.contains('dark')?'dark':'light');}catch{}}
});
document.addEventListener('change',event=>{
  const el=event.target;
  if(el.id==='area-filter'){S.area=el.value;S.page=0;}
  else if(el.id==='project-sort'){S.sort=el.value;S.page=0;}
  else if(el.id==='date-start'||el.id==='date-end'){
    if(!el.value||el.value<H.first||el.value>H.last){render();return;}
    if(el.id==='date-start')S.start=el.value;else S.end=el.value;
    if(S.start>S.end){if(el.id==='date-start')S.end=S.start;else S.start=S.end;}
    S.preset='';S.day='';S.commitPage=0;
  }else if(el.id==='grain')S.grain=el.value;else return;
  render();
});
document.addEventListener('input',event=>{
  const el=event.target;if(!['project-search','commit-search'].includes(el.id))return;
  const position=el.selectionStart,id=el.id,y=window.scrollY;
  if(id==='project-search'){S.query=el.value;S.page=0;}else{S.commitQuery=el.value;S.commitPage=0;}
  render();$(id).focus({preventScroll:true});$(id).setSelectionRange(position,position);window.scrollTo(0,y);
});
try{if(localStorage.getItem('colorvision-history-theme')==='dark')document.body.classList.add('dark');}catch{}
$('branch').textContent=H.branch+' · '+H.head.slice(0,8);
$('sidebar-info').textContent=H.first+' — '+H.last;
$('generated').textContent='生成于 '+new Date(H.generatedAt).toLocaleString('zh-CN');
render();
