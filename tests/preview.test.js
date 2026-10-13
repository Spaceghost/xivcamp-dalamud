// Generated from native Imba; edit tests/preview.test.imba.

/*body*/
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import vm from 'node:vm';

const source = readFileSync(new URL('../preview/main.js',import.meta.url),'utf8');

// Exercise the shipped preview bundle with browser-like focus and storage failures.
function preview(project,stored = '',blocked = false){
	const document = {activeElement: null,body: {dataset: {project: project}}};
	const storage = new Map([['xivdispatch-preview-draft',stored]]);
	function node(tag){
		const item = {
			tag: tag,dataset: {},attrs: {},children: [],listeners: {},textContent: '',value: '',
			setAttribute: function(key,value) { return this.attrs[key] = value; },
			append: function(...children) { return this.children.push(...children); },
			replaceChildren: function(...children) {
				if (this.all().includes(document.activeElement)) {
					document.activeElement = document.body;
				};
				return this.children = children;
			},
			addEventListener: function(event,handler) { return this.listeners[event] = handler; },
			focus: function() { return document.activeElement = this; },
			all: function() {
				return this.children.flatMap(function(child) { return [child,...child.all()]; });
			},
			querySelectorAll: function() {
				return this.all().filter(function(child) { return child.dataset.previewControl; });
			}
		};
		return item;
	};
	const root = node('main');
	document.createElement = node;
	document.querySelector = function() { return root; };
	const localStorage = {
		getItem: function(key) { return storage.get(key) || null; },
		setItem: function(key,value) {
			if (blocked) {
				throw new Error('Storage blocked');
			};
			return storage.set(key,value);
		},
		removeItem: function(key) {
			if (blocked) {
				throw new Error('Storage blocked');
			};
			return storage.delete(key);
		}
	};
	vm.runInNewContext(source,{document: document,localStorage: localStorage,URL: globalThis.URL,Blob: globalThis.Blob,setTimeout: setTimeout});
	function find(label){
		return root.all().find(function(item) { return item.textContent == label; });
	};
	function click(label){
		const item = find(label);
		assert.ok(item,label);
		item.focus();
		return item.listeners.click();
	};
	function draft(){
		return root.all().find(function(item) { return item.tag == 'textarea'; });
	};
	return {document: document,root: root,storage: storage,find: find,click: click,draft: draft};
};

test('Prism keeps keyboard focus across sample, output and connection changes',function() {
	const view = preview('xivprism');
	view.click('Advance sample');
	assert.equal(view.document.activeElement,view.find('Advance sample'));
	view.click('Simulate unplug');
	assert.equal(view.document.activeElement,view.find('Simulate reconnect'));
	view.click('Simulate reconnect');
	assert.equal(view.document.activeElement,view.find('Simulate unplug'));
	const output = view.root.all().find(function(item) { return item.tag == 'select'; });
	output.focus();
	output.value = 'tablet';
	output.listeners.change();
	assert.equal(view.document.activeElement.tag,'select');
	return assert.equal(view.document.activeElement.value,'tablet');
});

test('Camp keeps the selected preset focused after replacing its panels',function() {
	const view = preview('xivcamp');
	view.click('Work corner');
	assert.equal(view.document.activeElement,view.find('Work corner'));
	assert.equal(view.find('Work corner').attrs['aria-pressed'],'true');
	return assert.equal(view.find('Campfire').attrs['aria-pressed'],'false');
});

test('Dispatch retains an unsaved draft across quiet mode and conversation changes',function() {
	const view = preview('xivdispatch','',true);
	view.draft().value = 'An idea worth keeping';
	view.draft().listeners.input();
	view.click('Quiet session');
	assert.equal(view.document.activeElement,view.find('Leave quiet mode'));
	assert.equal(view.draft().value,'An idea worth keeping');
	view.click('Thread');
	assert.equal(view.draft().value,'An idea worth keeping');
	assert.ok(view.root.all().some(function(item) { return item.textContent.includes('this visit only'); }));
	return assert.equal(view.storage.get('xivdispatch-preview-draft'),'');
});

test('Dispatch preserves stored drafts and persists new edits when storage works',function() {
	const view = preview('xivdispatch','Earlier draft');
	assert.equal(view.draft().value,'Earlier draft');
	view.draft().value = 'Updated draft';
	view.draft().listeners.input();
	view.click('Pinned channel');
	assert.equal(view.draft().value,'Updated draft');
	assert.equal(view.storage.get('xivdispatch-preview-draft'),'Updated draft');
	view.click('Clear preview draft');
	view.click('Direct message');
	assert.equal(view.draft().value,'');
	return assert.equal(view.storage.has('xivdispatch-preview-draft'),false);
});

test('a failed storage clear stays cleared in memory and reports the limitation',function() {
	const view = preview('xivdispatch','Old persisted draft',true);
	view.click('Clear preview draft');
	assert.ok(view.root.all().some(function(item) { return item.textContent.includes('could not be cleared'); }));
	view.click('Thread');
	assert.equal(view.draft().value,'');
	return assert.equal(view.storage.get('xivdispatch-preview-draft'),'Old persisted draft');
});

test('only actual toggle controls expose pressed state',function() {
	const view = preview('xivprism');
	assert.equal(view.find('Advance sample').attrs['aria-pressed'],undefined);
	assert.equal(view.find('Export preview profile').attrs['aria-pressed'],undefined);
	return assert.equal(view.find('Prism only').attrs['aria-pressed'],'true');
});
