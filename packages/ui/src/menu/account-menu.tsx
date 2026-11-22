import AccountCircleIcon from '@mui/icons-material/AccountCircleRounded'
import CheckRoundedIcon from '@mui/icons-material/CheckRounded'
import Divider from '@mui/material/Divider'
import IconButton from '@mui/material/IconButton'
import ListItemIcon from '@mui/material/ListItemIcon'
import Menu from '@mui/material/Menu'
import MenuItem from '@mui/material/MenuItem'
import { styled, useTheme, useThemeProps } from '@mui/material/styles'
import useMediaQuery from '@mui/material/useMediaQuery'
import { useId, useState, type ElementType } from 'react'
import useLanguageOptions from '../i18n/use-language-options'

interface AccountMenuItemBase {
  key: string
  /** Already translated by the app. */
  label: string
}

export interface AccountMenuLinkItem extends AccountMenuItemBase {
  to: string
  onSelect?: never
}

export interface AccountMenuActionItem extends AccountMenuItemBase {
  onSelect: () => void
  to?: never
}

export type AccountMenuItem = AccountMenuLinkItem | AccountMenuActionItem

export interface AccountMenuProps {
  /** The trigger's accessible name, already translated. */
  label: string
  linkComponent: ElementType<{ to: string }>
  items: readonly AccountMenuItem[]
}

export type AccountMenuClassKey = 'root' | 'menu'

const name = 'WwAccountMenu'

const AccountMenuRoot = styled(IconButton, { name, slot: 'root' })<{
  ownerState: AccountMenuProps
}>({})

const AccountMenuMenu = styled(Menu, { name, slot: 'menu' })<{
  ownerState: AccountMenuProps
}>({})

// The app bar's account button. Its menu holds the app's items (links or actions) and, below 600px,
// where the app bar has no language button, the language. With no items it renders nothing from
// 600px up.
const AccountMenu = (inProps: AccountMenuProps) => {
  const props = useThemeProps({ props: inProps, name })
  const { label, linkComponent, items } = props
  const theme = useTheme()
  const fromSm = useMediaQuery(theme.breakpoints.up('sm'), { noSsr: true })
  const { options, select } = useLanguageOptions()
  const menuId = useId()
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)

  if (fromSm && items.length === 0) {
    return null
  }

  const open = anchor !== null
  const close = () => {
    setAnchor(null)
  }

  return (
    <>
      <AccountMenuRoot
        ownerState={props}
        color="inherit"
        aria-label={label}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        onClick={(event) => {
          setAnchor(event.currentTarget)
        }}
      >
        <AccountCircleIcon />
      </AccountMenuRoot>
      {/* `variant="menu"`: the ticked language doesn't take the initial focus, the first item does. */}
      <AccountMenuMenu
        ownerState={props}
        variant="menu"
        id={menuId}
        anchorEl={anchor}
        open={open}
        onClose={close}
      >
        {items.map((item) =>
          item.to === undefined ? (
            <MenuItem
              key={item.key}
              onClick={() => {
                close()
                item.onSelect()
              }}
            >
              {item.label}
            </MenuItem>
          ) : (
            <MenuItem key={item.key} component={linkComponent} to={item.to} onClick={close}>
              {item.label}
            </MenuItem>
          ),
        )}
        {!fromSm && items.length > 0 ? <Divider key="divider" component="li" /> : null}
        {fromSm
          ? null
          : options.map((option) => (
              <MenuItem
                key={option.key}
                role="menuitemradio"
                lang={option.lang}
                aria-checked={option.selected}
                selected={option.selected}
                onClick={() => {
                  select(option.key)
                  close()
                }}
              >
                <ListItemIcon>
                  {option.selected ? <CheckRoundedIcon fontSize="small" /> : null}
                </ListItemIcon>
                {option.content}
              </MenuItem>
            ))}
      </AccountMenuMenu>
    </>
  )
}

export default AccountMenu
