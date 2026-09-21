import {createUseStyles} from "react-jss";
import GroupsPageStore from "./stores/GroupsPageStore";
import {useRef} from "react";
import GroupIcon from "../groupIcon";
import {ThumbnailFromState} from "../AvatarEditorPage/components/avatarCardList";
import CreatorLink from "../creatorLink";
import HorizontalTabs from "../horizontalTabs";
import AboutTab from "./components/AboutTab";
import StoreTab from "./components/StoreTab";
import {abbreviateNumber} from "../../lib/numberUtils";
import GroupDropdown from "./components/GroupDropdown";
import ActionButton from "../actionButton";
import useButtonStyles from "../../styles/buttonStyles";
import Section from "./components/Section";
import {deletePost, GroupPostEntry, joinGroup, postToWall} from "../../services/groups-typed";
import Dropdown2016, {DropdownOption} from "../dropdown2016";
import NewLink from "../NewLink";
import dayjs from "../../lib/dayjs";
import PlayerHeadshot from "../playerHeadshot";
import GamesTab from "./components/GamesTab";
import UserGroupsStore from "./stores/UserGroupsStore";
import AuthenticationStore from "../../stores/authentication";
import AdBanner from "../ad/adBanner";
import {wait} from "../../lib/utils";
import {FeedbackType} from "../../models/feedback";
import FeedbackStore from "../../stores/feedback";
import {getTheme, themeType } from "../../services/theme";
import {itemNameToEncodedName} from "../../services/catalog";

