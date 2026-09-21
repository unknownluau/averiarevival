import searchUsersStore from "../stores/searchUsersStore";
import { createUseStyles } from "react-jss";
import { useEffect, useState } from "react";
import { SearchIcon } from "../../navbar/components/search";

const useStyles = createUseStyles({
  searchInput: {
    border: 'none',
    padding: 0,
    fontSize: '16px!important',
    marginLeft: '12px',
    marginTop: '2px',
    float: 'left',
    background: 'none',
    color: '#191919',
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
      color: 'var(--text-color-secondary)',
    },
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
  keyword: {
    fontWeight: 700,
  },
});

const InputRow = () => {
  const store = searchUsersStore.useContainer();
  const s = useStyles();
  const [query, setQuery] = useState(/** @type {string} */(''));

  useEffect(() => {
    if (store.keyword === query && query !== '') return;
    setQuery(store.keyword || '');
  }, [store.keyword]);

  const onClick = () => {
    store.setPage(1);
    store.setKeyword(query);
  };

  return <div className={`flex flex-column`}>
    <div className='col-12 col-lg-12'>
      <p className={s.resultsText}>Player Results for <span className={s.keyword}>{store.keyword || ''}</span></p>
    </div>
    <div>
      <div className={`col-8 col-lg-9 ${s.wrapper}`}>
        <input
          disabled={store.locked}
          value={query}
          onChange={e => setQuery(e.currentTarget.value)}
          onKeyPress={e => { if (e.key === 'Enter') onClick(); }}
          className={s.searchInput}
          type='text'
          placeholder='Search Players'
        />
        <SearchIcon onClick={onClick} />
      </div>
    </div>
  </div>;
};

export default InputRow;
