import { defineConfig } from 'vitepress'

const repository = 'https://github.com/ipjohnson/SplashTheMusical'

export default defineConfig({
	title: 'SplashTheMusical',
	description: 'Parse, generate and render Mermaid diagrams in .NET',
	base: process.env.SITE_BASE ?? '/SplashTheMusical/',
	cleanUrls: true,
	lastUpdated: false,
	srcExclude: ['README.md'],
	themeConfig: {
		nav: [
			{ text: 'Guide', link: '/guide/getting-started' },
			{ text: 'Comparison', link: '/compare/' },
			{ text: 'Parity report', link: '/parity' },
		],
		sidebar: {
			'/guide/': [
				{ text: 'Guide', items: [{ text: 'Getting started', link: '/guide/getting-started' }] },
			],
		},
		socialLinks: [{ icon: 'github', link: repository }],
		search: { provider: 'local' },
		footer: { message: 'Released under the MIT License.' },
	},
})