const useStyles = createUseStyles({
    groupDetailWrapper: {
        paddingLeft: 15,
        width: 'calc(100% - 15px - 160px)',
        "& > div": {
            width: '100%'
        },
        "@media (max-width: 767px)": {
            width: '100%',
            paddingLeft: 10,
            "& > div": {
                width: '100%',
            },
        }
    },
    groupHeaderContainer: {
        position: 'relative',
        "@media (max-width: 545px)": {
            flexDirection: 'column',
        }
    },
    groupImage: {
        width: 128,
        aspectRatio: '1 / 1',
        marginRight: 12,
        "@media (max-width: 767px)": {
            width: 90,
        },
        "@media (max-width: 545px)": {
            width: 128,
            margin: '0 auto',
            marginBottom: 12,
        },
    },
    groupInfoContainer: {
        width: 'calc(100% - 140px)',
        "@media (max-width: 545px)": {
            width: '100%',
        }
    },
    groupOwner: {
        margin: '5px 0',
        '& span': {
            color: 'var(--text-color-secondary)',
            marginRight: 5,
        },
        '& *': {
            fontWeight: 500,
        },
    },
    groupStatsContainer: {
        marginTop: 'auto',
        alignItems: 'center',
        width: '100%',
        "@media (max-width: 545px)": {
            justifyContent: 'center',
        }
    },
    groupStat: {
        padding: '0 12px',
        '& span': {
            color: 'var(--text-color-secondary)',
            fontWeight: 500,
            fontSize: 12,
        },
        '& h3': {
            fontSize: 12,
            fontWeight: 400,
            lineHeight: '1em',
            margin: '5px 0',
        },
    },
    groupHeaderDropdownContainer: {
        position: 'absolute',
        display: 'flex',
        top: 0,
        right: 6,
    },
    joinGroupBtnContainer: {
        marginLeft: 'auto',
    },
    joinGroupBtn: {
        padding: 9,
        fontWeight: 500,
    },
    tabContainer: {
        marginTop: 15,
    },
    wallContainer: {
        '& > div:not(:first-child)': {
            borderTop: '1px solid var(--background-color)',
        }
    },

    postContainer: {
        position: 'relative',
        padding: '12px 0',
        width: '100%',
        marginBottom: 0,
    },
    postHeadshotContainer: {
        display: 'inline-block',
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
    postInfo: {
        marginLeft: 12,
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
    postBody: {
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
    },
    wallPostContainer: {
        marginBottom: 12,
        "@media (max-width: 545px)": {
            flexDirection: 'column',
        }
    },
    wallPostBtnContainer: {
        alignContent: 'end',
        "@media (max-width: 545px)": {
            margin: '0 auto',
            marginTop: 8,
            width: '100%',
        }
    },
    wallPostBtn: {
        padding: '9px 24px',
        fontSize: 16,
        fontWeight: 500,
        marginLeft: 15,
        "@media (max-width: 545px)": {
            width: '100%',
            margin: '0 auto',
        },
    },
    wallPostText: {
        flexGrow: 1,
        borderRadius: 3,
        minHeight: 100,
        lineHeight: '1.6em',
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

    userGroupsWrapper: {
        flexDirection: 'column',
        maxWidth: 175,
        "@media (max-width: 767px)": {
            width: '100%',
            paddingLeft: 10,
            flexDirection: 'column-reverse',
            maxWidth: '100%',
        }
    },
    userGroupsContainer: {
        "@media (max-width: 767px)": {
            maxHeight: 'calc(5 * 52px)',
        }
    },
    createGroupBtn: {
        padding: 9,
        fontSize: 18,
        fontWeight: 500,
        lineHeight: '100%',
        borderRadius: 3,
        width: '100%',
        "@media (max-width: 767px)": {
            margin: '0 auto',
            marginBottom: 12,
        }
    },
    groupContainer: {
        padding: '10px 12px',
        position: 'relative',
        '&:hover': {
            boxShadow: 'inset 4px 0 0 0 var(--primary-color)',
            backgroundColor: 'var(--white-color-hover)',
        },
        '&.current': {
            boxShadow: 'inset 4px 0 0 0 var(--primary-color)',
        },
        '& span.primary': {
            top: 0,
            right: 0,
            width: 0,
            height: 0,
            position: 'absolute',
            borderTop: '20px solid var(--primary-color)',
            borderLeft: '20px solid transparent',
        },
    },
    groupNameContainer: {
        width: 'calc(100% - 37px)',
        '& span': {
            fontSize: 12,
            fontWeight: 500,
            overflow: 'hidden',
            width: '100%',
            display: 'inline-block',
        },
    },
    groupName: {
        display: 'flex',
        flexWrap: 'nowrap',
        alignItems: 'center',
        gap: 8,
        width: '100%',
        '& h1': {
            fontWeight: 800,
            lineHeight: '1em',
            fontSize: 32,
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            whiteSpace: 'nowrap',
            maxWidth: 'calc(100% - 40px)',
            padding: '5px 0',
            margin: 0,
            "@media (max-width: 545px)": {
                maxWidth: '100%',
            }
        },
        "@media (max-width: 545px)": {
            flexDirection: 'column',
        },
    },
    groupVerified: {
        width: 28,
    },
    userGroupImage: {
        width: 32,
        height: 32,
        display: 'inline-block',
    },
    header: {
        '& h3': {
            fontSize: 32,
            fontWeight: 800,
            lineHeight: '100%',
            color: p => p?.theme === themeType?.obc2019 ? "var(--white-color)" : "var(--text-color-primary)"
        },
        "@media (max-width: 767px)": {
            paddingLeft: 10,
        }
    },
    spinnerContainer: {
        height: '85vh',
    },
    banner: {
        marginBottom: 18,
    },

    contentContaining: {
        "@media (max-width: 767px)": {
            flexDirection: 'column',
        }
    },
    headerContainerThemed: {
        "& h3": {
            color: p => p?.theme === themeType?.obc2019 ? "var(--white-color)" : "var(--text-color-primary)"
        },
    },
    textThemed: {
        color: p => p?.theme === themeType?.obc2019 ? "var(--white-color)" : "var(--text-color-primary)"
    },

    pageControls: {
        display: 'flex',
        justifyContent: 'center',
        gap: 10,
        alignItems: 'center',
        paddingTop: 10,
    },
    paginationBtn: {
        aspectRatio: '1 / 1',
        padding: 3,
        display: 'flex',
        borderColor: p => p?.theme !== themeType.dark ? 'var(--text-color-secondary)' : 'transparent!important',
        '& span': {
            backgroundSize: '48px auto',
            height: 24,
            width: 24,
            backgroundImage: "url(/img/generic_03112016.svg)",
            backgroundRepeat: "no-repeat",
            display: 'inline-block',
            verticalAlign: 'middle',
            filter: p => p?.theme === themeType.dark ? 'invert(1)' : 'none',
        },
        "&.disabled": {
            filter: p => p?.theme === themeType.dark ? 'invert(1)' : 'none',
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
});

const GroupsPage = () => {
    const s = useStyles({theme: getTheme()});
    const auth = AuthenticationStore.useContainer();
    const buttonStyles = useButtonStyles();
    const store = GroupsPageStore.useContainer();
    const {group, posts, userPerms} = GroupsPageStore.useContainer();
    const {userGroups} = UserGroupsStore.useContainer();

    const feedback = FeedbackStore.useContainer();

    const textAreaRef = useRef(null);
    const postDeb = useRef(false);
    const pageDeb = useRef(false);
    const showGroupLoading = !store.group && (store.isLoading || !store.groupNotFound);

    return <div className={`container big padding-none`}>
        <AdBanner className={s.banner}/>
        <Section header="Groups" headerContainer={s.header} headerCenter={true} contentSectioned={false}
                 headerChildren={<>
                     <NewLink href={`/search/groups`}>
                         <span className={`link2018 fw-500`}>More Groups</span>
                     </NewLink>
                 </>} className={`flex ${s.contentContaining}`}>
            {
                !auth.isPending && auth.isAuthenticated ?
                    <div className={`${s.userGroupsWrapper} flex`}>
                        <ul className={`${s.userGroupsContainer} ${userGroups?.length === 0 ? "margin-none" : ""} section-content noShadow padding-none flex flex-column flex-nowrap overflow-x-hidden overflow-y-auto w-100`}>
                            {
                                userGroups?.map(ug => {
                                    if (!ug) return null;
                                    return <NewLink key={ug.group.id} href={`/groups/${ug.group.id}/${itemNameToEncodedName(ug.group.name)}`}
                                                    className={`${s.groupContainer} flex ${ug.group.id === store?.group?.id ? "current" : ""}`}
                                    >
                                        <div className={`${s.userGroupImage}`}>
                                            <GroupIcon url={ThumbnailFromState(ug?.imageUrl, ug?.state)}
                                                       id={ug.group.id}/>
                                        </div>
                                        <div
                                            className={`${s.groupNameContainer} padding-l-5 overflow-hidden text-start align-content-center`}>
                                            <span className={`text-overflow text-start`}>{ug.group.name}</span>
                                        </div>
                                        {
                                            ug?.isPrimary ? <span className='primary'/> : null
                                        }
                                    </NewLink>
                                })
                            }
                            {/*{*/}
                            {/*    userGroups.length === 0 ? <div className={`section-content-off noShadow`}></div> : null*/}
                            {/*}*/}
                        </ul>
                        <NewLink href='/My/CreateGroup.aspx'>
                            <ActionButton
                                label="Create Group"
                                buttonStyle={buttonStyles.newContinueButton}
                                className={s.createGroupBtn}
                            />
                        </NewLink>
                    </div> : null
            }
            {
                showGroupLoading ? <div className={`container ${s.spinnerContainer}`}>
                    <span className="spinner" style={{backgroundSize: "auto 36px"}}/>
                </div> : store.groupNotFound ?
                    <div className={s.textThemed} style={{marginLeft: 12, fontSize: 16, fontWeight: 500,}}>
                        This group does not exist.
                    </div> : group?.isLocked ?
                        <div className={s.textThemed} style={{marginLeft: 12, fontSize: 16, fontWeight: 500,}}>
                            This group is locked.
                        </div> :
                    <div
                        className={`${s.groupDetailWrapper} ${!auth.isPending && auth.isAuthenticated ? "" : "padding-none"} flex flex-column`}>
                        <div className={`${s.groupHeaderContainer} section-content noShadow flex`}>
                            <div className={`${s.groupImage}`}>
                                <GroupIcon name={group.name}
                                           url={ThumbnailFromState(group.icon.imageUrl, group.icon.state)}/>
                            </div>
                            <div className={`${s.groupInfoContainer} flex flex-column align-items-start`}>
                                <div className={`${s.groupName}`}>
                                    <h1>{group.name}</h1>
                                    {group.isVerified && <span className={`${s.groupVerified} icon-verified`} />}
                                </div>
                                <div className={`${s.groupOwner} flex align-items-center`}>
                                    <span>By</span>
                                    <CreatorLink type={'User'} id={group.owner.userId} name={group.owner.displayName}/>
                                </div>
                                <div className={`${s.groupStatsContainer} flex`}>
                                    <div className={`${s.groupStat}`}>
                                        <span>Members</span>
                                        <h3 title={group.memberCount.toString()}>{abbreviateNumber(group.memberCount)}</h3>
                                    </div>
                                    {userPerms && userPerms.role.id > 1 ? <div className={`${s.groupStat}`}>
                                        <span>Rank</span>
                                        <h3 title={userPerms.role.description}>{userPerms.role.name}</h3>
                                    </div> : <ActionButton
                                        className={s.joinGroupBtn}
                                        divClassName={s.joinGroupBtnContainer}
                                        buttonStyle={buttonStyles.newContinueButton}
                                        label='Join Group'
                                        onClick={async () => {
                                            try {
                                                await joinGroup({groupId: store.group?.id});
                                                feedback.addFeedback(`Joined ${store.group?.name}`, FeedbackType.SUCCESS, true);
                                                await wait(3);
                                                window.location.reload();
                                            } catch (e) {
                                                console.error(e);
                                                feedback.addFeedback(`Could not join group: ${e?.message}`, FeedbackType.ERROR, true);
                                            }
                                        }}
                                    />}
                                </div>
                            </div>
                            <div className={`${s.groupHeaderDropdownContainer}`}>
                                <GroupDropdown/>
                            </div>
                        </div>
                        <HorizontalTabs
                            options={[
                                {name: "About", element: <AboutTab/>},
                                {name: "Games", element: <GamesTab/>},
                                {name: "Store", element: <StoreTab/>},
                                // {name: "Affiliates", element: <AffiliatesTab />},
                            ]}
                            elementClass={`${s.tabContainer}`}
                        />
                        {
                            userPerms && userPerms.permissions.groupPostsPermissions.viewWall ? <Section header="Wall" headerContainer={s.headerContainerThemed}>
                                <div className={`${s.wallContainer} section-content noShadow`}>
                                    {userPerms.permissions.groupPostsPermissions.postToWall ?
                                        <div className={`${s.wallPostContainer} flex`}>
                            <textarea
                                className={s.wallPostText}
                                placeholder="Say something..."
                                maxLength={1000}
                                ref={textAreaRef}
                            />
                                            <ActionButton
                                                label="Post"
                                                className={s.wallPostBtn}
                                                divClassName={s.wallPostBtnContainer}
                                                buttonStyle={buttonStyles.newContinueButton}
                                                onClick={async () => {
                                                    if (postDeb.current) return;
                                                    postDeb.current = true;
                                                    try {
                                                        // TODO: make this just add to wall and not reload that can be annoying
                                                        const ptw = await postToWall({
                                                            groupId: store.group?.id,
                                                            content: textAreaRef?.current?.value
                                                        });
                                                        feedback.addFeedback(`Success`, FeedbackType.SUCCESS, true);
                                                        if (!ptw?.data) {
                                                            await wait(3);
                                                            window.location.reload();
                                                        }
                                                        store.setPosts(prevState => {
                                                            prevState.posts = [{
                                                                body: ptw?.data?.body,
                                                                id: ptw?.data?.id,
                                                                poster: {
                                                                    role: store?.userPerms?.role ?? null,
                                                                    user: ptw?.data?.poster,
                                                                },
                                                                created: ptw?.data?.created,
                                                                updated: ptw?.data?.updated,
                                                            }, ...prevState.posts];
                                                            return prevState;
                                                        })
                                                    } catch (e) {
                                                        console.error(e);
                                                        feedback.addFeedback(`Could not post to wall: ${e?.message}`, FeedbackType.ERROR, true);
                                                    }
                                                    await wait(3);
                                                    postDeb.current = false;
                                                }}
                                            />
                                        </div> : null}
                                    {posts.posts.length > 0 ? <div className={`${s.postsContainer}`}>
                                        {posts.posts.map(post => <GroupWallPost key={post.id} {...post} />)}
                                    </div> : <div className={`section-content-off noShadow`}>
                                        Nobody has said anything yet...
                                    </div>}
                                    <div className={`${s.pageControls}`}>
                                        <ActionButton
                                            className={`${s.paginationBtn} ${(posts?.posts?.length === 0 || posts?.prevPage == null) ? 'disabled' : ''}`}
                                            buttonStyle={(posts?.posts?.length === 0 || posts?.prevPage == null) ? buttonStyles.newDisabledCancelButton : buttonStyles.newCancelButton}
                                            onClick={async e => {
                                                e.preventDefault();
                                                if (pageDeb.current || store.isLoading || posts?.prevPage == null) {
                                                    return
                                                }
                                                pageDeb.current = true
                                                await store.fetchPosts((posts.page ?? 0)-1, posts.nextPage);
                                                pageDeb.current = false
                                            }}
                                        >
                                            <span className={s.backIcon}/>
                                        </ActionButton>
                                        <span className={s.pages}>
                                            Page {posts?.page === undefined || posts?.page === null ? "N/A" : posts?.page}
                                        </span>
                                        <ActionButton
                                            className={`${s.paginationBtn} ${(posts?.posts?.length === 0 || posts?.nextPage == null) ? 'disabled' : ''}`}
                                            buttonStyle={(posts?.posts?.length === 0 || posts?.nextPage == null) ? buttonStyles.newDisabledCancelButton : buttonStyles.newCancelButton}
                                            onClick={async e => {
                                                e.preventDefault();
                                                if (pageDeb.current || store.isLoading || posts?.nextPage == null) {
                                                    return
                                                }
                                                pageDeb.current = true
                                                await store.fetchPosts((posts?.page ?? 0)+1, posts?.nextPage);
                                                pageDeb.current = false
                                            }}
                                        >
                                            <span className={s.forwardIcon}/>
                                        </ActionButton>
                                    </div>
                                </div>
                            </Section> : null
                        }
                    </div>
            }
        </Section>
    </div>
}

const GroupWallPost = (post: GroupPostEntry) => {
    const s = useStyles();
    const auth = AuthenticationStore.useContainer();
    const store = GroupsPageStore.useContainer();
    const feedback = FeedbackStore.useContainer();

    const DropdownOptions: DropdownOption[] = [
        store?.group && store.userPerms?.permissions?.groupPostsPermissions?.deleteFromWall || post.poster.user.userId === auth.userId ? {
            name: 'Delete',
            onClick: async () => {
                try {
                    await deletePost({groupId: store.group.id, postId: post.id});
                    feedback.addFeedback("Successfully deleted post!", FeedbackType.SUCCESS, true);
                    await wait(3);
                    window.location.reload();
                } catch (e) {
                    console.error(e);
                    feedback.addFeedback(`Could not delete post: ${e?.message}`, FeedbackType.ERROR, true);
                }
            },
        } : null,
        {
            name: 'Report Abuse',
            url: '/internal/report-abuse',
        },
    ]

    return <div className={`${s.postContainer} flex`}>
        <NewLink className={`${s.postHeadshotContainer}`} href={'/users/' + post.poster.user.userId + '/profile'}>
            <PlayerHeadshot id={post.poster.user.userId} name={post.poster.user.username}/>
        </NewLink>
        <div className={`${s.postInfo} flex flex-column align-items-start`}>
            <CreatorLink type={2} id={post.poster.user.userId}
                         name={post?.poster?.user?.displayName ?? post?.poster?.user?.username}/>
            <span className={s.postBody}>{post.body}</span>
            <div>{post.poster.role.name} | {dayjs(post.updated).format('MMM D, YYYY | h:mm A')}</div>
        </div>
        <div className={`${s.groupHeaderDropdownContainer} flex align`}>
            <Dropdown2016 options={DropdownOptions}/>
        </div>
    </div>
}

export default GroupsPage;
