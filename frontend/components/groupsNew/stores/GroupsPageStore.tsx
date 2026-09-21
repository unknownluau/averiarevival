import {createContainer} from "unstated-next";
import {useEffect, useRef, useState} from "react";
import {
    getPermissionsForRoleset,
    getRolesetMembers,
    getWall,
    GroupPermissionsEntry,
    GroupPostEntry,
    GroupRoleEntry,
    GroupUserWithRoleIdThumbnail,
    GroupWithShout
} from "../../../services/groups-typed";
import AuthenticationStore from "../../../stores/authentication";
import {wait} from "../../../lib/utils";
import {getRoles} from "../../../services/groups";
import {ThumbnailEntry} from "../../../services/thumbnailsT";
import {multiGetAssetThumbnails, multiGetGroupIcons, multiGetUserHeadshots} from "../../../services/thumbnails";
import {getRobuxGroup} from "../../../services/economy";
import {getAssetDetailsClean, searchCatalog2} from "../../../services/catalog";
import {CatalogCategory, CatalogSortBy} from "../../CatalogPage/stores/CatalogPageStore";
import {userOwnsItems} from "../../../services/inventory";
import UserGroupsStore from "./UserGroupsStore";

const emptyGroupPermissions = () => ({
    groupPostsPermissions: {
        viewWall: false,
        postToWall: false,
        deleteFromWall: false,
        viewStatus: false,
        postToStatus: false,
    },
    groupMembershipPermissions: {
        changeRank: false,
        inviteMembers: false,
        removeMembers: false,
    },
    groupManagementPermissions: {
        manageRelationships: false,
        manageClan: false,
        viewAuditLogs: false,
    },
    groupEconomyPermissions: {
        spendGroupFunds: false,
        advertiseGroup: false,
        createItems: false,
        manageItems: false,
        addGroupPlaces: false,
        manageGroupGames: false,
        viewGroupPayouts: false,
    },
});

