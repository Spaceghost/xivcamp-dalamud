import * as compiler from 'imba/compiler'
import {buildSync} from 'esbuild'
import {readFileSync,writeFileSync} from 'node:fs'
const source = readFileSync('preview/main.imba','utf8')
const compiled = compiler.compile(source,{sourcePath:'preview/main.imba',platform:'browser',format:'esm'})
const errors = compiled.diagnostics.filter(do(d) d.severity == 1)
if errors.length
	throw new Error(JSON.stringify(errors))
const code = buildSync({stdin:{contents:compiled.js,resolveDir:process.cwd(),sourcefile:'preview/main.js'},bundle:true,write:false,format:'iife',platform:'browser',target:'es2022',charset:'ascii',legalComments:'none'}).outputFiles[0].text
const output = '// Generated from native Imba; edit preview/main.imba.\n' + code
if process.argv.includes('--check')
	if readFileSync('preview/main.js','utf8') != output
		throw new Error('Precompiled output is stale; run npm run build')
else
	writeFileSync('preview/main.js',output)
console.log('Imba preview compiled output verified.')
