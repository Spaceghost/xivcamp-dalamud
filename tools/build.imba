import * as compiler from 'imba/compiler'
import {buildSync} from 'esbuild'
import {readFileSync,writeFileSync} from 'node:fs'

for [sourcePath,outputPath,browser] of [['preview/main.imba','preview/main.js',true],['tests/preview.test.imba','tests/preview.test.js',false]]
	const source = readFileSync(sourcePath,'utf8')
	const compiled = compiler.compile(source,{sourcePath,platform:browser ? 'browser' : 'node',format:'esm'})
	const errors = compiled.diagnostics.filter(do(d) d.severity == 1)
	if errors.length
		throw new Error(sourcePath + ': ' + JSON.stringify(errors))
	const code = browser ? buildSync({stdin:{contents:compiled.js,resolveDir:process.cwd(),sourcefile:outputPath},bundle:true,write:false,format:'iife',platform:'browser',target:'es2022',charset:'ascii',legalComments:'none'}).outputFiles[0].text : compiled.js
	const output = '// Generated from native Imba; edit ' + sourcePath + '.\n' + code
	if process.argv.includes('--check')
		if readFileSync(outputPath,'utf8') != output
			throw new Error(outputPath + ' is stale; run npm run build')
	else
		writeFileSync(outputPath,output)
console.log('Imba preview and regression test compiled output verified.')
