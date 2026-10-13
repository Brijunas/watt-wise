import BrightnessAutoIcon from '@mui/icons-material/BrightnessAuto'
import DarkModeIcon from '@mui/icons-material/DarkMode'
import LightModeIcon from '@mui/icons-material/LightMode'
import IconButton from '@mui/material/IconButton'
import ListItemIcon from '@mui/material/ListItemIcon'
import ListItemText from '@mui/material/ListItemText'
import Menu from '@mui/material/Menu'
import MenuItem from '@mui/material/MenuItem'
import { useColorScheme } from '@mui/material/styles'
import { useId, useState } from 'react'
import { useTranslation } from 'react-i18next'

type Mode = 'light' | 'dark' | 'system'

const options = [
  { mode: 'light', labelKey: 'appearance.light', Icon: LightModeIcon },
  { mode: 'dark', labelKey: 'appearance.dark', Icon: DarkModeIcon },
  { mode: 'system', labelKey: 'appearance.auto', Icon: BrightnessAutoIcon },
] as const satisfies readonly { mode: Mode; labelKey: string; Icon: unknown }[]

export function ModeSwitch() {
  const { t } = useTranslation('common')
  const { mode, setMode } = useColorScheme()
  const menuId = useId()
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)

  if (!mode) {
    return null
  }

  const open = anchor !== null
  const current = options.find((option) => option.mode === mode) ?? options[2]

  return (
    <>
      <IconButton
        color="inherit"
        aria-label={t('appearance.label')}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        onClick={(event) => {
          setAnchor(event.currentTarget)
        }}
      >
        <current.Icon />
      </IconButton>
      <Menu
        id={menuId}
        anchorEl={anchor}
        open={open}
        onClose={() => {
          setAnchor(null)
        }}
      >
        {options.map(({ mode: value, labelKey, Icon }) => (
          <MenuItem
            key={value}
            role="menuitemradio"
            aria-checked={mode === value}
            selected={mode === value}
            onClick={() => {
              setMode(value)
              setAnchor(null)
            }}
          >
            <ListItemIcon>
              <Icon fontSize="small" />
            </ListItemIcon>
            <ListItemText>{t(labelKey)}</ListItemText>
          </MenuItem>
        ))}
      </Menu>
    </>
  )
}
