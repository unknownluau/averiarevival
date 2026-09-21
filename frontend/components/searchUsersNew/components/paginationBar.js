import { createUseStyles } from "react-jss";

const useStyles = createUseStyles({
  bar: {
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    gap: '6px',
    margin: '24px 0 0',
  },
  btn: {
    width: '34px',
    height: '34px',
    border: '1px solid #c3c3c3',
    background: '#ffffff',
    color: '#191919',
    borderRadius: '3px',
    cursor: 'pointer',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    fontSize: '14px',
    fontWeight: 600,
    padding: 0,
    '&:hover:not(:disabled)': {
      background: '#f2f2f2',
    },
    '&:disabled': {
      cursor: 'default',
      opacity: 0.35,
    },
  },
  pageText: {
    fontSize: '14px',
    color: '#191919',
    textAlign: 'center',
  },
});

/**
 * @param {{page: number; totalPages: number; onChange: (p: number) => void}} props
 */
const PaginationBar = ({ page, totalPages, onChange }) => {
  const s = useStyles();
  const atFirst = page <= 1;
  const atLast = page >= totalPages;

  return <div className={s.bar}>
    <button className={s.btn} disabled={atFirst} onClick={() => onChange(1)} aria-label='First page'>
      <span className='icon-first-page' aria-hidden='true' />
    </button>
    <button className={s.btn} disabled={atFirst} onClick={() => onChange(page - 1)} aria-label='Previous page'>
      <span className='icon-previous-page' aria-hidden='true' />
    </button>
    <span className={s.pageText}>{page} / {totalPages}</span>
    <button className={s.btn} disabled={atLast} onClick={() => onChange(page + 1)} aria-label='Next page'>
      <span className='icon-next-page' aria-hidden='true' />
    </button>
    <button className={s.btn} disabled={atLast} onClick={() => onChange(totalPages)} aria-label='Last page'>
      <span className='icon-last-page' aria-hidden='true' />
    </button>
  </div>;
};

export default PaginationBar;
