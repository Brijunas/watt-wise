import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { restoreSession } from '@wattwise/core'
import App from '@/app/app'
import AppProvider from '@/app/provider'
import createAppStore from '@/stores/create-app-store'

const container = document.getElementById('root')
if (!container) {
  throw new Error('Root element #root not found')
}

const store = createAppStore()
// The refresh cookie, if valid, brings the session back before the app decides what to show.
void store.dispatch(restoreSession())

createRoot(container).render(
  <StrictMode>
    <AppProvider store={store}>
      <App />
    </AppProvider>
  </StrictMode>,
)
