import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from '@/app/app'
import AppProvider from '@/app/provider'

const container = document.getElementById('root')
if (!container) {
  throw new Error('Root element #root not found')
}

createRoot(container).render(
  <StrictMode>
    <AppProvider>
      <App />
    </AppProvider>
  </StrictMode>,
)
