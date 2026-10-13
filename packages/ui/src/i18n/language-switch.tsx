import TranslateIcon from '@mui/icons-material/Translate'
import IconButton from '@mui/material/IconButton'
import Menu from '@mui/material/Menu'
import MenuItem from '@mui/material/MenuItem'
import { languageNames, languages } from '@wattwise/i18n'
import { useId, useState } from 'react'
import { useTranslation } from 'react-i18next'

export function LanguageSwitch() {
  const { t, i18n } = useTranslation('common')
  const menuId = useId()
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)

  const open = anchor !== null

  return (
    <>
      <IconButton
        color="inherit"
        aria-label={t('language.label')}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        onClick={(event) => {
          setAnchor(event.currentTarget)
        }}
      >
        <TranslateIcon />
      </IconButton>
      <Menu
        id={menuId}
        anchorEl={anchor}
        open={open}
        onClose={() => {
          setAnchor(null)
        }}
      >
        {languages.map((language) => (
          <MenuItem
            key={language}
            role="menuitemradio"
            lang={language}
            aria-checked={i18n.resolvedLanguage === language}
            selected={i18n.resolvedLanguage === language}
            onClick={() => {
              void i18n.changeLanguage(language)
              setAnchor(null)
            }}
          >
            {languageNames[language]}
          </MenuItem>
        ))}
      </Menu>
    </>
  )
}
