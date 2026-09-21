import Dropdown2016, { DropdownOption } from "../../dropdown2016";
import GroupsPageStore from "../stores/GroupsPageStore";
import UserGroupsStore from "../stores/UserGroupsStore";
import AuthenticationStore from "../../../stores/authentication";
import FeedbackStore from "../../../stores/feedback";
import { FeedbackType } from "../../../models/feedback";
import {
    GroupPermissionsApiResponse,
    GroupPermissionsEntry,
    leaveGroup,
    removePrimaryGroup,
    setGroupAsPrimary
} from "../../../services/groups-typed";
import { wait } from "../../../lib/utils";
import {useMemo, useState} from "react";
import LeaveGroupModal from "../modals/LeaveGroupModal";

const isAdmin = (p: GroupPermissionsApiResponse) => {
    if (!p) return false;
    return p.groupMembershipPermissions.changeRank || p.groupManagementPermissions.manageClan
        || p.groupManagementPermissions.viewAuditLogs || p.groupManagementPermissions.manageRelationships
        || p.groupMembershipPermissions.removeMembers || p.groupEconomyPermissions.spendGroupFunds
        || p.groupEconomyPermissions.advertiseGroup;
}

const GroupDropdown = ({}: {}) => {
    const auth = AuthenticationStore.useContainer();
    const feedback = FeedbackStore.useContainer();
    const store = GroupsPageStore.useContainer();
    const ustore = UserGroupsStore.useContainer();

    const [leaveOpen, setLeaveOpen] = useState(false);
    const primaryGroup = useMemo(() => ustore.GetPrimaryGroup(), [ustore]);

    // @ts-ignore
    const DropdownOptions: DropdownOption[] = useMemo(() => [
        store.group && isAdmin(store.userPerms?.permissions) ? {
            name: 'Configure Group',
            url: `/My/GroupAdmin.aspx?gid=${store.group?.id}`,
        } : null,
        store.group && store.userPerms?.permissions?.groupManagementPermissions?.viewAuditLogs ? {
            name: 'Audit Logs',
            url: `/groups/Audit.aspx?groupid=${store.group?.id}`,
        } : null,
        store.group && store.userPerms?.permissions?.groupEconomyPermissions?.advertiseGroup ? {
            name: 'Advertise Group',
            url: `/My/CreateUserAd.aspx?targetId=${store.group?.id}&targetType=group`,
        } : null,
        store.group && store.userPerms != null && primaryGroup?.group?.id !== store.group?.id ? {
            name: 'Make Primary',
            onClick: async () => {
                try {
                    await setGroupAsPrimary({ groupId: store.group?.id });
                    feedback.addFeedback("Primary group has been set", FeedbackType.SUCCESS, true);
                    await wait(3);
                    window.location.reload();
                } catch (e) {
                    console.error(e);
                    feedback.addFeedback(`Could not set primary group: ${e?.message}`, FeedbackType.ERROR, true);
                }
            },
        } : null,
        store.group && store.userPerms != null && primaryGroup?.group.id === store.group?.id ? {
            name: 'Remove Primary',
            onClick: async () => {
                try {
                    await removePrimaryGroup();
                    feedback.addFeedback("Primary group has been removed", FeedbackType.SUCCESS, true);
                    await wait(3);
                    window.location.reload();
                } catch (e) {
                    console.error(e);
                    feedback.addFeedback(`Could not remove primary group: ${e?.message}`, FeedbackType.ERROR, true);
                }
            },
        } : null,
        store.group && store.userPerms && store.group.owner.userId !== auth.userId ? {
            name: 'Leave Group',
            onClick: () => setLeaveOpen(true),
        } : null,
        {
            name: 'Report Abuse',
            url: '/internal/report-abuse',
        },
    ], [store?.group, store?.userPerms, auth?.userId, feedback, primaryGroup]);

    return <>
        {leaveOpen && <LeaveGroupModal ExitFunction={() => setLeaveOpen(false)} />}
        <Dropdown2016 options={DropdownOptions} />
    </>
}

export default GroupDropdown;