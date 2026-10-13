import { Link } from 'react-router'
import { NotFound } from '@wattwise/ui'
import { paths } from '@/config/routes'

export function NotFoundRoute() {
  return <NotFound linkComponent={Link} homeTo={paths.home} />
}
