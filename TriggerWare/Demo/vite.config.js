import {defineConfig} from 'vite'
import {svelte} from '@sveltejs/vite-plugin-svelte'
import {sveltePreprocess} from 'svelte-preprocess'

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [
        svelte({
            preprocess: sveltePreprocess({
                scss: {
                    includePaths: ['src'],
                },
            }),
        }),
    ],
    build: {
        outDir: "wwwroot"
    },
    server: {
        port: 3000,
        proxy: {
            '/api': {
                target: 'http://localhost:5173',
                secure: false
            }
        }
    }
})
