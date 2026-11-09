import { readFile, writeFile, copyFile, mkdir } from 'node:fs/promises';
const icons=['layout-grid','building-2','users','target','upload','download','shield-check','history','log-out','menu','x','plus','arrow-right','arrow-left','eye','eye-off','search','search-x','circle-check','circle-alert','clipboard-check','clock','circle-x','loader-circle','file-spreadsheet','lock-keyhole','chevron-down'];
await mkdir('wwwroot/icons',{recursive:true});
let sprite='<svg xmlns="http://www.w3.org/2000/svg">';
for(const icon of icons){const svg=await readFile(`node_modules/lucide-static/icons/${icon}.svg`,'utf8');sprite+=`<symbol id="${icon}" viewBox="0 0 24 24">${svg.match(/<svg\b[^>]*>([\s\S]*?)<\/svg>/)[1]}</symbol>`;}
await writeFile('wwwroot/icons/lucide.svg',sprite+'</svg>');
await copyFile('node_modules/lucide-static/LICENSE','wwwroot/icons/LICENSE');
await mkdir('wwwroot/fonts',{recursive:true});
for(const [font,weights] of [['ibm-plex-sans',[400,500,600]],['ibm-plex-mono',[400,600]]]){
 for(const weight of weights){const file=`${font}-latin-${weight}-normal.woff2`;await copyFile(`node_modules/@fontsource/${font}/files/${file}`,`wwwroot/fonts/${file}`);}
 await copyFile(`node_modules/@fontsource/${font}/LICENSE`,`wwwroot/fonts/${font}-LICENSE`);
}
await writeFile('wwwroot/icons/favicon.svg','<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32"><rect width="32" height="32" rx="3" fill="#163d47"/><circle cx="16" cy="16" r="6" fill="none" stroke="#f7f7f3" stroke-width="2"/><circle cx="25" cy="7" r="3" fill="#db852f"/></svg>');
