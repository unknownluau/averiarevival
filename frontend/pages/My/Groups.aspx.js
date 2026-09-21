import {useRouter} from "next/router";
import React, {useEffect} from "react";
import MyGroups from "../../components/myGroups";
import GroupPageStore from "../../components/myGroups/stores/groupPageStore";
import MyGroupsStore from "../../components/myGroups/stores/myGroupsStore";
import {getGroupPagesStyle} from "../../services/theme";

const MyGroupsPage = () => {
    const router = useRouter();

    useEffect(() => {
        if (getGroupPagesStyle() !== 'Legacy') {
            if (router.query['gid']) {
                router.push(`/groups/${router.query['gid']}/--`);
            } else {
                router.push(`/groups`);
            }
        }
    }, [router.query]);

    if (getGroupPagesStyle() !== 'Legacy') {
        return null;
    }

    return <MyGroupsStore.Provider>
        <GroupPageStore.Provider>
            <MyGroups id={router.query['gid']}/>
        </GroupPageStore.Provider>
    </MyGroupsStore.Provider>
}

export default MyGroupsPage;
