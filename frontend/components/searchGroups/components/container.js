import searchUsersStore from "../stores/searchGroupsStore";
import {useEffect} from "react";
import InputRow from "./inputRow";
import GroupsRow from "./groupsRow";
import {createUseStyles} from "react-jss";

const useStyles = createUseStyles({
    row: {
        background: 'none',
        minHeight: '100vh',
        padding: '0',
    }
})

const Container = props => {
    const store = searchUsersStore.useContainer();
    const s = useStyles();
    useEffect(() => {
        store.setKeyword(props.keyword);
        store.setData(null);
    }, [props]);

    return <div className={'flex ' + s.row}>
        <div className='col-12'>
            <InputRow/>
            <GroupsRow/>
        </div>
    </div>
}

export default Container;