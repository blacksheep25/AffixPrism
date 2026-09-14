const fs=require('fs'),path=require('path');
const sharp=require(process.env.AFFIXPRISM_SHARP || 'sharp');
const root=process.cwd(),out=path.join(root,'docs/branding'),assets=path.join(root,'src/AffixPrism/Assets');
const defs=`<defs><linearGradient id="gold" x1="0" y1="0" x2="1" y2="1"><stop stop-color="#F7E2A1"/><stop offset=".5" stop-color="#C8A353"/><stop offset="1" stop-color="#8D713B"/></linearGradient></defs>`;
const mark=`<path d="M128 20 38 222 96 192 128 70Z" fill="url(#gold)"/><path d="m128 20 44 103-33 17-11-70Z" fill="#B99B58"/><path d="m139 140 23 52 56 30-52-93Z" fill="url(#gold)"/><path d="m38 222 65-110-7 80Z" fill="#AD8746"/><path d="m162 192-23-52 79 82Z" fill="#8C6D37"/><path d="m15 148 124-18-4 5Z" fill="#EFEADB"/><path d="m142 132 102-51-8 23Z" fill="#42CAE3"/><path d="m145 137 99-14v15Z" fill="#9B74E0"/><path d="m146 143 96 11v19Z" fill="#E9B64D"/>`;
const svg=(body,view='0 0 256 256')=>`<svg xmlns="http://www.w3.org/2000/svg" viewBox="${view}">${defs}${body}</svg>`;
const icon=svg(`<rect x="4" y="4" width="248" height="248" rx="48" fill="#111613"/><rect x="5" y="5" width="246" height="246" rx="47" fill="none" stroke="#343B32" stroke-width="2"/>${mark}`);
const symbol=svg(mark);
const tray=(colour)=>svg(`<path d="M128 14 20 237 97 199 128 105 159 199 236 237Z" fill="${colour}"/><path d="m128 14 0 91-31 94-77 38Z" fill="${colour}"/>`);
const glyphs={
 A:'M0 90 25 0 35 0 60 90H44L39 70H21L16 90Z M25 55H35L30 29Z',
 F:'M0 0H60L49 15H16V37H49L38 52H16V79L0 90Z',
 I:'M4 0H44L34 12V78L44 90H4L14 78V12Z',
 X:'M0 0H18L32 31 47 0H64L42 44 65 90H47L32 58 17 90H0L23 44Z',
 P:'M0 0H42L60 18V38L42 56H16V79L0 90Z M16 15V41H36L44 33V23L36 15Z',
 R:'M0 0H42L60 18V38L43 55 65 90H46L25 56H16V90H0Z M16 15V41H36L44 33V23L36 15Z',
 S:'M60 0 47 15H16V30L60 62V74L44 90H0L13 75H43V67L0 35V16L16 0Z',
 M:'M0 90V0H13L35 36 57 0H70V90H54V30L35 60 16 30V90Z'
};
const letters=[...'AFFIXPRISM'].map((c,i)=>`<path transform="translate(${290+i*65} 80)" d="${glyphs[c]}" fill="${i<5?'#EDE7D5':'#D5B56A'}" fill-rule="evenodd"/>`).join('');
const logo=svg(`<g transform="translate(22 12) scale(.86)">${mark}</g>${letters}`,'0 0 970 256');
const files={'affixprism-symbol.svg':symbol,'affixprism-logo.svg':logo,'affixprism-icon.svg':icon,'affixprism-tray.svg':tray('#E1C681'),'affixprism-tray-white.svg':tray('#F0EFE8')};
async function ico(input,destination){let sizes=[16,20,24,32,48,64,128,256],images=await Promise.all(sizes.map(s=>sharp(Buffer.from(input)).resize(s,s).png().toBuffer()));let header=Buffer.alloc(6+16*sizes.length);header.writeUInt16LE(1,2);header.writeUInt16LE(sizes.length,4);let offset=header.length;images.forEach((b,i)=>{let p=6+i*16;header[p]=sizes[i]===256?0:sizes[i];header[p+1]=header[p];header.writeUInt16LE(1,p+4);header.writeUInt16LE(32,p+6);header.writeUInt32LE(b.length,p+8);header.writeUInt32LE(offset,p+12);offset+=b.length});fs.writeFileSync(destination,Buffer.concat([header,...images]));}
(async()=>{for(const [n,s] of Object.entries(files)){fs.writeFileSync(path.join(out,n),s);await sharp(Buffer.from(s)).resize(n.includes('logo')?1720:512).png().toFile(path.join(out,n.replace('.svg','.png')))}
fs.writeFileSync(path.join(assets,'AffixPrism.svg'),icon);await sharp(Buffer.from(icon)).resize(512).png().toFile(path.join(assets,'AffixPrism.png'));await ico(icon,path.join(assets,'AffixPrism.ico'));await ico(tray('#E1C681'),path.join(assets,'AffixPrism-tray.ico'));
const board=svg(`<rect width="1100" height="620" fill="#101411"/><g transform="translate(20 10) scale(1.07)">${logo.replace(/<svg[^>]*>|<\/svg>/g,'')}</g><text x="80" y="352" fill="#BDAF8A" font-family="Segoe UI" font-size="18">APPLICATION ICON</text><g transform="translate(70 370) scale(.78)">${icon.replace(/<svg[^>]*>|<\/svg>/g,'')}</g><text x="420" y="352" fill="#BDAF8A" font-family="Segoe UI" font-size="18">TRAY ICON</text><g transform="translate(410 385) scale(.6)">${tray('#E1C681').replace(/<svg[^>]*>|<\/svg>/g,'')}</g><g transform="translate(650 385) scale(.6)">${tray('#F0EFE8').replace(/<svg[^>]*>|<\/svg>/g,'')}</g><text x="860" y="420" fill="#BDAF8A" font-family="Segoe UI" font-size="16">Actual sizes</text><g transform="translate(870 455) scale(.0625)">${tray('#E1C681').replace(/<svg[^>]*>|<\/svg>/g,'')}</g><g transform="translate(915 451) scale(.09375)">${tray('#E1C681').replace(/<svg[^>]*>|<\/svg>/g,'')}</g>`,'0 0 1100 620');await sharp(Buffer.from(board)).png().toFile(path.join(out,'brand-preview.png'));
})();
