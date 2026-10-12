import { colorSchemeScript } from '@wattwise/ui/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react(), colorSchemeScript()],
  resolve: { tsconfigPaths: true },
})
