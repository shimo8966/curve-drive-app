const fs=require('fs'),vm=require('vm'),assert=require('assert/strict');
const source=fs.readFileSync(process.env.CURVE_APP_JS||'/workspace/sites/curve-drive-field/dist/app.js','utf8');let code='';
for(const line of source.split('\n'))if(/^(function (number|advance|model|plain)\b)/.test(line))code+=line+'\n';
code+=source.slice(source.indexOf('// BEGIN PREDICTION CORE'),source.indexOf('// END PREDICTION CORE'))+'globalThis.api={model,predictAlignment}';const ctx=vm.createContext({});vm.runInContext(code,ctx);const {model,predictAlignment:predict}=ctx.api;
const base={basics:{site:'架空予測テスト',x0:'0',y0:'0',heading:'0',pitch:'.5',z0:'0',grade:'0'},segs:[{type:'S',l:'20',r:''}],meas:[]};
function make(segs,stations,left){const d=JSON.parse(JSON.stringify(base));d.segs=segs;d.meas=stations.map(s=>({s:String(s),left:String(typeof left==='function'?left(s):left),up:'',depth:''}));return d}
const S=[{type:'S',l:'20',r:''}],stations=[0,.5,1,1.5,2];let data=make(S,stations,s=>20+10*s),m=model(data),before=JSON.stringify(m),p=predict(m);assert.equal(p.available,true);assert.ok(Math.abs(p.at1.left-50)<1e-6);assert.ok(Math.abs(p.at2.left-60)<1e-6);assert.equal(JSON.stringify(m),before);
for(const type of ['L','R']){const sign=type==='L'?1:-1;data=make([{type,l:'3',r:'45'},{type:'S',l:'10',r:''}],[1,1.5,2,2.5,3],0);p=predict(model(data));assert.equal(p.available,true);const exact=sign*45*(1-Math.cos(2/45))*1000;assert.ok(Math.abs(p.at2.left-exact)<2,{type,predicted:p.at2.left,exact});assert.match(p.curveInfo,/終了点/);assert.equal(p.points[0].ahead,0);assert.equal(p.points.at(-1).s,5);console.log('PASS '+type+' curve-to-straight continuation: predicted '+p.at2.left.toFixed(3)+'mm, analytic '+exact.toFixed(3)+'mm')}
data=make([{type:'L',l:'20',r:'100'}],[1,1.5,2,2.5,3],20);p=predict(model(data));assert.equal(p.available,true);assert.ok(Math.abs(p.at2.left-20)<2);assert.match(p.curveInfo,/17m/);
assert.equal(predict(model(make(S,[0,.5,1,1.5],0))).available,false);assert.equal(predict(model(make(S,[0,.25,.5,.75,1],0))).available,false);
p=predict(model(make(S,Array.from({length:31},(_,i)=>i/10),5)));assert.equal(p.available,true);assert.equal(p.count,7);assert.ok(Math.abs(p.at2.left-5)<1e-6);
p=predict(model(make(S,[0,.5,1,1.5,2,2.5,3],s=>Math.round(s*2)%2?30:-30)));assert.equal(p.available,false);assert.match(p.message,/安定/);
p=predict(model(make([{type:'S',l:'3',r:''}],stations,0)));assert.equal(p.available,true);assert.equal(p.horizon,1);assert.equal(p.at2,null);assert.ok(p.points.every(q=>q.s<=3));assert.equal(predict(model(make([{type:'S',l:'2',r:''}],stations,0))).available,false);
data=make(S,[0,.1,1,1.9,2.5],s=>10*s);p=predict(model(data));assert.equal(p.available,true);assert.ok(Math.abs(p.at2.left-45)<1e-6);
data.basics.x0='1000000';data.basics.y0='2000000';data.basics.heading='87';p=predict(model(data));assert.equal(p.available,true);assert.ok(Math.abs(p.at2.left-45)<.01);
console.log('PASS linear trend, normal-offset curve, sparse/short/unstable suppression, dense/irregular spacing, plan-end clipping, large-coordinate invariance and immutable measured data');
