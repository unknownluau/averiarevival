import { createUseStyles } from "react-jss";

const useFormStyles = createUseStyles({
    accountInfoLabel: {
        marginBottom: '6px',
        color: 'var(--text-color-secondary-dark)',
        fontSize: '15px',
    },
    accountInfoValue: {
        color: 'var(--text-color-primary)',
    },
    descInput: {
        width: '100%',
        borderRadius: '4px',
        padding: '6px 8px',
        border: '1px solid var(--text-color-quinary)',
        background: 'none',
    },
    select: {
        borderRadius: '2px',
        fontSize: '16px',
    },
    disabled: {
        background: 'var(--white-color)!important',
        color: 'var(--text-color-secondary)',
        '&:focus': {
            color: 'var(--text-color-secondary)',
            boxShadow: 'none',
        },
    },
    fakeInput: {
        height: '100%',
        overflow: 'hidden',
    },
    saveButtonWrapper: {
        float: 'right',
        marginTop: '16px',
    },
    saveButton: {
        background: 'var(--background-color)',
        border: '1px solid var(--text-color-quinary)',
        borderRadius: '4px',
        fontSize: '16px',
        padding: '4px 8px',
        cursor: 'pointer',
    },
    genderUnselected: {
        color: 'var(--text-color-quinary)',
        cursor: 'pointer',
    },
    customColorRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        cursor: 'pointer',
    },
    customColorSwatch: {
        width: '20px',
        height: '20px',
        padding: 0,
        border: '1px solid var(--text-color-quinary)',
        borderRadius: '2px',
        background: 'none',
        cursor: 'pointer',
        '&::-webkit-color-swatch-wrapper': { padding: 0 },
        '&::-webkit-color-swatch': { border: 'none' },
        '&::-moz-color-swatch': { border: 'none' },
    },
    customColorHex: {
        fontSize: '16px',
        color: 'var(--text-color-secondary)',
        fontFamily: 'inherit',
    },
});

export default useFormStyles;