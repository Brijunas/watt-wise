import DevicesIcon from '@mui/icons-material/DevicesRounded'
import LockIcon from '@mui/icons-material/LockRounded'
import List from '@mui/material/List'
import ListItem from '@mui/material/ListItem'
import ListItemButton from '@mui/material/ListItemButton'
import ListItemIcon from '@mui/material/ListItemIcon'
import ListItemText from '@mui/material/ListItemText'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { paths } from '@/config/routes'

const SettingsLinks = () => {
  const { t } = useTranslation('settings')
  return (
    <List>
      <ListItem disablePadding>
        <ListItemButton component={Link} to={paths.changePassword}>
          <ListItemIcon>
            <LockIcon />
          </ListItemIcon>
          <ListItemText primary={t('changePassword')} />
        </ListItemButton>
      </ListItem>
      <ListItem disablePadding>
        <ListItemButton component={Link} to={paths.devices}>
          <ListItemIcon>
            <DevicesIcon />
          </ListItemIcon>
          <ListItemText primary={t('devices')} />
        </ListItemButton>
      </ListItem>
    </List>
  )
}

export default SettingsLinks
