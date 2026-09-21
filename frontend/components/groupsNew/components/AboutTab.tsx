import {createUseStyles} from 'react-jss';
import {useState, useRef} from 'react';
import Section from "./Section";
import GroupsPageStore from "../stores/GroupsPageStore";
import {GroupUserWithRoleIdThumbnail, setStatus} from "../../../services/groups-typed";
import NewLink from "../../NewLink";
import {ThumbnailFromState} from "../../AvatarEditorPage/components/avatarCardList";
import CreatorLink from "../../creatorLink";
import dayjs from "../../../lib/dayjs";
import PlayerHeadshot from "../../playerHeadshot";
import useButtonStyles from "../../../styles/buttonStyles";
import ActionButton from "../../actionButton";
import {getTheme, themeType } from "../../../services/theme";
import {wait } from "../../../lib/utils";
import Selector from "../../selector";
import { abbreviateNumber } from "../../../lib/numberUtils";
import Dropdown2016, { DropdownOption } from "../../dropdown2016";
import FeedbackStore from "../../../stores/feedback";
import { FeedbackType } from "../../../models/feedback";

const useStyles = createUseStyles({
    description: {
        color: "var(--text-color-primary)",
        fontSize: 16,
        wordWrap: "break-word",
        fontFamily: "HCo Gotham SSm,Helvetica Neue,Helvetica,Arial,Lucida Grande,sans-serif",
        fontWeight: 400,
        lineHeight: "1.4em",
        whiteSpace: "pre-wrap",
        //paddingBottom: 12,
        textRendering: "auto",
        textAlign: 'start',
    },
    memberContainer: {
        gap: 5,
        flexWrap: 'wrap',
        "@media (max-width: 767px)": {
            flexWrap: 'nowrap',
        },
    },
    memberWrapper: {
        float: "left",
        width: "calc(11.1111111% - 5px)",
        height: "120px",
        display: 'flex',
        listStyle: "none",
        "@media (max-width: 767px)": {
            minWidth: 95,
        },
    },
    memberLink: {
        maxWidth: "90px",
        margin: "auto",
        textDecoration: 'none',
        display: 'flex',
        flexDirection: 'column',
        flexWrap: 'nowrap',
        width: '100%',
        gap: 3,
    },
    avatarContainer: {
        boxShadow: '0 1px 4px 0 rgba(25,25,25,0.3)',
        transition: "box-shadow 200ms ease",
        borderRadius: "50%",
        backgroundColor: "#d1d1d1",
        overflow: "hidden",
        maxHeight: '90px',
        maxWidth: '90px',
        display: 'inline-block',
        aspectRatio: '1 / 1',
        "& img": {
            objectFit: "cover",
            background: 'none',
            verticalAlign: "bottom",
        },
        "&:hover": {
            boxShadow: "0 1px 6px 0 rgba(25, 25, 25, 0.75)",
        },
    },
    userName: {
        margin: "3px 0 0",
        lineHeight: "1.867em",
        textOverflow: "ellipsis",
        fontWeight: 500,
    },

    shoutContainer: { position: 'relative' },
    shoutDropdown: { position: 'absolute', top: 0, right: 6, display: 'flex' },
    shoutInfoContainer: {},
    shoutHeadshotContainer: {
        width: 48,
        height: 48,
        borderRadius: '50%',
        padding: 0,
        boxShadow: 'rgba(25, 25, 25, 0.3) 0px 1px 4px 0px',
        transition: 'box-shadow 200ms ease-in-out',
        overflow: 'hidden',
        backgroundColor: 'var(--text-color-secondary)',
        '&:hover': {
            boxShadow: 'rgba(25, 25, 25, 0.75) 0px 1px 6px 0px',
        },
    },
    shoutInfo: {
        marginLeft: 12,
        width: 'calc(100% - 48px - 12px)',
        '& div': {
            marginTop: 12,
            width: '100%',
            color: 'var(--text-color-secondary)',
            lineHeight: '1.4em',
            fontSize: 12,
            fontWeight: 400,
            textAlign: 'start',
        },
    },
    shoutBody: {
        marginTop: 6,
        marginBottom: 0,
        color: "var(--text-color-primary)",
        fontSize: 16,
        wordWrap: "break-word",
        fontFamily: "HCo Gotham SSm,Helvetica Neue,Helvetica,Arial,Lucida Grande,sans-serif",
        fontWeight: 400,
        lineHeight: "1.4em",
        whiteSpace: "pre-wrap",
        textRendering: "auto",
        textAlign: 'start',
        width: '100%',
    },
    shoutPostContainer: {
        marginTop: 12,
        width: '100%',
        "@media (max-width: 545px)": {
            flexDirection: 'column',
        }
    },
    shoutInputContainer: {
        marginBottom: 0,
        flexGrow: 1,
        "@media (max-width: 545px)": {
            width: '100%',
        }
    },
    shoutInput: {
        width: '100%',
        height: 38,
        lineHeight: '1.6em',
        resize: 'none',
        borderRadius: 3,
        border: '1px solid var(--text-color-secondary)',
        padding: '5px 12px',
        fontSize: 16,
        fontWeight: 400,
        '-webkit-appearance': 'none',
        paddingRight: 30,
        '&:focus': {
            boxShadow: 'none',
            borderColor: 'var(--primary-color)',
            outline: 0,
        }
    },
    shoutRemainingChar: {
        '& span': {
            fontSize: 10,
            fontWeight: 400,
            color: 'var(--text-color-secondary)',
            lineHeight: '1.5em',
        },
    },
    shoutSubmitBtnCnt: {
        "@media (max-width: 545px)": {
            width: '100%',
        }
    },
    shoutSubmitBtn: {
        fontSize: 18,
        fontWeight: 500,
        lineHeight: '100%',
        margin: '0 0 0 12px',
        padding: 9,
        "@media (max-width: 545px)": {
            margin: '0 auto',
            width: '100%',
        }
    },

    controlContainer: {
        display: 'flex',
        "@media (max-width: 545px)": {},
    },
    pageControls: {
        display: 'flex',
        justifyContent: 'center',
        gap: 10,
        alignItems: 'center',
    },
    paginationBtn: {
        aspectRatio: '1 / 1',
        padding: 3,
        display: 'flex',
        borderColor: p => p.theme !== themeType.dark ? 'var(--text-color-secondary)' : 'transparent!important',
        '& span': {
            backgroundSize: '48px auto',
            height: 24,
            width: 24,
            backgroundImage: "url(/img/generic_03112016.svg)",
            backgroundRepeat: "no-repeat",
            display: 'inline-block',
            verticalAlign: 'middle',
            filter: p => p.theme === themeType.dark ? 'invert(1)' : 'none',
        },
        "&.disabled": {
            filter: p => p.theme === themeType.dark ? 'invert(1)' : 'none',
        },
    },
    pages: {
        color: p => p?.theme === themeType?.obc2019 ? "var(--white-color)" : "var(--text-color-primary)"
    },
    backIcon: {
        backgroundPosition:'0 -360px!important',
    },
    forwardIcon: {
        backgroundPosition:'0 -336px!important',
    },

    roleSelector: {
        marginLeft: 9,
        "@media (max-width: 545px)": {
            marginLeft: 'auto',
        },
    },
    selectorWrapper: {
        width: 230,
        "@media (max-width: 545px)": {
            width: 'auto',
        },
    },
    selector: {
        padding: "5px 12px",
        fontWeight: 500,
        borderRadius: 3,
        borderColor: 'var(--text-color-secondary)',
        lineHeight: '26px',
        fontSize: 16,
        '& > span': {
            lineHeight: '26px',
        },
    },
    selectorOption: {
        width: '100%',
        display: 'flex',
        fontWeight: 400,
        padding: '10px 12px',
        lineHeight: '1.42857',
    },
    roleName: {
        maxWidth: 'calc(100% - 70px)',
        width: '100%',
        display: 'inline-block',
        "@media (max-width: 767px)": {
            minWidth: 70,
        }
    },
    roleCount: {
        marginLeft: 'auto',
    },
    memberHeader: {
        '& h3': {
            marginRight: 'auto',
            color: p => p?.theme === themeType?.obc2019 ? "var(--white-color)" : "var(--text-color-primary)",
            "@media (max-width: 545px)": {
                margin: 0,
            },
        },
        "@media (max-width: 545px)": {
            flexDirection: 'column',
        },
    },
    memberSection: {
        "@media (max-width: 767px)": {
            overflowX: 'auto',
        }
    },
    headerContainerThemed: {
        "& h3": {
            color: p => p?.theme === themeType?.obc2019 ? "var(--white-color)" : "var(--text-color-primary)"
        },
    },
    spinnerContainer: {},
});

