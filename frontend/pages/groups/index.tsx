import Head from 'next/head';
import Theme2016 from '../../components/theme2016';
import GroupsPageStore from "../../components/groupsNew/stores/GroupsPageStore";
import GroupPreProcessor from "../../components/groupsNew/GroupPreProcessor";
import React from "react";
import MyGroupsStore from "../../components/myGroups/stores/myGroupsStore";
import { getGroupPagesStyle } from "../../services/theme";
import GroupPageStore from "../../components/myGroups/stores/groupPageStore";
import MyGroups from "../../components/myGroups";
import UserGroupsStore from "../../components/groupsNew/stores/UserGroupsStore";

const GamePage = ({}: {}) => {
    if (getGroupPagesStyle() !== 'Modern') return <MyGroupsStore.Provider>
        <GroupPageStore.Provider>
            <MyGroups id={null}/>
        </GroupPageStore.Provider>
    </MyGroupsStore.Provider>;

    return (
        <>
            <Head>
                <title>Averia Groups</title>
                <meta property="og:title" content='Averia Groups' />
                <meta property="og:url" content={`https://averia.lol/groups`} />
                <meta property="og:type" content="profile" />
                <meta name="twitter:card" content="summary_large_image" />
                <meta name="og:site_name" content="Averia" />
                <meta name="theme-color" content="#E2231A" />
            </Head>
            <Theme2016>
                <UserGroupsStore.Provider>
                    <GroupsPageStore.Provider>
                        <GroupPreProcessor group={null} loadDefault={true} />
                    </GroupsPageStore.Provider>
                </UserGroupsStore.Provider>
            </Theme2016>
        </>
    );
}

export default GamePage;
