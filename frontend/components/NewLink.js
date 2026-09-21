import NextLink from 'next/link';

/**
 * @typedef {Object} NewLinkProps
 * @property {string=} className
 * @property {(import("react").MouseEventHandler<HTMLAnchorElement>)=} onClick
 * @property {import("react").ReactNode=} children
 * @property {string|null=} href
 * @property {import("react").CSSProperties=} style
 */

/**
 * @returns {JSX.Element}
 * @constructor
 * @type {import("react").FC<NewLinkProps>}
 */
const NewLink = ({className, onClick, children, href, style}) => {
    return <NextLink href={href || "#"} passHref legacyBehavior>
        <a className={className} onClick={onClick} style={style}>
            {children}
        </a>
    </NextLink>
}

export default NewLink;
