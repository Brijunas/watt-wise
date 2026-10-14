import { UpdatePrompt } from '@wattwise/ui'
import { useRegisterSW } from 'virtual:pwa-register/react'

const PwaUpdatePrompt = () => {
  const {
    needRefresh: [needRefresh, setNeedRefresh],
    updateServiceWorker,
  } = useRegisterSW()

  return (
    <UpdatePrompt
      open={needRefresh}
      onReload={() => void updateServiceWorker(true)}
      onClose={() => {
        setNeedRefresh(false)
      }}
    />
  )
}

export default PwaUpdatePrompt
