import {test} from 'node:test'
import assert from 'node:assert/strict'
import {readFileSync} from 'node:fs'
import vm from 'node:vm'

const source = readFileSync(new URL('../preview/main.js', import.meta.url), 'utf8')

# Exercise the shipped preview bundle with browser-like focus and storage failures.
def preview(project, stored = '', blocked = false)
	const document = {activeElement: null, body: {dataset: {project}}}
	const storage = new Map([['xivdispatch-preview-draft', stored]])
	def node(tag)
		const item = {
			tag, dataset: {}, attrs: {}, children: [], listeners: {}, textContent: '', value: ''
			setAttribute: do(key, value) this.attrs[key] = value
			append: do(...children) this.children.push(...children)
			replaceChildren: do(...children)
				if this.all().includes(document.activeElement)
					document.activeElement = document.body
				this.children = children
			addEventListener: do(event, handler) this.listeners[event] = handler
			focus: do document.activeElement = this
			all: do
				return this.children.flatMap(do(child) [child, ...child.all()])
			querySelectorAll: do
				return this.all().filter(do(child) child.dataset.previewControl)
		}
		return item
	const root = node('main')
	document.createElement = node
	document.querySelector = do root
	const localStorage = {
		getItem: do(key) storage.get(key) || null
		setItem: do(key, value)
			if blocked
				throw new Error('Storage blocked')
			storage.set(key, value)
		removeItem: do(key)
			if blocked
				throw new Error('Storage blocked')
			storage.delete(key)
	}
	vm.runInNewContext(source, {document, localStorage, URL, Blob, setTimeout})
	def find(label)
		return root.all().find(do(item) item.textContent == label)
	def click(label)
		const item = find(label)
		assert.ok(item, label)
		item.focus()
		item.listeners.click()
	def draft
		return root.all().find(do(item) item.tag == 'textarea')
	return {document, root, storage, find, click, draft}

test('Prism keeps keyboard focus across sample, output and connection changes', do
	const view = preview('xivprism')
	view.click('Advance sample')
	assert.equal(view.document.activeElement, view.find('Advance sample'))
	view.click('Simulate unplug')
	assert.equal(view.document.activeElement, view.find('Simulate reconnect'))
	view.click('Simulate reconnect')
	assert.equal(view.document.activeElement, view.find('Simulate unplug'))
	const output = view.root.all().find(do(item) item.tag == 'select')
	output.focus()
	output.value = 'tablet'
	output.listeners.change()
	assert.equal(view.document.activeElement.tag, 'select')
	assert.equal(view.document.activeElement.value, 'tablet')
)

test('Camp keeps the selected preset focused after replacing its panels', do
	const view = preview('xivcamp')
	view.click('Work corner')
	assert.equal(view.document.activeElement, view.find('Work corner'))
	assert.equal(view.find('Work corner').attrs['aria-pressed'], 'true')
	assert.equal(view.find('Campfire').attrs['aria-pressed'], 'false')
)

test('Dispatch retains an unsaved draft across quiet mode and conversation changes', do
	const view = preview('xivdispatch', '', true)
	view.draft().value = 'An idea worth keeping'
	view.draft().listeners.input()
	view.click('Quiet session')
	assert.equal(view.document.activeElement, view.find('Leave quiet mode'))
	assert.equal(view.draft().value, 'An idea worth keeping')
	view.click('Thread')
	assert.equal(view.draft().value, 'An idea worth keeping')
	assert.ok(view.root.all().some(do(item) item.textContent.includes('this visit only')))
	assert.equal(view.storage.get('xivdispatch-preview-draft'), '')
)

test('Dispatch preserves stored drafts and persists new edits when storage works', do
	const view = preview('xivdispatch', 'Earlier draft')
	assert.equal(view.draft().value, 'Earlier draft')
	view.draft().value = 'Updated draft'
	view.draft().listeners.input()
	view.click('Pinned channel')
	assert.equal(view.draft().value, 'Updated draft')
	assert.equal(view.storage.get('xivdispatch-preview-draft'), 'Updated draft')
	view.click('Clear preview draft')
	view.click('Direct message')
	assert.equal(view.draft().value, '')
	assert.equal(view.storage.has('xivdispatch-preview-draft'), false)
)

test('a failed storage clear stays cleared in memory and reports the limitation', do
	const view = preview('xivdispatch', 'Old persisted draft', true)
	view.click('Clear preview draft')
	assert.ok(view.root.all().some(do(item) item.textContent.includes('could not be cleared')))
	view.click('Thread')
	assert.equal(view.draft().value, '')
	assert.equal(view.storage.get('xivdispatch-preview-draft'), 'Old persisted draft')
)

test('only actual toggle controls expose pressed state', do
	const view = preview('xivprism')
	assert.equal(view.find('Advance sample').attrs['aria-pressed'], undefined)
	assert.equal(view.find('Export preview profile').attrs['aria-pressed'], undefined)
	assert.equal(view.find('Prism only').attrs['aria-pressed'], 'true')
)
