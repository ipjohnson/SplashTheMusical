<script setup>
import { ref, computed, onMounted } from 'vue'
import { load, latest, imageUrl, caseUrl, formatScore } from './data.js'

const props = defineProps({ type: { type: String, required: true } })

const history = ref()
const issues = ref({})
const loaded = ref(false)
onMounted(async () => {
	;[history.value, issues.value] = await Promise.all([load(`history/${props.type}.json`), load('issues.json')])
	issues.value ??= {}
	loaded.value = true
})

const outcome = ref('all')
const order = ref('score')
const query = ref('')
const shown = ref(48)

const cases = computed(() => latest(history.value))
const counts = computed(() => {
	const c = { all: cases.value.length }
	for (const x of cases.value) c[x.outcome] = (c[x.outcome] ?? 0) + 1
	return c
})
const changed = name => (history.value.cases[name]?.length ?? 0) > 1

const filtered = computed(() => {
	const q = query.value.trim().toLowerCase()
	const list = cases.value.filter(c => (outcome.value === 'all' || c.outcome === outcome.value) && (!q || c.name.toLowerCase().includes(q)))
	// A case without a score, because a side drew nothing, comes after the scored ones in either score order.
	const byScore = sign => (a, b) =>
		(a.score === null) - (b.score === null) || sign * ((a.score ?? 0) - (b.score ?? 0)) || a.name.localeCompare(b.name)
	if (order.value === 'score') list.sort(byScore(1))
	else if (order.value === 'score-desc') list.sort(byScore(-1))
	else list.sort((a, b) => a.name.localeCompare(b.name))
	return list
})
const shortName = name => name.slice(props.type.length + 1)
</script>

<template>
	<p v-if="!loaded">Loading the comparison data.</p>
	<p v-else-if="!history">There is no comparison data for {{ type }} yet.</p>
	<template v-else>
		<p>
			{{ cases.length }} cases. A case passes when its similarity is at least {{ history.threshold }}<span v-if="history.reportOnly">, but {{ type }} is report-only, so its scores are not judged</span>.
		</p>
		<div class="compare-filters">
			<label>Outcome
				<select v-model="outcome">
					<option v-for="o in ['all', 'fail', 'pass', 'report', 'n/a']" :key="o" :value="o" :disabled="!counts[o]">
						{{ o }} ({{ counts[o] ?? 0 }})
					</option>
				</select>
			</label>
			<label>Order
				<select v-model="order">
					<option value="score">Lowest score first</option>
					<option value="score-desc">Highest score first</option>
					<option value="name">Name</option>
				</select>
			</label>
			<label>Search <input v-model="query" type="search" placeholder="case name" /></label>
		</div>
		<div class="compare-grid">
			<a v-for="c in filtered.slice(0, shown)" :key="c.name" class="compare-card" :href="caseUrl(c.name)">
				<div class="compare-pair">
					<img v-if="c.oracle" :src="imageUrl(c.oracle)" loading="lazy" alt="mermaid.js" />
					<span v-else class="compare-missing">mermaid.js refuses it</span>
					<img v-if="c.stm" :src="imageUrl(c.stm)" loading="lazy" alt="STM" />
					<span v-else class="compare-missing">STM drew nothing</span>
				</div>
				<div class="compare-caption">
					<span class="compare-name">{{ shortName(c.name) }}</span>
					<span :class="['compare-outcome', c.outcome.replace('/', '')]">{{ c.outcome }}</span>
					<span>{{ formatScore(c.score) }}</span>
					<span v-if="changed(c.name)" title="This case has more than one snapshot">history</span>
					<span v-if="issues[c.name]?.length">{{ issues[c.name].length }} open {{ issues[c.name].length === 1 ? 'issue' : 'issues' }}</span>
				</div>
			</a>
		</div>
		<p v-if="filtered.length > shown">
			<button class="compare-button" @click="shown += 48">Show more ({{ filtered.length - shown }} left)</button>
		</p>
	</template>
</template>
