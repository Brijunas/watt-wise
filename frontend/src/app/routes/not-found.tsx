import { Link } from 'react-router'
import { NotFound } from '@wattwise/ui'
import { paths } from '@/config/routes'

const NotFoundRoute = () => {
  return <NotFound linkComponent={Link} homeTo={paths.home} />
}

export default NotFoundRoute
