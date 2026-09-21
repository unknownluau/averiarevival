import { useRef, useState } from "react";
import { createUseStyles } from "react-jss";
import {getTheme, themeType} from "../../services/theme";

const useSelectorStyles = createUseStyles({
    selectorWrapper: {
        '@media(max-width: 994px)': {
            paddingLeft: 0,
        },
    },
    selectorClosed: {
        padding: '10px 15px',
        textAlign: 'left',
        width: '100%',
        color: 'var(--text-color-primary)',
        background: 'var(--white-color)',
        borderRadius: '4px',
        border: '1px solid var(--text-color-quinary)',
        fontSize: '16px',
        userSelect: 'none',
        cursor: 'pointer',
        '&:hover': {
            background: 'var(--primary-color)',
            color: p => p.theme === themeType.dark ? "var(--text-color-primary)" : "var(--white-color)",
        },
        "& *": {
            color: "inherit",
        }
    },
    selectorOpen: {
        background: 'var(--primary-color)',
        borderColor: 'var(--primary-color)',
        color: p => p.theme === themeType.dark ? "var(--text-color-primary)" : "var(--white-color)",
    },
    selectorCaret: {
        float: 'right',
    },
    selectorMenuOpen: {
        position: 'absolute',
        width: '100%',
        background: 'var(--white-color)',
        zIndex: 3,
        backgroundClip: 'padding-box',
        overflowX: 'hidden',
        borderRadius: 4,
    },
    selectOption: {
        padding: '10px 15px',
        marginBottom: 0,
        cursor: 'pointer',
        userSelect: 'none',
        fontSize: '16px',
        '&:hover': {
            boxShadow: 'inset 4px 0 0 0 var(--primary-color)',
            backgroundColor: 'var(--white-color-hover)',
        },
    },
});

/**
 *
 * @param {{options: {name: string; value: any; children?: any;}[]; onChange: (v: any) => void; value?: any; shadow?: boolean; wrapperClass?: string; selectorOptionClass?: string; className?: string;}} props
 * @returns
 */
const Selector = props => {
    const s = useSelectorStyles({theme: getTheme()});
    const [open, setOpen] = useState(false);
    const [selected, setSelected] = useState(() => {
        if (props.value) {
            return props.options.find(v => v.value === props.value)
        }
        return props.options[0]
    });
    const selectorRef = useRef(null);
    
    return <div className={`${s.selectorWrapper} ${props?.wrapperClass ?? ""}`}>
        <div ref={selectorRef} className={s.selectorClosed + ' ' + (open ? s.selectorOpen : '') + ' ' + props.className || ''} onClick={() => {
            setOpen(!open);
        }}>
            <span>{selected.name}</span>
            <span className={s.selectorCaret}>▼</span>
        </div>
        {
            open && selectorRef.current &&
            <div className={s.selectorMenuOpen} style={{ width: selectorRef.current.clientWidth + 'px', boxShadow: props.shadow ? "0 -5px 20px rgba(25,25,25,.15)" : "none" }}>
                {
                    props.options.map(v => {
                        return <p className={`${s.selectOption} ${props.selectorOptionClass || ""}`} key={v.value} onClick={async () => {
                            const change = await props.onChange(v);
                            // zTODO: add async compat.
                            if (typeof change == 'boolean' && change === false) return; // you can return false to cancel out the bototm stuff
                            setSelected(v);
                            setOpen(false);
                        }} title={v.value}>{v?.children || v.name}</p>
                    })
                }
            </div>
        }
    </div>
}

export default Selector;