const GroupsPageStore = createContainer(() => {
    const userGroupStore = UserGroupsStore.useContainer();
    const [group, setGroup] = useState<GroupFull|null>(null);
    const [groupNotFound, setGroupNotFound] = useState(false);
    const [posts, setPosts] = useState<GroupPosts>({posts: [], page: 0, nextPage: null, prevPage: null});
    const [members, setMembers] = useState<GroupMembers>({members: [], rank: 0, page: 0, nextPage: null, prevPage: null});
    const [memberCache, setMemberCache] = useState<GroupMembers[]>([]);
    const [postCache, setPostCache] = useState<GroupPosts[]>([]);

    const [userPerms, setUserPerms] = useState<GroupPermissionsEntry|null>(null);

    const [storeItems, setStoreItems] = useState<GroupStoreItems>({items: [], page: 0, total: 0, nextPage: null, prevPage: null});
    const [storeItemsCache, setStoreItemsCache] = useState<GroupStoreItems[]>([]);
    const sdeb = useRef(false);
    const fetchRequestRef = useRef(0);

    // TODO: should be in the other one, ill setup later

    const [isLoading, setLoading] = useState(false);
    const [isLoadingNE, setLoadingNE] = useState(false); // ne = non-essential, like role perms and user stuff

    const auth = AuthenticationStore.useContainer();

    useEffect(() => {
        if (!group) return;
        setUserPerms(null);
        let userGroup = userGroupStore?.userGroups?.find(g => g.group.id === group?.id);
        if (!userGroup) return;

        if (userGroup.role.rank !== 255) {
            setUserPerms({
                role: userGroup.role,
                permissions: emptyGroupPermissions(),
                areGroupGamesVisible: false,
                areGroupFundsVisible: false,
                areEnemiesAllowed: false,
                canConfigure: false,
            });
            return;
        }

        let cancelled = false;
        (async () => {
            try {
                let req: GroupPermissionsEntry = await getPermissionsForRoleset({ groupId: group.id, rolesetId: userGroup.role.id });
                if (cancelled || !req) return;
                setUserPerms(req);
                if (req.areGroupFundsVisible || req.permissions.groupEconomyPermissions.spendGroupFunds) {
                    try {
                        let funds = await getRobuxGroup({groupId: group.id});
                        if (!cancelled) {
                            setGroup(current => current?.id === group.id ? {...current, funds: funds ?? null} : current);
                        }
                    } catch (e) {}
                }
            } catch (e) {}
        })()

        return () => {
            cancelled = true;
        };
    }, [userGroupStore?.userGroups, group?.id]);

    async function fetchData(group: GroupWithShout, clearData?: boolean) {
        if (auth.isPending) return;
        const requestId = ++fetchRequestRef.current;
        const isCurrent = () => fetchRequestRef.current === requestId;
        await setLoading(true);
        await setGroupNotFound(false);

        if (clearData) {
            // reset everything
            await setGroup(null);
            await setUserPerms(null);
            await setPosts({posts: [], page: 0, nextPage: null, prevPage: null});
            await setMembers({members: [], rank: 0, page: 0, nextPage: null, prevPage: null});
            await setStoreItems({items: [], page: 0, total: 0, nextPage: null, prevPage: null});
            await setMemberCache([]);
            await setStoreItemsCache([]);
            await setPostCache([]);
        }

        if (group.isLocked) {
            setTimeout(() => { if (isCurrent()) setLoading(false) }, 1000);
            await setLoadingNE(true);

            await wait(1);
            if (!isCurrent()) return;
            setLoadingNE(false);
            setGroup({
                ...group,
                icon: null,
                roles: null,
                funds: null,
                games: []
            });
            return;
        }

        let groupIcon: ThumbnailEntry|null = null;
        try {
            // @ts-ignore
            groupIcon = (await multiGetGroupIcons({ groupIds: [group.id] }))[0];
            if (!isCurrent()) return;
        } catch (e) { console.error(e) }
        let groupRoles: GroupRoleEntry[] = [];
        try {
            groupRoles = await getRoles({ groupId: group.id }); // might be null
            if (!isCurrent()) return;
        } catch (e) { console.error(e) }
        try {
            let req = (await getWall({ groupId: group.id, sort: 'Desc', limit: 10, cursor: null}));
            if (!isCurrent()) return;
            if (req) {
                let post = {
                    posts: req.data,
                    page: 1,
                    nextPage: req.nextPageCursor,
                    prevPage: req.previousPageCursor,
                };
                setPosts(post);
                if (clearData) {
                    setPostCache([post]);
                } else {
                    setPostCache([...postCache, post]);
                }
            }
        } catch (e) { console.error(e) }
        try {
            if (!groupRoles || groupRoles.length <= 0 || groupRoles.filter(v=>v.id>1).length <= 0) throw new Error("no roles to process group members");
            let rankId = groupRoles.filter(v => v.rank > 0)[0]?.id;
            if (rankId === undefined) throw new Error("no default rank found for group members")
            let req = (await getRolesetMembers({ groupId: group.id, roleSetId: rankId, sortOrder: 'Desc', limit: 9, cursor: null}));
            if (!isCurrent()) return;
            if (req && req.data) {
                // @ts-ignore
                let memberThumbs = await multiGetUserHeadshots({userIds: req.data.map(v => v.userId)}) ?? [];
                if (!isCurrent()) return;
                let members = {
                    members: req.data.map(v => {
                        let thumb = memberThumbs.find(d => d.targetId === v.userId);
                        return {
                            ...v,
                            imageUrl: thumb?.imageUrl ?? null,
                            state: thumb?.state ?? null,
                        }
                    }),
                    rank: rankId,
                    page: 1,
                    nextPage: req.nextPageCursor,
                    prevPage: req.previousPageCursor,
                };
                setMembers(members);
                if (clearData) {
                    setMemberCache([members]);
                } else {
                    setMemberCache([...memberCache, members]);
                }
            }
        } catch (e) { console.error(e) }
        if (!isCurrent()) return;

        setGroup({
            ...group,
            icon: groupIcon ?? null,
            roles: groupRoles ?? null,
            funds: null,
            games: []
        });

        setTimeout(() => { if (isCurrent()) setLoading(false) }, 1000);
        await setLoadingNE(true);

        await wait(1);
        if (!isCurrent()) return;
        setLoadingNE(false);
    }

    const memberDeb = useRef(false);

    async function fetchMembers(rank: number, page: number, cursor: string) {
        if (memberDeb.current) return;
        memberDeb.current = true;
        await setMembers({members: [], rank: 0, page: 0, nextPage: null, prevPage: null});
        try {
            if (!group.roles || group.roles.length <= 0 || group.roles.filter(v=>v.id>1).length <= 0) throw new Error("no roles to process group members");
            let memberCached = memberCache.find(mc => mc.rank === rank && mc.page === page);
            if (memberCached) {
                setMembers(memberCached);
                return;
            }

            let req = (await getRolesetMembers({ groupId: group.id, roleSetId: rank, sortOrder: 'Desc', limit: 9, cursor: cursor}));
            if (req && req.data) {
                // @ts-ignore
                let memberThumbs = await multiGetUserHeadshots({userIds: req.data.map(v => v.userId)}) ?? [];
                let members = {
                    members: req.data.map((v: { userId: number; }) => {
                        let thumb = memberThumbs.find(d => d.targetId === v.userId);
                        return {
                            ...v,
                            imageUrl: thumb?.imageUrl ?? null,
                            state: thumb?.state ?? null,
                        }
                    }),
                    rank: rank,
                    page: page,
                    nextPage: req.nextPageCursor,
                    prevPage: req.previousPageCursor,
                };
                setMembers(members);
                setMemberCache([...memberCache, members]);
            } else {
                console.error("failed to fetch members for rank " + rank);
            }
        } catch (e) { console.error(e) } finally {
            memberDeb.current = false;
        }
    }

    async function fetchPosts(page: number, cursor: string) {
        try {
            let postCached = postCache.find(ps => ps.page === page);
            if (postCached) {
                setPosts(postCached);
                return;
            }

            let req = (await getWall({ groupId: group.id, sort: 'Desc', limit: 10, cursor: cursor}));
            if (req && req.data) {
                // @ts-ignore
                console.log('hi');
                console.dir(req.data);
                let currentPost = {
                    posts: req.data,
                    page: page,
                    nextPage: req.nextPageCursor,
                    prevPage: req.previousPageCursor,
                };
                setPosts(currentPost);
                setPostCache([...postCache, currentPost]);
            } else {
                console.error("failed to fetch posts for group " + group.id + " page " + page + " cursor " + cursor);
            }
        } catch (e) { console.error(e) }
    }

    async function fetchStoreItems(page: number, cursor: string) {
        try {
            if (!group?.id || sdeb.current) return;
            let storeItemsCached = storeItemsCache.find(gsi => gsi.page === page);
            if (storeItemsCached) {
                setStoreItems(storeItemsCached);
                return;
            }

            try {
                let success = await loadItems(page, cursor);
                console.log("loading success: " + success);
            } catch (e) {
                console.error("failed to load store items for group " + group.id);
                throw e;
            }
        } catch (e) { console.error(e) }
    }

    async function loadItems(page: number, cursor: string) {
        if (sdeb.current) return false;
        sdeb.current = true;
        // @ts-ignore
        const searchResultsFlat = await searchCatalog2({
            category: CatalogCategory.All,
            sort: CatalogSortBy.RecentlyUpdated,
            creatorType: 2,
            creatorId: group.id,
            limit: 24,
            cursor: cursor,
        });
        let newResult: GroupStoreItems = {
            items: [],
            page: page,
            total: searchResultsFlat._total,
            nextPage: searchResultsFlat.nextPageCursor,
            prevPage: searchResultsFlat.previousPageCursor,
        }
        if (searchResultsFlat.data.length === 0) {
            setStoreItems(newResult);
            setStoreItemsCache([...storeItemsCache, newResult]);
            await wait(0.75);
            sdeb.current = false;
            return false;
        }

        const searchResultsRaw = await getAssetDetailsClean(searchResultsFlat.data);
        if (!searchResultsRaw) { console.dir("Failed to load asset details from search results: " + searchResultsFlat); setStoreItems(newResult); await wait(0.75); sdeb.current = false; return false; }

        const thumbnails = await multiGetAssetThumbnails({ assetIds: searchResultsRaw.map(d => d.id) });
        // @ts-ignore
        const ownsAssets: { id: number; owned: boolean; }[] = auth?.isAuthenticated && auth?.userId ? await userOwnsItems({ userId: auth?.userId, assetIds: searchResultsRaw.map(d => d.id) }) : [];
        // @ts-ignore
        newResult.items = searchResultsRaw.map(d => {
            let thumb = thumbnails.find(t => t.targetId === d.id);
            let ownsAsset = ownsAssets.find(t => t.id === d.id);
            return {
                ...d,
                state: thumb?.state ?? null,
                imageUrl: thumb?.imageUrl ?? null,
                owned: ownsAsset?.owned ?? false,
            }
        });

        // newResult.items = Array(30).fill(null).map((_, index) => {
        //     return {
        //         ...newResult.items[0],
        //         name: `${newResult.items[0].name} ${index + 1}`
        //     };
        // });
        setStoreItems(newResult);
        setStoreItemsCache([...storeItemsCache, newResult]);
        sdeb.current = false;
        return true;
    }

    return {
        group, setGroup,
        groupNotFound, setGroupNotFound,
        isLoadingNE, setLoadingNE,
        posts, setPosts,
        members, setMembers,

        storeItems, setStoreItems,

        userPerms, setUserPerms,

        isLoading, setLoading,

        sdeb, memberDeb,

        fetchData,
        fetchMembers,
        fetchStoreItems,
        fetchPosts,
    }
});

