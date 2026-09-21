import { createContainer } from "unstated-next";
import { useEffect, useState } from "react";
import { searchUsers } from "../../../services/users";
import { multiGetPresence } from "../../../services/presence";
import { getPrimaryGroup } from "../../../services/groups";
import getFlag from "../../../lib/getFlag";

const pagesize = 12;

const SearchUsersStore = createContainer(() => {
  const [keyword, setKeyword] = useState(/** @type {string|null} */(null));
  const [page, setPage] = useState(1);
  const [data, setData] = useState(/** @type {any} */(null));
  const [locked, setLocked] = useState(true);
  const [presence, setPresence] = useState(/** @type {Record<string, any>} */({}));
  const [primaryGroups, setPrimaryGroups] = useState(/** @type {Record<string, any>} */({}));

  useEffect(() => {
    if (!keyword && !getFlag('searchUsersAllowNullKeyword', false)) return;
    setLocked(true);
    setPresence({});
    setPrimaryGroups({});
    searchUsers({
      keyword,
      limit: pagesize,
      offset: (page - 1) * pagesize,
    }).then(d => {
      setData(d);
      const ids = (d.UserSearchResults || []).map(v => v.UserId);
      if (ids.length === 0) return;
      multiGetPresence({ userIds: ids }).then(presenceData => {
        const obj = {};
        for (const p of presenceData) obj[p.userId] = p;
        setPresence(obj);
      });
      ids.forEach(uid => {
        getPrimaryGroup({ userId: uid })
          .then(pg => {
            if (!pg || !pg.group) return;
            setPrimaryGroups(prev => ({ ...prev, [uid]: pg }));
          })
          .catch(() => {});
      });
    }).finally(() => {
      setLocked(false);
    });
  }, [keyword, page]);

  return {
    keyword, setKeyword,
    page, setPage,
    data, setData,
    presence,
    primaryGroups,
    locked, setLocked,
    pageSize: pagesize,
  };
});

export default SearchUsersStore;
