# Interactive design previews. No game, account, device or network connection.
const project = document.body.dataset.project
const root = document.querySelector('#preview')
const state = {mode: 'standalone', output: 'glasses', connected: true, sample: 0, quiet: false, conversation: 'dm', preset: 'campfire'}

def element(kind, text = '', className = '')
	const node = document.createElement(kind)
	if text
		node.textContent = text
	if className
		node.className = className
	return node

def button(label, action, active = false)
	const node = element('button', label, 'chip')
	node.type = 'button'
	node.setAttribute('aria-pressed', String(active))
	node.addEventListener('click', action)
	return node

def panel(title, lines, position = '')
	const node = element('section', '', 'preview-panel ' + position)
	node.append(element('h3', title))
	lines.forEach(do(line) node.append(element('p', line)))
	return node

def exportDraft
	const profile = {version: 1, project, simulated: true, settings: {...state}}
	const url = URL.createObjectURL(new Blob([JSON.stringify(profile, null, 2)], {type: 'application/json'}))
	const link = element('a')
	link.href = url
	link.download = project + '-preview.json'
	link.click()
	setTimeout((do URL.revokeObjectURL(url)), 1000)

def tools
	const row = element('div', '', 'preview-controls')
	row.append(button('Export preview profile', exportDraft))
	return row

def renderPrism
	root.replaceChildren()
	const choices = element('div', '', 'preview-controls')
	['standalone', 'combined'].forEach(do(mode)
		choices.append(button(mode == 'standalone' ? 'Prism only' : 'Native Moonlight concept', (do
			state.mode = mode
			renderPrism()
		), state.mode == mode))
	)
	const output = element('select')
	output.setAttribute('aria-label', 'Preview output')
	['glasses', 'tablet', 'monitor'].forEach(do(value)
		const option = element('option', value == 'glasses' ? 'Glasses: peripheral HUD' : value == 'tablet' ? 'Tablet: journal' : 'Monitor: dashboard')
		option.value = value
		output.append(option)
	)
	output.value = state.output
	output.addEventListener('change', do
		state.output = output.value
		renderPrism()
	)
	choices.append(output, button(state.connected ? 'Simulate unplug' : 'Simulate reconnect', do
		state.connected = !state.connected
		renderPrism()
	), button('Advance sample', do
		state.sample++
		renderPrism()
	))
	root.append(choices)
	const stage = element('div', '', 'preview-stage prism-stage output-' + state.output)
	stage.dataset.mode = state.mode
	if state.mode == 'combined'
		stage.append(element('div', 'Game view stays here', 'preview-game'))
	if !state.connected
		stage.append(element('p', 'Display disconnected. Companion panels close; the game view remains available.', 'preview-disconnected'))
	else
		if state.output == 'tablet'
			stage.append(panel('Journal · sample chapter', ['A path through the woods', 'Find a quiet place beside the river.', 'Bookmark this chapter for later.'], 'journal-panel'))
		else
			stage.append(panel('Adventurer · sample', ['HP ' + (4280 - state.sample * 37 % 900) + ' / 5000', 'MP 8400 / 10000'], 'panel-left'))
			stage.append(panel('Wayfinder · sample', ['Riverside overlook', '320 yalms · north-east'], 'panel-right'))
			stage.append(panel('Journal · sample', ['A quiet evening', 'Return to the old tree.'], 'panel-bottom'))
			if state.mode == 'standalone' && state.output == 'glasses'
				stage.append(element('p', 'Clear centre', 'preview-centre'))
	root.append(stage)
	const status = element('p', state.connected ? 'Simulated output ready. No glasses or game are connected.' : 'Simulated output absent. No real display settings changed.', 'preview-state')
	status.setAttribute('role', 'status')
	root.append(status, tools())

def readDraft
	try
		return localStorage.getItem('xivdispatch-preview-draft') || ''
	catch
		return ''

def renderDispatch
	root.replaceChildren()
	const controls = element('div', '', 'preview-controls')
	['dm', 'channel', 'thread'].forEach(do(kind)
		controls.append(button(kind == 'dm' ? 'Direct message' : kind == 'channel' ? 'Pinned channel' : 'Thread', (do
			state.conversation = kind
			renderDispatch()
		), state.conversation == kind))
	)
	controls.append(button(state.quiet ? 'Leave quiet mode' : 'Quiet session', (do
		state.quiet = !state.quiet
		renderDispatch()
	), state.quiet))
	root.append(controls)
	const stage = element('div', '', 'preview-stage dispatch-stage')
	const lines = state.conversation == 'dm' ? ['Mira: Ready for a short break?', 'You: Let me finish this chapter first.'] : state.conversation == 'channel' ? ['#weekend-plans · fictional workspace', 'Mira: The next walk starts at the old bridge.', 'Ren: I will bring the map.'] : ['Mira: Which layout should we try?', '2 replies', 'Ren: Glasses for navigation, tablet for the journal.']
	stage.append(panel(state.conversation == 'thread' ? 'A thread within reach' : 'Dispatch · invented conversation', lines))
	const label = element('label', 'Your local preview draft')
	label.htmlFor = 'dispatch-draft'
	const draft = element('textarea')
	draft.id = 'dispatch-draft'
	draft.rows = 3
	draft.maxLength = 1000
	draft.value = readDraft()
	draft.placeholder = 'Try a draft. It stays in this browser only.'
	const notice = element('p', 'Drafts are local. This preview cannot send Slack messages.', 'preview-state')
	notice.setAttribute('role', 'status')
	draft.addEventListener('input', do
		try
			localStorage.setItem('xivdispatch-preview-draft', draft.value)
			notice.textContent = 'Preview draft saved in this browser. Nothing was sent.'
		catch
			notice.textContent = 'Browser storage is unavailable. Nothing was sent.'
	)
	stage.append(label, draft, button('Clear preview draft', do
		try
			localStorage.removeItem('xivdispatch-preview-draft')
		catch
			undefined
		draft.value = ''
		notice.textContent = 'Preview draft cleared. Nothing was sent.'
	))
	root.append(stage, element('p', state.quiet ? 'Quiet session: sample notifications are held.' : 'Sample notifications are visible. No Slack account is connected.', 'preview-state'), notice)

def renderCamp
	root.replaceChildren()
	const presets = {campfire: [['Journal', 'A chapter beside the fire.'], ['Piano', 'An evening station.'], ['Lantern', 'Friends within reach.']], work: [['Dispatch', 'Selected conversations.'], ['Notes', 'A quiet place to write.'], ['Terminal', 'Your chosen workspace.']], adventure: [['Wayfinder', 'A route to the overlook.'], ['Rug', 'A woven map beneath you.'], ['Prism', 'Navigation in your glasses.']]}
	const controls = element('div', '', 'preview-controls')
	Object.keys(presets).forEach(do(preset)
		controls.append(button(preset == 'work' ? 'Work corner' : preset == 'campfire' ? 'Campfire' : 'Adventure', (do
			state.preset = preset
			renderCamp()
		), state.preset == preset))
	)
	root.append(controls)
	const stage = element('div', '', 'preview-stage camp-stage preset-' + state.preset)
	presets[state.preset].forEach(do(item, index)
		stage.append(panel(item[0], [item[1], 'Sample panel · integration planned'], 'camp-panel-' + index))
	)
	root.append(stage, element('p', 'An arrangement preview. No real plugin window, music or world position has changed.', 'preview-state'), tools())

if root
	if project == 'xivprism'
		renderPrism()
	else if project == 'xivdispatch'
		renderDispatch()
	else if project == 'xivcamp'
		renderCamp()
