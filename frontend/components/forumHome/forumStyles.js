import { createUseStyles } from "react-jss";

const useForumStyles = createUseStyles({
    header: {},
    headerRow: {
        background: p => p.stylize === 'true' ? "var(--primary-color)" : "#29508d",
        '& th': {
            fontSize: '1.25rem',
            color: 'var(--text-color-primary)',
            fontWeight: 500,
            paddingLeft: '0.25rem',
            paddingTop: '0.5rem',
            paddingBottom: '0.5rem',
        }
    },
    bodyRow: {
        //background: '#f9f9f9',
        background: 'var(--white-color)',
        '&:hover': {
            //background: '#e9e9e9',
            background: 'var(--white-color-hover)',
        },
    },
    lastPostHeader: {
        minWidth: '110px',
    },
})

export default useForumStyles;