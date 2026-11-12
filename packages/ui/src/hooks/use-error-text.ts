import { useTranslation } from 'react-i18next'
import { errorMessage } from '@wattwise/core'

// Turns an API error into the text of the current language, through core's code-to-key table.
const useErrorText = () => {
  const { t } = useTranslation('errors')
  return (error: unknown): string => {
    const message = errorMessage(error)
    return t(message.key, message.params ?? {})
  }
}

export default useErrorText
