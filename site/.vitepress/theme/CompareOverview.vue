<script setup>
import { ref, computed, onMounted } from 'vue'
import { load, typeUrl, percent } from './data.js'
import Trend from './Trend.vue'

const index = ref()
const loaded = ref(false)
onMounted(async () => {
	index.value = await load('snapshots.json')
	loaded.value = true
})

const snapshots = computed(() => index.value?.snapshots ?? [])
const last = computed(() => snapshots.value.at(-1))
const previous = computed(() => snapshots.value.at(-2))

const rows = computed(() => {
	if (!last.value) return []
	return Object.entries(last.value.types).map(([type, t]) => {
		const judged = t.pass + t.fail
		const before = previous.value?.types[type]
		return {
			type,
			...t,
			rate: judged ? t.pass / judged : null,
			change: before ? t.pass - before.pass : null,
			meanTrend: snapshots.value.map(s => s.types[type]?.mean ?? null),
		}
	})
})

const totals = computed(() => {
	const sum = k => rows.value.reduce((a, r) => a + r[k], 0)
	return { cases: sum('cases'), pass: sum('pass'), fail: sum('fail'), report: sum('report') }
})
const passTrend = computed(() =>
	snapshots.value.map(s => {
		const t = Object.values(s.types)
		const pass = t.reduce((a, x) => a + x.pass, 0)
		const judged = t.reduce((a, x) => a + x.pass + x.fail, 0)
		return judged ? pass / judged : null
	}),
)
</script>

<template>
	<p v-if="!loaded">Loading the comparison data.</p>
	<p v-else-if="!last">
		There are no snapshots yet. The site workflow adds one after each push to main.
	</p>
	<template v-else>
		<p>
			Snapshot <strong>{{ last.id }}</strong> against mermaid.js {{ last.mermaid }}, one of {{ snapshots.length }}.
			{{ totals.pass }} of {{ totals.pass + totals.fail }} judged cases pass L4.
		</p>
		<div class="compare-total">
			<span>L4 pass rate over all snapshots</span>
			<Trend :values="passTrend" :width="480" :height="80" />
		</div>
		<table class="compare-types">
			<thead>
				<tr>
					<th>Type</th>
					<th>Cases</th>
					<th>Pass</th>
					<th>Fail</th>
					<th>Report-only</th>
					<th>Pass rate</th>
					<th>Since last</th>
					<th>Mean score</th>
					<th>Mean score over time</th>
				</tr>
			</thead>
			<tbody>
				<tr v-for="r in rows" :key="r.type">
					<td><a :href="typeUrl(r.type)">{{ r.type }}</a></td>
					<td>{{ r.cases }}</td>
					<td>{{ r.pass }}</td>
					<td>{{ r.fail }}</td>
					<td>{{ r.report }}</td>
					<td>{{ r.rate === null ? '-' : percent(r.pass, r.pass + r.fail) }}</td>
					<td :class="{ up: r.change > 0, down: r.change < 0 }">
						{{ r.change === null ? '-' : r.change > 0 ? '+' + r.change : r.change }}
					</td>
					<td>{{ r.mean === null ? '-' : r.mean.toFixed(3) }}</td>
					<td><Trend :values="r.meanTrend" /></td>
				</tr>
			</tbody>
		</table>
	</template>
</template>
