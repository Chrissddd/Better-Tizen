import fs from 'node:fs';
import path from 'node:path';
import deviceConfig from './tv_config.cjs';
if(process.argv.includes('--help')){
 console.log('node scripts/tv-remote.mjs KEY_NAME ...\nTV_IP or .local/device.json selects the paired TV; credentials remain local.');
 process.exit(0);
}
const tv=deviceConfig.tvAddress();
const root=path.resolve(import.meta.dirname,'..');
const privateDir=path.join(root,'runtime/private');fs.mkdirSync(privateDir,{recursive:true,mode:0o700});
const tokenPath=path.join(privateDir,'remote-token.json');
const query=new URLSearchParams({name:Buffer.from('Dusk setup').toString('base64')});
if(fs.existsSync(tokenPath)) query.set('token',JSON.parse(fs.readFileSync(tokenPath)).token);
const keys=process.argv.slice(2);for(const key of keys)if(!/^KEY_[A-Z0-9_]+$/.test(key))throw new Error('Invalid key');
const ws=new WebSocket('wss://'+tv+':8002/api/v2/channels/samsung.remote.control?'+query);
const timeout=setTimeout(()=>{console.log('AUTH_OR_CONNECTION_TIMEOUT');ws.close();process.exitCode=1;},28000);
ws.addEventListener('open',()=>console.log('REMOTE_SOCKET_OPEN'));
ws.addEventListener('message',async event=>{
 const data=JSON.parse(String(event.data));console.log('REMOTE_EVENT',data.event||'data');
 if(data.event==='ms.channel.connect'){
  if(data.data?.token)fs.writeFileSync(tokenPath,JSON.stringify({token:data.data.token}),{mode:0o600});
  console.log('REMOTE_AUTHENTICATED');clearTimeout(timeout);
  for(const key of keys){ws.send(JSON.stringify({method:'ms.remote.control',params:{Cmd:'Click',DataOfCmd:key,Option:'false',TypeOfRemote:'SendRemoteKey'}}));console.log('KEY_SENT',key);await new Promise(r=>setTimeout(r,1000));}
  setTimeout(()=>ws.close(),1000);
 }
 if(/unauthorized|denied/i.test(data.event||'')){clearTimeout(timeout);process.exitCode=2;ws.close();}
});
ws.addEventListener('error',event=>{console.log('REMOTE_ERROR',event.message||'websocket error');clearTimeout(timeout);process.exitCode=1;});
