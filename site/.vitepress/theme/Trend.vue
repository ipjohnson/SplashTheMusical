<script setup>
import { computed } from 'vue'

// A line of values over snapshots, from 0 to 1. A missing value leaves a gap.
const props = defineProps({ values: { type: Array, required: true }, width: { default: 160 }, height: { default: 32 } })

const points = computed(() => {
	const n = props.values.length
	const x = i => (n <= 1 ? props.width / 2 : (i * (props.width - 4)) / (n - 1) + 2)
	const y = v => props.height - 2 - v * (props.height - 4)
	const runs = []
	let run = []
	props.values.forEach((v, i) => {
		if (v === null || v === undefined) {
			if (run.length) runs.push(run)
			run = []
		} else run.push(`${x(i).toFixed(1)},${y(v).toFixed(1)}`)
	})
	if (run.length) runs.push(run)
	return runs
})
</script>

<template>
	<svg class="trend" :width="width" :height="height" :viewBox="`0 0 ${width} ${height}`" role="img">
		<line x1="0" :y1="height - 2" :x2="width" :y2="height - 2" class="axis" />
		<template v-for="(run, i) in points" :key="i">
			<polyline v-if="run.length > 1" :points="run.join(' ')" />
			<circle v-else :cx="run[0].split(',')[0]" :cy="run[0].split(',')[1]" r="2" />
		</template>
	</svg>
</template>