// have: shout, description, games (should have nothing), members, social links
const AboutTab = ({}: {}) => {
    const s = useStyles({theme: getTheme()});
    const buttonStyles = useButtonStyles();
    const store = GroupsPageStore.useContainer();
    const {group, userPerms, members} = GroupsPageStore.useContainer();
    const feedback = FeedbackStore.useContainer();
    const deb = useRef(false);

    const textAreaRef = useRef(null);
    const [textAreaRemainingChar, setTextAreaRemainingChar] = useState(0);

    const DropdownOptions: DropdownOption[] = [
        {
            name: 'Report Abuse',
            url: '/internal/report-abuse',
        },
    ]

    return <div>
        {
            userPerms && userPerms.permissions.groupPostsPermissions.viewStatus && group.shout ?
                <Section header={"Shout"} headerContainer={s.headerContainerThemed}>
                    <div className={`${s.shoutContainer} section-content noShadow`}>
                        <div className={`${s.shoutInfoContainer} flex`}>
                            <NewLink className={`${s.shoutHeadshotContainer}`}
                                     href={'/users/' + group.shout.poster.userId + '/profile'}>
                                <PlayerHeadshot id={group.shout.poster.userId} name={group.shout.poster.username}/>
                            </NewLink>
                            <div className={`${s.shoutInfo} flex flex-column align-items-start`}>
                                <CreatorLink type={'User'} id={group.shout.poster.userId}
                                             name={group.shout.poster.displayName}/>
                                <span className={s.shoutBody}>{group.shout.body}</span>
                                <div>{dayjs(group.shout.updated).format('MMM D, YYYY | h:mm A')}</div>
                            </div>
                        </div>
                        {userPerms && userPerms.permissions.groupPostsPermissions.postToStatus ?
                            <div className={`${s.shoutPostContainer} flex align-items-start`}>
                                <div className={`${s.shoutInputContainer} flex flex-column align-items-end`}>
                                    <input onChange={() => {
                                        setTextAreaRemainingChar(textAreaRef?.current?.value?.length)
                                    }} className={s.shoutInput} placeholder="Enter your shout!" maxLength={255}
                                           ref={textAreaRef}/>
                                    <div className={`${s.shoutRemainingChar}`}>
                                        <span>{textAreaRemainingChar}/255</span>
                                    </div>
                                </div>
                                <ActionButton
                                    label='Group Shout'
                                    buttonStyle={buttonStyles.newContinueButton}
                                    className={s.shoutSubmitBtn}
                                    divClassName={s.shoutSubmitBtnCnt}
                                    onClick={async () => {
                                        try {
                                            await setStatus({ groupId: store.group?.id, message: textAreaRef?.current?.value });
                                            feedback.addFeedback(`Success`, FeedbackType.SUCCESS, true);
                                            await wait(3);
                                            window.location.reload();
                                        } catch (e) {
                                            console.error(e);
                                            feedback.addFeedback(`Could not set shout: ${e?.message}`, FeedbackType.ERROR, true);
                                        }
                                    }}
                                />
                            </div> : null}
                        <div className={`${s.shoutDropdown}`}>
                            <Dropdown2016 options={DropdownOptions} />
                        </div>
                    </div>
                </Section> : null
        }
        <Section header={"Description"} headerContainer={s.headerContainerThemed} contentSectioned={true}>
            <pre className={`${s.description} w-100 m-0 overflow-hidden `}>{store.group.description}</pre>
        </Section>
        <Section header={"Members"} headerContainer={s.memberHeader} contentSectioned={true} className={`${members.members.length === 0 || !group.roles && !store.memberDeb.current ? "disabled" : ""} ${s.memberSection}`}
                 headerChildren={group.roles ? <div className={s.controlContainer}>
                     <div className={`${s.pageControls}`}>
                         <ActionButton
                             className={`${s.paginationBtn} ${(members?.members?.length === 0 || members?.prevPage == null) ? 'disabled' : ''}`}
                             buttonStyle={(members?.members?.length === 0 || members?.prevPage == null) ? buttonStyles.newDisabledCancelButton : buttonStyles.newCancelButton}
                             onClick={async e => {
                                 e.preventDefault();
                                 if (deb.current || store.isLoading || members?.prevPage == null) {
                                     return
                                 }
                                 deb.current = true
                                 await store.fetchMembers(members.rank, members.page-1, members.nextPage);
                                 deb.current = false
                             }}
                         >
                             <span className={s.backIcon}/>
                         </ActionButton>
                         <span className={s.pages}>
                            Page {members?.page === undefined || members?.page === null ? "N/A" : members?.page}
                         </span>
                         <ActionButton
                             className={`${s.paginationBtn} ${(members?.members?.length === 0 || members?.nextPage == null) ? 'disabled' : ''}`}
                             buttonStyle={(members?.members?.length === 0 || members?.nextPage == null) ? buttonStyles.newDisabledCancelButton : buttonStyles.newCancelButton}
                             onClick={async e => {
                                 e.preventDefault();
                                 if (deb.current || store.isLoading || members?.nextPage == null) {
                                     return
                                 }
                                 deb.current = true
                                 await store.fetchMembers(members.rank, members.page+1, members.nextPage);
                                 deb.current = false
                             }}
                         >
                             <span className={s.forwardIcon}/>
                         </ActionButton>
                     </div>
                     {/*just use selector class*/}
                     <div className={`${s.roleSelector}`}>
                         <Selector
                             shadow={true}
                             options={group.roles
                                 .filter(a => a.rank !== 0)
                                 .sort((a, b) => a.rank - b.rank)
                                 .map(a => ({
                                 name: a.name,
                                 value: a.id,
                                 children: <>
                                     <span className={`${s.roleName} text-overflow`}>{a.name}</span>
                                     <span className={s.roleCount}>({abbreviateNumber(a.memberCount)})</span>
                                 </>,
                             }))}
                             onChange={async (rank: {name: string; value: number;}) => {
                                 if (store.memberDeb.current || store.isLoading || members.rank === rank.value) return false;
                                 store.fetchMembers(rank.value, 1, null);
                                 // try {
                                 //     deb.current = true;
                                 //     await store.fetchMembers(rank.value, 1, null);
                                 //     //await wait(0.75);
                                 //     deb.current = false;
                                 //     return true;
                                 // } catch {
                                 //     deb.current = false;
                                 //     return false;
                                 // }
                             }}
                             wrapperClass={s.selectorWrapper}
                             selectorOptionClass={s.selectorOption}
                             className={s.selector}
                         />
                     </div>
                 </div> : null}
        >
            {!group.roles ? "There was an error loading group roles. Try again later." :
                store.memberDeb.current ? <div className={`${s.spinnerContainer}`}>
                        <span className="spinner" style={{backgroundSize: "auto 36px"}}/>
                    </div> :
                members.members.length === 0 ? "This role has no members." :
                    <ul className={`${s.memberContainer} flex margin-none padding-none`}>
                {members.members.map(m => (
                    <MemberItem key={m.userId} member={m}/>
                ))}
            </ul>}
        </Section>
        {/*<Section header={"Social Links"} contentSectioned={true}>*/}
        {/*    <pre className={`${s.description} w-100 m-0 overflow-hidden `}>{store.group.description}</pre>*/}
        {/*</Section>*/}
    </div>
};

const MemberItem = ({member}: { member: GroupUserWithRoleIdThumbnail }) => {
    const s = useStyles({theme: getTheme()});
    // NOTE: newlink is an example of how to solve jsdoc-ts issues
    return <li className={`${s.memberWrapper}`}>
        <NewLink href={`/users/${member.userId}/profile`} className={s.memberLink}>
            <span className={`${s.avatarContainer} border-0 text-center`}>
                <img
                    className={`rounded-circle w-100 h-100`}
                    alt={`Headshot of ${member.displayName}'s Avatar`}
                    src={ThumbnailFromState(member.imageUrl, member.state)}
                />
            </span>
            <span
                className={`${s.userName} link2019 overflow-hidden font-size-12 text-center text-nowrap text-decoration-none`}>{member.displayName}</span>
        </NewLink>
    </li>
}

export default AboutTab;