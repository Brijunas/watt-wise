import { RouterProvider } from 'react-router/dom'
import PwaUpdatePrompt from '@/components/pwa-update-prompt'
import { router } from './router'

const App = () => {
  return (
    <>
      <RouterProvider router={router} />
      <PwaUpdatePrompt />
    </>
  )
}

export default App