export type GroupFull = GroupWithShout & {
    icon: ThumbnailEntry|null;
    roles: GroupRoleEntry[];
    games: null[];
    funds: {
        robux: number,
        tickets: number,
    }|null;
};

export type GroupPosts = {
    posts: GroupPostEntry[];
    page: number;
    nextPage: string|null;
    prevPage: string|null;
}

export type GroupMembers = {
    members: GroupUserWithRoleIdThumbnail[];
    rank: number;
    page: number;
    nextPage: string|null;
    prevPage: string|null;
}

export type GroupStoreItems = {
    items: CatalogAssetDetails[];
    page: number;
    total: number;
    nextPage: string|null;
    prevPage: string|null;
}

export type CatalogAssetDetails = {
    id: number;
    assetType: number;
    name: string;
    description: string;
    genres: string[];
    creatorType: "User" | "Group";
    creatorTargetId: number;
    creatorName: string;
    offsaleDeadline: string | null;
    itemRestrictions: ("Limited" | "LimitedUnique")[];
    saleCount: number;
    itemType: "Asset" | string;
    favoriteCount: number;
    isForSale: boolean;
    commentsEnabled: boolean;
    price: number | null;
    priceTickets: number | null;
    lowestPrice: number | null;
    priceStatus: string | null;
    lowestSellerData: {
        userId: number;
        username: string;
        userAssetId: number;
        price: number;
        assetId: number;
    } | null;
    unitsAvailableForConsumption: number | null;
    serialCount: number;
    is18Plus: boolean;
    moderationStatus: "ReviewApproved" | string;
    createdAt: string;
    updatedAt: string;
    state?: string;
    imageUrl?: string;
    owned?: boolean;
}

export default GroupsPageStore;
