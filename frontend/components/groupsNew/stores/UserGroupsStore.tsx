import {createContainer} from "unstated-next";
import {useState} from "react";
import {getPrimaryGroup, getUserGroupsV2, UserGroupV2} from "../../../services/groups-typed";
import AuthenticationStore from "../../../stores/authentication";
import { multiGetGroupIcons } from "../../../services/thumbnails";

const GroupsPageStore = createContainer(() => {
    const [userGroups, setUserGroups] = useState<UserGroupV2[]>([]);
    const auth = AuthenticationStore.useContainer();

    const fetchData = async () => {
        if (auth.isAuthenticated && auth.userId) {
            try {
                let groups = await getUserGroupsV2({userId: auth.userId});
                if (groups) {
                    // @ts-ignore
                    let groupThumbs = await multiGetGroupIcons({groupIds: groups.map(v => v.group.id)}) ?? [];
                    groups = groups.map(g => {
                        let thumb = groupThumbs.find(t => t.targetId === g.group.id);
                        return {...g, imageUrl: thumb?.imageUrl ?? null, state: thumb?.state ?? null}
                    });
                }
                let primaryGroup = await getPrimaryGroup({userId: auth.userId});
                if (groups && primaryGroup && primaryGroup?.group?.id) {
                    groups = groups.map(g => g.group.id === primaryGroup.group.id ? {...g, isPrimary: true} : g)
                }
                if (groups) {
                    groups = groups.toSorted((a, b) =>
                        (Number(b?.isPrimary) - Number(a?.isPrimary)) ||
                        (Number(b.role.rank === 255) - Number(a.role.rank === 255)) ||
                        a.group.name.localeCompare(b.group.name)
                    );
                }
                setUserGroups(groups || []);
                return groups || [];
            } catch (e) { console.error(e) }
        }
        return null;
    }

    const GetPrimaryGroup = () => {
        if (!userGroups || userGroups.length === 0) return null;
        return userGroups.find(g => g.isPrimary);
    }

    return {
        userGroups, setUserGroups,

        fetchData,
        GetPrimaryGroup,
    }
});

export default GroupsPageStore;