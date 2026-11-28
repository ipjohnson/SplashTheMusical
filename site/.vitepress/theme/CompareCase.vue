<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { load, imageUrl, sourceUrl, typeUrl, issueUrl, formatScore, repository } from './data.js'

const name = ref('')
const history = ref()
const index = ref()
const issues = ref({})
const loaded = ref(false)
const selected = ref(0)
const source = ref()

onMounted(async () => {
	name.value = new URLSearchParams(window.location.search).get('name') ?? ''
	const type = name.value.split('/')[0]
	if (type) {
		;[history.value, index.value, issues.value] = await Promise.all([
			load(`history/${type}.json`),
			load('snapshots.json'),
			load('issues.json'),
		])
	}
	issues.value ??= {}
	selected.value = Math.max(0, entries.value.length - 1)
	loaded.value = true
})

const type = computed(() => name.value.split('/')[0])
const entries = computed(() => (history.value?.cases[name.value] ?? []).filter(e => !e.removed))
const entry = computed(() => entries.value[selected.value])
const snapshotOf = id => index.value?.snapshots.find(s => s.id === id)
const snapshot = computed(() => entry.value && snapshotOf(entry.value.snapshot))
const openIssues = computed(() => issues.value[name.value] ?? [])

watch(entry, async e => {
	source.value = undefined
	if (e?.source) source.value = await fetch(sourceUrl(e.source)).then(r => (r.ok ? r.text() : undefined))
})

const report = computed(() =>
	entry.value && issueUrl({ name: name.value, entry: entry.value, snapshot: snapshot.value, threshold: history.value.threshold, source: source.value }),
)
const searchUrl = computed(() => `${repository}/issues?q=${encodeURIComponent(`is:issue label:comparison in:title "[${name.value}]"`)}`)
</script>

<template>
	<p v-if="!loaded">Loading the comparison data.</p>
	<p v-else-if="!entry">
		There is no comparison of <code>{{ name || 'this case' }}</code>. Pick a case from the
		<a href="./">comparison overview</a>.
	</p>
	<template v-else>
		<h1 class="compare-title">{{ name }}</h1>
		<p>
			<a :href="typeUrl(type)">All {{ type }} cases</a>.
			Snapshot <a :href="`${repository}/commit/${snapshot?.commit}`">{{ entry.snapshot }}</a>,
			mermaid.js {{ snapshot?.mermaid }}.
			Similarity <strong>{{ formatScore(entry.score) }}</strong> against a threshold of {{ history.threshold }},
			<span :class="['compare-outcome', entry.outcome.replace('/', '')]">{{ entry.outcome }}</span>.
		</p>
		<div class="compare-actions">
			<a class="compare-button brand" :href="report" target="_blank" rel="noopener">Report a difference</a>
			<a v-if="openIssues.length" class="compare-button" :href="searchUrl" target="_blank" rel="noopener">
				{{ openIssues.length }} open {{ openIssues.length === 1 ? 'issue' : 'issues' }}
			</a>
		</div>
		<div class="compare-large">
			<figure>
				<figcaption>mermaid.js</figcaption>
				<img v-if="entry.oracle" :src="imageUrl(entry.oracle)" alt="mermaid.js's drawing" />
				<span v-else class="compare-missing">mermaid.js refuses this case</span>
			</figure>
			<figure>
				<figcaption>STM</figcaption>
				<img v-if="entry.stm" :src="imageUrl(entry.stm)" alt="STM's drawing" />
				<span v-else class="compare-missing">STM drew nothing</span>
			</figure>
		</div>
		<template v-if="entries.length > 1">
			<h2>History</h2>
			<p>STM's drawing at each snapshot where this case changed. Pick one to show it above.</p>
			<input v-model.number="selected" class="compare-slider" type="range" min="0" :max="entries.length - 1" step="1" />
			<div class="compare-history">
				<button
					v-for="(e, i) in entries"
					:key="e.snapshot"
					:class="['compare-step', { active: i === selected }]"
					@click="selected = i"
				>
					<img v-if="e.stm" :src="imageUrl(e.stm)" loading="lazy" alt="" />
					<span v-else class="compare-missing">nothing</span>
					<span>{{ e.snapshot }}</span>
					<span>{{ formatScore(e.score) }}</span>
				</button>
			</div>
		</template>
		<h2>Source</h2>
		<pre class="compare-source"><code>{{ source ?? 'Loading.' }}</code></pre>
	</template>
</template>
