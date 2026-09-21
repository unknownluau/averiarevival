import searchUsersStore from "../stores/searchGroupsStore";
import {createUseStyles} from "react-jss";
import GroupIcon from "../../groupIcon";
import dayjs from "../../../lib/dayjs";
import Link from "../../link";
import {abbreviateNumber} from "../../../lib/numberUtils";

const useStyles = createUseStyles({
    textRight: {
        textAlign: 'right',
    },
    status: {
        fontSize: '80px',
        position: 'relative',
        top: '-65px',
        display: 'block',
        height: 0,
    },
    groupName: {
        padding: '2px 0',
        fontSize: 14,
        fontWeight: 500,
    },
    groupDesc: {
        color: "var(--text-color-tertiary)",
        overflowWrap: 'break-word',
        overflowY: 'hidden',
        fontSize: 14,
        fontWeight: 400,
        height: 40,
        wordWrap: 'break-word',
        whiteSpace: 'pre-wrap',
    },
    groupWrapper: {
        background: 'var(--white-color)',
        transition: 'all 0.2s ease-in-out',
        marginBottom: 15,
        borderRadius: 4,
        overflow: 'hidden',
        boxShadow: '0 1px 4px 0 rgba(25,25,25,0.3)',
        cursor: 'pointer',
        "&:hover": {
            boxShadow: '0 1px 6px 0 rgba(25,25,25,0.75)',
        },
    },
    groupRow: {
        color: 'inherit',
    },
    groupInfoContainer: {
        display: 'flex',
        flexDirection: 'column',
        flex: 1,
        padding: '9px 0',
        paddingLeft: 9,
    },
    groupIconContainer: {
        width: 90,
        height: 90,
    },
    groups: {
        marginTop: 15,
    },
    groupMemberInfo: {
        "& > div": {
            margin: '0 25px',
            justifyContent: 'center',
            alignItems: 'center',
        },
    },
    memberCount: {
        gap: 2,
        fontWeight: 500,
    },
    memberVisibility: {
        "& > span": {
            fontWeight: 500,
            color: 'var(--success-color)',
        },
    },
});

const GroupsRow = () => {
    const store = searchUsersStore.useContainer();
    const s = useStyles();

    if (!store.data || !store.data.data)
        return null;

    return <div className={`flex ${s.groups}`}>
        <div className='col-12'>
            {
                store.data.data.length ? store.data.data.map(v => {
                    return <div className={s.groupRow} key={v.id}>
                        <Link href={`/My/Groups.aspx?gid=${v.id}`}>
                            <a href={`/My/Groups.aspx?gid=${v.id}`}>
                                <div className={`flex ${s.groupWrapper}`}>
                                    <div className={s.groupIconContainer}>
                                        <GroupIcon id={v.id}/>
                                    </div>
                                    <div className={s.groupInfoContainer}>
                                        <span className={`${s.groupName} text-overflow`}>{v.name}</span>
                                        <span className={`${s.groupDesc} text-overflow`}>{v?.description?.replace(/[\r\n]+/gm, " ")}</span>
                                    </div>
                                    <div className={`flex ${s.groupMemberInfo}`}>
                                        <div className={`flex ${s.memberCount}`}>
                                            <span className={`icon-nav-group`} />
                                            <span>{FormatMemberCount(v.memberCount)}</span>
                                        </div>
                                        <div className={`flex ${s.memberVisibility}`}>
                                            <span>Public</span>
                                        </div>
                                    </div>
                                </div>
                            </a>
                        </Link>
                    </div>
                }) : <div className={`section-content-off w-100`}>
                    No results for "{store.keyword}"
                </div>
            }
        </div>
    </div>
}

function FormatMemberCount(count) {
    if (count >= 10000) {
        return abbreviateNumber(count);
    } else if (count >= 1000) {
        return `${count.toString().slice(0, -3)},${count.toString().slice(-3)}`;
    }
    return count;
}

export default GroupsRow;