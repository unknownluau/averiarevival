<script lang="ts">
    import client from "../../lib/request";
    import { hasPermission } from "../../stores/rank";


    export let userId: string;
	let quickConfigShouldDelete = false;
    let permissionToAdd: string;
    let permissions = [];
    let permissionsAvailable = [];
    let permissionSearch = '';
    let addPermissionSearch = '';
    const canSetPermissions = hasPermission('SetPermissions');
    const presets = [
        {
            label: 'Asset Moderator & Support Team',
            permissions: [
                'GetAssetModerationDetails',
                'SetGameIconModerationStatus',
                'GetPendingModerationItems',
                'GetPendingGroupIcons',
                'GetStats',
                'GetPendingModerationGameIcons',
                'SetAssetModerationStatus',
                'SetGroupIconModerationStatus',
                'GetDetailsFromThumbnail',
            ],
        },
        {
            label: 'Junior moderator',
            permissions: [
                'SetGameIconModerationStatus',
                'GetAllAssetComments',
                'GetPendingModerationGameIcons',
                'GetGroupStatus',
                'GetAllAssetOwners',
                'GetUserStatusHistory',
                'GetStats',
                'DeleteItem',
                'DeleteGroupWallPost',
                'GetPendingGroupIcons',
                'DeleteUsername',
                'SetAssetModerationStatus',
                'LockAndUnlockGroup',
                'GetPendingModerationItems',
                'ManageInvites',
                'GetAssetModerationDetails',
                'GetGroupWall',
                'DeleteComment',
            ],
        },
        {
            label: 'Administrator',
            permissions: [
                'DeleteUserBadge',
                'SetPermissions',
                'GiveUserRobux',
                'GetGroupManageInfo',
                'LockAccount',
                'TrackItem',
                'GetPendingModerationItems',
                'GetAllAssetOwners',
                'DeleteItem',
                'GetPendingModerationGameIcons',
                'GetAssetModerationDetails',
                'GetPendingGroupIcons',
                'SetAssetModerationStatus',
                'GetStats',
                'SetGameIconModerationStatus',
                'DeleteGroupWallPost',
                'DeleteUsername',
                'ManageInvites',
                'GetGroupStatus',
                'LockAndUnlockGroup',
                'DeleteComment',
                'GetAllAssetComments',
                'GetGroupWall',
                'GetUserStatusHistory',
                'ResetDescription',
                'ManageApplications',
                'GetUserJoinCount',
                'GetUsersList',
                'GetUserDetailed',
                'UnbanUser',
                'ResetAvatar',
                'RegenerateAvatar',
                'RunLottery',
                'DeleteUserStatus',
                'GetUserTransactions',
                'ResetUsername',
                'LockForumThread',
                'RefundAndDeleteFirstPartyAssetSale',
                'GetUsersOnline',
                'ManageFeatureFlags',
                'GetUsersInGame',
            ],
        },
    ];

    $: filteredPermissions = permissions.filter(p =>
        p.permission.toLowerCase().includes(permissionSearch.toLowerCase())
    );
    $: filteredPermissionsAvailable = permissionsAvailable.filter(p =>
        p.toLowerCase().includes(addPermissionSearch.toLowerCase())
    );

    if (canSetPermissions) {
        client.get('/staff/permissions?userId=' + userId).then(d => {
            permissions = d.data;
        });

        client.get('/staff/permissions/list').then(d => {
            permissionsAvailable = d.data;
        });
    }

	const quickConfig = (arr: string[]) => {
		let promises = [];
		for (const perm of arr) {
			if (permissions.find(a => a.permission === perm)) continue;
			promises.push(client.request({
				method: 'POST',
				url: '/staff/permissions/?userId=' + userId + "&permission=" + perm,
			}));
		}
		Promise.all(promises).then(() => {
			let newPermissions = [...permissions];
			for (const item of arr) {
				newPermissions.push({
					permission: item,
					userId: parseInt(userId, 10),
				});
			}
			permissions = newPermissions;
		}).catch(e => {
			alert(e.message);
		})
	}

	const quickConfigDelete = (arr: string[]) => {
		let promises = [];
		for (const perm of arr) {
			if (!permissions.find(a => a.permission === perm)) continue;
			promises.push(client.request({
				method: 'DELETE',
				url: '/staff/permissions/?userId=' + userId + "&permission=" + perm,
			}));
		}
		Promise.all(promises).then(() => {
			permissions = permissions.filter(item => !arr.includes(item.permission));
		}).catch(e => {
			alert(e.message);
		})
	}

</script>

{#if canSetPermissions}
<div class="row mt-4">
    <div class="col-12">
        <h3>Permissions</h3>
        <input class="form-control mb-2" type="text" placeholder="Search permissions..." bind:value={permissionSearch} />
        <table class="table">
            <thead>
                <tr>
                    <th>Permission</th>
                    <th>Action</th>
                </tr>
            </thead>
            <tbody>
                    {#each filteredPermissions as permission}
                        <tr>
                            <td>
                                <p class="mb-0 mt-2">{permission.permission}</p>
                            </td>
                            <td>
                                <button class="btn btn-outline-danger" on:click={() => {
                                    client.request({
                                        method: 'DELETE',
                                        url: '/staff/permissions?userId=' + userId + '&permission=' + permission.permission,
                                    }).then(() => {
                                        permissions = permissions.filter(v => v.permission !== permission.permission);
                                    }).catch(e => {
                                        alert(e.message);
                                    })
                                }}>Remove</button>
                            </td>
                        </tr>
                    {/each}
            </tbody>
        </table>

    </div>
        <div class="col-4">
            <h3>Add Permission</h3>
            <input class="form-control mb-2" type="text" placeholder="Search..." bind:value={addPermissionSearch} />
            <select class="form-control" bind:value={permissionToAdd}>
                {#each filteredPermissionsAvailable as permission}
                    <option value={permission}>{permission}</option>
                {/each}
            </select>
        </div>
        <div class="col-2">
            <button class="btn btn-primary mt-4" on:click={() => {
                if (permissions.find(a => a.permission === permissionToAdd)) return;
                client.request({
                    method: 'POST',
                    url: '/staff/permissions/?userId=' + userId + "&permission=" + permissionToAdd,
                }).then(() => {
                    permissions = [...permissions, {
                        permission: permissionToAdd,
                        userId: parseInt(userId, 10),
                    }];
                }).catch(e => {
                    alert(e.message);
                })
            }}>Add Permission</button>
        </div>
	<div class="col-12 mt-4">
		<h3>Quick Config</h3>
		<select class="form-control col-12" bind:value={quickConfigShouldDelete}>
			<option value={false}>Add</option>
			<option value={true}>Remove</option>
		</select>
        {#each presets as preset}
		<button class="btn btn-primary mt-4 ml-2" on:click={() => {
                if (quickConfigShouldDelete) {
					quickConfigDelete(preset.permissions);
                } else {
					quickConfig(preset.permissions);
                }
            }}>{preset.label}</button>
        {/each}
		<button class="btn btn-danger mt-4 ml-2" on:click={() => {
                quickConfigDelete(permissions.map(p => p.permission));
            }}>Remove All Permissions</button>

	</div>
</div>
{/if}
