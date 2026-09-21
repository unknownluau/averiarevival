import * as groups from "./groups";
import {ThumbnailState} from "./thumbnailsT";

export type GroupBasic = {
    id: number;
    name: string;
    memberCount: number;
    isVerified: boolean;
}

export type UserGroupV2 = {group: GroupBasic; role: GroupRoleEntry; isPrimary?: boolean; imageUrl?: string; state?: ThumbnailState;}

export type GroupWithShout = {
    id: number;
    name: string;
    description: string | null;
    memberCount: number;
    isVerified: boolean;
    isLocked: boolean;
    owner: GroupUser | null;
    shout: ShoutEntry | null;
}

export type GroupUser = {
    userId: number;
    username: string;
    displayName: string;
    /** @deprecated Not set anymore */
    buildersClubMembershipType: string;
}

export type GroupUserWithThumbnail = GroupUser & {
    imageUrl?: string;
    state?: ThumbnailState;
}

export type GroupUserWithRoleId = GroupUser & {roleId: number};

export type GroupUserWithRoleIdThumbnail = GroupUserWithRoleId & {
    imageUrl?: string;
    state?: ThumbnailState;
}

export type ShoutEntry = {
    poster: GroupUser;
    body: string;
    created: Date;
    updated: Date;
}

export type GroupRoleEntry = {
    id: number;
    groupId: number;
    name: string;
    description: string|null;
    rank: number;
    memberCount: number;
}

export type GroupPermissionsEntry = {
    role: GroupRoleEntry;
    permissions: GroupPermissionsApiResponse;
    areGroupGamesVisible: boolean;
    areGroupFundsVisible: boolean;
    areEnemiesAllowed: boolean;
    canConfigure: boolean;
}

export type GroupPermissionsApiResponse = {
    groupPostsPermissions: {
        viewWall: boolean;
        postToWall: boolean;
        deleteFromWall: boolean;
        viewStatus: boolean;
        postToStatus: boolean;
    };
    groupMembershipPermissions: {
        changeRank: boolean;
        inviteMembers: boolean;
        removeMembers: boolean;
    };
    groupManagementPermissions: {
        manageRelationships: boolean;
        manageClan: boolean;
        viewAuditLogs: boolean;
    };
    groupEconomyPermissions: {
        spendGroupFunds: boolean;
        advertiseGroup: boolean;
        createItems: boolean;
        manageItems: boolean;
        addGroupPlaces: boolean;
        manageGroupGames: boolean;
        viewGroupPayouts: boolean;
    };
}

export type GroupPostEntry = {
    id: number;
    poster: {
        user: GroupUser;
        role: GroupRoleEntry;
    };
    body: string;
    created: Date;
    updated: Date;
}

export const getUserGroupsV2 = groups.getUserGroupsV2;
export const getUserGroups = groups.getUserGroups;
export const getPermissionsForRoleset = groups.getPermissionsForRoleset;
export const joinGroup = groups.joinGroup;
export const leaveGroup = groups.leaveGroup;
export const setStatus = groups.setStatus;
export const createGroup = groups.createGroup;
export const getRoles = groups.getRoles;
export const getMembers = groups.getMembers;
export const getRolesetMembers = groups.getRolesetMembers;
export const getWall = groups.getWall;
export const postToWall = groups.postToWall;
export const deletePost = groups.deletePost;
export const getInfo = groups.getInfo;
export const claimGroupOwnership = groups.claimGroupOwnership;
export const setGroupAsPrimary = groups.setGroupAsPrimary;
export const removePrimaryGroup = groups.removePrimaryGroup;
export const getPrimaryGroup = groups.getPrimaryGroup;
export const setUserRole = groups.setUserRole;
export const setGroupIcon = groups.setGroupIcon;
export const setGroupDescription = groups.setGroupDescription;
export const getGroupSettings = groups.getGroupSettings;
export const setGroupSettings = groups.setGroupSettings;
export const changeGroupOwner = groups.changeGroupOwner;
export const createRole = groups.createRole;
export const editRole = groups.editRole;
export const deleteRole = groups.deleteRole;
export const setRolePermissions = groups.setRolePermissions;
export const oneTimePayout = groups.oneTimePayout;
export const getGroupInfo = groups.getGroupInfo;
export const getGroupAuditLog = groups.getGroupAuditLog;
export const searchGroups = groups.searchGroups;

export const rawGroups = groups;
