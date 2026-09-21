import { useEffect } from "react";
import { createUseStyles } from "react-jss";
import searchUsersStore from "../stores/searchUsersStore";
import InputRow from "./inputRow";
import UsersGrid from "./usersGrid";

const useStyles = createUseStyles({
  row: {
    background: 'none',
    minHeight: '100vh',
    padding: 0,
  },
});

/**
 * @param {{keyword?: string}} props
 */
const Container = props => {
  const store = searchUsersStore.useContainer();
  const s = useStyles();

  useEffect(() => {
    store.setKeyword(props.keyword || '');
    store.setPage(1);
    store.setData(null);
  }, [props.keyword]);

  return <div className={'flex ' + s.row}>
    <div className='col-12'>
      <InputRow />
      <UsersGrid />
    </div>
  </div>;
};

export default Container;
