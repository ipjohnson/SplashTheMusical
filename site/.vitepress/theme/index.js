import DefaultTheme from 'vitepress/theme'
import CompareOverview from './CompareOverview.vue'
import CompareType from './CompareType.vue'
import CompareCase from './CompareCase.vue'
import './compare.css'

export default {
	extends: DefaultTheme,
	enhanceApp({ app }) {
		app.component('CompareOverview', CompareOverview)
		app.component('CompareType', CompareType)
		app.component('CompareCase', CompareCase)
	},
}
