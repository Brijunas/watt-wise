import BrightnessAutoIcon from '@mui/icons-material/BrightnessAuto'
import DarkModeIcon from '@mui/icons-material/DarkMode'
import LightModeIcon from '@mui/icons-material/LightMode'
import ListItemIcon from '@mui/material/ListItemIcon'
import ListItemText from '@mui/material/ListItemText'
import { useColorScheme } from '@mui/material/styles'
import { useTranslation } from 'react-i18next'
import IconMenu from '../menu/icon-menu'

type Mode = 'light' | 'dark' | 'system'

const options = [
  { mode: 'light', labelKey: 'appearance.light', Icon: LightModeIcon },
  { mode: 'dark', labelKey: 'appearance.dark', Icon: DarkModeIcon },
  { mode: 'system', labelKey: 'appearance.auto', Icon: BrightnessAutoIcon },
] as const satisfies readonly { mode: Mode; labelKey: string; Icon: unknown }[]

const ModeSwitch = () => {
  const { t } = useTranslation('common')
  const { mode, setMode } = useColorScheme()

  if (!mode) {
    return null
  }

  const current = options.find((option) => option.mode === mode) ?? options[2]

  return (
    <IconMenu
      label={t('appearance.label')}
      icon={<current.Icon />}
      options={options.map(({ mode: value, labelKey, Icon }) => ({
        key: value,
        selected: mode === value,
        content: (
          <>
            <ListItemIcon>
              <Icon fontSize="small" />
            </ListItemIcon>
            <ListItemText>{t(labelKey)}</ListItemText>
          </>
        ),
      }))}
      onSelect={setMode}
    />
  )
}

export default ModeSwitch
