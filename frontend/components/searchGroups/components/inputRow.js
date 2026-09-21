import searchUsersStore from "../stores/searchGroupsStore";
import {createUseStyles} from "react-jss";
import useButtonStyles from "../../../styles/buttonStyles";
import ActionButton from "../../actionButton";
import {useEffect, useState} from "react";
import {SearchIcon} from "../../navbar/components/search";

const useStyles = createUseStyles({
    column: {
        display: 'inline-block',
    },
    searchButton: {
        fontSize: '15px',
        paddingLeft: '6px',
        paddingRight: '6px',
    },
    searchInput: {
        border: 'none',
        padding: 0,
        fontSize: '16px!important',
        marginLeft: '12px',
        marginTop: '2px',
        float: 'left',
        background: 'none',
        color: '#191919', // search box is always white so this is necessary
        fontWeight: '200',
        display: 'block',
        lineHeight: '1.5',
        appearance: 'none',
        borderRadius: '5.25px',
        width: 'calc(100% - 36px - 12px)',
        '&:focus': {
            border: 'none!important',
            boxShadow: 'none!important',
            outline: 0,
        },
        '&::placeholder': {
            color: 'var(--text-color-secondary)'
        }
    },
    wrapper: {
        width: '100%',
        border: '1px solid var(--text-color-secondary)',
        height: '38px',
        padding: 0,
        background: 'white',
        borderRadius: 3,
        justifyContent: 'space-between',
        display: 'flex',
    },
    resultsText: {
        fontSize: 20,
        fontWeight: 700,
        margin: 0,
        padding: '5px 0 15px 0',
    },
})

const InputRow = () => {
    const store = searchUsersStore.useContainer();
    const s = useStyles();
    const [query, setQuery] = useState('');

    useEffect(() => {
        if (store.keyword === query && query !== '') return;
        setQuery(store.keyword);
    }, [store.keyword]);

    const onClick = () => {
        store.setKeyword(query);
    }

    return <div className={`flex flex-column`}>
        <div className='col-12 col-lg-12'>
            <p className={`fw-bold ${s.resultsText}`}>Group results for {store.keyword}</p>
        </div>
        <div>
            <div className={`col-8 col-lg-9 ${s.wrapper}`}>
                <input disabled={store.locked} value={query} onChange={(e) => {
                    setQuery(e.currentTarget.value);
                }} onKeyPress={e => {
                    if (e.key === 'Enter') {
                        onClick(e);
                    }
                }} className={s.searchInput} type='text' placeholder='Search All Groups'/>
                <SearchIcon onClick={onClick} />
            </div>
        </div>
    </div>
}

export default InputRow;