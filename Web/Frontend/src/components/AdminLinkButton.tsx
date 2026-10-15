import { Button, type ButtonProps } from 'antd'
import { useHref, useLinkClickHandler } from 'react-router-dom'

export function AdminLinkButton({ to, ...props }: Omit<ButtonProps, 'href' | 'onClick'> & { to: string }) {
  const href = useHref(to)
  const onClick = useLinkClickHandler<HTMLAnchorElement>(to, { target: props.target })
  return <Button {...props} href={href} onClick={onClick} />
}
