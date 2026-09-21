import * as groups from "./groups";
import {ThumbnailState} from "./thumbnailsT";
import {GroupBasic, GroupPermissionsEntry, GroupRoleEntry, GroupUserWithRoleId, UserGroupV2} from "./groups-typed";

export declare const getInfo: ({groupId: number}) => Promise<GroupWithShout>;
export declare const getRoles: ({groupId: number}) => Promise<{groupId: number; roles: GroupRoleEntry[];}>;
export declare const getWall: ({groupId:number,cursor:any,sort:any,limit:any}) => Promise<AveriaCollectionPaginated<GroupPostEntry>>;
export declare const getMembers = groups.getMembers as (
    args: { groupId: number; cursor?: any; limit?: any; sortOrder?: any }
) => Promise<AveriaCollectionPaginated<GroupUser>>;
export declare const getRolesetMembers = groups.getMembers as (
    args: { groupId: number; roleSetId: number; cursor?: string; limit?: number; sortOrder?: string }
) => Promise<AveriaCollectionPaginated<GroupUserWithRoleId>>
export function getPermissionsForRoleset(args: { groupId: number; rolesetId: number }): Promise<GroupPermissionsEntry>;

export function getUserGroupsV2(args: { userId: number }): Promise<UserGroupV2>;
export function getUserGroups(args: { userId: number }): Promise<any>;
export function joinGroup(args: { groupId: number }): Promise<any>;
export function leaveGroup(args: { groupId: number; userId: number }): Promise<any>;
export function setStatus(args: { groupId: number; message: string }): Promise<any>;
export function createGroup(args: { name: string; description: string; iconElement: HTMLInputElement }): Promise<any>;
export function getRolesetMembers(args: { groupId: number; roleSetId: number; cursor?: string; limit?: number; sortOrder?: string }): Promise<any>;
export function postToWall(args: { groupId: number; content: string }): Promise<any>;
export function deletePost(args: { groupId: number; postId: number }): Promise<any>;
export function claimGroupOwnership(args: { groupId: number }): Promise<any>;
export function setGroupAsPrimary(args: { groupId: number }): Promise<any>;
export function removePrimaryGroup(): Promise<any>;
export function getPrimaryGroup(args: { userId: number }): Promise<any>;
export function setUserRole(args: { groupId: number; userId: number; roleId: number }): Promise<any>;
export function setGroupIcon(args: { groupId: number; icon: File }): Promise<any>;
export function setGroupDescription(args: { groupId: number; description: string }): Promise<any>;
export function getGroupSettings(args: { groupId: number }): Promise<any>;
export function setGroupSettings(args: {
    groupId: number;
    isApprovalRequired: boolean;
    areEnemiesAllowed: boolean;
    areGroupFundsVisible: boolean;
    areGroupGamesVisible: boolean;
}): Promise<any>;
export function changeGroupOwner(args: { groupId: number; userId: number }): Promise<any>;
export function createRole(args: { groupId: number; name: string; description: string; rank: number }): Promise<any>;
export function editRole(args: { groupId: number; roleId: number; name: string; description: string; rank: number }): Promise<any>;
export function deleteRole(args: { groupId: number; roleId: number }): Promise<any>;
export function setRolePermissions(groupId: number, roleId: number, permissions: string[]): Promise<any>;
export function oneTimePayout(args: { groupId: number; userId: number; amount: number }): Promise<any>;
export function getGroupInfo(args: { groupId: number }): Promise<any>;
export function getGroupAuditLog(args: { groupId: number; cursor?: string; userId?: number; action?: string }): Promise<any>;
export function searchGroups(args: { keyword?: string; limit?: number; offset?: number }): Promise<any>;

export const rawGroups: any;
