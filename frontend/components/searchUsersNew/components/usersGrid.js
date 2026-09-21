import { createUseStyles } from "react-jss";
import searchUsersStore from "../stores/searchUsersStore";
import UserCard from "./userCard";
import PaginationBar from "./paginationBar";

const useStyles = createUseStyles({
  resultsCount: {
    fontSize: '12px',
    fontWeight: 400,
    color: '#757575',
    margin: '8px 0 12px',
  },
  noResults: {
    fontSize: '14px',
    color: '#757575',
    padding: '12px 0',
  },
  list: {
    listStyle: 'none',
    padding: 0,
    margin: 0,
  },
});

const UsersGrid = () => {
  const store = searchUsersStore.useContainer();
  const s = useStyles();

  if (!store.data || !store.data.UserSearchResults) return null;

  const results = store.data.UserSearchResults;
  const total = store.data.TotalResults || 0;
  const start = (store.page - 1) * store.pageSize + 1;
  const end = Math.min(start + results.length - 1, total || start + results.length - 1);
  const pageCount = total ? Math.ceil(total / store.pageSize) : store.page;

  if (results.length === 0) {
    return <div className={s.noResults}>No results for "{store.keyword}"</div>;
  }

  return <div>
    <div className={s.resultsCount}>{start.toLocaleString()} - {end.toLocaleString()} of {total.toLocaleString()} Results</div>
    <div className='row'>
      {results.map(u => (
        <div className='col-12 col-md-6 col-lg-4' key={u.UserId}>
          <ul className={s.list}>
            <UserCard
              user={u}
              presence={store.presence[u.UserId]}
              primaryGroup={store.primaryGroups[u.UserId]}
            />
          </ul>
        </div>
      ))}
    </div>
    {pageCount > 1 && <PaginationBar
      page={store.page}
      totalPages={pageCount}
      onChange={p => store.setPage(Math.max(1, Math.min(pageCount, p)))}
    />}
  </div>;
};

export default UsersGrid;
